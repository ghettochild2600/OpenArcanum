using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M8GCombatLoopValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/47781512457.sec";
    private const string BearKey = "G_9B807B01_A142_4949_80CE_5A085F3BEEB1";
    private const int OnfKos = 0x00000100;
    private const int OcfAnimal = 0x00008000;
    private static readonly ArcanumObjectId BearIdentity = ParseIdentity(BearKey);
    private static readonly ArcanumObjectId DiscoveredIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("11111111-aaaa-4aaa-8aaa-111111111111"));
    private static readonly ArcanumObjectId ExplicitIdentity = ArcanumObjectId.CreateGuid(
        Guid.Parse("22222222-bbbb-4bbb-8bbb-222222222222"));

    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M8G Phase 1/Run Physical PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M8G Phase 1 harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        GameObject discoveredObject = null;
        GameObject explicitObject = null;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        string baselineJson = null;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(FixtureSector), "authentic Polar Bear Cub sector loads");
            yield return null;
            Refresh(out loader, out ProductionPlayerLifecycle lifecycle);
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

            ArcanumObjectId pc = session.PlayerState.Identity;
            Check(session.TryGetLoadedObject(BearIdentity, out WorldObject bearRuntime)
                  && bearRuntime.Type == ObjectType.Npc, "authentic Polar Bear Cub is loaded");
            baselineJson = session.SaveGames.SerializeCurrentSession();
            int originalBearHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            int originalBearFatigue = session.Vitality.GetCurrentFatigue(BearIdentity);

            var initialBoundaries = new List<CombatRoundBoundary>();
            CombatStateService initialCombat = session.Combat;
            initialCombat.RoundCompleted += initialBoundaries.Add;
            Check(initialCombat.StartCombat(pc, BearIdentity).Succeeded,
                "production combat begins with the authoritative initial roster");
            ArcanumObjectId[] initialOrder = CheckInitialRoster(initialCombat, pc);
            Check(initialBoundaries.Count == 0 && initialCombat.ElapsedCombatTimeMilliseconds == 0,
                "starting combat does not complete a partial round");

            ArcanumObjectId current = initialCombat.CurrentParticipant;
            int round = initialCombat.RoundNumber;
            int actionPoints = initialCombat.CurrentActionPoints;
            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(initialCombat.IsActive && initialCombat.CurrentParticipant == current
                      && initialCombat.RoundNumber == round
                      && initialCombat.CurrentActionPoints == actionPoints
                      && initialBoundaries.Count == 0,
                    $"{mode} rebuild preserves roster, current turn, round and boundary state");
                Check(initialCombat.Participants.Select(value => value.Identity).SequenceEqual(initialOrder),
                    $"{mode} rebuild preserves the discovered authoritative roster exactly");
            }

            Check(session.SaveGames.LoadJson(baselineJson).Succeeded,
                "baseline load normalizes the presentation-proof combat");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.TryGetLoadedObject(BearIdentity, out bearRuntime),
                "bear presentation remains bound after graphics rebuild");
            Vector2Int pcTile = lifecycle.Presentation.Tile;

            var boundaries = new List<CombatRoundBoundary>();
            CombatStateService originalCombat = session.Combat;
            originalCombat.RoundCompleted += boundaries.Add;
            Check(originalCombat.StartCombat(pc, BearIdentity).Succeeded,
                "production combat restarts after the authentic discovery proof");
            ArcanumObjectId[] authoritativeOrder = CheckInitialRoster(originalCombat, pc);
            Check(boundaries.Count == 0 && originalCombat.ElapsedCombatTimeMilliseconds == 0,
                "restarted combat starts without a partial-round boundary");

            Vector2Int discoveredFar = FindFarWalkable(loader.NavigationMap, pcTile, default);
            WorldObject discovered = AddValidationNpc(session, loader, DiscoveredIdentity,
                "M8G discovered hostile", discoveredFar, 0);
            discoveredObject = discovered.gameObject;
            Vector2Int explicitFar = FindFarWalkable(loader.NavigationMap, pcTile, discoveredFar);
            WorldObject explicitActor = AddValidationNpc(session, loader, ExplicitIdentity,
                "M8G explicit hostile", explicitFar, 1);
            explicitObject = explicitActor.gameObject;
            Check(!originalCombat.Participants.Any(value => value.Identity == DiscoveredIdentity)
                  && !originalCombat.Participants.Any(value => value.Identity == ExplicitIdentity),
                "loaded hostile actors remain distinct from enrolled participants while out of range");

            Vector2Int near = FindNearWalkable(loader.NavigationMap, pcTile);
            MoveActor(session, loader, DiscoveredIdentity, discovered, near);
            Check(originalCombat.CurrentParticipant == authoritativeOrder[0] && boundaries.Count == 0,
                "becoming relevant does not steal the current turn or fabricate a boundary");
            ArcanumObjectId firstActor = originalCombat.CurrentParticipant;
            Check(originalCombat.EndCurrentTurn(firstActor).Succeeded && boundaries.Count == 0,
                "one actor turn does not complete the authoritative round");
            Check(originalCombat.RoundNumber == 1,
                "the boundary is round-based rather than actor-based");
            CompleteCurrentRound(originalCombat, 1);
            Check(boundaries.Count == 1 && Boundary(boundaries[0], 1, 1000)
                  && originalCombat.RoundNumber == 2,
                "one completed round emits exactly one 1,000 ms boundary and enrolls the newcomer");
            CheckDeterministicRoster(originalCombat, pc, DiscoveredIdentity);
            Check(originalCombat.Participants.Count(value => value.Identity == DiscoveredIdentity) == 1,
                "production discovery enrolls the newly relevant hostile exactly once");
            AdvanceToActorWithinRound(originalCombat, DiscoveredIdentity, 2);

            current = originalCombat.CurrentParticipant;
            actionPoints = originalCombat.CurrentActionPoints;
            Check(originalCombat.EngageParticipant(ExplicitIdentity).Succeeded,
                "explicit runtime engagement enrolls a valid hostile");
            Check(originalCombat.EngageParticipant(ExplicitIdentity).Failure == CombatFailure.AlreadyRegistered,
                "duplicate runtime engagement is rejected exactly once");
            Check(originalCombat.CurrentParticipant == current
                  && originalCombat.CurrentActionPoints == actionPoints,
                "runtime engagement preserves current-turn ownership and AP");
            CheckDeterministicRoster(originalCombat, pc, DiscoveredIdentity, ExplicitIdentity);

            Check(originalCombat.EndCurrentTurn(DiscoveredIdentity).Succeeded,
                "runtime-discovered actor receives one deterministic turn without a skip");
            AdvanceToActorWithinRound(originalCombat, ExplicitIdentity, 2);
            Check(originalCombat.EndCurrentTurn(ExplicitIdentity).Succeeded,
                "turn order continues exactly once past the explicitly enrolled actor");
            session.Vitality.ApplyHitPointDamage(ExplicitIdentity,
                session.Vitality.GetCurrentHitPoints(ExplicitIdentity));
            Check(session.Vitality.IsDead(ExplicitIdentity)
                  && !originalCombat.Participants.Any(value => value.Identity == ExplicitIdentity)
                  && boundaries.Count == 1,
                "non-current death removes the actor without a duplicate boundary");
            CompleteCurrentRound(originalCombat, 2);
            Check(boundaries.Count == 2 && Boundary(boundaries[1], 2, 2000),
                "runtime enrollment, death and unconscious skipping still complete one boundary");
            AdvanceToActorWithinRound(originalCombat, DiscoveredIdentity, 3);
            int discoveredFatigue = session.Vitality.GetCurrentFatigue(DiscoveredIdentity);
            session.Vitality.ApplyFatigueDamage(DiscoveredIdentity, discoveredFatigue);
            Check(session.Vitality.IsUnconscious(DiscoveredIdentity)
                  && originalCombat.Participants.Any(value => value.Identity == DiscoveredIdentity)
                  && originalCombat.CurrentParticipant != DiscoveredIdentity && boundaries.Count == 2,
                "unconscious current actor remains enrolled, is skipped once, and emits no boundary");
            Check(originalCombat.Participants.Count(value => value.Identity == DiscoveredIdentity) == 1,
                "round refresh never duplicates the discovered hostile");

            MoveActor(session, loader, DiscoveredIdentity, discovered, discoveredFar);
            CompleteCurrentRound(originalCombat, 3);
            Check(boundaries.Count == 3 && Boundary(boundaries[2], 3, 3000),
                "three completed rounds emit exactly three cumulative boundaries");
            Check(originalCombat.Participants.Count(value => value.Identity == DiscoveredIdentity) == 1
                  && originalCombat.Participants.Any(value => value.Identity == DiscoveredIdentity),
                "out-of-range unconscious participant remains enrolled exactly once");

            RemoveHostiles(originalCombat, pc);
            Check(originalCombat.CurrentParticipant == pc && boundaries.Count == 3,
                "actor removal advances to the PC without fabricating a round boundary");
            Check(originalCombat.EndCombat(pc).Succeeded && boundaries.Count == 3,
                "combat terminates after roster growth and emits no phantom boundary");
            Check(!originalCombat.IsActive && originalCombat.ElapsedCombatTimeMilliseconds == 0,
                "combat termination clears transient roster, engagement and elapsed round state");

            session.Vitality.RestoreFatigue(BearIdentity, originalBearFatigue);
            int beforeRestart = boundaries.Count;
            Check(originalCombat.StartCombat(pc, BearIdentity).Succeeded
                  && originalCombat.RoundNumber == 1
                  && originalCombat.ElapsedCombatTimeMilliseconds == 0
                  && boundaries.Count == beforeRestart,
                "new combat starts with clean round state and no boundary replay");
            Check(originalCombat.EngageParticipant(ExplicitIdentity).Failure
                  == CombatFailure.ParticipantUnavailable,
                "dead actor cannot be newly enrolled");
            Check(originalCombat.EndCombat(pc).Failure == CombatFailure.HostileParticipantActive,
                "eligible engaged hostile still blocks premature combat termination");

            session.Vitality.ApplyHitPointDamage(BearIdentity, 1);
            int committedBearHp = session.Vitality.GetCurrentHitPoints(BearIdentity);
            string activeJson = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(activeJson).Succeeded, "active-combat Save V1 load succeeds");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(session.Combat != originalCombat && !session.Combat.IsActive
                  && session.Combat.Participants.Count == 0
                  && session.Combat.EngagedParticipants.Count == 0
                  && session.Combat.CurrentParticipant.IsNull
                  && session.Combat.RoundNumber == 0
                  && session.Combat.ElapsedCombatTimeMilliseconds == 0
                  && boundaries.Count == beforeRestart,
                "Save V1 load restores no stale roster, engagement, turn, round or boundary");
            Check(session.Vitality.GetCurrentHitPoints(BearIdentity) == committedBearHp,
                "Save V1 preserves committed authoritative vitality while normalizing combat");

            int postLoadBoundaries = 0;
            session.Combat.RoundCompleted += _ => postLoadBoundaries++;
            Check(session.Combat.StartCombat(pc, BearIdentity).Succeeded
                  && session.Combat.RoundNumber == 1
                  && session.Combat.ElapsedCombatTimeMilliseconds == 0
                  && postLoadBoundaries == 0,
                "post-load combat begins cleanly without replaying a boundary");
            RemoveHostiles(session.Combat, pc);
            Check(session.Combat.EndCombat(pc).Succeeded && postLoadBoundaries == 0,
                "clean post-load combat can terminate without fabricating a boundary");

            Check(session.SaveGames.LoadJson(baselineJson).Succeeded,
                "pre-validation authoritative baseline restores");
            yield return null;
            Refresh(out loader, out lifecycle);
            Check(!session.Combat.IsActive
                  && session.Vitality.GetCurrentHitPoints(BearIdentity) == originalBearHp
                  && session.Vitality.GetCurrentFatigue(BearIdentity) == originalBearFatigue,
                "validation cleanup restores original combat and vitality state");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M8G PHASE 1 PLAYMODE VALIDATION PASS: initialRoster={initialOrder.Length}; "
                      + "dynamicDiscovery=exactlyOnce; explicitEngagement=exactlyOnce; "
                      + "ordering=source+pcTail; currentTurn=stable; deadEnrollment=rejected; "
                      + "unconscious=retained+skipped; boundaries=3x1000ms; "
                      + "deathRemoval=noDuplicate; termination=noBoundary; "
                      + "graphics=Original->Enhanced->Original; saveV1=transientNormalized+vitalityPreserved; "
                      + $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            if (discoveredObject != null) Object.Destroy(discoveredObject);
            if (explicitObject != null) Object.Destroy(explicitObject);
            _running = false;
        }
    }

    private static WorldObject AddValidationNpc(WorldMapSessionCoordinator session,
        WorldObjectSectorLoader loader, ArcanumObjectId identity, string name, Vector2Int tile, int sourceOrder)
    {
        const int prototype = 28001;
        int[] stats = Stats();
        var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
            0x28100000u, 0, 0, oid: GuidBytes(identity));
        PersistentObjectState state = session.GetOrCreate(source, FixtureSector, source.CurrentArtId.Value,
            false, false);
        session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype, stats, null);
        session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
            CharacterProgressionSource.Resolve(stats, null, null, null, null, null, OcfAnimal));
        session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
            CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50,
                OnfKos, OcfAnimal));
        session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
            CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0,
                null, 0, null, 0, null, 0));
        var go = new GameObject(name);
        WorldObject runtime = go.AddComponent<WorldObject>();
        runtime.Type = ObjectType.Npc;
        runtime.Tile = tile;
        runtime.TilePosition = tile;
        runtime.ArtId = 0x28100000u;
        runtime.Blocks = true;
        session.Bind(FixtureSector, state, runtime);
        loader.NavigationMap.Register(runtime, 0);
        session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
            FixtureSector, sourceOrder, OnfKos, OcfAnimal, 0));
        return runtime;
    }

    private static void MoveActor(WorldMapSessionCoordinator session, WorldObjectSectorLoader loader,
        ArcanumObjectId identity, WorldObject runtime, Vector2Int tile)
    {
        loader.NavigationMap.Unregister(runtime);
        Check(session.SetMovementState(identity, tile, runtime.ArtId, false),
            "authoritative world state accepts validation movement");
        loader.NavigationMap.Register(runtime, runtime.SourceFlags);
    }

    private static Vector2Int FindNearWalkable(SectorNavigationMap map, Vector2Int origin)
    {
        for (int distance = 2; distance <= 6; distance++)
        for (int y = Math.Max(0, origin.y - distance); y <= Math.Min(63, origin.y + distance); y++)
        for (int x = Math.Max(0, origin.x - distance); x <= Math.Min(63, origin.x + distance); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (Math.Max(Math.Abs(x - origin.x), Math.Abs(y - origin.y)) == distance
                && map.IsWalkable(candidate)) return candidate;
        }
        throw new InvalidOperationException("M8G Phase 1 validation FAIL: no nearby walkable tile.");
    }

    private static Vector2Int FindFarWalkable(SectorNavigationMap map, Vector2Int origin, Vector2Int excluded)
    {
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            var candidate = new Vector2Int(x, y);
            if (candidate != excluded
                && Math.Max(Math.Abs(x - origin.x), Math.Abs(y - origin.y)) > 16
                && map.IsWalkable(candidate)) return candidate;
        }
        throw new InvalidOperationException("M8G Phase 1 validation FAIL: no distant walkable tile.");
    }

    private static ArcanumObjectId[] CheckInitialRoster(CombatStateService combat, ArcanumObjectId pc)
    {
        ArcanumObjectId[] order = combat.Participants.Select(value => value.Identity).ToArray();
        Check(order.Length >= 2 && order[^1] == pc && order.Contains(BearIdentity)
              && order.Distinct().Count() == order.Length
              && combat.CurrentParticipant == order[0]
              && order.All(combat.IsParticipantEngaged),
            "initial discovery produces a unique engaged roster with deterministic PC-tail order");
        return order;
    }

    private static void CheckDeterministicRoster(CombatStateService combat, ArcanumObjectId pc,
        params ArcanumObjectId[] required)
    {
        CombatParticipant[] roster = combat.Participants.ToArray();
        bool sorted = true;
        for (int index = 1; index < roster.Length - 1; index++)
            sorted &= roster[index - 1].SourceOrder <= roster[index].SourceOrder;
        Check(roster.Length >= 2 && roster[^1].Identity == pc && sorted
              && roster.Select(value => value.Identity).Distinct().Count() == roster.Length
              && required.All(identity => roster.Any(value => value.Identity == identity)),
            "participant order is deterministic, source-ordered, PC-tail and duplicate-free");
    }

    private static void CompleteCurrentRound(CombatStateService combat, int expectedRound)
    {
        Check(combat.RoundNumber == expectedRound, $"round {expectedRound} is current before completion");
        int guard = combat.Participants.Count + 1;
        var turns = new HashSet<ArcanumObjectId>();
        while (combat.IsActive && combat.RoundNumber == expectedRound && guard-- > 0)
        {
            ArcanumObjectId actor = combat.CurrentParticipant;
            Check(!actor.IsNull && turns.Add(actor),
                $"round {expectedRound} gives each eligible actor at most one turn");
            Check(combat.EndCurrentTurn(actor).Succeeded,
                $"round {expectedRound} advances through production turn authority");
        }
        Check(combat.IsActive && combat.RoundNumber == expectedRound + 1,
            $"round {expectedRound} completes exactly once");
    }

    private static void AdvanceToActorWithinRound(CombatStateService combat,
        ArcanumObjectId expectedActor, int expectedRound)
    {
        int guard = combat.Participants.Count;
        while (combat.CurrentParticipant != expectedActor && guard-- > 0)
        {
            Check(combat.RoundNumber == expectedRound,
                $"actor {expectedActor} is reached without crossing a round boundary");
            Check(combat.EndCurrentTurn(combat.CurrentParticipant).Succeeded,
                $"production authority advances toward actor {expectedActor}");
        }
        Check(combat.RoundNumber == expectedRound && combat.CurrentParticipant == expectedActor,
            $"actor {expectedActor} owns one deterministic turn in round {expectedRound}");
    }

    private static void RemoveHostiles(CombatStateService combat, ArcanumObjectId pc)
    {
        foreach (ArcanumObjectId identity in combat.Participants.Select(value => value.Identity)
                     .Where(identity => identity != pc).ToArray())
            Check(combat.RemoveParticipant(identity).Succeeded,
                $"participant {identity} is removed through combat authority");
    }

    private static bool Boundary(CombatRoundBoundary boundary, int completedRound, long total)
        => boundary.CompletedRoundNumber == completedRound
           && boundary.ElapsedMilliseconds == CombatStateService.RoundBoundaryMilliseconds
           && boundary.TotalElapsedMilliseconds == total;

    private static int[] Stats()
    {
        var result = new int[CharacterAttributeSet.SourceStatArrayCount];
        for (int index = 0; index < CharacterAttributeSet.Count; index++) result[index] = 8;
        result[CharacterProgressionSource.LevelSourceSlot] = 1;
        result[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
        result[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
        return result;
    }

    private static byte[] GuidBytes(ArcanumObjectId identity)
    {
        string compact = identity.Key.Substring(2).Replace("_", string.Empty);
        var bytes = new byte[ArcanumObjectId.SerializedSize];
        bytes[0] = (byte)ArcanumObjectIdType.Guid;
        for (int index = 0; index < 16; index++)
            bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
        return bytes;
    }

    private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

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

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M8G Phase 1 validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
