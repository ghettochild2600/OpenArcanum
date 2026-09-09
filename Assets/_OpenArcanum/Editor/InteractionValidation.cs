using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class InteractionValidation
{
    private const string RealDoorSector = "maps/arcanum1-024-fixed/122473678402.sec";
    private static ArcanumObjectId _physicalDoor;
    private static int _newWarnings;
    private static int _newErrors;
    private static bool _trackingLogs;

    [MenuItem("OpenArcanum/Interaction/Run M2A EditMode Tests")]
    private static void RunM2ATests()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before EditMode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results(api));
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "M2AInteraction" }
        }));
    }

    [MenuItem("OpenArcanum/Interaction/Prepare Real Door Physical Click")]
    private static void PreparePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("Open the TestTerrain scene first.");
        loader.StartCoroutine(Prepare(loader));
    }

    [MenuItem("OpenArcanum/Interaction/Validate Physical Click + Scenarios")]
    private static void ValidatePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null || !_physicalDoor.IsPersistent)
            throw new InvalidOperationException("Run Prepare Real Door Physical Click first.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Prepare(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        Check(session.SelectSector(RealDoorSector), "coordinator loads real door sector");
        yield return null;
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        PlayerClickMoveInput clickInput = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        Camera camera = Camera.main;
        Check(lifecycle != null && navigation != null && interaction != null && clickInput != null && camera != null,
            "production input, interaction, navigation, lifecycle, and camera exist");
        Check(lifecycle.SpawnAndBind(), "production PC bound");

        WorldObjectSpriteOwner owner = EligibleDoors(loader)
            .FirstOrDefault(candidate => TryFindTargetPoint(loader, candidate, out _));
        Check(owner != null, "source-authored hittable animated door exists");
        _physicalDoor = owner.WorldObject.Identity;
        if (owner.WorldObject.IsOpen)
        {
            Check(session.Portals.Request(_physicalDoor, false), "close door for physical click");
            yield return WaitForPortal(session, _physicalDoor);
        }

        Vector2Int nearby = FindInRangeTile(loader.NavigationMap, owner.WorldObject.Tile);
        Check(nearby.x >= 0, "walkable in-range PC position exists");
        navigation.CancelRoute();
        uint standArt = CritterArtResolver.WithAnimRotation(navigation.Player.ArtId, 0,
            CritterArtResolver.RotationOf(navigation.Player.ArtId));
        Check(session.SetMovementState(session.PlayerState.Identity, nearby, standArt, false),
            "place PC in source-defined Use range");
        Check(TryFindTargetPoint(loader, owner, out Vector3 worldPoint), "door has deterministic visible hit pixel");
        camera.transform.position = new Vector3(worldPoint.x, worldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Check(WorldObjectTargetSelector.TrySelectPortal(loader.SpriteOwners, worldPoint, out ArcanumObjectId selected)
              && selected == _physicalDoor, "camera-center hit resolves authoritative door identity");

        BeginLogTracking();
        Debug.Log($"M2A PHYSICAL READY: sector={session.SelectedSector}; oid={_physicalDoor}; " +
            $"proto={owner.WorldObject.PrototypeNumber}; art={owner.OriginalAssetPath}; aid=0x{owner.WorldObject.ArtId:X8}; " +
            $"rotation={owner.RequestedRotation}; frames={owner.FrameCount}; fps={owner.FramesPerSecond}; " +
            $"player={session.PlayerState.Identity}; local={nearby}; click=center of Game view.");
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        PlayerClickMoveInput clickInput = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(navigation != null && interaction != null && clickInput != null && lifecycle != null,
            "production M2A composition exists");
        Check(clickInput.LastClickedObject.HasValue && clickInput.LastClickedObject.Value == _physicalDoor,
            $"physical Game-view click selected the authoritative ObjectID " +
            $"(expected={_physicalDoor}, actual={clickInput.LastClickedObject?.ToString() ?? "<ground>"})");
        Check(clickInput.LastInteractionResult.HasValue
              && clickInput.LastInteractionResult.Value.Code == WorldInteractionResultCode.Success,
            "physical click issued successful authoritative Use");
        yield return WaitForPortal(session, _physicalDoor);

        WorldObjectSpriteOwner doorOwner = FindOwner(loader, _physicalDoor);
        WorldObject door = doorOwner.WorldObject;
        PersistentObjectState doorState = session.States[_physicalDoor];
        int rotation = CritterArtResolver.RotationOf(door.ArtId);
        if ((rotation & 1) == 0) rotation++;
        Check(doorState.PortalOpen && door.IsOpen, "opening ART completed in authoritative Open state");
        Check(loader.NavigationMap.CanTraverse(door.Tile, rotation), "open door edge is passable");

        // Walk through the exact edge that was blocked while the door was closed.
        Vector2Int across = door.Tile + IsoProjection.DirDelta[rotation];
        Check(loader.NavigationMap.IsWalkable(door.Tile) && loader.NavigationMap.IsWalkable(across),
            "both sides of real door are walkable");
        navigation.CancelRoute();
        Check(session.SetMovementState(session.PlayerState.Identity, door.Tile, navigation.Player.ArtId, false),
            "place PC on near side of door edge");
        Check(navigation.TrySetDestination(across), "navigation accepts ground beyond open doorway");
        yield return WaitForNavigation(navigation);
        Check(navigation.Player.Tile == across, "PC walks through the formerly blocked edge");

        // Source Use toggles this ordinary unscripted portal; closing restores blocking.
        WorldInteractionResult closing = interaction.TryUse(_physicalDoor);
        Check(closing.IsSuccess && closing.RequestedPortalOpen == false, "second Use requests Close");
        yield return WaitForPortal(session, _physicalDoor);
        Check(!doorState.PortalOpen && !loader.NavigationMap.CanTraverse(door.Tile, rotation),
            "close completes and blocking returns");

        // Begin a distant approach, rebuild both presentation modes mid-intent, then finish Use.
        Vector2Int far = FindDistantApproachTile(loader.NavigationMap, door.Tile);
        Check(far.x >= 0, "reachable distant approach start exists");
        navigation.CancelRoute();
        Check(session.SetMovementState(session.PlayerState.Identity, far, navigation.Player.ArtId, false),
            "place PC at distant reachable tile");
        WorldInteractionResult approaching = interaction.TryUse(_physicalDoor);
        Check(approaching.Code == WorldInteractionResultCode.Approaching
              && interaction.PendingCommand.Value.Target == _physicalDoor, "distant Use retains approach intent");
        PersistentPlayerState playerState = session.PlayerState;
        GraphicsMode savedMode = OpenArcanumGraphicsSettings.Mode;
        OpenArcanumGraphicsSettings.SetRuntimeMode(savedMode == GraphicsMode.Original
            ? GraphicsMode.Enhanced : GraphicsMode.Original);
        loader.RebuildVisuals();
        Check(ReferenceEquals(session.PlayerState, playerState)
              && interaction.PendingCommand.HasValue
              && interaction.PendingCommand.Value.Target == _physicalDoor
              && !doorState.PortalOpen, "graphics rebuild preserves PC, pending intent, and portal state");
        OpenArcanumGraphicsSettings.SetRuntimeMode(savedMode);
        loader.RebuildVisuals();
        yield return WaitForInteraction(interaction, navigation, session, _physicalDoor);
        Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess,
            "arrival automatically executes Use");
        Check(SectorCoordinate.TryParse(doorState.SourceSector, out SectorCoordinate doorSector)
              && InteractionRangeRules.IsWithin(playerState.MapPosition,
                doorSector.ToGlobal(doorState.TilePosition),
                InteractionRangeRules.PortalUseRange), "PC stops within source Use range");
        yield return WaitForPortal(session, _physicalDoor);
        Check(doorState.PortalOpen, "approach-to-range opens real door");

        // Session state and navigation blocking survive full presentation unload/reload.
        string sector = session.SelectedSector;
        ArcanumObjectId playerIdentity = playerState.Identity;
        session.ClearSelectedSector();
        yield return null;
        Check(session.SelectSector(sector), "reload selected sector through coordinator");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        doorOwner = FindOwner(loader, _physicalDoor);
        door = doorOwner.WorldObject;
        rotation = CritterArtResolver.RotationOf(door.ArtId);
        if ((rotation & 1) == 0) rotation++;
        Check(ReferenceEquals(session.PlayerState, playerState) && session.PlayerState.Identity == playerIdentity,
            "reload preserves production PC identity/state instance");
        Check(ReferenceEquals(session.States[_physicalDoor], doorState) && door.IsOpen,
            "reload preserves same door state as Open");
        Check(loader.NavigationMap.CanTraverse(door.Tile, rotation), "reloaded Open door remains passable");
        CheckUniqueComposition(loader, navigation, lifecycle, interaction);

        // Cancellation: an ordinary movement command replaces an active interaction route.
        Check(MoveIntoRange(session, navigation, loader.NavigationMap, door.Tile), "position to close before cancellation");
        Check(interaction.TryUse(_physicalDoor).IsSuccess, "close before cancellation scenario");
        yield return WaitForPortal(session, _physicalDoor);
        far = FindDistantApproachTile(loader.NavigationMap, door.Tile);
        Check(session.SetMovementState(playerIdentity, far, navigation.Player.ArtId, false), "reset distant start");
        Check(interaction.TryUse(_physicalDoor).Code == WorldInteractionResultCode.Approaching,
            "begin cancellable approach");
        Vector2Int manual = FindManualDestination(loader.NavigationMap, far, door.Tile);
        Check(manual.x >= 0 && navigation.TrySetDestination(manual), "ordinary movement replaces interaction route");
        Check(interaction.LastResult.Value.Code == WorldInteractionResultCode.Cancelled,
            "manual movement returns Cancelled");
        yield return WaitForNavigation(navigation);
        Check(!doorState.PortalOpen && session.Portals.ActiveCount == 0,
            "cancelled target never executes later");

        // Replacement: a second stable target cancels the first command deterministically.
        WorldObjectSpriteOwner second = EligibleDoors(loader).FirstOrDefault(o => o.WorldObject.Identity != _physicalDoor);
        Check(second != null, "second real interaction target exists");
        far = FindDistantApproachTile(loader.NavigationMap, door.Tile);
        Check(session.SetMovementState(playerIdentity, far, navigation.Player.ArtId, false), "reset replacement start");
        Check(interaction.TryUse(_physicalDoor).Code == WorldInteractionResultCode.Approaching,
            "begin first replacement intent");
        WorldInteractionResult replacement = interaction.TryUse(second.WorldObject.Identity);
        Check(interaction.LastResult.HasValue
              && (interaction.PendingCommand.HasValue
                  ? interaction.PendingCommand.Value.Target == second.WorldObject.Identity
                  : replacement.Command.Target == second.WorldObject.Identity),
            "second ObjectID replaces first intent");
        interaction.CancelPending();
        yield return null;
        Check(!doorState.PortalOpen && session.Portals.ActiveCount == 0,
            "replaced first target does not execute");

        CheckUniqueComposition(loader, navigation, lifecycle, interaction);
        StopLogTracking();
        Check(_newWarnings == 0 && _newErrors == 0, $"no new Unity warnings/errors ({_newWarnings}/{_newErrors})");
        Debug.Log($"M2A real-door validation PASS: sector={sector}; oid={_physicalDoor}; " +
            $"proto={door.PrototypeNumber}; art={doorOwner.OriginalAssetPath}; " +
            "physical target/Use, ART opening, approach, walk-through, close blocking, persistence, " +
            "cancellation, replacement, graphics rebuild, stable PC identity and unique ownership verified; " +
            $"warnings={_newWarnings}; errors={_newErrors}.");
    }

    private static IEnumerable<WorldObjectSpriteOwner> EligibleDoors(WorldObjectSectorLoader loader)
        => loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null
            && owner.WorldObject.Type == ObjectType.Portal && owner.WorldObject.Identity.IsPersistent
            && !owner.WorldObject.Off && !owner.WorldObject.Locked && owner.WorldObject.UseScriptNum == 0
            && owner.WorldObject.PortalOpenable && !PortalTransitionScheduler.IsWindow(owner.WorldObject.ArtId)
            && owner.FrameCount > PortalTransitionScheduler.OpenFrame(owner.WorldObject.ArtId)
            && owner.FramesPerSecond > 0).OrderBy(owner => owner.WorldObject.Identity.Key, StringComparer.Ordinal);

    private static WorldObjectSpriteOwner FindOwner(WorldObjectSectorLoader loader, ArcanumObjectId id)
        => loader.SpriteOwners.Single(owner => owner.WorldObject != null && owner.WorldObject.Identity == id);

    private static bool TryFindTargetPoint(WorldObjectSectorLoader loader, WorldObjectSpriteOwner owner,
        out Vector3 worldPoint)
    {
        SpriteRenderer renderer = owner.WorldObject.View;
        Sprite sprite = renderer != null ? renderer.sprite : null;
        if (sprite == null) { worldPoint = default; return false; }
        Texture2D texture = sprite.texture;
        Rect rect = sprite.textureRect;
        Bounds bounds = sprite.bounds;
        int step = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(rect.width, rect.height) / 32f));
        for (int y = Mathf.FloorToInt(rect.y); y < Mathf.CeilToInt(rect.yMax); y += step)
        for (int x = Mathf.FloorToInt(rect.x); x < Mathf.CeilToInt(rect.xMax); x += step)
        {
            if (texture != null && texture.isReadable && texture.GetPixel(x, y).a <= 1f / 255f) continue;
            float u = (x + .5f - rect.x) / rect.width;
            float v = (y + .5f - rect.y) / rect.height;
            Vector3 local = new(Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                Mathf.Lerp(bounds.min.y, bounds.max.y, v), 0);
            Vector3 point = renderer.transform.TransformPoint(local);
            if (WorldObjectTargetSelector.TrySelectPortal(loader.SpriteOwners, point, out ArcanumObjectId selected)
                && selected == owner.WorldObject.Identity)
            {
                worldPoint = point;
                return true;
            }
        }
        worldPoint = default;
        return false;
    }

    private static Vector2Int FindInRangeTile(SectorNavigationMap map, Vector2Int target)
    {
        for (int y = Math.Max(0, target.y - InteractionRangeRules.PortalUseRange);
             y <= Math.Min(63, target.y + InteractionRangeRules.PortalUseRange); y++)
        for (int x = Math.Max(0, target.x - InteractionRangeRules.PortalUseRange);
             x <= Math.Min(63, target.x + InteractionRangeRules.PortalUseRange); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (map.IsWalkable(candidate)) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static Vector2Int FindDistantApproachTile(SectorNavigationMap map, Vector2Int target)
    {
        var route = new List<Vector2Int>();
        var planner = new InteractionApproachPlanner();
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) < 8 || !map.IsWalkable(candidate)) continue;
            if (planner.TryPlan(map, candidate, target, InteractionRangeRules.PortalUseRange,
                    out _, route) && route.Count >= 5) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static Vector2Int FindManualDestination(SectorNavigationMap map, Vector2Int start, Vector2Int target)
    {
        var finder = new DeterministicTilePathfinder();
        var route = new List<Vector2Int>();
        for (int radius = 3; radius < 12; radius++)
        for (int y = Math.Max(0, start.y - radius); y <= Math.Min(63, start.y + radius); y++)
        for (int x = Math.Max(0, start.x - radius); x <= Math.Min(63, start.x + radius); x++)
        {
            var candidate = new Vector2Int(x, y);
            if (InteractionRangeRules.Distance(candidate, target) <= InteractionRangeRules.PortalUseRange) continue;
            if (finder.TryFindPath(map, start, candidate, route) && route.Count >= 2) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static bool MoveIntoRange(WorldMapSessionCoordinator session, PlayerNavigationController navigation,
        SectorNavigationMap map, Vector2Int target)
    {
        Vector2Int tile = FindInRangeTile(map, target);
        navigation.CancelRoute();
        return tile.x >= 0 && session.SetMovementState(session.PlayerState.Identity, tile,
            CritterArtResolver.WithAnimRotation(navigation.Player.ArtId, 0,
                CritterArtResolver.RotationOf(navigation.Player.ArtId)), false);
    }

    private static IEnumerator WaitForPortal(WorldMapSessionCoordinator session, ArcanumObjectId id)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (session.Portals.TryGetPhase(id, out PortalPhase phase)
               && (phase == PortalPhase.Opening || phase == PortalPhase.Closing)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "portal transition completes before timeout");
    }

    private static IEnumerator WaitForNavigation(PlayerNavigationController navigation)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (navigation.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "navigation completes before timeout");
    }

    private static IEnumerator WaitForInteraction(PlayerInteractionController interaction,
        PlayerNavigationController navigation, WorldMapSessionCoordinator session, ArcanumObjectId id)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((interaction.Phase == PlayerInteractionPhase.ApproachingTarget || navigation.IsMoving)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "approach interaction completes before timeout");
        yield return WaitForPortal(session, id);
    }

    private static void CheckUniqueComposition(WorldObjectSectorLoader loader,
        PlayerNavigationController navigation, ProductionPlayerLifecycle lifecycle,
        PlayerInteractionController interaction)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one session coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one object loader");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        int players = Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Count(o => o.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity);
        int owners = loader.SpriteOwners.Count(o => o != null && o.WorldObject != null
            && o.WorldObject.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity);
        Check(players == 1 && owners == 1 && navigation.Player == lifecycle.Presentation
              && interaction != null, "one bound production PC presentation");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M2A real-door validation FAIL: " + label);
    }

    private static void BeginLogTracking()
    {
        StopLogTracking();
        _newWarnings = 0;
        _newErrors = 0;
        Application.logMessageReceived += TrackLog;
        _trackingLogs = true;
    }

    private static void StopLogTracking()
    {
        if (!_trackingLogs) return;
        Application.logMessageReceived -= TrackLog;
        _trackingLogs = false;
    }

    private static void TrackLog(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _newWarnings++;
        else if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) _newErrors++;
    }

    private sealed class Results : ICallbacks
    {
        private readonly TestRunnerApi _api;
        public Results(TestRunnerApi api) => _api = api;
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, "Logs/M2A-Interaction-EditMode.xml");
            Debug.Log($"M2A interaction FOCUSED EditMode suite: {result.TestStatus}; " +
                $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}.");
            _api.UnregisterCallbacks(this);
            Object.DestroyImmediate(_api);
        }
    }
}
