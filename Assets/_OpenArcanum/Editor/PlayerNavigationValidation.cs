using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.World;
using Arcanum.World.Demo;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PlayerNavigationValidation
{
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
        Check(playerOwners == 1 && lifecycle.Presentation == navigation.Player,
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
