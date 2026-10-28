using System;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime;
using Arcanum.Runtime.Save;
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
    [Category("UIEInventoryEquipment")]
    public sealed class UiEInventoryEquipmentTests
    {
        private const string Sector = "maps/test/1.sec";
        private const uint InventoryAid = 0x60041082u;
        private static RetailUiAssetResolver _retail;
        private string _fixtureRoot;
        private EnhancedUiAssetResolver _enhanced;
        private UiSkinResolver _skin;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private GameUiController _controller;
        private RetailInventoryEquipmentView _view;

        [OneTimeSetUp]
        public void OneTimeSetUp() => _retail = RetailUiAssetResolver.CreateProduction();

        [OneTimeTearDown]
        public void OneTimeTearDown() { _retail?.Dispose(); _retail = null; }

        [SetUp]
        public void SetUp()
        {
            _fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIE-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_fixtureRoot);
            _enhanced = new EnhancedUiAssetResolver(_fixtureRoot, _ => { });
            _skin = new UiSkinResolver(_retail, _enhanced);
            _root = new GameObject(nameof(UiEInventoryEquipmentTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity, Sector, Vector2.one,
                0x28100000u);
            _controller = new GameUiController(_session, new EmptySlots());
            _view = _root.AddComponent<RetailInventoryEquipmentView>();
            _view.Bind(_controller, _session, _skin);
            Assert.That(_controller.Open(GameUiScreen.Inventory), Is.True);
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
        public void RetailInventoryAssetsResolveDirectlyFromSourceData()
        {
            foreach (int id in new[] { 25, 221, 223, 233, 241, 242, 243, 244, 245, 246, 247, 248, 249,
                         340, 486, 676, 677 })
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(_retail.TryResolveInventoryItem(InventoryAid, out RetailItemArtAsset item), Is.True);
            Assert.That(item.SourcePath, Does.StartWith("art/item/"));
            Assert.That(item.LogicalSize.x, Is.GreaterThan(0));
            Assert.That(item.LogicalSize.y, Is.GreaterThan(0));
        }

        [Test]
        public void InventoryArtUsesThePaletteEncodedInTheRetainedAid()
        {
            var prototypes = new ProtoLibrary(GameDataLocator.FindDirectory("data/proto"));
            int[] sourceShopStock =
            {
                6049, 6050, 6051, 6052, 6057, 6071, 6077, 6079, 6080, 6093, 6101, 6117, 6128, 6184,
                8070, 8071, 8072, 8073, 8090, 8098, 8114, 8122, 8130, 8138, 8152, 8156, 8303, 8304,
            };
            ObjectProtoInfo encoded = sourceShopStock.Select(prototypes.Get)
                .First(value => value.InvAid.HasValue
                                && RetailUiAssetResolver.InventoryPaletteIndex(value.InvAid.Value) > 0);
            uint encodedArt = encoded.InvAid.Value;
            uint paletteZeroArt = encodedArt & ~0x30u;
            Assert.That(_retail.TryResolveInventoryItem(paletteZeroArt, out RetailItemArtAsset paletteZero), Is.True);
            Assert.That(_retail.TryResolveInventoryItem(encodedArt, out RetailItemArtAsset selected), Is.True);
            Assert.That(selected.SourcePath, Is.EqualTo(paletteZero.SourcePath),
                "palette bits select a palette within the same inventory ART");
            Assert.That(selected.Texture.GetPixels32(), Is.Not.EqualTo(paletteZero.Texture.GetPixels32()),
                "encoded palette variants must not collapse to palette zero");
        }

        [Test]
        public void RuntimeInventoryModesUseExactSourceRootsAndGridGeometry()
        {
            foreach (int id in new[] { RetailInventoryInteractionLayout.LootBackgroundSourceId,
                         RetailInventoryInteractionLayout.BarterBackgroundSourceId,
                         RetailInventoryInteractionLayout.PlayerInventorySourceId,
                         RetailInventoryInteractionLayout.TakeAllSourceId })
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(RetailInventoryInteractionLayout.LootGrid, Is.EqualTo(new Rect(8, 136, 320, 256)));
            Assert.That(RetailInventoryInteractionLayout.LootScrollbar, Is.EqualTo(new Rect(330, 136, 17, 256)));
            Assert.That(RetailInventoryInteractionLayout.BarterGrid, Is.EqualTo(new Rect(8, 168, 320, 224)));
            Assert.That(RetailInventoryInteractionLayout.BarterScrollbar, Is.EqualTo(new Rect(330, 168, 17, 224)));
            Assert.That(RetailInventoryInteractionLayout.ScreenRect(800, 600), Is.EqualTo(new Rect(0, 41, 800, 400)));
        }

        [Test]
        public void ContainerModeTransfersThroughExistingInventoryAuthority()
        {
            var source = new ObjectInstance(ObjectType.Container, 9700, 1L | (1L << 32),
                0xA0000000u, 0, 0, oid: GuidBytes(70));
            PersistentObjectState container = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            var runtimeObject = new GameObject("Container Runtime");
            runtimeObject.transform.SetParent(_root.transform);
            WorldObject runtime = runtimeObject.AddComponent<WorldObject>();
            runtime.Type = ObjectType.Container;
            _session.Bind(Sector, container, runtime);
            var itemSource = new ObjectInstance(ObjectType.Armor, 9771, null, 0x40000000u, 0, 0,
                oid: GuidBytes(71), parentOid: Bytes(container.Identity), invLocation: 0, invAid: InventoryAid);
            PersistentObjectState item = _session.GetOrCreate(itemSource, Sector, itemSource.CurrentArtId.Value,
                false, false, inventoryArtId: InventoryAid, inventoryFootprint: InventoryFootprint.OneCell,
                inventoryLocation: 0);

            Assert.That(_controller.BeginContainerLoot(container.Identity), Is.True, _controller.Feedback);
            Assert.That(_controller.Screen, Is.EqualTo(GameUiScreen.Container));
            Assert.That(_controller.ProjectOpenInventoryTarget().Single().Identity, Is.EqualTo(item.Identity));
            RetailInventoryInteractionView interaction =
                _root.AddComponent<RetailInventoryInteractionView>();
            interaction.Bind(_controller, _skin);
            interaction.Synchronize();
            RectTransform presenter = _root.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == $"Item {item.Identity.Key}");
            Assert.That(presenter.GetComponent<RetailStartingEquipmentDragTarget>(), Is.Not.Null);
            Assert.That(presenter.GetComponent<CanvasGroup>(), Is.Not.Null,
                "container/trade presenters must own their drag CanvasGroup when constructed");
            Assert.That(_controller.TransferOpenInventoryItem(item.Identity), Is.True, _controller.Feedback);
            Assert.That(item.ParentIdentity, Is.EqualTo(_session.PlayerState.Identity));
        }

        [Test]
        public void OrdinaryInventoryUsesExactSourcePanelComposition()
        {
            Assert.That(RetailInventoryEquipmentLayout.PaperDoll, Is.EqualTo(new Rect(0, 0, 800, 400)));
            Assert.That(RetailInventoryEquipmentLayout.Inventory, Is.EqualTo(new Rect(358, 0, 442, 400)));
            Assert.That(_view.PaperDollAsset.Key.SourceId, Is.EqualTo(223));
            Assert.That(_view.InventoryAsset.Key.SourceId, Is.EqualTo(221));
            Assert.That(_view.WindowTransform.sizeDelta, Is.EqualTo(new Vector2(800, 400)));
            Assert.That(_view.WindowTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 59)));
            Assert.That(_root.GetComponentsInChildren<RetailPcLensView>(true), Has.Length.EqualTo(1));
            Assert.That(_root.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "PC Lens Frame").Key.SourceId, Is.EqualTo(233));
            Assert.That(_root.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Arrange Items"), Is.Not.Null);
        }

        [Test]
        public void ArrangeItemsUsesOnlyTheTwoAuthoredSourceFramesForEveryPointerState()
        {
            SourceUiButton arrange = _root.GetComponentsInChildren<SourceUiButton>(true)
                .Single(value => value.name == "Arrange Items");
            Assert.That(arrange.CurrentPresentationKey, Is.EqualTo(new UiAssetKey(340, frame: 0)));
            arrange.OnPointerEnter(null);
            Assert.That(arrange.CurrentPresentationKey, Is.EqualTo(new UiAssetKey(340, frame: 0)),
                "TIG momentary Arrange stays up during hover");
            arrange.OnPointerDown(null);
            Assert.That(arrange.CurrentPresentationKey, Is.EqualTo(new UiAssetKey(340, frame: 1)),
                "ART 340 has no frame 2; source press uses its only down frame");
            arrange.OnPointerUp(null);
            Assert.That(arrange.CurrentPresentationKey, Is.EqualTo(new UiAssetKey(340, frame: 0)),
                "mouse release restores the authored up frame even before the pointer leaves");
            arrange.OnPointerExit(null);
            Assert.That(arrange.CurrentPresentationKey, Is.EqualTo(new UiAssetKey(340, frame: 0)));
            arrange.SetInteractable(false);
            Assert.That(arrange.CurrentPresentationKey, Is.EqualTo(new UiAssetKey(340, frame: 0)));
        }

        [Test]
        public void EveryOrdinaryInventoryDragPresenterOwnsCanvasGroupBeforeCallbacksAndAfterRebuild()
        {
            AddItem(40, ObjectType.Armor, 0, InventoryFootprint.OneCell);
            AddItem(41, ObjectType.Ammo, 1, InventoryFootprint.OneCell);
            AddItem(42, ObjectType.Gold, 2, InventoryFootprint.OneCell);
            _view.Synchronize();
            AssertInventoryDragPresenters(3);

            AddItem(43, ObjectType.Weapon, 3, InventoryFootprint.OneCell);
            _view.Synchronize();
            AssertInventoryDragPresenters(4);
        }

        [TestCase(800, 600, 0, 41)]
        [TestCase(1024, 768, 112, 125)]
        [TestCase(1920, 1080, 560, 281)]
        [TestCase(2560, 1440, 880, 461)]
        [TestCase(3840, 2160, 1520, 821)]
        [TestCase(1280, 720, 240, 101)]
        [TestCase(3440, 1440, 1320, 461)]
        public void BigWindowUsesSourceHrpCenteredOffset(int width, int height, float x, float y)
            => Assert.That(RetailInventoryEquipmentLayout.ScreenRect(width, height),
                Is.EqualTo(new Rect(x, y, 800, 400)));

        [Test]
        public void EquipmentSlotsUseExactSourceGeometryAndSilhouettes()
        {
            Assert.That(RetailInventoryEquipmentLayout.EquipmentSlots.Select(value => value.Location),
                Is.EqualTo(new[] { WornLocation.Helmet, WornLocation.Ring1, WornLocation.Ring2,
                    WornLocation.Medallion, WornLocation.Weapon, WornLocation.Shield, WornLocation.Armor,
                    WornLocation.Gauntlet, WornLocation.Boots }));
            Assert.That(RetailInventoryEquipmentLayout.EquipmentSlots.Select(value => value.EmptySourceId),
                Is.EqualTo(new[] { 241, 246, 247, 245, 249, 248, 244, 243, 242 }));
            Assert.That(RetailInventoryEquipmentLayout.EquipmentSlots.Single(value =>
                value.Location == WornLocation.Armor).Rect, Is.EqualTo(new Rect(119, 171, 128, 160)));
        }

        [Test]
        public void InventoryGridUsesRetainedLocationAndFootprint()
        {
            Assert.That(RetailInventoryEquipmentLayout.InventoryItemRect(0, InventoryFootprint.OneCell),
                Is.EqualTo(new Rect(368, 8, 32, 32)));
            Assert.That(RetailInventoryEquipmentLayout.InventoryItemRect(13, new InventoryFootprint(2, 3)),
                Is.EqualTo(new Rect(464, 40, 64, 96)));
        }

        [Test]
        public void InventorySummaryUsesSessionAuthorityAndExactSourceCounterGeometry()
        {
            GameUiInventorySummaryView summary = _controller.ProjectInventorySummary();
            Assert.That(summary, Is.Not.Null);
            Assert.That((summary.Weight, summary.CarryCapacity, summary.EncumbranceLevel),
                Is.EqualTo((0L, 4000, 0)));
            Assert.That(summary.EncumbranceThreshold, Is.EqualTo(600));
            Assert.That(summary.Ammunition, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(RetailInventoryEquipmentLayout.CounterRects.Select(value => (value.x, value.y)),
                Is.EqualTo(new[] { (729f, 70f), (729f, 93f), (729f, 116f), (729f, 139f), (729f, 162f) }));
            Assert.That(RetailInventoryEquipmentLayout.AttackValue, Is.EqualTo(new Rect(80, 358, 23, 35)));
            Assert.That(RetailInventoryEquipmentLayout.DefenseValue, Is.EqualTo(new Rect(261, 358, 23, 35)));
            Assert.That(RetailInventoryEquipmentLayout.AttackIcon, Is.EqualTo(new Rect(29, 318, 54, 71)),
                "ART 676 keeps its native retail frame extent at the source anchor");
            Assert.That(RetailInventoryEquipmentLayout.DefenseIcon, Is.EqualTo(new Rect(254, 319, 83, 71)),
                "ART 677 keeps its native retail frame extent at the source anchor");
            foreach (string name in new[] { "Weight", "Encumbrance", "Speed" })
                Assert.That(_root.GetComponentsInChildren<SourceUiBitmapText>(true)
                    .Single(value => value.name == name).FontSourceId,
                    Is.EqualTo(SourceUiBitmapText.Flare12FontSourceId),
                    $"{name} must use source inven_ui dword_682C74 / ART 229");
        }

        [Test]
        public void AttackDefenseValuesAndHoverUseSourceFontsAlignmentAndMessageAuthority()
        {
            SourceUiBitmapText attack = _root.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Attack Value");
            SourceUiBitmapText defense = _root.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Defense Value");
            Assert.That((attack.FontSourceId, defense.FontSourceId),
                Is.EqualTo((SourceUiBitmapText.InventoryStatFontSourceId,
                    SourceUiBitmapText.InventoryStatFontSourceId)));
            Assert.That(((RectTransform)attack.transform).anchoredPosition.x,
                Is.EqualTo(RetailInventoryEquipmentLayout.AttackValue.x
                           + RetailInventoryEquipmentLayout.AttackValue.width - attack.LogicalSize.x));
            Assert.That(((RectTransform)defense.transform).anchoredPosition,
                Is.EqualTo(new Vector2(RetailInventoryEquipmentLayout.DefenseValue.x,
                    -RetailInventoryEquipmentLayout.DefenseValue.y)));

            SourceHoverHintTarget attackHint = _root.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == "Total Attack");
            attackHint.OnPointerEnter(null);
            Assert.That(_controller.ContextualHelpKey, Is.EqualTo("inventory-total-attack"));
            Assert.That(_controller.ContextualHelpTitle, Does.StartWith("Total Attack:"));
            Assert.That(_controller.ContextualHelp, Does.Contain("Base To Hit:"));
            Assert.That(_controller.ContextualHelpIconSourceId, Is.EqualTo(675));
            attackHint.OnPointerExit(null);
            Assert.That(_controller.ContextualHelp, Is.Empty);

            SourceHoverHintTarget defenseHint = _root.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .Single(value => value.name == "Total Defense");
            defenseHint.OnPointerEnter(null);
            Assert.That(_controller.ContextualHelpKey, Is.EqualTo("inventory-total-defense"));
            Assert.That(_controller.ContextualHelpTitle, Does.StartWith("Total Defense:"));
            Assert.That(_controller.ContextualHelp, Does.Contain("Total AC:"));
            Assert.That(_controller.ContextualHelpIconSourceId, Is.EqualTo(674));
        }

        [Test]
        public void ArrangeUsesDescendingFootprintAndCommitsSourceGridLocations()
        {
            PersistentObjectState small = AddItem(20, ObjectType.Armor, 50, InventoryFootprint.OneCell);
            PersistentObjectState large = AddItem(21, ObjectType.Armor, 70, new InventoryFootprint(2, 3));
            Assert.That(_controller.ArrangeInventory(), Is.True);
            Assert.That(large.InventoryLocation, Is.Zero);
            Assert.That(small.InventoryLocation, Is.EqualTo(2));
        }

        [Test]
        public void ArrangeButtonAlternatesExactSourceHorizontalAndVerticalFirstFitLayouts()
        {
            PersistentObjectState small = AddItem(50, ObjectType.Generic, 100, InventoryFootprint.OneCell);
            PersistentObjectState tall = AddItem(51, ObjectType.Weapon, 90, new InventoryFootprint(1, 3));
            PersistentObjectState wide = AddItem(52, ObjectType.Generic, 80, new InventoryFootprint(3, 1));
            PersistentObjectState armor = AddItem(53, ObjectType.Armor, 70, new InventoryFootprint(2, 3));
            PersistentObjectState ammo = AddItem(54, ObjectType.Ammo, 60, InventoryFootprint.OneCell);
            PersistentObjectState gold = AddItem(55, ObjectType.Gold, 50, InventoryFootprint.OneCell);
            PersistentObjectState equipped = AddItem(56, ObjectType.Armor, 40, InventoryFootprint.OneCell);
            Assert.That(_controller.Equip(equipped.Identity), Is.True);
            ObjectPlacement worn = equipped.Placement;

            Button arrange = _root.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Arrange Items");
            arrange.onClick.Invoke();
            Assert.That(new[] { armor.InventoryLocation, tall.InventoryLocation, wide.InventoryLocation,
                    small.InventoryLocation, ammo.InventoryLocation, gold.InventoryLocation },
                Is.EqualTo(new[] { 0, 2, 3, 6, 7, 8 }), "first source press is row-major");
            Assert.That(equipped.Placement, Is.EqualTo(worn), "worn items are excluded from packing");

            Assert.That(_controller.MoveInventoryItem(gold.Identity, 19), Is.True,
                "manual placement remains authoritative after arrange");
            _view.Synchronize();
            arrange = _root.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Arrange Items");
            arrange.onClick.Invoke();
            Assert.That(new[] { armor.InventoryLocation, tall.InventoryLocation, wide.InventoryLocation,
                    small.InventoryLocation, ammo.InventoryLocation, gold.InventoryLocation },
                Is.EqualTo(new[] { 0, 30, 60, 70, 80, 90 }), "second source press is column-major");
            Assert.That(_session.States.Values.Count(value => value.ParentIdentity == _session.PlayerState.Identity),
                Is.EqualTo(7), "neither arrangement loses or duplicates an item");
        }

        [Test]
        public void ArrangedLocationsRoundTripAndPlayerCommandDoesNotRearrangeMerchantStock()
        {
            PersistentObjectState wide = AddItem(60, ObjectType.Generic, 80, new InventoryFootprint(3, 1));
            PersistentObjectState tall = AddItem(61, ObjectType.Weapon, 70, new InventoryFootprint(1, 3));
            Assert.That(_controller.ArrangeInventory(InventoryArrangeMode.Vertical), Is.True);
            (int wideLocation, int tallLocation) expected = (wide.InventoryLocation, tall.InventoryLocation);

            ArcanumObjectId merchant = AddInventoryOwner(70, ObjectType.Npc);
            PersistentObjectState merchantItem = AddItemToOwner(71, ObjectType.Armor, 77,
                new InventoryFootprint(2, 3), merchant);
            string saved = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_controller.MoveInventoryItem(wide.Identity, 90), Is.True);
            Assert.That(_session.SaveGames.LoadJson(saved).Succeeded, Is.True);
            Assert.That((_session.States[wide.Identity].InventoryLocation,
                    _session.States[tall.Identity].InventoryLocation), Is.EqualTo(expected),
                "authoritative arranged locations survive Save V1");

            Assert.That(_controller.ArrangeInventory(InventoryArrangeMode.Horizontal), Is.True);
            Assert.That(_session.States[merchantItem.Identity].InventoryLocation, Is.EqualTo(77),
                "the ordinary player arrange command must not mutate merchant stock");
        }

        [Test]
        public void ExactGridMoveRejectsOverlapWithoutMutatingEitherItem()
        {
            PersistentObjectState large = AddItem(22, ObjectType.Armor, 0, new InventoryFootprint(2, 2));
            PersistentObjectState small = AddItem(23, ObjectType.Armor, 5, InventoryFootprint.OneCell);
            Assert.That(_controller.MoveInventoryItem(small.Identity, 1), Is.False);
            Assert.That((large.InventoryLocation, small.InventoryLocation), Is.EqualTo((0, 5)));
            Assert.That(_controller.MoveInventoryItem(small.Identity, 2), Is.True);
            Assert.That(small.InventoryLocation, Is.EqualTo(2));
        }

        [Test]
        public void EquippedItemDropPreflightsFootprintThenMovesThroughInventoryAuthority()
        {
            PersistentObjectState occupied = AddItem(24, ObjectType.Armor, 0,
                new InventoryFootprint(2, 2));
            PersistentObjectState armor = AddItem(25, ObjectType.Armor, 20,
                new InventoryFootprint(2, 3));
            Assert.That(_controller.Equip(armor.Identity), Is.True);
            ObjectPlacement equipped = armor.Placement;

            Assert.That(_controller.MoveInventoryItem(armor.Identity, 1), Is.False);
            Assert.That(armor.Placement, Is.EqualTo(equipped),
                "an illegal drop must not unequip or partially mutate the item");
            Assert.That(occupied.InventoryLocation, Is.Zero);

            Assert.That(_controller.MoveInventoryItem(armor.Identity, 22), Is.True);
            Assert.That(armor.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Contained));
            Assert.That(armor.Placement.ParentIdentity, Is.EqualTo(_session.PlayerState.Identity));
            Assert.That(armor.InventoryLocation, Is.EqualTo(22));
        }

        [Test]
        public void ViewProjectsRetainedItemIdentityArtFootprintAndPlacement()
        {
            PersistentObjectState item = AddItem(1, ObjectType.Armor, 13, new InventoryFootprint(2, 3));
            _view.Synchronize();
            GameUiItemView projected = _view.ProjectedItems.Single(value => value.Identity == item.Identity);
            Assert.That(projected.InventoryArtId, Is.EqualTo(InventoryAid));
            Assert.That(projected.InventoryFootprint, Is.EqualTo(new InventoryFootprint(2, 3)));
            Assert.That(projected.InventoryLocation, Is.EqualTo(13));
            Assert.That(projected.IsEquipped, Is.False);
            Assert.That(_view.DynamicItemCount, Is.EqualTo(1));
        }

        [Test]
        public void ItemButtonSelectsThroughControllerWithoutMutatingPlacement()
        {
            PersistentObjectState item = AddItem(2, ObjectType.Armor, 4, InventoryFootprint.OneCell);
            ObjectPlacement before = item.Placement;
            _view.Synchronize();
            Button button = _root.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == $"Item {item.Identity.Key}");
            button.onClick.Invoke();
            Assert.That(_controller.SelectedItem, Is.EqualTo(item.Identity));
            Assert.That(item.Placement, Is.EqualTo(before));
        }

        [Test]
        public void ExistingEquipmentAuthorityMovesItemBetweenGridAndExactWornSlot()
        {
            PersistentObjectState armor = AddItem(3, ObjectType.Armor, 0, InventoryFootprint.OneCell);
            Assert.That(_controller.Equip(armor.Identity), Is.True);
            _view.Synchronize();
            Assert.That(_view.ProjectedItems.Single().WornLocation, Is.EqualTo(WornLocation.Armor));
            Assert.That(_root.GetComponentsInChildren<Transform>(true)
                .Any(value => value.name == $"Item {armor.Identity.Key}"), Is.True);
            Assert.That(_controller.Unequip(WornLocation.Armor), Is.True);
            _view.Synchronize();
            Assert.That(_view.ProjectedItems.Single().IsEquipped, Is.False);
            Assert.That(armor.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Contained));
        }

        [Test]
        public void NativeDragTargetsExactPaperDollSlotAndReturnsEquipmentToExactGridCell()
        {
            PersistentObjectState armor = AddItem(30, ObjectType.Armor, 0, new InventoryFootprint(2, 3));
            _view.Synchronize();
            RetailEquipmentSlotDefinition armorSlot = RetailInventoryEquipmentLayout.EquipmentSlots
                .Single(value => value.Location == WornLocation.Armor);

            Assert.That(InvokeInventoryDrop(armor.Identity, false,
                ScreenPoint(_view.WindowTransform, armorSlot.Rect.center)), Is.True);
            Assert.That(armor.Placement,
                Is.EqualTo(ObjectPlacement.EquippedBy(_session.PlayerState.Identity, WornLocation.Armor)));

            const int destination = 24;
            Rect gridCell = RetailInventoryEquipmentLayout.InventoryItemRect(destination, armor.InventoryFootprint);
            Assert.That(InvokeInventoryDrop(armor.Identity, true,
                ScreenPoint(_view.WindowTransform, gridCell.position),
                ScreenPoint(_view.WindowTransform, gridCell.position)), Is.True);
            Assert.That(armor.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_session.PlayerState.Identity)));
            Assert.That(armor.InventoryLocation, Is.EqualTo(destination));
        }

        [Test]
        public void SelectionCanBindToExistingQuickSlotAuthorityWithoutDuplicatingItemState()
        {
            PersistentObjectState item = AddItem(4, ObjectType.Weapon, 1, InventoryFootprint.OneCell);
            Assert.That(_controller.SelectItem(item.Identity), Is.True);
            Assert.That(_controller.AssignQuickSlotItem(5, item.Identity), Is.True);
            Assert.That(_session.Shortcuts.Get(5).PreferredItem, Is.EqualTo(item.Identity));
            Assert.That(_session.States.Values.Count(value => value.Identity == item.Identity), Is.EqualTo(1));
        }

        [Test]
        public void OriginalEnhancedOriginalRebuildPreservesInventoryAuthorityAndLogicalGeometry()
        {
            PersistentObjectState item = AddItem(5, ObjectType.Armor, 7, InventoryFootprint.OneCell);
            Rect before = RetailInventoryEquipmentLayout.InventoryItemRect(item.InventoryLocation,
                item.InventoryFootprint);
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            _skin.InvalidatePresentation();
            _view.Synchronize();
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            _skin.InvalidatePresentation();
            _view.Synchronize();
            Assert.That(RetailInventoryEquipmentLayout.InventoryItemRect(item.InventoryLocation,
                item.InventoryFootprint), Is.EqualTo(before));
            Assert.That(_view.ProjectedItems.Single().Identity, Is.EqualTo(item.Identity));
            Assert.That(_session.States.Values.Count(value => value.Identity == item.Identity), Is.EqualTo(1));
        }

        [Test]
        public void ClosingInventoryHidesOnlyPresentationAndDoesNotChangeState()
        {
            PersistentObjectState item = AddItem(6, ObjectType.Armor, 0, InventoryFootprint.OneCell);
            _view.Synchronize();
            _controller.Close();
            _view.Synchronize();
            Assert.That(_view.IsVisible, Is.False);
            Assert.That(item.ParentIdentity, Is.EqualTo(_session.PlayerState.Identity));
            Assert.That(_session.States.Values.Count(value => value.Identity == item.Identity), Is.EqualTo(1));
        }

        [Test]
        public void RebindingDoesNotDuplicateInventoryPresenter()
        {
            _view.Bind(_controller, _session, _skin);
            Assert.That(Object.FindObjectsByType<RetailInventoryEquipmentView>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(_root.GetComponentsInChildren<Canvas>(true)
                .Count(value => value.name == "Source-Faithful Inventory Equipment"), Is.EqualTo(1));
        }

        private PersistentObjectState AddItem(int sequence, ObjectType type, int location,
            InventoryFootprint footprint)
            => AddItemToOwner(sequence, type, location, footprint, _session.PlayerState.Identity);

        private PersistentObjectState AddItemToOwner(int sequence, ObjectType type, int location,
            InventoryFootprint footprint, ArcanumObjectId owner)
        {
            var source = new ObjectInstance(type, 9600 + sequence, null, 0x40000000u, 0, 0,
                oid: GuidBytes(sequence), parentOid: Bytes(owner),
                invLocation: location, invAid: InventoryAid);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                inventoryArtId: InventoryAid,
                stackQuantity: type == ObjectType.Ammo || type == ObjectType.Gold ? 1 : (int?)null,
                inventoryFootprint: footprint, inventoryLocation: location);
        }

        private ArcanumObjectId AddInventoryOwner(int sequence, ObjectType type)
        {
            var source = new ObjectInstance(type, 9500 + sequence, 1L | (1L << 32), 0x40000000u, 0, 0,
                oid: GuidBytes(sequence));
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false).Identity;
        }

        private void AssertInventoryDragPresenters(int expected)
        {
            RetailInventoryItemDragTarget[] targets =
                _root.GetComponentsInChildren<RetailInventoryItemDragTarget>(true);
            Assert.That(targets, Has.Length.EqualTo(expected));
            Assert.That(targets.All(value => value.GetComponent<CanvasGroup>() != null), Is.True,
                "every item type and every rebuilt presenter must establish the shared drag invariant");
        }

        private bool InvokeInventoryDrop(ArcanumObjectId item, bool equipped, Vector2 point,
            Vector2? itemTopLeft = null)
            => (bool)typeof(RetailInventoryEquipmentView)
                .GetMethod("OnInventoryDrop", System.Reflection.BindingFlags.Instance
                                              | System.Reflection.BindingFlags.NonPublic)
                .Invoke(_view, new object[] { item, equipped, point, itemTopLeft ?? point, null });

        private static Vector2 ScreenPoint(RectTransform window, Vector2 sourcePoint)
            => RectTransformUtility.WorldToScreenPoint(null,
                window.TransformPoint(new Vector3(sourcePoint.x - window.rect.width * window.pivot.x,
                    window.rect.height * (1f - window.pivot.y) - sourcePoint.y)));

        private static byte[] GuidBytes(int sequence)
            => Bytes(Parse($"G_{sequence:X8}_1111_2222_3333_444444444444"));

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
