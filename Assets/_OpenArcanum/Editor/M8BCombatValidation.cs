using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8BCombatValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string OtherMapSector = "maps/bates mansion lev 1/67108865.sec";
    private const string FixtureKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private const int FixturePrototype = 28422;
    private static readonly ArcanumObjectId FixtureIdentity = ParseIdentity(FixtureKey);
    private static readonly ArcanumObjectId MissingIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;
    private static string _temporarySlot;

    [MenuItem("OpenArcanum/M8B/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8B harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        _temporarySlot = null;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(FixtureSector), "real Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle,
                out PlayerNavigationController navigation, out PlayerInteractionController interaction,
                out ProductionSaveLoadPresenter savePresenter);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

            PersistentPlayerState player = session.PlayerState;
            Check(player != null && player.Identity.IsPersistent, "production PC has a stable ObjectID");
            ArcanumObjectId pc = player.Identity;
            PersistentObjectState bear = RequireBear(session);
            Check(session.TryGetLoadedObject(FixtureIdentity, out WorldObject bearRuntime)
                  && bearRuntime.Type == ObjectType.Npc, "authentic bear has one production presentation");
            Check(session.Combat.TryGetActorSource(FixtureIdentity, out CombatActorSource source)
                  && source.PrototypeNumber == FixturePrototype
                  && source.GetNaturalDamageMinimum(DamageType.Normal) == 3
                  && source.GetNaturalDamageMaximum(DamageType.Normal) == 6,
                "production loader registers authentic natural damage 3..6");

            yield return MovePlayerAdjacent(loader, navigation, bearRuntime.Tile);
            Check(InteractionRangeRules.Distance(navigation.Player.TilePosition, bearRuntime.TilePosition) == 1,
                "production PC physically reaches unarmed melee range 1");

            int initialWorldObjects = Count<WorldObject>();
            int initialPcHp = session.Vitality.GetCurrentHitPoints(pc);
            int initialPcFatigue = session.Vitality.GetCurrentFatigue(pc);
            int initialBearHp = session.Vitality.GetCurrentHitPoints(FixtureIdentity);
            int initialBearFatigue = session.Vitality.GetCurrentFatigue(FixtureIdentity);
            int vitalityCount = session.Vitality.States.Count;
            PersistentCharacterVitalityState pcVitality = session.Vitality.Get(pc);

            CombatSnapshot inactive = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.Inactive,
                "attack while combat is inactive fails");
            CheckRollback(session, pc, inactive, "inactive attack");

            StartAndCheck(session, pc, initialWorldObjects);
            Check(!navigation.TrySetDestination(FindFreeStep(loader, navigation.Player.Tile, bearRuntime.Tile)),
                "ordinary navigation cannot bypass active combat");

            ArcanumObjectId graphicsCurrent = session.Combat.CurrentParticipant;
            int graphicsRound = session.Combat.RoundNumber;
            int graphicsAp = session.Combat.CurrentActionPoints;
            int graphicsPcHp = session.Vitality.GetCurrentHitPoints(pc);
            int graphicsPcFatigue = session.Vitality.GetCurrentFatigue(pc);
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.Combat.IsActive && session.Combat.CurrentParticipant == graphicsCurrent
                      && session.Combat.RoundNumber == graphicsRound
                      && session.Combat.CurrentActionPoints == graphicsAp
                      && session.Vitality.GetCurrentHitPoints(pc) == graphicsPcHp
                      && session.Vitality.GetCurrentFatigue(pc) == graphicsPcFatigue,
                    $"{mode} rebuild preserves combat/AP/vitality");
                CheckOrder(session, pc);
                CheckUnique(loader, lifecycle, navigation, pc, initialWorldObjects);
            }

            Vector2Int overBudget = FindRouteDestination(loader, bearRuntime.Tile, navigation.Player.Tile,
                minimumSteps: 3);
            CombatSnapshot overBudgetBefore = new(session, pc);
            CombatMoveResult overBudgetResult = session.Combat.MoveInCombat(FixtureIdentity, overBudget);
            Check(overBudgetResult.Failure == CombatFailure.InsufficientActionPoints
                  && overBudgetResult.RouteSteps >= 3, "NPC over-budget movement is atomically rejected");
            CheckRollback(session, pc, overBudgetBefore, "NPC over-budget movement");

            Vector2Int bearStep = FindCommonAdjacentStep(loader, bearRuntime.Tile, navigation.Player.Tile);
            Vector2Int bearStart = bearRuntime.Tile;
            CombatMoveResult bearMove = session.Combat.MoveInCombat(FixtureIdentity, bearStep);
            int bearFacing = IsoProjection.DirFromDelta(bearStep.x - bearStart.x,
                bearStep.y - bearStart.y);
            Check(bearMove.Succeeded && bearMove.StepsMoved == 1 && bearMove.ActionPointsSpent == 2
                  && session.Combat.CurrentActionPoints == 3 && bearRuntime.Tile == bearStep,
                "bear movement spends exactly 2 AP for one source step");
            Check(!bearRuntime.IsMoving && CritterArtResolver.RotationOf(bearRuntime.ArtId) == bearFacing
                  && ((bearRuntime.ArtId >> 14) & 0x1F) == 0,
                "bear movement commits facing and returns WALK presentation to STAND");

            CombatSnapshot insufficientBefore = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.InsufficientActionPoints,
                "bear cannot attack with only 3 AP");
            CheckRollback(session, pc, insufficientBefore, "insufficient attack AP");

            Check(session.Combat.EndCurrentTurn(FixtureIdentity).Succeeded
                  && session.Combat.CurrentParticipant == pc, "bear turn advances to PC");
            CombatSnapshot outOfTurnBefore = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.NotCurrentParticipant,
                "out-of-turn bear attack fails");
            CheckRollback(session, pc, outOfTurnBefore, "out-of-turn attack");
            Check(session.Combat.EndCurrentTurn(pc).Succeeded
                  && session.Combat.CurrentParticipant == FixtureIdentity
                  && session.Combat.RoundNumber == 2 && session.Combat.CurrentActionPoints == 5,
                "PC turn rolls exactly once to round-two bear AP 5");

            session.Combat.SetRandomSource(new SequenceRandom(1, 5));
            CombatHitChance hitChance = session.Combat.GetBasicMeleeHitChance(FixtureIdentity, pc);
            CombatAttackResult hit = session.Combat.Attack(FixtureIdentity, pc);
            Check(hit.Succeeded && hit.Hit && !hit.Dodged && hitChance.AttackChance == 40
                  && hit.RawHitPointDamage == 5 && hit.MitigatedHitPointDamage == 5
                  && hit.ActionPointCost == 5 && hit.ActionPointsSpent == 5,
                "seeded production hit resolves 40% chance and authentic adjusted damage 5");
            Check(session.Vitality.GetCurrentHitPoints(pc) == initialPcHp - 5
                  && session.Vitality.GetCurrentFatigue(pc) == initialPcFatigue
                  && ReferenceEquals(session.Vitality.Get(pc), pcVitality)
                  && session.Vitality.States.Count == vitalityCount,
                "hit mutates the one M4B PC vitality record exactly once");
            Check(session.Combat.CurrentParticipant == pc && session.Combat.CurrentActionPoints > 0,
                "hit exhausts bear AP and advances to PC");
            int damagedPcHp = session.Vitality.GetCurrentHitPoints(pc);

            Check(session.Combat.EndCurrentTurn(pc).Succeeded
                  && session.Combat.CurrentParticipant == FixtureIdentity
                  && session.Combat.RoundNumber == 3 && session.Combat.CurrentActionPoints == 5,
                "round-three bear begins with AP reset to 5");
            session.Combat.SetRandomSource(new SequenceRandom(100));
            int missHp = session.Vitality.GetCurrentHitPoints(pc);
            int missFatigue = session.Vitality.GetCurrentFatigue(pc);
            CombatAttackResult miss = session.Combat.Attack(FixtureIdentity, pc);
            Check(miss.Succeeded && !miss.Hit && miss.RawHitPointDamage == 0
                  && miss.MitigatedHitPointDamage == 0 && miss.ActionPointsSpent == 5,
                "seeded production miss spends 5 AP without damage");
            Check(session.Vitality.GetCurrentHitPoints(pc) == missHp
                  && session.Vitality.GetCurrentFatigue(pc) == missFatigue
                  && session.Combat.CurrentParticipant == pc,
                "miss preserves M4B vitality and advances to PC");

            Vector2Int pcRunStep = FindFreeStep(loader, navigation.Player.Tile, bearRuntime.Tile);
            CombatMoveResult pcRun = session.Combat.MoveInCombat(pc, pcRunStep, pcAlwaysRun: true);
            Check(pcRun.Succeeded && pcRun.StepsMoved == 1 && pcRun.ActionPointsSpent == 1
                  && session.Combat.CurrentActionPoints == 7,
                "optional PC always-run spends 1 AP per step");
            Vector2Int pcWalkDestination = FindRouteDestination(loader, pcRunStep, bearRuntime.Tile,
                minimumSteps: 3, maximumSteps: 3, minimumDistanceFromBlocked: 3);
            CombatMoveResult pcWalk = session.Combat.MoveInCombat(pc, pcWalkDestination);
            Check(pcWalk.Succeeded && pcWalk.StepsMoved == 3 && pcWalk.ActionPointsSpent == 6
                  && session.Combat.CurrentActionPoints == 1,
                "ordinary PC combat movement spends 2 AP per cardinal or diagonal step");
            int fatigueBeforeOverdraw = session.Vitality.GetCurrentFatigue(pc);
            Vector2Int overdrawStep = FindStepAway(loader, navigation.Player.Tile, bearRuntime.Tile);
            CombatMoveResult overdraw = session.Combat.MoveInCombat(pc, overdrawStep);
            Check(overdraw.Succeeded && overdraw.StepsMoved == 1 && overdraw.ActionPointsSpent == 1
                  && overdraw.FatigueDamage == 2
                  && session.Vitality.GetCurrentFatigue(pc) == fatigueBeforeOverdraw - 2,
                "PC source overdraw admits one final step for 2 Fatigue");
            Check(session.Combat.CurrentParticipant == FixtureIdentity
                  && session.Combat.RoundNumber == 4 && session.Combat.CurrentActionPoints == 5,
                "PC overdraw reaches zero AP and advances once to round-four bear");
            Check(!navigation.Player.IsMoving && navigation.Player.Tile == overdrawStep
                  && ((navigation.Player.ArtId >> 14) & 0x1F) == 0,
                "PC combat movement finishes at deterministic STAND presentation");

            CombatSnapshot outOfRangeBefore = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.OutOfRange,
                "out-of-range attack fails");
            CheckRollback(session, pc, outOfRangeBefore, "out-of-range attack");
            CombatSnapshot invalidBefore = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, MissingIdentity).Failure
                  == CombatFailure.ParticipantNotRegistered, "invalid target fails");
            CheckRollback(session, pc, invalidBefore, "invalid target");
            Check(session.Combat.Attack(FixtureIdentity, pc, (CombatAttackMode)99).Failure
                  == CombatFailure.UnsupportedAttackMode, "invalid attack mode fails");
            CheckRollback(session, pc, invalidBefore, "invalid attack mode");
            Check(session.Combat.MoveInCombat(FixtureIdentity, navigation.Player.Tile).Failure
                  == CombatFailure.InvalidDestination, "another combat actor tile is blocked");
            CheckRollback(session, pc, invalidBefore, "blocked combat movement");

            int bearHp = session.Vitality.GetCurrentHitPoints(FixtureIdentity);
            session.Vitality.ApplyHitPointDamage(FixtureIdentity, bearHp);
            CombatSnapshot deadActor = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.ParticipantUnavailable,
                "dead current actor cannot attack");
            CheckRollback(session, pc, deadActor, "dead actor attack");
            session.Vitality.RestoreHitPoints(FixtureIdentity, bearHp);
            int bearFatigue = session.Vitality.GetCurrentFatigue(FixtureIdentity);
            session.Vitality.ApplyFatigueDamage(FixtureIdentity, bearFatigue);
            CombatSnapshot unconsciousActor = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.ParticipantUnavailable,
                "unconscious current actor cannot attack");
            CheckRollback(session, pc, unconsciousActor, "unconscious actor attack");
            session.Vitality.RestoreFatigue(FixtureIdentity, bearFatigue);

            int pcHp = session.Vitality.GetCurrentHitPoints(pc);
            session.Vitality.ApplyHitPointDamage(pc, pcHp);
            CombatSnapshot deadTarget = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.ParticipantUnavailable,
                "dead target cannot be attacked");
            CheckRollback(session, pc, deadTarget, "dead target attack");
            session.Vitality.RestoreHitPoints(pc, pcHp);
            int pcFatigue = session.Vitality.GetCurrentFatigue(pc);
            session.Vitality.ApplyFatigueDamage(pc, pcFatigue);
            CombatSnapshot unconsciousTarget = new(session, pc);
            Check(session.Combat.Attack(FixtureIdentity, pc).Failure == CombatFailure.ParticipantUnavailable,
                "unconscious target cannot be attacked");
            CheckRollback(session, pc, unconsciousTarget, "unconscious target attack");
            session.Vitality.RestoreFatigue(pc, pcFatigue);

            Check(session.Combat.EndCombat(pc).Failure == CombatFailure.HostileParticipantActive,
                "active hostile still blocks EndCombat after damage");
            Check(session.Combat.RemoveParticipant(FixtureIdentity).Succeeded
                  && session.Combat.EndCombat(pc).Succeeded, "EndCombat succeeds after hostile resolution");
            CheckInactive(session);
            Check(session.Vitality.GetCurrentHitPoints(pc) == damagedPcHp,
                "M4B damage persists after transient combat ends");
            Check(navigation.TrySetDestination(FindFreeStep(loader, navigation.Player.Tile, bearRuntime.Tile)),
                "ordinary navigation resumes after combat");
            navigation.CancelRoute();
            Check(session.ExecuteInteraction(new WorldInteractionCommand(pc, FixtureIdentity,
                      WorldInteractionCommandType.Use)).Code != WorldInteractionResultCode.Blocked,
                "ordinary interaction resumes after combat");

            StartAndCheck(session, pc, initialWorldObjects);
            Check(session.Vitality.GetCurrentHitPoints(pc) == damagedPcHp,
                "new combat reuses prior authoritative M4B damage");
            Check(session.Combat.RemoveParticipant(FixtureIdentity).Succeeded
                  && session.Combat.EndCombat(pc).Succeeded, "second combat cycle ends cleanly");

            StartAndCheck(session, pc, initialWorldObjects);
            savePresenter.Open(SaveLoadPanelMode.Save);
            _temporarySlot = savePresenter.Controller.SuggestedSlotId;
            savePresenter.Controller.SelectNewSlot();
            savePresenter.Controller.RequestSave();
            Check(string.IsNullOrEmpty(savePresenter.Controller.ErrorMessage)
                  && savePresenter.Controller.SelectedSlotId == _temporarySlot
                  && session.Combat.IsActive, "save UI stores V1 while live combat remains transient");
            string activeJson = session.SaveGames.SerializeCurrentSession();
            Check(!activeJson.Contains("combat", StringComparison.OrdinalIgnoreCase),
                "V1 payload contains no combat state");
            savePresenter.Open(SaveLoadPanelMode.Load);
            Check(savePresenter.Controller.SelectSlot(_temporarySlot), "temporary M8B slot is selectable");
            savePresenter.Controller.RequestLoad();
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction, out savePresenter);
            Check(!session.Combat.IsActive && session.Combat.Participants.Count == 0,
                "load normalizes active combat to Inactive");
            Check(session.Vitality.GetCurrentHitPoints(pc) == damagedPcHp,
                "load restores post-attack M4B damage");
            Check(session.SaveSlots.DeleteSlot(_temporarySlot).Succeeded, "temporary M8B slot is removed");
            _temporarySlot = null;

            StartAndCheck(session, pc, Count<WorldObject>());
            session.ClearSelectedSector();
            yield return null;
            Check(!session.Combat.IsActive && !session.HasSelectedSector,
                "sector unload normalizes transient combat");
            Check(session.SelectSector(FixtureSector), "fixture reloads after combat unload");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction, out savePresenter);
            Check(session.Vitality.GetCurrentHitPoints(pc) == damagedPcHp,
                "sector reload preserves authoritative vitality damage");

            StartAndCheck(session, pc, Count<WorldObject>());
            Check(session.SelectSector(OtherMapSector), "cross-map selection succeeds during combat");
            yield return null;
            Check(!session.Combat.IsActive && session.SelectedSector == OtherMapSector,
                "cross-map transition normalizes transient combat");
            Check(session.SelectSector(FixtureSector), "fixture restores after cross-map transition");
            yield return null;
            Refresh(out loader, out lifecycle, out navigation, out interaction, out savePresenter);
            Check(session.PlayerState.Identity == pc && RequireBear(session).Identity == FixtureIdentity
                  && session.Vitality.GetCurrentHitPoints(pc) == damagedPcHp,
                "transition preserves identities and M4B damage without combat state");
            CheckUnique(loader, lifecycle, navigation, pc, Count<WorldObject>());

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M8B PLAYMODE VALIDATION PASS: sector={FixtureSector}; bear={FixtureIdentity}; "
                      + "order=bear,pc; bearAP=5; npcStepAP=2; pcWalkAP=2; pcRunAP=1; "
                      + "overdrawFatigue=2; hitChance=40; hitRaw=5; hitMitigated=5; missDamage=0; "
                      + $"pcHP={initialPcHp}->{damagedPcHp}; rounds=4; graphics=Original->Enhanced->Original; "
                      + "endCombat=DamageRetained; saveV1=TransientExcluded; load=Inactive; unload=Inactive; "
                      + $"mapChange=Inactive; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            session.Combat.ResetRandomSource();
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (!string.IsNullOrEmpty(_temporarySlot)) session.SaveSlots.DeleteSlot(_temporarySlot);
            _temporarySlot = null;
            _running = false;
        }
    }

    private static IEnumerator MovePlayerAdjacent(WorldObjectSectorLoader loader,
        PlayerNavigationController navigation, Vector2Int target)
    {
        foreach (Vector2Int delta in IsoProjection.DirDelta)
        {
            Vector2Int candidate = target + delta;
            if (!loader.NavigationMap.IsWalkable(candidate) || !navigation.TrySetDestination(candidate)) continue;
            int frames = 0;
            while (navigation.IsMoving && frames++ < 900) yield return null;
            Check(!navigation.IsMoving && navigation.Player.Tile == candidate,
                "ordinary production navigation reaches the selected adjacent tile");
            yield break;
        }
        throw new InvalidOperationException("M8B validation FAIL: no reachable adjacent bear tile.");
    }

    private static Vector2Int FindCommonAdjacentStep(WorldObjectSectorLoader loader, Vector2Int start,
        Vector2Int pc)
    {
        for (int rotation = 0; rotation < IsoProjection.DirDelta.Length; rotation++)
        {
            Vector2Int candidate = start + IsoProjection.DirDelta[rotation];
            if (candidate != pc && loader.NavigationMap.CanTraverse(start, rotation)
                && InteractionRangeRules.Distance(candidate, pc) == 1) return candidate;
        }
        throw new InvalidOperationException("M8B validation FAIL: no bear step remains in melee range.");
    }

    private static Vector2Int FindRouteDestination(WorldObjectSectorLoader loader, Vector2Int start,
        Vector2Int blocked, int minimumSteps, int maximumSteps = int.MaxValue,
        int minimumDistanceFromBlocked = 0)
    {
        var finder = new DeterministicTilePathfinder();
        var route = new List<Vector2Int>();
        for (int radius = minimumSteps; radius <= 12; radius++)
        for (int y = Math.Max(0, start.y - radius); y <= Math.Min(63, start.y + radius); y++)
        for (int x = Math.Max(0, start.x - radius); x <= Math.Min(63, start.x + radius); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (!loader.NavigationMap.IsWalkable(candidate) || candidate == blocked
                || InteractionRangeRules.Distance(candidate, blocked) < minimumDistanceFromBlocked
                || !finder.TryFindPath(loader.NavigationMap, start, candidate, route)
                || route.Contains(blocked) || route.Count < minimumSteps || route.Count > maximumSteps) continue;
            return candidate;
        }
        throw new InvalidOperationException("M8B validation FAIL: no route with the requested length.");
    }

    private static Vector2Int FindFreeStep(WorldObjectSectorLoader loader, Vector2Int start, Vector2Int blocked)
    {
        for (int rotation = 0; rotation < IsoProjection.DirDelta.Length; rotation++)
        {
            Vector2Int candidate = start + IsoProjection.DirDelta[rotation];
            if (candidate != blocked && loader.NavigationMap.CanTraverse(start, rotation)) return candidate;
        }
        throw new InvalidOperationException("M8B validation FAIL: no free combat step.");
    }

    private static Vector2Int FindStepAway(WorldObjectSectorLoader loader, Vector2Int start, Vector2Int blocked)
    {
        int distance = InteractionRangeRules.Distance(start, blocked);
        for (int rotation = 0; rotation < IsoProjection.DirDelta.Length; rotation++)
        {
            Vector2Int candidate = start + IsoProjection.DirDelta[rotation];
            if (candidate != blocked && loader.NavigationMap.CanTraverse(start, rotation)
                && InteractionRangeRules.Distance(candidate, blocked) > distance) return candidate;
        }
        return FindFreeStep(loader, start, blocked);
    }

    private static void StartAndCheck(WorldMapSessionCoordinator session, ArcanumObjectId pc,
        int expectedWorldObjects)
    {
        Check(session.Combat.StartCombat(pc, FixtureIdentity).Succeeded, "production StartCombat succeeds");
        Check(session.Combat.CurrentParticipant == FixtureIdentity && session.Combat.RoundNumber == 1
              && session.Combat.CurrentActionPoints == 5 && session.Combat.MaximumActionPoints == 5,
            "authentic bear owns the first turn with AP 5");
        CheckOrder(session, pc);
        Check(Count<WorldObject>() == expectedWorldObjects, "combat creates no WorldObject clones");
    }

    private static void CheckOrder(WorldMapSessionCoordinator session, ArcanumObjectId pc)
    {
        Check(session.Combat.Participants.Count == 2
              && session.Combat.Participants[0].Identity == FixtureIdentity
              && session.Combat.Participants[1].Identity == pc
              && session.Combat.Participants.Select(value => value.Identity).Distinct().Count() == 2,
            "participants remain exactly bear then PC without duplicates");
    }

    private static void CheckInactive(WorldMapSessionCoordinator session)
    {
        Check(!session.Combat.IsActive && session.Combat.Participants.Count == 0
              && session.Combat.CurrentParticipant.IsNull && session.Combat.RoundNumber == 0
              && session.Combat.CurrentActionPoints == 0 && session.Combat.MaximumActionPoints == 0,
            "combat transient state is fully inactive");
    }

    private static void CheckRollback(WorldMapSessionCoordinator session, ArcanumObjectId pc,
        CombatSnapshot expected, string label)
        => Check(expected.Equals(new CombatSnapshot(session, pc)), label + " preserves the complete combat transaction");

    private readonly struct CombatSnapshot : IEquatable<CombatSnapshot>
    {
        public readonly int ActionPoints;
        public readonly int Round;
        public readonly ArcanumObjectId Current;
        public readonly int PcHp;
        public readonly int PcFatigue;
        public readonly int BearHp;
        public readonly int BearFatigue;
        public readonly Vector2 PcPosition;
        public readonly Vector2 BearPosition;

        public CombatSnapshot(WorldMapSessionCoordinator session, ArcanumObjectId pc)
        {
            ActionPoints = session.Combat.CurrentActionPoints;
            Round = session.Combat.RoundNumber;
            Current = session.Combat.CurrentParticipant;
            PcHp = session.Vitality.GetCurrentHitPoints(pc);
            PcFatigue = session.Vitality.GetCurrentFatigue(pc);
            BearHp = session.Vitality.GetCurrentHitPoints(FixtureIdentity);
            BearFatigue = session.Vitality.GetCurrentFatigue(FixtureIdentity);
            PcPosition = session.PlayerState.TilePosition;
            BearPosition = session.States[FixtureIdentity].TilePosition;
        }

        public bool Equals(CombatSnapshot other)
            => ActionPoints == other.ActionPoints && Round == other.Round && Current == other.Current
               && PcHp == other.PcHp && PcFatigue == other.PcFatigue
               && BearHp == other.BearHp && BearFatigue == other.BearFatigue
               && PcPosition == other.PcPosition && BearPosition == other.BearPosition;
    }

    private sealed class SequenceRandom : ICombatRandom
    {
        private readonly Queue<int> _values;
        public SequenceRandom(params int[] values) => _values = new Queue<int>(values);

        public int NextInclusive(int minimum, int maximum)
        {
            Check(_values.Count > 0, "combat requests only expected deterministic RNG samples");
            int value = _values.Dequeue();
            Check(value >= minimum && value <= maximum, "deterministic RNG sample is inside source roll bounds");
            return value;
        }
    }

    private static PersistentObjectState RequireBear(WorldMapSessionCoordinator session)
    {
        Check(session.States.TryGetValue(FixtureIdentity, out PersistentObjectState bear),
            "exact Polar Bear Cub ObjectID resolves");
        Check(bear.Type == ObjectType.Npc && bear.PrototypeNumber == FixturePrototype,
            "exact fixture is NPC prototype 28422");
        return bear;
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ProductionPlayerLifecycle lifecycle,
        PlayerNavigationController navigation, ArcanumObjectId pc, int expectedWorldObjects)
    {
        Check(Count<WorldMapSessionCoordinator>() == 1 && Count<WorldObjectSectorLoader>() == 1
              && Count<ProductionPlayerLifecycle>() == 1 && Count<PlayerNavigationController>() == 1
              && Count<PlayerInteractionController>() == 1 && Count<ProductionSaveLoadPresenter>() == 1,
            "one production gameplay composition remains");
        Check(CountIdentity(pc) == 1 && CountIdentity(FixtureIdentity) == 1,
            "one PC and one bear presentation remain");
        Check(Count<WorldObject>() == expectedWorldObjects, "no duplicate WorldObject presentation appears");
        Check(loader.SpriteOwners.Where(owner => owner?.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one sprite owner per persistent identity");
        Check(navigation.Player == lifecycle.Presentation, "navigation remains bound to the production PC");
    }

    private static int CountIdentity(ArcanumObjectId identity)
        => Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(value => value.Identity == identity);

    private static int Count<T>() where T : Object
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

    private static ArcanumObjectId ParseIdentity(string key)
    {
        if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
            throw new InvalidOperationException("Invalid validation ObjectID: " + key);
        return identity;
    }

    private static void Refresh(out WorldObjectSectorLoader loader, out ProductionPlayerLifecycle lifecycle,
        out PlayerNavigationController navigation, out PlayerInteractionController interaction,
        out ProductionSaveLoadPresenter savePresenter)
    {
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        savePresenter = Object.FindFirstObjectByType<ProductionSaveLoadPresenter>();
        Check(loader != null && lifecycle != null && navigation != null && interaction != null
              && savePresenter != null, "production TestTerrain composition remains available");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8B validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
