using System;
using System.IO;
using System.Linq;
using Arcanum.Formats.Objects;
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
            foreach (int id in new[] { 221, 223, 233, 241, 242, 243, 244, 245, 246, 247, 248, 249,
                         340, 676, 677 })
                Assert.That(_retail.TryResolve(new UiAssetKey(id), out _), Is.True, $"source id {id}");
            Assert.That(_retail.TryResolveInventoryItem(InventoryAid, out RetailItemArtAsset item), Is.True);
            Assert.That(item.SourcePath, Does.StartWith("art/item/"));
            Assert.That(item.LogicalSize.x, Is.GreaterThan(0));
            Assert.That(item.LogicalSize.y, Is.GreaterThan(0));
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
        {
            var source = new ObjectInstance(type, 9600 + sequence, null, 0x40000000u, 0, 0,
                oid: GuidBytes(sequence), parentOid: Bytes(_session.PlayerState.Identity),
                invLocation: location, invAid: InventoryAid);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                inventoryArtId: InventoryAid, inventoryFootprint: footprint, inventoryLocation: location);
        }

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
