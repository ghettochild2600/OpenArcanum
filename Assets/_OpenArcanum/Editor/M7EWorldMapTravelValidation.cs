using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using Arcanum.World.Demo;
using OpenArcanum.Rendering;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class M7EWorldMapTravelValidation
{
    private const string BatesReturnSector = "maps/arcanum1-024-fixed/68853695432.sec";
    private const string TarantSector = "maps/arcanum1-024-fixed/68853695436.sec";
    private static readonly Vector2 BatesReturn = new(61976, 65664);
    private static readonly Vector2 TarantArrival = new(62243, 65664);
    private static readonly AreaId Tarant = new(21);
    private static readonly AreaId KnaTha = new(58);
    private static bool _running;
    private static int _warnings;
    private static int _errors;

    [MenuItem("OpenArcanum/M7E/Prepare Physical Bates to Tarant Validation")]
    private static void Prepare()
    {
        if (!Application.isPlaying || _running)
            throw new InvalidOperationException("Enter TestTerrain Play mode; run only one M7E harness.");
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
        try
        {
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(BatesReturnSector), "authentic Bates return sector loads");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            ProductionPlayerLifecycle lifecycle = Object.FindFirstObjectByType<ProductionPlayerLifecycle>();
            ProductionWorldMapDestinationPresenter presenter =
                Object.FindFirstObjectByType<ProductionWorldMapDestinationPresenter>();
            Check(loader != null && lifecycle != null && presenter != null,
                "production loader, PC lifecycle, and destination presenter exist");
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            PersistentPlayerState player = session.PlayerState;
            ArcanumObjectId pc = player.Identity;
            Check(session.SetMovementState(pc, new Vector2(24, 0), player.ArtId, false),
                "production PC reaches exact Bates return tile");
            Check(player.MapPosition == BatesReturn, "authoritative source is global tile (61976,65664)");

            PersistentCharacterState character = session.Characters.Get(pc);
            PersistentCharacterVitalityState vitality = session.Vitality.Get(pc);
            PersistentCharacterProgressionState progression = session.Progression.Get(pc);
            PersistentCharacterDerivedState derived = session.DerivedStats.Get(pc);
            session.Campaign.SetFlag(77, 1);
            Check(session.Progression.IncreaseSkill(pc, CharacterSkill.Throwing) == SkillIncreaseResult.Success,
                "M4 progression fixture mutates before travel");
            ItemCreationResult item = session.CreateItem(9056, ObjectPlacement.ContainedBy(pc));
            Check(item.Succeeded, "M3 Gold fixture is created in the PC inventory");
            session.SetPlayerDestination(new Vector2Int(28, 0));
            session.Campaign.DiscoverArea(Tarant);

            string beforeFailure = session.SaveGames.SerializeCurrentSession();
            Check(session.RequestWorldMapTravel(pc,
                      new WorldMapTravelRequest(new AreaId(999), default)).Failure
                  == WorldMapTravelFailure.InvalidRequest,
                "invalid destination fails closed");
            Check(session.SaveGames.SerializeCurrentSession() == beforeFailure,
                "invalid destination leaves the source session unchanged");
            session.Campaign.DiscoverArea(KnaTha);
            WorldMapSelectionResult blocked = session.WorldMapDestinations.TrySelectWorldArea(KnaTha);
            Check(blocked.Succeeded, "K'na Tha is a valid known destination before route proof");
            string beforeRouteFailure = session.SaveGames.SerializeCurrentSession();
            Check(session.RequestWorldMapTravel(pc, blocked.Request).Failure
                  == WorldMapTravelFailure.RouteUnavailable,
                "authentic K'na Tha route fails without substitute routing");
            Check(session.SaveGames.SerializeCurrentSession() == beforeRouteFailure
                  && session.SelectedSector == BatesReturnSector && player.MapPosition == BatesReturn,
                "failed route rolls back without presentation or domain mutation");

            presenter.Open();
            Debug.Log("M7E TRAVEL READY: physically click Tarant [area 21].");
            yield return Wait(() => presenter.HasSelection, "literal Tarant destination click is received");
            Check(presenter.LastSelection.Succeeded && presenter.LastSelection.Request.AreaId == Tarant,
                "literal click emits the source-backed Tarant request");
            Check(presenter.LastTravel.Succeeded, "production presenter submits to world-travel authority");
            WorldMapRoute route = presenter.LastTravel.Route;
            Check(route != null && route.StepCount == 4
                  && route.Rotations.SequenceEqual(new[] { 5, 5, 5, 5 }),
                "authentic route is exactly 5,5,5,5");
            Check(session.WorldMapTravel.State.Phase == WorldMapTravelPhase.Idle,
                "bounded lifecycle returns to Idle");
            Check(session.SelectedSector == TarantSector && player.MapPosition == TarantArrival,
                "arrival is exact START_MAP Tarant sector and global tile");
            Check(session.PlayerState == player && player.Identity == pc,
                "travel retains the same production PC state and identity");
            Check(session.Characters.Get(pc) == character && session.Vitality.Get(pc) == vitality
                  && session.Progression.Get(pc) == progression && session.DerivedStats.Get(pc) == derived,
                "M4 character domains retain their authoritative state objects");
            Check(session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.Throwing) == 1
                  && session.Campaign.GetFlag(77) == 1 && session.Campaign.IsAreaKnown(Tarant)
                  && session.States.TryGetValue(item.State.Identity, out PersistentObjectState retained)
                  && retained == item.State && retained.ParentIdentity == pc,
                "M3-M7 inventory, progression, campaign, and discovery state survives travel");
            Check(player.Destination == null, "transient local navigation intent is normalized");
            CheckUnique(loader, pc);

            string save = session.SaveGames.SerializeCurrentSession();
            Check(save.Contains("\"version\": 1") && !save.Contains("worldMapTravel"),
                "stable arrival remains V1 without transient travel serialization");
            session.ResetAuthoritativeSession();
            Check(session.SaveGames.LoadJson(save).Succeeded, "post-arrival V1 save reloads");
            yield return null;
            loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
            player = session.PlayerState;
            Check(player.Identity == pc && session.SelectedSector == TarantSector
                  && player.MapPosition == TarantArrival && session.Campaign.IsAreaKnown(Tarant),
                "save/load restores exact Tarant arrival, identity, and discovery");

            foreach (GraphicsMode mode in new[] { GraphicsMode.Original, GraphicsMode.Enhanced, GraphicsMode.Original })
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(mode);
                loader.RebuildVisuals();
                Check(session.PlayerState.Identity == pc && session.SelectedSector == TarantSector
                      && session.PlayerState.MapPosition == TarantArrival,
                    "graphics rebuild preserves authoritative post-travel state");
                CheckUnique(loader, pc);
            }

            Application.logMessageReceived -= Track;
            Check(_warnings == 0 && _errors == 0, $"warnings={_warnings}; errors={_errors}");
            Debug.Log($"M7E PLAYMODE VALIDATION PASS: source={BatesReturn}; destination=area21@{TarantArrival}; "
                      + $"route=5,5,5,5; sector={TarantSector}; samePC={pc}; M3-M7=True; rollback=True; "
                      + $"V1=True; Original->Enhanced->Original; warnings={_warnings}; errors={_errors}.");
        }
        finally
        {
            OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
            Application.logMessageReceived -= Track;
            _running = false;
        }
    }

    private static void CheckUnique(WorldObjectSectorLoader loader, ArcanumObjectId pc)
    {
        Check(Count<WorldMapSessionCoordinator>() == 1 && Count<TileMapDemo>() == 1
              && Count<WorldObjectSectorLoader>() == 1 && Count<ProductionPlayerLifecycle>() == 1
              && Count<PlayerNavigationController>() == 1 && Count<PlayerInteractionController>() == 1
              && Count<ProductionWorldMapDestinationPresenter>() == 1,
            "one coordinator/terrain/loader/lifecycle/navigation/interaction/destination presenter");
        Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(value => value.name == "WorldObjects") == 1,
            "one WorldObjects root");
        Check(Object.FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Count(value => value.Identity == pc) == 1,
            "one production PC runtime/presentation");
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
        if (!condition) throw new InvalidOperationException("M7E validation FAIL: " + label);
    }

    private static void Track(string _, string __, LogType type)
    {
        if (type == LogType.Warning) _warnings++;
        else if (type is LogType.Error or LogType.Assert or LogType.Exception) _errors++;
    }
}
