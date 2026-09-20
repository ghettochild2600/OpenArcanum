using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M7EWorldMapTravel")]
    public sealed class M7EWorldMapTravelTests
    {
        private const string BatesReturnSector = "maps/arcanum1-024-fixed/68853695432.sec";
        private static readonly Vector2Int BatesReturn = new(61976, 65664);
        private static readonly AreaId Tarant = new(21);
        private static readonly AreaId UnavailableKnaTha = new(58);
        private static readonly WorldMapTile TarantTile = new(62243, 65664);

        private static DatVirtualFileSystem _vfs;
        private static AreaList _areas;
        private static MapList _maps;
        private static MapTransitionResolver _transitions;
        private static WorldMapTravelSource _travelSource;

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private Owner _owner;

        [OneTimeSetUp]
        public void LoadAuthenticResources()
        {
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            if (string.IsNullOrEmpty(module)) Assert.Ignore("The local clean Arcanum module archive is unavailable.");
            _vfs = new DatVirtualFileSystem();
            _vfs.MountFile(module);
            foreach (string archive in new[] { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" })
            {
                string path = GameDataLocator.Find(archive);
                if (!string.IsNullOrEmpty(path)) _vfs.MountFile(path);
            }
            _areas = AreaList.FromMes(MesReader.Read(_vfs.ReadAllBytes("mes/gamearea.mes")));
            _maps = MapList.Read(_vfs.ReadAllBytes("rules/maplist.mes"));
            _transitions = new MapTransitionResolver(_maps, _vfs.Exists, _vfs.ReadAllBytes);
            _travelSource = new WorldMapTravelSource(_maps, _transitions, _vfs.Exists, _vfs.ReadAllBytes);
        }

        [OneTimeTearDown] public void ReleaseResources() => _vfs?.Dispose();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M7EWorldMapTravelTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _owner = new Owner(_session);
            _session.RegisterObjectOwner(_owner);
            BindSources(_session, _travelSource, _transitions);
            Assert.That(_session.SelectSector(BatesReturnSector), Is.True);
            Assert.That(SectorCoordinate.TryParse(BatesReturnSector, out SectorCoordinate sourceSector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                BatesReturnSector, sourceSector.ToLocal(BatesReturn), 0x28100000u);
            _session.BindPlayer(BatesReturnSector, _pc, Runtime(_root, "Player"));
            _session.Characters.GetOrCreateDevelopmentPlayer(_pc.Identity);
            _session.Progression.GetOrCreateDevelopmentPlayer(_pc.Identity);
            _session.Vitality.GetOrCreateDevelopmentPlayer(_pc.Identity);
            _session.DerivedStats.GetOrCreateDevelopmentPlayer(_pc.Identity);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticCoordinatesKeepGlobalSectorAndLocalSpacesDistinct()
        {
            Assert.That(_maps.TryGet(1, out MapListEntry start), Is.True);
            Assert.That(start.Type, Is.EqualTo(MapType.StartMap));
            Assert.That(start.Name, Is.EqualTo("Arcanum1-024-fixed"));
            Assert.That(_areas.TryGet(Tarant, out Area area), Is.True);
            Assert.That(new WorldMapTile(area.TileX, area.TileY), Is.EqualTo(TarantTile));

            WorldMapSector source = WorldMapSector.FromTile(new WorldMapTile(BatesReturn.x, BatesReturn.y));
            WorldMapSector destination = WorldMapSector.FromTile(TarantTile);
            Assert.That(source, Is.EqualTo(new WorldMapSector(968, 1026)));
            Assert.That(destination, Is.EqualTo(new WorldMapSector(972, 1026)));
            Assert.That(SectorCoordinate.TryParse(BatesReturnSector, out SectorCoordinate sourceCoordinate), Is.True);
            Assert.That(sourceCoordinate.ToLocal(BatesReturn), Is.EqualTo(new Vector2Int(24, 0)));
            var destinationCoordinate = new SectorCoordinate("maps/arcanum1-024-fixed", 972, 1026);
            Assert.That(destinationCoordinate.ToLocal(new Vector2Int(62243, 65664)),
                Is.EqualTo(new Vector2Int(35, 0)));
        }

        [Test]
        public void AuthenticRetailTopologyPlansOneDeterministicTarantRoute()
        {
            Assert.That(_travelSource.TryResolve(out MapListEntry start, out WorldMapRouteTopology topology,
                out WorldMapTravelSourceFailure failure, out string detail), Is.True, $"{failure}: {detail}");
            Assert.That(start.MapId, Is.EqualTo(1));
            Assert.That(topology.Width, Is.EqualTo(2000));
            Assert.That(topology.Height, Is.EqualTo(2000));
            var planner = new WorldMapRoutePlanner();
            var source = new WorldMapTile(BatesReturn.x, BatesReturn.y);
            Assert.That(planner.TryPlan(topology, source, Tarant, TarantTile, out WorldMapRoute first), Is.True);
            Assert.That(planner.TryPlan(topology, source, Tarant, TarantTile, out WorldMapRoute second), Is.True);
            Assert.That(first.StepCount, Is.EqualTo(4));
            Assert.That(first.Rotations, Is.EqualTo(new[] { 5, 5, 5, 5 }));
            Assert.That(second.Rotations, Is.EqualTo(first.Rotations));
            Assert.That(first.Sectors[0], Is.EqualTo(new WorldMapSector(968, 1026)));
            Assert.That(first.Sectors[first.Sectors.Count - 1], Is.EqualTo(new WorldMapSector(972, 1026)));
            Assert.That(first.Sectors.All(value => !topology.IsBlocked(value)), Is.True);
            Debug.Log($"M7E AUTHENTIC ROUTE: source={first.Sectors[0]}; "
                      + $"destination={first.Sectors[first.Sectors.Count - 1]}; steps={first.StepCount}.");
        }

        [Test]
        public void SourceStyleBlockedTerrainFailsWithoutInventingAnotherPathfinder()
        {
            var terrain = new ushort[20 * 20];
            var blocked = new bool[32];
            blocked[0] = true;
            var topology = new WorldMapRouteTopology("maps/test", 20, 20, terrain, blocked,
                Array.Empty<long>(), _ => false);
            Assert.That(new WorldMapRoutePlanner().TryPlan(topology,
                new WorldMapTile(64, 64), new AreaId(1), new WorldMapTile(640, 640), out _), Is.False);
        }

        [Test]
        public void AuthenticUnroutableKnaThaFailsWithoutSessionMutation()
        {
            _session.Campaign.DiscoverArea(UnavailableKnaTha);
            string before = _session.SaveGames.SerializeCurrentSession();
            WorldMapSelectionResult selection =
                _session.WorldMapDestinations.TrySelectWorldArea(UnavailableKnaTha);
            Assert.That(selection.Succeeded, Is.True, selection.Detail);

            WorldMapTravelResult result =
                _session.RequestWorldMapTravel(_pc.Identity, selection.Request);

            Assert.That(result.Failure, Is.EqualTo(WorldMapTravelFailure.RouteUnavailable));
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
            Assert.That(_session.SelectedSector, Is.EqualTo(BatesReturnSector));
            Assert.That(_session.WorldMapTravel.State.Phase, Is.EqualTo(WorldMapTravelPhase.Idle));
        }

        [Test]
        public void KnownRequestExecutesExactLifecycleAndAuthenticStartMapArrival()
        {
            WorldMapTravelRequest request = KnownRequest();
            var phases = new List<WorldMapTravelPhase>();
            _session.WorldMapTravel.PhaseChanged += state => phases.Add(state.Phase);
            PersistentPlayerState player = _session.PlayerState;

            WorldMapTravelResult result = _session.RequestWorldMapTravel(player.Identity, request);

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(phases, Is.EqualTo(new[] { WorldMapTravelPhase.Planning, WorldMapTravelPhase.Travelling,
                WorldMapTravelPhase.Arriving, WorldMapTravelPhase.Completed, WorldMapTravelPhase.Idle }));
            Assert.That(_session.WorldMapTravel.State.Phase, Is.EqualTo(WorldMapTravelPhase.Idle));
            Assert.That(_session.PlayerState, Is.SameAs(player));
            Assert.That(player.MapPosition, Is.EqualTo(new Vector2(62243, 65664)));
            Assert.That(_session.SelectedSector,
                Is.EqualTo(new SectorCoordinate("maps/arcanum1-024-fixed", 972, 1026).Path));
            Assert.That(result.Transition.Destination.MapId, Is.EqualTo(1));
            Assert.That(result.Transition.Destination.GlobalTile, Is.EqualTo(new Vector2Int(62243, 65664)));
        }

        [Test]
        public void UnknownAndInvalidRequestsFailBeforeAnySessionMutation()
        {
            string before = _session.SaveGames.SerializeCurrentSession();
            WorldMapTravelResult unknown = _session.RequestWorldMapTravel(_pc.Identity,
                new WorldMapTravelRequest(UnavailableKnaTha, new WorldMapTile(91902, 39305)));
            WorldMapTravelResult invalid = _session.RequestWorldMapTravel(_pc.Identity,
                new WorldMapTravelRequest(new AreaId(0), default));
            Assert.That(unknown.Failure, Is.EqualTo(WorldMapTravelFailure.Unavailable));
            Assert.That(invalid.Failure, Is.EqualTo(WorldMapTravelFailure.InvalidRequest));
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
            Assert.That(_session.WorldMapTravel.State.Phase, Is.EqualTo(WorldMapTravelPhase.Idle));
        }

        [Test]
        public void TamperedDestinationCoordinateFailsBeforePlanningOrMutation()
        {
            _session.Campaign.DiscoverArea(Tarant);
            string before = _session.SaveGames.SerializeCurrentSession();
            var tampered = new WorldMapTravelRequest(Tarant, new WorldMapTile(62244, 65664));
            Assert.That(_session.RequestWorldMapTravel(_pc.Identity, tampered).Failure,
                Is.EqualTo(WorldMapTravelFailure.InvalidRequest));
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
        }

        [Test]
        public void ReentrantRequestIsBusyWhileOriginalCompletesOnce()
        {
            WorldMapTravelRequest request = KnownRequest();
            WorldMapTravelResult nested = default;
            int attempts = 0;
            _session.WorldMapTravel.PhaseChanged += state =>
            {
                if (state.Phase != WorldMapTravelPhase.Travelling || attempts++ != 0) return;
                nested = _session.RequestWorldMapTravel(_pc.Identity, request);
            };
            WorldMapTravelResult result = _session.RequestWorldMapTravel(_pc.Identity, request);
            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(nested.Failure, Is.EqualTo(WorldMapTravelFailure.Busy));
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo(new Vector2(62243, 65664)));
        }

        [Test]
        public void NonStartMapContextIsRejectedWithoutArbitraryOriginSubstitution()
        {
            var otherRoot = new GameObject("M7E local context");
            try
            {
                var session = otherRoot.AddComponent<WorldMapSessionCoordinator>();
                session.RegisterObjectOwner(new Owner(session));
                BindSources(session, _travelSource, _transitions);
                const string local = "maps/bates mansion lev 1/67108865.sec";
                Assert.That(session.SelectSector(local), Is.True);
                var pc = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    local, new Vector2(40, 28), 0x28100000u);
                session.BindPlayer(local, pc, Runtime(otherRoot, "Local Player"));
                session.Campaign.DiscoverArea(Tarant);
                Assert.That(session.RequestWorldMapTravel(pc.Identity,
                    session.WorldMapDestinations.TrySelectWorldArea(Tarant).Request).Failure,
                    Is.EqualTo(WorldMapTravelFailure.InvalidContext));
                Assert.That(pc.MapPosition, Is.EqualTo(new Vector2(104, 92)));
                Assert.That(session.SelectedSector, Is.EqualTo(local));
            }
            finally { Object.DestroyImmediate(otherRoot); }
        }

        [Test]
        public void ArrivalPreflightFailureLeavesSourceAndOwnersUntouched()
        {
            var badTransitions = new MapTransitionResolver(_maps, _vfs.Exists,
                path => path.EndsWith("/map.prp", StringComparison.OrdinalIgnoreCase)
                    ? new byte[1] : _vfs.ReadAllBytes(path));
            _session.BindWorldMapTravelSource(new WorldMapTravelSource(
                _maps, badTransitions, _vfs.Exists, _vfs.ReadAllBytes));
            WorldMapTravelRequest request = KnownRequest();
            string before = _session.SaveGames.SerializeCurrentSession();
            int clears = _owner.Clears;
            WorldMapTravelResult result = _session.RequestWorldMapTravel(_pc.Identity, request);
            Assert.That(result.Failure, Is.EqualTo(WorldMapTravelFailure.ArrivalUnavailable));
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
            Assert.That(_owner.Clears, Is.EqualTo(clears));
            Assert.That(_session.WorldMapTravel.State.Phase, Is.EqualTo(WorldMapTravelPhase.Idle));
        }

        [Test]
        public void DestinationPresentationFailureRollsBackCompleteSourceState()
        {
            WorldMapTravelRequest request = KnownRequest();
            string before = _session.SaveGames.SerializeCurrentSession();
            string destination = new SectorCoordinate("maps/arcanum1-024-fixed", 972, 1026).Path;
            _owner.RejectSector = destination;
            WorldMapTravelResult result = _session.RequestWorldMapTravel(_pc.Identity, request);
            Assert.That(result.Failure, Is.EqualTo(WorldMapTravelFailure.PresentationFailed));
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
            Assert.That(_session.SelectedSector, Is.EqualTo(BatesReturnSector));
            Assert.That(_owner.PresentedSector, Is.EqualTo(BatesReturnSector));
            Assert.That(_session.WorldMapTravel.State.Phase, Is.EqualTo(WorldMapTravelPhase.Idle));
        }

        [Test]
        public void TravelPreservesPcCampaignCharacterInventoryAndClearsLocalIntent()
        {
            PersistentPlayerState player = _pc;
            PersistentCharacterState character = _session.Characters.Get(player.Identity);
            PersistentCharacterProgressionState progression = _session.Progression.Get(player.Identity);
            _session.Campaign.SetFlag(77, 1);
            Assert.That(_session.Progression.IncreaseSkill(player.Identity, CharacterSkill.Throwing),
                Is.EqualTo(SkillIncreaseResult.Success));
            var goldPrototype = new ObjectProtoInfo(9056, ObjectType.Gold, 0x40000000u) { GoldQuantity = 37 };
            _session.BindPrototypeSource(number => number == 9056 ? goldPrototype : null);
            ItemCreationResult created = _session.CreateItem(9056, ObjectPlacement.ContainedBy(player.Identity));
            Assert.That(created.Succeeded, Is.True);
            _session.SetPlayerDestination(new Vector2Int(62000, 65664));

            Assert.That(_session.RequestWorldMapTravel(player.Identity, KnownRequest()).Succeeded, Is.True);

            Assert.That(_session.PlayerState, Is.SameAs(player));
            Assert.That(_session.Characters.Get(player.Identity), Is.SameAs(character));
            Assert.That(_session.Progression.Get(player.Identity), Is.SameAs(progression));
            Assert.That(_session.Progression.GetPurchasedSkillPoints(player.Identity, CharacterSkill.Throwing),
                Is.EqualTo(1));
            Assert.That(_session.Campaign.GetFlag(77), Is.EqualTo(1));
            Assert.That(_session.Campaign.KnownAreas, Is.EqualTo(new[] { Tarant }));
            Assert.That(_session.States[created.State.Identity], Is.SameAs(created.State));
            Assert.That(created.State.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(player.Identity)));
            Assert.That(created.State.StackQuantity, Is.EqualTo(37));
            Assert.That(player.Destination, Is.Null);
        }

        [Test]
        public void StablePostTravelV1SaveLoadRestoresMapPositionAndDiscoveryWithoutNewSchema()
        {
            Assert.That(_session.RequestWorldMapTravel(_pc.Identity, KnownRequest()).Succeeded, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"version\": 1"));
            Assert.That(json, Does.Not.Contain("worldMapTravel"));
            _session.ResetAuthoritativeSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo(new Vector2(62243, 65664)));
            Assert.That(_session.Campaign.IsAreaKnown(Tarant), Is.True);
            Assert.That(_session.WorldMapTravel.State.Phase, Is.EqualTo(WorldMapTravelPhase.Idle));
        }

        [Test]
        public void MissingTravelSourceFailsExplicitlyWithoutMutation()
        {
            var otherRoot = new GameObject("M7E source-less");
            try
            {
                var session = otherRoot.AddComponent<WorldMapSessionCoordinator>();
                session.RegisterObjectOwner(new Owner(session));
                session.BindAreaSource(_areas);
                Assert.That(session.SelectSector(BatesReturnSector), Is.True);
                SectorCoordinate.TryParse(BatesReturnSector, out SectorCoordinate sector);
                var pc = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    BatesReturnSector, sector.ToLocal(BatesReturn), 0x28100000u);
                session.BindPlayer(BatesReturnSector, pc, Runtime(otherRoot, "Source-less Player"));
                session.Campaign.DiscoverArea(Tarant);
                WorldMapTravelRequest request = session.WorldMapDestinations.TrySelectWorldArea(Tarant).Request;
                Assert.That(session.RequestWorldMapTravel(pc.Identity, request).Failure,
                    Is.EqualTo(WorldMapTravelFailure.NoSourceData));
                Assert.That(pc.MapPosition, Is.EqualTo((Vector2)BatesReturn));
            }
            finally { Object.DestroyImmediate(otherRoot); }
        }

        [Test]
        public void ExistingDestinationPresenterSubmitsRequestToAuthorityOnce()
        {
            _session.Campaign.DiscoverArea(Tarant);
            var presenter = _root.AddComponent<ProductionWorldMapDestinationPresenter>();
            int emitted = 0;
            presenter.TravelRequested += _ => emitted++;
            presenter.Open();
            Assert.That(presenter.Select(Tarant).Succeeded, Is.True);
            Assert.That(emitted, Is.EqualTo(1));
            Assert.That(presenter.LastTravel.Succeeded, Is.True, presenter.LastTravel.Detail);
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo(new Vector2(62243, 65664)));
            Assert.That(presenter.IsOpen, Is.False);
        }

        private WorldMapTravelRequest KnownRequest()
        {
            _session.Campaign.DiscoverArea(Tarant);
            WorldMapSelectionResult selection = _session.WorldMapDestinations.TrySelectWorldArea(Tarant);
            Assert.That(selection.Succeeded, Is.True, selection.Detail);
            return selection.Request;
        }

        private static void BindSources(WorldMapSessionCoordinator session, WorldMapTravelSource travel,
            MapTransitionResolver transitions)
        {
            session.BindAreaSource(_areas);
            session.BindMapTransitionSource(transitions);
            session.BindWorldMapTravelSource(travel);
        }

        private static WorldObject Runtime(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = ObjectType.Pc;
            return runtime;
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public string RejectSector { get; set; }
            public int Clears { get; private set; }
            public string ConfiguredSector => BatesReturnSector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public bool PresentSector(string sectorPath)
            {
                string normalized = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                if (normalized == RejectSector) return false;
                PresentedSector = normalized;
                _session.BeginSector(normalized);
                return true;
            }
            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                Clears++;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
