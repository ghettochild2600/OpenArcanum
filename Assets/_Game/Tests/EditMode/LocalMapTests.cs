using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using OpenArcanum.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("LocalMap")]
    public sealed class LocalMapTests
    {
        private const string Sector = "maps/test/67108865.sec"; // source sector (1,1)
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private Dictionary<string, byte[]> _files;

        [SetUp]
        public void SetUp()
        {
            _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [Sector] = BuildSector(7),
                ["townmap/crash/crash.tmi"] = BuildTmi(7, 4000, 4000, 2, 2, 64, 64, .1f),
                ["townmap/crash/crash000000.bmp"] = BuildBmp(),
                ["worldmap/overview.bmp"] = BuildBmp(),
            };
            _root = new GameObject(nameof(LocalMapTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            MapList maps = MapList.Read(new MesFile(new List<KeyValuePair<int, string>>
            {
                new(MapList.FirstKey, "test, 0, 0, WorldMap: 0"),
            }));
            _session.BindLocalMapSource(new LocalMapSource(
                new MesFile(new List<KeyValuePair<int, string>> { new(7, "crash") }),
                maps,
                new MesFile(new List<KeyValuePair<int, string>>
                {
                    new(50, "8, 8, chunks, ZoomedName: overview, MapKeyedTo: 1"),
                }),
                path => _files.ContainsKey(Normalize(path)), path => _files[Normalize(path)]));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity, Sector, Vector2.zero,
                0x28100000u);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void SourceLayoutUsesWmapUiRectanglesAndArt()
        {
            Assert.That(RetailLocalMapLayout.Window, Is.EqualTo(new Rect(0, 41, 800, 400)));
            Assert.That(RetailLocalMapLayout.Canvas, Is.EqualTo(new Rect(150, 11, 501, 365)));
            Assert.That(RetailLocalMapLayout.ContinentCanvas, Is.EqualTo(new Rect(218, 11, 365, 365)));
            Assert.That(RetailLocalMapLayout.PcLens, Is.EqualTo(new Rect(25, 24, 89, 89)));
            Assert.That((RetailLocalMapLayout.BackgroundSourceId, RetailLocalMapLayout.PcLensFrameSourceId,
                    RetailLocalMapLayout.CrossMarkerSourceId), Is.EqualTo((138, 198, 204)));
            Assert.That((RetailLocalMapLayout.TownMapButton, RetailLocalMapLayout.WorldMapButton,
                    RetailLocalMapLayout.ScrollButton, RetailLocalMapLayout.TravelButton),
                Is.EqualTo((new Vector2(20, 165), new Vector2(19, 267),
                    new Vector2(375, 347), new Vector2(698, 312))));
        }

        [Test]
        public void TownMapInfoParsesSourceRecordAndProjectsIsometricLocation()
        {
            TownMapDefinition value = TownMapDefinition.Parse(7, "crash", false,
                BuildTmi(7, 4000, 4000, 2, 2, 64, 64, .1f));
            Assert.That((value.Id, value.MapId, value.Width, value.Height, value.Columns, value.Rows),
                Is.EqualTo((7, 7, 4000, 4000, 2, 2)));
            Assert.That(value.TryProject(new Vector2(64, 64), out Vector2 pixel), Is.True);
            Assert.That(pixel, Is.EqualTo(new Vector2(200, 200)));
            Assert.That(value.TileIndex(new Vector2(64, 64)), Is.EqualTo(3));
        }

        [Test]
        public void WaitableFlagIsRemovedFromAuthoredName()
        {
            LocalMapSource.ParseName("Stillwater   [W: 1]", out string name, out bool waitable);
            Assert.That(name, Is.EqualTo("Stillwater"));
            Assert.That(waitable, Is.True);
        }

        [Test]
        public void MissingSpecificTileFallsBackToSourceZeroTile()
        {
            var source = new LocalMapSource(
                new MesFile(new List<KeyValuePair<int, string>> { new(7, "crash") }),
                path => _files.ContainsKey(Normalize(path)), path => _files[Normalize(path)]);
            Assert.That(source.TryResolve(Sector, out TownMapDefinition definition), Is.True);
            Assert.That(source.ReadTile(definition, 3).Width, Is.EqualTo(1));
            Assert.That(LocalMapSource.TilePath(definition, 3),
                Is.EqualTo("townmap/crash/crash000003.bmp"));
        }

        [Test]
        public void ContinentSelectorUsesCurrentMapsAuthoredOverview()
        {
            Assert.That(_session.LocalMap.TryReadContinent(out WorldMapInfo info, out BmpImage image), Is.True);
            Assert.That((info.ZoomedName, info.MapKeyedTo, image.Width, image.Height),
                Is.EqualTo(("overview", 1, 1, 1)));
        }

        [Test]
        public void ProjectionMarksCurrentTileAndUsesLivePlayerMarker()
        {
            Assert.That(_session.LocalMap.TryProject(out LocalMapProjection projection), Is.True);
            Assert.That(projection.KnownTiles, Is.EquivalentTo(new[] { 3 }));
            LocalMapMarker marker = projection.Markers.Single();
            Assert.That((marker.Kind, marker.SourceArtId, marker.Pixel),
                Is.EqualTo((LocalMapMarkerKind.Player, 204, new Vector2(200, 200))));
        }

        [Test]
        public void FogMaskMatchesSourceAdjacentCornerRules()
        {
            var definition = new TownMapDefinition(1, "test", false, 1, 400, 400, 3, 3,
                MakeLocation(0, 0), 1f);
            var projection = new LocalMapProjection(definition, new[] { 4 }, Array.Empty<LocalMapMarker>());
            Assert.That(projection.FogCornerMask(4), Is.EqualTo(0xF));
            Assert.That(projection.FogCornerMask(0), Is.EqualTo(0x2));
            Assert.That(projection.FogCornerMask(1), Is.EqualTo(0x3));
            Assert.That(projection.FogCornerMask(8), Is.EqualTo(0x8));
        }

        [Test]
        public void DiscoveredTilesPersistThroughSaveProjection()
        {
            Assert.That(_session.LocalMap.TryProject(out _), Is.True);
            LocalMapSaveData saved = _session.LocalMap.ExportSaveData();
            var replayRoot = new GameObject("Local Map Replay");
            try
            {
                var replay = replayRoot.AddComponent<WorldMapSessionCoordinator>();
                replay.LocalMap.RestoreSaveData(saved);
                Assert.That(replay.LocalMap.KnownTiles(7), Is.EquivalentTo(new[] { 3 }));
                Assert.That(LocalMapStateService.ValidateSaveData(saved, out _), Is.True);
            }
            finally { Object.DestroyImmediate(replayRoot); }
        }

        [Test]
        public void PanningCentersAndClampsToSourceViewportLimits()
        {
            Assert.That(RetailLocalMapLayout.CenterOn(new Vector2(750, 600), new Vector2(1000, 800)),
                Is.EqualTo(new Vector2(499, 417)));
            Assert.That(RetailLocalMapLayout.ClampPan(new Vector2(-20, 900), new Vector2(1000, 800)),
                Is.EqualTo(new Vector2(0, 435)));
        }

        [Test]
        public void SourceInterfaceAssetsResolve()
        {
            using RetailUiAssetResolver retail = RetailUiAssetResolver.CreateProduction();
            foreach (int id in new[] { 138, 198, 204, 811, 812, 139, 813 })
                Assert.That(retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source ART {id}");
            foreach (int id in new[] { 811, 812, 139, 813 })
                Assert.That(retail.TryResolve(new UiAssetKey(id, frame: 1), out _), Is.True,
                    $"source ART {id} active frame");
        }

        private static string Normalize(string path) => path.Replace('\\', '/').ToLowerInvariant();

        private static long MakeLocation(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static byte[] BuildTmi(int map, int width, int height, int columns, int rows,
            int centerX, int centerY, float scale)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(TownMapDefinition.SourceVersion);
            writer.Write(0);
            writer.Write(map);
            writer.Write(width);
            writer.Write(height);
            writer.Write(columns);
            writer.Write(rows);
            writer.Write(0);
            writer.Write(MakeLocation(centerX, centerY));
            writer.Write(scale);
            writer.Write(0);
            return stream.ToArray();
        }

        private static byte[] BuildSector(int townMap)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(0);
            for (int index = 0; index < 4096; index++) writer.Write(0u);
            writer.Write(1);
            writer.Write(0xAA0004);
            writer.Write(0);
            writer.Write(0); writer.Write(0); writer.Write(0);
            writer.Write(townMap); writer.Write(0); writer.Write(0);
            writer.Write(0); writer.Write(0); writer.Write(0);
            writer.Write(new byte[512]);
            writer.Write(0);
            return stream.ToArray();
        }

        private static byte[] BuildBmp()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B'); writer.Write((byte)'M');
            writer.Write(58); writer.Write(0); writer.Write(54);
            writer.Write(40); writer.Write(1); writer.Write(1);
            writer.Write((short)1); writer.Write((short)24); writer.Write(0);
            writer.Write(4); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            writer.Write((byte)0x20); writer.Write((byte)0x40); writer.Write((byte)0x80); writer.Write((byte)0);
            return stream.ToArray();
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(path);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string path = PresentedSector;
                PresentedSector = null;
                if (path != null) _session.UnloadSector(path);
            }
        }
    }
}
