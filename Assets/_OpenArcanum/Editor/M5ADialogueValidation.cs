using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.Script;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M5ADialogueValidation
{
    private const string FixtureSector = "maps/arcanum1-024-fixed/68786586569.sec";
    private const string FixtureKey = "G_DF753C8F_B655_D411_8F1D_00A0CC6511C6";
    private const int FixturePrototype = 17232;
    private const int FixtureDialogue = 1760;
    private const int FixtureQuest = 1130;

    private static ArcanumObjectId _npcIdentity;
    private static Vector2 _approachStart;
    private static PersistentObjectState _npcState;
    private static Vector3 _targetWorldPoint;
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M5A/Prepare Authentic NPC Click")]
    private static void PreparePhysicalClick()
    {
        WorldObjectSectorLoader loader = RequirePlayLoader();
        loader.StartCoroutine(Prepare(loader));
    }

    [MenuItem("OpenArcanum/M5A/Prepare Changed-State NPC Click")]
    private static void PrepareSecondPhysicalClick()
    {
        WorldObjectSectorLoader loader = RequirePlayLoader();
        loader.StartCoroutine(PrepareSecond(loader));
    }

    [MenuItem("OpenArcanum/M5A/Validate Authentic Dialogue Lifecycle")]
    private static void ValidatePhysicalLifecycle()
    {
        WorldObjectSectorLoader loader = RequirePlayLoader();
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Prepare(WorldObjectSectorLoader loader)
    {
        BeginTracking();
        WorldMapSessionCoordinator session = loader.Session;
        ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
        PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
        Camera camera = Camera.main;
        Check(lifecycle != null && navigation != null && interaction != null && input != null && camera != null,
            "production player, navigation, interaction, click input, and camera exist");
        Check(session.PlayerState != null, "production PC state exists");
        if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");

        Check(session.TryTransitionPlayer(FixtureSector, new Vector2(1, 1), session.PlayerState.ArtId),
            "coordinator loads Thomgrak's retail sector");
        yield return null;
        loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
        lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
        Check(loader != null && navigation != null && lifecycle != null && lifecycle.Presentation != null,
            "production PC presentation rebinds in the fixture sector");

        _npcState = session.States.Values.Single(state => state.Identity.Key == FixtureKey);
        _npcIdentity = _npcState.Identity;
        Check(_npcState.Type == ObjectType.Npc && _npcState.PrototypeNumber == FixturePrototype
              && _npcState.DialogNum == FixtureDialogue,
            "exact retail Thomgrak NPC and dialogue attachment load");
        Check(session.Campaign.GetPcQuestState(FixtureQuest) == (int)QuestState.Unknown
              && session.Campaign.GetLocalFlag(_npcIdentity, (int)Sap.Dialog, 1) == 0,
            "initial campaign and NPC-local state are clean");
        Check(session.TryGetLoadedObject(_npcIdentity, out WorldObject runtime),
            "Thomgrak has one loaded presentation");
        WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(candidate => candidate != null
            && candidate.WorldObject != null && candidate.WorldObject.Identity == _npcIdentity);
        Check(TryFindTargetPoint(loader, owner, out _targetWorldPoint),
            "Thomgrak has a selectable visible pixel");

        Vector2Int far = FindWalkable(loader, runtime.Tile, 6, 24);
        Check(far.x >= 0 && session.SetMovementState(session.PlayerState.Identity, far,
                navigation.Player.ArtId, false),
            "place PC at a reachable out-of-range talk start");
        _approachStart = session.PlayerState.MapPosition;
        camera.transform.position = new Vector3(_targetWorldPoint.x, _targetWorldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Vector3 screenPoint = camera.WorldToScreenPoint(_targetWorldPoint);
        Debug.Log($"M5A PHYSICAL READY: sector={session.SelectedSector}; oid={_npcIdentity}; " +
                  $"proto={_npcState.PrototypeNumber}; dialog={_npcState.DialogNum}; start={_approachStart}; " +
                  $"npcTile={runtime.Tile}; world={_targetWorldPoint}; screen={screenPoint}; click=center of Game view.");

        float deadline = Time.realtimeSinceStartup + 60f;
        while (session.Dialogue.Phase != DialogueSessionPhase.AwaitingPlayerChoice
               && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, "physical click opens dialogue before timeout");
        Check(input.LastClickedObject == _npcIdentity && input.LastClickAccepted
              && input.LastInteractionResult.HasValue
              && input.LastInteractionResult.Value.Command.Type == WorldInteractionCommandType.Talk,
            "literal Game-view click selects Thomgrak's stable ObjectID and submits Talk");
        Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess
              && interaction.LastResult.Value.Command.Target == _npcIdentity,
            "arrival executes one authoritative Talk command");
        Check(session.PlayerState.MapPosition != _approachStart,
            "physical click moves the production PC through ordinary navigation");
        Check(session.Dialogue.NpcIdentity == _npcIdentity
              && session.Dialogue.PcIdentity == session.PlayerState.Identity
              && session.Dialogue.DialogueNumber == FixtureDialogue
              && session.Dialogue.CurrentLine == 1,
            "authentic SAP_DIALOG opens initial node 1 with stable participants");
        Check(session.Dialogue.AvailableResponses.Select(line => line.Num)
                .SequenceEqual(new[] { 2, 11, 12, 19 }),
            "initial authentic response set is deterministic");
        Debug.Log("M5A INITIAL DIALOGUE PASS: choose response 2 (authored line 11), then response 1 twice to accept and close.");
    }

    private static IEnumerator PrepareSecond(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        Check(_npcIdentity.IsPersistent && session.Dialogue.Phase == DialogueSessionPhase.Completed,
            "initial authentic conversation ended normally");
        Check(session.Campaign.GetLocalFlag(_npcIdentity, (int)Sap.Dialog, 1) == 1
              && session.Campaign.GetPcQuestState(FixtureQuest) == (int)QuestState.Accepted,
            "authored response committed local flag 1 and quest 1130 Accepted exactly once");
        Check(session.TryGetLoadedObject(_npcIdentity, out _), "Thomgrak remains loaded for second interaction");
        Camera camera = Camera.main;
        camera.transform.position = new Vector3(_targetWorldPoint.x, _targetWorldPoint.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
        yield return null;
        Debug.Log("M5A CHANGED-STATE CLICK READY: click center of Game view again, then run Validate Authentic Dialogue Lifecycle.");
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        try
        {
            WorldMapSessionCoordinator session = loader.Session;
            PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            Check(input != null && interaction != null && navigation != null && lifecycle != null,
                "production M5A composition remains present");
            Check(input.LastClickedObject == _npcIdentity && input.LastClickAccepted,
                "second literal click again selects the exact stable NPC ObjectID");
            Check(session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice
                  && session.Dialogue.CurrentLine == 140
                  && session.Dialogue.AvailableResponses.Any(line => line.Num == 143)
                  && session.Dialogue.AvailableResponses.All(line => line.Num != 145),
                "changed state selects authentic second-conversation node 140 and Accepted branch 143");
            Check(session.Campaign.GetPcQuestState(FixtureQuest) == (int)QuestState.Accepted,
                "second dialogue start does not duplicate quest mutation");

            int activeLine = session.Dialogue.CurrentLine;
            Check(interaction.TryTalk(_npcIdentity).Code == WorldInteractionResultCode.DialogueBusy
                  && session.Dialogue.CurrentLine == activeLine,
                "repeated Talk cannot duplicate an active dialogue session");
            RebuildBothModes(loader, initialMode);
            yield return null;
            Check(session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice
                  && session.Dialogue.CurrentLine == activeLine
                  && session.Campaign.GetPcQuestState(FixtureQuest) == (int)QuestState.Accepted,
                "Original to Enhanced to Original rebuild preserves active dialogue and campaign state");
            CheckUnique(loader, navigation, lifecycle, session);

            PersistentObjectState retainedNpc = _npcState;
            Check(session.ReloadSelectedSector(), "active-dialogue sector unload/reload succeeds");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            Check(session.Dialogue.Phase == DialogueSessionPhase.Cancelled,
                "sector unload deterministically cancels active dialogue");
            Check(ReferenceEquals(session.States[_npcIdentity], retainedNpc)
                  && session.Campaign.GetLocalFlag(_npcIdentity, (int)Sap.Dialog, 1) == 1
                  && session.Campaign.GetPcQuestState(FixtureQuest) == (int)QuestState.Accepted,
                "NPC reload retains the same authoritative NPC and campaign state");
            Check(session.TryGetLoadedObject(_npcIdentity, out WorldObject reloadedNpc),
                "NPC reload restores one presentation");

            WorldInteractionResult restart = interaction.TryTalk(_npcIdentity);
            Check(restart.IsSuccess && session.Dialogue.CurrentLine == 140,
                "post-reload dialogue still selects the authentic changed-state branch");
            Check(session.Dialogue.Cancel("M5A post-reload proof complete."),
                "post-reload dialogue cancels deterministically");

            Vector2Int far = FindWalkable(loader, reloadedNpc.Tile, 6, 24);
            Vector2Int replacement = FindWalkable(loader, reloadedNpc.Tile, 2, 4);
            Check(far.x >= 0 && replacement.x >= 0
                  && session.SetMovementState(session.PlayerState.Identity, far, navigation.Player.ArtId, false),
                "place PC for approach cancellation proof");
            Check(interaction.TryTalk(_npcIdentity).Code == WorldInteractionResultCode.Approaching,
                "out-of-range Talk begins an ordinary approach");
            Check(navigation.TrySetDestination(replacement)
                  && interaction.LastResult.HasValue
                  && interaction.LastResult.Value.Code == WorldInteractionResultCode.Cancelled,
                "replacement navigation cancels the pending Talk approach");

            Check(session.SetMovementState(session.PlayerState.Identity, far, navigation.Player.ArtId, false)
                  && interaction.TryTalk(_npcIdentity).Code == WorldInteractionResultCode.Approaching,
                "second pending Talk begins for target-loss proof");
            Check(session.UnbindPresentation(FixtureSector, _npcIdentity),
                "NPC presentation is removed before approach arrival");
            yield return null;
            Check(interaction.LastResult.HasValue
                  && interaction.LastResult.Value.Code == WorldInteractionResultCode.TargetNotFound
                  && !session.Dialogue.IsBusy,
                "target loss cancels pending Talk without starting dialogue");
            Check(session.ReloadSelectedSector(), "fixture reload restores target after target-loss proof");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            Check(session.Campaign.GetPcQuestState(FixtureQuest) == (int)QuestState.Accepted
                  && session.Campaign.GetLocalFlag(_npcIdentity, (int)Sap.Dialog, 1) == 1,
                "all lifecycle operations preserve authoritative campaign state");
            CheckUnique(loader, navigation, lifecycle, session);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M5A PLAYMODE VALIDATION PASS: sector={FixtureSector}; oid={_npcIdentity}; " +
                      $"proto={FixturePrototype}; dialog={FixtureDialogue}; initialLine=1; chosenLine=11; " +
                      $"quest={FixtureQuest}:Accepted; localFlag1=1; secondLine=140; " +
                      $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static WorldObjectSectorLoader RequirePlayLoader()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        return Object.FindFirstObjectByType<WorldObjectSectorLoader>()
               ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
    }

    private static void RebuildBothModes(WorldObjectSectorLoader loader, GraphicsMode initial)
    {
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
            if (!WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, point,
                    out ArcanumObjectId id, out ObjectType type)
                || id != owner.WorldObject.Identity || type != ObjectType.Npc) continue;
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
                    InteractionRangeRules.TalkApproachRange, out _, route)) return candidate;
        }
        return new Vector2Int(-1, -1);
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, PlayerNavigationController navigation,
        ProductionPlayerLifecycle lifecycle, WorldMapSessionCoordinator session)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one world-object owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        Check(Object.FindObjectsByType<ProductionDialoguePresenter>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one dialogue presenter");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(root => root.name == "WorldObjects") == 1, "one sector object root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1,
            "one production PC presentation");
        Check(loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                               && owner.WorldObject.Identity == _npcIdentity) == 1,
            "one Thomgrak presentation");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no orphan sprite owners");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
        Check(navigation.Player == lifecycle.Presentation, "navigation remains bound only to the production PC");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M5A validation FAIL: " + label);
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
