using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PlayerNavigationValidation
{
    [MenuItem("OpenArcanum/Player Navigation/Run Focused EditMode Tests")]
    private static void RunFocusedTests()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before EditMode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new FocusedResults());
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "PlayerNavigation" }
        }));
    }

    private sealed class FocusedResults : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/PlayerNavigation-EditMode.xml");
            Debug.Log($"Player navigation FOCUSED EditMode suite: {result.TestStatus}; " +
                $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}.");
        }
    }

    [MenuItem("OpenArcanum/Player Navigation/Run Real Sector Validation")]
    private static void RunRealSectorValidation()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        var navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        if (loader == null || navigation == null || !loader.IsLoaded)
            throw new InvalidOperationException("Load the TestTerrain real sector first.");
        loader.StartCoroutine(Validate(loader, navigation));
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Player navigation validation FAIL: " + label);
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader, PlayerNavigationController navigation)
    {
        navigation.EnableDevelopmentNpcFallback();
        Check(navigation.TryBindConfiguredPlayer(), "bind stable real NPC fallback");
        WorldObject player = navigation.Player;
        Check(player != null && player.Identity.IsPersistent, "persistent runtime player");
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
        OpenArcanumGraphicsSettings.SetRuntimeMode(savedMode);
        loader.RebuildVisuals();
        yield return null;
        Check(navigation.Player.TilePosition != beforeRebuild || !navigation.IsMoving,
            "movement continues after graphics rebuild");

        Vector2 persisted = navigation.Player.TilePosition;
        string sector = loader.CurrentSector;
        Check(loader.UnloadSector() > 0, "unload moving sector");
        yield return null;
        Check(loader.LoadSector(sector), "reload moving sector");
        yield return null;
        Check(navigation.TryBindConfiguredPlayer(), "rebind after reload");
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
        Debug.Log($"Player navigation real-sector PASS: sector={loader.CurrentSector}; " +
            $"player={navigation.Player.Identity}; start={start}; destination={final}; " +
            "click routing, replacement, source obstacles, graphics rebuild, unload/reload, WALK/STAND verified.");
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
