using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M7ALocalTransition")]
    public sealed class M7ALocalMapTransitionTests
    {
        private const string SourceSector = "maps/source/0.sec";
        private const string DestinationSector = "maps/destination/67108865.sec";
        private static readonly Vector2Int SourceTile = new(10, 10);
        private static readonly Vector2Int DestinationTile = new(70, 90);

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private TrackingOwner _terrain;
        private SessionOwner _objects;
        private Dictionary<string, byte[]> _files;
        private PersistentPlayerState _player;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M7ALocalMapTransitionTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _terrain = new TrackingOwner();
            _objects = new SessionOwner(_session);
            _session.RegisterTerrainOwner(_terrain);
            _session.RegisterObjectOwner(_objects);
            _files = Files(BuildJmp((0, Loc(10, 10), 2, Loc(70, 90))));
            _session.BindMapTransitionSource(Resolver(_files));
            Assert.That(_session.SelectSector(SourceSector), Is.True);
            _player = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                SourceSector, SourceTile, 0x28100000u);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void StableSourceIdentityIncludesMapAndExactGlobalTile()
        {
            var source = new MapTransitionSourceId(12, new Vector2Int(110, 92));
            Assert.That(source.ToString(), Is.EqualTo("jump:12:110:92"));
            Assert.That(source, Is.EqualTo(new MapTransitionSourceId(12, new Vector2Int(110, 92))));
            Assert.That(source, Is.Not.EqualTo(new MapTransitionSourceId(12, new Vector2Int(109, 92))));
        }

        [Test]
        public void RealBatesGoldenResolvesPositiveMapIdAndExactDestination()
        {
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["maps/bates mansion lev 1/map.jmp"] = BuildJmp(
                    (0, Loc(110, 92), 1, Loc(61976, 65664)),
                    (0, Loc(109, 94), 1, Loc(61976, 65664))),
                ["maps/arcanum1-024-fixed/map.prp"] = Properties(131072, 131072),
                ["maps/arcanum1-024-fixed/68853695432.sec"] = Array.Empty<byte>(),
            };
            MapTransitionResolver resolver = new(MapList.Read(MesReader.Read(BatesMapList())),
                files.ContainsKey, path => files[path]);

            MapTransitionResult result = resolver.Resolve(
                new MapTransitionSourceId(12, new Vector2Int(110, 92)));

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(result.Destination.MapId, Is.EqualTo(1));
            Assert.That(result.Destination.MapPath, Is.EqualTo("maps/arcanum1-024-fixed"));
            Assert.That(result.Destination.GlobalTile, Is.EqualTo(new Vector2Int(61976, 65664)));
            Assert.That(result.Destination.Sector.Path,
                Is.EqualTo("maps/arcanum1-024-fixed/68853695432.sec"));
            Assert.That(result.Destination.Facing, Is.Null);
        }

        [Test]
        public void MalformedJumpTableFailsExplicitly()
        {
            _files["maps/source/map.jmp"] = new byte[] { 0xff, 0xff, 0xff, 0x7f };
            MapTransitionResult result = Resolver(_files).Resolve(new MapTransitionSourceId(1, SourceTile));
            Assert.That(result.Failure, Is.EqualTo(MapTransitionFailure.JumpTableMalformed));
        }

        [Test]
        public void MissingJumpTileFailsExplicitly()
        {
            MapTransitionResult result = Resolver(_files).Resolve(
                new MapTransitionSourceId(1, new Vector2Int(11, 10)));
            Assert.That(result.Failure, Is.EqualTo(MapTransitionFailure.JumpPointMissing));
        }

        [Test]
        public void NonPositiveDestinationSentinelIsNotGuessed()
        {
            _files["maps/source/map.jmp"] = BuildJmp((0, Loc(10, 10), -1, Loc(20, 20)));
            MapTransitionResult result = Resolver(_files).Resolve(new MapTransitionSourceId(1, SourceTile));
            Assert.That(result.Failure, Is.EqualTo(MapTransitionFailure.UnsupportedDestinationMap));
        }

        [Test]
        public void DestinationOutsideMapBoundsFailsBeforePresentation()
        {
            _files["maps/source/map.jmp"] = BuildJmp((0, Loc(10, 10), 2, Loc(200, 90)));
            MapTransitionResult result = Resolver(_files).Resolve(new MapTransitionSourceId(1, SourceTile));
            Assert.That(result.Failure, Is.EqualTo(MapTransitionFailure.DestinationTileInvalid));
        }

        [Test]
        public void CrossMapRequestRelocatesSamePcAndDrivesBothOwners()
        {
            uint walkFacingSix = CritterArtResolver.WithAnimRotation(_player.ArtId, 1, 6);
            _player.ArtId = walkFacingSix;
            PersistentCharacterState character = _session.Characters.Get(_player.Identity);
            _session.Campaign.SetFlag(77, 1);

            MapTransitionResult result = _session.RequestMapTransition(_player.Identity,
                new MapTransitionSourceId(1, SourceTile));

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(_session.PlayerState, Is.SameAs(_player));
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(_session.Characters.Get(_player.Identity), Is.SameAs(character));
            Assert.That(_session.Campaign.GetFlag(77), Is.EqualTo(1));
            Assert.That(_session.CurrentMap, Is.EqualTo("maps/destination"));
            Assert.That(_session.SelectedSector, Is.EqualTo(DestinationSector));
            Assert.That(_player.MapPosition, Is.EqualTo((Vector2)DestinationTile));
            Assert.That(_player.TilePosition, Is.EqualTo(new Vector2(6, 26)));
            Assert.That(CritterArtResolver.RotationOf(_player.ArtId), Is.EqualTo(6));
            Assert.That((_player.ArtId >> 6) & 0x1f, Is.Zero);
            Assert.That(_terrain.PresentedSector, Is.EqualTo(DestinationSector));
            Assert.That(_objects.PresentedSector, Is.EqualTo(DestinationSector));
            Assert.That(_session.LoadedSectorCount, Is.EqualTo(1));
            Assert.That(_session.IsMapTransitionActive, Is.False);
        }

        [Test]
        public void WrongActorCannotChangeLiveMap()
        {
            MapTransitionResult result = _session.RequestMapTransition(
                ArcanumObjectId.CreateAuthored(99), new MapTransitionSourceId(1, SourceTile));
            AssertUnchanged(result, MapTransitionFailure.InvalidActor);
        }

        [Test]
        public void WrongSourceTileCannotChangeLiveMap()
        {
            MapTransitionResult result = _session.RequestMapTransition(_player.Identity,
                new MapTransitionSourceId(1, new Vector2Int(11, 10)));
            AssertUnchanged(result, MapTransitionFailure.SourceTileMismatch);
        }

        [Test]
        public void MissingDestinationSectorFailsPreflightWithoutTeardown()
        {
            _files.Remove(DestinationSector);
            _session.BindMapTransitionSource(Resolver(_files));
            int terrainClears = _terrain.ClearCalls;
            int objectClears = _objects.ClearCalls;

            MapTransitionResult result = _session.RequestMapTransition(_player.Identity,
                new MapTransitionSourceId(1, SourceTile));

            AssertUnchanged(result, MapTransitionFailure.DestinationSectorMissing);
            Assert.That(_terrain.ClearCalls, Is.EqualTo(terrainClears));
            Assert.That(_objects.ClearCalls, Is.EqualTo(objectClears));
        }

        [Test]
        public void DestinationPresentationFailureRollsBackSamePcToSource()
        {
            _objects.RejectedSector = DestinationSector;
            MapTransitionResult result = _session.RequestMapTransition(_player.Identity,
                new MapTransitionSourceId(1, SourceTile));

            Assert.That(result.Failure, Is.EqualTo(MapTransitionFailure.PresentationFailed), result.Detail);
            Assert.That(_session.PlayerState, Is.SameAs(_player));
            Assert.That(_session.CurrentMap, Is.EqualTo("maps/source"));
            Assert.That(_session.SelectedSector, Is.EqualTo(SourceSector));
            Assert.That(_player.MapPosition, Is.EqualTo((Vector2)SourceTile));
            Assert.That(_terrain.PresentedSector, Is.EqualTo(SourceSector));
            Assert.That(_objects.PresentedSector, Is.EqualTo(SourceSector));
        }

        [Test]
        public void ReentrantTransitionIsRejectedAsBusy()
        {
            MapTransitionResult nested = default;
            _session.SectorUnloading += _ => nested = _session.RequestMapTransition(_player.Identity,
                new MapTransitionSourceId(1, SourceTile));

            MapTransitionResult result = _session.RequestMapTransition(_player.Identity,
                new MapTransitionSourceId(1, SourceTile));

            Assert.That(result.Succeeded, Is.True, result.Detail);
            Assert.That(nested.Failure, Is.EqualTo(MapTransitionFailure.Busy));
        }

        [Test]
        public void FractionalPcPositionDoesNotActivatePassiveJump()
        {
            _session.SetMovementState(_player.Identity, new Vector2(10.25f, 10), _player.ArtId, true);
            MapTransitionResult result = _session.RequestCurrentJumpPoint(_player.Identity);
            Assert.That(result.Failure, Is.EqualTo(MapTransitionFailure.SourceTileMismatch));
            Assert.That(_session.SelectedSector, Is.EqualTo(SourceSector));
        }

        [Test]
        public void PostTransitionV1SaveRestoresDestinationMapAndPcIdentity()
        {
            Assert.That(_session.RequestCurrentJumpPoint(_player.Identity).Succeeded, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"version\": 1"));

            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.CurrentMap, Is.EqualTo("maps/destination"));
            Assert.That(_session.SelectedSector, Is.EqualTo(DestinationSector));
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(_session.PlayerState.MapPosition, Is.EqualTo((Vector2)DestinationTile));
            Assert.That(_session.PlayerState.Destination, Is.Null);
        }

        private void AssertUnchanged(MapTransitionResult result, MapTransitionFailure failure)
        {
            Assert.That(result.Failure, Is.EqualTo(failure), result.Detail);
            Assert.That(_session.PlayerState, Is.SameAs(_player));
            Assert.That(_session.SelectedSector, Is.EqualTo(SourceSector));
            Assert.That(_session.CurrentMap, Is.EqualTo("maps/source"));
            Assert.That(_player.MapPosition, Is.EqualTo((Vector2)SourceTile));
        }

        [Test]
        public void LargeMovementTickObservesFirstJumpAndDiscardsResidualOldRouteMovement()
        {
            var follower = new TileRouteFollower();
            follower.Replace(new Vector2(40, 28), new[] {
                new Vector2Int(41, 28), new Vector2Int(42, 28), new Vector2Int(43, 28),
                new Vector2Int(44, 28), new Vector2Int(45, 28), new Vector2Int(46, 28) });
            var entered = new List<Vector2Int>();
            follower.Advance(100f, (tile, facing) => {
                entered.Add(tile);
                if (tile != new Vector2Int(45, 28)) return false;
                Assert.That(facing, Is.EqualTo(IsoProjection.DirFromDelta(1, 0)));
                follower.Cancel(tile); // The production lifecycle unbind cancels the same route.
                return true;
            });
            Assert.That(entered.Count, Is.EqualTo(5));
            Assert.That(entered.Contains(new Vector2Int(46, 28)), Is.False);
            Assert.That(follower.Position, Is.EqualTo(new Vector2(45, 28)));
            Assert.That(follower.IsMoving, Is.False);
        }

        [Test]
        public void OrdinaryRouteRetainsFractionalMovementAndReportsEveryEnteredTile()
        {
            var follower = new TileRouteFollower();
            follower.Replace(Vector2.zero, new[] { new Vector2Int(1, 0), new Vector2Int(2, 1), new Vector2Int(3, 1) });
            var entered = new List<Vector2Int>();
            Assert.That(follower.Advance(2.25f, (tile, _) => { entered.Add(tile); return false; }), Is.True);
            Assert.That(entered, Is.EqualTo(new[] { new Vector2Int(1, 0), new Vector2Int(2, 1) }));
            Assert.That(follower.Position, Is.EqualTo(new Vector2(2.25f, 1)));
        }

        [TestCase("missing-table", MapTransitionFailure.JumpTableMissing)]
        [TestCase("missing-tile", MapTransitionFailure.JumpPointMissing)]
        [TestCase("unresolved-map", MapTransitionFailure.DestinationMapMissing)]
        [TestCase("negative-coordinate", MapTransitionFailure.DestinationTileInvalid)]
        [TestCase("missing-properties", MapTransitionFailure.DestinationPropertiesMissing)]
        [TestCase("malformed-properties", MapTransitionFailure.DestinationPropertiesMalformed)]
        [TestCase("sentinel", MapTransitionFailure.UnsupportedDestinationMap)]
        [TestCase("conflicting-records", MapTransitionFailure.AmbiguousJumpPoint)]
        public void FailedPreflightPreservesCompleteSessionAndPresentation(string scenario, MapTransitionFailure expected)
        {
            switch (scenario)
            {
                case "missing-table": _files.Remove("maps/source/map.jmp"); break;
                case "missing-tile": _files["maps/source/map.jmp"] = BuildJmp(); break;
                case "unresolved-map": _files["maps/source/map.jmp"] = BuildJmp((0, Loc(10, 10), 99, Loc(70, 90))); break;
                case "negative-coordinate": _files["maps/source/map.jmp"] = BuildJmp((0, Loc(10, 10), 2, Loc(-1, 90))); break;
                case "missing-properties": _files.Remove("maps/destination/map.prp"); break;
                case "malformed-properties": _files["maps/destination/map.prp"] = new byte[1]; break;
                case "sentinel": _files["maps/source/map.jmp"] = BuildJmp((0, Loc(10, 10), 0, Loc(70, 90))); break;
                case "conflicting-records": _files["maps/source/map.jmp"] = BuildJmp(
                    (0, Loc(10, 10), 2, Loc(70, 90)), (0, Loc(10, 10), 2, Loc(71, 90))); break;
            }
            _session.BindMapTransitionSource(Resolver(_files));
            _session.Campaign.SetVar(10, 321);
            string before = _session.SaveGames.SerializeCurrentSession();
            int terrainClears = _terrain.ClearCalls, objectClears = _objects.ClearCalls;
            var result = _session.RequestMapTransition(_player.Identity, new MapTransitionSourceId(1, SourceTile));
            AssertUnchanged(result, expected);
            Assert.That(_session.SaveGames.SerializeCurrentSession(), Is.EqualTo(before));
            Assert.That(_terrain.ClearCalls, Is.EqualTo(terrainClears));
            Assert.That(_objects.ClearCalls, Is.EqualTo(objectClears));
            Assert.That(_terrain.PresentedSector, Is.EqualTo(SourceSector));
            Assert.That(_objects.PresentedSector, Is.EqualTo(SourceSector));
        }

        private static MapTransitionResolver Resolver(Dictionary<string, byte[]> files)
            => new(MapList.Read(MesReader.Read(
                    "{5000}{Source, 0, 0}\n" +
                    "{5001}{Destination, 0, 0}\n")),
                files.ContainsKey, path => files[path]);

        private static Dictionary<string, byte[]> Files(byte[] jump)
            => new(StringComparer.Ordinal)
            {
                ["maps/source/map.jmp"] = jump,
                ["maps/source/map.prp"] = Properties(128, 128),
                ["maps/destination/map.prp"] = Properties(128, 128),
                [DestinationSector] = Array.Empty<byte>(),
            };

        private static string BatesMapList()
        {
            var text = new StringBuilder();
            text.AppendLine("{5000}{Arcanum1-024-fixed, 92958, 82592}");
            for (int key = 5001; key < 5011; key++)
                text.AppendLine($"{{{key}}}{{Unused-{key}, 0, 0}}");
            text.AppendLine("{5011}{Bates Mansion Lev 1, 104, 92}");
            return text.ToString();
        }

        private static long Loc(int x, int y) => (x & 0xffffffffL) | ((long)y << 32);

        private static byte[] BuildJmp(params (uint flags, long src, int dstMap, long dst)[] points)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(points.Length);
            foreach (var point in points)
            {
                writer.Write(point.flags);
                writer.Write(0);
                writer.Write(point.src);
                writer.Write(point.dstMap);
                writer.Write(0);
                writer.Write(point.dst);
            }
            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] Properties(long width, long height)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(0);
            writer.Write(0);
            writer.Write(width);
            writer.Write(height);
            writer.Flush();
            return stream.ToArray();
        }

        private sealed class TrackingOwner : ISectorPresentationOwner
        {
            public int ClearCalls;
            public string ConfiguredSector => SourceSector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                return true;
            }
            public void ClearPresentedSector()
            {
                ClearCalls++;
                PresentedSector = null;
            }
        }

        private sealed class SessionOwner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public string RejectedSector;
            public int ClearCalls;
            public string ConfiguredSector => SourceSector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;

            public SessionOwner(WorldMapSessionCoordinator session) => _session = session;

            public bool PresentSector(string sectorPath)
            {
                string normalized = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                if (normalized == RejectedSector) return false;
                PresentedSector = normalized;
                _session.BeginSector(normalized);
                return true;
            }

            public void ClearPresentedSector()
            {
                ClearCalls++;
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
