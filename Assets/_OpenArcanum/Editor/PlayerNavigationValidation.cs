using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using Arcanum.World.Demo;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PlayerNavigationValidation
{
    private static bool _m1BCrossSectorValidationRunning;

    [MenuItem("OpenArcanum/Player Navigation/Run M1B Cross-Sector EditMode Tests")]
    private static void RunM1BCrossSectorTests()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before EditMode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new M1BCrossSectorResults(api));
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "M1BNavigation" }
        }));
    }

    private sealed class M1BCrossSectorResults : ICallbacks
    {
        private readonly TestRunnerApi _api;
        public M1BCrossSectorResults(TestRunnerApi api) => _api = api;
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/M1B-CrossSector-EditMode.xml");
            Debug.Log($"M1B cross-sector FOCUSED EditMode suite: {result.TestStatus}; " +
                $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}.");
            _api.UnregisterCallbacks(this);
            Object.DestroyImmediate(_api);
        }
    }

    [MenuItem("OpenArcanum/Player Navigation/Run M1A Lifecycle EditMode Tests")]
    private static void RunM1LifecycleTests()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before EditMode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new M1LifecycleResults(api));
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "M1Lifecycle" }
        }));
    }

    private sealed class M1LifecycleResults : ICallbacks
    {
        private readonly TestRunnerApi _api;
        public M1LifecycleResults(TestRunnerApi api) => _api = api;
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/M1A-Lifecycle-EditMode.xml");
            Debug.Log($"M1A lifecycle FOCUSED EditMode suite: {result.TestStatus}; " +
                $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}.");
            _api.UnregisterCallbacks(this);
            Object.DestroyImmediate(_api);
        }
    }

    [MenuItem("OpenArcanum/Player Navigation/Run Focused EditMode Tests")]
    private static void RunFocusedTests()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before EditMode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new FocusedResults(api));
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "PlayerNavigation" }
        }));
    }

    private sealed class FocusedResults : ICallbacks
    {
        private readonly TestRunnerApi _api;
        public FocusedResults(TestRunnerApi api) => _api = api;
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/PlayerNavigation-EditMode.xml");
            Debug.Log($"Player navigation FOCUSED EditMode suite: {result.TestStatus}; " +
                $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}.");
            _api.UnregisterCallbacks(this);
            Object.DestroyImmediate(_api);
        }
    }

    [MenuItem("OpenArcanum/Player Navigation/Run Real Sector Validation")]
    private static void RunRealSectorValidation()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        var navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        var clickInput = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        var lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        var session = Object.FindFirstObjectByType<WorldMapSessionCoordinator>();
        if (loader == null || navigation == null || clickInput == null || lifecycle == null || session == null || !loader.IsLoaded)
            throw new InvalidOperationException("Load the TestTerrain real sector first.");
        loader.StartCoroutine(Validate(loader, navigation, clickInput, lifecycle, session));
    }

    [MenuItem("OpenArcanum/Player Navigation/Run M1B Real Cross-Sector Validation")]
    private static void RunM1BRealCrossSectorValidation()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        if (_m1BCrossSectorValidationRunning) throw new InvalidOperationException("M1B validation is already running.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        var navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        var lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        var session = Object.FindFirstObjectByType<WorldMapSessionCoordinator>();
        if (loader == null || navigation == null || lifecycle == null || session == null || !loader.IsLoaded)
            throw new InvalidOperationException("Load the TestTerrain real sector first.");
        loader.StartCoroutine(ValidateM1BCrossSector(loader, navigation, lifecycle, session));
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Player navigation validation FAIL: " + label);
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader, PlayerNavigationController navigation,
        PlayerClickMoveInput clickInput, ProductionPlayerLifecycle lifecycle, WorldMapSessionCoordinator session)
    {
        Check(clickInput.LastClickedTile.HasValue && clickInput.LastClickAccepted,
            "physical Game-view click accepted as a route");
        Check(lifecycle.SpawnAndBind(), "bind deterministic production PC");
        WorldObject player = navigation.Player;
        Check(player != null && player.Type == Arcanum.Formats.Objects.ObjectType.Pc
            && player.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity, "persistent production player");
        PersistentPlayerState playerState = session.PlayerState;
        const uint runtimePoseMask = (0x1Fu << 6) | (0x7u << 11);
        Check((player.ArtId & ~runtimePoseMask) == 0x28100000u,
            "source-valid production PC presentation ART");
        CheckSharedSelectionAndUniqueComposition(loader, navigation, lifecycle, session);
        Vector2Int start = player.Tile;
        Check(loader.NavigationMap.IsWalkable(start), "controlled player removed from static occupancy");

        var finder = new DeterministicTilePathfinder();
        var route = new List<Vector2Int>();
        Vector2Int first = FindReachable(loader.NavigationMap, finder, start, 5, route);
        Check(first != start && route.Count > 0, "reachable real-map destination");
        Check(RouteIsValid(loader.NavigationMap, start, route), "real route obeys every traversed edge");
        Check(navigation.TrySetDestination(first), "begin first route");
        yield return null;

        Vector2Int replacement = FindReachable(loader.NavigationMap, finder, start, 8, route, first);
        Check(replacement != start && replacement != first, "replacement destination");
        Check(navigation.TrySetDestination(replacement), "replace active route");
        Check(navigation.Destination == replacement, "old destination cancelled");

        uint walkArt = navigation.Player.ArtId;
        Check(((walkArt >> 6) & 0x1F) == 1, "WALK action while moving");
        Vector2 beforeRebuild = navigation.Player.TilePosition;
        GraphicsMode savedMode = OpenArcanumGraphicsSettings.Mode;
        OpenArcanumGraphicsSettings.SetRuntimeMode(savedMode == GraphicsMode.Original
            ? GraphicsMode.Enhanced : GraphicsMode.Original);
        loader.RebuildVisuals();
        Check(navigation.Player.TilePosition == beforeRebuild, "graphics rebuild preserves gameplay position");
        Check(ReferenceEquals(session.PlayerState, playerState)
            && navigation.Player.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity,
            "graphics rebuild preserves session-owned PC identity/state");
        CheckSharedSelectionAndUniqueComposition(loader, navigation, lifecycle, session);
        OpenArcanumGraphicsSettings.SetRuntimeMode(savedMode);
        loader.RebuildVisuals();
        yield return null;
        Check(navigation.Player.TilePosition != beforeRebuild || !navigation.IsMoving,
            "movement continues after graphics rebuild");

        Vector2 persisted = navigation.Player.TilePosition;
        var identity = navigation.Player.Identity;
        string sector = session.SelectedSector;
        session.ClearSelectedSector();
        yield return null;
        Check(navigation.Player == null && lifecycle.Presentation == null,
            "sector unload unbinds production presentation");
        Check(CountNamedRoots("WorldObjects") == 0, "sector unload removes object presentation root");
        Check(session.SelectSector(sector), "coordinator reloads terrain and objects");
        yield return null;
        Check(navigation.Player != null && navigation.Player.Identity == identity, "production PC rebound after reload");
        Check(ReferenceEquals(session.PlayerState, playerState), "reload preserves session-owned PC state instance");
        CheckSharedSelectionAndUniqueComposition(loader, navigation, lifecycle, session);
        Check(Vector2.Distance(navigation.Player.TilePosition, persisted) < 0.001f,
            "fractional movement position restored");
        Check(((navigation.Player.ArtId >> 6) & 0x1F) == 0 && !navigation.Player.IsMoving,
            "interrupted movement restores STAND");

        Vector2Int blocked = FindBlocked(loader.NavigationMap);
        Check(blocked.x >= 0 && !navigation.TrySetDestination(blocked), "blocked destination rejected");
        Check(!navigation.IsMoving, "failed route leaves player idle");

        start = navigation.Player.Tile;
        Vector2Int final = FindReachable(loader.NavigationMap, finder, start, 3, route);
        Check(final != start && navigation.TrySetDestination(final), "route after reload");
        double timeout = Time.timeAsDouble + 10;
        while (navigation.IsMoving && Time.timeAsDouble < timeout) yield return null;
        Check(!navigation.IsMoving, "route completes");
        Check(navigation.Player.Tile == final, "destination reached");
        Check(((navigation.Player.ArtId >> 6) & 0x1F) == 0, "STAND action after arrival");
        Debug.Log($"Player navigation real-sector PASS: sector={session.SelectedSector}; " +
            $"player={navigation.Player.Identity}; start={start}; destination={final}; " +
            "shared terrain/object selection, production PC lifecycle, click routing, replacement, source obstacles, " +
            "graphics rebuild, unload/reload, unique presentation ownership and WALK/STAND verified.");
    }

    private static IEnumerator ValidateM1BCrossSector(WorldObjectSectorLoader loader,
        PlayerNavigationController navigation, ProductionPlayerLifecycle lifecycle,
        WorldMapSessionCoordinator session)
    {
        _m1BCrossSectorValidationRunning = true;
        float savedTimeScale = Time.timeScale;
        GraphicsMode savedMode = OpenArcanumGraphicsSettings.Mode;
        try
        {
            Time.timeScale = 8f;
            Check(lifecycle.SpawnAndBind(), "bind deterministic production PC for M1B");
            Check(SectorCoordinate.TryParse(session.SelectedSector, out SectorCoordinate source),
                "parse the selected source sector");
            SectorCoordinate target = source.Neighbor(1, 0);
            Check(loader.SectorExists(target.Path), "real east-adjacent sector exists");

            PersistentPlayerState state = session.PlayerState;
            ArcanumObjectId identity = state.Identity;
            Vector2 sourceStart = navigation.Player.TilePosition;
            uint standArt = CritterArtResolver.WithAnimRotation(navigation.Player.ArtId, 0, 5)
                            & ~(0x1Fu << 14);
            var finder = new DeterministicTilePathfinder();
            var planner = new CrossSectorBoundaryPlanner();
            var route = new List<Vector2Int>();
            CrossSectorBoundaryPlanner.Plan legalPlan = default;
            Vector2Int targetDestination = default;
            bool foundSharedBoundary = false;

            for (int y = 0; y < SectorCoordinate.Size && !foundSharedBoundary; y++)
            {
                Vector2Int probeDestination = Vector2Int.RoundToInt(target.ToGlobal(new Vector2Int(8, y)));
                if (!planner.TryPlan(source, loader.NavigationMap, Vector2Int.RoundToInt(sourceStart),
                        probeDestination, loader.SectorExists, out CrossSectorBoundaryPlanner.Plan plan))
                    continue;
                if (!session.TryTransitionPlayer(target.Path, plan.EntryTile, standArt))
                    continue;
                yield return null;

                bool targetSideOpen = loader.NavigationMap.CanExit(plan.EntryTile, (plan.Rotation + 4) & 7);
                if (targetSideOpen)
                {
                    targetDestination = FindReachableInward(loader.NavigationMap, finder, plan.EntryTile, route);
                    foundSharedBoundary = targetDestination != plan.EntryTile;
                    if (foundSharedBoundary) legalPlan = plan;
                }
                Check(session.TryTransitionPlayer(source.Path, sourceStart, standArt),
                    "return from boundary discovery to the source sector");
                yield return null;
            }

            Check(foundSharedBoundary, "discover a mutually traversable real-sector east boundary");
            Check(ReferenceEquals(session.PlayerState, state) && state.Identity == identity,
                "boundary discovery preserves session-owned PC identity");
            CheckSharedSelectionAndUniqueComposition(loader, navigation, lifecycle, session);
            Dictionary<ArcanumObjectId, PersistentObjectState> sourceStates = session.States
                .Where(pair => pair.Value.SourceSector == source.Path)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<ArcanumObjectId, (uint ArtId, bool Off, bool Locked, bool PortalOpen)> sourceValues =
                sourceStates.ToDictionary(pair => pair.Key,
                    pair => (pair.Value.ArtId, pair.Value.Off, pair.Value.Locked, pair.Value.PortalOpen));

            Vector2Int targetGlobal = Vector2Int.RoundToInt(target.ToGlobal(targetDestination));
            Vector2Int sourceBoundaryGlobal = Vector2Int.RoundToInt(source.ToGlobal(legalPlan.ExitTile));
            yield return RunCrossSectorLeg(loader, navigation, lifecycle, session, state, identity,
                source, target, targetGlobal, targetDestination, true);
            yield return RunCrossSectorLeg(loader, navigation, lifecycle, session, state, identity,
                target, source, sourceBoundaryGlobal, legalPlan.ExitTile, false);
            yield return RunCrossSectorLeg(loader, navigation, lifecycle, session, state, identity,
                source, target, targetGlobal, targetDestination, false);
            yield return RunCrossSectorLeg(loader, navigation, lifecycle, session, state, identity,
                target, source, sourceBoundaryGlobal, legalPlan.ExitTile, false);

            Vector2 beforeRejected = state.MapPosition;
            SectorCoordinate absentWest = source.Neighbor(-1, 0);
            Check(!loader.SectorExists(absentWest.Path), "chosen real west neighbor is absent");
            Vector2Int absentDestination = Vector2Int.RoundToInt(absentWest.ToGlobal(new Vector2Int(63, legalPlan.ExitTile.y)));
            Check(!navigation.TrySetGlobalDestination(absentDestination), "missing/blocked sector edge is rejected");
            Check(session.SelectedSector == source.Path && state.MapPosition == beforeRejected
                && !navigation.IsMoving && !state.Destination.HasValue,
                "rejected edge leaves PC stopped in the valid source sector");
            CheckSharedSelectionAndUniqueComposition(loader, navigation, lifecycle, session);
            Check(sourceStates.All(pair => session.States.TryGetValue(pair.Key, out PersistentObjectState current)
                                           && ReferenceEquals(current, pair.Value)),
                "revisiting the source sector reuses every persistent object state");
            Check(sourceValues.All(pair => session.States.TryGetValue(pair.Key, out PersistentObjectState current)
                                           && current.ArtId == pair.Value.ArtId
                                           && current.Off == pair.Value.Off
                                           && current.Locked == pair.Value.Locked
                                           && current.PortalOpen == pair.Value.PortalOpen),
                "revisiting the source sector preserves persistent object and portal values");

            Debug.Log($"M1B real cross-sector PASS: {source.Path} <-> {target.Path}; " +
                $"player={identity}; boundary={legalPlan.ExitTile}->{legalPlan.EntryTile}; " +
                $"target={targetDestination}; trips=4; shared selection, automatic continuation, " +
                "entry/facing/WALK/STAND, graphics rebuild, blocked edge and unique ownership verified.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(savedMode);
            if (loader != null && loader.IsLoaded) loader.RebuildVisuals();
            Time.timeScale = savedTimeScale;
            _m1BCrossSectorValidationRunning = false;
        }
    }

    private static IEnumerator RunCrossSectorLeg(WorldObjectSectorLoader loader,
        PlayerNavigationController navigation, ProductionPlayerLifecycle lifecycle,
        WorldMapSessionCoordinator session, PersistentPlayerState playerState,
        ArcanumObjectId identity, SectorCoordinate source, SectorCoordinate target,
        Vector2Int globalDestination, Vector2Int localDestination, bool rebuildInTarget)
    {
        Check(session.SelectedSector == source.Path, "cross-sector leg starts in expected sector");
        var planner = new CrossSectorBoundaryPlanner();
        Check(planner.TryPlan(source, loader.NavigationMap,
                Vector2Int.RoundToInt(navigation.Player.TilePosition), globalDestination,
                loader.SectorExists, out CrossSectorBoundaryPlanner.Plan expectedPlan)
              && expectedPlan.TargetSector == target,
            "deterministic route selects the expected adjacent sector");
        Vector2Int expectedEntry = expectedPlan.EntryTile;
        int crossingRotation = expectedPlan.Rotation;
        Check(navigation.TrySetGlobalDestination(globalDestination), "cross-sector route accepted");
        bool sawCrossingWalk = false;
        bool sawTargetEntry = false;
        bool sawTargetMovement = false;
        bool rebuilt = false;
        int targetFrames = 0;
        float deadline = Time.realtimeSinceStartup + 20f;

        while ((!sawTargetEntry || navigation.GlobalDestination.HasValue || navigation.IsMoving)
               && Time.realtimeSinceStartup < deadline)
        {
            WorldObject player = navigation.Player;
            if (player != null && ((player.ArtId >> 6) & 0x1F) == 1
                && CritterArtResolver.RotationOf(player.ArtId) == crossingRotation)
                sawCrossingWalk = true;

            if (session.SelectedSector == target.Path && player != null)
            {
                targetFrames++;
                if (!sawTargetEntry)
                {
                    Check(Vector2.Distance(player.TilePosition, expectedEntry) < 0.001f,
                        "PC appears at the wrapped target-sector entry tile");
                    sawTargetEntry = true;
                }
                if (Vector2.Distance(player.TilePosition, expectedEntry) > 0.05f) sawTargetMovement = true;

                if (rebuildInTarget && !rebuilt && targetFrames > 1)
                {
                    Vector2 position = player.TilePosition;
                    string selectedSector = session.SelectedSector;
                    Vector2Int? destination = playerState.Destination;
                    uint artId = player.ArtId;
                    bool isMoving = player.IsMoving;
                    GraphicsMode mode = OpenArcanumGraphicsSettings.Mode;
                    OpenArcanumGraphicsSettings.SetRuntimeMode(mode == GraphicsMode.Original
                        ? GraphicsMode.Enhanced : GraphicsMode.Original);
                    loader.RebuildVisuals();
                    Check(ReferenceEquals(session.PlayerState, playerState) && playerState.Identity == identity
                        && navigation.Player.Identity == identity
                        && Vector2.Distance(navigation.Player.TilePosition, position) < 0.001f
                        && session.SelectedSector == selectedSector && playerState.Destination == destination
                        && navigation.Player.ArtId == artId && navigation.Player.IsMoving == isMoving,
                        "mid-route graphics rebuild preserves authoritative PC state");
                    CheckSharedSelectionAndUniqueComposition(loader, navigation, lifecycle, session);
                    OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                    loader.RebuildVisuals();
                    Check(Vector2.Distance(navigation.Player.TilePosition, position) < 0.001f
                        && session.SelectedSector == selectedSector && playerState.Destination == destination
                        && navigation.Player.Identity == identity && navigation.Player.ArtId == artId
                        && navigation.Player.IsMoving == isMoving,
                        "restoring graphics mode preserves the full navigation state");
                    rebuilt = true;
                }
            }
            yield return null;
        }

        Check(Time.realtimeSinceStartup < deadline, "cross-sector route completes before timeout");
        Check(session.SelectedSector == target.Path,
            $"coordinator selected target sector (expected {target.Path}, actual {session.SelectedSector})");
        Check(sawTargetEntry, "target entry was observed after coordinator transition");
        Check(sawCrossingWalk, "boundary crossing exposes the correct WALK facing");
        if (navigation.Player != null && Vector2.Distance(navigation.Player.TilePosition, expectedEntry) > 0.05f)
            sawTargetMovement = true;
        Check(localDestination == expectedEntry || sawTargetMovement,
            $"route continues automatically after the target sector loads " +
            $"(entry={expectedEntry}, current={navigation.Player?.TilePosition}, final={localDestination}, " +
            $"globalIntent={navigation.GlobalDestination}, moving={navigation.IsMoving}, path={navigation.LastPathSucceeded})");
        Check(navigation.Player.Tile == localDestination && !navigation.IsMoving
            && !navigation.GlobalDestination.HasValue, "global destination reached and cleared");
        Check(((navigation.Player.ArtId >> 6) & 0x1F) == 0, "PC returns to STAND after arrival");
        Check(ReferenceEquals(session.PlayerState, playerState) && navigation.Player.Identity == identity,
            "same persistent PC identity survives the sector transition");
        CheckSharedSelectionAndUniqueComposition(loader, navigation, lifecycle, session);
    }

    private static Vector2Int FindReachableInward(SectorNavigationMap map,
        DeterministicTilePathfinder finder, Vector2Int entry, List<Vector2Int> route)
    {
        for (int x = Math.Max(8, entry.x + 8); x < 32; x++)
        for (int offset = 0; offset < SectorCoordinate.Size; offset++)
        {
            int y = entry.y + (offset % 2 == 0 ? offset / 2 : -(offset + 1) / 2);
            if (y < 0 || y >= SectorCoordinate.Size) continue;
            var candidate = new Vector2Int(x, y);
            if (finder.TryFindPath(map, entry, candidate, route) && route.Count >= 6) return candidate;
        }
        route.Clear();
        return entry;
    }

    private static void CheckSharedSelectionAndUniqueComposition(WorldObjectSectorLoader loader,
        PlayerNavigationController navigation, ProductionPlayerLifecycle lifecycle,
        WorldMapSessionCoordinator session)
    {
        var terrain = Object.FindFirstObjectByType<TileMapDemo>();
        Check(terrain != null && terrain.PresentedSector == session.SelectedSector
            && loader.PresentedSector == session.SelectedSector,
            "one normalized selection drives terrain and objects");
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one session coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one world-object loader");
        Check(Object.FindObjectsByType<TileMapDemo>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one terrain presentation owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<ProductionPlayerLifecycle>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one production-player lifecycle");
        Check(CountNamedRoots("WorldObjects") == 1, "one WorldObjects presentation root");

        int playerOwners = 0;
        foreach (WorldObjectSpriteOwner owner in loader.SpriteOwners)
            if (owner != null && owner.WorldObject != null
                && owner.WorldObject.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity)
                playerOwners++;
        int runtimePlayers = Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Count(worldObject => worldObject.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity);
        int spriteOwners = Object.FindObjectsByType<WorldObjectSpriteOwner>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Count(owner => owner.WorldObject != null
                            && owner.WorldObject.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity);
        Check(playerOwners == 1 && runtimePlayers == 1 && spriteOwners == 1
              && lifecycle.Presentation == navigation.Player,
            "one production PC presentation and SpriteOwner");
    }

    private static int CountNamedRoots(string name)
    {
        int count = 0;
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
            if (transform.name == name) count++;
        return count;
    }

    private static Vector2Int FindReachable(SectorNavigationMap map, DeterministicTilePathfinder finder,
        Vector2Int start, int minimumDistance, List<Vector2Int> route, Vector2Int excluded = default)
    {
        for (int radius = minimumDistance; radius < 24; radius++)
        for (int y = Math.Max(0, start.y - radius); y <= Math.Min(63, start.y + radius); y++)
        for (int x = Math.Max(0, start.x - radius); x <= Math.Min(63, start.x + radius); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (candidate == start || candidate == excluded
                || Mathf.Max(Mathf.Abs(x - start.x), Mathf.Abs(y - start.y)) < radius)
                continue;
            if (finder.TryFindPath(map, start, candidate, route) && route.Count >= minimumDistance)
                return candidate;
        }
        route.Clear();
        return start;
    }

    private static bool RouteIsValid(SectorNavigationMap map, Vector2Int start, IReadOnlyList<Vector2Int> route)
    {
        Vector2Int current = start;
        foreach (Vector2Int next in route)
        {
            int direction = IsoProjection.DirFromDelta(next.x - current.x, next.y - current.y);
            if (direction < 0 || !map.CanTraverse(current, direction)) return false;
            current = next;
        }
        return true;
    }

    private static Vector2Int FindBlocked(SectorNavigationMap map)
    {
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            if (!map.IsWalkable(new Vector2Int(x, y))) return new Vector2Int(x, y);
        return new Vector2Int(-1, -1);
    }
}
