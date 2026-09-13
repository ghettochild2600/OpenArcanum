using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M5BQuestValidation
{
    private const string DaggerSector = "maps/arcanum1-024-fixed/96435438664.sec";
    private const string MayorSector = "maps/arcanum1-024-fixed/96636765255.sec";
    private const string MayorKey = "G_787AD4AB_9061_2B4E_A691_F582800B2BB3";
    private const string DaggerKey = "G_52E2AC87_1A3B_6842_8C2E_5247C9571D11";
    private const int MayorPrototype = 17088;
    private const int DaggerPrototype = 6071;
    private const int DaggerNameIndex = 2002;
    private const int DialogueNumber = 1009;
    private const int QuestNumber = 1005;

    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M5B/Prepare and Validate Authentic Quest 1005")]
    private static void PrepareAndValidate()
    {
        WorldObjectSectorLoader loader = RequirePlayLoader();
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        BeginTracking();
        try
        {
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

            Check(session.TryTransitionPlayer(DaggerSector, new Vector2(1, 1), session.PlayerState.ArtId),
                "coordinator loads the dagger's authentic authored sector");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            Check(loader != null && navigation != null && lifecycle != null && lifecycle.Presentation != null,
                "production PC rebinds in the dagger sector");
            PersistentObjectState dagger = session.States.Values.Single(state => state.Identity.Key == DaggerKey);
            Check(dagger.PrototypeNumber == DaggerPrototype && dagger.NameIndex == DaggerNameIndex
                  && dagger.Type == ObjectType.Weapon && dagger.Placement.Kind == ObjectPlacementKind.Contained,
                "exact source dagger instance and authored containment load");
            Check(session.TransferItem(dagger.Identity, dagger.Placement,
                    ObjectPlacement.ContainedBy(session.PlayerState.Identity)).Succeeded,
                "existing inventory authority transfers the exact dagger to the PC fixture");
            Check(dagger.Placement.ParentIdentity == session.PlayerState.Identity,
                "the PC authoritatively owns the authentic dagger");

            Check(session.TryTransitionPlayer(MayorSector, new Vector2(1, 1), session.PlayerState.ArtId),
                "coordinator loads the Black Root mayor sector");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            PersistentObjectState mayor = session.States.Values.Single(state => state.Identity.Key == MayorKey);
            Check(mayor.PrototypeNumber == MayorPrototype && mayor.DialogNum == DialogueNumber
                  && mayor.Type == ObjectType.Npc,
                "exact source mayor ObjectID, prototype, and SAP_DIALOG attachment load");
            Check(session.TryGetLoadedObject(mayor.Identity, out WorldObject mayorRuntime),
                "mayor has one loaded presentation");

            session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Mentioned);
            Check(session.Dialogue.Start(session.PlayerState.Identity, mayor.Identity) == DialogueStartStatus.Started,
                "authentic mayor dialogue starts for acceptance setup");
            Check(Choose(session.Dialogue, 3) == DialogueChoiceStatus.Advanced
                  && session.Campaign.GetPcQuestState(QuestNumber) == (int)QuestState.Accepted,
                "authored response 3 establishes authentic Accepted quest state");
            Check(session.Dialogue.Cancel("M5B authentic Accepted-state fixture"),
                "acceptance setup dialogue cancels cleanly");
            int acceptedReaction = session.DerivedStats.GetReaction(mayor.Identity, session.PlayerState.Identity);
            Check(acceptedReaction >= 41,
                "acceptance preserves or raises reaction to the source floor of 41");
            Check(session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry accepted)
                  && accepted.State == QuestState.Accepted && accepted.StateLabel == "Accepted"
                  && !string.IsNullOrWhiteSpace(accepted.Description),
                "read-only journal projects the source Accepted entry");
            QuestTimestamp acceptedTimestamp = accepted.Timestamp;

            int initialLevel = session.Progression.GetLevel(session.PlayerState.Identity);
            int initialPoints = session.Progression.GetUnspentCharacterPoints(session.PlayerState.Identity);
            int initialMaxHp = session.Vitality.GetMaximumHitPoints(session.PlayerState.Identity);
            int initialMaxFatigue = session.Vitality.GetMaximumFatigue(session.PlayerState.Identity);
            int initialCurrentHp = session.Vitality.GetCurrentHitPoints(session.PlayerState.Identity);
            int initialCurrentFatigue = session.Vitality.GetCurrentFatigue(session.PlayerState.Identity);

            WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(candidate => candidate != null
                && candidate.WorldObject != null && candidate.WorldObject.Identity == mayor.Identity);
            Check(TryFindTargetPoint(loader, owner, out Vector3 targetWorldPoint),
                "mayor has a selectable visible pixel");
            Vector2Int far = FindWalkable(loader, mayorRuntime.Tile, 6, 24);
            Check(far.x >= 0 && session.SetMovementState(session.PlayerState.Identity, far,
                    navigation.Player.ArtId, false),
                "place PC at a reachable out-of-range Talk start");
            Vector2 approachStart = session.PlayerState.MapPosition;
            camera.transform.position = new Vector3(targetWorldPoint.x, targetWorldPoint.y, camera.transform.position.z);
            if (camera.orthographic) camera.orthographicSize = 3f;
            yield return null;
            Vector3 screenPoint = camera.WorldToScreenPoint(targetWorldPoint);
            Debug.Log($"M5B PHYSICAL READY: sector={session.SelectedSector}; mayor={mayor.Identity}; " +
                      $"proto={mayor.PrototypeNumber}; dialog={mayor.DialogNum}; dagger={dagger.Identity}; " +
                      $"start={approachStart}; npcTile={mayorRuntime.Tile}; world={targetWorldPoint}; " +
                      $"screen={screenPoint}; click=center of Game view.");

            float deadline = Time.realtimeSinceStartup + 60f;
            while (session.Dialogue.Phase != DialogueSessionPhase.AwaitingPlayerChoice
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Time.realtimeSinceStartup < deadline, "physical click opens authentic mayor dialogue");
            Check(input.LastClickedObject == mayor.Identity && input.LastClickAccepted
                  && input.LastInteractionResult.HasValue
                  && input.LastInteractionResult.Value.Command.Type == WorldInteractionCommandType.Talk,
                "literal Game-view click selects the stable mayor ObjectID and submits Talk");
            Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess
                  && interaction.LastResult.Value.Command.Target == mayor.Identity,
                "arrival executes one authoritative Talk command");
            Check(session.PlayerState.MapPosition != approachStart,
                "physical click moves the production PC through normal navigation");
            Check(session.Dialogue.CurrentLine == 1
                  && session.Dialogue.AvailableResponses.Any(line => line.Num == 4)
                  && session.Dialogue.AvailableResponses.All(line => line.Num != 5),
                "Accepted state exposes response 4 and excludes the Completed-only response");
            Debug.Log("M5B ACCEPTED DIALOGUE PASS: physically choose the dagger response (line 4), " +
                      "the hand-in response (line 102), the 100-gold response (line 431), then goodbye (line 443).");

            deadline = Time.realtimeSinceStartup + 120f;
            while (session.Dialogue.Phase != DialogueSessionPhase.Completed
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Time.realtimeSinceStartup < deadline, "physical quest-1005 conversation completes before timeout");
            Check(session.Campaign.GetPcQuestState(QuestNumber) == (int)QuestState.Completed,
                "quest 1005 becomes Completed");
            Check(dagger.Placement.Kind == ObjectPlacementKind.Contained
                  && dagger.Placement.ParentIdentity == mayor.Identity,
                "authentic dagger transfers to the mayor through inventory authority");
            Check(session.GetGold(session.PlayerState.Identity) == 100,
                "authored positive gold reward creates a 100-quantity Gold stack");
            Check(session.Progression.GetExperience(session.PlayerState.Identity) == 800,
                "source quest XP is awarded exactly once");
            Check(session.Progression.GetLevel(session.PlayerState.Identity) == initialLevel
                  && session.Progression.GetUnspentCharacterPoints(session.PlayerState.Identity) == initialPoints,
                "M4C remains authoritative because 800 XP does not cross the next threshold");
            Check(session.Vitality.GetMaximumHitPoints(session.PlayerState.Identity) == initialMaxHp
                  && session.Vitality.GetMaximumFatigue(session.PlayerState.Identity) == initialMaxFatigue
                  && session.Vitality.GetCurrentHitPoints(session.PlayerState.Identity) == initialCurrentHp
                  && session.Vitality.GetCurrentFatigue(session.PlayerState.Identity) == initialCurrentFatigue,
                "M4B vitality maxima and current damage state remain unchanged without a level transition");
            Check(session.DerivedStats.GetAlignment(session.PlayerState.Identity) == 50
                  && session.DerivedStats.GetReaction(mayor.Identity, session.PlayerState.Identity)
                  == acceptedReaction + 10,
                "source alignment +50 and completion reaction +10 apply exactly once");
            Check(session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry completed)
                  && completed.State == QuestState.Completed && completed.StateLabel == "Completed"
                  && completed.Description == accepted.Description && completed.Timestamp != acceptedTimestamp,
                "journal projects Completed from campaign state with a new source-shaped timestamp");
            QuestTimestamp completedTimestamp = completed.Timestamp;
            PersistentObjectState gold = session.States.Values.Single(state => state.PrototypeNumber == 9056
                && state.Placement.Kind == ObjectPlacementKind.Contained
                && state.Placement.ParentIdentity == session.PlayerState.Identity);
            Check(gold.Type == ObjectType.Gold && gold.StackQuantity == 100,
                "reward uses the existing Gold stack identity and quantity semantics");

            camera.transform.position = new Vector3(targetWorldPoint.x, targetWorldPoint.y, camera.transform.position.z);
            yield return null;
            Debug.Log("M5B COMPLETED CLICK READY: click center of Game view again to prove the source Completed-only branch.");
            deadline = Time.realtimeSinceStartup + 60f;
            while (session.Dialogue.Phase != DialogueSessionPhase.AwaitingPlayerChoice
                   && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Time.realtimeSinceStartup < deadline, "second physical click opens the Completed conversation");
            Check(input.LastClickedObject == mayor.Identity && input.LastClickAccepted,
                "second literal click selects the same stable mayor ObjectID");
            Check(session.Dialogue.CurrentLine == 1
                  && session.Dialogue.AvailableResponses.Any(line => line.Num == 5 && line.TokenCode == 't'
                      && !string.IsNullOrWhiteSpace(line.Text))
                  && session.Dialogue.AvailableResponses.All(line => line.Num != 4),
                "Completed-only source response 5 appears with its t: source label and response 4 is absent");
            Check(session.Dialogue.Cancel("M5B Completed-only follow-up observed"),
                "Completed follow-up closes without invoking out-of-scope training");
            CheckStableRewards(session, mayor, dagger, gold, completedTimestamp, acceptedReaction + 10);

            RebuildBothModes(loader, initialMode);
            yield return null;
            CheckStableRewards(session, mayor, dagger, gold, completedTimestamp, acceptedReaction + 10);
            CheckUnique(loader, navigation, lifecycle, session, mayor.Identity);

            Check(session.TryTransitionPlayer(DaggerSector, new Vector2(1, 1), session.PlayerState.ArtId),
                "PC traverses to the dagger source sector after completion");
            yield return null;
            Check(ReferenceEquals(session.States[dagger.Identity], dagger)
                  && dagger.Placement.ParentIdentity == mayor.Identity,
                "foreign-sector source reload retains the transferred dagger without resurrection");
            Check(session.TryTransitionPlayer(MayorSector, new Vector2(1, 1), session.PlayerState.ArtId),
                "PC traverses back to the mayor sector");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            CheckStableRewards(session, mayor, dagger, gold, completedTimestamp, acceptedReaction + 10);
            Check(session.ReloadSelectedSector(), "completed mayor sector unload/reload succeeds");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            CheckStableRewards(session, mayor, dagger, gold, completedTimestamp, acceptedReaction + 10);
            CheckUnique(loader, navigation, lifecycle, session, mayor.Identity);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M5B PLAYMODE VALIDATION PASS: quest={QuestNumber}:Completed; mayor={mayor.Identity}; " +
                      $"dialog={DialogueNumber}; dagger={dagger.Identity}:proto{DaggerPrototype}:name{DaggerNameIndex}; " +
                      $"gold=100; xp=800; alignment=50; reaction={acceptedReaction + 10}; followup=5(t:); " +
                      $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static void CheckStableRewards(WorldMapSessionCoordinator session, PersistentObjectState mayor,
        PersistentObjectState dagger, PersistentObjectState gold, QuestTimestamp timestamp, int reaction)
    {
        Check(session.Campaign.GetPcQuestState(QuestNumber) == (int)QuestState.Completed
              && session.Campaign.GetPcQuestTimestamp(QuestNumber) == timestamp,
            "terminal quest state and timestamp remain stable");
        Check(session.Progression.GetExperience(session.PlayerState.Identity) == 800
              && session.DerivedStats.GetAlignment(session.PlayerState.Identity) == 50
              && session.DerivedStats.GetReaction(mayor.Identity, session.PlayerState.Identity) == reaction,
            "XP, alignment, and reaction cannot repeat");
        Check(ReferenceEquals(session.States[dagger.Identity], dagger)
              && dagger.Placement.ParentIdentity == mayor.Identity,
            "dagger identity and destination cannot duplicate or resurrect");
        Check(ReferenceEquals(session.States[gold.Identity], gold)
              && gold.StackQuantity == 100 && session.GetGold(session.PlayerState.Identity) == 100
              && session.States.Values.Count(state => state.PrototypeNumber == 9056
                  && state.Placement.Kind == ObjectPlacementKind.Contained
                  && state.Placement.ParentIdentity == session.PlayerState.Identity) == 1,
            "one Gold stack retains exactly the authored reward");
        Check(session.Journal.TryProject(QuestNumber, false, out QuestJournalEntry entry)
              && entry.State == QuestState.Completed && entry.Timestamp == timestamp,
            "journal remains a read-only Completed projection");
    }

    private static DialogueChoiceStatus Choose(ProductionDialogueSession dialogue, int line)
    {
        int index = dialogue.AvailableResponses.ToList().FindIndex(response => response.Num == line);
        Check(index >= 0, $"dialogue response {line} is available");
        return dialogue.SelectResponse(index);
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
        var route = new System.Collections.Generic.List<Vector2Int>();
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
        ProductionPlayerLifecycle lifecycle, WorldMapSessionCoordinator session, ArcanumObjectId mayor)
    {
        Check(Object.FindObjectsByType<WorldMapSessionCoordinator>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one campaign/session coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one world-object sector owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        Check(Object.FindObjectsByType<ProductionDialoguePresenter>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one dialogue presenter");
        Check(Object.FindObjectsByType<ProductionJournalPresenter>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one journal presenter");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(root => root.name == "WorldObjects") == 1, "one sector object root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1,
            "one production PC presentation");
        Check(loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                               && owner.WorldObject.Identity == mayor) == 1,
            "one mayor presentation");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no orphan sprite owners");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
        Check(navigation.Player == lifecycle.Presentation,
            "navigation remains bound only to the production PC");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M5B validation FAIL: " + label);
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
