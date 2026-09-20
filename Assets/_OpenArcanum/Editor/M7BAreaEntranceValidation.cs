using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World.Demo;
using Newtonsoft.Json.Linq;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real-data preparation and observation. Entry/exit activation is driven by literal Game-view clicks.</summary>
internal static class M7BAreaEntranceValidation
{
    private const string Overland = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string Bates = "maps/bates mansion lev 1/67108865.sec";
    private static readonly Vector2 BatesArrival = new(104, 92);
    private static readonly Vector2 OverlandArrival = new(61976, 65664);
    private static bool _running;
    private static int _warnings, _errors;

    [MenuItem("OpenArcanum/M7B/Prepare Physical Entrance and Return Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M7B harness.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>()
                     ?? throw new InvalidOperationException("TestTerrain has no world-object loader.");
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true; _warnings = _errors = 0;
        GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
        Application.logMessageReceived += Track;
        var session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector("maps/arcanum1-024-fixed/101602821844.sec"), "inventory fixture loads");
            yield return null;
            ArcanumObjectId pc = session.PlayerState.Identity;
            var armor = session.States.Values.Single(s => s.Identity.Key == "G_0435F503_6600_6342_97B2_6D9E1A85A2F2");
            var ammo = session.States.Values.Single(s => s.Identity.Key == "G_9239E097_A8D2_C147_9F58_76077340C60E");
            Check(session.TransferItem(armor.Identity, armor.Placement, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "authentic armor retained");
            Check(session.EquipItem(pc, armor.Identity, WornLocation.Armor).Succeeded, "authentic armor equipped");
            Check(session.TransferItem(ammo.Identity, ammo.Placement, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "quantity-60 Ammo retained");
            var carried = session.CreateItem(10078, ObjectPlacement.ContainedBy(pc));
            Check(carried.Succeeded, "dynamic carried Food created");
            session.AddGold(pc, 123);
            session.Progression.AwardExperience(pc, 2100);
            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Persuasion) == SkillIncreaseResult.Success,
                "purchased skill retained");
            Check(session.Progression.SetTrainingLevel(pc, CharacterSkill.Persuasion,
                SkillTrainingLevel.Apprentice) == TrainingAssignmentResult.Success, "training retained");
            session.Vitality.ApplyHitPointDamage(pc, 3);
            session.Vitality.ApplyFatigueDamage(pc, 4);
            session.DerivedStats.SetAlignment(pc, 50);
            session.Campaign.SetFlag(77, 1);
            session.Campaign.SetVar(10, 321);
            session.Campaign.SetPcQuestState(1005, (int)QuestState.Mentioned);
            session.Campaign.SetPcQuestState(1005, (int)QuestState.Accepted);

            Check(session.SelectSector(Bates), "Bates preloads authoritative object state");
            yield return null;
            Check(session.SelectSector(Overland), "authentic overland entrance sector loads");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            var nav = Object.FindFirstObjectByType<PlayerNavigationController>();
            var input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            var interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            var life = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            Check(life.SpawnAndBind(), "production PC binds");
            var playerState = session.PlayerState;
            var characterState = session.Characters.Get(pc);
            var domainObjects = session.States.ToDictionary(pair => pair.Key, pair => pair.Value);
            var entrance = session.States[AreaEntranceResolver.BatesEntranceIdentity];
            Check(entrance.Type == ObjectType.Scenery && entrance.PrototypeNumber == 4036
                  && entrance.UseScriptNum == 1267, "exact retail entrance is admitted");
            Check(session.TryGetLoadedObject(entrance.Identity, out WorldObject entranceRuntime),
                "entrance runtime is loaded");
            var entranceOwner = loader.SpriteOwners.Single(owner => owner?.WorldObject?.Identity == entrance.Identity);
            Check(TryFindTargetPoint(loader, entranceOwner, out Vector3 targetPoint),
                "entrance has a visible selectable pixel");
            Vector2Int approach = FindApproach(loader, entranceRuntime.Tile);
            Check(approach.x >= 0 && session.SetMovementState(pc, approach, playerState.ArtId, false),
                "PC starts out of range on a valid approach route");
            JObject baseline = Snapshot(session);
            int batesSelections = 0, overlandSelections = 0;
            session.SectorSelected += path => { if (path == Bates) batesSelections++; else if (path == Overland) overlandSelections++; };
            Focus(targetPoint);
            yield return null;
            Debug.Log($"M7B ENTRANCE READY: literal center Game-view click; entrance={entrance.Identity}; "
                      + $"proto={entrance.PrototypeNumber}; SAP_USE={entrance.UseScriptNum}; startLocal={approach}; "
                      + $"entranceGlobal={AreaEntranceResolver.BatesSourceTile}; screen={Camera.main.WorldToScreenPoint(targetPoint)}.");
            yield return Wait(() => session.SelectedSector == Bates, "literal scenery click enters Bates");
            Check(input.LastClickedObject == entrance.Identity && input.LastClickAccepted
                  && input.LastInteractionResult?.Command.Type == WorldInteractionCommandType.Use,
                "literal click uses exact scenery through normal interaction");
            Check(session.LastAreaEntranceResult.Succeeded
                  && session.LastAreaEntranceResult.Destination.MapId == 12
                  && session.LastAreaEntranceResult.Destination.Tile == new Vector2Int(104, 92),
                "strict resolver returns exact Bates destination");
            Check(session.PlayerState == playerState && session.Characters.Get(pc) == characterState
                  && session.PlayerState.Identity == pc && session.PlayerState.MapPosition == BatesArrival,
                "same PC arrives at exact Bates tile");
            Check(((session.PlayerState.ArtId >> 6) & 31) == 0, "PC arrives standing");
            Check(domainObjects.All(pair => session.States.TryGetValue(pair.Key, out var value)
                                            && ReferenceEquals(value, pair.Value)), "preloaded state references retained");
            Check(JToken.DeepEquals(baseline, Snapshot(session)), "M3-M6 domain state unchanged by entry");
            yield return null; // old presentation roots use Unity's end-of-frame Destroy lifecycle
            CheckUnique(loader, pc);

            string directory = Path.Combine(Application.temporaryCachePath, "OpenArcanum-M7B-" + Guid.NewGuid().ToString("N"));
            var slots = new SessionSaveSlotService(session, directory);
            var batesItem = session.CreateItem(10078, ObjectPlacement.ContainedBy(pc));
            Check(batesItem.Succeeded && session.ExecuteInteraction(WorldInteractionCommand.Drop(pc,
                batesItem.State.Identity, Bates, new Vector2(41, 28))).IsSuccess, "Bates-side dynamic world state mutates");
            Check(slots.SaveSlot("bates").Succeeded, "V1 Bates slot saves");
            string batesSave = session.SaveGames.SerializeCurrentSession();
            RebuildSequence(loader, pc, Bates);
            Vector2Int exit = new(45, 28);
            Focus(TileWorld(loader, exit));
            yield return null;
            Debug.Log("M7B RETURN READY: literal center Game-view ground click; authentic passive exit local=(45,28).");
            yield return Wait(() => session.SelectedSector == Overland, "literal ground navigation triggers passive return");
            Check(input.LastClickedTile == exit && input.LastClickAccepted, "literal exit ground click accepted");
            Check(session.LastMapTransitionResult.Succeeded
                  && session.LastMapTransitionResult.Destination.MapId == 1
                  && session.PlayerState.MapPosition == OverlandArrival, "exact authored overland return");
            Check(session.PlayerState == playerState && session.PlayerState.Identity == pc,
                "return retains same PC reference/ObjectID");
            Check(session.States[batesItem.State.Identity] == batesItem.State
                  && batesItem.State.Placement == ObjectPlacement.InWorld(Bates, new Vector2(41, 28)),
                "Bates-side state remains foreign and retained");
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            nav = Object.FindFirstObjectByType<PlayerNavigationController>();
            input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            Check(nav.TrySetDestination(new Vector2Int(23, 0)), "overland navigation resumes");
            yield return Wait(() => !nav.IsMoving, "overland navigation completes");
            var overlandItem = session.CreateItem(10078, ObjectPlacement.ContainedBy(pc));
            Check(overlandItem.Succeeded && session.ExecuteInteraction(WorldInteractionCommand.Drop(pc,
                overlandItem.State.Identity, Overland, new Vector2(23, 0))).IsSuccess,
                "overland-side dynamic world state mutates");
            Check(slots.SaveSlot("overland").Succeeded, "V1 overland slot saves");
            string overlandSave = session.SaveGames.SerializeCurrentSession();
            RebuildSequence(loader, pc, Overland);
            CheckUnique(loader, pc);

            entranceOwner = loader.SpriteOwners.Single(owner => owner?.WorldObject?.Identity == entrance.Identity);
            Check(TryFindTargetPoint(loader, entranceOwner, out targetPoint), "entrance remains selectable after return/rebuild");
            Focus(targetPoint);
            yield return null;
            Debug.Log("M7B REENTRY READY: literal center Game-view click for second independent entrance activation.");
            yield return Wait(() => session.SelectedSector == Bates, "second literal entrance activation");
            Check(input.LastClickedObject == entrance.Identity && input.LastClickAccepted,
                "second literal click uses same entrance identity");
            Check(session.States[batesItem.State.Identity] == batesItem.State
                  && batesItem.State.Placement == ObjectPlacement.InWorld(Bates, new Vector2(41, 28)),
                "Bates state survives B-A-B");
            Check(session.States[overlandItem.State.Identity] == overlandItem.State
                  && overlandItem.State.Placement == ObjectPlacement.InWorld(Overland, new Vector2(23, 0)),
                "overland state survives A-B");
            Check(batesSelections == 2 && overlandSelections == 1, "each click/exit activates exactly once");
            session.AddGold(pc, 1);
            Check(session.SetMovementState(pc, new Vector2(42, 28), session.PlayerState.ArtId, false),
                "change Bates state before load");
            Check(slots.LoadSlot("bates").Succeeded && session.SaveGames.SerializeCurrentSession() == batesSave,
                "Bates V1 slot restores exact map/location/domain state");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            nav = Object.FindFirstObjectByType<PlayerNavigationController>();
            interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            var pending = session.CreateItem(10078, ObjectPlacement.InWorld(Bates, new Vector2(46, 28)));
            Check(pending.Succeeded && interaction.TryPickUp(pending.State.Identity).IsAccepted,
                "pending normal pickup approaches across passive exit");
            yield return Wait(() => session.SelectedSector == Overland, "second normal route triggers return");
            Check(interaction.PendingCommand == null && interaction.LastResult?.Code == WorldInteractionResultCode.Cancelled,
                "pending interaction is cancelled and cannot replay");
            Check(session.States[pending.State.Identity].Placement == ObjectPlacement.InWorld(Bates, new Vector2(46, 28)),
                "cancelled target stays in Bates");
            Check(session.ExecuteInteraction(WorldInteractionCommand.PickUp(pc, pending.State.Identity)).Code
                  == WorldInteractionResultCode.ItemNotInWorld, "stale Bates target is rejected overland");
            Check(slots.LoadSlot("overland").Succeeded && session.SaveGames.SerializeCurrentSession() == overlandSave,
                "overland V1 slot restores exact map/location/domain state");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            nav = Object.FindFirstObjectByType<PlayerNavigationController>();
            Check(nav.TrySetDestination(FindMove(loader, nav.Player.Tile)), "navigation works after overland load");
            yield return Wait(() => !nav.IsMoving, "post-load navigation completes");
            CheckUnique(loader, pc);
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M7B PLAYMODE VALIDATION PASS: entrance=(61974,65664)->map12:(104,92); "
                      + $"return->map1:(61976,65664); samePC={pc}; physicalEntries=2; physicalReturns=1; "
                      + $"normalRouteReturn=1; A/B retained; V1 Bates+overland; Original->Enhanced->Original both sides; "
                      + $"warnings={_warnings}; errors={_errors}; tempSlots={directory}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void RebuildSequence(WorldObjectSectorLoader loader, ArcanumObjectId pc, string sector)
    {
        Vector2 exact = loader.Session.PlayerState.MapPosition;
        JObject state = Snapshot(loader.Session);
        foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
            loader.RebuildVisuals();
            Check(loader.Session.SelectedSector == sector && loader.Session.PlayerState.Identity == pc
                  && loader.Session.PlayerState.MapPosition == exact && JToken.DeepEquals(state, Snapshot(loader.Session)),
                "graphics rebuild preserves exact authoritative map/state");
            CheckUnique(loader, pc);
        }
    }

