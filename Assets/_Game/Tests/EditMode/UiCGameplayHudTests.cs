using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
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
    [Category("UICGameplayHud")]
    public sealed class UiCGameplayHudTests
    {
        private const string Sector = "maps/test/1.sec";
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailGameplayHudView _view;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIC-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiCGameplayHudTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            var gold = new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3)
                { GoldQuantity = 1 };
            _session.BindPrototypeSource(number => number == 9056 ? gold : null);
            _session.BindMapTransitionSource(new MapTransitionResolver(
                MapList.Read(MesReader.Read("{5000}{test, 0, 0}\n")), _ => false, _ => null));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity, Sector, Vector2.one,
                0x28100000u);
            _controller = new GameUiController(_session, new EmptySlots());
            _view = _root.AddComponent<RetailGameplayHudView>();
            _view.Bind(_controller, _session, screen => _controller.Open(screen), _skin);
            _view.Synchronize(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (EventSystem events in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (events.name == "OpenArcanum UI EventSystem") Object.DestroyImmediate(events.gameObject);
            _skin?.Dispose();
            _enhanced?.Dispose();
            OpenArcanumGraphicsSettings.ClearRuntimeMode();
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, recursive: true);
        }

        [Test]
        public void RetailHudAssetsResolveFromSourceData()
        {
            int[] ids = { 3, 17, 18, 19, 20, 82, 93, 105, 111, 137, 169, 171, 172, 173, 181,
                184, 185, 186, 187, 188, 192, 193, 194, 195, 207, 208, 216, 229, 250, 251,
                252, 253, 279, 280, 292, 293, 354, 469, 470, 471, 472, 473, 474, 558, 559,
                560, 561, 565, 628, 632, 772, 773, 782 };
            foreach (int id in ids)
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(Original(RetailGameplayHudLayout.TopSourceId).LogicalSize,
                Is.EqualTo(new Vector2Int(800, 41)));
            Assert.That(Original(RetailGameplayHudLayout.BottomSourceId).LogicalSize,
                Is.EqualTo(new Vector2Int(800, 159)));
        }

        [Test]
        public void SourceGeometryPreservesTopWorldAndBottomPartition()
        {
            Assert.That(RetailGameplayHudLayout.TopInterface, Is.EqualTo(new Rect(0, 0, 800, 41)));
            Assert.That(RetailGameplayHudLayout.WorldViewport, Is.EqualTo(new Rect(0, 41, 800, 400)));
            Assert.That(RetailGameplayHudLayout.BottomInterface, Is.EqualTo(new Rect(0, 441, 800, 159)));
            Assert.That(RetailGameplayHudLayout.MessageWindow, Is.EqualTo(new Rect(196, 492, 410, 107)));
            Assert.That(RetailGameplayHudLayout.BottomWindowLocal(RetailGameplayHudLayout.MessageWindow),
                Is.EqualTo(new Rect(196, 51, 410, 107)));
        }

        [TestCase(800, 600, 0, 0, 441)]
        [TestCase(1024, 768, 112, 0, 609)]
        [TestCase(1920, 1080, 560, 0, 921)]
        [TestCase(2560, 1440, 880, 0, 1281)]
        [TestCase(3840, 2160, 1520, 0, 2001)]
        [TestCase(1280, 720, 240, 0, 561)]
        [TestCase(1600, 1200, 400, 0, 1041)]
        [TestCase(1920, 1200, 560, 0, 1041)]
        [TestCase(3440, 1440, 1320, 0, 1281)]
        public void SourceWindowsUseNativeHrpGravityPositioning(
            int width, int height, float expectedX, float expectedTopY, float expectedBottomY)
        {
            Assert.That(RetailGameplayHudLayout.TopScreenRect(width, height),
                Is.EqualTo(new Rect(expectedX, expectedTopY, 800, 41)));
            Assert.That(RetailGameplayHudLayout.BottomScreenRect(width, height),
                Is.EqualTo(new Rect(expectedX, expectedBottomY, 800, 159)));
        }

        [Test]
        public void QuickSlotsUseExactTenSourcePositionsAndIds()
        {
            Assert.That(RetailGameplayHudLayout.QuickSlotX,
                Is.EqualTo(new[] { 198f, 237f, 276f, 315f, 354f, 418f, 456f, 495f, 534f, 573f }));
            Assert.That(RetailGameplayHudLayout.QuickSlotSourceIds,
                Is.EqualTo(new[] { 173, 174, 175, 176, 177, 178, 179, 180, 181, 172 }));
        }

        [TestCase(0, 100, 0)]
        [TestCase(1, 100, 8)]
        [TestCase(50, 100, 52)]
        [TestCase(99, 100, 88)]
        [TestCase(100, 100, 88)]
        [TestCase(20, 0, 0)]
        public void VialCropUsesSourceBoundedEightPixelOverlap(int current, int maximum, int expected)
            => Assert.That(RetailGameplayHudLayout.LiquidVisiblePixels(current, maximum), Is.EqualTo(expected));

        [Test]
        public void ViewBuildsOneSourcePresenterAndProjectsAuthoritativeVitals()
        {
            GameUiHudView projection = _controller.ProjectHud();
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_view.TopAsset.Key.SourceId, Is.EqualTo(185));
            Assert.That(_view.BottomAsset.Key.SourceId, Is.EqualTo(184));
            Assert.That(_root.GetComponentsInChildren<SourceUiImage>(true)
                .Any(value => value.CurrentAsset?.Key.SourceId == 3), Is.False);
            Assert.That(_view.LastProjection.HitPoints, Is.EqualTo(projection.HitPoints));
            Assert.That(_view.LastProjection.Fatigue, Is.EqualTo(projection.Fatigue));
            Assert.That(Object.FindObjectsByType<RetailGameplayHudView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void InventoryKeepsHudVisibleButOtherModalScreensHideItWithoutChangingAuthority()
        {
            PersistentPlayerState player = _session.PlayerState;
            Assert.That(_controller.Open(GameUiScreen.Inventory), Is.True);
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_controller.Open(GameUiScreen.Character), Is.True);
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.False);
            Assert.That(_session.PlayerState, Is.SameAs(player));
            _controller.Close();
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.True);
        }

        [Test]
        public void AuthenticInventoryControlReusesExistingControllerRoute()
        {
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Inventory");
            button.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Inventory));
            button.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.None));
        }

        [Test]
        public void CountersUseSourceCloisterFontZeroPaddingBlackBackingAndCenteredMeasuredText()
        {
            SourceUiBitmapText health = _root.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Health Counter Text");
            Assert.That(health.FontSourceId, Is.EqualTo(171));
            Assert.That(health.Text, Has.Length.EqualTo(3));
            Assert.That(health.Text, Does.Match("^[0-9]{3}$"));
            RectTransform textRect = (RectTransform)health.transform;
            Assert.That(textRect.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(textRect.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(textRect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Image backing = health.transform.parent.GetComponent<Image>();
            Assert.That(backing.color, Is.EqualTo(Color.black));
            Assert.That(((RectTransform)backing.transform).sizeDelta,
                Is.EqualTo(RetailGameplayHudLayout.HealthCounter.size));
        }

        [Test]
        public void MaintainedSpellAperturesUseExactSourceOpenAndPluggedOverlays()
        {
            GameUiHudView projection = _controller.ProjectHud();
            SourceUiImage[] slots = _root.GetComponentsInChildren<SourceUiImage>(true)
                .Where(value => value.name.StartsWith("Maintained Spell Slot", StringComparison.Ordinal))
                .OrderBy(value => value.name).ToArray();
            Assert.That(slots, Has.Length.EqualTo(5));
            for (int index = 0; index < slots.Length; index++)
            {
                bool open = index < projection.MaintainedSpellSlotCapacity;
                Assert.That(slots[index].Key.SourceId, Is.EqualTo(open ? 188 + index : 628 + index));
                Assert.That(((RectTransform)slots[index].transform).sizeDelta,
                    Is.EqualTo(open ? new Vector2(32f, 32f) : new Vector2(35f, 35f)));
            }
        }

        [Test]
        public void PrimaryHighlightsAreSavedNotificationsNotOpenScreenState()
        {
            _session.GameplayHud.Notify(HudPrimaryNotification.Inventory);
            _view.Synchronize(true);
            SourceUiButton inventory = _root.GetComponentsInChildren<SourceUiButton>(true)
                .Single(value => value.name == "Inventory");
            Assert.That(inventory.CurrentPresentationKey.SourceId, Is.EqualTo(559));

            inventory.Button.onClick.Invoke();
            _view.Synchronize(true);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Inventory));
            Assert.That(_session.GameplayHud.HasNotification(HudPrimaryNotification.Inventory), Is.False);
            Assert.That(inventory.CurrentPresentationKey.SourceId, Is.EqualTo(186));
            inventory.Button.onClick.Invoke();
            _view.Synchronize(true);
            Assert.That(inventory.CurrentPresentationKey.SourceId, Is.EqualTo(186));
            _session.GameplayHud.Notify(HudPrimaryNotification.Inventory);
            _view.Synchronize(true);
            Assert.That(inventory.CurrentPresentationKey.SourceId, Is.EqualTo(559));
        }

        [Test]
        public void SessionResetDisposesOldHudObserverAndReplacementReceivesInventoryEventsOnce()
        {
            GameplayHudStateService previous = _session.GameplayHud;
            previous.ClearNotification(HudPrimaryNotification.Inventory);

            _session.ResetAuthoritativeSession();
            Assert.That(_session.SelectSector(Sector), Is.True);
            ArcanumObjectId player = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.GetOrCreatePlayer(player, Sector, Vector2.one, 0x28100000u);
            GameplayHudStateService replacement = _session.GameplayHud;
            replacement.ClearNotification(HudPrimaryNotification.Inventory);

            ItemCreationResult created = _session.CreateItem(9056, ObjectPlacement.ContainedBy(player));

            Assert.That(created.Succeeded, Is.True);
            Assert.That(replacement.HasNotification(HudPrimaryNotification.Inventory), Is.True);
            Assert.That(previous.HasNotification(HudPrimaryNotification.Inventory), Is.False,
                "the disposed observer must not retain coordinator event authority");
        }

        [Test]
        public void FateCounterAndFullHealUseAuthoritativeTransactionalState()
        {
            ArcanumObjectId player = _session.PlayerState.Identity;
            _session.Vitality.ApplyHitPointDamage(player, 5);
            _session.Vitality.ApplyFatigueDamage(player, 7);
            _session.GameplayHud.GrantFatePoint();
            Assert.That(_controller.ToggleFatePanel(), Is.True);
            FateResult result = _session.GameplayHud.ActivateFate(FateChoice.FullHeal);
            _view.Synchronize(true);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_session.GameplayHud.FatePoints, Is.Zero);
            Assert.That(_session.Vitality.GetCurrentHitPoints(player),
                Is.EqualTo(_session.Vitality.GetMaximumHitPoints(player)));
            Assert.That(_session.Vitality.GetCurrentFatigue(player),
                Is.EqualTo(_session.Vitality.GetMaximumFatigue(player)));
            SourceUiBitmapText fate = _root.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Fate Counter Text");
            Assert.That(fate.Text, Is.EqualTo("00"));
        }

        [Test]
        public void UnsupportedDeferredFateFailsBeforePointOrVitalityMutation()
        {
            ArcanumObjectId player = _session.PlayerState.Identity;
            _session.GameplayHud.GrantFatePoint();
            int hp = _session.Vitality.GetCurrentHitPoints(player);
            int fatigue = _session.Vitality.GetCurrentFatigue(player);
            FateResult result = _session.GameplayHud.ActivateFate(FateChoice.CriticalHit);
            Assert.That(result.Failure, Is.EqualTo(FateFailure.UnsupportedDeferredEffect));
            Assert.That((_session.GameplayHud.FatePoints,
                    _session.Vitality.GetCurrentHitPoints(player), _session.Vitality.GetCurrentFatigue(player)),
                Is.EqualTo((1, hp, fatigue)));
        }

        [Test]
        public void MaintainedSpellUsesSourceIconOrderAndClickCancelsThroughMagicAuthority()
        {
            ArcanumObjectId player = _session.PlayerState.Identity;
            _session.Magic.SetKnownCollegeRank(player, SpellCollege.Earth, 1);
            SpellCastResult cast = _session.Magic.Cast(new SpellCastRequest(player,
                PhaseOneSpellCatalog.StrengthOfEarth, player));
            Assert.That(cast.Succeeded, Is.True);
            _view.Synchronize(true);
            SourceUiImage first = _root.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Maintained Spell Slot 1");
            Assert.That(first.Key.SourceId, Is.EqualTo(93));
            first.GetComponent<Button>().onClick.Invoke();
            Assert.That(_session.Magic.ActiveEffects, Is.Empty);
        }

        [Test]
        public void WildernessSleepAdvancesSourceTimeHealsAndDemaintainsExactlyOnce()
        {
            ArcanumObjectId player = _session.PlayerState.Identity;
            _session.Magic.SetKnownCollegeRank(player, SpellCollege.Earth, 1);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(player,
                PhaseOneSpellCatalog.StrengthOfEarth, player)).Succeeded, Is.True);
            _session.Vitality.ApplyHitPointDamage(player, 8);
            _session.Vitality.ApplyFatigueDamage(player, 9);
            int hpBefore = _session.Vitality.GetCurrentHitPoints(player);
            int fatigueBefore = _session.Vitality.GetCurrentFatigue(player);
            int healRate = _session.DerivedStats.GetDerivedStat(player, CharacterDerivedStat.HealRate);

            SleepResult result = _session.GameplayHud.Sleep(SleepOption.OneHour);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.EqualTo(3_600_000));
            Assert.That(_session.Vitality.GetCurrentHitPoints(player),
                Is.EqualTo(Math.Min(_session.Vitality.GetMaximumHitPoints(player), hpBefore + healRate)));
            Assert.That(_session.Vitality.GetCurrentFatigue(player),
                Is.EqualTo(Math.Min(_session.Vitality.GetMaximumFatigue(player), fatigueBefore + 3 * healRate)));
            Assert.That(_session.Magic.ActiveEffects, Is.Empty);
        }

        [Test]
        public void ContextCounterExperienceGaugeClockAndRecentActionsProjectSourceFacts()
        {
            ArcanumObjectId player = _session.PlayerState.Identity;
            _session.AddGold(player, 1234);
            _session.Progression.AwardExperience(player, 1050);
            _session.GameplayHud.RecordRecentAction(new QuickSlotBinding(QuickSlotKind.Spell, default,
                PhaseOneSpellCatalog.StrengthOfEarth));
            _view.Synchronize(true);

            Assert.That((_view.LastProjection.ContextIconSourceId, _view.LastProjection.ContextQuantity),
                Is.EqualTo((474, 1234)));
            Assert.That(_view.LastProjection.ExperienceGaugeValue, Is.EqualTo(550));
            Assert.That(_view.ClockPeriod, Is.EqualTo(RetailClockPeriod.Midday));
            SourceUiImage pointer = _root.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Clock Pointer");
            Assert.That(((RectTransform)pointer.transform).sizeDelta, Is.EqualTo(new Vector2(5f, 29f)));
            SourceUiBitmapText money = _root.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Context Counter Text");
            Assert.That(money.Text, Is.EqualTo("001234"));
            SourceUiImage recent = _root.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Recent Action 1");
            Assert.That(recent.Key.SourceId, Is.EqualTo(93));
            Assert.That(_root.GetComponentsInChildren<SourceUiImage>(true)
                .Count(value => value.name.StartsWith("Experience Segment") && value.gameObject.activeSelf),
                Is.EqualTo(5));
        }

        [Test]
        public void HudAuthorityRoundTripsInOptionalSaveV1Domain()
        {
            _session.GameplayHud.GrantFatePoint();
            _session.GameplayHud.Notify(HudPrimaryNotification.Logbook);
            _session.GameplayHud.RecordRecentAction(new QuickSlotBinding(QuickSlotKind.Spell, default,
                PhaseOneSpellCatalog.StrengthOfEarth));
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"gameplayHud\""));
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.GameplayHud.FatePoints, Is.EqualTo(1));
            Assert.That(_session.GameplayHud.HasNotification(HudPrimaryNotification.Logbook), Is.True);
            Assert.That(_session.GameplayHud.RecentActions[0].SourceId,
                Is.EqualTo(PhaseOneSpellCatalog.StrengthOfEarth));
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.Zero);
        }

        [TestCase("Character", GameUiScreen.Character)]
        [TestCase("Logbook", GameUiScreen.Journal)]
        [TestCase("Map", GameUiScreen.Map)]
        [TestCase("Skills", GameUiScreen.Skills)]
        [TestCase("Spells", GameUiScreen.Magic)]
        [TestCase("Schematics", GameUiScreen.Crafting)]
        public void SourceControlsReuseExistingScreenRoutes(string buttonName, GameUiScreen expected)
        {
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == buttonName);
            button.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(expected));
        }

        [Test]
        public void NonSourceConvenienceButtonsAreAbsentFromRetailComposition()
        {
            string[] forbidden = { "PARTY", "BARTER", "SAVE/LOAD" };
            string[] names = _root.GetComponentsInChildren<Button>(true).Select(value => value.name).ToArray();
            Assert.That(forbidden.Intersect(names), Is.Empty);
        }

        [Test]
        public void QuickSlotProjectionUsesCoordinatorOwnedBindingAndQuantity()
        {
            PersistentObjectState item = AddStack(1, 12);
            Assert.That(_session.Shortcuts.AssignItem(0, item.Identity), Is.True);
            _view.Synchronize(true);
            RetailQuickSlotPresentation slot = _view.QuickSlots[0];
            Assert.That((slot.Kind, slot.SourceId, slot.Quantity),
                Is.EqualTo((QuickSlotKind.Item, item.PrototypeNumber, 12)));
            Assert.That(_session.Shortcuts.Get(0).PreferredItem, Is.EqualTo(item.Identity));
        }

        [Test]
        public void EmptyQuickSlotClickUsesExistingControllerAndFailsWithoutMutation()
        {
            int states = _session.States.Count;
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Quick Slot 1");
            button.onClick.Invoke();
            Assert.That(_controller.Feedback, Does.Contain("empty"));
            Assert.That(_session.States.Count, Is.EqualTo(states));
        }

        [Test]
        public void SelectedInventoryItemBindsThroughSourceQuickSlotTarget()
        {
            PersistentObjectState item = AddStack(3, 4);
            Assert.That(_controller.Open(GameUiScreen.Inventory), Is.True);
            Assert.That(_controller.SelectItem(item.Identity), Is.True);
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Quick Slot 1");
            button.onClick.Invoke();
            Assert.That(_session.Shortcuts.Get(0).PreferredItem, Is.EqualTo(item.Identity));
            Assert.That(_session.Shortcuts.Get(0).Kind, Is.EqualTo(QuickSlotKind.Item));
        }

        [Test]
        public void DynamicChildrenRegisterToTheirSourceOwnedWindow()
        {
            Transform top = _root.GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "Retail Top HUD Window");
            Transform bottom = _root.GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "Retail Bottom HUD Window");
            Assert.That(_root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Inventory")
                .transform.parent, Is.EqualTo(top));
            Assert.That(_root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Combat")
                .transform.parent, Is.EqualTo(bottom));
            Assert.That(_root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Quick Slot 1")
                .transform.parent, Is.EqualTo(bottom));
            Assert.That(_root.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Health Empty Vial").transform.parent, Is.EqualTo(bottom));
            Assert.That(_root.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Message Lens").transform.parent, Is.EqualTo(bottom));
        }

        [TestCase(800, 600)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void CameraViewportMatchesSourceWorldAperture(int width, int height)
        {
            Rect viewport = RetailGameplayHudLayout.GameplayCameraViewport(width, height);
            Assert.That(viewport.x, Is.Zero);
            Assert.That(viewport.width, Is.EqualTo(1f));
            Assert.That(viewport.y, Is.Zero);
            Assert.That(viewport.height, Is.EqualTo(1f));
        }

        [Test]
        public void HudHitBandsBlockOnlyCenteredSourceComposition()
        {
            Assert.That(RetailGameplayHudLayout.ContainsInterfacePoint(new Vector2(960, 1070), 1920, 1080), Is.True);
            Assert.That(RetailGameplayHudLayout.ContainsInterfacePoint(new Vector2(960, 500), 1920, 1080), Is.False);
            Assert.That(RetailGameplayHudLayout.ContainsInterfacePoint(new Vector2(30, 20), 1920, 1080), Is.False);
        }

        [Test]
        public void GameplayCursorUsesNativeFullScreenMappingWithSourceHotspot()
        {
            RetailMainMenuCursorView cursor = _root.GetComponentsInChildren<RetailMainMenuCursorView>(true)
                .Single(value => value.name == "Retail Gameplay Cursor");
            Assert.That(cursor.transform.parent.name, Is.EqualTo("Retail Gameplay Cursor Layer"));
            Assert.That(cursor.transform.lossyScale.x, Is.EqualTo(1f).Within(.0001f));
            Assert.That(cursor.transform.lossyScale.y, Is.EqualTo(1f).Within(.0001f));
            Assert.That(cursor.CurrentAsset.LogicalSize, Is.EqualTo(new Vector2Int(24, 27)));
        }

        [TestCase(184, 3200, 636)]
        [TestCase(185, 3200, 164)]
        public void EnhancedHudStripUsesExactFourTimesPixelsWithIdenticalLogicalGeometry(
            int sourceId, int textureWidth, int textureHeight)
        {
            UiResolvedAsset original = Original(sourceId);
            WriteFixture(original, original.LogicalSize * 4);
            _skin.InvalidatePresentation();
            Assert.That(_skin.TryResolve(original.Key, UiAssetSkin.Enhanced, out UiResolvedAsset enhanced), Is.True);
            Assert.That(enhanced.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
            Assert.That(enhanced.Texture.width, Is.EqualTo(textureWidth));
            Assert.That(enhanced.Texture.height, Is.EqualTo(textureHeight));
            Assert.That(enhanced.LogicalSize, Is.EqualTo(original.LogicalSize));
        }

        [Test]
        public void EnhancedControlFixtureAndMissingAssetFallbackArePerAsset()
        {
            UiResolvedAsset control = Original(186);
            WriteFixture(control, control.LogicalSize * 4);
            _skin.InvalidatePresentation();
            Assert.That(_skin.TryResolve(control.Key, UiAssetSkin.Enhanced, out UiResolvedAsset enhanced), Is.True);
            Assert.That(enhanced.ResolvedSkin, Is.EqualTo(UiAssetSkin.Enhanced));
            Assert.That(_skin.TryResolve(new UiAssetKey(187), UiAssetSkin.Enhanced, out UiResolvedAsset fallback), Is.True);
            Assert.That(fallback.IsFallback, Is.True);
            Assert.That(fallback.LogicalSize, Is.EqualTo(Original(187).LogicalSize));
        }

        [Test]
        public void OriginalEnhancedOriginalSwitchPreservesStateSlotsAndHitGeometry()
        {
            PersistentObjectState item = AddStack(2, 3);
            Assert.That(_session.Shortcuts.AssignItem(1, item.Identity), Is.True);
            Rect before = RetailGameplayHudLayout.BottomInterface;
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            _view.Synchronize(true);
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            _view.Synchronize(true);
            Assert.That(RetailGameplayHudLayout.BottomInterface, Is.EqualTo(before));
            Assert.That(_view.QuickSlots[1].SourceId, Is.EqualTo(item.PrototypeNumber));
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(Object.FindObjectsByType<RetailGameplayHudView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
        }

        [Test]
        public void ExternalFixtureCanBeReboundToOwnedProductionResolver()
        {
            _view.Bind(_controller, _session, screen => _controller.Open(screen));
            _view.Synchronize(true);

            Assert.That(_view.IsAvailable, Is.True);
            Assert.That(_view.TopAsset?.Key.SourceId, Is.EqualTo(185));
            Assert.That(_view.BottomAsset?.Key.SourceId, Is.EqualTo(184));
            Assert.That(_view.LastProjection, Is.Not.Null);
        }

        private UiResolvedAsset Original(int sourceId)
        {
            Assert.That(_retail.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset value), Is.True);
            return value;
        }

        private PersistentObjectState AddStack(int sequence, int quantity)
        {
            const int prototype = 9500;
            var source = new ObjectInstance(ObjectType.Ammo, prototype, null, 0x70000000u, 0, 0,
                oid: GuidBytes(sequence), parentOid: Bytes(_session.PlayerState.Identity), invLocation: 0);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                inventoryArtId: 0u, stackQuantity: quantity, inventoryLocation: 0);
        }

        private void WriteFixture(UiResolvedAsset original, Vector2Int size)
        {
            string path = _enhanced.GetReplacementPath(original);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, mipChain: false);
            try
            {
                texture.SetPixel(0, 0, new Color(1f, 0f, 1f, 1f));
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static byte[] GuidBytes(int sequence)
            => Bytes(Parse($"G_{sequence:X8}_0000_0000_0000_000000000000"));

        private static ArcanumObjectId Parse(string key)
        { ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId id); return id; }

        private static byte[] Bytes(ArcanumObjectId identity)
        {
            string compact = identity.Key.Substring(2).Replace("_", "");
            var bytes = new byte[24]; bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private sealed class EmptySlots : ISessionSaveSlotOperations
        {
            public SessionSaveSlotResult SaveSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadSlot(string slotId) => new(SessionSaveSlotFailure.None);
            public SessionSaveSlotListResult ListSlots()
                => new(SessionSaveSlotFailure.None, Array.Empty<SessionSaveSlotInfo>());
            public SessionSaveSlotResult DeleteSlot(string slotId) => new(SessionSaveSlotFailure.None);
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string path)
            { PresentedSector = WorldMapSessionCoordinator.NormalizeSector(path); _session.BeginSector(PresentedSector); return true; }
            public void ClearPresentedSector()
            { string path = PresentedSector; PresentedSector = null; if (path != null) _session.UnloadSector(path); }
        }
    }
}
