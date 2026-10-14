using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Party;
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
            _session.BindSleepLocationSource(new FixedSleepLocationSource(SleepLocationMode.WildernessSleep));
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
                503, 504, 505, 560, 561, 565, 616, 628, 632, 772, 773, 782, 826 };
            foreach (int id in ids)
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(Original(RetailGameplayHudLayout.TopSourceId).LogicalSize,
                Is.EqualTo(new Vector2Int(800, 41)));
            Assert.That(Original(RetailGameplayHudLayout.BottomSourceId).LogicalSize,
                Is.EqualTo(new Vector2Int(800, 159)));
        }

        [Test]
        public void FollowerHudProjectsSourceSlotsPortraitVitalsPoisonSaveLoadAndRemoval()
        {
            const int followerPrototype = 28422;
            var followerProto = new ObjectProtoInfo(followerPrototype, ObjectType.Npc, 0x28100000u,
                description: followerPrototype) { Portrait = 1000 };
            var gold = new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3)
                { GoldQuantity = 1 };
            _session.BindPrototypeSource(number => number == followerPrototype ? followerProto
                : number == 9056 ? gold : null);
            _session.BindDescriptionSource(id => id == followerPrototype ? "Virgil" : null);
            ArcanumObjectId follower = Parse("G_11111111_1111_1111_1111_111111111111");
            var source = new ObjectInstance(ObjectType.Npc, followerPrototype, 0L,
                0x28100000u, 0, 0, oid: Bytes(follower));
            _session.GetOrCreate(source, follower, Sector, source.CurrentArtId.Value, false, false);
            int[] stats = Enumerable.Repeat(8, CharacterAttributeSet.SourceStatArrayCount).ToArray();
            stats[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            stats[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            _session.Characters.GetOrCreateSourceCharacter(follower, ObjectType.Npc, followerPrototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(follower, ObjectType.Npc, followerPrototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(follower, ObjectType.Npc, followerPrototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, 0, 0));
            _session.Vitality.GetOrCreateSourceCharacter(follower, ObjectType.Npc, followerPrototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
            Assert.That(_session.Party.Join(follower).Succeeded, Is.True);

            _view.Synchronize(true);
            GameUiPartyMemberView projected = _controller.ProjectParty().Single();
            Assert.That((projected.Name, projected.PortraitId, projected.PoisonLevel,
                    projected.HealthPercent, projected.FatiguePercent),
                Is.EqualTo(("Virgil", 1000, 0, 100, 100)));
            RectTransform slot = (RectTransform)Descendant("Follower Slot 1");
            Assert.That((slot.anchoredPosition, slot.sizeDelta),
                Is.EqualTo((new Vector2(11f, -50f), new Vector2(40f, 49f))));
            Assert.That(Descendant("Follower Slot 6").gameObject.activeSelf, Is.False);
            Assert.That(Descendant("Follower Slot 1").Find("Frame").GetComponent<SourceUiImage>().Key.SourceId,
                Is.EqualTo(RetailGameplayHudLayout.FollowerFrameSourceId));
            Assert.That(Descendant("Follower Slot 1").Find("Portrait").GetComponent<Image>().sprite,
                Is.Not.Null);

            int hpDamage = _session.Vitality.GetMaximumHitPoints(follower) * 3 / 4;
            int fatigueDamage = _session.Vitality.GetMaximumFatigue(follower) / 2;
            _session.Vitality.ApplyHitPointDamage(follower, hpDamage);
            _session.Vitality.ApplyFatigueDamage(follower, fatigueDamage);
            _session.Poison.Apply(follower, 50);
            _view.Synchronize(true);
            projected = _controller.ProjectParty().Single();
            SourceUiImage health = Descendant("Follower Slot 1").Find("Health").GetComponent<SourceUiImage>();
            SourceUiImage fatigue = Descendant("Follower Slot 1").Find("Fatigue").GetComponent<SourceUiImage>();
            Assert.That((projected.Poisoned, health.Key.SourceId),
                Is.EqualTo((true, RetailGameplayHudLayout.FollowerPoisonSourceId)));
            Assert.That(health.Image.fillAmount,
                Is.EqualTo(RetailGameplayHudLayout.FollowerVisiblePixels(projected.HealthPercent) / 34f));
            Assert.That(fatigue.Image.fillAmount,
                Is.EqualTo(RetailGameplayHudLayout.FollowerVisiblePixels(projected.FatiguePercent) / 34f));

            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            _view.Synchronize(true);
            Assert.That((_controller.ProjectParty().Single().Identity,
                    _controller.ProjectParty().Single().PoisonLevel),
                Is.EqualTo((follower, projected.PoisonLevel)));
            Assert.That(_session.Party.Remove(follower).Succeeded, Is.True);
            _view.Synchronize(true);
            Assert.That(Descendant("Follower Slot 1").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void FollowerHudGeometryAndThresholdsMatchFollowerUiSource()
        {
            Assert.That(RetailGameplayHudLayout.FollowerSlotCount, Is.EqualTo(6));
            Assert.That(Enumerable.Range(0, 6).Select(RetailGameplayHudLayout.FollowerSlot),
                Is.EqualTo(new[]
                {
                    new Rect(11, 50, 40, 49), new Rect(11, 112, 40, 49),
                    new Rect(11, 174, 40, 49), new Rect(11, 236, 40, 49),
                    new Rect(11, 298, 40, 49), new Rect(11, 360, 40, 49),
                }));
            Assert.That(RetailGameplayHudLayout.FollowerPortrait, Is.EqualTo(new Rect(4, 4, 32, 32)));
            Assert.That(RetailGameplayHudLayout.FollowerHealth, Is.EqualTo(new Rect(3, 39, 34, 3)));
            Assert.That(RetailGameplayHudLayout.FollowerFatigue, Is.EqualTo(new Rect(3, 44, 34, 3)));
            Assert.That((RetailGameplayHudLayout.FollowerLowHealthThreshold,
                    RetailGameplayHudLayout.FollowerPoisonThreshold), Is.EqualTo((20, 20)));
            Assert.That(RetailGameplayHudLayout.FollowerVisiblePixels(50), Is.EqualTo(17));
        }

        [Test]
        public void FollowerPortraitHoverClickAndCommandMenuFollowSourceRouting()
        {
            ArcanumObjectId follower = AddFollower("G_22222222_2222_2222_2222_222222222222", 28423,
                "Virgil");
            _view.Synchronize(true);
            Transform slot = Descendant("Follower Slot 1");
            RetailFollowerSlotTarget target = slot.GetComponent<RetailFollowerSlotTarget>();

            target.OnPointerEnter(null);
            Assert.That(_view.WorldTarget?.Identity, Is.EqualTo(follower));
            target.OnPointerExit(null);
            Assert.That(_view.WorldTarget, Is.Null);
            target.OnPointerClick(new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left });
            Assert.That(_controller.SelectedWorldTarget, Is.EqualTo(follower));

            target.OnPointerClick(new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Right });
            RectTransform menu = (RectTransform)Descendant("Follower Command Menu");
            Assert.That(menu.gameObject.activeSelf, Is.True);
            Assert.That(menu.anchoredPosition, Is.EqualTo(new Vector2(51f, -50f)));
            Assert.That(menu.Find("Follower Command Background").GetComponent<SourceUiImage>().Key.SourceId,
                Is.EqualTo(RetailGameplayHudLayout.FollowerCommandMenuSourceId));
            for (int index = 0; index < RetailGameplayHudLayout.FollowerCommandCount; index++)
            {
                Assert.That(_retail.TryReadMessage("mes/follower_ui.mes", index, out string source), Is.True);
                Assert.That(menu.Find($"Follower Command Label {index}").GetComponent<SourceUiBitmapText>().Text,
                    Is.EqualTo(source));
                RectTransform row = (RectTransform)menu.Find($"Follower Command {index}");
                Assert.That(row.anchoredPosition,
                    Is.EqualTo(new Vector2(10f, -(3f + 18f * index))));
                Assert.That(row.sizeDelta, Is.EqualTo(new Vector2(118f, 15f)));
            }
        }

        [Test]
        public void FollowerCommandsTargetOneMemberAndUseSourceWaitBackOffSemantics()
        {
            ArcanumObjectId virgil = AddFollower("G_33333333_3333_3333_3333_333333333333", 28424,
                "Virgil");
            ArcanumObjectId sogg = AddFollower("G_44444444_4444_4444_4444_444444444444", 28425,
                "Sogg");

            Assert.That(_controller.IssueFollowerOrder(virgil, PartyOrder.Wait), Is.True);
            PartyFollowerOrderState virgilState = _session.Party.GetOrderState(virgil);
            PartyFollowerOrderState soggState = _session.Party.GetOrderState(sogg);
            Assert.That((virgilState.LastOrder, virgilState.FollowingEnabled),
                Is.EqualTo((PartyOrder.Wait, false)));
            Assert.That(soggState.FollowingEnabled, Is.True);

            Assert.That(_controller.IssueFollowerOrder(virgil, PartyOrder.BackOff), Is.True);
            Assert.That(_session.Party.GetOrderState(virgil).FollowingEnabled, Is.False,
                "source Back Off stops attacking but does not cancel Wait");
            Assert.That(_controller.IssueFollowerOrder(virgil, PartyOrder.StayClose), Is.True);
            Assert.That((_session.Party.GetOrderState(virgil).FollowingEnabled,
                    _session.Party.GetOrderState(virgil).DesiredFollowRange),
                Is.EqualTo((true, PartyFollowerMovementService.SourceCloseRange)));
            Assert.That(_controller.IssueFollowerOrder(virgil, PartyOrder.SpreadOut), Is.True);
            Assert.That(_session.Party.GetOrderState(virgil).DesiredFollowRange,
                Is.EqualTo(PartyFollowerMovementService.SourceSpreadRange));

            Assert.That(_controller.OpenFollowerCharacter(virgil), Is.True);
            Assert.That((_controller.Screen, _controller.CharacterTarget, _controller.CharacterReadOnly),
                Is.EqualTo((GameUiScreen.Character, virgil, true)));
        }

        [Test]
        public void WorldTargetProjectionSharesNameDispositionDeathAndSourceLensGeometry()
        {
            ArcanumObjectId npc = Parse("G_0000C001_0000_0000_0000_000000000000");
            var source = new ObjectInstance(ObjectType.Npc, 30000, 0L, 0x28100000u, 0, 0);
            _session.GetOrCreate(source, npc, Sector, source.CurrentArtId.Value, false, false,
                nameIndex: 30000);
            int[] stats = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) stats[index] = 8;
            stats[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            stats[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Elf;
            _session.Characters.GetOrCreateSourceCharacter(npc, ObjectType.Npc, 30000, stats, null);
            _session.Vitality.GetOrCreateSourceCharacter(npc, ObjectType.Npc, 30000,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0,
                    null, 0, null, 0, null, 0));
            _session.BindPrototypeSource(number => number == 30000
                ? new ObjectProtoInfo(30000, ObjectType.Npc, 0x28100000u, description: 30000) : null);
            _session.BindDescriptionSource(id => id == 30000 ? "Virgil" : null);

            GameUiWorldTargetView friendly = _controller.ProjectWorldTarget(npc);
            Assert.That((friendly.Name, friendly.Dead, friendly.Hostile, friendly.IconSourceId),
                Is.EqualTo(("Virgil", false, false, 376)));
            Assert.That(_session.Social.SetHostile(npc, _session.PlayerState.Identity), Is.True);
            Assert.That(_controller.ProjectWorldTarget(npc).Hostile, Is.True);
            _session.Vitality.ApplyHitPointDamage(npc, 1000);
            GameUiWorldTargetView corpse = _controller.ProjectWorldTarget(npc);
            Assert.That((corpse.Dead, corpse.Hostile), Is.EqualTo((true, false)));
            _view.SetWorldTarget(corpse);
            _view.Synchronize(true);
            Assert.That(_view.WorldTarget, Is.SameAs(corpse));
            Assert.That(RetailGameplayHudLayout.WorldTargetIcon, Is.EqualTo(new Rect(217, 510, 64, 64)));
            Assert.That(RetailGameplayHudLayout.WorldTargetName, Is.EqualTo(new Rect(290, 504, 291, 19)));
            Assert.That(RetailGameplayHudLayout.WorldTargetStatus, Is.EqualTo(new Rect(290, 528, 291, 18)));
        }

        [Test]
        public void AuthoredDescriptionPrecedesPrototypeAndWorldItemsUseSharedInspection()
        {
            ArcanumObjectId named = Parse("G_0000C101_0000_0000_0000_000000000000");
            ArcanumObjectId ordinary = Parse("G_0000C102_0000_0000_0000_000000000000");
            var prototype = new ObjectProtoInfo(10093, ObjectType.Generic, 0x60000009u,
                description: 10093, invAid: 0x60001009u, weight: 20) { HpPoints = 100 };
            _session.BindPrototypeSource(number => number == 10093 ? prototype
                : number == 9056 ? new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u,
                    invAid: 3) { GoldQuantity = 1 } : null);
            _session.BindDescriptionSource(id => id switch
            {
                40001 => "Preston Radcliffe",
                10093 => "Wine",
                _ => null,
            });
            var namedSource = new ObjectInstance(ObjectType.Generic, 10093, 0L, 0x60000009u, 0, 0);
            var ordinarySource = new ObjectInstance(ObjectType.Generic, 10093, 1L, 0x60000009u, 1, 0);
            PersistentObjectState namedState = _session.GetOrCreate(namedSource, named, Sector,
                namedSource.CurrentArtId.Value, false, false, inventoryArtId: prototype.InvAid,
                unitWeight: 20, maximumHitPoints: 100, descriptionId: 40001);
            PersistentObjectState ordinaryState = _session.GetOrCreate(ordinarySource, ordinary, Sector,
                ordinarySource.CurrentArtId.Value, false, false, inventoryArtId: prototype.InvAid,
                unitWeight: 20, maximumHitPoints: 100);

            Assert.That(_session.ResolveDisplayName(namedState), Is.EqualTo("Preston Radcliffe"));
            Assert.That(_session.ResolveDisplayName(ordinaryState), Is.EqualTo("Wine"));
            GameUiWorldTargetView target = _controller.ProjectWorldTarget(ordinary);
            Assert.That(target, Is.Not.Null);
            Assert.That(target.Item.Name, Is.EqualTo("Wine"));
            Assert.That(target.Item.Summary, Does.Contain("Weight: 20 stone"));
            Assert.That(target.Item.Summary, Does.Contain("100/100"));
        }

        [Test]
        public void WorldHoverOwnsTheInformationLensAndIdleDoesNotRestoreWeaponText()
        {
            ArcanumObjectId item = Parse("G_0000C103_0000_0000_0000_000000000000");
            var prototype = new ObjectProtoInfo(10093, ObjectType.Generic, 0x60000009u,
                description: 10093, invAid: 0x60001009u, weight: 20) { HpPoints = 100 };
            _session.BindPrototypeSource(number => number == 10093 ? prototype : null);
            _session.BindDescriptionSource(id => id == 10093 ? "Wine" : null);
            var source = new ObjectInstance(ObjectType.Generic, 10093, 0L, 0x60000009u, 0, 0);
            _session.GetOrCreate(source, item, Sector, source.CurrentArtId.Value, false, false,
                inventoryArtId: prototype.InvAid, unitWeight: 20, maximumHitPoints: 100);

            _controller.ShowFeedback("Combat ready.");
            _view.SetWorldTarget(_controller.ProjectWorldTarget(item));
            _view.Synchronize(true);
            Transform message = Descendant("Message");
            Transform targetName = Descendant("World Target Name");
            Assert.That(message.gameObject.activeSelf, Is.False);
            Assert.That(targetName.gameObject.activeSelf, Is.True);

            _view.SetWorldTarget(null);
            _controller.ShowFeedback(string.Empty);
            _view.Synchronize(true);
            Assert.That(message.gameObject.activeSelf, Is.True);
            Assert.That(targetName.gameObject.activeSelf, Is.False);
            Assert.That(_controller.Feedback, Is.Empty);
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
        public void ManagementSheetsAndLogbookKeepHudVisibleButOtherModalScreensHideItWithoutChangingAuthority()
        {
            PersistentPlayerState player = _session.PlayerState;
            Assert.That(_controller.Open(GameUiScreen.Inventory), Is.True);
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_controller.Open(GameUiScreen.Character), Is.True);
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_controller.Open(GameUiScreen.Journal), Is.True);
            _view.Synchronize(true);
            Assert.That(_view.IsVisible, Is.True);
            Assert.That(_controller.Open(GameUiScreen.Options), Is.True);
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
            Assert.That(inventory.CurrentPresentationKey.Frame, Is.Zero);

            inventory.OnPointerEnter(null);
            Assert.That(inventory.CurrentPresentationKey.Frame, Is.Zero,
                "source notification ART has no separate hover frame");
            inventory.OnPointerDown(null);
            Assert.That(inventory.CurrentPresentationKey.Frame, Is.EqualTo(1),
                "source notification ART uses its second and final frame while pressed");
            inventory.OnPointerUp(null);
            inventory.OnPointerExit(null);

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
        public void SupportedDeferredFateArmsExactlyOnceAndCanRefundBeforeResolution()
        {
            _session.GameplayHud.GrantFatePoint();
            FateResult result = _session.GameplayHud.ActivateFate(FateChoice.CriticalHit);

            Assert.That(result.Succeeded, Is.True);
            Assert.That((_session.GameplayHud.FatePoints,
                    _session.GameplayHud.IsFateActive(FateChoice.CriticalHit)), Is.EqualTo((0, true)));
            Assert.That(_session.GameplayHud.ActivateFate(FateChoice.CriticalHit).Failure,
                Is.EqualTo(FateFailure.AlreadyActive));
            Assert.That(_session.GameplayHud.DeactivateFate(FateChoice.CriticalHit).Succeeded, Is.True);
            Assert.That((_session.GameplayHud.FatePoints,
                    _session.GameplayHud.IsFateActive(FateChoice.CriticalHit)), Is.EqualTo((1, false)));
        }

        [TestCase(FateChoice.ForceGoodReaction)]
        [TestCase(FateChoice.CriticalHit)]
        [TestCase(FateChoice.CriticalMiss)]
        [TestCase(FateChoice.SaveAgainstMagick)]
        [TestCase(FateChoice.SpellAtMaximum)]
        public void SourceBackedDeferredFatesCanBeArmed(FateChoice choice)
        {
            _session.GameplayHud.GrantFatePoint();
            Assert.That(_session.GameplayHud.ActivateFate(choice).Succeeded, Is.True);
            Assert.That(_session.GameplayHud.IsFateActive(choice), Is.True);
        }

        [TestCase(FateChoice.CriticalSuccessGambling)]
        [TestCase(FateChoice.CriticalSuccessHeal)]
        [TestCase(FateChoice.CriticalSuccessPickPocket)]
        [TestCase(FateChoice.CriticalSuccessRepair)]
        [TestCase(FateChoice.CriticalSuccessPickLocks)]
        [TestCase(FateChoice.CriticalSuccessDisarmTraps)]
        public void UnsupportedSkillFateFailsBeforePointMutation(FateChoice choice)
        {
            _session.GameplayHud.GrantFatePoint();
            FateResult result = _session.GameplayHud.ActivateFate(choice);
            Assert.That(result.Failure, Is.EqualTo(FateFailure.UnsupportedDeferredEffect));
            Assert.That((_session.GameplayHud.FatePoints, _session.GameplayHud.ActiveFateFlags),
                Is.EqualTo((1, 0)));
        }

        [Test]
        public void FateCatalogRecordsAllRetailChoicesAndResolverBoundaries()
        {
            Assert.That(FateCatalog.All.Select(value => value.Choice),
                Is.EqualTo(Enum.GetValues(typeof(FateChoice)).Cast<FateChoice>()));
            Assert.That(FateCatalog.All.All(value => value.Cost == 1), Is.True);
            Assert.That(FateCatalog.All.Count(value => value.HasRuntimeResolver), Is.EqualTo(6));
            Assert.That(FateCatalog.All.Count(value => !value.HasRuntimeResolver), Is.EqualTo(6));
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
        public void FullSleepRowExecutesAndTwoFrameFamiliesNeverRequestFrameTwo()
        {
            Assert.That(_controller.ToggleSleepPanel(), Is.True, _controller.Feedback);
            _view.Synchronize(true);
            SourceUiButton sleep = _root.GetComponentsInChildren<SourceUiButton>(true)
                .Single(value => value.name == "Sleep Choice 0");
            sleep.OnPointerDown(null);
            Assert.That(sleep.CurrentPresentationKey,
                Is.EqualTo(new UiAssetKey(293, frame: 1)));
            sleep.OnPointerUp(null);
            Button row = _root.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Sleep Row 0");
            row.onClick.Invoke();
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.EqualTo(3_600_000));

            SourceUiButton menu = _root.GetComponentsInChildren<SourceUiButton>(true)
                .Single(value => value.name == "Sleep");
            menu.OnPointerDown(null);
            Assert.That(menu.CurrentPresentationKey, Is.EqualTo(new UiAssetKey(137, frame: 1)));
        }

        [Test]
        public void TownWaitAdvancesTimeAndDemaintainsWithoutRecovery()
        {
            _session.BindSleepLocationSource(new FixedSleepLocationSource(SleepLocationMode.TownWait));
            ArcanumObjectId player = _session.PlayerState.Identity;
            _session.Magic.SetKnownCollegeRank(player, SpellCollege.Earth, 1);
            Assert.That(_session.Magic.Cast(new SpellCastRequest(player,
                PhaseOneSpellCatalog.StrengthOfEarth, player)).Succeeded, Is.True);
            _session.Vitality.ApplyHitPointDamage(player, 8);
            _session.Vitality.ApplyFatigueDamage(player, 9);
            int hp = _session.Vitality.GetCurrentHitPoints(player);
            int fatigue = _session.Vitality.GetCurrentFatigue(player);

            SleepResult result = _session.GameplayHud.Sleep(SleepOption.OneHour);

            Assert.That((result.Succeeded, result.Mode, result.HoursAdvanced),
                Is.EqualTo((true, SleepLocationMode.TownWait, 1)));
            Assert.That((_session.SourceTime.ElapsedMilliseconds,
                    _session.Vitality.GetCurrentHitPoints(player), _session.Vitality.GetCurrentFatigue(player)),
                Is.EqualTo((3_600_000L, hp, fatigue)));
            Assert.That(_session.Magic.ActiveEffects, Is.Empty);
        }

        [Test]
        public void TownWaitRejectsUntilHealedBeforeTimeOrVitalityMutation()
        {
            _session.BindSleepLocationSource(new FixedSleepLocationSource(SleepLocationMode.TownWait));
            ArcanumObjectId player = _session.PlayerState.Identity;
            _session.Vitality.ApplyHitPointDamage(player, 5);
            int hp = _session.Vitality.GetCurrentHitPoints(player);

            SleepResult result = _session.GameplayHud.Sleep(SleepOption.UntilHealed);

            Assert.That(result.Failure, Is.EqualTo(SleepFailure.InvalidOption));
            Assert.That((_session.SourceTime.ElapsedMilliseconds,
                    _session.Vitality.GetCurrentHitPoints(player)), Is.EqualTo((0L, hp)));
        }

        [Test]
        public void TownMapWaitableFlagUsesExactSourceParsing()
        {
            SleepLocationSource source = new(MesReader.Read(
                "{3}{Tarant [w: 1]}\n{4}{Black Root [W:0]}\n{5}{Stillwater}\n"),
                _ => false, _ => null);

            Assert.That(source.ResolveTownMap(0), Is.EqualTo(SleepLocationMode.WildernessSleep));
            Assert.That(source.ResolveTownMap(3), Is.EqualTo(SleepLocationMode.TownWait));
            Assert.That(source.ResolveTownMap(4), Is.EqualTo(SleepLocationMode.Unavailable));
            Assert.That(source.ResolveTownMap(5), Is.EqualTo(SleepLocationMode.Unavailable));
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

        [TestCase("Character", 1005)]
        [TestCase("Logbook", 1004)]
        [TestCase("Map", 1007)]
        [TestCase("Inventory", 1006)]
        [TestCase("Fate", 1008)]
        [TestCase("Sleep", 1009)]
        [TestCase("Combat", 1002)]
        [TestCase("Skills", 1001)]
        [TestCase("Spells", 1000)]
        [TestCase("Schematics", 1003)]
        public void PermanentHudControlHoverResolvesOwnedSourceFeedbackWithoutGameplayMutation(
            string controlName, int messageKey)
        {
            Assert.That(_retail.TryReadMessage("mes/intgame.mes", messageKey, out string sourceText), Is.True);
            GameUiScreen screen = _controller.Screen;
            GameUiHudPanel panel = _controller.HudPanel;
            bool combat = _controller.ProjectHud().CombatActive;
            long time = _session.SourceTime.ElapsedMilliseconds;
            SourceHoverHintTarget hint = Descendant(controlName).GetComponent<SourceHoverHintTarget>();

            Assert.That(hint, Is.Not.Null);
            hint.OnPointerEnter(null);
            _view.Synchronize(true);

            Assert.That(_controller.ContextualHelpKey, Is.EqualTo($"mes/intgame.mes:{messageKey}"));
            Assert.That(_controller.ContextualHelp, Is.EqualTo(sourceText));
            Assert.That(_controller.ContextualHelpTitle, Is.Empty);
            Assert.That(_view.ContextualHelpText, Is.EqualTo(sourceText));
            Assert.That((_controller.Screen, _controller.HudPanel, _controller.ProjectHud().CombatActive,
                    _session.SourceTime.ElapsedMilliseconds),
                Is.EqualTo((screen, panel, combat, time)));

            hint.OnPointerExit(null);
            _view.Synchronize(true);
            Assert.That((_controller.ContextualHelpKey, _controller.ContextualHelp),
                Is.EqualTo((string.Empty, string.Empty)));
        }

        [TestCase("Recent Action 2", 6, 26)]
        [TestCase("Recent Action 1", 5, 25)]
        public void PermanentBottomLeftSkillHoverUsesMorphTitleAndFlareBodyFromRetailMes(
            string controlName, int titleKey, int bodyKey)
        {
            Assert.That(_retail.TryReadMessage("mes/skill.mes", titleKey, out string title), Is.True);
            Assert.That(_retail.TryReadMessage("mes/skill.mes", bodyKey, out string body), Is.True);
            SourceHoverHintTarget hint = Descendant(controlName).GetComponent<SourceHoverHintTarget>();

            hint.OnPointerEnter(null);
            _view.Synchronize(true);

            Assert.That(_controller.ContextualHelpKey, Is.EqualTo($"mes/skill.mes:{titleKey}+{bodyKey}"));
            Assert.That((_view.ContextualHelpTitle, _view.ContextualHelpText), Is.EqualTo((title, body)));
            SourceUiBitmapText renderedTitle = Descendant("Contextual Help Title")
                .GetComponent<SourceUiBitmapText>();
            SourceUiBitmapText renderedBody = Descendant("Contextual Help").GetComponent<SourceUiBitmapText>();
            Assert.That(renderedTitle.FontSourceId,
                Is.EqualTo(RetailGameplayHudLayout.ContextualHelpTitleFontSourceId));
            Assert.That(renderedBody.FontSourceId,
                Is.EqualTo(RetailGameplayHudLayout.ContextualHelpFontSourceId));
            Assert.That(renderedTitle.gameObject.activeSelf, Is.True);
            Assert.That(renderedBody.gameObject.activeSelf, Is.True);
            Assert.That(renderedBody.GetComponentsInChildren<Image>(true).First().color, Is.EqualTo(Color.white));

            hint.OnPointerExit(null);
            _view.Synchronize(true);
            Assert.That(_view.IsContextualHelpVisible, Is.False);
        }

        [Test]
        public void DirectHudHoverReplacementUsesOwnerKeyAndStaleExitCannotClearNewHelp()
        {
            SourceHoverHintTarget character = Descendant("Character").GetComponent<SourceHoverHintTarget>();
            SourceHoverHintTarget logbook = Descendant("Logbook").GetComponent<SourceHoverHintTarget>();
            character.OnPointerEnter(null);
            logbook.OnPointerEnter(null);
            character.OnPointerExit(null);

            Assert.That(_controller.ContextualHelpKey, Is.EqualTo("mes/intgame.mes:1004"));
            Assert.That(_retail.TryReadMessage("mes/intgame.mes", 1004, out string logbookText), Is.True);
            Assert.That(_controller.ContextualHelp, Is.EqualTo(logbookText));

            logbook.OnPointerExit(null);
            Assert.That(_controller.ContextualHelp, Is.Empty);
        }

        [Test]
        public void HudContextualHelpUsesSourceIconFontsColorsAndTextRectangles()
        {
            Assert.That(RetailGameplayHudLayout.WorldTargetIcon, Is.EqualTo(new Rect(217, 510, 64, 64)));
            Assert.That(RetailGameplayHudLayout.ContextualHelpText, Is.EqualTo(new Rect(290, 504, 291, 78)));
            Assert.That(RetailGameplayHudLayout.ContextualHelpTitle, Is.EqualTo(new Rect(290, 504, 291, 19)));
            Assert.That(RetailGameplayHudLayout.ContextualHelpBody, Is.EqualTo(new Rect(290, 528, 291, 54)));
            Assert.That(RetailGameplayHudLayout.ContextualHelpIconSourceId, Is.EqualTo(444));
            Assert.That(RetailGameplayHudLayout.ContextualHelpTitleFontSourceId, Is.EqualTo(27));
            Assert.That(RetailGameplayHudLayout.ContextualHelpFontSourceId, Is.EqualTo(229));

            SourceHoverHintTarget character = Descendant("Character").GetComponent<SourceHoverHintTarget>();
            character.OnPointerEnter(null);
            _view.Synchronize(true);
            SourceUiImage icon = Descendant("World Target Icon").GetComponent<SourceUiImage>();
            SourceUiBitmapText text = Descendant("Contextual Help").GetComponent<SourceUiBitmapText>();
            Assert.That(icon.Key.SourceId, Is.EqualTo(444));
            Assert.That(((RectTransform)text.transform).anchoredPosition,
                Is.EqualTo(new Vector2(290f, -(504f - 441f))));
            Assert.That(text.GetComponentsInChildren<Image>(true).First().color, Is.EqualTo(Color.white));
        }

        [Test]
        public void OpeningClockAndLogbookUseAuthoritative1885CalendarAndClockHelpSnapshotsOnEnter()
        {
            Assert.That(SourceCalendar.FormatHud(SourceTimeService.InitialMilliseconds + 37_000L),
                Is.EqualTo("Current Time: 15:00:37   Current Date: 1/1/1885"));
            Assert.That(SourceCalendar.FormatLogbook(
                    SourceCalendar.Timestamp(SourceTimeService.InitialMilliseconds)),
                Is.EqualTo("Jan 1, 1885  3:00pm"));

            _view.Synchronize(true);
            SourceHoverHintTarget clock = _root.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.gameObject.name == "Clock Clip");
            clock.OnPointerEnter(null);
            _view.Synchronize(true);
            string snapshot = _controller.ContextualHelp;
            Assert.That(_controller.ContextualHelpKey, Is.EqualTo("mes/intgame.mes:clock"));
            Assert.That(snapshot, Is.EqualTo("Current Time: 15:00:00   Current Date: 1/1/1885"));
            _session.SourceTime.Advance(37_000);
            _view.Synchronize(true);
            Assert.That(_controller.ContextualHelp, Is.EqualTo(snapshot),
                "the source freezes only the displayed hover snapshot");
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.EqualTo(37_000));
            clock.OnPointerExit(null);
            _view.Synchronize(true);
            Assert.That(_controller.ContextualHelp, Is.Empty);
            clock.OnPointerEnter(null);
            Assert.That(_controller.ContextualHelp,
                Is.EqualTo("Current Time: 15:00:37   Current Date: 1/1/1885"));
        }

        [Test]
        public void HudAuthorityRoundTripsInOptionalSaveV1Domain()
        {
            _session.GameplayHud.GrantFatePoint();
            _session.GameplayHud.GrantFatePoint();
            Assert.That(_session.GameplayHud.ActivateFate(FateChoice.CriticalHit).Succeeded, Is.True);
            _session.GameplayHud.Notify(HudPrimaryNotification.Logbook);
            _session.GameplayHud.RecordRecentAction(new QuickSlotBinding(QuickSlotKind.Spell, default,
                PhaseOneSpellCatalog.StrengthOfEarth));
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"gameplayHud\""));
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.GameplayHud.FatePoints, Is.EqualTo(1));
            Assert.That(_session.GameplayHud.IsFateActive(FateChoice.CriticalHit), Is.True);
            Assert.That(_session.GameplayHud.HasNotification(HudPrimaryNotification.Logbook), Is.True);
            Assert.That(_session.GameplayHud.RecentActions[0].SourceId,
                Is.EqualTo(PhaseOneSpellCatalog.StrengthOfEarth));
            Assert.That(_session.SourceTime.ElapsedMilliseconds, Is.Zero);
        }

        private sealed class FixedSleepLocationSource : ISleepLocationSource
        {
            private readonly SleepLocationMode _mode;
            public FixedSleepLocationSource(SleepLocationMode mode) => _mode = mode;
            public SleepLocationMode Resolve(string selectedSector) => _mode;
        }

        [TestCase("Character", GameUiScreen.Character)]
        [TestCase("Logbook", GameUiScreen.Journal)]
        [TestCase("Map", GameUiScreen.Map)]
        [TestCase("Spells", GameUiScreen.Magic)]
        [TestCase("Schematics", GameUiScreen.Crafting)]
        public void SourceControlsReuseExistingScreenRoutes(string buttonName, GameUiScreen expected)
        {
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == buttonName);
            button.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(expected));
        }

        [Test]
        public void SkillsSourceControlUsesRotatingHudPanelWithoutModalAuthority()
        {
            Button button = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Skills");
            button.onClick.Invoke();
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.None));
            Assert.That(_controller.HudPanel, Is.EqualTo(GameUiHudPanel.Skills));
        }

        [Test]
        public void TwoFrameSkillActionFamilyUsesHoverFrameForPressedPresentation()
        {
            Button skills = _root.GetComponentsInChildren<Button>(true).Single(value => value.name == "Skills");
            skills.onClick.Invoke();
            SourceUiButton[] actions = _root.GetComponentsInChildren<SourceUiButton>(true)
                .Where(value => value.name.StartsWith("Source Skill Action", StringComparison.Ordinal))
                .OrderBy(value => value.name).ToArray();
            Assert.That(actions, Has.Length.EqualTo(4));
            Assert.That(actions.Select(value => value.CurrentPresentationKey.SourceId),
                Is.EqualTo(new[] { 279, 280, 282, 278 }));
            foreach (SourceUiButton action in actions)
            {
                action.OnPointerEnter(null);
                Assert.That(action.CurrentPresentationKey.Frame, Is.EqualTo(1), action.name);
                action.OnPointerDown(null);
                Assert.That(action.CurrentPresentationKey.Frame, Is.EqualTo(1), action.name);
                action.OnPointerUp(null);
                action.OnPointerExit(null);
                Assert.That(action.CurrentPresentationKey.Frame, Is.Zero, action.name);
            }
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

        [TestCase(600, 100f, 3f)]
        [TestCase(1080, 100f, 5.4f)]
        public void CameraWorldScalePreservesOneSourcePixelPerScreenPixel(
            int height, float pixelsPerUnit, float expectedSize)
        {
            float size = RetailGameplayHudLayout.GameplayCameraOrthographicSize(height, pixelsPerUnit);
            Assert.That(size, Is.EqualTo(expectedSize).Within(.0001f));
            Assert.That(height / (2f * size), Is.EqualTo(pixelsPerUnit).Within(.0001f));
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

        private Transform Descendant(string name)
            => _root.GetComponentsInChildren<Transform>(true).Single(value => value.name == name);

        private ArcanumObjectId AddFollower(string identity, int prototype, string name)
        {
            ArcanumObjectId follower = Parse(identity);
            var proto = new ObjectProtoInfo(prototype, ObjectType.Npc, 0x28100000u,
                description: prototype) { Portrait = 1000 };
            _session.BindPrototypeSource(number => number == prototype ? proto : null);
            _session.BindDescriptionSource(id => id == prototype ? name : null);
            var source = new ObjectInstance(ObjectType.Npc, prototype, 0L,
                0x28100000u, 0, 0, oid: Bytes(follower));
            _session.GetOrCreate(source, follower, Sector, source.CurrentArtId.Value, false, false);
            int[] stats = Enumerable.Repeat(8, CharacterAttributeSet.SourceStatArrayCount).ToArray();
            stats[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            stats[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            _session.Characters.GetOrCreateSourceCharacter(follower, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(follower, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(follower, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, 0, 0));
            _session.Vitality.GetOrCreateSourceCharacter(follower, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 15, null, 0,
                    null, 0, null, 0, null, 0));
            Assert.That(_session.Party.Join(follower).Succeeded, Is.True);
            return follower;
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
