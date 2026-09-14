using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M6CManualSaveLoadValidation
{
    private const string Ground = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string FoodKey = "G_8781D726_74FE_0846_AD0A_88EE591B6383";
    private static int _warnings, _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M6C/Prepare Manual UI PlayMode Validation")]
    private static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        string directory = Path.Combine(Application.temporaryCachePath,
            "OpenArcanum-M6C-Physical-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        BeginTracking();
        try
        {
            WorldMapSessionCoordinator session = loader.Session;
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(Ground), "real food sector loads");
            yield return null;

            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            ProductionSaveLoadPresenter presenter = Object.FindFirstObjectByType<ProductionSaveLoadPresenter>();
            PlayerInputGate gate = Object.FindFirstObjectByType<PlayerInputGate>();
            PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            PlayerClickMoveInput clickInput = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            Camera camera = Camera.main;
            Check(loader != null && presenter != null && gate != null && navigation != null && interaction != null
                  && clickInput != null && lifecycle != null && lifecycle.Presentation != null && camera != null,
                "production composition exists");

            var slots = new SessionSaveSlotService(session, directory);
            presenter.BindSlotOperations(slots);
            presenter.Close();
            File.WriteAllText(Path.Combine(directory, "m6ccorrupt" + SessionSaveSlotService.SlotFileExtension),
                "{corrupt");

            PersistentObjectState food = session.States.Values.Single(value => value.Identity.Key == FoodKey);
            ArcanumObjectId pc = session.PlayerState.Identity;
            Vector2Int foodTile = Vector2Int.RoundToInt(food.Placement.TilePosition);
            Vector2Int start = FindReachablePickupStart(loader, foodTile);
            Check(start.x >= 0 && session.SetMovementState(pc, start, session.PlayerState.ArtId, false),
                "PC starts beside real food");
            yield return null;
            Vector2Int boundStart = Vector2Int.RoundToInt(navigation.Player.TilePosition);
            Check(boundStart == start,
                $"bound production PC starts at the authoritative tile {start} (runtime={boundStart})");
            Vector3 playerWorld = lifecycle.Presentation.transform.position;
            camera.transform.position = new Vector3(playerWorld.x, playerWorld.y, camera.transform.position.z);
            if (camera.orthographic) camera.orthographicSize = 3f;
            yield return null;
            Vector2Int moveTarget = FindPhysicalMoveTarget(loader, start);
            Check(moveTarget.x >= 0, "nearby unobscured movement target exists");
            Vector3 moveWorld = loader.transform.TransformPoint(
                IsoProjection.TileToWorld(moveTarget.x, moveTarget.y, loader.PixelsPerUnit));
            Vector3 moveScreen = camera.WorldToScreenPoint(moveWorld);
            Vector2 a = session.PlayerState.MapPosition;
            Debug.Log($"M6C MANUAL READY: press F6, create manual01, close the panel, then click "
                      + $"start={start}; moveTarget={moveTarget}; GameViewScreen={moveScreen}.");

            yield return WaitFor(() => Exists(directory, "manual01"), "manual01 created through UI");
            Debug.Log("M6C MANUAL STEP: Slot A exists. Close the panel and click-move the PC, then create manual02.");
            yield return WaitFor(() => clickInput.LastClickedTile.HasValue || clickInput.LastClickedObject.HasValue,
                "literal post-panel world click reaches production input");
            Check(clickInput.LastClickedTile == moveTarget && clickInput.LastClickAccepted,
                $"literal click selects and accepts target tile {moveTarget} "
                + $"(tile={clickInput.LastClickedTile?.ToString() ?? "<none>"}; "
                + $"object={clickInput.LastClickedObject?.ToString() ?? "<none>"}; accepted={clickInput.LastClickAccepted})");
            Debug.Log($"M6C MANUAL INPUT: tile={clickInput.LastClickedTile}; accepted={clickInput.LastClickAccepted}.");
            yield return WaitFor(() => session.PlayerState.MapPosition != a, "world click moves PC after closing UI");
            yield return WaitFor(() => !navigation.IsMoving, "world click navigation completes before saving B");
            Vector2 b = session.PlayerState.MapPosition;
            yield return WaitFor(() => Exists(directory, "manual02"), "manual02 created through UI");
            DateTime betaWritten = File.GetLastWriteTimeUtc(PathFor(directory, "manual02"));
            Debug.Log("M6C MANUAL STEP: Slot B exists. Load manual01 through the LOAD panel.");

            yield return WaitFor(() => !presenter.IsOpen && session.PlayerState.MapPosition == a,
                "loading A closes UI and restores A position");
            Check(!gate.IsBlocked, "input resumes after loading A");
            Debug.Log("M6C MANUAL STEP: A restored. Open LOAD again, select manual02, and load it.");
            yield return WaitFor(() => !presenter.IsOpen && session.PlayerState.MapPosition == b,
                "loading B closes UI and restores B position");
            Check(!gate.IsBlocked, "input resumes after loading B");
            Debug.Log("M6C MANUAL STEP: B restored. Load m6ccorrupt and observe the error.");

            yield return WaitFor(() => presenter.IsOpen && gate.IsBlocked
                                      && presenter.Controller.SelectedSlotId == "m6ccorrupt"
                                      && !string.IsNullOrEmpty(presenter.Controller.ErrorMessage),
                "corrupt slot error remains visible while B stays active");
            Check(session.PlayerState.MapPosition == b && Exists(directory, "m6ccorrupt"),
                "corrupt load leaves B exact and file visible");
            Debug.Log("M6C MANUAL STEP: Corrupt recovery passed. In SAVE mode overwrite manual02 and confirm.");

            yield return WaitFor(() => File.GetLastWriteTimeUtc(PathFor(directory, "manual02")) > betaWritten,
                "confirmed overwrite updates manual02 through slot service");
            Debug.Log("M6C MANUAL STEP: Overwrite passed. Select manual01, request DELETE, and confirm.");
            yield return WaitFor(() => !Exists(directory, "manual01"), "confirmed delete removes manual01");
            Debug.Log("M6C MANUAL STEP: Delete passed. Request DELETE for manual02, then CANCEL.");
            yield return WaitFor(() => Exists(directory, "manual02")
                                      && presenter.Controller.Confirmation == SaveLoadConfirmation.None
                                      && presenter.Controller.StatusMessage != null
                                      && presenter.Controller.StatusMessage.StartsWith("Cancelled", StringComparison.Ordinal),
                "cancelled delete leaves manual02 intact");
            Vector2Int resumeStart = Vector2Int.RoundToInt(navigation.Player.TilePosition);
            Vector2Int resumeTarget = FindPhysicalMoveTarget(loader, resumeStart);
            Check(resumeTarget.x >= 0, "post-load unobscured movement target exists");
            Vector3 resumedPlayerWorld = lifecycle.Presentation.transform.position;
            camera.transform.position = new Vector3(resumedPlayerWorld.x, resumedPlayerWorld.y,
                camera.transform.position.z);
            if (camera.orthographic) camera.orthographicSize = 3f;
            yield return null;
            Vector3 resumeWorld = loader.transform.TransformPoint(
                IsoProjection.TileToWorld(resumeTarget.x, resumeTarget.y, loader.PixelsPerUnit));
            Debug.Log($"M6C MANUAL STEP: Cancel passed. Close UI, then click-move to {resumeTarget} "
                      + $"at screen={camera.WorldToScreenPoint(resumeWorld)} before clicking the real food.");

            yield return WaitFor(() => !presenter.IsOpen && !gate.IsBlocked
                                      && session.PlayerState.MapPosition != b,
                "world navigation resumes after UI closes");
            Check(session.SetMovementState(pc, a, session.PlayerState.ArtId, false),
                "PC returns to the proven pickup approach after navigation resumes");
            yield return WaitFor(() => Vector2Int.RoundToInt(navigation.Player.TilePosition) == start,
                "bound production PC returns to the proven pickup approach");
            WorldObjectSpriteOwner foodOwner = loader.SpriteOwners.Single(owner => owner?.WorldObject != null
                && owner.WorldObject.Identity == food.Identity);
            Check(TryFindTargetPoint(loader, foodOwner, out Vector3 foodWorld),
                "real food has a selectable visible pixel after load");
            camera.transform.position = new Vector3(foodWorld.x, foodWorld.y, camera.transform.position.z);
            if (camera.orthographic) camera.orthographicSize = 3f;
            yield return null;
            Debug.Log($"M6C MANUAL STEP: Navigation resumed. Click the center of Game view to pick up "
                      + $"the real food at world={foodWorld}; screen={camera.WorldToScreenPoint(foodWorld)}.");
            yield return WaitFor(() => session.States[food.Identity].Placement
                                      == ObjectPlacement.ContainedBy(pc),
                "world interaction resumes after load");

            Check(Object.FindObjectsByType<ProductionSaveLoadPresenter>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1, "one save/load presenter");
            Check(Object.FindObjectsByType<PlayerInputGate>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1, "one input gate");
            Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1, "one session coordinator");
            Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1, "one navigation controller");
            Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
                      FindObjectsSortMode.None).Length == 1, "one interaction controller");
            Check(loader.SpriteOwners.Where(owner => owner?.WorldObject != null)
                .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1), "unique object views");

            StopTracking();
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M6C PLAYMODE VALIDATION PASS: UI-created A/B; positions={a}->{b}->{a}->{b}; "
                      + $"corrupt-visible; overwrite-confirmed; delete-confirmed; delete-cancelled; "
                      + $"navigation+interaction-resumed; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            StopTracking();
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
        }
    }

    private static IEnumerator WaitFor(Func<bool> condition, string label)
    {
        float deadline = Time.realtimeSinceStartup + 600f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, label);
    }

    private static Vector2Int FindWalkable(WorldObjectSectorLoader loader, Vector2Int preferred)
    {
        if (loader.NavigationMap.IsWalkable(preferred)) return preferred;
        for (int radius = 1; radius <= 8; radius++)
            for (int y = Mathf.Max(0, preferred.y - radius); y <= Mathf.Min(63, preferred.y + radius); y++)
                for (int x = Mathf.Max(0, preferred.x - radius); x <= Mathf.Min(63, preferred.x + radius); x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (loader.NavigationMap.IsWalkable(candidate)) return candidate;
                }
        return new Vector2Int(-1, -1);
    }

    private static Vector2Int FindReachablePickupStart(WorldObjectSectorLoader loader, Vector2Int target)
    {
        var planner = new InteractionApproachPlanner();
        var route = new List<Vector2Int>();
        for (int radius = 1; radius <= 8; radius++)
            for (int y = Mathf.Max(0, target.y - radius); y <= Mathf.Min(63, target.y + radius); y++)
                for (int x = Mathf.Max(0, target.x - radius); x <= Mathf.Min(63, target.x + radius); x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (!loader.NavigationMap.IsWalkable(candidate)) continue;
                    if (planner.TryPlan(loader.NavigationMap, candidate, target,
                            InteractionRangeRules.ItemPickupRange, out _, route)) return candidate;
                }
        return new Vector2Int(-1, -1);
    }

    private static Vector2Int FindPhysicalMoveTarget(WorldObjectSectorLoader loader, Vector2Int start)
    {
        var pathfinder = new DeterministicTilePathfinder();
        var route = new List<Vector2Int>();
        for (int radius = 1; radius <= 20; radius++)
            for (int y = Mathf.Max(0, start.y - radius); y <= Mathf.Min(63, start.y + radius); y++)
                for (int x = Mathf.Max(0, start.x - radius); x <= Mathf.Min(63, start.x + radius); x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (candidate == start || !loader.NavigationMap.IsWalkable(candidate)) continue;
                    route.Clear();
                    if (!pathfinder.TryFindPath(loader.NavigationMap, start, candidate, route)
                        || route.Count < 3) continue;
                    Vector3 world = loader.transform.TransformPoint(
                        IsoProjection.TileToWorld(candidate.x, candidate.y, loader.PixelsPerUnit));
                    if (!WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, world,
                            out _, out _)) return candidate;
                }
        return new Vector2Int(-1, -1);
    }

    private static string PathFor(string directory, string id)
        => Path.Combine(directory, id + SessionSaveSlotService.SlotFileExtension);
    private static bool Exists(string directory, string id) => File.Exists(PathFor(directory, id));

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
            if (!WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, point,
                    out ArcanumObjectId id, out ObjectType type)
                || id != owner.WorldObject.Identity || type != owner.WorldObject.Type) continue;
            float dx = x + .5f - rect.center.x;
            float dy = y + .5f - rect.center.y;
            float distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            found = true;
            bestDistance = distance;
            bestPoint = point;
        }
        worldPoint = bestPoint;
        return found;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M6C validation FAIL: " + label);
    }
    private static void BeginTracking()
    {
        StopTracking(); _warnings = 0; _errors = 0; Application.logMessageReceived += Track; _tracking = true;
    }
    private static void StopTracking()
    {
        if (!_tracking) return; Application.logMessageReceived -= Track; _tracking = false;
    }
    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
