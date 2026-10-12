using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UiF2LogbookPresentation")]
    public sealed class UiF2LogbookPresentationTests
    {
        [Test]
        public void SourceWindowAndPageGeometryRemainFixedAtEightHundredBySixHundred()
        {
            Assert.That(RetailLogbookLayout.ScreenRect(800, 600), Is.EqualTo(new Rect(0, 41, 800, 400)));
            Assert.That(RetailLogbookLayout.LeftContent, Is.EqualTo(new Rect(222, 72, 215, 270)));
            Assert.That(RetailLogbookLayout.RightContent, Is.EqualTo(new Rect(468, 72, 215, 270)));
            Assert.That(RetailLogbookLayout.PageLineCapacity, Is.EqualTo(18));
        }

        [TestCase(1024, 768)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void SourceWindowCentersWithoutScalingAtTargetResolutions(int width, int height)
        {
            Rect rect = RetailLogbookLayout.ScreenRect(width, height);
            Assert.That(rect.size, Is.EqualTo(new Vector2(800, 400)));
            Assert.That(rect.x, Is.EqualTo((width - 800) * .5f));
            Assert.That(rect.y, Is.EqualTo(41 + (height - 600) * .5f));
        }

        [Test]
        public void SevenTabsUseExactSourceOrderIdsAndPositions()
        {
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.Tab), Is.EqualTo(new[]
            {
                RetailLogbookTab.RumorsAndNotes, RetailLogbookTab.Quests, RetailLogbookTab.Reputations,
                RetailLogbookTab.BlessingsAndCurses, RetailLogbookTab.KillsAndInjuries,
                RetailLogbookTab.Background, RetailLogbookTab.Keys,
            }));
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.SourceId),
                Is.EqualTo(new[] { 265, 267, 271, 266, 270, 268, 269 }));
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.Position.y),
                Is.EqualTo(new[] { 83f, 129f, 175f, 221f, 267f, 313f, 359f }));
        }

        [Test]
        public void ButtonStatesSelectFramesWithoutChangingPaletteOrRotation()
        {
            Assert.That(RetailLogbookLayout.ButtonFrame(267, 1).ToString(), Is.EqualTo("ui:0267:p00:r00:f001"));
            Assert.That(RetailLogbookLayout.ButtonFrame(273, 2).ToString(), Is.EqualTo("ui:0273:p00:r00:f002"));
        }

        [Test]
        public void WordWrappingUsesMeasuredWidthAndPreservesParagraphBreaks()
        {
            IReadOnlyList<string> lines = RetailLogbookPaginator.Wrap("one two three\nfour", 7, _ => 1);
            Assert.That(lines, Is.EqualTo(new[] { "one two", "three", "four" }));
        }

        [Test]
        public void BackgroundPaginatorFillsAuthoredPagesInSourceOrder()
        {
            string body = string.Join("\n", Enumerable.Range(1, 45).Select(value => $"L{value:00}"));
            IReadOnlyList<IReadOnlyList<string>> pages = RetailLogbookPaginator.Paginate(body, 215, 18, _ => 1);
            Assert.That(pages.Select(value => value.Count), Is.EqualTo(new[] { 18, 18, 9 }));
            Assert.That(pages.SelectMany(value => value),
                Is.EqualTo(Enumerable.Range(1, 45).Select(value => $"L{value:00}")));
        }

        [Test]
        public void SpreadPackerFillsLeftThenRightAndReturnsLastCompleteEntry()
        {
            var blocks = new List<RetailLogbookQuestBlock>();
            for (int index = 0; index < 6; index++)
                blocks.Add(new RetailLogbookQuestBlock(default, new[] { "a", "b", "c" }));
            Assert.That(RetailLogbookPaginator.LastVisibleEntry(blocks, 0, 6), Is.EqualTo(1));
            Assert.That(RetailLogbookPaginator.LastVisibleEntry(blocks, 4, 6), Is.EqualTo(5));
        }

        [Test]
        public void InvalidPaginatorRequestsFailClosed()
        {
            var blocks = new List<RetailLogbookQuestBlock>
                { new(default, new[] { "entry" }) };
            Assert.Throws<ArgumentOutOfRangeException>(() => RetailLogbookPaginator.LastVisibleEntry(blocks, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => RetailLogbookPaginator.LastVisibleEntry(blocks, 0, 0));
        }
    }

    [Category("UiF2LogbookPresentation")]
    public sealed class UiF2BackgroundLogbookTests
    {
        private const string StartSector = "maps/arcanum1-024-fixed/86570436012.sec";
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailLogbookView _view;
        private string _longBody;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _retail?.Dispose();
            _retail = null;
        }

        [SetUp]
        public void SetUp()
        {
            _longBody = string.Join("\n", Enumerable.Range(1, 45).Select(value => $"L{value:00}"));
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIF2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiF2BackgroundLogbookTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new FakeOwner(_session));
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);
            _session.BindPrototypeSource(number => number switch
            {
                9056 => new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3)
                    { GoldQuantity = 1 },
                8049 => new ObjectProtoInfo(8049, ObjectType.Armor, 0x70000000u,
                    description: 8049, invAid: 8049),
                _ => null,
            });
            _session.BindCharacterCreationSource(CreateCatalog(_longBody));
            Finalize(0, "First");
            _controller = new GameUiController(_session);
            Assert.That(_controller.Open(GameUiScreen.Journal), Is.True);
            _view = _root.AddComponent<RetailLogbookView>();
            _view.Bind(_controller, _session, _skin);
            _view.SelectTab(RetailLogbookTab.Background);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _skin?.Dispose();
            _enhanced?.Dispose();
            OpenArcanumGraphicsSettings.ClearRuntimeMode();
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, true);
        }

        [Test]
        public void FinalizedBackgroundProjectsBodyAndPaginatesAcrossSourceSpreads()
        {
            GameUiBackgroundLogbookView projected = _controller.ProjectBackgroundLogbook();
            Assert.That(projected.BackgroundId, Is.Zero);
            Assert.That(projected.TextId, Is.EqualTo(1000));
            Assert.That(projected.Body, Is.EqualTo(_longBody));
            Assert.That(_view.ProjectedEntryCount, Is.EqualTo(3));
            Assert.That(RenderedLines(), Is.EqualTo(
                Enumerable.Range(1, 36).Select(value => $"L{value:00}")));

            _view.TurnRight();
            Assert.That(_view.CurrentLeftPage, Is.EqualTo(3));
            Assert.That(RenderedLines(), Is.EqualTo(
                Enumerable.Range(37, 9).Select(value => $"L{value:00}")));
            _view.TurnLeft();
            Assert.That(_view.CurrentLeftPage, Is.EqualTo(1));
        }

        [Test]
        public void ReopeningPreservesPageAndNewFinalizedBackgroundResetsWithoutStaleText()
        {
            _view.TurnRight();
            _controller.Close();
            _view.Synchronize();
            Assert.That(_controller.Open(GameUiScreen.Journal), Is.True);
            _view.Synchronize();
            Assert.That(_view.SelectedTab, Is.EqualTo(RetailLogbookTab.Background));
            Assert.That(_view.CurrentLeftPage, Is.EqualTo(3));
            Assert.That(RenderedLines(), Does.Contain("L45"));

            _session.ResetAuthoritativeSession();
            Finalize(1, "Second");
            Assert.That(_controller.Open(GameUiScreen.Journal), Is.True);
            _view.Synchronize();

            Assert.That(_view.CurrentLeftPage, Is.EqualTo(1));
            Assert.That(_view.ProjectedEntryCount, Is.EqualTo(1));
            Assert.That(RenderedLines(), Is.EqualTo(new[] { "A different authored background." }));
            Assert.That(RenderedLines(), Has.None.StartsWith("L"));
        }

        [Test]
        public void GameplayCursorRemainsAboveLogbookAcrossOpenCloseOpen()
        {
            var events = new GameObject("UI-F.2 EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(_root.transform, false);
            RetailGameplayHudView hud = _root.AddComponent<RetailGameplayHudView>();
            hud.Bind(_controller, _session, _ => { }, _skin);
            Canvas logbookCanvas = _view.GetComponentInChildren<Canvas>(true);

            AssertCursorAboveLogbook(hud, logbookCanvas);
            _controller.Close();
            _view.Synchronize();
            hud.Synchronize(show: true);
            AssertCursorAboveLogbook(hud, logbookCanvas);
            Assert.That(_controller.Open(GameUiScreen.Journal), Is.True);
            _view.Synchronize();
            hud.Synchronize(show: true);
            AssertCursorAboveLogbook(hud, logbookCanvas);
        }

        private static void AssertCursorAboveLogbook(RetailGameplayHudView hud, Canvas logbookCanvas)
        {
            Assert.That(hud.CursorCanvas, Is.Not.Null);
            Assert.That(hud.CursorCanvas.overrideSorting, Is.True);
            Assert.That(hud.CursorCanvas.sortingOrder, Is.GreaterThan(logbookCanvas.sortingOrder));
        }

        private string[] RenderedLines()
            => _view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Where(value => value.gameObject.name == "Quest Text")
                .Select(value => value.Text)
                .ToArray();

        private void Finalize(int backgroundId, string name)
        {
            CharacterCreationFinalizeResult result = _session.CharacterCreation.FinalizeNewGame(new CharacterCreationSpecification
            {
                Name = name,
                BackgroundId = backgroundId,
                PortraitId = 1005,
            });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static CharacterCreationCatalog CreateCatalog(string longBody)
        {
            MesFile rules = Mes(
                (0, "1000"), (1, "86"), (2, ""), (3, "400"), (4, ""),
                (10, "1001"), (11, "86"), (12, ""), (13, "400"), (14, ""));
            MesFile text = Mes(
                (1000, "First Background\n" + longBody),
                (1001, "Second Background\nA different authored background."));
            MapList maps = MapList.Read(Mes((5000,
                "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0")));
            return CharacterCreationCatalog.FromMes(rules, text, Mes((86, "")),
                Mes((1005, "HUM1")), maps);
        }

        private static MesFile Mes(params (int key, string value)[] values)
            => new(values.Select(value => new KeyValuePair<int, string>(value.key, value.value)).ToList());

        private sealed class FakeOwner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public FakeOwner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => StartSector;
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
