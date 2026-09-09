using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M1BNavigation")]
    public sealed class M1BCrossSectorNavigationTests
    {
        private const string MapPath = "maps/arcanum1-024-fixed";
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("M1BCrossSectorNavigationTests");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void SourceSectorIdentityAndGlobalLocalCoordinatesRoundTrip()
        {
            Assert.That(SectorCoordinate.TryParse(" MAPS\\Arcanum1-024-fixed\\101602821844.SEC ", out var sector), Is.True);
            Assert.That((sector.X, sector.Y), Is.EqualTo((1748, 1514)));
            Assert.That(sector.Path, Is.EqualTo("maps/arcanum1-024-fixed/101602821844.sec"));
            Vector2 local = new(63.5f, 12.25f);
            Vector2 global = sector.ToGlobal(local);
            Assert.That(global, Is.EqualTo(new Vector2(111935.5f, 96908.25f)));
            Assert.That(sector.ToLocal(global), Is.EqualTo(local));
            Assert.That(SectorCoordinate.FromGlobal(sector.MapPath, global), Is.EqualTo(sector));
        }

        [Test]
        public void NeighborUsesSourcePackedFilename()
        {
            var sector = new SectorCoordinate(MapPath, 1748, 1514);
            Assert.That(sector.Neighbor(1, 0).Path,
                Is.EqualTo("maps/arcanum1-024-fixed/101602821845.sec"));
            Assert.That(sector.Neighbor(0, 1).Path,
                Is.EqualTo("maps/arcanum1-024-fixed/101669930708.sec"));
        }

        [Test]
        public void BoundaryChoiceIsLegalAndDeterministic()
        {
            var sector = new SectorCoordinate(MapPath, 10, 20);
            Vector2Int final = Vector2Int.RoundToInt(sector.Neighbor(1, 0).ToGlobal(new Vector2Int(5, 10)));
            var planner = new CrossSectorBoundaryPlanner();
            Assert.That(planner.TryPlan(sector, Map(), new Vector2Int(60, 10), final, _ => true, out var first), Is.True);
            Assert.That(planner.TryPlan(sector, Map(), new Vector2Int(60, 10), final, _ => true, out var second), Is.True);
            Assert.That(first.TargetSector, Is.EqualTo(sector.Neighbor(1, 0)));
            Assert.That(first.ExitTile, Is.EqualTo(new Vector2Int(63, 10)));
            Assert.That(first.EntryTile, Is.EqualTo(new Vector2Int(0, 10)));
            Assert.That(first.Rotation, Is.EqualTo(5));
            Assert.That(second.ExitTile, Is.EqualTo(first.ExitTile));
            Assert.That(second.Route, Is.EqualTo(first.Route));
        }

        [Test]
        public void BlockedCurrentBoundaryIsRejected()
        {
            var blocked = new List<Vector2Int>();
            for (int y = 0; y < SectorCoordinate.Size; y++) blocked.Add(new Vector2Int(63, y));
            var sector = new SectorCoordinate(MapPath, 10, 20);
            Vector2Int final = Vector2Int.RoundToInt(sector.Neighbor(1, 0).ToGlobal(new Vector2Int(5, 10)));
            Assert.That(new CrossSectorBoundaryPlanner().TryPlan(
                sector, Map(blocked.ToArray()), new Vector2Int(60, 10), final, _ => true, out _), Is.False);
        }

        [Test]
        public void RejectedPreferredBoundaryFallsBackDeterministically()
        {
            var sector = new SectorCoordinate(MapPath, 10, 20);
            Vector2Int final = Vector2Int.RoundToInt(sector.Neighbor(1, 0).ToGlobal(new Vector2Int(5, 10)));
            var planner = new CrossSectorBoundaryPlanner();
            Assert.That(planner.TryPlan(sector, Map(), new Vector2Int(60, 10), final, _ => true,
                out var plan, candidate => candidate.ExitTile != new Vector2Int(63, 10)), Is.True);
            Assert.That(plan.TargetSector, Is.EqualTo(sector.Neighbor(1, 0)));
            Assert.That(plan.ExitTile, Is.Not.EqualTo(new Vector2Int(63, 10)));
            Assert.That(plan.EntryTile, Is.EqualTo(new Vector2Int(0, plan.ExitTile.y)));
        }

        [Test]
        public void TargetEntryBlockPreventsBoundaryCrossing()
        {
            SectorNavigationMap current = Map();
            SectorNavigationMap adjacent = Map(new Vector2Int(0, 10));
            Assert.That(current.CanCrossBoundaryTo(adjacent, new Vector2Int(63, 10), 5), Is.False);
            Assert.That(current.CanCrossBoundaryTo(Map(), new Vector2Int(63, 10), 5), Is.True);
        }

        [Test]
        public void PlayerGlobalStateRetainsIdentityDestinationAndCorrectEntry()
        {
            var session = _root.AddComponent<WorldMapSessionCoordinator>();
            var a = new SectorCoordinate(MapPath, 1748, 1514);
            var b = a.Neighbor(1, 0);
            PersistentPlayerState player = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                a.Path, new Vector2(63, 10), 0x28100000u);
            session.SetPlayerDestination(Vector2Int.RoundToInt(b.ToGlobal(new Vector2Int(8, 10))));
            player.Relocate(b.Path, new Vector2(0, 10), player.ArtId);

            Assert.That(player.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(player.Sector, Is.EqualTo(b.Path));
            Assert.That(player.TilePosition, Is.EqualTo(new Vector2(0, 10)));
            Assert.That(player.MapPosition, Is.EqualTo(b.ToGlobal(new Vector2(0, 10))));
            Assert.That(player.Destination, Is.EqualTo(Vector2Int.RoundToInt(b.ToGlobal(new Vector2Int(8, 10)))));
        }

        [Test]
        public void CoordinatorTransitionPreservesOnePlayerStateAndDestination()
        {
            var session = _root.AddComponent<WorldMapSessionCoordinator>();
            var terrain = new FakeOwner();
            var objects = new FakeOwner();
            session.RegisterTerrainOwner(terrain);
            session.RegisterObjectOwner(objects);
            var a = new SectorCoordinate(MapPath, 1748, 1514);
            var b = a.Neighbor(1, 0);
            Assert.That(session.SelectSector(a.Path), Is.True);
            PersistentPlayerState player = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                a.Path, new Vector2(63, 10), 0x28100000u);
            Vector2Int final = Vector2Int.RoundToInt(b.ToGlobal(new Vector2Int(8, 10)));
            session.SetPlayerDestination(final);

            Assert.That(session.TryTransitionPlayer(b.Path, new Vector2(0, 10), player.ArtId), Is.True);
            Assert.That(session.PlayerState, Is.SameAs(player));
            Assert.That(player.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(player.Sector, Is.EqualTo(b.Path));
            Assert.That(player.TilePosition, Is.EqualTo(new Vector2(0, 10)));
            Assert.That(player.Destination, Is.EqualTo(final));
            Assert.That(terrain.PresentedSector, Is.EqualTo(b.Path));
            Assert.That(objects.PresentedSector, Is.EqualTo(b.Path));
        }

        [Test]
        public void TransitionEntryIsHeldForExactlyOneMovementUpdate()
        {
            var session = _root.AddComponent<WorldMapSessionCoordinator>();
            var a = new SectorCoordinate(MapPath, 1748, 1514);
            var b = a.Neighbor(1, 0);
            PersistentPlayerState player = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                a.Path, new Vector2(63, 10), 0x28100000u);
            Vector2Int final = Vector2Int.RoundToInt(b.ToGlobal(new Vector2Int(8, 10)));
            session.SetPlayerDestination(final);
            player.Relocate(b.Path, new Vector2(0, 10), player.ArtId);

            var hold = new SectorEntryFrameHold();
            var follower = new TileRouteFollower();
            follower.Replace(player.TilePosition, new[] { new Vector2Int(1, 10), new Vector2Int(2, 10) });
            hold.Arm();

            if (!hold.Consume()) follower.Advance(1f);
            Assert.That(follower.Position, Is.EqualTo(new Vector2(0, 10)));
            Assert.That(player.MapPosition, Is.EqualTo(b.ToGlobal(new Vector2(0, 10))));
            Assert.That(player.Destination, Is.EqualTo(final));

            if (!hold.Consume()) follower.Advance(0.5f);
            Assert.That(follower.Position, Is.EqualTo(new Vector2(0.5f, 10)));
            Assert.That(player.Destination, Is.EqualTo(final));
            Assert.That(hold.Pending, Is.False);
        }

        [Test]
        public void FailedTransitionRestoresPriorSectorAndStopsAtValidPosition()
        {
            var session = _root.AddComponent<WorldMapSessionCoordinator>();
            var a = new SectorCoordinate(MapPath, 1748, 1514);
            var b = a.Neighbor(1, 0);
            var terrain = new FakeOwner { RejectedSector = b.Path };
            var objects = new FakeOwner();
            session.RegisterTerrainOwner(terrain);
            session.RegisterObjectOwner(objects);
            Assert.That(session.SelectSector(a.Path), Is.True);
            PersistentPlayerState player = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                a.Path, new Vector2(63, 10), 0x28100000u);

            Assert.That(session.TryTransitionPlayer(b.Path, new Vector2(0, 10), player.ArtId), Is.False);
            Assert.That(session.SelectedSector, Is.EqualTo(a.Path));
            Assert.That(player.Sector, Is.EqualTo(a.Path));
            Assert.That(player.TilePosition, Is.EqualTo(new Vector2(63, 10)));
            Assert.That((player.ArtId >> 6) & 0x1F, Is.Zero);
        }

        [Test]
        public void DestinationSurvivesPresentationReplacementAndClearsOnArrival()
        {
            var session = _root.AddComponent<WorldMapSessionCoordinator>();
            var sector = new SectorCoordinate(MapPath, 1748, 1514);
            PersistentPlayerState player = session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                sector.Path, new Vector2(4, 5), 0x28100000u);
            Vector2Int final = Vector2Int.RoundToInt(sector.ToGlobal(new Vector2Int(9, 5)));
            session.SetPlayerDestination(final);
            WorldObject first = Runtime();
            player.Restore(first);
            player.Capture(first);
            Object.DestroyImmediate(first.gameObject);
            WorldObject rebuilt = Runtime();
            player.Restore(rebuilt);

            Assert.That(player.Destination, Is.EqualTo(final));
            Assert.That(rebuilt.Identity, Is.EqualTo(player.Identity));
            session.ClearPlayerDestination();
            Assert.That(player.Destination, Is.Null);
        }

        private WorldObject Runtime()
        {
            var go = new GameObject("PlayerProjection");
            go.transform.SetParent(_root.transform);
            return go.AddComponent<WorldObject>();
        }

        private static SectorNavigationMap Map(params Vector2Int[] blocked)
        {
            var mask = new bool[SectorTerrain.TileCount];
            foreach (Vector2Int tile in blocked) mask[tile.y * SectorCoordinate.Size + tile.x] = true;
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]), mask, names);
        }

        private sealed class FakeOwner : ISectorPresentationOwner
        {
            public string RejectedSector;
            public string ConfiguredSector => null;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                if (sectorPath == RejectedSector) return false;
                PresentedSector = sectorPath;
                return true;
            }
            public void ClearPresentedSector() => PresentedSector = null;
        }
    }
}
