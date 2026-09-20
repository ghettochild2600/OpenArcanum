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

internal static class M7DWorldMapDestinationValidation
{
    private const string ClarissaSector = "maps/arcanum1-024-fixed/96502547529.sec";
    private const string ClarissaKey = "G_48830599_2627_9E4B_9996_FEB1835EDCDD";
    private static readonly AreaId KnaTha = new(58);
    private static readonly AreaId Tarant = new(21);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M7D/Prepare Physical Destination Validation")]
    private static void Prepare()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M7D harness.");
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
            "OpenArcanum-M7D-" + Guid.NewGuid().ToString("N"));
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(ClarissaSector), "Clarissa retail sector loads");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            PlayerInteractionController interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            PlayerClickMoveInput input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            ProductionWorldMapDestinationPresenter presenter =
                Object.FindFirstObjectByType<ProductionWorldMapDestinationPresenter>();
            Check(loader != null && lifecycle != null && interaction != null && input != null && presenter != null,
                "production composition and destination presenter exist");
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            ArcanumObjectId pc = session.PlayerState.Identity;

            Check(session.WorldMapDestinations.TryProjectAll(out var all, out var catalogFailure)
                  && catalogFailure == WorldMapDestinationFailure.None && all.Count == 79,
                "79 canonical retail destinations project in source order");
            Check(session.WorldMapDestinations.TryProjectVisible(out var visible, out _)
                  && visible.Count == 0, "source-style projection hides all unknown destinations");
            Check(session.WorldMapDestinations.TrySelectWorldArea(KnaTha).Failure
                  == WorldMapDestinationFailure.Unavailable,
                "K'na Tha begins unavailable");
            Check(session.WorldMapDestinations.TrySelectWorldArea(Tarant).Failure
                  == WorldMapDestinationFailure.Unavailable,
                "Tarant begins unavailable despite the independent Bates entrance");
            Check(session.WorldMapDestinations.TrySelectWorldArea(new AreaId(36)).Failure
                  == WorldMapDestinationFailure.InvalidDestinationRecord,
                "retail unknown alias is explicitly not a destination");
            presenter.Open();
            Debug.Log("M7D UNKNOWN PANEL READY: physically verify no K'na Tha/Tarant marker is shown, then click CLOSE.");
            yield return Wait(() => !presenter.IsOpen, "physical close confirms the empty source-style panel");

            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Throwing) == SkillIncreaseResult.Success,
                "production PC gains one existing Throwing point for the source condition");
            Check(session.Progression.SetTrainingLevel(pc, CharacterSkill.Throwing,
                      SkillTrainingLevel.Apprentice) == TrainingAssignmentResult.Success,
                "existing M4C authority satisfies Clarissa's tr3 1 condition");
            PersistentObjectState npc = session.States.Values.Single(value => value.Identity.Key == ClarissaKey);
            Check(session.TryGetLoadedObject(npc.Identity, out WorldObject runtime), "Clarissa has one runtime");
            WorldObjectSpriteOwner owner = loader.SpriteOwners.Single(value => value?.WorldObject?.Identity == npc.Identity);
            Check(TryFindTargetPoint(loader, owner, out Vector3 targetPoint),
                "Clarissa has a visible selectable pixel");
            Vector2Int far = FindWalkable(loader, runtime.Tile, 6, 24);
            Check(far.x >= 0 && session.SetMovementState(pc, far, session.PlayerState.ArtId, false),
                "PC starts on a normal out-of-range Talk approach");
            Focus(targetPoint);
            yield return null;
            Debug.Log($"M7D DISCOVERY READY: literally click Clarissa; npc={npc.Identity}; screen="
                      + $"{Camera.main.WorldToScreenPoint(targetPoint)}; then choose "
                      + "2 -> 44 -> 55 -> 70 -> 77 -> 81 -> 97 -> 88 -> 93.");
            yield return Wait(() => session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice,
                "literal Clarissa click opens production dialogue");
            Check(input.LastClickedObject == npc.Identity && input.LastClickAccepted
                  && interaction.LastResult?.Command.Type == WorldInteractionCommandType.Talk,
                "literal click selected Clarissa through production Talk");
            yield return Wait(() => session.Campaign.IsAreaKnown(KnaTha),
                "physical authored dialogue reaches exact mm58");

            Check(session.WorldMapDestinations.TryGetDestination(KnaTha, out var projected, out _)
                  && projected.IsKnown && projected.IsSelectable,
                "M7C discovery immediately changes the read-only projection");
            Check(session.WorldMapDestinations.TrySelectWorldArea(Tarant).Failure
                  == WorldMapDestinationFailure.Unavailable,
                "unrelated Tarant remains unavailable");
            Vector2 positionBeforeSelection = session.PlayerState.MapPosition;
            string sectorBeforeSelection = session.SelectedSector;
            int requests = 0;
            WorldMapTravelRequest request = default;
            presenter.TravelRequested += value => { requests++; request = value; };
            presenter.Open();
            Debug.Log("M7D KNOWN PANEL READY: physically click K'na Tha [area 58].");
            yield return Wait(() => presenter.HasSelection && presenter.LastSelection.Succeeded,
                "literal destination button produces a typed request");
            Check(requests == 1 && request.AreaId == KnaTha
                  && request.Destination == new WorldMapTile(91902, 39305),
                "one exact K'na Tha travel intent is emitted");
            Check(session.PlayerState.MapPosition == positionBeforeSelection
                  && session.SelectedSector == sectorBeforeSelection && !session.IsMapTransitionActive,
                "selection performs no PC relocation or map transition");
            Check(session.Campaign.KnownAreas.Count == 1
                  && session.Campaign.IsAreaKnown(KnaTha) && !session.Campaign.IsAreaKnown(Tarant),
                "selection performs no discovery or other campaign mutation");
            presenter.Close();

            var slots = new SessionSaveSlotService(session, directory);
            Check(slots.SaveSlot("m7d").Succeeded, "temporary V1 M7D slot saves");
            session.ResetAuthoritativeSession();
            Check(session.WorldMapDestinations.TrySelectWorldArea(KnaTha).Failure
                  == WorldMapDestinationFailure.Unavailable,
                "reset recomputes an empty projection against replacement campaign state");
            Check(slots.LoadSlot("m7d").Succeeded, "temporary V1 slot loads");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            presenter = Object.FindFirstObjectByType<ProductionWorldMapDestinationPresenter>();
            Check(session.PlayerState.Identity == pc && session.SelectedSector == ClarissaSector
                  && session.WorldMapDestinations.TrySelectWorldArea(KnaTha).Succeeded,
                "V1 load restores PC, source sector, and selectable K'na Tha");
            Check(session.WorldMapDestinations.TrySelectWorldArea(Tarant).Failure
                  == WorldMapDestinationFailure.Unavailable,
                "V1 load leaves unrelated unknown destination unavailable");

            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.WorldMapDestinations.TrySelectWorldArea(KnaTha).Succeeded
                      && session.PlayerState.Identity == pc && session.SelectedSector == ClarissaSector,
                    "graphics rebuild preserves selection state without travel");
                CheckUnique(loader, pc);
            }
            Check(presenter != null && Count<ProductionWorldMapDestinationPresenter>() == 1,
                "one destination presenter remains after restore and rebuild");

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M7D PLAYMODE VALIDATION PASS: projected=79; hiddenUnknown=True; "
                      + $"selected=area58@{request.Destination}; requests={requests}; noTravel=True; "
                      + $"TarantUnavailable=True; V1; Original->Enhanced->Original; warnings={_warnings}; "
                      + $"errors={_errors}; tempSlot={directory}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
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
              && Count<ProductionWorldMapDestinationPresenter>() == 1,
            "one coordinator/terrain/loader/lifecycle/navigation/interaction/destination presenter");
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
        if (!condition) throw new InvalidOperationException("M7D validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
