using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M3BInventoryValidation
{
    private const string GroundSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string GroundItemKey = "G_8781D726_74FE_0846_AD0A_88EE591B6383";
    private const string ContainerSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string ContainerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";

    private static ArcanumObjectId _itemId;
    private static Vector2 _approachStart;
    private static PersistentObjectState _itemState;
    private static int _placementChanges;
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M3B/Prepare Real Pickup Click _F8")]
    private static void PreparePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("Open the TestTerrain scene first.");
        loader.StartCoroutine(Prepare(loader));
    }

    [MenuItem("OpenArcanum/M3B/Validate Real Pickup Lifecycle _F9")]
    private static void ValidatePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null || !_itemId.IsPersistent)
            throw new InvalidOperationException("Run Prepare Real Pickup Click first.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Prepare(WorldObjectSectorLoader loader)
    {
        BeginTracking();
        WorldMapSessionCoordinator session = loader.Session;
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        Camera camera = Camera.main;
        Check(lifecycle != null && navigation != null && interaction != null && camera != null,
            "production PC, navigation, interaction, and camera exist");
        Check(session.PlayerState != null, "production PC state exists");
        if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

        Check(session.TryTransitionPlayer(GroundSector, new Vector2(10, 46), session.PlayerState.ArtId),
            "coordinator moves the same production PC into the fixture sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && navigation != null && lifecycle != null && lifecycle.Presentation != null,
            "production presentation rebinds in the fixture sector");

        _itemState = session.States.Values.Single(state => state.Identity.Key == GroundItemKey);
        _itemId = _itemState.Identity;
        Check(_itemState.Type == ObjectType.Food && _itemState.PrototypeNumber == 10078,
            "exact source-authored food fixture");
        Check(_itemState.TilePosition == new Vector2(10, 46) && _itemState.Placement.Sector == GroundSector,
            "fixture begins at its source world placement");
        Check(_itemState.UseScriptNum == 0 && (_itemState.ItemFlags & 0x20) == 0,
            "fixture has no supported use hook and is not OIF_NO_DROP");
        Check(session.TryGetLoadedObject(_itemId, out WorldObject runtime), "fixture has one world presentation");
        Check(!runtime.Blocks && loader.NavigationMap.IsWalkable(runtime.Tile),
            "this OIF_NO_BLOCK food fixture does not add navigation occupancy");
        WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(candidate => candidate != null
            && candidate.WorldObject != null && candidate.WorldObject.Identity == _itemId);
        Check(TryFindTargetPoint(loader, owner, out Vector3 worldPoint), "fixture has a selectable visible pixel");

        Vector2Int far = FindWalkable(loader, runtime.Tile, 6, 24);
        Check(far.x >= 0 && session.SetMovementState(session.PlayerState.Identity, far,
            navigation.Player.ArtId, false), "place PC at a reachable out-of-range approach start");
        _approachStart = session.PlayerState.MapPosition;
        _placementChanges = 0;
        session.ObjectPlacementChanged -= CountPlacementChange;
        session.ObjectPlacementChanged += CountPlacementChange;

        camera.transform.position = new Vector3(worldPoint.x, worldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        Debug.Log($"M3B PHYSICAL READY: sector={session.SelectedSector}; oid={_itemId}; type={_itemState.Type}; " +
                  $"proto={_itemState.PrototypeNumber}; itemFlags=0x{_itemState.ItemFlags:X8}; " +
                  $"player={session.PlayerState.Identity}; start={_approachStart}; world={worldPoint}; " +
                  $"screen={screenPoint}; click=center of Game view, then press F9.");
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(input != null && interaction != null && navigation != null && lifecycle != null,
            "production M3B composition exists");
        Check(input.LastClickedObject == _itemId && input.LastClickAccepted,
            $"physical Game-view click selects the stable item ObjectID (expected={_itemId}, " +
            $"actual={input.LastClickedObject?.ToString() ?? "<ground>"}, " +
            $"result={input.LastInteractionResult?.Code.ToString() ?? "<none>"})");

        float deadline = Time.realtimeSinceStartup + 20f;
        while ((interaction.Phase == PlayerInteractionPhase.ApproachingTarget || navigation.IsMoving)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "physical pickup approach completes before timeout");
        Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess
              && interaction.LastResult.Value.Command.Type == WorldInteractionCommandType.PickUp
              && interaction.LastResult.Value.Command.Target == _itemId,
            "arrival executes one authoritative PickUp command");
        Check(session.PlayerState.MapPosition != _approachStart, "physical click navigation moved the production PC");
        Check(SectorCoordinate.TryParse(GroundSector, out SectorCoordinate groundCoordinate),
            "fixture sector coordinate parses");
        Check(InteractionRangeRules.Distance(session.PlayerState.MapPosition,
                  groundCoordinate.ToGlobal(new Vector2(10, 46))) == 0,
            "source range zero places the PC on the exact item tile");
        Check(_placementChanges == 1, "literal pickup commits exactly one placement mutation");
        Check(_itemState.Placement == ObjectPlacement.ContainedBy(session.PlayerState.Identity)
              && ProjectionCount(loader, _itemState) == 0,
            "pickup removes the world presentation and parents authoritative state to the PC");
        Check(!session.TryGetLoadedObject(_itemId, out _)
              && loader.NavigationMap.IsWalkable(new Vector2Int(10, 46)),
            "pickup removes the item runtime; non-blocking source navigation remains walkable");

        PersistentPlayerState player = session.PlayerState;
        PersistentObjectState item = _itemState;
        ArcanumObjectId itemIdentity = item.Identity;
        RebuildBothModes(loader);
        yield return null;
        Check(ReferenceEquals(session.PlayerState, player) && ReferenceEquals(session.States[itemIdentity], item)
              && item.ParentIdentity == player.Identity && ProjectionCount(loader, item) == 0,
            "visual rebuild preserves PC/item identity and containment");
        Check(session.ReloadSelectedSector(), "reload source sector");
        yield return null;
        Check(item.ParentIdentity == player.Identity && ProjectionCount(loader, item) == 0,
            "source reload does not respawn the collected item");

        Vector2 sourceTile = new(10, 46);
        Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(player.Identity, itemIdentity,
            GroundSector, sourceTile)).IsSuccess, "restore world placement for cancellation proof");
        yield return null;
        Check(session.TryGetLoadedObject(itemIdentity, out WorldObject runtime),
            "cancellation fixture is presented");
        Vector2Int cancellationStart = FindWalkable(loader, runtime.Tile, 6, 24);
        Vector2Int cancellationDestination = FindWalkable(loader, runtime.Tile, 2, 4);
        Check(cancellationStart.x >= 0 && cancellationDestination.x >= 0
              && session.SetMovementState(player.Identity, cancellationStart, navigation.Player.ArtId, false),
            "place PC for cancellation approach");
        int changesBeforeCancellation = _placementChanges;
        Check(interaction.TryPickUp(itemIdentity).Code == WorldInteractionResultCode.Approaching,
            "pickup begins an ordinary out-of-range approach");
        Check(navigation.TrySetDestination(cancellationDestination),
            "ordinary ground movement replaces the pickup approach");
        Check(interaction.LastResult.HasValue
              && interaction.LastResult.Value.Code == WorldInteractionResultCode.Cancelled,
            "replacement movement cancels the pending pickup");
        float cancellationDeadline = Time.realtimeSinceStartup + 10f;
        while (navigation.IsMoving && Time.realtimeSinceStartup < cancellationDeadline) yield return null;
        Check(Time.realtimeSinceStartup < cancellationDeadline,
            "replacement ground movement completes before timeout");
        yield return null;
        Check(item.Placement == ObjectPlacement.InWorld(GroundSector, sourceTile)
              && _placementChanges == changesBeforeCancellation,
            "cancelled approach never commits a stale pickup");
        Check(session.SetMovementState(player.Identity, sourceTile, navigation.Player.ArtId, false)
              && session.ExecuteInteraction(WorldInteractionCommand.PickUp(player.Identity, itemIdentity)).IsSuccess,
            "restore Contained(PC) through the same authoritative command");

        Check(session.TryTransitionPlayer(ContainerSector, new Vector2(36, 58), player.ArtId),
            "PC crosses with the collected item");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        PersistentObjectState container = session.States.Values.Single(state => state.Identity.Key == ContainerKey);
        Check(container.Type == ObjectType.Container && container.PrototypeNumber == 3052,
            "exact real destination container");
        Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(player.Identity, itemIdentity,
            container.Identity)).IsSuccess && item.ParentIdentity == container.Identity,
            "command transfers PC item to the real container");
        Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(player.Identity, itemIdentity,
            player.Identity)).IsSuccess && item.ParentIdentity == player.Identity,
            "command transfers real container item back to the PC");

        Vector2 dropTile = Vector2Int.RoundToInt(player.TilePosition);
        Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(player.Identity, itemIdentity,
            ContainerSector, dropTile)).IsSuccess, "command drops the authored item at the PC tile");
        yield return null;
        Check(item.Identity == itemIdentity && item.Placement == ObjectPlacement.InWorld(ContainerSector, dropTile)
              && ProjectionCount(loader, item) == 1, "drop preserves identity and creates one world presentation");
        Check(session.TryGetLoadedObject(itemIdentity, out WorldObject droppedRuntime)
              && droppedRuntime.Type == ObjectType.Food && droppedRuntime.PrototypeNumber == 10078
              && !droppedRuntime.Blocks && loader.NavigationMap.IsWalkable(droppedRuntime.Tile),
            "drop restores the ordinary source ART/prototype presentation and appropriate navigation state");
        RebuildBothModes(loader);
        Check(ProjectionCount(loader, item) == 1, "rebuild keeps exactly one authored-item presentation");
        Check(session.ReloadSelectedSector(), "reload destination sector");
        yield return null;
        Check(item.Placement == ObjectPlacement.InWorld(ContainerSector, dropTile)
              && ProjectionCount(loader, item) == 1,
            "destination reload restores the foreign source-authored item exactly once");

        Check(session.TryTransitionPlayer(GroundSector, new Vector2(63, 46), player.ArtId),
            "PC returns to original item sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(item.Placement.Sector == ContainerSector && ProjectionCount(loader, item) == 0,
            "original sector does not respawn the relocated source record");
        Check(session.TryTransitionPlayer(ContainerSector, new Vector2(0, 46), player.ArtId),
            "PC returns to destination sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(ProjectionCount(loader, item) == 1, "relocated authored item returns exactly once");

        Vector2 dynamicTile = Vector2Int.RoundToInt(player.TilePosition);
        ItemCreationResult created = session.CreateItem(10078, ObjectPlacement.InWorld(ContainerSector, dynamicTile));
        Check(created.Succeeded && created.State.Identity.Type == ArcanumObjectIdType.SessionDynamic,
            "runtime item receives a session-dynamic identity");
        ArcanumObjectId dynamicIdentity = created.State.Identity;
        Check(session.ExecuteInteraction(WorldInteractionCommand.PickUp(player.Identity, dynamicIdentity)).IsSuccess,
            "runtime item uses the same PickUp command path");
        Vector2 dynamicDrop = new(Mathf.Min(63, dynamicTile.x + 1), dynamicTile.y);
        Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(player.Identity, dynamicIdentity,
            ContainerSector, dynamicDrop)).IsSuccess, "runtime item uses the same Drop command path");
        RebuildBothModes(loader);
        Check(created.State.Identity == dynamicIdentity, "graphics-mode rebuild preserves dynamic identity");
        ItemCreationResult afterRebuild = session.CreateItem(10078, ObjectPlacement.ContainedBy(player.Identity));
        Check(afterRebuild.Succeeded && afterRebuild.State.Identity.Key == "D_0000000000000002",
            "graphics rebuild preserves deterministic dynamic allocation state");
        Check(session.ReloadSelectedSector(), "reload dynamic-item destination sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(created.State.Identity == dynamicIdentity
              && created.State.Placement == ObjectPlacement.InWorld(ContainerSector, dynamicDrop)
              && ProjectionCount(loader, created.State) == 1,
            "dynamic identity/placement survives reload with one presentation");

        CheckUnique(loader, navigation, lifecycle, session);
        session.ObjectPlacementChanged -= CountPlacementChange;
        StopTracking();
        Check(_warnings == 0 && _errors == 0,
            $"no new Unity warnings/errors (warnings={_warnings}, errors={_errors})");
        Debug.Log($"M3B inventory validation PASS: groundSector={GroundSector}; item={itemIdentity}; " +
                  $"type={item.Type}; proto={item.PrototypeNumber}; itemFlags=0x{item.ItemFlags:X8}; " +
                  $"containerSector={ContainerSector}; container={container.Identity}; " +
                  $"containerProto={container.PrototypeNumber}; dynamic={dynamicIdentity}; " +
                  $"afterRebuild={afterRebuild.State.Identity}; warnings={_warnings}; errors={_errors}.");
    }

    private static void CountPlacementChange(PersistentObjectState state, ObjectPlacement _, ObjectPlacement __)
    {
        if (state != null && state.Identity == _itemId) _placementChanges++;
    }

    private static int ProjectionCount(WorldObjectSectorLoader loader, PersistentObjectState state)
        => loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                             && owner.WorldObject.Identity == state.Identity);

    private static void RebuildBothModes(WorldObjectSectorLoader loader)
    {
        GraphicsMode initial = OpenArcanumGraphicsSettings.Mode;
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(initial);
        loader.RebuildVisuals();
    }

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
        bool found = false;
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = default;
        for (int y = Mathf.FloorToInt(rect.y); y < Mathf.CeilToInt(rect.yMax); y += step)
        for (int x = Mathf.FloorToInt(rect.x); x < Mathf.CeilToInt(rect.xMax); x += step)
        {
            if (texture != null && texture.isReadable && texture.GetPixel(x, y).a <= 1f / 255f) continue;
            Vector3 local = new(Mathf.Lerp(bounds.min.x, bounds.max.x, (x + .5f - rect.x) / rect.width),
                Mathf.Lerp(bounds.min.y, bounds.max.y, (y + .5f - rect.y) / rect.height), 0);
            Vector3 point = renderer.transform.TransformPoint(local);
            if (WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, point,
                    out ArcanumObjectId id, out ObjectType type)
                && id == owner.WorldObject.Identity && type == owner.WorldObject.Type)
            {
                float dx = x + .5f - rect.center.x;
                float dy = y + .5f - rect.center.y;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    bestPoint = point;
                }
            }
        }
        worldPoint = bestPoint;
        return found;
    }

    private static Vector2Int FindWalkable(WorldObjectSectorLoader loader, Vector2Int target, int min, int max)
    {
        var planner = new InteractionApproachPlanner();
        var route = new List<Vector2Int>();
        for (int y = 0; y < SectorCoordinate.Size; y++)
        for (int x = 0; x < SectorCoordinate.Size; x++)
        {
            var candidate = new Vector2Int(x, y);
            int distance = InteractionRangeRules.Distance(candidate, target);
            if (distance < min || distance > max || !loader.NavigationMap.IsWalkable(candidate)) continue;
            if (planner.TryPlan(loader.NavigationMap, candidate, target,
                    InteractionRangeRules.ItemPickupRange, out _, route)) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, PlayerNavigationController navigation,
        ProductionPlayerLifecycle lifecycle, WorldMapSessionCoordinator session)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one WorldObjects owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(root => root.name == "WorldObjects") == 1, "one sector object root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1,
            "one production PC runtime");
        Check(loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                               && owner.WorldObject.Identity == session.PlayerState.Identity) == 1,
            "one production PC sprite owner");
        Check(navigation.Player == lifecycle.Presentation, "navigation binds only to production PC");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no duplicate or orphan sprite owners");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
                  .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M3B validation FAIL: " + label);
    }

    private static void BeginTracking()
    {
        StopTracking();
        _warnings = 0;
        _errors = 0;
        Application.logMessageReceived += Track;
        _tracking = true;
    }

    private static void StopTracking()
    {
        if (!_tracking) return;
        Application.logMessageReceived -= Track;
        _tracking = false;
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) _errors++;
    }
}
