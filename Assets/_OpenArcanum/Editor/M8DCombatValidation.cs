using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8DCombatValidation
{
    private const string EquipmentSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string CombatSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string BowKey = "G_1575DBCA_4990_C243_8184_524D51F7D533";
    private const string ArrowKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private static readonly ArcanumObjectId BowIdentity = ParseIdentity(BowKey);
    private static readonly ArcanumObjectId ArrowIdentity = ParseIdentity(ArrowKey);
    private static readonly ArcanumObjectId BearIdentity = ParseIdentity(BearKey);
    private static readonly ArcanumObjectId MissingIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M8D/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8D harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(EquipmentSector), "authentic bow/ammo sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

            ArcanumObjectId pc = session.PlayerState.Identity;
            PersistentObjectState bow = RequireState(session, BowIdentity, ObjectType.Weapon, 6055);
            PersistentObjectState arrows = RequireState(session, ArrowIdentity, ObjectType.Ammo, 7058);
            MoveOwnedItem(session, bow, pc);
            MoveOwnedItem(session, arrows, pc);
            Check(session.EquipItem(pc, bow.Identity, WornLocation.Weapon).Succeeded,
                "M3C equips the authentic bow on the production PC");
            Check(session.SelectSector(CombatSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC rebinds");

            PersistentObjectState bear = RequireState(session, BearIdentity, ObjectType.Npc, 28422);
            Check(session.TryGetLoadedObject(BearIdentity, out WorldObject bearRuntime),
                "authentic bear has one production presentation");
            WorldObject pcRuntime = lifecycle.Presentation;
            Vector2Int firingTile = FindClearRangedTile(loader.NavigationMap, bearRuntime.Tile, 3, 8);
            MoveActor(session, loader, pc, pcRuntime, firingTile, true);
            string livingJson = session.SaveGames.SerializeCurrentSession();
            Check(livingJson.Contains("\"version\": 1"), "baseline save uses V1");
            Relationship[] relationships = Relationships(session, BearIdentity);
            int worldObjects = Count<WorldObject>();

            int deathTransitions = 0;
            session.Vitality.Changed += change =>
            {
                if (change.Identity == BearIdentity && change.PreviousHitPoints > 0
                    && change.CurrentHitPoints <= 0) deathTransitions++;
            };
            StartPcTurn(session, pc);
            int apBeforeInvalid = session.Combat.CurrentActionPoints;
            int hpBeforeInvalid = session.Vitality.GetCurrentHitPoints(BearIdentity);
            Check(session.Combat.Attack(pc, MissingIdentity, CombatAttackMode.BasicRanged).Failure
                  == CombatFailure.ParticipantNotRegistered, "invalid target fails transactionally");
            Check(session.Combat.CurrentActionPoints == apBeforeInvalid
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == hpBeforeInvalid,
                "invalid target preserves AP and vitality");

            int shots = 0;
            while (!session.Vitality.IsDead(BearIdentity))
            {
                CombatHitChance chance = session.Combat.GetBasicRangedHitChance(pc, BearIdentity,
                    bow.WeaponData, InteractionRangeRules.Distance(pcRuntime.Tile, bearRuntime.Tile));
                session.Combat.SetRandomSource(chance.DodgeChance > 0
                    ? new SequenceRandom(1, 100, 10, 5)
                    : new SequenceRandom(1, 10, 5));
                CombatAttackResult attack = session.Combat.Attack(pc, BearIdentity, CombatAttackMode.BasicRanged);
                Check(attack.Succeeded && attack.Hit && attack.WeaponIdentity == BowIdentity
                      && attack.AmmoIdentity == ArrowIdentity && attack.MitigatedHitPointDamage > 0,
                    "deterministic lethal proof uses production ranged attack and M4B damage");
                shots++;
                if (session.Vitality.IsDead(BearIdentity)) break;
                Check(session.Combat.EndCurrentTurn(pc).Succeeded
                      && session.Combat.CurrentParticipant == BearIdentity
                      && session.Combat.EndCurrentTurn(BearIdentity).Succeeded
                      && session.Combat.CurrentParticipant == pc,
                    "nonlethal shot advances bear-to-PC without duplicate turns");
            }

            Check(deathTransitions == 1 && session.Vitality.GetCurrentHitPoints(BearIdentity) <= 0,
                "authoritative dead threshold changes exactly once");
            Check(bearRuntime.Identity == BearIdentity && bearRuntime.Type == ObjectType.Npc
                  && bearRuntime.IsDead && ((bearRuntime.ArtId >> 6) & 0x1F) == 7,
                "corpse retains NPC identity and projects fall-down animation 7");
            Check(!bearRuntime.Blocks && loader.NavigationMap.IsWalkable(bearRuntime.Tile),
                "dead actor loses dynamic navigation occupancy");
            Check(session.Combat.Participants.All(value => value.Identity != BearIdentity)
                  && session.Combat.CurrentParticipant == pc && session.Combat.RoundNumber == shots,
                "non-current defeat removes bear without consuming or duplicating the PC turn");
            Check(Relationships(session, BearIdentity).SequenceEqual(relationships),
                "death preserves all bear inventory/equipment relationships");
            int deadAp = session.Combat.CurrentActionPoints;
            Check(session.Combat.Attack(BearIdentity, pc).Failure == CombatFailure.NotCurrentParticipant
                  && session.Combat.CurrentActionPoints == deadAp,
                "dead actor cannot attack or spend AP");
            uint corpseArt = bearRuntime.ArtId;
            session.Vitality.ApplyHitPointDamage(BearIdentity, 5);
            Check(deathTransitions == 1 && bearRuntime.ArtId == corpseArt
                  && CountIdentity(BearIdentity) == 1 && Count<WorldObject>() == worldObjects,
                "repeat lethal damage is idempotent and creates no replacement identity");
            Check(session.Combat.EndCombat(pc).Succeeded && !session.Combat.IsActive,
                "explicit EndCombat remains the bounded combat-end policy");

            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.TryGetLoadedObject(BearIdentity, out bearRuntime)
                      && bearRuntime.IsDead && !bearRuntime.Blocks
                      && loader.NavigationMap.IsWalkable(bearRuntime.Tile)
                      && session.Vitality.IsDead(BearIdentity)
                      && Relationships(session, BearIdentity).SequenceEqual(relationships)
                      && CountIdentity(BearIdentity) == 1,
                    $"{mode} rebuild preserves corpse domain state without duplication");
            }

            string deadJson = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(deadJson).Succeeded, "dead-state V1 load succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.Vitality.IsDead(BearIdentity)
                  && session.TryGetLoadedObject(BearIdentity, out bearRuntime)
                  && bearRuntime.Identity == BearIdentity && bearRuntime.IsDead && !bearRuntime.Blocks
                  && Relationships(session, BearIdentity).SequenceEqual(relationships),
                "V1 load restores dead state, stable identity, and relationships");
            session.ClearSelectedSector();
            yield return null;
            Check(session.SelectSector(CombatSector), "dead actor sector reloads");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.Vitality.IsDead(BearIdentity)
                  && session.TryGetLoadedObject(BearIdentity, out bearRuntime)
                  && bearRuntime.IsDead && !bearRuntime.Blocks && loader.NavigationMap.IsWalkable(bearRuntime.Tile),
                "sector reload cannot resurrect the source-authored identity or ghost blocker");

            Check(session.SaveGames.LoadJson(livingJson).Succeeded, "living baseline restores for fatigue proof");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.TryGetLoadedObject(BearIdentity, out bearRuntime), "bear presentation restores alive");
            int unconsciousTransitions = 0;
            session.Vitality.Changed += change =>
            {
                if (change.Identity == BearIdentity && change.PreviousFatigue > 0
                    && change.CurrentFatigue <= 0) unconsciousTransitions++;
            };
            Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded
                  && session.Combat.CurrentParticipant == BearIdentity,
                "authentic bear starts as current actor for unconsciousness proof");
            int fatigue = session.Vitality.GetCurrentFatigue(BearIdentity);
            session.Vitality.ApplyFatigueDamage(BearIdentity, fatigue);
            Check(unconsciousTransitions == 1 && session.Vitality.IsUnconscious(BearIdentity)
                  && !session.Vitality.IsDead(BearIdentity) && bearRuntime.Identity == BearIdentity,
                "fatigue threshold creates unconscious, not dead, state exactly once");
            Check(session.Combat.Participants.Any(value => value.Identity == BearIdentity)
                  && session.Combat.CurrentParticipant == pc && session.Combat.RoundNumber == 1,
                "unconscious current actor remains registered and advances once to PC");
            Check(bearRuntime.Blocks && !loader.NavigationMap.IsWalkable(bearRuntime.Tile),
                "unconscious actor retains audited movement blocking");
            session.Vitality.ApplyFatigueDamage(BearIdentity, 5);
            Check(unconsciousTransitions == 1, "repeated unconscious damage is idempotent");
            Check(session.Combat.Attack(BearIdentity, pc).Failure == CombatFailure.NotCurrentParticipant,
                "unconscious actor cannot act");
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.TryGetLoadedObject(BearIdentity, out bearRuntime)
                      && session.Vitality.IsUnconscious(BearIdentity) && !bearRuntime.IsDead
                      && bearRuntime.Blocks && !loader.NavigationMap.IsWalkable(bearRuntime.Tile)
                      && CountIdentity(BearIdentity) == 1,
                    $"{mode} rebuild preserves unconscious state and blocking");
            }
            string unconsciousJson = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(unconsciousJson).Succeeded, "unconscious-state V1 load succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(!session.Combat.IsActive && session.Vitality.IsUnconscious(BearIdentity)
                  && session.TryGetLoadedObject(BearIdentity, out bearRuntime)
                  && bearRuntime.Blocks && !loader.NavigationMap.IsWalkable(bearRuntime.Tile),
                "V1 load normalizes combat while restoring unconscious state and blocking");

            Check(session.SaveGames.LoadJson(livingJson).Succeeded,
                "living baseline restores for current-actor defeat proof");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded
                  && session.Combat.CurrentParticipant == BearIdentity,
                "bear owns current turn before current-actor defeat");
            session.Vitality.ApplyHitPointDamage(BearIdentity,
                session.Vitality.GetCurrentHitPoints(BearIdentity));
            Check(session.Combat.CurrentParticipant == pc && session.Combat.RoundNumber == 1
                  && session.Combat.Participants.Count == 1
                  && session.Combat.Participants[0].Identity == pc,
                "current-actor defeat advances once with no duplicate round or participant");
            Check(session.Combat.EndCombat(pc).Succeeded,
                "explicit EndCombat succeeds when no eligible hostile remains");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M8D PLAYMODE VALIDATION PASS: sector={CombatSector}; bear={BearIdentity}; "
                      + $"lethalShots={shots}; deathTransitions=1; corpse=sameIdentity+anim7+noBlock; "
                      + "inventoryEquipment=retained; combatEnd=explicit; unconscious=retainedParticipant+blocking; "
                      + "currentActorDefeat=singleAdvance; graphics=Original->Enhanced->Original; "
                      + "saveV1=dead+unconscious; reload=noResurrection; duplicates=0; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void StartPcTurn(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded,
            "production StartCombat succeeds");
        Check(session.Combat.CurrentParticipant == BearIdentity
              && session.Combat.EndCurrentTurn(BearIdentity).Succeeded
              && session.Combat.CurrentParticipant == pc,
            "authentic bear-to-PC turn handoff succeeds");
    }

    private static void MoveOwnedItem(WorldMapSessionCoordinator session, PersistentObjectState item,
        ArcanumObjectId owner)
    {
        if (item.Placement.Kind == ObjectPlacementKind.Equipped)
            Check(session.UnequipItem(item.Placement.ParentIdentity, item.Placement.WornLocation).Succeeded,
                "source equipped item unequips before transfer");
        if (item.Placement != ObjectPlacement.ContainedBy(owner))
            Check(session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(owner)).Succeeded,
                "authentic item transfers through M3 authority");
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile, bool controlled)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative movement state accepts validation placement");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
        if (controlled) loader.NavigationMap.SetControlledObject(runtime);
    }

    private static Vector2Int FindClearRangedTile(SectorNavigationMap map, Vector2Int target,
        int minimumDistance, int maximumDistance)
    {
        for (int distance = minimumDistance; distance <= maximumDistance; distance++)
        for (int y = Math.Max(0, target.y - distance); y <= Math.Min(63, target.y + distance); y++)
        for (int x = Math.Max(0, target.x - distance); x <= Math.Min(63, target.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) == distance && map.IsWalkable(candidate)
                && map.HasProjectileLineOfFire(candidate, target)) return candidate;
        }
        throw new InvalidOperationException("M8D validation FAIL: no clear in-range source tile.");
    }

    private static PersistentObjectState RequireState(WorldMapSessionCoordinator session,
        ArcanumObjectId identity, ObjectType type, int prototype)
    {
        Check(session.States.TryGetValue(identity, out PersistentObjectState state),
            $"exact ObjectID {identity} resolves");
        Check(state.Type == type && state.PrototypeNumber == prototype,
            $"{identity} retains expected type/prototype");
        return state;
    }

    private static Relationship[] Relationships(WorldMapSessionCoordinator session, ArcanumObjectId parent)
        => session.States.Values.Where(value => value.Placement.ParentIdentity == parent)
            .Select(value => new Relationship(value.Identity, value.Placement)).OrderBy(value => value.Identity).ToArray();

    private readonly struct Relationship : IEquatable<Relationship>
    {
        public readonly ArcanumObjectId Identity;
        public readonly ObjectPlacement Placement;
        public Relationship(ArcanumObjectId identity, ObjectPlacement placement)
        {
            Identity = identity;
            Placement = placement;
        }
        public bool Equals(Relationship other) => Identity == other.Identity && Placement == other.Placement;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);
        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum, "deterministic sample is in source range");
            return value;
        }
    }

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && lifecycle != null, "production TestTerrain composition remains available");
    }

    private static int CountIdentity(ArcanumObjectId identity)
        => Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(value => value.Identity == identity);

    private static int Count<T>() where T : Object
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8D validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
