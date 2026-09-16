using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using Arcanum.World.Demo;
using Newtonsoft.Json.Linq;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real-data setup and observation only; map changes under test are driven by normal navigation.</summary>
internal static class M7ALocalTransitionValidation
{
    private const string Source = "maps/bates mansion lev 1/67108865.sec";
    private const string Destination = "maps/arcanum1-024-fixed/68853695432.sec";
    private static readonly Vector2 Arrival = new(61976, 65664);
    private static bool _running;
    private static int _warnings, _errors;

    [MenuItem("OpenArcanum/M7A/Prepare Physical Transition Validation")]
    private static void Run()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M7A harness.");
        var loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        loader.StartCoroutine(Validate(loader));
    }

    private static IEnumerator Validate(WorldObjectSectorLoader loader)
    {
        _running = true;
        _warnings = _errors = 0;
        Application.logMessageReceived += Track;
        GraphicsMode initial = OpenArcanumGraphicsSettings.Mode;
        var session = loader.Session;
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector("maps/arcanum1-024-fixed/101602821844.sec"), "inventory source loads");
            yield return null;
            ArcanumObjectId pc = session.PlayerState.Identity;
            var armor = session.States.Values.Single(s => s.Identity.Key == "G_0435F503_6600_6342_97B2_6D9E1A85A2F2");
            var ammo = session.States.Values.Single(s => s.Identity.Key == "G_9239E097_A8D2_C147_9F58_76077340C60E");
            Check(session.TransferItem(armor.Identity, armor.Placement, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "authentic armor retained by PC");
            Check(session.EquipItem(pc, armor.Identity, WornLocation.Armor).Succeeded, "authentic armor equipped");
            Check(session.TransferItem(ammo.Identity, ammo.Placement, ObjectPlacement.ContainedBy(pc)).Succeeded,
                "authentic quantity-60 Ammo retained");
            var dynamic = session.CreateItem(10078, ObjectPlacement.ContainedBy(pc));
            Check(dynamic.Succeeded, "dynamic Food created through production authority");
            session.AddGold(pc, 123);
            session.Progression.AwardExperience(pc, 2100);
            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Persuasion) == SkillIncreaseResult.Success,
                "non-default purchased skill");
            Check(session.Progression.SetTrainingLevel(pc, CharacterSkill.Persuasion, SkillTrainingLevel.Apprentice)
                  == TrainingAssignmentResult.Success, "non-default training");
            session.Vitality.ApplyHitPointDamage(pc, 3);
            session.Vitality.ApplyFatigueDamage(pc, 4);
            session.DerivedStats.SetAlignment(pc, 50);
            session.Campaign.SetFlag(77, 1);
            session.Campaign.SetVar(10, 321);
            session.Campaign.SetPcQuestState(1005, (int)QuestState.Mentioned);
            session.Campaign.SetPcQuestState(1005, (int)QuestState.Accepted);
            // Pre-register both real maps so exact snapshot comparisons distinguish lifecycle from first-time decoding.
            Check(session.SelectSector(Destination), "real destination preloads");
            yield return null;
            Check(session.SelectSector(Source), "real Bates source loads");
            yield return null;
            var life = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            var nav = Object.FindFirstObjectByType<PlayerNavigationController>();
            var input = Object.FindFirstObjectByType<PlayerClickMoveInput>();
            var interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
            Check(life.SpawnAndBind(), "production PC explicitly bound");
            var start = new Vector2Int(40, 28); // MapList's authentic Bates start (104,92), sector-local.
            Check(loader.NavigationMap.IsWalkable(start)
                  && session.SetMovementState(pc, start, session.PlayerState.ArtId, false), "authentic source start");
            yield return null;
            Check(nav.Player == life.Presentation && nav.Player.Identity == pc, "normal PC-only binding");
            Vector2Int jump = FindJump(loader, start);
            Check(jump.x >= 0, "reachable unobscured authentic passive tile");
            Focus(loader, jump);
            yield return null;
            string sourceSave = session.SaveGames.SerializeCurrentSession();
            JObject before = Snapshot(session);
            var playerState = session.PlayerState;
            var characterState = session.Characters.Get(pc);
            var domainObjects = session.States.ToDictionary(x => x.Key, x => x.Value);
            int arrivals = 0;
            int departureFacing = -1;
            Action<string> selected = path => { if (path == Destination) arrivals++; };
            Action<string> unloaded = path => { if (path == Source) departureFacing = CritterArtResolver.RotationOf(session.PlayerState.ArtId); };
            session.SectorSelected += selected;
            session.SectorUnloading += unloaded;
            try
            {
                Debug.Log($"M7A PHYSICAL READY: literal center-of-Game-view ground click; start={start}; "
                          + $"jumpLocal={jump}; jumpGlobal={new Vector2Int(jump.x + 64, jump.y + 64)}; pc={pc}; "
                          + $"source={Source}; destination={Destination}; GameScreen={Camera.main.WorldToScreenPoint(TileWorld(loader, jump))}.");
                yield return Wait(() => input.LastClickedTile == jump && input.LastClickAccepted,
                    "literal physical ground click accepted");
                Debug.Log($"M7A PHYSICAL INPUT: tile={input.LastClickedTile}; accepted={input.LastClickAccepted}; route="
                          + string.Join(";", nav.Route));
                yield return Wait(() => session.SelectedSector == Destination || !nav.IsMoving,
                    "physical navigation settles");
                Check(session.SelectedSector == Destination,
                    $"production passive transition ({session.LastMapTransitionResult.Failure}: {session.LastMapTransitionResult.Detail})");
                Check(session.PlayerState == playerState && session.Characters.Get(pc) == characterState,
                    "same PC and character-state references");
                Check(domainObjects.All(pair => session.States.TryGetValue(pair.Key, out var value)
                                                && ReferenceEquals(pair.Value, value)), "all existing object-state references");
                Check(session.PlayerState.Identity == pc && session.PlayerState.MapPosition == Arrival,
                    "exact authored arrival and same ObjectID");
                Check(CritterArtResolver.RotationOf(session.PlayerState.ArtId) == departureFacing
                      && ((session.PlayerState.ArtId >> 6) & 31) == 0, "preserved source-facing STAND");
                Check(nav.GlobalDestination == null && nav.Destination == null && !nav.IsMoving
                      && interaction.PendingCommand == null, "old route and interaction clear");
                Check(JToken.DeepEquals(before, Snapshot(session)),
                    "all M3-M6 object/character/campaign state unchanged (movement excluded only)");
                yield return null;
                CheckUnique(loader, pc);
                Check(arrivals == 1, "one activation produces one destination selection");
                Debug.Log($"M7A PHYSICAL ARRIVAL PASS: pc={pc}; map={session.CurrentMap}; global={session.PlayerState.MapPosition}; "
                          + $"local={session.PlayerState.TilePosition}; facing={departureFacing}; action=STAND; activations={arrivals}; "
                          + $"objects={domainObjects.Count}; attributes/vitality/progression/skills/derived/alignment/inventory/equipment/stacks/Gold/dynamic/campaign/journal=exact.");

                string directory = Path.Combine(Application.temporaryCachePath, "OpenArcanum-M7A-" + Guid.NewGuid().ToString("N"));
                var slots = new SessionSaveSlotService(session, directory);
                Check(slots.SaveSlot("m7arrival").Succeeded, "V1 exact-arrival destination slot saves");
                string saved = session.SaveGames.SerializeCurrentSession();

                Vector2Int next = FindMove(loader, nav.Player.Tile);
                Check(next.x >= 0, "unobscured destination navigation target");
                Focus(loader, next);
                yield return null;
                Debug.Log($"M7A RESUME READY: literal center Game-view click; destinationLocal={next}.");
                yield return Wait(() => input.LastClickedTile == next && input.LastClickAccepted,
                    "literal post-arrival navigation click");
                yield return Wait(() => !nav.IsMoving, "post-arrival navigation completes");
                Check(session.PlayerState.TilePosition == (Vector2)next, "post-arrival movement exact");

                session.AddGold(pc, 1);
                Check(nav.TrySetDestination(FindMove(loader, nav.Player.Tile)), "change destination location normally");
                yield return Wait(() => !nav.IsMoving, "changed location settles");
                Check(slots.LoadSlot("m7arrival").Succeeded, "destination slot loads directly");
                yield return null;
                Check(session.SaveGames.SerializeCurrentSession() == saved, "exact destination V1 world/domain restoration");
                Check(session.PlayerState.Identity == pc && session.SelectedSector == Destination,
                    "slot restores destination map and PC");
                Check(session.PlayerState.MapPosition == Arrival, "slot restores exact source-authored arrival");
                Check(nav.TrySetDestination(FindMove(loader, nav.Player.Tile)), "post-load navigation starts");
                yield return Wait(() => !nav.IsMoving, "post-load navigation completes");
                Check(session.PlayerState.MapPosition != Arrival, "post-load navigation moves PC");
                Check(slots.LoadSlot("m7arrival").Succeeded, "restore exact arrival for rebuild proof");
                yield return null;
                var food = session.States[dynamic.State.Identity];
                Vector2 drop = session.PlayerState.TilePosition;
                Check(session.ExecuteInteraction(WorldInteractionCommand.Drop(pc, food.Identity, Destination, drop)).IsSuccess
                      && session.ExecuteInteraction(WorldInteractionCommand.PickUp(pc, food.Identity)).IsSuccess,
                    "post-load real-prototype dynamic drop/pickup continues");
                before = Snapshot(session);
                Vector2 exact = session.PlayerState.MapPosition;
                foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
                {
                    OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                    loader.RebuildVisuals();
                    yield return null;
                    Check(session.PlayerState.MapPosition == exact && session.PlayerState.Identity == pc
                          && JToken.DeepEquals(before, Snapshot(session)), "graphics rebuild preserves map/domain state");
                    CheckUnique(loader, pc);
                }
                Check(session.ReloadSelectedSector(), "destination reload");
                yield return null;
                Check(session.PlayerState.MapPosition == exact && JToken.DeepEquals(before, Snapshot(session)),
                    "destination reload preserves all state");
                CheckUnique(loader, pc);
                Debug.Log($"M7A SAVE/GRAPHICS PASS: V1; destination={Destination}; global={exact}; "
                          + $"slot={slots.SaveDirectory}; Original->Enhanced->Original; reload; post-load drop/pickup.");

                // There is no authored reverse jump. Restore a test source snapshot, never invent a return transition.
                Check(session.SaveGames.LoadJson(sourceSave).Succeeded, "test source snapshot restores for second activation");
                yield return null;
                nav = Object.FindFirstObjectByType<PlayerNavigationController>();
                interaction = Object.FindFirstObjectByType<PlayerInteractionController>();
                int priorArrivals = arrivals;
                var laterJump = new Vector2Int(46, 28);
                var pendingItem = session.CreateItem(10078, ObjectPlacement.InWorld(Source, laterJump));
                Check(pendingItem.Succeeded && interaction.TryPickUp(pendingItem.State.Identity).IsAccepted,
                    "real-prototype pending pickup approaches the passive jump");
                yield return Wait(() => session.SelectedSector == Destination || !nav.IsMoving, "second activation settles");
                Check(session.SelectedSector == Destination && interaction.PendingCommand == null
                      && interaction.LastResult.HasValue
                      && interaction.LastResult.Value.Code == WorldInteractionResultCode.Cancelled,
                    "pending pickup cancels on transition and cannot fire at destination");
                Check(session.LastMapTransitionResult.Destination.Source.GlobalTile == new Vector2Int(109, 92),
                    "normal route-through activates the first entered passive tile, not its later endpoint");
                Check(session.States[pendingItem.State.Identity].Placement == ObjectPlacement.InWorld(Source, laterJump),
                    "cancelled item remains in source world");
                Check(session.ExecuteInteraction(WorldInteractionCommand.PickUp(pc, pendingItem.State.Identity)).Code
                      == WorldInteractionResultCode.ItemNotInWorld, "stale source target cannot execute");
                yield return null;
                CheckUnique(loader, pc);
                Check(arrivals == priorArrivals + 1, "second independent activation occurs exactly once");
                Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
                Debug.Log($"M7A PLAYMODE VALIDATION PASS: physical transition+post-arrival click; exact domain preservation; "
                          + $"pending pickup cancelled; stale target rejected; two unique activations; V1 slot restore; "
                          + $"graphics/reload; no authentic reverse map.jmp; warnings={_warnings}; errors={_errors}.");
            }
            finally { session.SectorSelected -= selected; session.SectorUnloading -= unloaded; }
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initial);
            Application.logMessageReceived -= Track;
            _running = false;
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

    private static Vector2Int FindJump(WorldObjectSectorLoader loader, Vector2Int start)
    {
        var finder = new DeterministicTilePathfinder(); var route = new List<Vector2Int>();
        for (int y = 28; y <= 30; y++) for (int x = 45; x <= 46; x++)
        {
            var tile = new Vector2Int(x, y);
            if (loader.NavigationMap.IsWalkable(tile) && finder.TryFindPath(loader.NavigationMap, start, tile, route)
                && !WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, TileWorld(loader, tile), out _, out _))
                return tile;
        }
        return new Vector2Int(-1, -1);
    }

    private static Vector2Int FindMove(WorldObjectSectorLoader loader, Vector2Int start)
    {
        var finder = new DeterministicTilePathfinder(); var route = new List<Vector2Int>();
        for (int radius = 2; radius <= 8; radius++)
        for (int y = Math.Max(0, start.y - radius); y <= Math.Min(63, start.y + radius); y++)
        for (int x = Math.Max(0, start.x - radius); x <= Math.Min(63, start.x + radius); x++)
        {
            var tile = new Vector2Int(x, y);
            if (tile != start && loader.NavigationMap.IsWalkable(tile)
                && finder.TryFindPath(loader.NavigationMap, start, tile, route)
                && !WorldObjectTargetSelector.TrySelectInteractionTarget(loader.SpriteOwners, TileWorld(loader, tile), out _, out _))
                return tile;
        }
        return new Vector2Int(-1, -1);
    }

    private static Vector3 TileWorld(WorldObjectSectorLoader loader, Vector2Int tile)
        => loader.transform.TransformPoint(IsoProjection.TileToWorld(tile.x, tile.y, loader.PixelsPerUnit));

    private static void Focus(WorldObjectSectorLoader loader, Vector2Int tile)
    {
        Vector3 point = TileWorld(loader, tile); var camera = Camera.main;
        camera.transform.position = new Vector3(point.x, point.y, camera.transform.position.z);
        camera.orthographicSize = 3f;
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ArcanumObjectId pc)
    {
        Check(Count<WorldMapSessionCoordinator>() == 1 && Count<TileMapDemo>() == 1
              && Count<WorldObjectSectorLoader>() == 1 && Count<ProductionPlayerLifecycle>() == 1
              && Count<PlayerNavigationController>() == 1 && Count<PlayerInteractionController>() == 1,
            "one coordinator/terrain/loader/lifecycle/navigation/interaction");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(t => t.name == "WorldObjects") == 1, "one WorldObjects root including inactive roots");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(o => o.Identity == pc) == 1, "one PC runtime/presentation");
        Check(loader.SpriteOwners.Where(o => o?.WorldObject != null).GroupBy(o => o.WorldObject.Identity)
                  .All(g => g.Count() == 1), "no duplicate sprite owners");
        Check(Object.FindFirstObjectByType<PlayerNavigationController>().Player
              == Object.FindFirstObjectByType<ProductionPlayerLifecycle>().Presentation, "PC-only navigation binding");
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
    { if (!condition) throw new InvalidOperationException("M7A validation FAIL: " + label); }
    private static void Track(string _, string __, LogType type)
    { if (type == LogType.Warning) _warnings++; else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++; }
}
