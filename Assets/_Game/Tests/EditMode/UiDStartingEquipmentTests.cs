using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("UIDCharacterCreation")]
    [Category("M12BCharacterCreationNewGame")]
    public sealed class UiDStartingEquipmentTests
    {
        private static RetailUiAssetResolver _retail;
        private static readonly ArcanumObjectId Merchant = ArcanumObjectId.CreateAuthored(0x7100u);
        private const int CheapArmorPrototype = 6021;
        private const int ExpensivePrototype = 6022;

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private CharacterCreationCatalog _catalog;
        private GameUiController _controller;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;

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
            _root = new GameObject(nameof(UiDStartingEquipmentTests));
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UID-Shop-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _catalog = CreateCatalog();
            _session.RegisterObjectOwner(new ShopOwner(_session, _catalog.ShoppingSector));
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);
            _session.BindPrototypeSource(ResolvePrototype);
            _session.BindDescriptionSource(id => id switch
            {
                28001 => "Human Shopkeeper",
                CheapArmorPrototype => "Leather Armor",
                ExpensivePrototype => "Expensive Rations",
                _ => null,
            });
            _session.BindEconomySource(InventorySourceCatalog.FromMes(
                Mes((1, "Human Shopkeeper: 0 0 100 6001 100 6002"),
                    (108, "Merwin: 0 0 100 8137")),
                Mes((1, "ALL"), (108, "ALL"))));
            _session.BindCharacterCreationSource(_catalog);
            _controller = new GameUiController(_session);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            _skin?.Dispose();
            _enhanced?.Dispose();
            OpenArcanumGraphicsSettings.ClearRuntimeMode();
            if (Directory.Exists(_fixtureRoot)) Directory.Delete(_fixtureRoot, true);
        }

        [Test]
        public void SourceShopkeeperPresentationUsesAuthenticPanelsGeometryCountersAndNavigationHelp()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();

            foreach (int id in RetailStartingEquipmentLayout.RequiredSourceIds)
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.WindowTransform.sizeDelta, Is.EqualTo(new Vector2(800f, 400f)));
            RectTransform barterBackground = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Shopkeeper Center Region Background");
            Assert.That(barterBackground.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(barterBackground.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(barterBackground.pivot, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(barterBackground.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(barterBackground.sizeDelta, Is.EqualTo(new Vector2(360f, 400f)));
            Assert.That(view.GetComponentsInChildren<ScrollRect>(true), Is.Empty);
            Assert.That(view.DynamicItemCount, Is.EqualTo(4),
                "merchant=" + string.Join(",", _controller.ProjectMerchantWares()
                    .Select(value => $"{value.Item.Type}:{value.Item.PrototypeNumber}"))
                + "; player=" + string.Join(",", _controller.ProjectPlayerSaleItems()
                    .Select(value => $"{value.Item.Type}:{value.Item.PrototypeNumber}")));
            Assert.That(Text(view, "Human Shopkeeper Name").Text, Is.EqualTo("HUMAN SHOPKEEPER"));
            Assert.That(Text(view, "Counter 0").Text, Is.EqualTo("400"));
            for (int index = 1; index < 5; index++)
                Assert.That(Text(view, $"Counter {index}").Text, Is.EqualTo("0"));
            for (int index = 0; index < 5; index++)
            {
                SourceUiBitmapText counter = Text(view, $"Counter {index}");
                Rect bounds = RetailStartingEquipmentLayout.CounterRects[index];
                Assert.That(((RectTransform)counter.transform).anchoredPosition.x,
                    Is.EqualTo(bounds.x + (bounds.width - ((RectTransform)counter.transform).sizeDelta.x) * .5f));
            }
            Assert.That(RetailStartingEquipmentLayout.MessageIcon, Is.EqualTo(new Rect(217, 69, 64, 64)));
            Assert.That(RetailStartingEquipmentLayout.MessageTitle, Is.EqualTo(new Rect(290, 63, 291, 19)));
            Assert.That(RetailStartingEquipmentLayout.MessageBody, Is.EqualTo(new Rect(290, 87, 291, 54)));

            RectTransform merchantItems = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Merchant Inventory Items");
            RectTransform playerItems = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Player Inventory Items");
            Assert.That(merchantItems.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(playerItems.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(merchantItems.sizeDelta, Is.EqualTo(RetailStartingEquipmentLayout.MerchantGrid.size));
            Assert.That(playerItems.sizeDelta, Is.EqualTo(RetailStartingEquipmentLayout.PlayerGrid.size));
            foreach (RectTransform item in merchantItems.Cast<Transform>().Cast<RectTransform>())
                AssertContainedBy(item, merchantItems, "merchant stock");
            foreach (RectTransform item in playerItems.Cast<Transform>().Cast<RectTransform>())
                AssertContainedBy(item, playerItems, "player inventory");

            AssertTopLeft(view, "Merchant Paper Doll", RetailStartingEquipmentLayout.MerchantPaperDollToggle.position);
            AssertTopLeft(view, "Merchant Inventory", RetailStartingEquipmentLayout.MerchantInventoryToggle.position);
            AssertTopLeft(view, "Player Paper Doll", RetailStartingEquipmentLayout.PlayerPaperDollToggle.position);
            AssertTopLeft(view, "Player Inventory", RetailStartingEquipmentLayout.PlayerInventoryToggle.position);
            AssertTopLeft(view, "Arrange Items", RetailStartingEquipmentLayout.ArrangeButton.position);
            Assert.That(RetailStartingEquipmentLayout.GambleBox, Is.EqualTo(new Rect(703, 236, 89, 54)));
            Assert.That(RetailStartingEquipmentLayout.UseBox, Is.EqualTo(new Rect(703, 290, 89, 54)));
            Assert.That(RetailStartingEquipmentLayout.DropBox, Is.EqualTo(new Rect(703, 344, 89, 54)));
            AssertTopLeft(view, "Gamble Drop Box", RetailStartingEquipmentLayout.GambleBox.position);
            AssertTopLeft(view, "Use Drop Box", RetailStartingEquipmentLayout.UseBox.position);
            AssertTopLeft(view, "Drop Drop Box", RetailStartingEquipmentLayout.DropBox.position);
            Image merchantPortrait = view.GetComponentsInChildren<Image>(true)
                .Single(value => value.name == "Human Shopkeeper Portrait");
            Assert.That(merchantPortrait.sprite, Is.Not.Null);
            Assert.That(((RectTransform)merchantPortrait.transform).sizeDelta,
                Is.EqualTo(RetailStartingEquipmentLayout.MerchantPortrait.size));
            Assert.That(SourceButton(view, "Merchant Inventory").CurrentPresentationKey.Frame, Is.Zero,
                "source toggle ARTs latch without an alternate bitmap frame");
            Invoke(view, "Merchant Paper Doll");
            Assert.That(view.transform.Find("Source-Faithful Starting Equipment/Shopkeeper Center Region/"
                                           + "Merchant Paper Doll Panel").gameObject.activeSelf, Is.True);
            Assert.That(view.DynamicItemCount, Is.EqualTo(2),
                "the merchant paper doll replaces merchant stock while the player's source clothing and Gold remain visible");
            Invoke(view, "Merchant Inventory");
            Assert.That(view.DynamicItemCount, Is.EqualTo(4));

            Hover(view, "Shopkeeper Continue", true);
            Assert.That(view.MessageTitleText, Is.EqualTo("Action:"));
            Assert.That(Wrapped(view, "Shopkeeper Message Body"), Is.EqualTo("Next Screen"));
            Hover(view, "Shopkeeper Continue", false);
            Assert.That(view.MessageTitleText, Is.Empty);
            Hover(view, "Shopkeeper Back", true);
            Assert.That(view.MessageTitleText, Is.EqualTo("Action:"));
            Assert.That(Wrapped(view, "Shopkeeper Message Body"), Is.EqualTo("Previous Screen"));
        }

        [Test]
        public void SourceShopkeeperItemsExposeQuotesAndButtonsSubmitBothRejectedAndSuccessfulTrades()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView expensive = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == ExpensivePrototype);
            string expensiveName = $"{expensive.Direction} {expensive.Item.Identity.Key}";

            Hover(view, expensiveName, true);
            Assert.That(view.MerchantQuoteText, Is.Not.Empty);
            Assert.That(view.MerchantQuoteText, Does.Contain(expensive.UnitPrice.ToString()));
            Assert.That(view.MerchantQuoteText, Does.Not.EndWith("%)"),
                "untrained Haggle must not disclose the merchant's markup");
            Assert.That(Text(view, "Shopkeeper Message Title").Text, Is.EqualTo("Expensive Rations"));
            Invoke(view, expensiveName);
            Assert.That(view.MerchantQuoteText, Is.EqualTo(EconomyFailure.InsufficientFunds.ToString()));
            Assert.That(_session.States[expensive.Item.Identity].ParentIdentity, Is.EqualTo(Merchant));

            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            string armorName = $"{armor.Direction} {armor.Item.Identity.Key}";
            Invoke(view, armorName);
            GameUiMerchantItemView resale = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Identity == armor.Item.Identity);
            Assert.That(view.MerchantQuoteText, Is.Not.Empty);
            Assert.That(view.MerchantQuoteText, Does.Contain(resale.UnitPrice.ToString()));
            Assert.That(Text(view, "Counter 0").Text, Is.EqualTo((400 - armor.UnitPrice).ToString()));
        }

        [Test]
        public void PhysicalGoldProjectsTheAuthoritativeStackAndTracksTransactions()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView gold = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Type == ObjectType.Gold);
            string goldName = $"{gold.Direction} {gold.Item.Identity.Key}";

            Hover(view, goldName, true);
            Assert.That(view.MessageTitleText, Is.EqualTo("Gold"));
            Assert.That(Wrapped(view, "Shopkeeper Message Body"), Is.EqualTo("Quantity: 400 Weight: 0 stone"));
            RectTransform goldRoot = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == goldName);
            Assert.That(goldRoot.GetComponent<Button>(), Is.Null,
                "Gold is an authoritative special inventory view, not an independently tradable balance");

            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            Invoke(view, $"{armor.Direction} {armor.Item.Identity.Key}");
            int expected = 400 - armor.UnitPrice;
            Assert.That(_controller.PlayerGold, Is.EqualTo(expected));
            Assert.That(Text(view, "Counter 0").Text, Is.EqualTo(expected.ToString()));
            GameUiMerchantItemView changedGold = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Type == ObjectType.Gold);
            Assert.That(changedGold.Item.Quantity, Is.EqualTo(expected));
            Hover(view, $"{changedGold.Direction} {changedGold.Item.Identity.Key}", true);
            Assert.That(Wrapped(view, "Shopkeeper Message Body"),
                Is.EqualTo($"Quantity: {expected} Weight: 0 stone"));
        }

        [Test]
        public void HaggleApprenticeAppendsSourceMarkupWithoutChangingBuyOrSellPrices()
        {
            FinalizeCustomCharacter();
            SetHaggleTraining(SkillTrainingLevel.None);
            GameUiMerchantItemView ordinaryBuy = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            GameUiMerchantItemView ordinarySell = _controller.ProjectPlayerSaleItems()
                .First(value => value.Item.Type != ObjectType.Gold);
            EconomyPriceBreakdown price = _session.Economy.GetPrice(ordinaryBuy.Direction, Merchant,
                ProductionPlayerLifecycle.DefaultPlayerIdentity, ordinaryBuy.Item.Identity);
            int markup = 100 * (ordinaryBuy.UnitPrice - price.EffectiveWorth) / price.EffectiveWorth;
            if (markup == 0) markup = 1;
            string itemName = $"{ordinaryBuy.Direction} {ordinaryBuy.Item.Identity.Key}";

            SetHaggleTraining(SkillTrainingLevel.Apprentice);
            GameUiMerchantItemView apprenticeBuy = _controller.ProjectMerchantWares()
                .Single(value => value.Item.Identity == ordinaryBuy.Item.Identity);
            GameUiMerchantItemView apprenticeSell = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Identity == ordinarySell.Item.Identity);
            Assert.That((apprenticeBuy.UnitPrice, apprenticeSell.UnitPrice),
                Is.EqualTo((ordinaryBuy.UnitPrice, ordinarySell.UnitPrice)),
                "training disclosure must not change either established price path");
            RetailStartingEquipmentView view = BuildView();

            Hover(view, itemName, true);
            Assert.That(view.MerchantQuoteText, Does.EndWith($"({markup}%)"));
        }

        [Test]
        public void EditorCompletionFinalizesOnceAndRoutesSamePlayerToAuthenticShoppingMap()
        {
            BeginCustomCharacter();

            Assert.That(_controller.FinalizeNewGame(), Is.True, _controller.Feedback);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.StartingEquipment));
            Assert.That(_session.CharacterCreation.StartingEquipmentActive, Is.True);
            Assert.That(_session.CharacterCreation.StartingEquipmentMerchant, Is.EqualTo(Merchant));
            Assert.That(_session.SelectedSector, Is.EqualTo(_catalog.ShoppingSector));
            Assert.That(_session.PlayerState.Sector, Is.EqualTo(_catalog.ShoppingSector));
            Assert.That(_session.PlayerState.Identity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(_session.GetGold(_session.PlayerState.Identity), Is.EqualTo(400));
            Assert.That(_controller.ProjectMerchantWares().Select(value => value.Item.PrototypeNumber),
                Is.EquivalentTo(new[] { CheapArmorPrototype, ExpensivePrototype }));
        }

        [Test]
        public void MerchantGridUsesSourceScrollbarAndUnclampedRetainedRows()
        {
            Assert.That(RetailStartingEquipmentLayout.MerchantScrollbar,
                Is.EqualTo(new Rect(330, 168, 17, 224)));
            Rect rowNine = RetailStartingEquipmentLayout.ItemRect(
                RetailStartingEquipmentLayout.MerchantGrid, 90, InventoryFootprint.OneCell, 2f);
            Assert.That(rowNine, Is.EqualTo(new Rect(8, 392, 32, 32)));
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            Assert.That(view.GetComponentsInChildren<Scrollbar>(true)
                .Single(value => value.name == "Merchant Inventory Scrollbar"), Is.Not.Null);
        }

        [Test]
        public void PurchaseUsesEconomyPriceAndMutatesAuthoritativeGoldAndInventory()
        {
            FinalizeCustomCharacter();
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            int before = _controller.PlayerGold;

            Assert.That(armor.Failure, Is.EqualTo(EconomyFailure.None));
            Assert.That(_controller.Trade(armor.Direction, armor.Item.Identity), Is.True, _controller.Feedback);
            Assert.That(_controller.PlayerGold, Is.EqualTo(before - armor.UnitPrice));
            Assert.That(_session.TryGetObjectState(armor.Item.Identity, out PersistentObjectState purchased), Is.True);
            Assert.That(purchased.ParentIdentity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(_controller.ProjectPlayerSaleItems().Any(value => value.Item.Identity == armor.Item.Identity),
                Is.True);
        }

        [Test]
        public void InsufficientFundsRejectsWithoutMovingItemOrGold()
        {
            FinalizeCustomCharacter();
            GameUiMerchantItemView item = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == ExpensivePrototype);
            int before = _controller.PlayerGold;

            Assert.That(item.Failure, Is.EqualTo(EconomyFailure.InsufficientFunds));
            Assert.That(_controller.Trade(item.Direction, item.Item.Identity), Is.False);
            Assert.That(_controller.Feedback, Is.EqualTo(EconomyFailure.InsufficientFunds.ToString()));
            Assert.That(_controller.PlayerGold, Is.EqualTo(before));
            Assert.That(_session.States[item.Item.Identity].ParentIdentity, Is.EqualTo(Merchant));
        }

        [Test]
        public void CompletionMovesSamePlayerAndPurchasedEquipmentToStartMapThenSaveLoadPreservesIt()
        {
            FinalizeCustomCharacter("Ada");
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            int price = armor.UnitPrice;
            Assert.That(_controller.Trade(armor.Direction, armor.Item.Identity), Is.True);
            Assert.That(_controller.Equip(armor.Item.Identity), Is.True);

            Assert.That(_controller.CompleteStartingEquipment(), Is.True, _controller.Feedback);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.None));
            Assert.That(_session.CharacterCreation.StartingEquipmentActive, Is.False);
            Assert.That(_session.SelectedSector, Is.EqualTo(_catalog.StartSector));
            Assert.That(_session.PlayerState.Sector, Is.EqualTo(_catalog.StartSector));
            Assert.That(_session.PlayerState.TilePosition, Is.EqualTo(_catalog.StartTile));
            Assert.That(_session.States[armor.Item.Identity].Placement.Kind,
                Is.EqualTo(ObjectPlacementKind.Equipped));
            Assert.That(_session.States[armor.Item.Identity].Placement.WornLocation,
                Is.EqualTo(WornLocation.Armor));
            Assert.That(_session.GetGold(ProductionPlayerLifecycle.DefaultPlayerIdentity), Is.EqualTo(400 - price));

            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.CharacterCreation.Finalized.Name, Is.EqualTo("Ada"));
            Assert.That(_session.CharacterCreation.Finalized.Background.Id, Is.Zero);
            Assert.That(_session.SelectedSector, Is.EqualTo(_catalog.StartSector));
            Assert.That(_session.GetGold(ProductionPlayerLifecycle.DefaultPlayerIdentity), Is.EqualTo(400 - price));
            Assert.That(_session.States[armor.Item.Identity].Placement.Kind,
                Is.EqualTo(ObjectPlacementKind.Equipped));
        }

        [Test]
        public void BackCancelsFinalizationAndAllowsEditorToFinalizeOneFreshShopSession()
        {
            FinalizeCustomCharacter();
            Assert.That(_controller.ReturnToCharacterEditor(), Is.True, _controller.Feedback);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.CharacterCreation));
            Assert.That(_session.PlayerState, Is.Null);
            Assert.That(_session.CharacterCreation.IsFinalized, Is.False);

            Assert.That(_controller.FinalizeNewGame(), Is.True, _controller.Feedback);
            Assert.That(_controller.ProjectMerchantWares().Count, Is.EqualTo(2));
            Assert.That(_controller.FinalizeNewGame(), Is.False);
            Assert.That(_controller.ProjectMerchantWares().Count, Is.EqualTo(2),
                "a rejected duplicate finalization must not restock or duplicate source inventory");
        }

        [Test]
        public void PregeneratedCharacterBypassesShopAndUsesAuthoredStartingEquipment()
        {
            Assert.That(_controller.BeginPregeneratedCharacter(), Is.True);
            Assert.That(_controller.FinalizePregeneratedCharacter(), Is.True, _controller.Feedback);

            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.None));
            Assert.That(_session.CharacterCreation.StartingEquipmentActive, Is.False);
            Assert.That(_session.SelectedSector, Is.EqualTo(_catalog.StartSector));
            PersistentObjectState armor = _session.States.Values.Single(value => value.PrototypeNumber == 8157);
            Assert.That(armor.ParentIdentity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(armor.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Equipped));
        }

        private void BeginCustomCharacter(string name = "Arbuckle")
        {
            Assert.That(_controller.BeginNewGame(), Is.True, _controller.Feedback);
            _controller.SetCreationName(name);
            _controller.SetCreationBackground(0);
            _controller.SetCreationPortrait(1005);
        }

        private void FinalizeCustomCharacter(string name = "Arbuckle")
        {
            BeginCustomCharacter(name);
            Assert.That(_controller.FinalizeNewGame(), Is.True, _controller.Feedback);
        }

        private void SetHaggleTraining(SkillTrainingLevel training)
        {
            int[] points = new int[CharacterSkillRules.SkillCount];
            var levels = new SkillTrainingLevel[CharacterSkillRules.SkillCount];
            points[(int)CharacterSkill.Haggle] = 5;
            levels[(int)CharacterSkill.Haggle] = training;
            _session.Progression.Get(ProductionPlayerLifecycle.DefaultPlayerIdentity).RestoreSkills(points, levels);
        }

        private RetailStartingEquipmentView BuildView()
        {
            RetailStartingEquipmentView view = _root.AddComponent<RetailStartingEquipmentView>();
            view.Bind(_controller, _session, _skin);
            view.Synchronize();
            return view;
        }

        private static void Invoke(RetailStartingEquipmentView view, string name)
        {
            Button button = view.GetComponentsInChildren<Button>(true).Single(value => value.name == name);
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
            view.Synchronize();
        }

        private static void Hover(RetailStartingEquipmentView view, string name, bool entered)
        {
            SourceHoverHintTarget hint = view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == name);
            if (entered) hint.OnPointerEnter(null); else hint.OnPointerExit(null);
        }

        private static SourceUiBitmapText Text(RetailStartingEquipmentView view, string name)
            => view.GetComponentsInChildren<SourceUiBitmapText>(true).Single(value => value.name == name);

        private static SourceUiButton SourceButton(RetailStartingEquipmentView view, string name)
            => view.GetComponentsInChildren<SourceUiButton>(true).Single(value => value.name == name);

        private static void AssertTopLeft(RetailStartingEquipmentView view, string name, Vector2 expected)
        {
            RectTransform rect = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == name);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(expected.x, -expected.y)));
        }

        private static void AssertContainedBy(RectTransform item, RectTransform grid, string description)
        {
            float left = item.anchoredPosition.x;
            float top = -item.anchoredPosition.y;
            Assert.That(left, Is.GreaterThanOrEqualTo(0f), description + " left");
            Assert.That(top, Is.GreaterThanOrEqualTo(0f), description + " top");
            Assert.That(left + item.sizeDelta.x, Is.LessThanOrEqualTo(grid.sizeDelta.x),
                description + " right");
            Assert.That(top + item.sizeDelta.y, Is.LessThanOrEqualTo(grid.sizeDelta.y),
                description + " bottom");
        }

        private static string Wrapped(RetailStartingEquipmentView view, string name)
        {
            RectTransform body = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == name);
            return string.Join(" ", body.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Select(value => value.Text));
        }

        private static ObjectProtoInfo ResolvePrototype(int number)
        {
            if (number == 9056)
                return new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3)
                    { GoldQuantity = 1 };
            if (number == CheapArmorPrototype)
                return new ObjectProtoInfo(number, ObjectType.Armor, 0x70000000u,
                    description: number, invAid: 1, weight: 60, worth: 5)
                    { HpPoints = 50 };
            if (number == ExpensivePrototype)
                return new ObjectProtoInfo(number, ObjectType.Food, 0x70000001u,
                    description: number, invAid: 2, weight: 1, worth: 1000);
            if (number == 8157)
                return new ObjectProtoInfo(8157, ObjectType.Armor, 0x70000002u, invAid: 3, worth: 20);
            if (number is 8049 or 8065 or 8069 or 8133 or 8134 or 8135)
                return new ObjectProtoInfo(number, ObjectType.Armor, 0x70000003u,
                    description: number, invAid: (uint)number, worth: 20);
            if (number is 15178 or 15179)
                return new ObjectProtoInfo(number, ObjectType.Generic, 0x70000004u,
                    description: number, invAid: (uint)number, worth: 1);
            if (number == 28001)
                return new ObjectProtoInfo(28001, ObjectType.Npc, 0x28100000u, description: 28001);
            return number == 16067 ? PregeneratedPrototype() : null;
        }

        private static CharacterCreationCatalog CreateCatalog()
        {
            MesFile rules = Mes((0, "1000"), (1, "86"), (2, ""), (3, "400"), (4, ""),
                (390, "1039"), (391, "86"), (392, "HEM"), (393, "0"), (394, ""));
            MesFile text = Mes((1000, "No significant background\n\nNothing happened."),
                (1039, "Merwin's Background\n\nRetail pregenerated source."));
            MesFile portraits = Mes((1005, "HUM1"), (1017, "HAM2"));
            MapList maps = MapList.Read(Mes(
                (5000, "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0"),
                (5001, "shop, 50, 60, Type: SHOPPING_MAP, WorldMap: 0")));
            return CharacterCreationCatalog.FromMes(rules, text, Mes((86, "")), portraits, maps,
                Mes((76, "Merwin Tumblebrook")), Mes((551, "123")));
        }

        private static ObjectProtoInfo PregeneratedPrototype()
        {
            ObjectProtoInfo prototype = ObjectProtoInfo.CreateEngineOwnedPc(16067, 0x2D100000u,
                1017, (int)CharacterGender.Male, (int)CharacterRace.Halfling);
            prototype.InventorySource = 108;
            return prototype;
        }

        private static MesFile Mes(params (int key, string value)[] values)
            => new(values.Select(value => new KeyValuePair<int, string>(value.key, value.value)).ToList());

        private static int[] Stats()
        {
            var values = new int[CharacterAttributeSet.SourceStatArrayCount];
            for (int index = 0; index < CharacterAttributeSet.Count; index++) values[index] = 8;
            values[CharacterProgressionSource.LevelSourceSlot] = 1;
            values[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            values[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return values;
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private sealed class ShopOwner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            private readonly string _shoppingSector;

            public ShopOwner(WorldMapSessionCoordinator session, string shoppingSector)
            {
                _session = session;
                _shoppingSector = shoppingSector;
            }

            public string ConfiguredSector => _shoppingSector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;

            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                if (string.Equals(PresentedSector, _shoppingSector, StringComparison.OrdinalIgnoreCase))
                    CreateShopkeeper();
                return true;
            }

            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }

            private void CreateShopkeeper()
            {
                var source = new ObjectInstance(ObjectType.Npc, 28001, Location(2, 1), 0x28100000u, 0, 0);
                _session.GetOrCreate(source, Merchant, PresentedSector, source.CurrentArtId.Value, false, false,
                    nameIndex: 28001, retailPriceMultiplier: 0, inventorySourceId: 1);
                int[] stats = Stats();
                _session.Characters.GetOrCreateSourceCharacter(Merchant, ObjectType.Npc, 28001, stats, null);
                _session.Progression.GetOrCreateSourceCharacter(Merchant, ObjectType.Npc, 28001,
                    CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
                _session.DerivedStats.GetOrCreateSourceCharacter(Merchant, ObjectType.Npc, 28001,
                    CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, 0, 0));
                _session.Vitality.GetOrCreateSourceCharacter(Merchant, ObjectType.Npc, 28001,
                    CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0,
                        null, 0, null, 0, null, 0));
            }
        }
    }
}
