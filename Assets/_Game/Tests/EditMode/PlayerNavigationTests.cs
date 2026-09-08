using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("PlayerNavigation")]
    public sealed class PlayerNavigationTests
    {
        private static SectorNavigationMap Map(params Vector2Int[] blocked)
        {
            var art = new uint[SectorTerrain.TileCount];
            var mask = new bool[SectorTerrain.TileCount];
            foreach (Vector2Int tile in blocked) mask[tile.y * 64 + tile.x] = true;
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(art), mask, names);
        }

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(17, 42)] [TestCase(63, 63)]
        public void TileWorldConversionRoundTrips(int x, int y)
        {
            Vector3 world = IsoProjection.TileToWorld(x, y, 100f);
            Assert.That(IsoProjection.WorldToTile(world, 100f), Is.EqualTo(new Vector2Int(x, y)));
        }

        [Test] public void ExplicitAndTerrainBlocksRejectDestinations()
        {
            SectorNavigationMap map = Map(new Vector2Int(4, 5));
            Assert.That(map.IsWalkable(new Vector2Int(4, 5)), Is.False);
            Assert.That(map.IsWalkable(new Vector2Int(-1, 5)), Is.False);
            Assert.That(map.IsWalkable(new Vector2Int(4, 6)), Is.True);
        }

        [Test] public void WallBlocksOnlyItsAuthoredCrossingEdge()
        {
            SectorNavigationMap map = Map();
            var go = new GameObject("Wall");
            var wall = go.AddComponent<WorldObject>();
            wall.Type = Formats.Objects.ObjectType.Wall;
            wall.Tile = new Vector2Int(2, 2);
            wall.ArtId = ((uint)ArtId.TypeWall << 28) | (1u << 11); // solid piece, rotation 1
            map.Register(wall, 0);
            Assert.That(map.CanTraverse(wall.Tile, 1), Is.False);
            Assert.That(map.CanTraverse(wall.Tile, 3), Is.True);
            Object.DestroyImmediate(go);
        }

        [Test] public void FindsRouteAroundObstacle()
        {
            SectorNavigationMap map = Map(new Vector2Int(2, 1));
            var route = new List<Vector2Int>();
            Assert.That(new DeterministicTilePathfinder().TryFindPath(
                map, new Vector2Int(1, 1), new Vector2Int(3, 1), route), Is.True);
            Assert.That(route, Has.No.Member(new Vector2Int(2, 1)));
            Assert.That(route[route.Count - 1], Is.EqualTo(new Vector2Int(3, 1)));
        }

        [Test] public void UnreachableDestinationFailsWithoutPartialRoute()
        {
            var blocked = new List<Vector2Int>();
            Vector2Int destination = new Vector2Int(10, 10);
            foreach (Vector2Int delta in IsoProjection.DirDelta) blocked.Add(destination + delta);
            var route = new List<Vector2Int> { new Vector2Int(99, 99) };
            Assert.That(new DeterministicTilePathfinder().TryFindPath(Map(blocked.ToArray()), new Vector2Int(1, 1), destination, route), Is.False);
            Assert.That(route, Is.Empty);
        }

        [Test] public void SameInputsProduceSamePath()
        {
            SectorNavigationMap map = Map(new Vector2Int(2, 1), new Vector2Int(2, 2));
            var a = new List<Vector2Int>();
            var b = new List<Vector2Int>();
            var finder = new DeterministicTilePathfinder();
            Assert.That(finder.TryFindPath(map, new Vector2Int(1, 1), new Vector2Int(4, 2), a), Is.True);
            Assert.That(finder.TryFindPath(map, new Vector2Int(1, 1), new Vector2Int(4, 2), b), Is.True);
            Assert.That(b, Is.EqualTo(a));
        }

        [TestCase(-1, -1, 0)] [TestCase(-1, 0, 1)] [TestCase(-1, 1, 2)] [TestCase(0, 1, 3)]
        [TestCase(1, 1, 4)] [TestCase(1, 0, 5)] [TestCase(1, -1, 6)] [TestCase(0, -1, 7)]
        public void FacingUsesAllEngineDirections(int dx, int dy, int facing)
            => Assert.That(IsoProjection.DirFromDelta(dx, dy), Is.EqualTo(facing));

        [Test] public void RouteTransitionsMovingToIdle()
        {
            var follower = new TileRouteFollower();
            follower.Replace(Vector2.zero, new[] { Vector2Int.right, new Vector2Int(2, 0) });
            Assert.That(follower.IsMoving, Is.True);
            Assert.That(follower.Advance(1f), Is.True);
            Assert.That(follower.Position, Is.EqualTo((Vector2)Vector2Int.right));
            Assert.That(follower.Advance(1f), Is.False);
            Assert.That(follower.Position, Is.EqualTo(new Vector2(2, 0)));
        }

        [Test] public void ReplacingRouteCancelsOldDestination()
        {
            var follower = new TileRouteFollower();
            follower.Replace(Vector2.zero, new[] { new Vector2Int(5, 0) });
            follower.Advance(.5f);
            follower.Replace(follower.Position, new[] { new Vector2Int(0, 1) });
            follower.Advance(10f);
            Assert.That(follower.Position, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(follower.IsMoving, Is.False);
        }

        [Test] public void WalkAndIdleArtKeepFacingAndClearRuntimeFrame()
        {
            uint stand = ((uint)ArtId.TypeCritter << 28) | (4u << 11) | (17u << 14);
            uint walk = CritterArtResolver.WithAnimRotation(stand, 1, 6) & ~(0x1Fu << 14);
            uint idle = CritterArtResolver.WithAnimRotation(walk, 0, 6);
            Assert.That((walk >> 6) & 0x1F, Is.EqualTo(1));
            Assert.That(CritterArtResolver.RotationOf(walk), Is.EqualTo(6));
            Assert.That((walk >> 14) & 0x1F, Is.Zero);
            Assert.That((idle >> 6) & 0x1F, Is.Zero);
            Assert.That(CritterArtResolver.RotationOf(idle), Is.EqualTo(6));
        }
    }
}
