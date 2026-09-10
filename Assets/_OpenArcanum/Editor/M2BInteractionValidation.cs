using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M2BInteractionValidation
{
    private const string ScriptedDoorSector = "maps/caladon-panarrii temple/67108865.sec";
    private const int ScriptNum = 1162;
    private const int GateFlag = 2087;
    private static ArcanumObjectId _doorId;
    private static Vector2 _approachStart;
    private static int _warnings, _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/Interaction/Run M2B EditMode Tests")]
    private static void RunTests()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before EditMode tests.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Results(api));
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            categoryNames = new[] { "M2BUseScript" }
        }));
    }

    [MenuItem("OpenArcanum/Interaction/Prepare M2B Scripted Portal Click")]
    private static void PreparePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("Open the TestTerrain scene first.");
        loader.StartCoroutine(Prepare(loader));
    }

    [MenuItem("OpenArcanum/Interaction/Validate M2B Scripted Portal Click")]
    private static void ValidatePhysicalClick()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null || !_doorId.IsPersistent)
            throw new InvalidOperationException("Run Prepare M2B Scripted Portal Click first.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Prepare(WorldObjectSectorLoader loader)
    {
        BeginTracking();
        WorldMapSessionCoordinator session = loader.Session;
        Check(session.SelectSector(ScriptedDoorSector), "coordinator loads the source-authored scripted-door sector");
        yield return null;
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        Camera camera = Camera.main;
        Check(lifecycle != null && navigation != null && interaction != null && camera != null,
            "production PC, navigation, interaction, and camera exist");
        if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

        WorldObjectSpriteOwner owner = loader.SpriteOwners
            .Where(o => o != null && o.WorldObject != null && o.WorldObject.Type == ObjectType.Portal
                && o.WorldObject.UseScriptNum == ScriptNum && o.WorldObject.Identity.IsPersistent
                && !o.WorldObject.Off && !o.WorldObject.Locked && o.WorldObject.PortalOpenable)
            .OrderBy(o => o.WorldObject.Identity.Key, StringComparer.Ordinal)
            .FirstOrDefault(o => TryFindTargetPoint(loader, o, out _));
        Check(owner != null, "real visible Panarii offices portal with SAP_USE 1162 exists");
        _doorId = owner.WorldObject.Identity;
        PersistentObjectState state = session.States[_doorId];
        if (owner.WorldObject.IsOpen)
        {
            Check(session.Portals.Request(_doorId, false), "close scripted portal before validation");
            yield return WaitForPortal(session, _doorId);
        }

        Vector2Int inRange = FindWalkable(loader, owner.WorldObject.Tile, 0, InteractionRangeRules.PortalUseRange);
        Check(inRange.x >= 0 && session.SetMovementState(session.PlayerState.Identity, inRange,
            navigation.Player.ArtId, false), "place PC for skip-default probe");
        session.ScriptGlobals.SetFlag(GateFlag, 0);
        WorldInteractionResult suppressed = interaction.TryUse(_doorId);
        Check(suppressed.IsSuccess && suppressed.ScriptNum == ScriptNum && suppressed.ScriptRunDefault == false
              && suppressed.RequestedPortalOpen == null && !state.PortalOpen && session.Portals.ActiveCount == 0,
            "authentic clear-flag branch executes and suppresses built-in portal behavior");

        session.ScriptGlobals.SetFlag(GateFlag, 1);
        Vector2Int far = FindWalkable(loader, owner.WorldObject.Tile, 8, 28);
        Check(far.x >= 0 && session.SetMovementState(session.PlayerState.Identity, far,
            navigation.Player.ArtId, false), "place PC at a distant reachable approach start");
        _approachStart = session.PlayerState.MapPosition;
        Check(TryFindTargetPoint(loader, owner, out Vector3 worldPoint), "scripted portal has a visible hit pixel");
        camera.transform.position = new Vector3(worldPoint.x, worldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        Debug.Log($"M2B PHYSICAL READY: sector={session.SelectedSector}; oid={_doorId}; script={ScriptNum}; " +
            $"flag={GateFlag}=1; player={session.PlayerState.Identity}; world={worldPoint}; screen={screenPoint}.");
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(input != null && interaction != null && navigation != null && lifecycle != null,
            "production M2B composition exists");
        Check(input.LastClickedObject == _doorId && input.LastClickAccepted,
            $"physical click selects and accepts the stable scripted-portal ObjectID " +
            $"(expected={_doorId}, actual={input.LastClickedObject?.ToString() ?? "<ground>"}, " +
            $"result={input.LastInteractionResult?.Code.ToString() ?? "<none>"})");

        float deadline = Time.realtimeSinceStartup + 20f;
        while ((interaction.Phase == PlayerInteractionPhase.ApproachingTarget || navigation.IsMoving)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "physical approach completes before timeout");
        Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess
              && interaction.LastResult.Value.ScriptNum == ScriptNum
              && interaction.LastResult.Value.ScriptRunDefault == true
              && interaction.LastResult.Value.RequestedPortalOpen == true,
            "arrival executes SAP_USE and its run-default decision reaches the scheduler");
        Check(session.PlayerState.MapPosition != _approachStart, "physical click navigation moved the production PC");
        yield return WaitForPortal(session, _doorId);

        PersistentPlayerState player = session.PlayerState;
        PersistentObjectState door = session.States[_doorId];
        Check(door.PortalOpen, "script-permitted built-in portal transition completes");
        Check(session.ScriptGlobals.GetFlag(GateFlag) == 1, "session-owned script state remains authoritative");

        GraphicsMode mode = OpenArcanumGraphicsSettings.Mode;
        OpenArcanumGraphicsSettings.SetRuntimeMode(mode == GraphicsMode.Original ? GraphicsMode.Enhanced : GraphicsMode.Original);
        loader.RebuildVisuals();
        OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
        loader.RebuildVisuals();
        Check(ReferenceEquals(session.PlayerState, player) && ReferenceEquals(session.States[_doorId], door)
              && door.PortalOpen && session.ScriptGlobals.GetFlag(GateFlag) == 1,
            "visual rebuild preserves PC, portal, script state, and identity");

        string sector = session.SelectedSector;
        session.ClearSelectedSector();
        yield return null;
        Check(session.SelectSector(sector), "coordinator reloads scripted-door sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(ReferenceEquals(session.PlayerState, player) && ReferenceEquals(session.States[_doorId], door)
              && door.PortalOpen && session.ScriptGlobals.GetFlag(GateFlag) == 1,
            "unload/reload preserves identity and authoritative gameplay/script state");
        CheckUnique(loader, navigation, lifecycle);
        StopTracking();
        Check(_warnings == 0 && _errors == 0, $"no new Unity warnings/errors ({_warnings}/{_errors})");
        Debug.Log($"M2B scripted-portal validation PASS: sector={sector}; oid={_doorId}; script={ScriptNum}; " +
            "physical click/approach, stable actor-target context, skip-default and run-default decisions, " +
            "scheduler-only built-in transition, rebuild/reload persistence, and unique ownership verified; " +
            $"warnings={_warnings}; errors={_errors}.");
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
            if (WorldObjectTargetSelector.TrySelectPortal(loader.SpriteOwners, point, out ArcanumObjectId id)
                && id == owner.WorldObject.Identity)
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
        var route = new System.Collections.Generic.List<Vector2Int>();
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            var candidate = new Vector2Int(x, y);
            int distance = InteractionRangeRules.Distance(candidate, target);
            if (distance < min || distance > max || !loader.NavigationMap.IsWalkable(candidate)) continue;
            if (min == 0 || planner.TryPlan(loader.NavigationMap, candidate, target,
                    InteractionRangeRules.PortalUseRange, out _, route)) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static IEnumerator WaitForPortal(WorldMapSessionCoordinator session, ArcanumObjectId id)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (session.Portals.TryGetPhase(id, out PortalPhase phase)
               && (phase == PortalPhase.Opening || phase == PortalPhase.Closing)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "portal transition completes before timeout");
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, PlayerNavigationController navigation,
        ProductionPlayerLifecycle lifecycle)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "one coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "one WorldObjects owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "one interaction controller");
        int players = Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(o => o.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity);
        int owners = loader.SpriteOwners.Count(o => o != null && o.WorldObject != null
            && o.WorldObject.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity);
        Check(players == 1 && owners == 1 && navigation.Player == lifecycle.Presentation,
            "one bound production PC presentation");
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("M2B validation FAIL: " + label);
    }

    private static void BeginTracking()
    {
        StopTracking(); _warnings = 0; _errors = 0;
        Application.logMessageReceived += Track; _tracking = true;
    }
    private static void StopTracking()
    {
        if (!_tracking) return;
        Application.logMessageReceived -= Track; _tracking = false;
    }
    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) _errors++;
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
            TestRunnerApi.SaveResultToFile(result, "Logs/M2B-SAP-Use-EditMode.xml");
            Debug.Log($"M2B SAP_USE FOCUSED EditMode suite: {result.TestStatus}; " +
                $"passed={result.PassCount}; failed={result.FailCount}; skipped={result.SkipCount}.");
            _api.UnregisterCallbacks(this);
            Object.DestroyImmediate(_api);
        }
    }
}
