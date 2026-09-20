using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World.Demo;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M7CAreaDiscoveryValidation
{
    private const string ClarissaSector = "maps/arcanum1-024-fixed/96502547529.sec";
    private const string OverlandEntranceSector = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string BatesSector = "maps/bates mansion lev 1/67108865.sec";
    private const string ClarissaKey = "G_48830599_2627_9E4B_9996_FEB1835EDCDD";
    private static readonly AreaId KnaTha = new(58);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M7C/Prepare Physical Discovery Validation")]
    private static void Prepare()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M7C harness.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                                         ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        WorldMapSessionCoordinator session = loader.Session;
        string directory = Path.Combine(Application.temporaryCachePath,
            "OpenArcanum-M7C-" + Guid.NewGuid().ToString("N"));
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(ClarissaSector), "Clarissa retail sector loads");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            PlayerNavigationController navigation = Object.FindFirstObjectByType<PlayerNavigationController>();
            PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            Check(loader != null && lifecycle != null && navigation != null && interaction != null && input != null,
                "production composition exists");
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;
            Check(session.Campaign.KnownAreas.Count == 0 && !session.Campaign.IsAreaKnown(KnaTha)
                  && !session.Campaign.CanSelectWorldArea(KnaTha), "new campaign starts with K'na Tha unknown");
            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Throwing) == SkillIncreaseResult.Success,
                "production PC gains one existing Throwing point for the source condition");
            Check(session.Progression.SetTrainingLevel(pc, CharacterSkill.Throwing,
                      SkillTrainingLevel.Apprentice) == TrainingAssignmentResult.Success,
                "existing M4C authority satisfies Clarissa's tr3 1 condition");

            PersistentObjectState npc = session.States.Values.Single(value => value.Identity.Key == ClarissaKey);
            Check(npc.Type == ObjectType.Npc && npc.PrototypeNumber == 17229 && npc.DialogNum == 1497,
                "exact Clarissa NPC and dialogue fixture load");
            Check(session.TryGetLoadedObject(npc.Identity, out WorldObject runtime),
                "Clarissa has one loaded runtime");
            WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(value => value?.WorldObject?.Identity == npc.Identity);
            Check(TryFindTargetPoint(loader, owner, out Vector3 targetPoint),
                "Clarissa has a visible selectable pixel");
            Vector2Int far = FindWalkable(loader, runtime.Tile, 6, 24);
            Check(far.x >= 0 && session.SetMovementState(pc, far, session.PlayerState.ArtId, false),
                "PC starts on a normal out-of-range Talk approach");
            int discoveries = 0;
            session.Campaign.AreaDiscovered += id => { if (id == KnaTha) discoveries++; };
            Focus(targetPoint);
            yield return null;
            Debug.Log($"M7C DISCOVERY READY: literal center Game-view click; npc={npc.Identity}; "
                      + $"proto={npc.PrototypeNumber}; dialog={npc.DialogNum}; area=58 K'na Tha unknown; "
                      + $"screen={Camera.main.WorldToScreenPoint(targetPoint)}.");

            yield return Wait(() => session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice,
                "literal NPC click opens authentic dialogue");
            Check(input.LastClickedObject == npc.Identity && input.LastClickAccepted
                  && interaction.LastResult?.Command.Type == WorldInteractionCommandType.Talk,
                "literal click selects Clarissa and submits production Talk");
            Check(session.Dialogue.CurrentLine == 1 && session.Dialogue.DialogueNumber == 1497,
                "authentic SAP_DIALOG starts at line 1");
            Debug.Log("M7C DIALOGUE READY: physically choose authored response lines "
                      + "2 -> 44 -> 55 -> 70 -> 77 -> 81 -> 97 -> 88 -> 93.");

            yield return Wait(() => session.Campaign.IsAreaKnown(KnaTha),
                "physical source dialogue reaches mm58");
            Check(session.Dialogue.CurrentLine == 123 && session.Campaign.GetPcQuestState(1097) == 2,
                "source response 93 marked K'na Tha after accepting quest 1097");
            Check(discoveries == 1 && session.Campaign.KnownAreas.Count == 1
                  && session.Campaign.CanSelectWorldArea(KnaTha),
                "one typed authoritative discovery and future selection query agree");
            Check(session.Campaign.TryDiscoverArea(KnaTha, out bool duplicateChanged, out var duplicateFailure)
                  && !duplicateChanged && duplicateFailure == Arcanum.Runtime.Campaign.CampaignStateFailure.None
                  && discoveries == 1 && session.Campaign.KnownAreas.Count == 1,
                "duplicate discovery is idempotent without another event/state entry");

            RebuildSequence(loader, session, pc, ClarissaSector);
            var slots = new SessionSaveSlotService(session, directory);
            Check(slots.SaveSlot("m7c").Succeeded, "temporary V1 M7C slot saves");

            Check(session.TryTransitionPlayer(OverlandEntranceSector, new Vector2(22, 0), session.PlayerState.ArtId),
                "same-map A to entrance-sector B transition succeeds");
            yield return null;
            Check(session.Campaign.IsAreaKnown(KnaTha), "known state survives same-map sector transition");
            PersistentObjectState entrance = session.States[AreaEntranceResolver.BatesEntranceIdentity];
            Check(session.SetMovementState(pc, entrance.TilePosition, session.PlayerState.ArtId, false),
                "PC reaches authentic M7B entrance source tile");
            Check(session.RequestAreaEntrance(pc, entrance.Identity).Succeeded,
                "existing authentic entrance performs a cross-map transition");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            Check(session.SelectedSector == BatesSector && session.Campaign.IsAreaKnown(KnaTha)
                  && session.Campaign.KnownAreas.Count == 1,
                "known state survives cross-map transition without entry discovery");
            Check(!session.Campaign.IsAreaKnown(new AreaId(21)),
                "entering Bates does not invent Tarant discovery");
            RebuildSequence(loader, session, pc, BatesSector);

            session.ResetAuthoritativeSession();
            yield return null;
            Check(!session.Campaign.IsAreaKnown(KnaTha), "authoritative reset restores default unknown state");
            Check(slots.LoadSlot("m7c").Succeeded, "temporary V1 slot loads after reset");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            Check(session.SelectedSector == ClarissaSector && session.PlayerState.Identity == pc
                  && session.Campaign.IsAreaKnown(KnaTha) && session.Campaign.CanSelectWorldArea(KnaTha)
                  && session.Campaign.KnownAreas.Count == 1,
                "A-B-A save/reset/load restores exact PC, sector, and one known area");
            RebuildSequence(loader, session, pc, ClarissaSector);
            CheckUnique(loader, pc);

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M7C PLAYMODE VALIDATION PASS: area=58 K'na Tha; event=dialog1497:line93:mm58; "
                      + $"discoveries={discoveries}; duplicateChanged={duplicateChanged}; "
                      + $"Clarissa->overland->Bates->slotRestoreClarissa; V1; Original->Enhanced->Original; "
                      + $"warnings={_warnings}; errors={_errors}; tempSlot={directory}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void RebuildSequence(WorldObjectSectorLoader loader, WorldMapSessionCoordinator session,
        ArcanumObjectId pc, string sector)
    {
        foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
            loader.RebuildVisuals();
            Check(session.SelectedSector == sector && session.PlayerState.Identity == pc
                  && session.Campaign.IsAreaKnown(KnaTha) && session.Campaign.KnownAreas.Count == 1,
                "graphics rebuild preserves one authoritative known area");
            CheckUnique(loader, pc);
        }
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
        float best = float.PositiveInfinity;
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
            float distance = Mathf.Pow(x + .5f - rect.center.x, 2) + Mathf.Pow(y + .5f - rect.center.y, 2);
            if (distance >= best) continue;
            found = true;
            best = distance;
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

    private static void Focus(Vector3 point)
    {
        Camera camera = Camera.main;
        camera.transform.position = new Vector3(point.x, point.y, camera.transform.position.z);
        if (camera.orthographic) camera.orthographicSize = 3f;
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ArcanumObjectId pc)
    {
        Check(Count<WorldMapSessionCoordinator>() == 1 && Count<TileMapDemo>() == 1
              && Count<WorldObjectSectorLoader>() == 1 && Count<ProductionPlayerLifecycle>() == 1
              && Count<PlayerNavigationController>() == 1 && Count<PlayerInteractionController>() == 1
              && Count<ProductionDialoguePresenter>() == 1,
            "one coordinator/terrain/loader/lifecycle/navigation/interaction/dialogue presenter");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(value => value.name == "WorldObjects") == 1, "one WorldObjects root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(value => value.Identity == pc) == 1, "one PC runtime/presentation");
        Check(loader.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length == loader.SpriteOwners.Count,
            "no orphan sprite owners");
        Check(loader.SpriteOwners.Where(value => value?.WorldObject != null)
            .GroupBy(value => value.WorldObject.Identity).All(group => group.Count() == 1),
            "one presentation per persistent identity");
    }

    private static int Count<T>() where T : Object
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

    private static IEnumerator Wait(Func<bool> condition, string label)
    {
        float end = Time.realtimeSinceStartup + 300f;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        Check(condition(), label);
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("M7C validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
