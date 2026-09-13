using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M5CTrainingValidation
{
    private const string MayorSector = "maps/arcanum1-024-fixed/96636765255.sec";
    private const string MayorKey = "G_787AD4AB_9061_2B4E_A691_F582800B2BB3";
    private const int MayorPrototype = 17088;
    private const int DialogueNumber = 1009;
    private const int QuestNumber = 1005;
    private static int _warnings;
    private static int _errors;
    private static bool _tracking;

    [MenuItem("OpenArcanum/M5C/Prepare and Validate Authentic Mayor Training")]
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

            Check(session.TryTransitionPlayer(MayorSector, new Vector2(1, 1), session.PlayerState.ArtId),
                "coordinator loads the authentic mayor sector");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            PersistentObjectState mayor = session.States.Values.Single(state => state.Identity.Key == MayorKey);
            Check(mayor.PrototypeNumber == MayorPrototype && mayor.DialogNum == DialogueNumber
                  && mayor.Type == ObjectType.Npc,
                "exact source mayor ObjectID, prototype, type, and SAP_DIALOG load");
            Check(session.TryGetLoadedObject(mayor.Identity, out WorldObject mayorRuntime),
                "mayor has one loaded presentation");

            SetCompletedQuestFixture(session);
            session.DerivedStats.SetReaction(mayor.Identity, session.PlayerState.Identity, 53);
            if (session.Progression.GetEffectiveSkillRank(session.PlayerState.Identity,
                    CharacterSkill.Persuasion) < 1)
            {
                int needed = Math.Max(0, CharacterProgressionService.GetExperienceForLevel(2)
                                         - session.Progression.GetExperience(session.PlayerState.Identity));
                session.Progression.AwardExperience(session.PlayerState.Identity, needed);
                Check(session.Progression.IncreaseSkill(session.PlayerState.Identity, CharacterSkill.Persuasion)
                      == SkillIncreaseResult.Success, "authoritative M4C progression establishes Persuasion rank 4");
            }
            Check(session.Progression.GetTrainingLevel(session.PlayerState.Identity, CharacterSkill.Persuasion)
                  == SkillTrainingLevel.None, "Persuasion begins untrained");
            Check(session.GetGold(session.PlayerState.Identity) == 0 && session.GetGold(mayor.Identity) == 0,
                "fresh validation inventories begin without Gold");
            session.AddGold(session.PlayerState.Identity, 100);
            Check(session.GetGold(session.PlayerState.Identity) == 100,
                "existing inventory authority creates the exact 100-Gold fixture");

            WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(candidate => candidate != null
                && candidate.WorldObject != null && candidate.WorldObject.Identity == mayor.Identity);
            Check(TryFindTargetPoint(loader, owner, out Vector3 targetWorldPoint),
                "mayor has a selectable visible pixel");
            Vector2Int far = FindWalkable(loader, mayorRuntime.Tile, 6, 24);
            Check(far.x >= 0 && session.SetMovementState(session.PlayerState.Identity, far,
                    navigation.Player.ArtId, false), "place PC at a reachable out-of-range Talk start");
            Vector2 approachStart = session.PlayerState.MapPosition;
            Focus(camera, targetWorldPoint);
            yield return null;
            Debug.Log($"M5C PHYSICAL READY: sector={session.SelectedSector}; mayor={mayor.Identity}; " +
                      $"proto={mayor.PrototypeNumber}; dialog={mayor.DialogNum}; response=5(t:11); " +
                      $"rank={session.Progression.GetEffectiveSkillRank(session.PlayerState.Identity, CharacterSkill.Persuasion)}; " +
                      $"reaction=53; gold=100; expectedCost=99; start={approachStart}; " +
                      $"npcTile={mayorRuntime.Tile}; world={targetWorldPoint}; click=center of Game view.");

            yield return WaitFor(() => session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice,
                60f, "literal mayor click opens production dialogue");
            Check(input.LastClickedObject == mayor.Identity && input.LastClickAccepted,
                "literal Game-view click selects the stable mayor ObjectID");
            Check(input.LastInteractionResult.HasValue
                  && input.LastInteractionResult.Value.Command.Type == WorldInteractionCommandType.Talk,
                "physical click submits the normal Talk command");
            Check(interaction.LastResult.HasValue && interaction.LastResult.Value.IsSuccess,
                "arrival executes one authoritative Talk command");
            Check(session.PlayerState.MapPosition != approachStart,
                "physical click moves the production PC through normal navigation");
            Check(session.Dialogue.CurrentLine == 1
                  && session.Dialogue.AvailableResponses.Any(line => line.Num == 5
                      && line.TokenCode == 't' && line.TokenPayload == "11"),
                "Completed-only authentic t:11 response is visible");
            Debug.Log("M5C TRAINING READY: physically choose the training response, Persuasion, Yes, " +
                      "then the acknowledgement.");

            yield return WaitFor(() => session.Dialogue.TrainingView == TrainingDialogueView.SkillSelection,
                60f, "physical t: response opens source skill selection");
            Check(session.Dialogue.OfferedTrainingSkills.SequenceEqual(new[] { CharacterSkill.Persuasion }),
                "typed training request offers exactly Persuasion");
            yield return WaitFor(() => session.Dialogue.TrainingView == TrainingDialogueView.Payment,
                60f, "physical Persuasion choice opens payment");
            Check(session.Dialogue.NpcText.Contains("99"), "source payment prompt shows exact 99-Gold cost");
            yield return WaitFor(() => session.Dialogue.TrainingView == TrainingDialogueView.Result
                                      && session.Dialogue.LastTrainingResult.Succeeded,
                60f, "physical Yes atomically trains and pays");
            Check(session.GetGold(session.PlayerState.Identity) == 1 && session.GetGold(mayor.Identity) == 99,
                "successful transaction moves exactly 99 Gold from PC to mayor");
            Check(session.Progression.GetTrainingLevel(session.PlayerState.Identity, CharacterSkill.Persuasion)
                  == SkillTrainingLevel.Apprentice, "M4C owns the resulting Apprentice state");
            yield return WaitFor(() => session.Dialogue.TrainingView == TrainingDialogueView.None,
                60f, "physical acknowledgement returns to authored target");
            Check(session.Dialogue.CurrentLine == 20, "training returns to authored line 20");
            session.Dialogue.Cancel("M5C successful physical flow observed");

            Focus(camera, targetWorldPoint);
            yield return null;
            Debug.Log("M5C REJECTION READY: click center again, choose training, Persuasion, then continue. " +
                      "This must report already trained without another charge.");
            yield return WaitFor(() => session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice,
                60f, "second literal click opens the same authentic mayor");
            yield return WaitFor(() => session.Dialogue.TrainingView == TrainingDialogueView.SkillSelection,
                60f, "repeat physical t: response opens skill selection");
            yield return WaitFor(() => session.Dialogue.TrainingView == TrainingDialogueView.Result
                                      && session.Dialogue.LastTrainingResult.Failure
                                      == DialogueTrainingFailure.AlreadyTrained,
                60f, "repeat physical Persuasion selection reports already trained");
            Check(session.GetGold(session.PlayerState.Identity) == 1 && session.GetGold(mayor.Identity) == 99,
                "rejection cannot repeat payment");
            yield return WaitFor(() => session.Dialogue.TrainingView == TrainingDialogueView.None,
                60f, "physical rejection acknowledgement returns to authored target");
            session.Dialogue.Cancel("M5C repeat rejection observed");

            RebuildBothModes(loader, initialMode);
            yield return null;
            CheckStable(session, mayor);
            CheckUnique(loader, navigation, lifecycle, session, mayor.Identity);
            Check(session.ReloadSelectedSector(), "mayor sector unload/reload succeeds");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            CheckStable(session, mayor);
            CheckUnique(loader, navigation, lifecycle, session, mayor.Identity);

            StopTracking();
            Check(_warnings == 0 && _errors == 0,
                $"no new Unity warnings/errors (warnings={_warnings}; errors={_errors})");
            Debug.Log($"M5C PLAYMODE VALIDATION PASS: mayor={mayor.Identity}; dialog=1009; token=t:11; " +
                      "skill=Persuasion; tier=Apprentice; reaction=53; cost=99; pcGold=1; trainerGold=99; " +
                      $"warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            StopTracking();
        }
    }

    private static void SetCompletedQuestFixture(WorldMapSessionCoordinator session)
    {
        int state = session.Campaign.GetPcQuestState(QuestNumber);
        if (state == (int)QuestState.Unknown)
            session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Mentioned);
        state = session.Campaign.GetPcQuestState(QuestNumber);
        if (state == (int)QuestState.Mentioned)
            session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Accepted);
        state = session.Campaign.GetPcQuestState(QuestNumber);
        if (state is (int)QuestState.Accepted or (int)QuestState.Achieved)
            session.Campaign.SetPcQuestState(QuestNumber, (int)QuestState.Completed);
        Check(session.Campaign.GetPcQuestState(QuestNumber) == (int)QuestState.Completed,
            "quest 1005 is in its authentic Completed gate");
    }

    private static IEnumerator WaitFor(Func<bool> predicate, float seconds, string label)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(Time.realtimeSinceStartup < deadline, label);
    }

    private static void CheckStable(WorldMapSessionCoordinator session, PersistentObjectState mayor)
    {
        Check(session.Progression.GetTrainingLevel(session.PlayerState.Identity, CharacterSkill.Persuasion)
              == SkillTrainingLevel.Apprentice, "Apprentice state survives presentation lifecycle");
        Check(session.GetGold(session.PlayerState.Identity) == 1 && session.GetGold(mayor.Identity) == 99,
            "both authoritative Gold balances survive presentation lifecycle");
        Check(ReferenceEquals(session.States[mayor.Identity], mayor),
            "mayor stable identity survives presentation lifecycle");
    }

    private static void Focus(Camera camera, Vector3 point)
    {
        camera.transform.position = new Vector3(point.x, point.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
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
            FindObjectsSortMode.None).Length == 1, "one session coordinator");
        Check(Object.FindObjectsByType<WorldObjectSectorLoader>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one object-sector owner");
        Check(Object.FindObjectsByType<PlayerNavigationController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one navigation controller");
        Check(Object.FindObjectsByType<PlayerInteractionController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one interaction controller");
        Check(Object.FindObjectsByType<ProductionDialoguePresenter>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length == 1, "one dialogue presenter");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(runtime => runtime.Identity == session.PlayerState.Identity) == 1,
            "one production PC presentation");
        Check(loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                               && owner.WorldObject.Identity == mayor) == 1,
            "one mayor presentation");
        Check(loader.SpriteOwners.Where(owner => owner != null && owner.WorldObject != null)
            .GroupBy(owner => owner.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
        Check(navigation.Player == lifecycle.Presentation,
            "navigation remains bound only to production PC");
    }

    private static WorldObjectSectorLoader RequirePlayLoader()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in TestTerrain first.");
        return Object.FindFirstObjectByType<WorldObjectSectorLoader>()
               ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M5C validation FAIL: " + label);
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
