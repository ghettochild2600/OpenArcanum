using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M3DStackValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
    private const string AdjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string ContainerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
    private const string StackKey = "G_9239E097_A8D2_C147_9F58_76077340C60E";
    private const string IncompatibleKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
    private static readonly Vector2 FixtureWorldTile = new(12, 18);

    private static PersistentObjectState _stack;
    private static ArcanumObjectId _stackIdentity;
    private static int _sourceQuantity;
    private static Vector2 _approachStart;
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M3D/Prepare Real Stack Click")]
    private static void PreparePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("Open the TestTerrain scene first.");
        loader.StartCoroutine(Prepare(loader));
    }

    [MenuItem("OpenArcanum/M3D/Validate Real Stack Lifecycle")]
    private static void ValidatePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null || !_stackIdentity.IsPersistent)
            throw new InvalidOperationException("Run Prepare Real Stack Click first.");
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

        Check(session.TryTransitionPlayer(FixtureSector, FixtureWorldTile, session.PlayerState.ArtId),
            "coordinator selects the real fixture sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && navigation != null && lifecycle != null && lifecycle.Presentation != null,
            "production presentation rebinds in the fixture sector");

        PersistentObjectState container = session.States.Values.Single(state => state.Identity.Key == ContainerKey);
        _stack = session.States.Values.Single(state => state.Identity.Key == StackKey);
        PersistentObjectState incompatible = session.States.Values.Single(state => state.Identity.Key == IncompatibleKey);
        _stackIdentity = _stack.Identity;
        _sourceQuantity = _stack.StackQuantity.GetValueOrDefault();
        Check(container.Type == ObjectType.Container && container.PrototypeNumber == 3052,
            "exact real source container");
        Check(_stack.Type == ObjectType.Ammo && _stack.PrototypeNumber == 7059 && _sourceQuantity == 60,
            "exact real source-authored ammo stack and instance quantity");
        Check(_stack.ItemFlags == 0 && _stack.UseScriptNum == 0,
            "fixture has no item flags or use script");
        Check(_stack.AuthoredParentIdentity == container.Identity
              && _stack.Placement == ObjectPlacement.ContainedBy(container.Identity),
            "fixture begins in its exact authored container");
        Check(incompatible.Type == ObjectType.Ammo && incompatible.PrototypeNumber == 7058
              && incompatible.StackQuantity == 70 && !session.CanStack(_stack.Identity, incompatible.Identity),
            "real comparison stack has a different prototype and is incompatible");

        Check(session.TransferItem(_stackIdentity, _stack.Placement,
            ObjectPlacement.InWorld(FixtureSector, FixtureWorldTile)).Succeeded,
            "fixture setup moves the authentic stack into the world through M3A");
        yield return null;
        Check(session.TryGetLoadedObject(_stackIdentity, out WorldObject runtime),
            "fixture has one ordinary world presentation");
        Check(ProjectionCount(loader, _stack) == 1, "fixture presentation is unique");
        WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(candidate => candidate != null
            && candidate.WorldObject != null && candidate.WorldObject.Identity == _stackIdentity);
        Check(TryFindTargetPoint(loader, owner, out Vector3 worldPoint), "fixture has a selectable visible pixel");

        Check(session.SetMovementState(session.PlayerState.Identity, runtime.Tile,
            navigation.Player.ArtId, false), "place PC at source pickup range zero");
        _approachStart = session.PlayerState.MapPosition;
        camera.transform.position = new Vector3(worldPoint.x, worldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        Debug.Log($"M3D PHYSICAL READY: sector={FixtureSector}; oid={_stackIdentity}; type={_stack.Type}; " +
                  $"proto={_stack.PrototypeNumber}; quantity={_sourceQuantity}; art=0x{_stack.ArtId:X8}; " +
                  $"itemFlags=0x{_stack.ItemFlags:X8}; use={_stack.UseScriptNum}; " +
                  $"start={_approachStart}; screen={screenPoint}; click=center of Game view, then press F11.");
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(input != null && interaction != null && navigation != null && lifecycle != null,
            "production M3D composition exists");
        Check(input.LastClickedObject == _stackIdentity && input.LastClickAccepted,
            $"literal click selects the stable stack ObjectID (actual={input.LastClickedObject?.ToString() ?? "<ground>"})");

        float deadline = Time.realtimeSinceStartup + 20f;
        while ((interaction.Phase == PlayerInteractionPhase.ApproachingTarget || navigation.IsMoving)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "physical pickup approach completes before timeout");
        Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess
              && interaction.LastResult.Value.Command.Type == WorldInteractionCommandType.PickUp,
            "arrival executes authoritative pickup");
        Check(session.PlayerState.MapPosition == _approachStart,
            "literal click executes at the prepared source range-zero tile");

        PersistentPlayerState player = session.PlayerState;
        PersistentObjectState stack = _stack;
        ArcanumObjectId retainedIdentity = stack.Identity;
        Check(retainedIdentity == _stackIdentity && stack.StackQuantity == _sourceQuantity
              && stack.Placement == ObjectPlacement.ContainedBy(player.Identity)
              && ProjectionCount(loader, stack) == 0,
            "pickup preserves authored identity/quantity and removes presentation");

        PersistentObjectState container = session.States.Values.Single(state => state.Identity.Key == ContainerKey);
        PersistentObjectState incompatible = session.States.Values.Single(state => state.Identity.Key == IncompatibleKey);
        int incompatibleQuantity = incompatible.StackQuantity.GetValueOrDefault();
        Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(player.Identity, incompatible.Identity,
            player.Identity)).IsSuccess, "move real incompatible stack to the same owner");
        ObjectPlacement retainedPlacement = stack.Placement;
        ObjectPlacement incompatiblePlacement = incompatible.Placement;
        StackMergeResult rejected = session.MergeStacks(incompatible.Identity, stack.Identity);
        Check(rejected.Code == StackResultCode.Incompatible
              && stack.StackQuantity == _sourceQuantity
              && incompatible.StackQuantity == incompatibleQuantity
              && stack.Placement == retainedPlacement && incompatible.Placement == incompatiblePlacement,
            "incompatible prototype merge rejects without mutation");
        Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(player.Identity, incompatible.Identity,
            container.Identity)).IsSuccess, "restore incompatible real stack to its container");

        int splitQuantity = 25;
        StackSplitResult split = session.SplitStack(retainedIdentity, splitQuantity);
        Check(split.Succeeded && stack.Identity == retainedIdentity
              && stack.StackQuantity == _sourceQuantity - splitQuantity
              && split.CreatedState.Identity.Key == "D_0000000000000001"
              && split.CreatedState.StackQuantity == splitQuantity
              && split.CreatedState.Placement == ObjectPlacement.ContainedBy(player.Identity)
              && stack.StackQuantity + split.CreatedState.StackQuantity == _sourceQuantity,
            "split preserves authored identity and creates one monotonic D_ stack");
        ArcanumObjectId splitIdentity = split.CreatedState.Identity;
        Check(ProjectionCount(loader, stack) == 0 && ProjectionCount(loader, split.CreatedState) == 0,
            "contained stacks remain authoritative state without duplicate presentations");

        Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(player.Identity, splitIdentity,
            container.Identity)).IsSuccess
              && split.CreatedState.Placement == ObjectPlacement.ContainedBy(container.Identity)
              && split.CreatedState.StackQuantity == splitQuantity,
            "split stack transfers independently from PC to real container");
        Check(session.ExecuteInteraction(WorldInteractionCommand.Transfer(player.Identity, splitIdentity,
            player.Identity)).IsSuccess,
            "container to PC insertion executes through the production transfer command");
        Check(stack.Identity == retainedIdentity && stack.StackQuantity == _sourceQuantity
              && !session.TryGetObjectState(splitIdentity, out _) && session.IsObjectRemoved(splitIdentity),
            "source-style insertion merge keeps destination identity and consumes split source atomically");
        Check(session.ChildrenOf(player.Identity).Count(id => id == retainedIdentity) == 1,
            "merged inventory contains the surviving identity exactly once");

        Vector2 dropTile = Vector2Int.RoundToInt(player.TilePosition);
        Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(player.Identity, retainedIdentity,
            FixtureSector, dropTile)).IsSuccess, "drop accepts the final merged stack");
        yield return null;
        Check(stack.Identity == retainedIdentity && stack.StackQuantity == _sourceQuantity
              && stack.Placement == ObjectPlacement.InWorld(FixtureSector, dropTile)
              && ProjectionCount(loader, stack) == 1,
            "drop preserves total quantity/identity and creates one world presentation");
        Check(session.TryGetLoadedObject(retainedIdentity, out WorldObject droppedRuntime)
              && droppedRuntime.AmmoQuantity == _sourceQuantity,
            "drop projects authoritative ammo quantity into the runtime object cache");

        RebuildBothModes(loader);
        yield return null;
        Check(stack.Identity == retainedIdentity && stack.StackQuantity == _sourceQuantity
              && ProjectionCount(loader, stack) == 1,
            "Original/Enhanced rebuild preserves quantity, identity, and one presentation");
        Check(session.ReloadSelectedSector(), "reload final world placement");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(stack.Identity == retainedIdentity && stack.StackQuantity == _sourceQuantity
              && ProjectionCount(loader, stack) == 1
              && !session.TryGetObjectState(splitIdentity, out _),
            "reload preserves surviving stack and does not resurrect merged split state");
        Check(session.TryGetLoadedObject(retainedIdentity, out WorldObject reloadedRuntime)
              && reloadedRuntime.AmmoQuantity == _sourceQuantity,
            "reload restores authoritative ammo quantity into the runtime object cache");

        Check(session.TryTransitionPlayer(AdjacentSector, new Vector2(0, 18), player.ArtId),
            "production PC crosses A to B with final stack retained in A");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(stack.StackQuantity == _sourceQuantity && ProjectionCount(loader, stack) == 0,
            "foreign sector has no duplicate stack presentation");
        Check(session.TryTransitionPlayer(FixtureSector, new Vector2(63, 18), player.ArtId),
            "production PC crosses B to A");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(stack.Identity == retainedIdentity && stack.StackQuantity == _sourceQuantity
              && ProjectionCount(loader, stack) == 1,
            "A to B to A traversal restores one unchanged stack presentation");

        Check(session.SetMovementState(player.Identity, stack.TilePosition, player.ArtId, false),
            "place PC at dynamic-stack proof tile");
        Check(session.ExecuteInteraction(WorldInteractionCommand.PickUp(player.Identity, retainedIdentity)).IsSuccess,
            "return authored stack to PC containment for dynamic proof");
        ItemCreationResult dynamic = session.CreateItem(7059, ObjectPlacement.ContainedBy(player.Identity));
        Check(dynamic.Succeeded && dynamic.State.Identity.Key == "D_0000000000000002"
              && dynamic.State.StackQuantity == 10,
            "runtime-created compatible stack uses next allocator identity and prototype quantity");
        StackSplitResult dynamicSplit = session.SplitStack(dynamic.State.Identity, 4);
        Check(dynamicSplit.Succeeded && dynamic.State.Identity.Key == "D_0000000000000002"
              && dynamic.State.StackQuantity == 6
              && dynamicSplit.CreatedState.Identity.Key == "D_0000000000000003"
              && dynamicSplit.CreatedState.StackQuantity == 4,
            "dynamic split advances the shared allocator exactly once");
        StackMergeResult dynamicMerge = session.MergeStacks(dynamicSplit.CreatedState.Identity,
            dynamic.State.Identity);
        Check(dynamicMerge.Succeeded && dynamicMerge.SourceConsumed
              && dynamic.State.Identity.Key == "D_0000000000000002"
              && dynamic.State.StackQuantity == 10
              && session.IsObjectRemoved(dynamicSplit.CreatedState.Identity),
            "dynamic merge preserves destination identity and restores total quantity");
        ItemCreationResult afterDynamicSplit = session.CreateItem(7058,
            ObjectPlacement.ContainedBy(player.Identity));
        Check(afterDynamicSplit.Succeeded && afterDynamicSplit.State.Identity.Key == "D_0000000000000004",
            "next creation proves monotonic allocation across split and consumed tombstones");
        RebuildBothModes(loader);
        Check(dynamic.State.Identity.Key == "D_0000000000000002" && dynamic.State.StackQuantity == 10,
            "graphics rebuild cannot mutate dynamic stack state");
        Check(session.ReloadSelectedSector(), "reload dynamic contained stacks");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        Check(dynamic.State.Identity.Key == "D_0000000000000002" && dynamic.State.StackQuantity == 10
              && dynamic.State.Placement == ObjectPlacement.ContainedBy(player.Identity),
            "dynamic stack quantity/identity survives sector reload");

        CheckUnique(loader, navigation, lifecycle, session, stack, dynamic.State, afterDynamicSplit.State);
        StopTracking();
        Check(_warnings == 0 && _errors == 0,
            $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
        Debug.Log($"M3D stack validation PASS: sector={FixtureSector}; stack={retainedIdentity}; " +
                  $"proto={stack.PrototypeNumber}; type={stack.Type}; sourceQuantity={_sourceQuantity}; " +
                  $"container={container.Identity}; incompatible={incompatible.Identity}; " +
                  $"split={splitIdentity}; dynamic={dynamic.State.Identity}; " +
                  $"dynamicSplit={dynamicSplit.CreatedState.Identity}; next={afterDynamicSplit.State.Identity}; " +
                  $"warnings={_warnings}; errors={_errors}.");
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

    private static void CheckUnique(WorldObjectSectorLoader loader, PlayerNavigationController navigation,
        ProductionPlayerLifecycle lifecycle, WorldMapSessionCoordinator session,
        params PersistentObjectState[] retainedStates)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one world-object owner");
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
        foreach (PersistentObjectState state in retainedStates)
            Check(session.States.Values.Count(candidate => candidate.Identity == state.Identity) == 1,
                $"one authoritative state for {state.Identity}");
        Check(session.ChildrenOf(session.PlayerState.Identity).Distinct().Count()
              == session.ChildrenOf(session.PlayerState.Identity).Count,
            "no duplicate ordinary inventory membership");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no duplicate or orphan sprite owners");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M3D validation FAIL: " + label);
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
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
