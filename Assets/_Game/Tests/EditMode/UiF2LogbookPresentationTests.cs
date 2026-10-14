using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Social;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
            Assert.That(RetailLogbookLayout.LeftHeader, Is.EqualTo(new Rect(236, 43, 187, 19)));
            Assert.That(RetailLogbookLayout.RightHeader, Is.EqualTo(new Rect(482, 43, 187, 19)));
            Assert.That(RetailLogbookLayout.LeftFooter, Is.EqualTo(new Rect(222, 346, 215, 15)));
            Assert.That(RetailLogbookLayout.RightFooter, Is.EqualTo(new Rect(468, 346, 215, 15)));
            Assert.That(RetailLogbookLayout.LineHeight, Is.EqualTo(15f));
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
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.SourceScreenPosition.y),
                Is.EqualTo(new[] { 83f, 129f, 175f, 221f, 267f, 313f, 359f }));
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.SourceScreenPosition.x),
                Is.EqualTo(new[] { 696f, 696f, 696f, 695f, 696f, 696f, 696f }));
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
            _session.BindSocialSources(
                ReputationCatalog.FromMes(MesReader.Read(
                    "{1000}{0 0 0, 10 0 0}\n{1001}{0 0 0, 15 0 0}")),
                SocialAiCatalog.FromMes(MesReader.Read(
                    "{1}{0 0 0 0 0 0 0 0 0 0 40 500}")));
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
        public void SevenTabsUseExactSourceRectanglesRelativeToTheLogbookPanel()
        {
            Rect[] expected =
            {
                new(696f, 42f, 46f, 41f),
                new(696f, 88f, 46f, 41f),
                new(696f, 134f, 46f, 41f),
                new(695f, 180f, 46f, 41f),
                new(696f, 226f, 46f, 41f),
                new(696f, 272f, 46f, 41f),
                new(696f, 318f, 46f, 41f),
            };

            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.PanelRect), Is.EqualTo(expected));
            for (int index = 0; index < RetailLogbookLayout.Tabs.Length; index++)
            {
                RetailLogbookTabLayout layout = RetailLogbookLayout.Tabs[index];
                SourceUiButton button = _view.GetComponentsInChildren<SourceUiButton>(true)
                    .Single(value => value.gameObject.name == layout.Label);
                RectTransform rect = (RectTransform)button.transform;
                Assert.That(rect.parent.gameObject.name, Is.EqualTo("Retail Logbook"));
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(expected[index].x, -expected[index].y)));
                Assert.That(rect.sizeDelta, Is.EqualTo(expected[index].size));
                Assert.That(button.Button.interactable, Is.True);
                Assert.That(button.CurrentPresentationKey.SourceId, Is.EqualTo(layout.SourceId));
                if (index > 0)
                    Assert.That(expected[index].y - expected[index - 1].y - expected[index - 1].height,
                        Is.EqualTo(5f));
            }
        }

        [Test]
        public void TabHoverKeepsSelectionAndGeometryWhileProjectingSourceHelpUntilExit()
        {
            string[] expectedHelp =
            {
                "Rumors & Notes", "Quests", "Reputations", "Blessings and Curses",
                "Kills & Injuries", "Background", "Keyring Contents",
            };
            RetailGameplayHudView hud = _root.AddComponent<RetailGameplayHudView>();
            hud.Bind(_controller, _session, _ => { }, _skin);

            for (int index = 0; index < RetailLogbookLayout.Tabs.Length; index++)
            {
                RetailLogbookTabLayout layout = RetailLogbookLayout.Tabs[index];
                SourceUiButton button = _view.GetComponentsInChildren<SourceUiButton>(true)
                    .Single(value => value.gameObject.name == layout.Label);
                SourceHoverHintTarget hint = button.GetComponent<SourceHoverHintTarget>();
                RectTransform rect = (RectTransform)button.transform;
                Vector2 position = rect.anchoredPosition;
                Vector2 size = rect.sizeDelta;
                UiAssetKey presentation = button.CurrentPresentationKey;
                RetailLogbookTab active = _view.SelectedTab;

                Assert.That(_retail.TryReadMessage("mes/logbk_ui.mes", index, out string sourceHelp), Is.True);
                Assert.That(sourceHelp, Is.EqualTo(expectedHelp[index]));
                Assert.That(hint, Is.Not.Null);

                button.OnPointerEnter(null);
                hint.OnPointerEnter(null);
                hud.Synchronize(true);

                Assert.That(_view.SelectedTab, Is.EqualTo(active));
                Assert.That(rect.anchoredPosition, Is.EqualTo(position));
                Assert.That(rect.sizeDelta, Is.EqualTo(size));
                Assert.That(button.CurrentPresentationKey, Is.EqualTo(presentation));
                Assert.That(_controller.ContextualHelp, Is.EqualTo(expectedHelp[index]));
                Assert.That(hud.IsContextualHelpVisible, Is.True);
                Assert.That(hud.ContextualHelpText, Is.EqualTo(expectedHelp[index]));

                SourceUiBitmapText helpText = _root.GetComponentsInChildren<SourceUiBitmapText>(true)
                    .Single(value => value.gameObject.name == "Contextual Help");
                Assert.That(helpText.FontSourceId,
                    Is.EqualTo(RetailGameplayHudLayout.ContextualHelpFontSourceId));
                SourceUiImage helpIcon = _root.GetComponentsInChildren<SourceUiImage>(true)
                    .Single(value => value.gameObject.name == "World Target Icon");
                Assert.That(helpIcon.Key.SourceId,
                    Is.EqualTo(RetailGameplayHudLayout.ContextualHelpIconSourceId));

                hint.OnPointerExit(null);
                button.OnPointerExit(null);
                hud.Synchronize(true);

                Assert.That(_controller.ContextualHelp, Is.Empty);
                Assert.That(hud.IsContextualHelpVisible, Is.False);
                Assert.That(_view.SelectedTab, Is.EqualTo(active));
                Assert.That(button.CurrentPresentationKey, Is.EqualTo(presentation));
                Assert.That(rect.anchoredPosition, Is.EqualTo(position));
                Assert.That(rect.sizeDelta, Is.EqualTo(size));
            }

            SourceUiButton blessings = _view.GetComponentsInChildren<SourceUiButton>(true)
                .Single(value => value.gameObject.name == "Blessings & Curses");
            SourceHoverHintTarget blessingsHint = blessings.GetComponent<SourceHoverHintTarget>();
            blessings.Button.onClick.Invoke();
            Assert.That(_view.SelectedTab, Is.EqualTo(RetailLogbookTab.BlessingsAndCurses));
            Assert.That(blessings.CurrentPresentationKey,
                Is.EqualTo(RetailLogbookLayout.ButtonFrame(266, 1)));

            blessings.OnPointerEnter(null);
            blessingsHint.OnPointerEnter(null);
            Assert.That(blessings.CurrentPresentationKey,
                Is.EqualTo(RetailLogbookLayout.ButtonFrame(266, 1)));
            Assert.That(_controller.ContextualHelp, Is.EqualTo("Blessings and Curses"));
            blessingsHint.OnPointerExit(null);
            blessings.OnPointerExit(null);

            Assert.That(_view.SelectedTab, Is.EqualTo(RetailLogbookTab.BlessingsAndCurses));
            Assert.That(_controller.ContextualHelp, Is.Empty);
            Assert.That(blessings.CurrentPresentationKey,
                Is.EqualTo(RetailLogbookLayout.ButtonFrame(266, 1)));
        }

        [Test]
        public void ReputationPageUsesAuthoritativeAcquisitionTimeSourceOrderAndRetailNames()
        {
            ArcanumObjectId pc = _session.PlayerState.Identity;
            Assert.That(_session.Social.AddReputation(pc, new ReputationId(1001)), Is.True);
            long firstTimestamp = _session.SourceTime.ElapsedMilliseconds;
            _session.SourceTime.Advance(3 * 60 * 1000);
            Assert.That(_session.Social.AddReputation(pc, new ReputationId(1000)), Is.True);
            long secondTimestamp = _session.SourceTime.ElapsedMilliseconds;

            IReadOnlyList<ReputationLogbookEntry> projection =
                _session.Social.GetReputationLogbookEntries(pc);
            Assert.That(projection.Select(value => value.Reputation.Value),
                Is.EqualTo(new[] { 1001, 1000 }));
            Assert.That(projection.Select(value => value.Timestamp),
                Is.EqualTo(new[] { firstTimestamp, secondTimestamp }));

            Assert.That(_retail.TryReadMessage("mes/gamereplog.mes", 1001, out string firstName), Is.True);
            Assert.That(_retail.TryReadMessage("mes/gamereplog.mes", 1000, out string secondName), Is.True);
            _view.SelectTab(RetailLogbookTab.Reputations);
            string[] rendered = RenderedLines();
            Assert.That(rendered, Does.Contain(SourceCalendar.FormatLogbook(SourceCalendar.Timestamp(firstTimestamp))));
            Assert.That(rendered, Does.Contain(SourceCalendar.FormatLogbook(SourceCalendar.Timestamp(secondTimestamp))));
            string flattened = string.Join(" ", rendered);
            Assert.That(flattened, Does.Contain(firstName));
            Assert.That(flattened, Does.Contain(secondName));
            Assert.That(flattened.IndexOf(firstName, StringComparison.Ordinal),
                Is.LessThan(flattened.IndexOf(secondName, StringComparison.Ordinal)));
            Assert.That(_view.ProjectedEntryCount, Is.EqualTo(2));
            Assert.That(_view.GetComponentsInChildren<SourceUiButton>(true)
                .Where(value => value.name is "Previous Page" or "Next Page")
                .All(value => !value.gameObject.activeSelf), Is.True);
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

        [Test]
        public void LogbookPcLensUsesLiveCritterRendererInsideSourceFrame()
        {
            RetailPcLensView lens = _view.GetComponentInChildren<RetailPcLensView>(true);
            Assert.That(lens, Is.Not.Null);
            Assert.That(lens.gameObject.name, Is.EqualTo("PC Lens Live Character"));
            Assert.That(_view.GetComponentsInChildren<SourceUiImage>(true)
                .Any(value => value.gameObject.name == "PC Lens Frame"), Is.True);
        }

        [Test]
        public void KnownRumorProjectsAuthoredSourceTextAndCalendarTimestamp()
        {
            MesFile source = Mes((20 * 2013, "The crash site contains the remains of a zeppelin."));
            _session.BindRumorSource(source, null);
            _session.Campaign.SetRumorKnown(2013);

            RumorJournalEntry entry = _controller.ProjectRumors().Single();
            Assert.That(entry.RumorId, Is.EqualTo(2013));
            Assert.That(entry.Description, Is.EqualTo("The crash site contains the remains of a zeppelin."));
            Assert.That(SourceCalendar.FormatLogbook(entry.Timestamp), Is.EqualTo("Jan 1, 1885  3:00pm"));

            _view.SelectTab(RetailLogbookTab.RumorsAndNotes);
            Assert.That(RenderedLines(), Does.Contain("Jan 1, 1885  3:00pm"));
            Assert.That(RenderedLines(), Has.Some.Contains("crash site"));
        }

        [Test]
        public void OpeningRumorTimestampsBelongToAcquisitionAndDoNotFollowTheLaterClock()
        {
            MesFile source = Mes(
                (20 * 2013, "The first authored opening note."),
                (20 * 2014, "The second authored opening note."));
            _session.BindRumorSource(source, null);
            _session.Campaign.SetRumorKnown(2013);
            _session.Campaign.SetRumorKnown(2014);

            Assert.That(_controller.ProjectRumors().Select(value => SourceCalendar.FormatLogbook(value.Timestamp)),
                Is.EqualTo(new[] { "Jan 1, 1885  3:00pm", "Jan 1, 1885  3:00pm" }));

            _session.SourceTime.Advance(3 * 60 * 1000);
            Assert.That(_controller.ProjectRumors().Select(value => SourceCalendar.FormatLogbook(value.Timestamp)),
                Is.EqualTo(new[] { "Jan 1, 1885  3:00pm", "Jan 1, 1885  3:00pm" }));
        }

        [Test]
        public void RumorSpreadUsesSourceFontsTextOriginAndHidesUnavailablePageControls()
        {
            MesFile source = Mes(
                (20 * 2013, "It seems as if only I have survived the crash of the I.F.S. Zephyr. The zeppelin " +
                    "was unexpectedly set upon by strange flying machines. Among the wreckage I found a dying " +
                    "gnome who gave me a signet ring and bid me to find the boy who owned it, with a vague warning " +
                    "of something that is almost here. He also said that he had escaped, but I have no clues as to " +
                    "where he escaped from."),
                (20 * 2014, "After I dragged myself from the wreckage of the zeppelin crash, I was approached by " +
                    "a monk of the Panarii religion. He seems to believe that I am the Living One, the reincarnation " +
                    "of some deity of theirs."));
            _session.BindRumorSource(source, null);
            _session.Campaign.SetRumorKnown(2013);
            _session.Campaign.SetRumorKnown(2014);
            _view.SelectTab(RetailLogbookTab.RumorsAndNotes);

            SourceUiBitmapText[] text = _view.GetComponentsInChildren<SourceUiBitmapText>(true);
            SourceUiBitmapText[] lines = text.Where(value => value.gameObject.name == "Quest Text").ToArray();
            Assert.That(lines, Is.Not.Empty);
            Assert.That(lines.Select(value => value.FontSourceId),
                Has.All.EqualTo(RetailLogbookLayout.BodyFontSourceId));
            Assert.That(text.Single(value => value.gameObject.name == "Left Header").FontSourceId,
                Is.EqualTo(RetailLogbookLayout.HeaderFontSourceId));
            Assert.That(text.Single(value => value.gameObject.name == "Right Header").FontSourceId,
                Is.EqualTo(RetailLogbookLayout.HeaderFontSourceId));
            Assert.That(text.Single(value => value.gameObject.name == "Left Page").FontSourceId,
                Is.EqualTo(RetailLogbookLayout.BodyFontSourceId));
            Assert.That(((RectTransform)lines[0].transform).anchoredPosition,
                Is.EqualTo(new Vector2(RetailLogbookLayout.LeftContent.x, -RetailLogbookLayout.LeftContent.y)));
            SourceUiBitmapText leftHeader = text.Single(value => value.gameObject.name == "Left Header");
            Assert.That(((RectTransform)leftHeader.transform).anchoredPosition,
                Is.EqualTo(new Vector2(RetailLogbookLayout.LeftHeader.x
                    + (RetailLogbookLayout.LeftHeader.width - leftHeader.LogicalSize.x) * .5f,
                    -RetailLogbookLayout.LeftHeader.y)));
            Image glyph = lines[0].GetComponentInChildren<Image>(true);
            Assert.That(glyph, Is.Not.Null);
            Assert.That(glyph.color, Is.EqualTo(Color.black));
            Assert.That(glyph.material.shader.name, Is.EqualTo(SourceUiBitmapText.TigBitmapFontShaderName));

            Assert.That(PageControl("Previous Page").gameObject.activeSelf, Is.False);
            Assert.That(PageControl("Next Page").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PageControlsAppearOnlyWhenAnotherRumorSpreadExists()
        {
            var entries = new List<(int key, string value)>();
            for (int id = 2100; id < 2112; id++)
            {
                entries.Add((20 * id,
                    $"Authored rumor {id} has enough words to occupy several lines in the source Logbook page."));
            }
            _session.BindRumorSource(Mes(entries.ToArray()), null);
            for (int id = 2100; id < 2112; id++) _session.Campaign.SetRumorKnown(id);
            _view.SelectTab(RetailLogbookTab.RumorsAndNotes);

            Assert.That(PageControl("Previous Page").gameObject.activeSelf, Is.False);
            Assert.That(PageControl("Next Page").gameObject.activeSelf, Is.True);

            _view.TurnRight();
            Assert.That(PageControl("Previous Page").gameObject.activeSelf, Is.True);
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

        private SourceUiButton PageControl(string name)
            => _view.GetComponentsInChildren<SourceUiButton>(true)
                .Single(value => value.gameObject.name == name);

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
