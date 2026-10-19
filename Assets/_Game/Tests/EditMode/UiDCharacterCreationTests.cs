using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UIDCharacterCreation")]
    public sealed class UiDCharacterCreationTests
    {
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailCharacterCreationView _view;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UID-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiDCharacterCreationTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BindCharacterCreationSource(CreateCatalog());
            _session.BindPrototypeSource(number => number is >= 6001 and <= 6207
                ? new ObjectProtoInfo(number, ObjectType.Generic, 0x70000000u,
                    description: number, invAid: (uint)number)
                : null);
            _session.BindDescriptionSource(number => number is >= 6001 and <= 6207
                ? $"Source Component {number}"
                : null);
            _session.BindSpellPresentationSource(SpellPresentationCatalog.FromMes(CreateSpellText()));
            _session.BindCraftingSource(CreateSchematicCatalog());
            _controller = new GameUiController(_session, new EmptySlots());
            Assert.That(_controller.BeginNewGame(), Is.True);
            _view = _root.AddComponent<RetailCharacterCreationView>();
            _view.Bind(_controller, _session, _skin);
            _view.Synchronize();
            _view.NameInput.text = "Ada";
            _view.Synchronize();
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
        public void RequiredIdentityAndEditorAssetsResolveFromRetailData()
        {
            foreach (int id in RetailCharacterCreationLayout.RequiredSourceIds)
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(_retail.TryResolve(new UiAssetKey(765), out UiResolvedAsset identity), Is.True);
            Assert.That(identity.LogicalSize, Is.EqualTo(new Vector2Int(800, 400)));
            Assert.That(_retail.TryResolve(new UiAssetKey(336), out UiResolvedAsset top), Is.True);
            Assert.That(top.LogicalSize, Is.EqualTo(new Vector2Int(800, 41)));
            Assert.That(_retail.TryResolve(new UiAssetKey(335), out UiResolvedAsset bottom), Is.True);
            Assert.That(bottom.LogicalSize, Is.EqualTo(new Vector2Int(800, 159)));
            Assert.That(_retail.TryResolve(new UiAssetKey(22), out UiResolvedAsset editor), Is.True);
            Assert.That(editor.LogicalSize, Is.EqualTo(new Vector2Int(800, 400)));
            Assert.That(_retail.TryResolvePortrait("HUM1", out RetailPortraitAsset portrait), Is.True);
            Assert.That(portrait.SourcePath, Does.StartWith("portrait/"));
        }

        [Test]
        public void ViewStartsOnOneFixedIdentityCompositionWithoutGlobalScrollRect()
        {
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Identity));
            Assert.That(_view.IdentityRoot.sizeDelta, Is.EqualTo(new Vector2(800f, 400f)));
            Assert.That(_view.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
            Assert.That(Object.FindObjectsByType<RetailCharacterCreationView>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void IdentityGeometryMatchesRuntimeSourceRectangles()
        {
            Assert.That(RetailCharacterCreationLayout.Window, Is.EqualTo(new Rect(0, 41, 800, 400)));
            Assert.That(RetailCharacterCreationLayout.BottomBar, Is.EqualTo(new Rect(0, 441, 800, 159)));
            Assert.That(RetailCharacterCreationLayout.BackButton, Is.EqualTo(new Vector2(69, 496)));
            Assert.That(RetailCharacterCreationLayout.ContinueButton, Is.EqualTo(new Vector2(675, 496)));
            Assert.That(RetailCharacterCreationLayout.Portrait, Is.EqualTo(new Rect(95, 28, 64, 64)));
            Assert.That(RetailCharacterCreationLayout.PortraitDraw, Is.EqualTo(new Rect(95, 28, 128, 128)));
            Assert.That(RetailCharacterCreationLayout.EditorPortrait, Is.EqualTo(new Rect(141, 21, 66, 66)));
            Assert.That(((RectTransform)_view.IdentityPortraitImage.transform).sizeDelta,
                Is.EqualTo(new Vector2(128, 128)));
            Assert.That(RetailCharacterCreationLayout.Name, Is.EqualTo(new Rect(46, 183, 228, 19)));
            Assert.That(RetailCharacterCreationLayout.Gender, Is.EqualTo(new Rect(66, 243, 186, 19)));
            Assert.That(RetailCharacterCreationLayout.Race, Is.EqualTo(new Rect(66, 293, 186, 19)));
            Assert.That(RetailCharacterCreationLayout.Background, Is.EqualTo(new Rect(66, 343, 186, 19)));
            Assert.That(RetailCharacterCreationLayout.EditorSubpanel, Is.EqualTo(new Rect(503, 63, 235, 325)));
            Assert.That(RetailCharacterCreationLayout.EditorName, Is.EqualTo(new Rect(212, 15, 270, 19)));
            Assert.That(RetailCharacterCreationLayout.EditorLevel, Is.EqualTo(new Rect(212, 34, 120, 19)));
            Assert.That(RetailCharacterCreationLayout.EditorPoints, Is.EqualTo(new Rect(212, 53, 120, 19)));
            Assert.That(RetailCharacterCreationLayout.EditorExperienceNext,
                Is.EqualTo(new Rect(212, 72, 150, 19)));
            Assert.That(RetailCharacterCreationLayout.EditorMessageIcon, Is.EqualTo(new Rect(217, 69, 64, 64)));
            Assert.That(RetailCharacterCreationLayout.EditorMessageTitle, Is.EqualTo(new Rect(290, 63, 291, 19)));
            Assert.That(RetailCharacterCreationLayout.EditorMessageBody, Is.EqualTo(new Rect(290, 87, 291, 54)));

            AssertTopLeft("Previous Portrait", new Vector2(43, 118 - 41));
            AssertTopLeft("Next Portrait", new Vector2(244, 118 - 41));
            AssertTopLeft("Previous Gender", new Vector2(42, 281 - 41));
            AssertTopLeft("Next Gender", new Vector2(255, 282 - 41));
            AssertTopLeft("Previous Race", new Vector2(42, 331 - 41));
            AssertTopLeft("Next Race", new Vector2(255, 332 - 41));
            AssertTopLeft("Previous Background", new Vector2(42, 381 - 41));
            AssertTopLeft("Next Background", new Vector2(255, 382 - 41));
            AssertTopLeft("Identity Back", new Vector2(69, 55));
            AssertTopLeft("Identity Continue", new Vector2(675, 55));
        }

        [Test]
        public void IdentityTypographyMatchesRuntimeSourceFontsColorsSpacingAndWrapping()
        {
            _view.NameInput.text = string.Empty;
            _view.Synchronize();
            Assert.That(_controller.CreationDraft.Name, Is.Empty);
            SourceUiBitmapText name = Text("Name Bitmap Value");
            Assert.That(name.Text, Is.EqualTo("Choose Name"));
            Assert.That(name.FontSourceId, Is.EqualTo(SourceUiBitmapText.Morph15FontSourceId));
            AssertGlyphTint(name, new Color32(255, 50, 50, 255));
            AssertCenteredWithin((RectTransform)name.transform, new Rect(0f, 0f, 228f, 19f));

            foreach (string selector in new[] { "Gender", "Race", "Background" })
            {
                SourceUiBitmapText text = Text(selector);
                Assert.That(text.FontSourceId, Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId), selector);
                AssertGlyphTint(text, new Color32(255, 50, 50, 255));
            }

            SourceUiBitmapText statsTitle = Text("Initial Stats");
            Assert.That(statsTitle.FontSourceId, Is.EqualTo(SourceUiBitmapText.Morph15FontSourceId));
            AssertGlyphTint(statsTitle, new Color32(250, 250, 250, 255));
            AssertCenteredWithin((RectTransform)statsTitle.transform,
                RetailCharacterCreationLayout.InitialStatsTitle);

            Hover("Race Context", true);
            SourceUiBitmapText descriptionTitle = Text("Description Title");
            Assert.That(descriptionTitle.Text, Is.EqualTo("Race"));
            Assert.That(descriptionTitle.FontSourceId, Is.EqualTo(SourceUiBitmapText.Morph15FontSourceId));
            AssertGlyphTint(descriptionTitle, new Color32(250, 250, 250, 255));
            AssertCenteredWithin((RectTransform)descriptionTitle.transform,
                RetailCharacterCreationLayout.DescriptionTitle);
            SourceUiBitmapText[] lines = _view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Description Body")
                .GetComponentsInChildren<SourceUiBitmapText>(true);
            Assert.That(lines, Has.Length.LessThanOrEqualTo(12));
            Assert.That(lines[0].Text,
                Is.EqualTo("Humans have no special modifiers to their stats and abilities. Humans are the"));
            Assert.That(lines.All(value => value.FontSourceId == SourceUiBitmapText.Flare12FontSourceId), Is.True);
        }

        [TestCase(SourceUiBitmapText.Morph15FontSourceId)]
        [TestCase(SourceUiBitmapText.Pork12FontSourceId)]
        [TestCase(SourceUiBitmapText.Flare12FontSourceId)]
        [TestCase(SourceUiBitmapText.Nick16FontSourceId)]
        [TestCase(SourceUiBitmapText.Garmond9FontSourceId)]
        public void SourceCharacterCreationFontsPreservePaletteCoverageAsAlpha(int sourceId)
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(sourceId, frame: 'A' - 31),
                out UiResolvedAsset glyph), Is.True);
            Assert.That(glyph.Texture.GetPixels32().Any(pixel => pixel.a > 0 && pixel.a < 255), Is.True,
                $"source font {sourceId} should retain anti-aliased palette coverage");
        }

        [Test]
        public void Flare12GlyphsUseTigGammaSpaceCoverageBlendAtNativePixelAlignment()
        {
            SourceUiBitmapText label = Text("Initial Strength");
            Image[] glyphs = label.GetComponentsInChildren<Image>(true);
            Assert.That(glyphs, Is.Not.Empty);
            Assert.That(glyphs.Select(value => value.material.shader.name),
                Has.All.EqualTo(SourceUiBitmapText.TigBitmapFontShaderName));

            foreach (Image glyph in glyphs)
            {
                Assert.That(glyph.mainTexture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(glyph.sprite.pixelsPerUnit, Is.EqualTo(1f));
                Vector2 origin = ((RectTransform)glyph.transform).anchoredPosition;
                Assert.That(origin.x, Is.EqualTo(Mathf.Round(origin.x)));
                Assert.That(origin.y, Is.EqualTo(Mathf.Round(origin.y)));
            }

            Assert.That((Color32)glyphs[0].color, Is.EqualTo(new Color32(250, 250, 250, 255)));
            Assert.That(TigBlendOverBlack(250, 32), Is.EqualTo(31));
            Assert.That(TigBlendOverBlack(250, 128), Is.EqualTo(125));
            Assert.That(TigBlendOverBlack(250, 255), Is.EqualTo(249));

            int legacyLinearMidpoint = Mathf.RoundToInt(
                Mathf.LinearToGammaSpace((250f / 255f) * (128f / 255f)) * 255f);
            Assert.That(legacyLinearMidpoint, Is.GreaterThanOrEqualTo(185));
            Assert.That(legacyLinearMidpoint - TigBlendOverBlack(250, 128), Is.GreaterThan(50),
                "The regression must distinguish TIG byte-space coverage from Unity's default linear UI blend.");

            var expectedCoverage = new[]
            {
                new { Character = 'S', Pixels = 43, Unique = 13, Full = 14, Intermediate = 29, Sum = 7018,
                    Histogram = "16:3,32:1,48:3,68:4,85:1,101:2,117:3,137:2,186:5,206:2,222:1,238:2,255:14" },
                new { Character = '8', Pixels = 52, Unique = 14, Full = 13, Intermediate = 39, Sum = 8669,
                    Histogram = "16:5,32:1,48:1,68:1,85:4,101:2,137:5,153:5,170:1,186:4,206:4,222:2,238:4,255:13" },
                new { Character = 'R', Pixels = 54, Unique = 12, Full = 16, Intermediate = 38, Sum = 9131,
                    Histogram = "16:5,32:1,48:1,68:1,117:12,137:1,153:4,170:2,206:8,222:2,238:1,255:16" },
                new { Character = 'g', Pixels = 43, Unique = 13, Full = 7, Intermediate = 36, Sum = 6880,
                    Histogram = "16:1,48:2,85:3,101:3,117:9,137:2,153:2,170:1,186:6,206:4,222:2,238:1,255:7" },
                new { Character = 'W', Pixels = 69, Unique = 15, Full = 23, Intermediate = 46, Sum = 11779,
                    Histogram = "16:4,32:3,48:1,68:4,85:4,101:2,117:3,137:7,153:2,170:2,186:3,206:6,222:3,238:2,255:23" }
            };
            foreach (var expected in expectedCoverage)
            {
                Assert.That(_retail.TryResolve(
                    new UiAssetKey(SourceUiBitmapText.Flare12FontSourceId, frame: expected.Character - 31),
                    out UiResolvedAsset representative), Is.True);
                byte[] coverage = representative.Texture.GetPixels32()
                    .Select(pixel => pixel.a)
                    .Where(alpha => alpha > 0)
                    .ToArray();
                string histogram = string.Join(",", coverage.GroupBy(alpha => alpha)
                    .OrderBy(group => group.Key)
                    .Select(group => $"{group.Key}:{group.Count()}"));
                Assert.That(coverage.Length, Is.EqualTo(expected.Pixels), expected.Character.ToString());
                Assert.That(coverage.Distinct().Count(), Is.EqualTo(expected.Unique), expected.Character.ToString());
                Assert.That(coverage.Count(alpha => alpha == 255), Is.EqualTo(expected.Full), expected.Character.ToString());
                Assert.That(coverage.Count(alpha => alpha < 255), Is.EqualTo(expected.Intermediate), expected.Character.ToString());
                Assert.That(coverage.Sum(alpha => alpha), Is.EqualTo(expected.Sum), expected.Character.ToString());
                Assert.That(histogram, Is.EqualTo(expected.Histogram), expected.Character.ToString());
            }
        }

        [Test]
        [Category("UID2CharacterCreation")]
        public void PartialWindowOwnsIndependentTopCenterAndBottomRegions()
        {
            Assert.That(_view.TopBackgroundAsset.Key.SourceId, Is.EqualTo(336));
            Assert.That(_view.IdentityBackgroundAsset.Key.SourceId, Is.EqualTo(765));
            Assert.That(_view.IdentityBottomBackgroundAsset.Key.SourceId, Is.EqualTo(335));
            Assert.That(_view.TopRegionRoot, Is.Not.SameAs(_view.IdentityRoot));
            Assert.That(_view.IdentityRoot, Is.Not.SameAs(_view.IdentityBottomRegionRoot));
            Assert.That(_view.TopRegionRoot.sizeDelta, Is.EqualTo(new Vector2(800f, 41f)));
            Assert.That(_view.IdentityRoot.sizeDelta, Is.EqualTo(new Vector2(800f, 400f)));
            Assert.That(_view.IdentityBottomRegionRoot.sizeDelta, Is.EqualTo(new Vector2(800f, 159f)));

            AssertSourceButtonOwnedBy("Identity Continue", 321, _view.IdentityBottomRegionRoot);
            AssertSourceButtonOwnedBy("Identity Back", 323, _view.IdentityBottomRegionRoot);
            AssertSourceButtonOwnedBy("Previous Portrait", 283, _view.IdentityRoot);
            AssertSourceButtonOwnedBy("Next Portrait", 284, _view.IdentityRoot);
            Assert.That(_view.IdentityNavigationRoot, Is.SameAs(_view.IdentityBottomRegionRoot));
            Assert.That(_view.IdentityRoot.IsChildOf(_view.IdentityBottomRegionRoot), Is.False,
                "Large navigation button must not be parented to center Character Creation panel.");
        }

        [Test]
        public void NameEntryUsesRetailLimitAndUpdatesOnlyControllerDraft()
        {
            Assert.That(_view.NameInput.characterLimit, Is.EqualTo(23));
            _view.NameInput.text = "Ada";
            Assert.That(_controller.CreationDraft.Name, Is.EqualTo("Ada"));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void EmptyNamePromptClearsImmediatelyWhenEditingStarts()
        {
            _view.NameInput.text = string.Empty;
            _view.Synchronize();
            SourceUiBitmapText name = Text("Name Bitmap Value");
            Assert.That(name.Text, Is.EqualTo("Choose Name"));

            _view.NameInput.OnSelect(new BaseEventData(EventSystem.current));

            Assert.That(_view.NameInput.text, Is.Empty);
            Assert.That(_controller.CreationDraft.Name, Is.Empty);
            Assert.That(name.Text, Is.Empty);
        }

        [Test]
        public void PortraitGenderAndRaceCyclingRemainCatalogAndBodyLegal()
        {
            int first = _controller.CreationDraft.PortraitId;
            Invoke("Next Portrait");
            Assert.That(_controller.CreationDraft.PortraitId, Is.Not.EqualTo(first));
            Invoke("Previous Gender");
            Assert.That(_controller.CreationDraft.Gender, Is.EqualTo(CharacterGender.Female));
            Assert.That(_session.CharacterCreation.Catalog.IsPortraitLegal(_controller.CreationDraft.PortraitId,
                _controller.CreationDraft.Race, _controller.CreationDraft.Gender), Is.True);
            Invoke("Next Race");
            Assert.That(_controller.CreationDraft.Race, Is.EqualTo(CharacterRace.Elf));
            Assert.That(_controller.CreationDraft.Gender, Is.EqualTo(CharacterGender.Female));
        }

        [TestCase(CharacterRace.Human, CharacterGender.Male)]
        [TestCase(CharacterRace.Human, CharacterGender.Female)]
        [TestCase(CharacterRace.Elf, CharacterGender.Male)]
        [TestCase(CharacterRace.Elf, CharacterGender.Female)]
        [TestCase(CharacterRace.HalfElf, CharacterGender.Male)]
        [TestCase(CharacterRace.HalfElf, CharacterGender.Female)]
        [TestCase(CharacterRace.HalfOrc, CharacterGender.Male)]
        [TestCase(CharacterRace.HalfOrc, CharacterGender.Female)]
        [TestCase(CharacterRace.Dwarf, CharacterGender.Male)]
        [TestCase(CharacterRace.Gnome, CharacterGender.Male)]
        [TestCase(CharacterRace.Halfling, CharacterGender.Male)]
        [TestCase(CharacterRace.HalfOgre, CharacterGender.Male)]
        public void IdentityChangeResetsToFirstLegalPortrait(CharacterRace race, CharacterGender gender)
        {
            _controller.SetCreationIdentity(race, gender);
            PortraitDefinition expected = _session.CharacterCreation.Catalog.FindFirstPortrait(race, gender);
            Assert.That(expected, Is.Not.Null);
            Assert.That(_controller.CreationDraft.PortraitId, Is.EqualTo(expected.Id));
            Assert.That(_session.CharacterCreation.Catalog.IsPortraitLegal(
                _controller.CreationDraft.PortraitId, race, gender), Is.True);
        }

        [Test]
        public void GenderRulesCoverAllEightRacesAndRaceCyclingSkipsUnsupportedFemaleBodies()
        {
            CharacterRace[] female =
                { CharacterRace.Human, CharacterRace.Elf, CharacterRace.HalfElf, CharacterRace.HalfOrc };
            CharacterRace[] maleOnly =
                { CharacterRace.Dwarf, CharacterRace.Gnome, CharacterRace.Halfling, CharacterRace.HalfOgre };
            foreach (CharacterRace race in female)
                Assert.That(CharacterCreationRules.IsGenderLegal(race, CharacterGender.Female), Is.True, race.ToString());
            foreach (CharacterRace race in maleOnly)
                Assert.That(CharacterCreationRules.IsGenderLegal(race, CharacterGender.Female), Is.False, race.ToString());

            Invoke("Previous Gender");
            CharacterRace[] cycled = new CharacterRace[4];
            for (int index = 0; index < cycled.Length; index++)
            {
                cycled[index] = _controller.CreationDraft.Race;
                Invoke("Next Race");
            }
            Assert.That(cycled, Is.EqualTo(female));
            Assert.That(_controller.CreationDraft.Gender, Is.EqualTo(CharacterGender.Female));
        }

        [Test]
        public void IdentityLabelsRemainInsideImmutableSourceRectanglesAfterRepeatedCycling()
        {
            RectTransform gender = (RectTransform)_view.IdentityRoot.Find("Gender");
            RectTransform race = (RectTransform)_view.IdentityRoot.Find("Race");
            RectTransform background = (RectTransform)_view.IdentityRoot.Find("Background");
            for (int index = 0; index < 100; index++)
            {
                Invoke("Next Gender");
                Invoke("Next Race");
                Invoke("Next Background");
            }
            AssertCenteredWithin(gender, RetailCharacterCreationLayout.Gender);
            AssertCenteredWithin(race, RetailCharacterCreationLayout.Race);
            AssertCenteredWithin(background, RetailCharacterCreationLayout.Background);
        }

        [Test]
        public void BackgroundCycleUsesSourceEligibilityOrderWrapAndReplacement()
        {
            CharacterCreationCatalog catalog = _session.CharacterCreation.Catalog;
            Assert.That(catalog.FindSelectableBackgrounds(CharacterRace.Human, CharacterGender.Male)
                    .Select(value => value.Id), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(catalog.FindSelectableBackgrounds(CharacterRace.Elf, CharacterGender.Male)
                    .Select(value => value.Id), Is.EqualTo(new[] { 0, 1, 2, 4, 5 }));
            Assert.That(catalog.TryGetBackground(2, out BackgroundDefinition blacksmith), Is.True);
            Assert.That(blacksmith.Name, Is.EqualTo("Apprenticed to a Blacksmith"));
            Assert.That(catalog.TryGetBackground(3, out BackgroundDefinition raisedByElves), Is.True);
            Assert.That(raisedByElves.Name, Is.EqualTo("Raised by Elves"));
            Assert.That(raisedByElves.IsLegal(CharacterRace.Human, CharacterGender.Male), Is.True);
            Assert.That(raisedByElves.IsLegal(CharacterRace.Human, CharacterGender.Female), Is.True);
            Assert.That(raisedByElves.IsLegal(CharacterRace.Elf, CharacterGender.Male), Is.False);

            FieldInfo archives = typeof(WorldObjectSectorLoader).GetField("artArchives",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(archives, Is.Not.Null);
            Assert.That((string[])archives.GetValue(_root.AddComponent<WorldObjectSectorLoader>()),
                Is.EqualTo(new[] { "arcanum4.dat", "arcanum3.dat", "arcanum2.dat", "arcanum1.dat" }));

            Assert.That(_controller.CreationDraft.BackgroundId, Is.Zero);
            foreach (int expected in new[] { 1, 2, 3, 4, 5, 0 })
            {
                Invoke("Next Background");
                Assert.That(_controller.CreationDraft.BackgroundId, Is.EqualTo(expected));
            }
            Invoke("Previous Background");
            Assert.That(_controller.CreationDraft.BackgroundId, Is.EqualTo(5));
            Invoke("Previous Background");
            Assert.That(_controller.CreationDraft.BackgroundId, Is.EqualTo(4));

            _controller.SetCreationIdentity(CharacterRace.Elf, CharacterGender.Male);
            Assert.That(_controller.CreationDraft.BackgroundId, Is.Zero);
            foreach (int expected in new[] { 1, 2, 4, 5, 0 })
            {
                Invoke("Next Background");
                Assert.That(_controller.CreationDraft.BackgroundId, Is.EqualTo(expected));
            }

            _controller.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            _controller.SetCreationBackground(1);
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Beauty), Is.EqualTo(7));
            _controller.SetCreationBackground(2);
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Beauty), Is.EqualTo(8));
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Strength), Is.EqualTo(9));
            _controller.SetCreationBackground(1);
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Beauty), Is.EqualTo(7));
        }

        [Test]
        public void RetailBackgroundMatrixMatchesSourceEligibilityAndOrderForEveryPlayableIdentity()
        {
            using var vfs = new DatVirtualFileSystem();
            foreach (string archive in new[] { "arcanum4.dat", "arcanum3.dat", "arcanum2.dat", "arcanum1.dat" })
            {
                string path = GameDataLocator.Find(archive);
                Assert.That(path, Is.Not.Null.And.Not.Empty, $"Missing retail source archive {archive}.");
                vfs.MountFile(path);
            }

            MesFile rules = MesReader.Read(vfs.ReadAllBytes("rules/backgrnd.mes"));
            MesFile text = MesReader.Read(vfs.ReadAllBytes("mes/gameback.mes"));
            MesFile effects = MesReader.Read(vfs.ReadAllBytes("rules/effect.mes"));
            MesFile portraits = Mes(
                (1000, "HUM1"), (1001, "HUF1"), (1002, "DWM1"), (1003, "ELM1"),
                (1004, "ELF1"), (1005, "HEM1"), (1006, "HEF1"), (1007, "GNM1"),
                (1008, "HAM1"), (1009, "HOM1"), (1010, "HOF1"), (1011, "HGM1"));
            MapList maps = MapList.Read(Mes((5000,
                "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0")));
            CharacterCreationCatalog catalog = CharacterCreationCatalog.FromMes(
                rules, text, effects, portraits, maps);

            Assert.That(catalog.Backgrounds.Keys.OrderBy(value => value), Is.EqualTo(Enumerable.Range(0, 79)),
                "The retail matrix is the contiguous source-ID sequence 0..78.");
            foreach (BackgroundDefinition background in catalog.Backgrounds.Values)
            {
                int key = background.Id * 10;
                int.TryParse(rules.Get(key + 1), out int expectedEffect);
                int.TryParse(rules.Get(key + 3), out int expectedGold);
                Assert.That(background.TextId, Is.EqualTo(int.Parse(rules.Get(key))));
                Assert.That(background.EffectId, Is.EqualTo(expectedEffect));
                Assert.That(background.Conditions, Is.EqualTo(rules.Get(key + 2) ?? string.Empty));
                Assert.That(background.StartingGold, Is.EqualTo(Math.Max(0, expectedGold)));
                Assert.That(background.RawEffect, Is.EqualTo(effects.Get(background.EffectId) ?? string.Empty));
                Assert.That(background.Name, Is.Not.Null.And.Not.Empty);
            }

            var identities = new[]
            {
                (CharacterRace.Human, CharacterGender.Male, "HUM", 49),
                (CharacterRace.Human, CharacterGender.Female, "HUF", 52),
                (CharacterRace.Dwarf, CharacterGender.Male, "DWM", 34),
                (CharacterRace.Elf, CharacterGender.Male, "ELM", 36),
                (CharacterRace.Elf, CharacterGender.Female, "ELF", 38),
                (CharacterRace.HalfElf, CharacterGender.Male, "HEM", 39),
                (CharacterRace.HalfElf, CharacterGender.Female, "HEF", 41),
                (CharacterRace.Gnome, CharacterGender.Male, "GNM", 43),
                (CharacterRace.Halfling, CharacterGender.Male, "HAM", 44),
                (CharacterRace.HalfOrc, CharacterGender.Male, "HOM", 44),
                (CharacterRace.HalfOrc, CharacterGender.Female, "HOF", 46),
                (CharacterRace.HalfOgre, CharacterGender.Male, "HGM", 38),
            };
            foreach (var identity in identities)
            {
                int[] expected = catalog.Backgrounds.Values
                    .Where(value => SourceAllowsPlayer(value.Conditions, identity.Item3))
                    .OrderBy(value => value.Id).Select(value => value.Id).ToArray();
                BackgroundDefinition[] actual = catalog.FindSelectableBackgrounds(identity.Item1, identity.Item2)
                    .ToArray();
                Assert.That(actual.Select(value => value.Id), Is.EqualTo(expected), identity.Item3);
                Assert.That(actual.Length, Is.EqualTo(identity.Item4), identity.Item3);
                Assert.That(actual.Select(value => value.Id), Is.Ordered.Ascending, identity.Item3);
                Assert.That(actual.Any(value => value.HasUnsupportedEffects), Is.True,
                    $"Effect coverage must not alter source eligibility for {identity.Item3}.");
                Assert.That(actual.All(value => value.Id is < 39 or > 50), Is.True,
                    $"NPC-only pregenerated backgrounds must remain excluded for {identity.Item3}.");
            }
        }

        [Test]
        public void BackgroundCycleImmediatelyReplacesEffectiveStatsWithoutStacking()
        {
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Beauty), Is.EqualTo(8));
            SourceUiBitmapText[] labels = _view.GetComponentsInChildren<SourceUiBitmapText>(true);
            Assert.That(labels.Single(value => value.name == "Initial Strength").Text, Is.EqualTo("ST: 8"));
            Assert.That(labels.Single(value => value.name == "Initial Constitution").Text, Is.EqualTo("CN: 8"));
            Assert.That(labels.Single(value => value.name == "Initial Dexterity").Text, Is.EqualTo("DX: 8"));
            Assert.That(labels.Single(value => value.name == "Initial Willpower").Text, Is.EqualTo("WP: 8"));
            Assert.That(labels.Single(value => value.name == "Initial Derived 0").Text,
                Is.EqualTo("Carry Weight: 4000"));
            Assert.That(labels.Single(value => value.name == "Initial Derived 1").Text,
                Is.EqualTo("Damage Bonus: -1"));
            Assert.That(labels.Single(value => value.name == "Initial Derived 2").Text,
                Is.EqualTo("AC Adjustment: -2"));
            Assert.That(labels.Single(value => value.name == "Initial Derived 3").Text, Is.EqualTo("Speed: 8"));
            Assert.That(labels.Single(value => value.name == "Initial Derived 4").Text, Is.EqualTo("Heal Rate: 3"));
            Assert.That(labels.Single(value => value.name == "Initial Derived 5").Text,
                Is.EqualTo("Poison Recovery: 8"));
            Assert.That(labels.Single(value => value.name == "Initial Derived 6").Text,
                Is.EqualTo("Reaction Modifier: -7"));
            Invoke("Next Background");
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Beauty), Is.EqualTo(7));
            _view.Synchronize();
            SourceUiBitmapText beauty = _view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Initial Beauty");
            Assert.That(beauty.Text, Is.EqualTo("BE: 7"));
            SourceUiBitmapText reaction = _view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Initial Derived 6");
            Assert.That(reaction.Text,
                Is.EqualTo("Reaction Modifier: -12"));
            Invoke("Previous Background");
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(_controller.CreationDraft,
                CharacterAttribute.Beauty), Is.EqualTo(8));
        }

        [Test]
        public void ContinueTransitionsToSourceEditorWithoutCreatingPlayer()
        {
            _view.ContinueToEditor();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Editor));
            Assert.That(_view.EditorRoot.sizeDelta, Is.EqualTo(new Vector2(800f, 400f)));
            Assert.That(_view.EditorPanelRoot.sizeDelta, Is.EqualTo(new Vector2(235f, 325f)));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void EditorUsesSourceAutoLevelHoverWindowAndSelection()
        {
            _view.ContinueToEditor();
            AssertTopLeft("Editor Back", new Vector2(69, 55));
            AssertTopLeft("Editor Continue", new Vector2(675, 55));
            AssertTopLeft("Auto-Level Schemes", new Vector2(754, 21));

            Hover("Auto-Level Schemes", true);
            Assert.That(Text("Editor Message Title").Text, Is.Not.Empty);
            Assert.That(Wrapped("Editor Message Body"), Is.Not.Empty);
            Hover("Auto-Level Schemes", false);
            Invoke("Auto-Level Schemes");

            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Editor));
            Assert.That(_session.PlayerState, Is.Null);
            Assert.That(_view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Auto-Level Schemes Window").Key.SourceId, Is.EqualTo(567));
            Assert.That(Text("Auto-Level Scheme 1").Text, Is.EqualTo("Melee Specialist"));
            Invoke("Select Auto-Level Scheme 1");
            Assert.That(_controller.CreationDraft.AutoLevelScheme, Is.EqualTo(1));
            Assert.That(_view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Any(value => value.name == "Auto-Level Scheme 1"), Is.False);
        }

        [Test]
        public void InvalidNameCannotLeaveIdentityStage()
        {
            _view.NameInput.text = string.Empty;
            _view.ContinueToEditor();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Identity));
            Assert.That(_controller.CreationValidation.Failure, Is.EqualTo(CharacterCreationFailure.InvalidName));
            SourceUiBitmapText validation = Text("Identity Validation");
            Assert.That(validation.Text, Is.EqualTo("You must choose a name."));
            Assert.That(validation.transform.parent, Is.SameAs(_view.IdentityBottomRegionRoot));
            Assert.That(_view.IdentityBottomRegionRoot.Find("Identity Message Icon").gameObject.activeSelf, Is.False);
        }

        [TestCase("Identity Continue", "Next Screen")]
        [TestCase("Identity Back", "Previous Screen")]
        public void IdentityNavigationUsesSharedSourceContextHelp(string control, string expectedBody)
        {
            Hover(control, true);
            Assert.That(Text("Identity Message Title").Text, Is.EqualTo("Action:"));
            Assert.That(Wrapped("Identity Message Body"), Is.EqualTo(expectedBody));
            Assert.That(_view.IdentityBottomRegionRoot.Find("Identity Message Icon").gameObject.activeSelf, Is.True);
            Hover(control, false);
            Assert.That(Text("Identity Message Title").Text, Is.Empty);
        }

        [Test]
        public void IdentityContextUsesSourceGenderAndRaceDescriptions()
        {
            Hover("Gender Context", true);
            Assert.That(Text("Description Title").Text, Is.Empty);
            string gender = Wrapped("Description Body");
            Assert.That(gender, Does.Contain("male gender"));
            Hover("Gender Context", false);
            Hover("Race Context", true);
            Assert.That(Text("Description Title").Text, Is.EqualTo("Race"));
            string race = Wrapped("Description Body");
            Assert.That(race, Is.Not.Empty);
            Hover("Race Context", false);
            Hover("Background Context", true);
            string backgroundTitle = Text("Description Title").Text;
            string background = Wrapped("Description Body");
            Assert.That(background, Is.Not.Empty);

            foreach (string control in new[] { "Previous Gender", "Next Gender" })
            {
                Hover(control, true);
                Assert.That(Text("Description Title").Text, Is.Empty, control);
                Assert.That(Wrapped("Description Body"), Is.EqualTo(gender), control);
                Hover(control, false);
            }
            foreach (string control in new[] { "Previous Race", "Next Race" })
            {
                Hover(control, true);
                Assert.That(Text("Description Title").Text, Is.EqualTo("Race"), control);
                Assert.That(Wrapped("Description Body"), Is.EqualTo(race), control);
                Hover(control, false);
            }
            foreach (string control in new[] { "Previous Background", "Next Background" })
            {
                Hover(control, true);
                Assert.That(Text("Description Title").Text, Is.EqualTo(backgroundTitle), control);
                Assert.That(Wrapped("Description Body"), Is.EqualTo(background), control);
                Hover(control, false);
            }
        }

        [Test]
        public void FreshIdentityUsesRaceContextAndRefreshCannotReplaceItsOwner()
        {
            Assert.That(Text("Description Title").Text, Is.EqualTo("Race"));
            string race = Wrapped("Description Body");
            Assert.That(race,
                Is.EqualTo("Humans have no special modifiers to their stats and abilities. Humans are the "
                           + "most populous and distributed race by far, having numerous cities and "
                           + "kingdoms throughout the world. They interact with the other races as much "
                           + "as the gnomes, and they interbreed with everything (hence, half-elves, half- "
                           + "ogres, and half-orcs). They are not inherently predisposed towards magick or "
                           + "technology, but they are the front-runners in the latest technological race. "
                           + "Being shorter-lived than most of the other races as well as fairly ingenious "
                           + "tinkerers, they are probably attracted to the quick return on investment that "
                           + "technology promises."));

            Hover("Race Context", true);
            _controller.SetCreationBackground(1);
            _view.Synchronize();
            Assert.That(Text("Description Title").Text, Is.EqualTo("Race"));
            Assert.That(Wrapped("Description Body"), Is.EqualTo(race));

            Hover("Race Context", false);
            Assert.That(Text("Description Title").Text, Is.EqualTo("Raised by Snake Handlers"));
            Assert.That(Wrapped("Description Body"), Is.EqualTo("Poison and beauty tradeoffs."));

            _controller.Close();
            _view.Synchronize();
            Assert.That(_controller.BeginNewGame(), Is.True);
            _view.Synchronize();
            Assert.That(Text("Description Title").Text, Is.EqualTo("Race"));
            Assert.That(Wrapped("Description Body"), Is.EqualTo(race));
        }

        [Test]
        public void AttributeButtonsRouteThroughControllerAndCharacterPointValidation()
        {
            _view.ContinueToEditor();
            Invoke("Increase Strength");
            Assert.That(_controller.CreationDraft.GetAttribute(CharacterAttribute.Strength), Is.EqualTo(9));
            Assert.That(_controller.CreationValidation.RemainingCharacterPoints, Is.EqualTo(4));
            Invoke("Decrease Strength");
            Assert.That(_controller.CreationDraft.GetAttribute(CharacterAttribute.Strength), Is.EqualTo(8));
        }

        [Test]
        public void EditorProjectsSourceIdentityDerivedResistanceAndAptitudeValues()
        {
            _view.NameInput.text = "Ada";
            _view.ContinueToEditor();
            Assert.That(Text("Editor Name").Text, Is.EqualTo("Ada"));
            Assert.That(Text("Editor Level").Text, Is.EqualTo("LEVEL: 1"));
            Assert.That(Text("Character Points").Text, Is.EqualTo("POINTS: 5"));
            Assert.That(Text("Editor Experience Next").Text, Is.EqualTo("XP NEXT: 2100"));
            Assert.That(Text("Editor Race").Text, Is.EqualTo("RACE: HUMAN"));
            Assert.That(Text("Editor Gender").Text, Is.EqualTo("GENDER: MALE"));
            Assert.That(Text("Editor Age").Text, Is.EqualTo("AGE: 20"));
            Assert.That(Text("Editor Derived 0").Text, Is.EqualTo("Carry Weight: 4000"));
            Assert.That(Text("Editor Derived 7").Text, Is.EqualTo("Max Followers: 2"));
            Assert.That(Text("Editor Resistance 0").Text, Is.EqualTo("Damage: 0"));
            Assert.That(Text("Editor Resistance 3").Text, Is.EqualTo("Poison: 20"));
            Assert.That(Text("Magick Aptitude Value").Text, Is.EqualTo("0"));
            Assert.That(Text("Technology Aptitude Value").Text, Is.EqualTo("0"));
        }

        [TestCase("Editor Continue", "Next Screen")]
        [TestCase("Editor Back", "Previous Screen")]
        public void EditorNavigationUsesSharedSourceContextHelp(string control, string expectedBody)
        {
            _view.ContinueToEditor();
            Hover(control, true);
            Assert.That(Text("Editor Message Title").Text, Is.EqualTo("Action:"));
            Assert.That(Wrapped("Editor Message Body"), Is.EqualTo(expectedBody));
            Assert.That(_view.EditorBottomRegionRoot.Find("Editor Message Icon").gameObject.activeSelf, Is.True);
            Hover(control, false);
        }

        [Test]
        public void HitPointAndFatigueControlsSpendAndRefundCharacterPointsAtSourceCoordinates()
        {
            _view.ContinueToEditor();
            int remaining = _controller.CreationValidation.RemainingCharacterPoints;
            AssertTopLeft("Decrease Hit Points", new Vector2(409, 144));
            AssertTopLeft("Increase Hit Points", new Vector2(465, 144));
            AssertTopLeft("Decrease Fatigue", new Vector2(409, 214));
            AssertTopLeft("Increase Fatigue", new Vector2(465, 214));
            Invoke("Increase Hit Points");
            Assert.That((_controller.CreationDraft.HitPointPoints,
                    _controller.CreationValidation.RemainingCharacterPoints), Is.EqualTo((1, remaining - 1)));
            Invoke("Increase Fatigue");
            Assert.That((_controller.CreationDraft.FatiguePoints,
                    _controller.CreationValidation.RemainingCharacterPoints), Is.EqualTo((1, remaining - 2)));
            Invoke("Decrease Hit Points");
            Invoke("Decrease Fatigue");
            Assert.That((_controller.CreationDraft.HitPointPoints, _controller.CreationDraft.FatiguePoints,
                _controller.CreationValidation.RemainingCharacterPoints), Is.EqualTo((0, 0, remaining)));
        }

        [Test]
        public void AttributeHoverProjectsRetailMessageIntoLowerInformationPanel()
        {
            _view.ContinueToEditor();
            SourceHoverHintTarget hint = _view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .First(value => value.name == "Strength Value");
            hint.OnPointerEnter(null);
            SourceUiBitmapText title = _view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Editor Message Title");
            RectTransform body = _view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Editor Message Body");
            SourceUiBitmapText[] bodyLines = body.GetComponentsInChildren<SourceUiBitmapText>(true);
            Assert.That(title.Text, Is.EqualTo("Strength"));
            Assert.That(string.Join(" ", bodyLines.Select(value => value.Text)), Is.Not.Empty);
            Assert.That(title.transform.parent, Is.SameAs(_view.EditorBottomRegionRoot));
            Assert.That(body.parent, Is.SameAs(_view.EditorBottomRegionRoot));
            Assert.That(((RectTransform)title.transform).anchoredPosition, Is.EqualTo(new Vector2(290f, -63f)));
            Assert.That(body.anchoredPosition, Is.EqualTo(new Vector2(290f, -87f)));
            Assert.That(bodyLines, Has.Length.LessThanOrEqualTo(3));
            hint.OnPointerExit(null);
        }

        [Test]
        public void SourceMessageProjectionSeparatesTitleAndWrappedBody()
        {
            RetailMessageWindowProjection message = RetailMessageWindowProjection.FromSource(
                "Constitution\nThe endurance of the character.");
            Assert.That(message.Title, Is.EqualTo("Constitution"));
            Assert.That(message.Body, Is.EqualTo("The endurance of the character."));
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(20)]
        public void AttributeValueUsesSourceIntegerCenterForOneAndTwoDigitValues(int value)
        {
            _view.ContinueToEditor();
            _controller.CreationDraft.SetAttribute(CharacterAttribute.Strength, value);
            _controller.CreationDraft.SetAttribute(CharacterAttribute.Intelligence, value);
            Invoke("Magic");
            Invoke("Skills");

            SourceUiBitmapText leftText = Text("Strength Value");
            SourceUiBitmapText rightText = Text("Intelligence Value");
            AssertSourceIntegerCenter(leftText, RetailCharacterCreationLayout.AttributeValueCenterX[0], $" {value} ");
            AssertSourceIntegerCenter(rightText, RetailCharacterCreationLayout.AttributeValueCenterX[4], $" {value} ");
            Assert.That(((RectTransform)leftText.transform).anchoredPosition.y,
                Is.EqualTo(-(RetailCharacterCreationLayout.AttributePlus[0].y
                             + RetailCharacterCreationLayout.AttributeValueTopOffsetFromPlus)));
            Assert.That(((RectTransform)rightText.transform).anchoredPosition.y,
                Is.EqualTo(-(RetailCharacterCreationLayout.AttributePlus[4].y
                             + RetailCharacterCreationLayout.AttributeValueTopOffsetFromPlus)));
            Assert.That(RetailCharacterCreationLayout.AttributeValueCenterX[0],
                Is.Not.EqualTo(RetailCharacterCreationLayout.AttributeValueCenterX[4]),
                "left and right attribute columns have independently measured visible-oval centers");
        }

        [Test]
        public void EditorUsesSourceFontsAndMeasuredCentersForAlignmentAndSecondaryStats()
        {
            _view.ContinueToEditor();

            SourceUiBitmapText alignment = Text("Alignment Value");
            Assert.That(alignment.FontSourceId, Is.EqualTo(SourceUiBitmapText.Cloister18FontSourceId));
            AssertSourceIntegerCenter(alignment, RetailCharacterCreationLayout.AlignmentValueCenterX, "0");
            AssertSourceIntegerCenter(Text("Hit Point Value"),
                RetailCharacterCreationLayout.VitalityValueCenterX, " 30 ");
            AssertSourceIntegerCenter(Text("Fatigue Value"),
                RetailCharacterCreationLayout.VitalityValueCenterX, " 30 ");
            Assert.That(Text("Editor Derived 0").FontSourceId,
                Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId));
            Assert.That(Text("Editor Resistance 0").FontSourceId,
                Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId));
            SourceUiBitmapText derivedHeading = Text("Derived Stats Heading");
            Assert.That(derivedHeading.FontSourceId, Is.EqualTo(SourceUiBitmapText.Morph15FontSourceId));
            AssertSourceIntegerCenter(derivedHeading, 178f, "Derived Stats");
            SourceUiBitmapText resistanceHeading = Text("Resistance Heading");
            Assert.That(resistanceHeading.FontSourceId, Is.EqualTo(SourceUiBitmapText.Morph15FontSourceId));
            AssertSourceIntegerCenter(resistanceHeading, 383f, "Resistance");
        }

        private static void AssertSourceIntegerCenter(SourceUiBitmapText text, float sourceCenterX,
            string expectedText)
        {
            RectTransform rect = (RectTransform)text.transform;
            Assert.That(text.Text, Is.EqualTo(expectedText));
            Assert.That(text.SourceMeasuredWidth, Is.GreaterThan(0));
            Assert.That(rect.anchoredPosition.x,
                Is.EqualTo((int)sourceCenterX - text.SourceMeasuredWidth / 2));
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(Mathf.Round(rect.anchoredPosition.x)),
                "source bitmap glyph origins must remain on integer framebuffer pixels");
            foreach (RectTransform glyph in text.GetComponentsInChildren<RectTransform>(true).Skip(1))
                Assert.That(glyph.anchoredPosition.x, Is.EqualTo(Mathf.Round(glyph.anchoredPosition.x)),
                    "each source bitmap glyph must begin on an integer framebuffer pixel");
        }

        [Test]
        public void SkillsPanelUsesFourSourceGroupsAndM12BMutation()
        {
            _view.ContinueToEditor();
            Invoke("Increase Bow");
            Assert.That(_controller.CreationDraft.GetSkillPoints(CharacterSkill.Bow), Is.EqualTo(1));
            Assert.That(_view.SkillGauges[CharacterSkill.Bow].fillAmount, Is.EqualTo(.2f).Within(.001f));
            Invoke("Skill Group 3");
            Assert.That(_view.SkillGroup, Is.EqualTo(3));
            Invoke("Increase Repair");
            Assert.That(_controller.CreationDraft.GetSkillPoints(CharacterSkill.Repair), Is.EqualTo(1));
        }

        [Test]
        public void SkillRowsProjectSourceUnitsAndDynamicGoverningRequirement()
        {
            _view.ContinueToEditor();
            Assert.That(_retail.TryReadMessage("mes/skill.mes", 20, out string sourceDescription), Is.True);
            Assert.That(sourceDescription, Does.Contain("dependent upon your dexterity"));
            Assert.That(_view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Any(value => value.name == "Bow Rank"), Is.False,
                "source draws the gauge and training name; no synthetic rank string overlays it");
            Hover("Bow Name", true);
            Assert.That(Text("Editor Message Title").Text, Is.EqualTo("Bow"));
            Assert.That(Text("Editor Message Requirement").Text, Is.EqualTo("Minimum DX to raise: 6"));
            Assert.That(Wrapped("Editor Message Body"), Does.Contain("dependent upon your dexterity"));
            Invoke("Increase Bow");
            Assert.That(_view.SkillGauges[CharacterSkill.Bow].fillAmount, Is.EqualTo(.2f).Within(.001f));
            Hover("Bow Name", true);
            Assert.That(Text("Editor Message Requirement").Text, Is.EqualTo("Minimum DX to raise: 9"));
        }

        [TestCase(0, CharacterSkill.Bow, "DX")]
        [TestCase(1, CharacterSkill.Prowling, "PE")]
        [TestCase(2, CharacterSkill.Haggle, "WP")]
        [TestCase(3, CharacterSkill.Repair, "IN")]
        public void EverySkillFamilyUsesItsAuthoritativeGoverningAttribute(int group, CharacterSkill skill,
            string abbreviation)
        {
            _view.ContinueToEditor();
            Invoke($"Skill Group {group}");
            Hover($"{skill} Name", true);
            Assert.That(Text("Editor Message Requirement").Text,
                Is.EqualTo($"Minimum {abbreviation} to raise: 6"));
        }

        [Test]
        public void SkillGaugeUsesExactSourceTwentyUnitCropAndRestoresOnDecrease()
        {
            _view.ContinueToEditor();
            Image gauge = _view.SkillGauges[CharacterSkill.Bow];
            Assert.That(gauge.enabled, Is.False);
            Invoke("Increase Bow");
            gauge = _view.SkillGauges[CharacterSkill.Bow];
            Assert.That(gauge.enabled, Is.True);
            Assert.That(gauge.fillAmount, Is.EqualTo(.2f).Within(.001f));
            Assert.That(((RectTransform)gauge.transform).sizeDelta, Is.EqualTo(new Vector2(120f, 27f)));
            Invoke("Decrease Bow");
            Assert.That(_view.SkillGauges[CharacterSkill.Bow].enabled, Is.False);
        }

        [Test]
        public void MagicPanelUsesSourceCollegeIconsAndSequentialM12BRank()
        {
            _view.ContinueToEditor();
            Invoke("Magic");
            Assert.That(_view.EditorPage, Is.EqualTo(RetailCreationEditorPage.Magic));
            Invoke("Increase Spell");
            Assert.That(_controller.CreationDraft.GetSpellRank(SpellCollege.Conveyance), Is.EqualTo(1));
            Assert.That(Text("College Name").Text, Is.EqualTo("Conveyance Source"));
            Assert.That(Text("Spell 1").Text, Is.EqualTo("Source Spell 0"));
            Assert.That(Text("Spell Level 1").Text, Is.EqualTo("Minimum Level: 1"));
            Assert.That(_view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Spell 0 Icon").Key.SourceId, Is.EqualTo(12));
        }

        [Test]
        public void TechnologyPanelUsesEightSourceDisciplinesAndM12BRank()
        {
            _view.ContinueToEditor();
            Invoke("Technology");
            Assert.That(_view.EditorPage, Is.EqualTo(RetailCreationEditorPage.Technology));
            Assert.That(Text("Technology Discipline Icon").FontSourceId, Is.EqualTo(300));
            SourceUiBitmapText technologyName = Text("Technology Name");
            Assert.That(technologyName.FontSourceId, Is.EqualTo(SourceUiBitmapText.Nick16FontSourceId));
            Assert.That(((RectTransform)technologyName.transform).anchoredPosition,
                Is.EqualTo(new Vector2(118 - technologyName.SourceMeasuredWidth / 2, -69)));
            AssertGlyphTint(technologyName, new Color32(255, 255, 255, 255));
            SourceUiBitmapText expertise = Text("Technology Expertise");
            Assert.That(((RectTransform)expertise.transform).anchoredPosition, Is.EqualTo(new Vector2(204, -72)));
            AssertGlyphTint(expertise, new Color32(0, 0, 255, 255));
            SourceUiBitmapText nextName = Text("Schematic 1");
            SourceUiBitmapText nextComponents = Text("Schematic Components 1");
            SourceUiBitmapText unavailableName = Text("Schematic 2");
            SourceUiBitmapText unavailableComponents = Text("Schematic Components 2");
            Assert.That(nextName.FontSourceId, Is.EqualTo(SourceUiBitmapText.Nick16FontSourceId));
            Assert.That(nextComponents.FontSourceId, Is.EqualTo(SourceUiBitmapText.Garmond9FontSourceId));
            Assert.That(((RectTransform)nextName.transform).anchoredPosition, Is.EqualTo(new Vector2(46, -94)));
            Assert.That(((RectTransform)nextComponents.transform).anchoredPosition, Is.EqualTo(new Vector2(46, -109)));
            AssertGlyphTint(nextName, new Color32(255, 255, 0, 255));
            AssertGlyphTint(nextComponents, new Color32(255, 255, 0, 255));
            AssertGlyphTint(unavailableName, new Color32(128, 128, 128, 255));
            AssertGlyphTint(unavailableComponents, new Color32(128, 128, 128, 255));
            Invoke("Increase Technology");
            Assert.That(_controller.CreationDraft.GetTechnologyRank(TechnologyDiscipline.Herbology), Is.EqualTo(1));
            Assert.That(Text("Technology Name").Text, Is.EqualTo("Herbology Novice"));
            Assert.That(Text("Technology Expertise").Text, Is.EqualTo("10"));
            Assert.That(Text("Schematic 1").Text, Is.EqualTo("Herbology Schematic 1"));
            Assert.That(Text("Schematic Components 1").Text,
                Is.EqualTo("[Source Component 6001]+[Source Component 6101]"));
            Assert.That(Text("Schematic Components 1").Text, Does.Not.Match(@"\[\d+\]"));
            Hover("Schematic 1", true);
            Assert.That(Text("Editor Message Title").Text, Is.Not.Empty);
            Assert.That(Text("Editor Message Requirement").Text, Is.EqualTo("Minimum IN: 5"));
            Assert.That(Wrapped("Editor Message Body"), Is.Not.Empty);
            Hover("Schematic 1", false);
            Assert.That(Text("Editor Message Title").Text, Is.Empty);
        }

        [Test]
        public void EditorUsesEffectiveWholeAttributesAndRestoresSourceHeadings()
        {
            Invoke("Next Background");
            _view.ContinueToEditor();
            Assert.That(Text("Beauty Value").Text, Is.EqualTo(" 7 "));
            Assert.That(Text("Beauty Value").Text, Does.Not.Contain("/"));
            Assert.That(((RectTransform)_view.EditorPortraitImage.transform).sizeDelta,
                Is.EqualTo(new Vector2(66, 66)));
        }

        [Test]
        public void EscapeReturnsEditorToIdentityThenMainMenuWithoutPartialPlayer()
        {
            _view.ContinueToEditor();
            _view.HandleEscape();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Identity));
            Assert.That(_controller.CreationDraft, Is.Not.Null);
            _view.HandleEscape();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.MainMenu));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void OriginalEnhancedOriginalRebuildPreservesDraftStageAndPanelSelection()
        {
            _view.ContinueToEditor();
            Invoke("Magic");
            _controller.SetCreationName("Ada");
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            _view.Synchronize();
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            _view.Synchronize();
            Assert.That(_view.Stage, Is.EqualTo(RetailCharacterCreationStage.Editor));
            Assert.That(_view.EditorPage, Is.EqualTo(RetailCreationEditorPage.Magic));
            Assert.That(_controller.CreationDraft.Name, Is.EqualTo("Ada"));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [TestCase(800, 600, 0, 41, 441)]
        [TestCase(1024, 768, 112, 125, 609)]
        [TestCase(1920, 1080, 560, 281, 921)]
        [TestCase(2560, 1440, 880, 461, 1281)]
        [TestCase(3840, 2160, 1520, 821, 2001)]
        [Category("UID2CharacterCreation")]
        public void PartialRegionsUseNativeHighResolutionAnchors(
            int width, int height, float expectedX, float expectedCenterY, float expectedBottomY)
        {
            Rect top = RetailPartialMainMenuLayout.TopScreenRect(width, height);
            Rect center = RetailPartialMainMenuLayout.CenterScreenRect(width, height);
            Rect bottom = RetailPartialMainMenuLayout.BottomScreenRect(width, height);
            Assert.That(top, Is.EqualTo(new Rect(expectedX, 0f, 800f, 41f)));
            Assert.That(center, Is.EqualTo(new Rect(expectedX, expectedCenterY, 800f, 400f)));
            Assert.That(bottom, Is.EqualTo(new Rect(expectedX, expectedBottomY, 800f, 159f)));
            Assert.That(top.width, Is.EqualTo(800f));
            Assert.That(center.width, Is.EqualTo(800f));
            Assert.That(bottom.width, Is.EqualTo(800f));
            Assert.That(center.yMin, Is.GreaterThanOrEqualTo(top.yMax));
            Assert.That(bottom.yMin, Is.GreaterThanOrEqualTo(center.yMax));
        }

        private void Invoke(string name)
        {
            Button button = _view.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == name);
            Assert.That(button.interactable, Is.True, name);
            button.onClick.Invoke();
            _view.Synchronize();
        }

        private void Hover(string name, bool entered)
        {
            SourceHoverHintTarget hint = _view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == name);
            if (entered) hint.OnPointerEnter(null); else hint.OnPointerExit(null);
        }

        private SourceUiBitmapText Text(string name)
            => _view.GetComponentsInChildren<SourceUiBitmapText>(true).Single(value => value.name == name);

        private string Wrapped(string name)
        {
            RectTransform body = _view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == name);
            return string.Join(" ", body.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Select(value => value.Text));
        }

        private void AssertTopLeft(string name, Vector2 expected)
        {
            RectTransform rect = _view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == name);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(expected.x, -expected.y)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
        }

        private void AssertSourceButtonOwnedBy(string name, int sourceId, RectTransform expectedParent)
        {
            SourceUiButton button = _view.GetComponentsInChildren<SourceUiButton>(true)
                .Single(value => value.name == name);
            Assert.That(button.CurrentPresentationKey.SourceId, Is.EqualTo(sourceId));
            Assert.That(button.transform.parent, Is.SameAs(expectedParent),
                "Large navigation button must not be parented to center Character Creation panel.");
        }

        private static void AssertCenteredWithin(RectTransform text, Rect bounds)
        {
            float x = text.anchoredPosition.x;
            Assert.That(x, Is.GreaterThanOrEqualTo(bounds.x));
            Assert.That(x + text.sizeDelta.x, Is.LessThanOrEqualTo(bounds.xMax + .001f));
            Assert.That(x + text.sizeDelta.x * .5f, Is.EqualTo(bounds.center.x).Within(.501f));
        }

        private static void AssertGlyphTint(SourceUiBitmapText text, Color32 expected)
        {
            Image glyph = text.GetComponentsInChildren<Image>(true)
                .First(value => value.GetComponent<SourceUiImage>() != null
                                && value.gameObject.activeInHierarchy);
            Assert.That((Color32)glyph.color, Is.EqualTo(expected), text.name);
        }

        private static int TigBlendOverBlack(int tint, int coverage)
            => tint * coverage >> 8;

        private static bool SourceAllowsPlayer(string conditions, string identity)
        {
            if (string.IsNullOrEmpty(conditions)) return true;
            string source = conditions.ToUpperInvariant();
            return source.Contains(identity) || source.Contains("ANY");
        }

        private static CharacterCreationCatalog CreateCatalog()
        {
            MesFile rules = Mes(
                (0, "1000"), (1, "86"), (2, ""), (3, "400"), (4, ""),
                (10, "1010"), (11, "87"), (12, ""), (13, "400"), (14, ""),
                (20, "1020"), (21, "88"), (22, ""), (23, "400"), (24, ""),
                (30, "1030"), (31, "89"), (32, "HUF HUM"), (33, "400"), (34, "8157"),
                (40, "1040"), (41, "90"), (42, ""), (43, "100"), (44, ""),
                (50, "1050"), (51, "91"), (52, ""), (53, "400"), (54, ""));
            MesFile text = Mes(
                (1000, "No Significant Background\n\nNothing happened."),
                (1010, "Raised by Snake Handlers\n\nPoison and beauty tradeoffs."),
                (1020, "Apprenticed to a Blacksmith\n\nStrength and repair training."),
                (1030, "Raised by Elves\n\nElven training."),
                (1040, "Raised by Monks\n\nPerception training."),
                (1050, "Unsupported\n\nUnknown source effect."));
            MesFile effects = Mes((86, ""), (87, "resistpoison 20, be -1"),
                (88, "st +1, repair +2, dx -2"),
                (89, "repair -1, firearms -1, picklock -1, armtrap -1"),
                (90, "pe +1"), (91, "unknown 1"));
            MesFile portraits = Mes(
                (1000, "HUM1"), (1001, "HUM2"), (1002, "HUF1"),
                (1003, "DWM1"), (1004, "ELM1"), (1005, "ELF1"),
                (1006, "HEM1"), (1007, "HEF1"), (1008, "GNM1"),
                (1009, "HAM1"), (1010, "HOM1"), (1011, "HOF1"), (1012, "HGM1"));
            MapList maps = MapList.Read(Mes((5000,
                "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0")));
            return CharacterCreationCatalog.FromMes(rules, text, effects, portraits, maps,
                Mes((0, "No Scheme"), (1, "Melee Specialist")));
        }

        private static MesFile CreateSpellText()
        {
            var rows = new List<KeyValuePair<int, string>>();
            for (int id = 0; id < 80; id++) rows.Add(new KeyValuePair<int, string>(id, $"Source Spell {id}"));
            for (int id = 0; id < 16; id++) rows.Add(new KeyValuePair<int, string>(500 + id,
                id == 0 ? "Conveyance Source" : $"College {id}"));
            return new MesFile(rows);
        }

        private static SchematicCatalog CreateSchematicCatalog()
        {
            var rules = new List<KeyValuePair<int, string>>();
            var text = new List<KeyValuePair<int, string>>();
            for (int degree = 1; degree <= 7; degree++)
            {
                int id = 1990 + degree * 10;
                int name = 100 + degree;
                int description = 200 + degree;
                rules.Add(new(id, name.ToString()));
                rules.Add(new(id + 1, description.ToString()));
                rules.Add(new(id + 2, (300 + degree).ToString()));
                rules.Add(new(id + 3, (5980 + degree).ToString()));
                rules.Add(new(id + 4, (6080 + degree).ToString()));
                rules.Add(new(id + 5, (6180 + degree).ToString()));
                rules.Add(new(id + 6, "1"));
                text.Add(new(name, $"Herbology Schematic {degree}"));
                text.Add(new(description, $"Description {degree}"));
            }
            return SchematicCatalog.FromMes(new MesFile(rules), new MesFile(text));
        }

        private static MesFile Mes(params (int key, string value)[] values)
            => new(values.Select(value => new KeyValuePair<int, string>(value.key, value.value)).ToList());

        private sealed class EmptySlots : ISessionSaveSlotOperations
        {
            public SessionSaveSlotResult SaveSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotListResult ListSlots()
                => new(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new(SessionSaveSlotFailure.None);
        }
    }
}
