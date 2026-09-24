using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8HRealTimeCombatValidation
{
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string CombatSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
    private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private const int OnfKos = 0x00000100;
    private const int OcfAnimal = 0x00008000;
    private static readonly ArcanumObjectId BowIdentity = ParseIdentity(BowKey);
    private static readonly ArcanumObjectId ArrowIdentity = ParseIdentity(ArrowKey);
    private static readonly ArcanumObjectId BearIdentity = ParseIdentity(BearKey);
    private static readonly ArcanumObjectId DynamicIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("88888888-aaaa-4aaa-8aaa-888888888888"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M8H/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8H harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        WorldMapSessionCoordinator session = loader.Session;
        GameObject dynamicObject = null;
        string baselineJson = null;
        Application.logMessageReceived += Track;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "authentic bow and arrow sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = RequireState(session, BowIdentity, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = RequireState(session, ArrowIdentity, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, BowIdentity, WornLocation.Weapon).Succeeded,
                "authentic Bow equips through production inventory authority");
            Check(session.SelectSector(CombatSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds");
            Check(session.TryGetLoadedObject(BearIdentity, out WorldObject bearRuntime),
                "authentic Polar Bear Cub presentation is loaded");
            WorldObject pcRuntime = lifecycle.Presentation;
            baselineJson = session.SaveGames.SerializeCurrentSession();

            FindOneStepMeleeApproach(loader.NavigationMap, bearRuntime.Tile,
                out Vector2Int movementOrigin, out Vector2Int meleeTile);
            MoveActor(session, loader, pc, pcRuntime, movementOrigin, controlled: true);
            CombatStateService combat = session.Combat;
            var boundaries = new List<CombatRoundBoundary>();
            combat.RoundCompleted += boundaries.Add;
            Check(combat.StartCombat(pc, BearIdentity, CombatMode.RealTime).Succeeded,
                "production PC enters real-time combat with the authentic hostile");
            Check(combat.Mode == CombatMode.RealTime && combat.CurrentParticipant.IsNull
                  && combat.CurrentActionPoints == 0 && combat.Participants.Any(value => value.Identity == pc)
                  && combat.Participants.Any(value => value.Identity == BearIdentity),
                "real-time combat reuses the authoritative roster without turn AP ownership");

            Check(combat.ScheduleRealTimeMove(pc, meleeTile, pcAlwaysRun: true).Succeeded,
                "production PC movement schedules against source ART timing");
            Check(combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState movement)
                  && movement.HasPendingAction && movement.PendingAction.Kind == CombatRealTimeActionKind.Move
                  && movement.PendingAction.ReadyAtMilliseconds > combat.ElapsedCombatTimeMilliseconds,
                "movement exposes deterministic start and completion time");
            long movementDelay = movement.PendingAction.EffectAtMilliseconds
                                 - combat.ElapsedCombatTimeMilliseconds;
            if (movementDelay > 0)
            {
                Check(combat.AdvanceRealTime(checked((int)movementDelay - 1)).Succeeded,
                    "authoritative source time advances to one millisecond before movement completion");
                Check(pcRuntime.Tile == movementOrigin, "movement does not commit before source completion");
                Check(combat.AdvanceRealTime(1).Succeeded, "movement reaches exact source completion");
            }
            else
            {
                Check(combat.AdvanceRealTime(0).Succeeded, "zero-action-frame movement resolves deterministically");
            }
            Check(pcRuntime.Tile == meleeTile
                  && combat.LastRealTimeActionResolution?.MoveResult?.ActionPointsSpent == 0,
                "movement commits once through navigation authority without turn-based AP spending");
            CheckBoundaries(combat, boundaries);

            Check(session.UnequipItem(pc, WornLocation.Weapon).Succeeded,
                "Bow unequips for the bounded unarmed melee proof");
            combat.SetRandomSource(new SequenceRandom(100, 100));
            Check(combat.ScheduleRealTimeAttack(new CombatAttackRequest(pc, BearIdentity,
                CombatAttackMode.BasicMelee)).Succeeded, "unarmed melee schedules while the PC is ready");
            Check(combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState melee)
                  && melee.HasPendingAction && melee.PendingAction.EffectAtMilliseconds
                  <= melee.PendingAction.ReadyAtMilliseconds,
                "melee action exposes source effect and recovery times");
            Check(combat.ScheduleRealTimeAttack(new CombatAttackRequest(pc, BearIdentity)).Failure
                  == CombatFailure.ActorBusy, "PC cannot attack again during source recovery");
            AdvanceToEffect(combat, melee.PendingAction);
            CombatAttackResult meleeResult = combat.LastAttackResult.Value;
            Check(meleeResult.Succeeded && meleeResult.Request.Mode == CombatAttackMode.BasicMelee
                  && meleeResult.ActionPointCost == CombatStateService.UnarmedAttackActionPointCost
                  && meleeResult.ActionPointsSpent == 0,
                "real-time melee executes once through the existing structured attack kernel");
            Check(combat.ScheduleRealTimeAttack(new CombatAttackRequest(pc, BearIdentity)).Failure
                  == CombatFailure.ActorBusy, "melee remains unavailable until source recovery completes");
            AdvanceToReady(combat, pc);
            Check(combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState readyAfterMelee)
                  && readyAfterMelee.IsReady, "PC becomes ready exactly at melee recovery completion");

            Check(session.EquipItem(pc, BowIdentity, WornLocation.Weapon).Succeeded,
                "authentic Bow re-equips for the ranged proof");
            Vector2Int rangedTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3);
            MoveActor(session, loader, pc, pcRuntime, rangedTile, controlled: true);
            int ammoBefore = arrows.StackQuantity.Value;
            CombatHitChance rangedChance = combat.GetBasicRangedHitChance(pc, BearIdentity,
                bow.WeaponData, InteractionRangeRules.Distance(rangedTile, bearRuntime.Tile));
            combat.SetRandomSource(rangedChance.DodgeChance > 0
                ? new SequenceRandom(1, 100, 100, 10, 5)
                : new SequenceRandom(1, 100, 10, 5));
            var rangedRequest = new CombatAttackRequest(pc, BearIdentity,
                CombatAttackMode.BasicRanged, CombatCalledLocation.Torso);
            Check(combat.ScheduleRealTimeAttack(rangedRequest).Succeeded,
                "authentic Bow attack schedules through the real-time command path");
            Check(combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState ranged)
                  && ranged.PendingAction.Kind == CombatRealTimeActionKind.RangedAttack,
                "Bow timing uses a distinct inspectable ranged action");

            long beforeRebuild = combat.ElapsedCombatTimeMilliseconds;
            CombatRealTimeActionState pendingBeforeRebuild = ranged.PendingAction;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(combat.ElapsedCombatTimeMilliseconds == beforeRebuild
                      && combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState rebuilt)
                      && rebuilt.PendingAction.EffectAtMilliseconds == pendingBeforeRebuild.EffectAtMilliseconds
                      && rebuilt.PendingAction.ReadyAtMilliseconds == pendingBeforeRebuild.ReadyAtMilliseconds,
                    $"{mode} presentation rebuild preserves authoritative real-time scheduling");
            }

            AdvanceToEffect(combat, pendingBeforeRebuild);
            CombatAttackResult rangedResult = combat.LastAttackResult.Value;
            Check(rangedResult.Succeeded && rangedResult.Hit
                  && rangedResult.Request.Equals(rangedRequest)
                  && rangedResult.WeaponIdentity == BowIdentity && rangedResult.AmmoIdentity == ArrowIdentity
                  && rangedResult.AmmoQuantityBefore == ammoBefore
                  && rangedResult.AmmoQuantityAfter == ammoBefore - 1
                  && rangedResult.ActionPointsSpent == 0
                  && rangedResult.FinalEffectiveAttackValue == rangedResult.Chance.AttackChance,
                "real-time Bow reuses M8C/M8G ammo, ledger, hit and vitality authority exactly once");
            AdvanceToReady(combat, pc);
            CheckBoundaries(combat, boundaries);

            Vector2Int dynamicTile = FindNearWalkable(loader.NavigationMap, pcRuntime.Tile,
                bearRuntime.Tile);
            WorldObject dynamicActor = AddValidationNpc(session, loader, dynamicTile);
            dynamicObject = dynamicActor.gameObject;
            Check(combat.EngageParticipant(DynamicIdentity).Succeeded
                  && combat.EngageParticipant(DynamicIdentity).Failure == CombatFailure.AlreadyRegistered,
                "runtime hostile engagement enrolls exactly once during real-time combat");
            Check(combat.Participants.Count(value => value.Identity == DynamicIdentity) == 1
                  && combat.TryGetRealTimeActorState(DynamicIdentity, out CombatRealTimeActorState dynamicState)
                  && dynamicState.IsReady,
                "dynamic engagement creates one ready scheduler state without a turn owner");

            int priorBoundaries = boundaries.Count;
            int untilBoundary = (int)(CombatStateService.RoundBoundaryMilliseconds
                                      - combat.ElapsedCombatTimeMilliseconds
                                      % CombatStateService.RoundBoundaryMilliseconds);
            Check(combat.AdvanceRealTime(untilBoundary).Succeeded, "clock reaches the next exact boundary");
            Check(combat.AdvanceRealTime(2000).Succeeded, "one hitch crosses two more exact boundaries");
            Check(boundaries.Count == priorBoundaries + 3, "one exact crossing plus a 2,000 ms hitch emits three boundaries");
            CheckBoundaries(combat, boundaries);

            MoveActor(session, loader, pc, pcRuntime, rangedTile, controlled: true);
            session.Vitality.ApplyHitPointDamage(BearIdentity,
                session.Vitality.GetCurrentHitPoints(BearIdentity) - 1);
            int xpBefore = session.Progression.GetExperience(pc);
            rangedChance = combat.GetBasicRangedHitChance(pc, BearIdentity,
                bow.WeaponData, InteractionRangeRules.Distance(rangedTile, bearRuntime.Tile));
            combat.SetRandomSource(rangedChance.DodgeChance > 0
                ? new SequenceRandom(1, 100, 100, 10, 5)
                : new SequenceRandom(1, 100, 10, 5));
            Check(combat.ScheduleRealTimeAttack(new CombatAttackRequest(pc, BearIdentity,
                CombatAttackMode.BasicRanged)).Succeeded, "lethal Bow transaction schedules while ready");
            Check(combat.TryGetRealTimeActorState(pc, out CombatRealTimeActorState lethal),
                "lethal action exposes production source timing");
            AdvanceToEffect(combat, lethal.PendingAction);
            Check(session.Vitality.IsDead(BearIdentity)
                  && session.TryGetObjectState(BearIdentity, out PersistentObjectState corpse)
                  && corpse.DeathConsequencesProcessed
                  && !combat.Participants.Any(value => value.Identity == BearIdentity)
                  && !combat.TryGetRealTimeActorState(BearIdentity, out _)
                  && session.Progression.GetExperience(pc) > xpBefore,
                "lethal real-time attack reuses M8D/M8E and removes future target authority");
            int committedXp = session.Progression.GetExperience(pc);
            Check(session.DeathConsequences.Process(pc, BearIdentity).Failure
                  == DeathConsequenceFailure.AlreadyProcessed
                  && session.Progression.GetExperience(pc) == committedXp,
                "death consequences and XP cannot replay");

            Check(combat.RemoveParticipant(DynamicIdentity).Succeeded,
                "validation-only dynamic hostile leaves through roster authority");
            ArcanumObjectId[] discoveredHostiles = combat.Participants
                .Where(value => value.Identity != pc)
                .Select(value => value.Identity)
                .ToArray();
            foreach (ArcanumObjectId hostile in discoveredHostiles)
                Check(combat.RemoveParticipant(hostile).Succeeded,
                    "round-boundary-discovered hostile leaves through roster authority");
            Check(combat.EndCombat(pc).Succeeded && !combat.IsActive
                  && combat.Mode == CombatMode.TurnBased && combat.ElapsedCombatTimeMilliseconds == 0
                  && combat.Participants.Count == 0 && combat.LastRealTimeActionResolution == null,
                "real-time combat terminates into a clean noncombat state");

            string committedJson = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(committedJson).Succeeded, "Save V1 reload succeeds after committed death");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(!session.Combat.IsActive && session.Combat.LastAttackResult == null
                  && session.Combat.LastRealTimeActionResolution == null
                  && session.Vitality.IsDead(BearIdentity)
                  && session.TryGetObjectState(BearIdentity, out corpse)
                  && corpse.DeathConsequencesProcessed
                  && session.Progression.GetExperience(pc) == committedXp,
                "Save V1 drops transient real-time transactions while preserving death, world and XP consequences");

            Check(session.SaveGames.LoadJson(baselineJson).Succeeded,
                "pre-validation authoritative baseline restores");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(!session.Combat.IsActive && !session.Vitality.IsDead(BearIdentity),
                "validation cleanup restores the authentic target and noncombat state");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log("M8H PLAYMODE VALIDATION PASS: mode=RealTime; roster=production; "
                      + "clock=source-owned; movement=source-ART-timed; melee=kernel-reused; "
                      + "bow=kernel+ammo+ledger-reused; readiness=effect+recovery; "
                      + "dynamicEngagement=exactlyOnce; boundaries=exact+hitch-safe; "
                      + "presentation=Original->Enhanced->Original-independent; "
                      + "death=M8D/M8E-once; saveV1=transient-normalized+consequences-preserved; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (dynamicObject != null) Object.Destroy(dynamicObject);
            if (baselineJson != null && (session.Combat.IsActive || session.Vitality.IsDead(BearIdentity)))
                session.SaveGames.LoadJson(baselineJson);
            _running = false;
        }
    }

    private static void AdvanceToEffect(CombatStateService combat, CombatRealTimeActionState action)
    {
        long remaining = action.EffectAtMilliseconds - combat.ElapsedCombatTimeMilliseconds;
        Check(remaining >= 0 && remaining <= int.MaxValue, "effect time remains representable");
        Check(combat.AdvanceRealTime((int)remaining).Succeeded, "source clock reaches exact action effect time");
        Check(combat.LastRealTimeActionResolution?.Action.Actor == action.Actor,
            "scheduled effect resolves exactly once for its actor");
    }

    private static void AdvanceToReady(CombatStateService combat, ArcanumObjectId actor)
    {
        Check(combat.TryGetRealTimeActorState(actor, out CombatRealTimeActorState state),
            "actor scheduler state remains available through recovery");
        long remaining = state.ReadyAtMilliseconds - combat.ElapsedCombatTimeMilliseconds;
        Check(remaining >= 0 && remaining <= int.MaxValue, "recovery time remains representable");
        Check(combat.AdvanceRealTime((int)remaining).Succeeded, "source clock reaches exact recovery time");
    }

    private static void CheckBoundaries(CombatStateService combat,
        IReadOnlyList<CombatRoundBoundary> boundaries)
    {
        long expected = combat.ElapsedCombatTimeMilliseconds / CombatStateService.RoundBoundaryMilliseconds;
        Check(boundaries.Count == expected && combat.RoundNumber == expected + 1,
            "round hook count exactly matches authoritative combat time");
        for (int index = 0; index < boundaries.Count; index++)
            Check(boundaries[index].CompletedRoundNumber == index + 1
                  && boundaries[index].ElapsedMilliseconds == CombatStateService.RoundBoundaryMilliseconds
                  && boundaries[index].TotalElapsedMilliseconds
                  == (index + 1L) * CombatStateService.RoundBoundaryMilliseconds,
                "each real-time round boundary is exact and nonduplicated");
    }

    private static WorldObject AddValidationNpc(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, Vector2Int tile)
    {
        const int prototype = 28001;
        int[] stats = Stats();
        var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
            0x28100000u, 0, 0, oid: GuidBytes(DynamicIdentity));
        PersistentObjectState state = session.GetOrCreate(source, CombatSector,
            source.CurrentArtId.Value, false, false);
        session.Characters.GetOrCreateSourceCharacter(DynamicIdentity, ObjectType.Npc, prototype, stats, null);
        session.Progression.GetOrCreateSourceCharacter(DynamicIdentity, ObjectType.Npc, prototype,
            CharacterProgressionSource.Resolve(stats, null, null, null, null, null, OcfAnimal));
        session.DerivedStats.GetOrCreateSourceCharacter(DynamicIdentity, ObjectType.Npc, prototype,
            CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50,
                OnfKos, OcfAnimal));
        session.Vitality.GetOrCreateSourceCharacter(DynamicIdentity, ObjectType.Npc, prototype,
            CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0,
                null, 0, null, 0, null, 0));
        var go = new GameObject("M8H runtime hostile");
        WorldObject runtime = go.AddComponent<WorldObject>();
        runtime.Type = ObjectType.Npc;
        runtime.Tile = tile;
        runtime.TilePosition = tile;
        runtime.ArtId = 0x28100000u;
        runtime.Blocks = true;
        session.Bind(CombatSector, state, runtime);
        loader.NavigationMap.Register(runtime, 0);
        session.Combat.RegisterActorSource(new CombatActorSource(DynamicIdentity, ObjectType.Npc,
            prototype, CombatSector, 1, OnfKos, OcfAnimal, 0));
        return runtime;
    }

    private static void FindOneStepMeleeApproach(SectorNavigationMap map, Vector2Int target,
        out Vector2Int origin, out Vector2Int destination)
    {
        for (int destinationRotation = 0; destinationRotation < IsoProjection.DirDelta.Length;
             destinationRotation++)
        {
            Vector2Int candidateDestination = target + IsoProjection.DirDelta[destinationRotation];
            if (!map.IsWalkable(candidateDestination)) continue;
            for (int originRotation = 0; originRotation < IsoProjection.DirDelta.Length; originRotation++)
            {
                Vector2Int candidateOrigin = candidateDestination - IsoProjection.DirDelta[originRotation];
                if (candidateOrigin == target || !map.IsWalkable(candidateOrigin)
                    || !map.CanTraverse(candidateOrigin, originRotation)) continue;
                origin = candidateOrigin;
                destination = candidateDestination;
                return;
            }
        }
        throw new InvalidOperationException("M8H validation FAIL: no one-step melee approach exists.");
    }

    private static Vector2Int FindClearRangedTile(SectorNavigationMap map, Vector2Int target,
        int distance)
    {
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M8H validation FAIL: no clear ranged tile exists.");
    }

    private static Vector2Int FindNearWalkable(SectorNavigationMap map, Vector2Int pc,
        Vector2Int excluded)
    {
        for (int distance = 2; distance <= 6; distance++)
        for (int y = Math.Max(0, pc.y - distance); y <= Math.Min(63, pc.y + distance); y++)
        for (int x = Math.Max(0, pc.x - distance); x <= Math.Min(63, pc.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (candidate != excluded && InteractionRangeRules.Distance(candidate, pc) == distance
                && map.IsWalkable(candidate)) return candidate;
        }
        throw new InvalidOperationException("M8H validation FAIL: no dynamic-engagement tile exists.");
    }

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.Placement.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "source equipped item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(owner)).Succeeded,
                "authentic item transfers through containment authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile, bool controlled)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement accepts validation placement");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        if (controlled) loader.NavigationMap.SetControlledObject(runtime);
    }

    private static PersistentObjectState RequireState(WorldMapSessionCoordinator session,
        ArcanumObjectId identity, ObjectType type, int prototype)
    {
        Check(session.States.TryGetValue(identity, out PersistentObjectState state)
              && state.Type == type && state.PrototypeNumber == prototype,
            $"authentic {type} prototype {prototype} resolves by exact ObjectID");
        return state;
    }

    private static int[] Stats()
    {
        var result = new int[CharacterAttributeSet.SourceStatArrayCount];
        for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
        result[CharacterProgressionSource.LevelSourceSlot] = 1;
        result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
        result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
        return result;
    }

    private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

    private static byte[] GuidBytes(ArcanumObjectId identity)
    {
        string compact = identity.Key.Substring(2).Replace("_", string.Empty);
        var bytes = new byte[ArcanumObjectId.SerializedSize];
        bytes[0] = (byte)ArcanumObjectIdType.Guid;
        for (int index = 0; index < 16; index++)
            bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
        return bytes;
    }

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Refresh(out WorldObjectSectorLoader loader,
        out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null,
            "production TestTerrain composition remains available");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8H validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);

        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum,
                "deterministic RNG sample is inside source roll bounds");
            return value;
        }
    }
}
