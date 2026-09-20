using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M7DWorldMapDestination")]
    public sealed class M7DWorldMapDestinationTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/96502547529.sec";
        private static readonly AreaId KnaTha = new(58);
        private static readonly AreaId Tarant = new(21);
        private static AreaList _areas;
        private static DatVirtualFileSystem _vfs;

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;

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
        }

        [OneTimeTearDown] public void ReleaseResources() => _vfs?.Dispose();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M7DWorldMapDestinationTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            _session.BindAreaSource(_areas);
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(10, 12), 0x28100000u);
            _session.BindPlayer(Sector, _pc, Runtime("Player", ObjectType.Pc));
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticProjectionPreservesSourceFieldsAndCanonicalOrder()
        {
            Assert.That(_session.WorldMapDestinations.TryProjectAll(out var destinations,
                out WorldMapDestinationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(WorldMapDestinationFailure.None));
            Assert.That(destinations.Count, Is.EqualTo(79));
            int[] ids = destinations.Select(value => value.AreaId.Value).ToArray();
            Assert.That(ids, Is.Ordered);
            Assert.That(ids.Contains(0), Is.False);
            Assert.That(ids.Contains(36), Is.False);
            Assert.That(ids.Contains(46), Is.False);

            WorldMapDestination destination = destinations.Single(value => value.AreaId == KnaTha);
            Assert.That(destination.DisplayName, Is.EqualTo("K'na Tha"));
            Assert.That(destination.WorldTile, Is.EqualTo(new WorldMapTile(91902, 39305)));
            Assert.That(destination.LabelXOffset, Is.Zero);
            Assert.That(destination.LabelYOffset, Is.Zero);
            Assert.That(destination.DiscoveryRadiusTiles, Is.EqualTo(-1));
            Assert.That(destination.IsKnown, Is.False);
            Assert.That(destination.IsSelectable, Is.False);
        }

        [Test]
        public void UnknownDestinationsRemainQueryableButHiddenFromSourceStyleView()
        {
            Assert.That(_session.WorldMapDestinations.TryGetDestination(KnaTha,
                out WorldMapDestination knaTha, out _), Is.True);
            Assert.That(knaTha.IsKnown, Is.False);
            Assert.That(_session.WorldMapDestinations.TryGetDestination(Tarant,
                out WorldMapDestination tarant, out _), Is.True);
            Assert.That(tarant.DisplayName, Is.EqualTo("Tarant"));
            Assert.That(tarant.WorldTile, Is.EqualTo(new WorldMapTile(62243, 65664)));
            Assert.That(tarant.IsSelectable, Is.False);
            Assert.That(_session.WorldMapDestinations.TryProjectVisible(out var visible, out _), Is.True);
            Assert.That(visible, Is.Empty);
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
        }

        [Test]
        public void DiscoveryImmediatelyRecomputesVisibleSelectableProjectionWithoutDuplicates()
        {
            _session.Campaign.DiscoverArea(KnaTha);
            _session.Campaign.DiscoverArea(KnaTha);
            Assert.That(_session.WorldMapDestinations.TryProjectVisible(out var visible, out _), Is.True);
            Assert.That(visible.Count, Is.EqualTo(1));
            Assert.That(visible[0].AreaId, Is.EqualTo(KnaTha));
            Assert.That(visible[0].IsKnown, Is.True);
            Assert.That(visible[0].IsSelectable, Is.True);
        }

        [Test]
        public void SelectingKnownAreaProducesTypedIntentOnly()
        {
            _session.Campaign.DiscoverArea(KnaTha);
            Vector2 beforePosition = _pc.TilePosition;
            string beforeSector = _session.SelectedSector;
            int beforeKnown = _session.Campaign.KnownAreas.Count;

            WorldMapSelectionResult result = _session.WorldMapDestinations.TrySelectWorldArea(KnaTha);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Request.AreaId, Is.EqualTo(KnaTha));
            Assert.That(result.Request.Destination, Is.EqualTo(new WorldMapTile(91902, 39305)));
            Assert.That(_pc.TilePosition, Is.EqualTo(beforePosition));
            Assert.That(_session.SelectedSector, Is.EqualTo(beforeSector));
            Assert.That(_session.Campaign.KnownAreas.Count, Is.EqualTo(beforeKnown));
            Assert.That(_session.IsMapTransitionActive, Is.False);
        }

        [Test]
        public void SelectingUnknownAreaIsUnavailableAndDoesNotDiscoverOrTravel()
        {
            Vector2 before = _pc.TilePosition;
            WorldMapSelectionResult result = _session.WorldMapDestinations.TrySelectWorldArea(Tarant);
            Assert.That(result.Failure, Is.EqualTo(WorldMapDestinationFailure.Unavailable));
            Assert.That(result.Destination.DisplayName, Is.EqualTo("Tarant"));
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
            Assert.That(_pc.TilePosition, Is.EqualTo(before));
            Assert.That(_session.SelectedSector, Is.EqualTo(Sector));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(999)]
        public void InvalidAreaSelectionFailsExplicitly(int value)
        {
            WorldMapSelectionResult result = _session.WorldMapDestinations.TrySelectWorldArea(new AreaId(value));
            Assert.That(result.Failure, Is.EqualTo(WorldMapDestinationFailure.InvalidArea));
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);
        }

        [TestCase(36)]
        [TestCase(46)]
        public void RetailUnknownAliasesAreInvalidDestinations(int value)
        {
            var id = new AreaId(value);
            Assert.That(_session.WorldMapDestinations.TryGetDestination(id, out _,
                out WorldMapDestinationFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldMapDestinationFailure.InvalidDestinationRecord));
            Assert.That(_session.WorldMapDestinations.TrySelectWorldArea(id).Failure,
                Is.EqualTo(WorldMapDestinationFailure.InvalidDestinationRecord));
        }

        [Test]
        public void SourceLessProjectionFailsExplicitly()
        {
            var projection = new WorldMapDestinationProjection(null, new CampaignStateService());
            Assert.That(projection.TryProjectAll(out var destinations,
                out WorldMapDestinationFailure projectionFailure), Is.False);
            Assert.That(destinations, Is.Empty);
            Assert.That(projectionFailure, Is.EqualTo(WorldMapDestinationFailure.AreaSourceUnavailable));
            Assert.That(projection.TrySelectWorldArea(KnaTha).Failure,
                Is.EqualTo(WorldMapDestinationFailure.AreaSourceUnavailable));
        }

        [Test]
        public void DuplicateSourceIdentityFailsCatalogAndSelectionExplicitly()
        {
            var duplicate = new AreaList(new List<Area>
            {
                new(1, 10, 20, 0, 0, "First", "First", 320),
                new(1, 30, 40, 0, 0, "Second", "Second", 320),
            });
            var campaign = new CampaignStateService();
            campaign.BindAreaSource(duplicate);
            var projection = new WorldMapDestinationProjection(duplicate, campaign);
            Assert.That(projection.TryProjectAll(out _, out WorldMapDestinationFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(WorldMapDestinationFailure.DuplicateAreaIdentity));
            Assert.That(projection.TrySelectWorldArea(new AreaId(1)).Failure,
                Is.EqualTo(WorldMapDestinationFailure.DuplicateAreaIdentity));
        }

        [Test]
        public void RepeatedSelectionIsDeterministicAndSideEffectFree()
        {
            _session.Campaign.DiscoverArea(KnaTha);
            WorldMapSelectionResult first = _session.WorldMapDestinations.TrySelectWorldArea(KnaTha);
            WorldMapSelectionResult second = _session.WorldMapDestinations.TrySelectWorldArea(KnaTha);
            Assert.That(first.Succeeded && second.Succeeded, Is.True);
            Assert.That(second.Request, Is.EqualTo(first.Request));
            Assert.That(_session.Campaign.KnownAreas, Is.EqualTo(new[] { KnaTha }));
        }

        [Test]
        public void SaveRestoreRebindsProjectionToReplacementCampaignState()
        {
            WorldMapDestinationProjection before = _session.WorldMapDestinations;
            _session.Campaign.DiscoverArea(KnaTha);
            string json = _session.SaveGames.SerializeCurrentSession();
            _session.ResetAuthoritativeSession();
            Assert.That(_session.WorldMapDestinations.TrySelectWorldArea(KnaTha).Failure,
                Is.EqualTo(WorldMapDestinationFailure.Unavailable));
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.WorldMapDestinations, Is.Not.SameAs(before));
            Assert.That(_session.WorldMapDestinations.TrySelectWorldArea(KnaTha).Succeeded, Is.True);
            Assert.That(_session.WorldMapDestinations.TrySelectWorldArea(Tarant).Failure,
                Is.EqualTo(WorldMapDestinationFailure.Unavailable));
        }

        [Test]
        public void PresenterReadsProjectionAndEmitsExactlyOneIntentWithoutMutatingCampaign()
        {
            var presenter = _root.AddComponent<ProductionWorldMapDestinationPresenter>();
            presenter.Open();
            Assert.That(presenter.HasSelection, Is.False);
            Assert.That(presenter.TryGetVisibleDestinations(out var before, out _), Is.True);
            Assert.That(before, Is.Empty);
            Assert.That(_session.Campaign.KnownAreas, Is.Empty);

            _session.Campaign.DiscoverArea(KnaTha);
            int requests = 0;
            WorldMapTravelRequest request = default;
            presenter.TravelRequested += value => { requests++; request = value; };
            Assert.That(presenter.TryGetVisibleDestinations(out var after, out _), Is.True);
            Assert.That(after.Select(value => value.AreaId), Is.EqualTo(new[] { KnaTha }));
            Assert.That(presenter.Select(KnaTha).Succeeded, Is.True);
            Assert.That(presenter.HasSelection, Is.True);
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(request.AreaId, Is.EqualTo(KnaTha));
            Assert.That(_session.Campaign.KnownAreas, Is.EqualTo(new[] { KnaTha }));
            Assert.That(_root.GetComponents<ProductionWorldMapDestinationPresenter>().Length, Is.EqualTo(1));
        }

        [Test]
        public void ProductionLoaderCreatesOneDestinationPresenterIdempotently()
        {
            var other = new GameObject("M7D loader");
            try
            {
                WorldObjectSectorLoader loader = other.AddComponent<WorldObjectSectorLoader>();
                var ensure = typeof(WorldObjectSectorLoader).GetMethod("EnsureProductionPresentationComponents",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(ensure, Is.Not.Null);
                ensure.Invoke(loader, null);
                ensure.Invoke(loader, null);
                Assert.That(other.GetComponents<ProductionWorldMapDestinationPresenter>().Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(other); }
        }

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
