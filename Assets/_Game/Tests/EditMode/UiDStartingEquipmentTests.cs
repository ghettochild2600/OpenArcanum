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
using UnityEngine.EventSystems;
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
            Assert.That(_session.Economy.TryResolveMerchant(Merchant, out ArcanumObjectId inventoryOwner,
                out _), Is.True);
            Assert.That(_controller.ProjectMerchantWares().All(value =>
                    (_session.States[value.Item.Identity].ItemFlags
                     & EconomyStateService.ItemFlagIdentified) != 0), Is.True,
                "source barter identifies the merchant/substitute inventory before drawing it");
            Assert.That(_session.ChildrenOf(inventoryOwner).Select(identity => _session.States[identity])
                    .Where(value => value.Type != ObjectType.Gold)
                    .Select(value => value.InventoryLocation).OrderBy(value => value), Is.EqualTo(new[] { 0, 1 }),
                "source barter horizontally arranges the non-PC inventory on open");
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
            Assert.That(RetailStartingEquipmentLayout.PlayerWeight, Is.EqualTo(new Rect(428f, 32f, 185f, 15f)));
            Assert.That(RetailStartingEquipmentLayout.PlayerEncumbrance, Is.EqualTo(new Rect(428f, 47f, 185f, 15f)));
            Assert.That(RetailStartingEquipmentLayout.PlayerSpeed, Is.EqualTo(new Rect(428f, 62f, 185f, 15f)));
            Assert.That(RetailStartingEquipmentLayout.PlayerAttackIcon,
                Is.EqualTo(new Rect(374f, 318f, 54f, 71f)));
            Assert.That(RetailStartingEquipmentLayout.PlayerDefenseIcon,
                Is.EqualTo(new Rect(599f, 319f, 83f, 71f)));
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
            SourceUiButton arrange = SourceButton(view, "Arrange Items");
            Assert.That(arrange.CurrentPresentationKey.Frame, Is.Zero);
            arrange.OnPointerEnter(null);
            Assert.That(arrange.CurrentPresentationKey.Frame, Is.Zero,
                "source momentary Arrange control remains up while merely hovered");
            arrange.OnPointerDown(null);
            Assert.That(arrange.CurrentPresentationKey.Frame, Is.EqualTo(1),
                "two-frame source ART 340 uses its active frame only while pressed");
            arrange.OnPointerUp(null);
            Assert.That(arrange.CurrentPresentationKey.Frame, Is.Zero,
                "Arrange is momentary and returns to the source up frame on mouse release");
            arrange.OnSelect(null);
            Assert.That(arrange.CurrentPresentationKey.Frame, Is.Zero,
                "Unity event focus must not leave Arrange depressed after a click");
            arrange.OnPointerEnter(null);
            arrange.OnPointerExit(null);
            Assert.That(arrange.CurrentPresentationKey.Frame, Is.Zero);
            Invoke(view, "Merchant Paper Doll");
            Assert.That(view.transform.Find("Source-Faithful Starting Equipment/Shopkeeper Center Region/"
                                           + "Merchant Paper Doll Panel").gameObject.activeSelf, Is.True);
            Assert.That(view.DynamicItemCount, Is.EqualTo(2),
                "the merchant paper doll replaces merchant stock while the player's source clothing and Gold remain visible");
            Invoke(view, "Merchant Inventory");
            Assert.That(view.DynamicItemCount, Is.EqualTo(4));
            Invoke(view, "Player Paper Doll");
            Assert.That(view.transform.Find("Source-Faithful Starting Equipment/Shopkeeper Center Region/"
                                           + "Player Paper Doll Panel").gameObject.activeSelf, Is.True);
            Assert.That(view.GetComponentsInChildren<RectTransform>(true)
                    .Single(value => value.name == "Player Inventory Items").childCount, Is.Zero,
                "switching to the source paper doll must rebuild and remove stale inventory presenters");
            SourceUiImage[] playerSilhouettes = view.GetComponentsInChildren<SourceUiImage>(true)
                .Where(value => value.name.StartsWith("Player Empty ", StringComparison.Ordinal)).ToArray();
            Assert.That(playerSilhouettes, Has.Length.EqualTo(9));
            Assert.That(playerSilhouettes.Select(value => value.Key.SourceId).OrderBy(value => value),
                Is.EqualTo(Enumerable.Range(241, 9)),
                "inven_ui.c draws the nine authored empty-slot silhouettes over ART 341");
            Assert.That(playerSilhouettes.All(value => value.gameObject.activeInHierarchy), Is.True);
            Assert.That(Text(view, "Player Total Weight").FontSourceId, Is.EqualTo(229));
            Assert.That(Text(view, "Player Encumbrance").FontSourceId, Is.EqualTo(229));
            Assert.That(Text(view, "Player Speed").FontSourceId, Is.EqualTo(229));
            Assert.That(Text(view, "Player Total Weight").Text, Does.Contain(":"));
            Assert.That(Text(view, "Player Encumbrance").Text, Does.StartWith("Encumbrance:"));
            Assert.That(Text(view, "Player Speed").Text, Does.StartWith("Speed:"));
            Assert.That(view.DynamicItemCount, Is.EqualTo(2),
                "only the still-visible merchant inventory contributes dynamic presenters");
            Invoke(view, "Player Inventory");
            Assert.That(view.GetComponentsInChildren<RectTransform>(true)
                    .Single(value => value.name == "Player Inventory Items").childCount, Is.EqualTo(2));
            Assert.That(view.DynamicItemCount, Is.EqualTo(4));

            Hover(view, "Shopkeeper Continue", true);
            Assert.That(view.MessageTitleText, Is.EqualTo("Action:"));
            Assert.That(Wrapped(view, "Shopkeeper Message Body"), Is.EqualTo("Next Screen"));
            Hover(view, "Shopkeeper Continue", false);
            Assert.That(view.MessageTitleText, Is.Empty);
            Hover(view, "Shopkeeper Back", true);
            Assert.That(view.MessageTitleText, Is.EqualTo("Action:"));
            Assert.That(Wrapped(view, "Shopkeeper Message Body"), Is.EqualTo("Previous Screen"));

            GameUiMerchantItemView gold = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Type == ObjectType.Gold);
            Hover(view, $"{gold.Direction} {gold.Item.Identity.Key}", true);
            Assert.That(view.GetComponentsInChildren<SourceUiImage>(true)
                    .Single(value => value.name == "Shopkeeper Message Icon").Key.SourceId,
                Is.EqualTo(RetailStartingEquipmentLayout.GoldInspectionIconSourceId));
            Hover(view, "Shopkeeper Continue", true);
            Assert.That(view.GetComponentsInChildren<SourceUiImage>(true)
                    .Single(value => value.name == "Shopkeeper Message Icon").Key.SourceId,
                Is.EqualTo(RetailStartingEquipmentLayout.FeedbackIconSourceId),
                "navigation help owns the source feedback icon instead of retaining a prior Gold inspection icon");
            Assert.That(view.MessageTitleText, Is.EqualTo("Action:"));
            Assert.That(Wrapped(view, "Shopkeeper Message Body"), Is.EqualTo("Next Screen"));
        }

        [Test]
        public void ShopkeeperArrangeAlternatesHorizontalAndVerticalOnEveryPress()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;

            int[] Locations() => _session.ChildrenOf(pc)
                .Select(identity => _session.States[identity])
                .Where(value => value.Placement.Kind == ObjectPlacementKind.Contained)
                .Select(value => value.InventoryLocation)
                .OrderBy(value => value)
                .ToArray();

            Invoke(view, "Arrange Items");
            Assert.That(Locations(), Is.EqualTo(new[] { 0, 1 }), "first press is source horizontal");
            Assert.That(_controller.Feedback, Is.EqualTo("Inventory arranged horizontally."));

            Invoke(view, "Arrange Items");
            Assert.That(Locations(), Is.EqualTo(new[] { 0, 10 }), "second press is source vertical");
            Assert.That(_controller.Feedback, Is.EqualTo("Inventory arranged vertically."));

            Invoke(view, "Arrange Items");
            Assert.That(Locations(), Is.EqualTo(new[] { 0, 1 }), "third press returns to horizontal");
            Invoke(view, "Arrange Items");
            Assert.That(Locations(), Is.EqualTo(new[] { 0, 10 }), "fourth press returns to vertical");
        }

        [Test]
        public void ShopkeeperHoverMessagesReusePreparedBitmapPresenters()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            RectTransform body = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Shopkeeper Message Body");
            SourceUiBitmapText[] prepared = body.GetComponentsInChildren<SourceUiBitmapText>(true);
            Image[][] preparedGlyphs = prepared
                .Select(line => line.GetComponentsInChildren<Image>(true))
                .ToArray();

            Assert.That(prepared.Length, Is.EqualTo(6));
            Assert.That(prepared.All(line => line.GlyphPresenterCount >= 48), Is.True);

            for (int index = 0; index < 10; index++)
            {
                Hover(view, "Shopkeeper Continue", true);
                Hover(view, "Shopkeeper Continue", false);
                Hover(view, "Shopkeeper Back", true);
                Hover(view, "Shopkeeper Back", false);
            }

            SourceUiBitmapText[] after = body.GetComponentsInChildren<SourceUiBitmapText>(true);
            Assert.That(after, Is.EqualTo(prepared));
            for (int index = 0; index < after.Length; index++)
                Assert.That(after[index].GetComponentsInChildren<Image>(true), Is.EqualTo(preparedGlyphs[index]));
        }

        [Test]
        public void WarmShopkeeperItemHoverUsesPreparedInspectionWithSubMillisecondZeroAllocationTransitions()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            SourceHoverHintTarget[] itemHints = view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Where(value => value.name.StartsWith(EconomyTransactionDirection.BuyFromMerchant.ToString(),
                    StringComparison.Ordinal)
                    || value.name.StartsWith(EconomyTransactionDirection.SellToMerchant.ToString(),
                        StringComparison.Ordinal))
                .Take(2).ToArray();
            Assert.That(itemHints, Has.Length.EqualTo(2));

            foreach (SourceHoverHintTarget hint in itemHints)
            {
                hint.OnPointerEnter(null);
                hint.OnPointerExit(null);
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            const int sweeps = 200;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int index = 0; index < sweeps; index++)
                foreach (SourceHoverHintTarget hint in itemHints)
                {
                    hint.OnPointerEnter(null);
                    hint.OnPointerExit(null);
                }
            stopwatch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            double transitions = sweeps * itemHints.Length * 2d;
            double averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / transitions;
            TestContext.WriteLine($"Shopkeeper warm hover: {transitions:0} transitions, "
                                  + $"{averageMilliseconds:F4} ms average, {allocated} B allocated.");
            Assert.That(averageMilliseconds, Is.LessThan(0.5d));
            Assert.That(allocated, Is.Zero);
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
            Assert.That(goldRoot.GetComponent<RetailStartingEquipmentDragTarget>(), Is.Not.Null,
                "source Gold remains a physically rearrangeable player-inventory object");
            Assert.That(goldRoot.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(goldRoot.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Any(value => value.name == "Quantity"), Is.False,
                "source barter does not draw the Gold amount over the grid icon");
            Assert.That(view.GetComponentsInChildren<SourceUiImage>(true)
                    .Single(value => value.name == "Shopkeeper Message Icon").Key.SourceId,
                Is.EqualTo(RetailStartingEquipmentLayout.GoldInspectionIconSourceId));
            SourceUiBitmapText goldQuantity = Text(view, "Gold Quantity");
            SourceUiBitmapText goldWeight = Text(view, "Gold Weight");
            Assert.That(goldQuantity.FontSourceId, Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId));
            Assert.That(goldWeight.FontSourceId, Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId));
            Assert.That((Color32)goldQuantity.GetComponentsInChildren<Image>(true).First().color,
                Is.EqualTo(new Color32(236, 229, 204, 255)));
            Assert.That((Color32)goldWeight.GetComponentsInChildren<Image>(true).First().color,
                Is.EqualTo(new Color32(255, 50, 50, 255)));
            Assert.That(((RectTransform)goldQuantity.transform).anchoredPosition.y, Is.EqualTo(0f));
            Assert.That(((RectTransform)goldWeight.transform).anchoredPosition.y, Is.EqualTo(-36f));
            Assert.That(view.MerchantQuoteText, Is.Not.Empty,
                "Gold follows the source merchant-wont-buy quote route");
            Assert.That(Enumerable.Range(1200, 100).Any(id =>
                    _retail.TryReadMessage("mes/gd_npc_m2m.mes", id, out string line)
                    && string.Equals(line.Trim(), view.MerchantQuoteText, StringComparison.Ordinal)),
                Is.True, "Gold uses a source gd_npc merchant-wont-buy line");

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
        public void PhysicalGoldDragRetainsOneAuthorityPositionAndQuantityAcrossTradeAndSaveLoad()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            GameUiMerchantItemView gold = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Type == ObjectType.Gold);
            RectTransform root = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == $"{gold.Direction} {gold.Item.Identity.Key}");
            RetailStartingEquipmentDragTarget drag = root.GetComponent<RetailStartingEquipmentDragTarget>();
            const int destination = 119;
            Vector2 target = ScreenPoint(view.WindowTransform,
                RetailStartingEquipmentLayout.PlayerGrid.x
                + destination % RetailStartingEquipmentLayout.GridColumns * RetailStartingEquipmentLayout.CellSize + 16f,
                RetailStartingEquipmentLayout.PlayerGrid.y
                + destination / RetailStartingEquipmentLayout.GridColumns * RetailStartingEquipmentLayout.CellSize + 16f);
            var eventSystemObject = new GameObject("Gold Drag EventSystem", typeof(EventSystem));
            try
            {
                var pointer = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(root.rect.center)),
                };
                drag.OnPointerDown(pointer);
                drag.OnBeginDrag(pointer);
                pointer.position = target;
                drag.OnDrag(pointer);
                drag.OnEndDrag(pointer);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }

            Assert.That(_session.States[gold.Item.Identity].InventoryLocation, Is.EqualTo(destination));
            int movedQuantity = _session.States[gold.Item.Identity].StackQuantity.Value;
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            Assert.That(_controller.Trade(armor.Direction, armor.Item.Identity), Is.True, _controller.Feedback);
            Assert.That(_session.States[gold.Item.Identity].InventoryLocation, Is.EqualTo(destination));
            GameUiMerchantItemView resale = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Identity == armor.Item.Identity);
            Assert.That(_controller.Trade(resale.Direction, resale.Item.Identity), Is.True, _controller.Feedback);
            Assert.That(_session.States[gold.Item.Identity].InventoryLocation, Is.EqualTo(destination));
            Assert.That(_session.States[gold.Item.Identity].StackQuantity, Is.Not.EqualTo(movedQuantity));

            int retainedQuantity = _session.States[gold.Item.Identity].StackQuantity.Value;
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            PersistentObjectState[] retainedGold = _session.ChildrenOf(pc).Select(identity => _session.States[identity])
                .Where(value => value.Type == ObjectType.Gold).ToArray();
            Assert.That(retainedGold, Has.Length.EqualTo(1));
            Assert.That(retainedGold[0].Identity, Is.EqualTo(gold.Item.Identity));
            Assert.That(retainedGold[0].InventoryLocation, Is.EqualTo(destination));
            Assert.That(retainedGold[0].StackQuantity, Is.EqualTo(retainedQuantity));
        }

        [Test]
        public void PhysicalGoldProjectionUsesOneAuthoritativeBalanceAtSourceBoundaryValues()
        {
            FinalizeCustomCharacter();
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;

            AssertProjectedGold(400);
            Assert.That(_session.TryTransferGold(pc, Merchant, 399, out string failure), Is.True, failure);
            AssertProjectedGold(1);
            Assert.That(_session.TryTransferGold(pc, Merchant, 1, out failure), Is.True, failure);
            AssertProjectedGold(0);
            _session.AddGold(pc, 400);
            AssertProjectedGold(400);
            _session.AddGold(pc, 12_345);
            AssertProjectedGold(12_745);
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
        public void RepeatedSourceAmmoEntriesBecomeOneAuthoritativeStackAndSurviveSaveLoad()
        {
            _session.BindEconomySource(InventorySourceCatalog.FromMes(
                Mes((1, "Human Shopkeeper: 0 0 100 7038 100 7038 100 7038 100 7038 100 7038 "
                        + "100 7038 100 7038 100 7038 100 7038 100 7038")),
                Mes((1, "ALL"))));
            FinalizeCustomCharacter();
            Assert.That(_session.Economy.TryResolveMerchant(Merchant, out ArcanumObjectId owner, out _), Is.True);
            PersistentObjectState arrows = _session.ChildrenOf(owner).Select(id => _session.States[id])
                .Single(value => value.PrototypeNumber == 7058);
            Assert.That(arrows.StackQuantity, Is.EqualTo(100));
            Assert.That(_controller.ProjectMerchantWares().Count(value => value.Item.PrototypeNumber == 7058),
                Is.EqualTo(1));

            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            PersistentObjectState reloaded = _session.ChildrenOf(owner).Select(id => _session.States[id])
                .Single(value => value.PrototypeNumber == 7058);
            Assert.That(reloaded.StackQuantity, Is.EqualTo(100));
        }

        [Test]
        public void ShopkeeperDropRestoresMerchantStockButPreservesPlayerRelocationAndCrossGridTransfers()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            Assert.That(_session.Economy.TryResolveMerchant(Merchant, out ArcanumObjectId merchantOwner,
                out _), Is.True);
            GameUiMerchantItemView expensive = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == ExpensivePrototype);
            Vector2 merchantCell = ScreenPoint(view.WindowTransform,
                RetailStartingEquipmentLayout.MerchantGrid.x + 16f,
                RetailStartingEquipmentLayout.MerchantGrid.y + RetailStartingEquipmentLayout.CellSize + 16f);
            int originalMerchantLocation = _session.States[expensive.Item.Identity].InventoryLocation;
            Assert.That(Drop(view, expensive, merchantCell), Is.False);
            Assert.That(_session.States[expensive.Item.Identity].InventoryLocation,
                Is.EqualTo(originalMerchantLocation));

            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.States[expensive.Item.Identity].InventoryLocation,
                Is.EqualTo(originalMerchantLocation));

            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            Vector2 playerCell = ScreenPoint(view.WindowTransform,
                RetailStartingEquipmentLayout.PlayerGrid.x + 16f,
                RetailStartingEquipmentLayout.PlayerGrid.y
                + 10f * RetailStartingEquipmentLayout.CellSize + 16f);
            Assert.That(Drop(view, armor, playerCell), Is.True);
            Assert.That(_session.States[armor.Item.Identity].ParentIdentity,
                Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            GameUiMerchantItemView resale = _controller.ProjectPlayerSaleItems()
                .Single(value => value.Item.Identity == armor.Item.Identity);
            Assert.That(Drop(view, resale, merchantCell), Is.True);
            Assert.That(_session.States[armor.Item.Identity].ParentIdentity, Is.EqualTo(merchantOwner));

            GameUiMerchantItemView playerItem = _controller.ProjectPlayerSaleItems()
                .First(value => value.Item.Type != ObjectType.Gold);
            int playerOriginal = _session.States[playerItem.Item.Identity].InventoryLocation;
            Vector2 secondPlayerCell = ScreenPoint(view.WindowTransform,
                RetailStartingEquipmentLayout.PlayerGrid.x + 5f * RetailStartingEquipmentLayout.CellSize + 16f,
                RetailStartingEquipmentLayout.PlayerGrid.y + 3f * RetailStartingEquipmentLayout.CellSize + 16f);
            Assert.That(Drop(view, playerItem, secondPlayerCell), Is.True);
            Assert.That(_session.States[playerItem.Item.Identity].InventoryLocation,
                Is.Not.EqualTo(playerOriginal));
        }

        [Test]
        public void ShopkeeperStackUsesSourceQuantityPanelWithoutGridBadgeAndCommitsExactAmount()
        {
            _session.BindEconomySource(InventorySourceCatalog.FromMes(
                Mes((1, "Human Shopkeeper: 0 0 100 7038 100 7038 100 7038 100 7038 100 7038 "
                        + "100 7038 100 7038 100 7038 100 7038 100 7038")),
                Mes((1, "ALL"))));
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView arrows = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == 7058);
            RectTransform root = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == $"{arrows.Direction} {arrows.Item.Identity.Key}");
            Assert.That(root.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Any(value => value.name == "Quantity"), Is.False,
                "source barter grids draw item ART without a stack-count overlay");
            int sourceBefore = arrows.Item.Quantity;
            int goldBefore = _controller.PlayerGold;
            Vector2 playerCell = ScreenPoint(view.WindowTransform,
                RetailStartingEquipmentLayout.PlayerGrid.x + 16f,
                RetailStartingEquipmentLayout.PlayerGrid.y
                + 10f * RetailStartingEquipmentLayout.CellSize + 16f);

            Assert.That(Drop(view, arrows, playerCell), Is.True);
            Assert.That((view.QuantityPanelVisible, view.SelectedQuantity, view.MaximumQuantity),
                Is.EqualTo((true, 0, sourceBefore)));
            SourceUiBitmapText roller = Text(view, "Quantity Value");
            Assert.That(roller.FontSourceId, Is.EqualTo(SourceUiBitmapText.RollerFontSourceId));
            Assert.That(roller.Text, Is.EqualTo("000000"),
                "roller_ui_draw paints all six wheels, including source leading zeroes");
            Assert.That(roller.GetComponentsInChildren<Image>(true)
                .SelectMany(value => value.sprite.texture.GetPixels32())
                .Any(pixel => pixel.a == byte.MaxValue), Is.True,
                "source roller font 25 uses opaque palette glyphs rather than TIG red-channel coverage");
            Assert.That(((RectTransform)roller.transform).anchoredPosition.x,
                Is.EqualTo(RetailStartingEquipmentLayout.QuantityValue.x));
            Assert.That(_session.States[arrows.Item.Identity].StackQuantity, Is.EqualTo(sourceBefore),
                "opening the quantity selector is non-mutating");
            Invoke(view, "Quantity Plus");
            Assert.That(view.SelectedQuantity, Is.EqualTo(1));
            Invoke(view, "Quantity Confirm");
            Assert.That(view.QuantityPanelVisible, Is.False);
            Assert.That(_session.States[arrows.Item.Identity].StackQuantity, Is.EqualTo(sourceBefore - 1));
            Assert.That(_controller.ProjectPlayerSaleItems().Single(value =>
                value.Item.PrototypeNumber == 7058).Item.Quantity, Is.EqualTo(1));
            Assert.That(_controller.PlayerGold, Is.LessThan(goldBefore));
        }

        [Test]
        public void QuantityCancelAndInsufficientConfirmationNeverMutateEitherInventory()
        {
            _session.BindEconomySource(InventorySourceCatalog.FromMes(
                Mes((1, "Human Shopkeeper: 0 0 100 7038 100 7038 100 7038")), Mes((1, "ALL"))));
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView arrows = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == 7058);
            int quantity = arrows.Item.Quantity;
            int gold = _controller.PlayerGold;
            Invoke(view, $"{arrows.Direction} {arrows.Item.Identity.Key}");
            Invoke(view, "Quantity Take All");
            Invoke(view, "Quantity Cancel");
            Assert.That(_session.States[arrows.Item.Identity].StackQuantity, Is.EqualTo(quantity));
            Assert.That(_controller.PlayerGold, Is.EqualTo(gold));
            Assert.That(_controller.ProjectPlayerSaleItems().Any(value =>
                value.Item.PrototypeNumber == 7058), Is.False);
        }

        [Test]
        public void SourceItemInspectionSeparatesPropertiesWeightAndCondition()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            Hover(view, $"{armor.Direction} {armor.Item.Identity.Key}", true);
            SourceUiBitmapText properties = Text(view, "Item Properties");
            SourceUiBitmapText weight = Text(view, "Item Weight");
            SourceUiBitmapText condition = Text(view, "Item Condition");
            Assert.That(properties.FontSourceId, Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId));
            Assert.That(weight.Text, Is.EqualTo("Weight: 60 stone"));
            Assert.That(condition.Text, Is.EqualTo("50/50"));
            Assert.That(((RectTransform)weight.transform).anchoredPosition.y, Is.EqualTo(-36f));
            Assert.That(((RectTransform)condition.transform).anchoredPosition.x,
                Is.GreaterThan(((RectTransform)weight.transform).anchoredPosition.x));
            Assert.That((Color32)weight.GetComponentsInChildren<Image>(true).First().color,
                Is.EqualTo(new Color32(255, 50, 50, 255)));
            Assert.That((Color32)condition.GetComponentsInChildren<Image>(true).First().color,
                Is.EqualTo(new Color32(255, 50, 50, 255)));
        }

        [Test]
        public void ArmorInspectionUsesSourceResistancePresentationOrder()
        {
            FinalizeCustomCharacter();
            _session.BindPrototypeSource(ResolvePrototype);
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);

            GameUiItemInspectionView inspection = _controller.ProjectItemInspection(armor.Item.Identity);

            Assert.That(inspection.PropertyLines, Is.EqualTo(new[] { "AC:6", "DR:+10", "FR:+3" }));
        }

        [Test]
        public void ShopkeeperActionBoxesUseSourceDragSemanticsAndRefuseMerchantGambling()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView merchant = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            ObjectPlacement merchantPlacement = _session.States[merchant.Item.Identity].Placement;
            Vector2 gamble = ScreenPoint(view.WindowTransform,
                RetailStartingEquipmentLayout.GambleBox.center.x,
                RetailStartingEquipmentLayout.GambleBox.center.y);
            Assert.That(Drop(view, merchant, gamble), Is.False);
            Assert.That(_session.States[merchant.Item.Identity].Placement, Is.EqualTo(merchantPlacement));
            Assert.That(view.MerchantQuoteText, Is.EqualTo("I am not a gambling man."));

            Vector2 use = ScreenPoint(view.WindowTransform,
                RetailStartingEquipmentLayout.UseBox.center.x,
                RetailStartingEquipmentLayout.UseBox.center.y);
            Assert.That(Drop(view, merchant, use), Is.False);
            Assert.That(_session.States[merchant.Item.Identity].Placement, Is.EqualTo(merchantPlacement));
        }

        [Test]
        public void StartingShopPaperDollItemsRemainAuthoritativeDraggableInspectionPresenters()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView wearable = _controller.ProjectPlayerSaleItems()
                .First(value => _session.TryGetObjectState(value.Item.Identity, out PersistentObjectState state)
                                && WorldMapSessionCoordinator.TryGetNaturalWornLocation(state, out _));
            Assert.That(_controller.Equip(wearable.Item.Identity), Is.True, _controller.Feedback);
            Assert.That(_session.States[wearable.Item.Identity].Placement.Kind,
                Is.EqualTo(ObjectPlacementKind.Equipped));

            Invoke(view, "Player Paper Doll");
            RectTransform equipped = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == $"Equipped {wearable.Item.Identity.Key}");
            Assert.That(equipped.GetComponent<RetailStartingEquipmentDragTarget>(), Is.Not.Null,
                "source paper-doll items remain real draggable presenters");
            Assert.That(equipped.GetComponent<CanvasGroup>(), Is.Not.Null,
                "paper-doll drag callbacks share the inventory presenter invariant");
            Hover(view, equipped.name, true);
            Assert.That(view.MessageTitleText, Is.Not.Empty,
                "equipped source items retain the shared item-inspection route");
        }

        [Test]
        public void ShopkeeperDragMovesTheActualItemAndInvalidDropRestoresPresentationWithoutMutation()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            RectTransform root = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == $"{armor.Direction} {armor.Item.Identity.Key}");
            RetailStartingEquipmentDragTarget drag = root.GetComponent<RetailStartingEquipmentDragTarget>();
            Assert.That(root.GetComponent<CanvasGroup>(), Is.Not.Null,
                "trade items must own their drag raycast group before native pointer input begins");
            Transform originalParent = root.parent;
            Vector2 originalPosition = root.anchoredPosition;
            ObjectPlacement originalPlacement = _session.States[armor.Item.Identity].Placement;
            var eventSystemObject = new GameObject("Shopkeeper Drag EventSystem", typeof(EventSystem));
            try
            {
                var pointer = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(root.rect.center)),
                };
                drag.OnBeginDrag(pointer);
                Assert.That(root.parent.name, Is.EqualTo("Shopkeeper Items"));
                Vector2 before = root.anchoredPosition;
                pointer.position += new Vector2(48f, 32f);
                drag.OnDrag(pointer);
                Assert.That(root.anchoredPosition, Is.Not.EqualTo(before));
                pointer.position = new Vector2(-100f, -100f);
                drag.OnEndDrag(pointer);
                Assert.That(root.parent, Is.SameAs(originalParent));
                Assert.That(root.anchoredPosition, Is.EqualTo(originalPosition));
                Assert.That(_session.States[armor.Item.Identity].Placement, Is.EqualTo(originalPlacement));
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void ShopkeeperRebuildGivesEveryDraggableTradePresenterCanvasGroupBeforeInput()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            AssertTradeDragPresenters(view);

            Invoke(view, "Merchant Paper Doll");
            Invoke(view, "Merchant Inventory");
            AssertTradeDragPresenters(view);
        }

        [Test]
        public void MerchantDragReleaseOutsidePlayerGridSuppressesTheSyntheticClickPurchase()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            RectTransform root = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == $"{armor.Direction} {armor.Item.Identity.Key}");
            RetailStartingEquipmentDragTarget drag = root.GetComponent<RetailStartingEquipmentDragTarget>();
            Button click = root.GetComponent<Button>();
            ObjectPlacement original = _session.States[armor.Item.Identity].Placement;
            var eventSystemObject = new GameObject("Shopkeeper Click Suppression EventSystem", typeof(EventSystem));
            try
            {
                var pointer = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(root.rect.center)),
                };
                drag.OnPointerDown(pointer);
                drag.OnBeginDrag(pointer);
                pointer.position = new Vector2(-100f, -100f);
                drag.OnDrag(pointer);
                drag.OnEndDrag(pointer);
                click.onClick.Invoke();

                Assert.That(_session.States[armor.Item.Identity].Placement, Is.EqualTo(original));
                Assert.That(_controller.ProjectMerchantWares().Any(value => value.Item.Identity == armor.Item.Identity),
                    Is.True);
                Assert.That(view.QuantityPanelVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void MerchantDragReleasedOverMerchantInventoryRestoresWithoutPurchase()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            GameUiMerchantItemView armor = _controller.ProjectMerchantWares()
                .Single(value => value.Item.PrototypeNumber == CheapArmorPrototype);
            RectTransform root = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == $"{armor.Direction} {armor.Item.Identity.Key}");
            RectTransform merchantTarget = view.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "Merchant Inventory Drop Target");
            RetailStartingEquipmentDragTarget drag = root.GetComponent<RetailStartingEquipmentDragTarget>();
            Button click = root.GetComponent<Button>();
            Vector2 originalPosition = root.anchoredPosition;
            ObjectPlacement originalPlacement = _session.States[armor.Item.Identity].Placement;
            var eventSystemObject = new GameObject("Shopkeeper Same-Side Drag EventSystem", typeof(EventSystem));
            try
            {
                var pointer = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(root.rect.center)),
                    pointerCurrentRaycast = new RaycastResult { gameObject = merchantTarget.gameObject },
                };
                drag.OnPointerDown(pointer);
                drag.OnBeginDrag(pointer);
                pointer.position += new Vector2(48f, 32f);
                drag.OnDrag(pointer);
                drag.OnEndDrag(pointer);
                click.onClick.Invoke();

                Assert.That(root.anchoredPosition, Is.EqualTo(originalPosition));
                Assert.That(_session.States[armor.Item.Identity].Placement, Is.EqualTo(originalPlacement));
                Assert.That(_controller.ProjectMerchantWares().Any(value => value.Item.Identity == armor.Item.Identity),
                    Is.True);
                Assert.That(view.QuantityPanelVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void ShopkeeperPaperDollStatsUseSourceFontsGeometryAndCombatHelp()
        {
            FinalizeCustomCharacter();
            RetailStartingEquipmentView view = BuildView();
            Invoke(view, "Merchant Paper Doll");

            Assert.That(Text(view, "Merchant Attack").FontSourceId,
                Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId));
            Assert.That(Text(view, "Merchant Defense").FontSourceId,
                Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId));
            Assert.That(Text(view, "Player Attack").FontSourceId,
                Is.EqualTo(SourceUiBitmapText.InventoryStatFontSourceId));
            Assert.That(RetailStartingEquipmentLayout.MerchantAttackHelp,
                Is.EqualTo(new Rect(75f, 329f, 55f, 40f)));
            Assert.That(RetailStartingEquipmentLayout.MerchantDefenseHelp,
                Is.EqualTo(new Rect(225f, 329f, 55f, 40f)));

            Hover(view, "Merchant Attack Help", true);
            Assert.That(Text(view, "Shopkeeper Message Title").Text, Does.StartWith("Total Attack:"));
            Assert.That(Text(view, "Base To Hit").Text, Does.StartWith("Base To Hit:"));
            Assert.That(view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Shopkeeper Message Icon").Key.SourceId, Is.EqualTo(675));
            Hover(view, "Merchant Attack Help", false);
            Assert.That(Text(view, "Shopkeeper Message Title").Text, Is.Empty);

            Hover(view, "Merchant Defense Help", true);
            Assert.That(Text(view, "Shopkeeper Message Title").Text, Does.StartWith("Total Defense:"));
            Assert.That(Text(view, "Total AC").Text, Does.StartWith("Total AC:"));
            Assert.That(view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "Shopkeeper Message Icon").Key.SourceId, Is.EqualTo(674));
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

        private static void AssertTradeDragPresenters(RetailStartingEquipmentView view)
        {
            RetailStartingEquipmentDragTarget[] targets =
                view.GetComponentsInChildren<RetailStartingEquipmentDragTarget>(true);
            Assert.That(targets, Is.Not.Empty);
            Assert.That(targets.All(value => value.GetComponent<CanvasGroup>() != null), Is.True,
                "merchant/player items, including rebuilt and scrolled presenters, share one drag invariant");
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
                .Where(value => value.gameObject.activeSelf)
                .Select(value => value.Text));
        }

        private static bool Drop(RetailStartingEquipmentView view, GameUiMerchantItemView item,
            Vector2 screenPosition)
        {
            object result = typeof(RetailStartingEquipmentView).GetMethod("DropItem",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(GameUiMerchantItemView), typeof(Vector2), typeof(Camera) },
                    modifiers: null)
                ?.Invoke(view, new object[] { item, screenPosition, null });
            return result is true;
        }

        private static Vector2 ScreenPoint(RectTransform window, float sourceX, float sourceY)
        {
            float localX = sourceX - window.rect.width * window.pivot.x;
            float localY = window.rect.height * (1f - window.pivot.y) - sourceY;
            return RectTransformUtility.WorldToScreenPoint(null,
                window.TransformPoint(new Vector3(localX, localY, 0f)));
        }

        private void AssertProjectedGold(int expected)
        {
            Assert.That(_session.GetGold(ProductionPlayerLifecycle.DefaultPlayerIdentity), Is.EqualTo(expected));
            GameUiMerchantItemView[] gold = _controller.ProjectPlayerSaleItems()
                .Where(value => value.Item.Type == ObjectType.Gold).ToArray();
            if (expected == 0) Assert.That(gold, Is.Empty);
            else
            {
                Assert.That(gold, Has.Length.EqualTo(1));
                Assert.That(gold[0].Item.Quantity, Is.EqualTo(expected));
                Assert.That(gold[0].Item.Weight, Is.Zero);
            }
        }

        private static ObjectProtoInfo ResolvePrototype(int number)
        {
            if (number == 9056)
                return new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3)
                    { GoldQuantity = 1 };
            if (number == CheapArmorPrototype)
                return new ObjectProtoInfo(number, ObjectType.Armor, 0x70000000u,
                    description: number, invAid: 1, weight: 60, worth: 5)
                    { HpPoints = 50, ArmorAc = 6, ArmorResist = new[] { 10, 3, 0, 0, 0 } };
            if (number == ExpensivePrototype)
                return new ObjectProtoInfo(number, ObjectType.Food, 0x70000001u,
                    description: number, invAid: 2, weight: 1, worth: 1000);
            if (number == 7058)
                return new ObjectProtoInfo(7058, ObjectType.Ammo, 0x50000000u,
                    description: 7058, invAid: 4, weight: 1, worth: 1)
                    { AmmoQuantity = 10, ItemFlags = 0 };
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