    private static JObject Snapshot(WorldMapSessionCoordinator session)
    {
        var json = JObject.Parse(session.SaveGames.SerializeCurrentSession());
        var world = (JObject)json["world"];
        world.Remove("currentMap"); world.Remove("selectedSector");
        var player = (JObject)world["player"];
        foreach (string name in new[] { "mapPath", "mapX", "mapY", "artId" }) player.Remove(name);
        foreach (JObject record in (JArray)json["objects"])
            if ((string)record["identity"] == session.PlayerState.Identity.Key)
                foreach (string name in new[] { "sourceSector", "artId", "tileX", "tileY", "placement" }) record.Remove(name);
        return json;
    }

    private static bool TryFindTargetPoint(WorldObjectSectorLoader loader, WorldObjectSpriteOwner owner,
        out Vector3 worldPoint)
    {
        SpriteRenderer renderer = owner.WorldObject.View;
        Sprite sprite = renderer != null ? renderer.sprite : null;
        if (sprite == null) { worldPoint = default; return false; }
        Texture2D texture = sprite.texture; Rect rect = sprite.textureRect; Bounds bounds = sprite.bounds;
        int step = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(rect.width, rect.height) / 32f));
        bool found = false; float best = float.PositiveInfinity; Vector3 bestPoint = default;
        for (int y = Mathf.FloorToInt(rect.y); y < Mathf.CeilToInt(rect.yMax); y += step)
        for (int x = Mathf.FloorToInt(rect.x); x < Mathf.CeilToInt(rect.xMax); x += step)
        {
            if (texture != null && texture.isReadable && texture.GetPixel(x, y).a <= 1f / 255f) continue;
            Vector3 local = new(Mathf.Lerp(bounds.min.x, bounds.max.x, (x + .5f - rect.x) / rect.width),
                Mathf.Lerp(bounds.min.y, bounds.max.y, (y + .5f - rect.y) / rect.height), 0);
            Vector3 point = renderer.transform.TransformPoint(local);
            if (!WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, point,
                    out ArcanumObjectId id, out ObjectType type)
                || id != owner.WorldObject.Identity || type != ObjectType.Scenery) continue;
            float distance = Mathf.Pow(x + .5f - rect.center.x, 2) + Mathf.Pow(y + .5f - rect.center.y, 2);
            if (distance >= best) continue;
            found = true; best = distance; bestPoint = point;
        }
        worldPoint = bestPoint; return found;
    }

    private static Vector2Int FindApproach(WorldObjectSectorLoader loader, Vector2Int target)
    {
        var planner = new InteractionApproachPlanner(); var route = new List<Vector2Int>();
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            var tile = new Vector2Int(x, y); int distance = InteractionRangeRules.Distance(tile, target);
            if (distance < 3 || distance > 10 || !loader.NavigationMap.IsWalkable(tile)) continue;
            if (planner.TryPlan(loader.NavigationMap, tile, target, InteractionRangeRules.PortalUseRange,
                    out _, route)) return tile;
        }
        return new Vector2Int(-1, -1);
    }

    private static Vector2Int FindMove(WorldObjectSectorLoader loader, Vector2Int start)
    {
        for (int radius = 2; radius < 10; radius++)
        for (int y = Math.Max(0, start.y - radius); y <= Math.Min(63, start.y + radius); y++)
        for (int x = Math.Max(0, start.x - radius); x <= Math.Min(63, start.x + radius); x++)
        {
            var tile = new Vector2Int(x, y);
            if (tile != start && loader.NavigationMap.IsWalkable(tile)) return tile;
        }
        return new Vector2Int(-1, -1);
    }

    private static Vector3 TileWorld(WorldObjectSectorLoader loader, Vector2Int tile)
        => loader.transform.TransformPoint(IsoProjection.TileToWorld(tile.x, tile.y, loader.PixelsPerUnit));
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
        Check(Object.FindFirstObjectByType<PlayerNavigationController>().Player
              == Object.FindFirstObjectByType<ProductionPlayerLifecycle>().Presentation,
            "navigation bound only to production PC");
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
    { if (!condition) throw new InvalidOperationException("M7B validation FAIL: " + label); }
    private static void Track(string _, string __, LogType type)
    { if (type == LogType.Warning) _warnings++; else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++; }
}
