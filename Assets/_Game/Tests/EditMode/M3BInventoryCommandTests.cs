using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M3B")]
    public sealed class M3BInventoryCommandTests
    {
        private const string Sector = "maps/test/1.sec";
        private const string OtherSector = "maps/test/2.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private PersistentPlayerState _player;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M3BInventoryCommandTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _navigation = _root.AddComponent<PlayerNavigationController>();
            _interaction = _root.AddComponent<PlayerInteractionController>();
            _session.BeginSector(Sector);
            SetProperty(_session, nameof(WorldMapSessionCoordinator.SelectedSector), Sector);
            SetProperty(_loader, nameof(WorldObjectSectorLoader.NavigationMap), Map());
            _player = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject runtime = Runtime("Player", ObjectType.Pc);
            _session.BindPlayer(Sector, _player, runtime);
            Assert.That(_navigation.TryBind(runtime), Is.True);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void PickupUsesSourceRangeZeroAndMutatesExactlyOnce()
        {
            PersistentObjectState item = Item(1, 1, 1);
            int changes = 0;
            _session.ObjectPlacementChanged += (state, _, _) => { if (state.Identity == item.Identity) changes++; };

            WorldInteractionCommand command = WorldInteractionCommand.PickUp(_player.Identity, item.Identity);
            Assert.That(InteractionRangeRules.ItemPickupRange, Is.Zero);
            Assert.That(_session.ExecuteInteraction(command).Code, Is.EqualTo(WorldInteractionResultCode.Success));
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_player.Identity)));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(_session.ExecuteInteraction(command).Code, Is.EqualTo(WorldInteractionResultCode.AlreadyContained));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void AdjacentPickupIsOutOfRangeButApproachesExactItemTile()
        {
            PersistentObjectState item = Item(1, 2, 1);
            WorldInteractionCommand command = WorldInteractionCommand.PickUp(_player.Identity, item.Identity);
            Assert.That(_session.ExecuteInteraction(command).Code, Is.EqualTo(WorldInteractionResultCode.OutOfRange));

            WorldInteractionResult accepted = _interaction.TryPickUp(item.Identity);
            Assert.That(accepted.Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(accepted.Command.InteractionPosition, Is.EqualTo(new Vector2Int(66, 1)));
            _navigation.AdvanceNavigation(10);
            _interaction.AdvanceInteraction();
            Assert.That(_interaction.LastResult.Value.Code, Is.EqualTo(WorldInteractionResultCode.Success));
            Assert.That(item.ParentIdentity, Is.EqualTo(_player.Identity));
        }

        [Test]
        public void UnreachablePickupIsRejectedWithoutMutation()
        {
            SetProperty(_loader, nameof(WorldObjectSectorLoader.NavigationMap), Map(new Vector2Int(5, 5)));
            PersistentObjectState item = Item(1, 5, 5);
            ObjectPlacement before = item.Placement;
            Assert.That(_interaction.TryPickUp(item.Identity).Code,
                Is.EqualTo(WorldInteractionResultCode.NoReachableInteractionPosition));
            Assert.That(item.Placement, Is.EqualTo(before));
        }

        [Test]
        public void ManualMoveCancelsPickupAndNeverExecutesIt()
        {
            PersistentObjectState item = Item(1, 8, 1);
            Assert.That(_interaction.TryPickUp(item.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_navigation.TrySetDestination(new Vector2Int(2, 4)), Is.True);
            Assert.That(_interaction.LastResult.Value.Code, Is.EqualTo(WorldInteractionResultCode.Cancelled));
            _navigation.AdvanceNavigation(100);
            _interaction.AdvanceInteraction();
            Assert.That(item.Placement.Kind, Is.EqualTo(ObjectPlacementKind.World));
        }

        [Test]
        public void NewTargetReplacesPickupWithoutExecutingPriorTarget()
        {
            PersistentObjectState first = Item(1, 8, 1);
            PersistentObjectState second = Item(2, 8, 4);
            Assert.That(_interaction.TryPickUp(first.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_interaction.TryPickUp(second.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_interaction.PendingCommand.Value.Target, Is.EqualTo(second.Identity));
            Assert.That(first.Placement.Kind, Is.EqualTo(ObjectPlacementKind.World));
        }

        [Test]
        public void ItemDisappearingBeforeArrivalFailsSafely()
        {
            PersistentObjectState item = Item(1, 8, 1);
            Assert.That(_interaction.TryPickUp(item.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(_player.Identity)).Succeeded, Is.True);
            _session.UnbindPresentation(Sector, item.Identity);
            _interaction.AdvanceInteraction();
            Assert.That(_interaction.LastResult.Value.Code, Is.EqualTo(WorldInteractionResultCode.TargetNotFound));
            Assert.That(item.ParentIdentity, Is.EqualTo(_player.Identity));
        }

        [Test]
        public void ItemMovingBeforeArrivalCannotExecuteAtStalePosition()
        {
            PersistentObjectState item = Item(1, 8, 1);
            Assert.That(_interaction.TryPickUp(item.Identity).Code, Is.EqualTo(WorldInteractionResultCode.Approaching));
            Assert.That(_session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(12, 12))).Succeeded, Is.True);
            _navigation.AdvanceNavigation(100);
            _interaction.AdvanceInteraction();
            Assert.That(_interaction.LastResult.Value.Code,
                Is.EqualTo(WorldInteractionResultCode.NoReachableInteractionPosition));
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.InWorld(Sector, new Vector2(12, 12))));
        }

        [Test]
        public void DropRequiresExactActorOwnershipAndValidActiveTile()
        {
            PersistentObjectState item = Item(1, 1, 1);
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.PickUp(_player.Identity, item.Identity)).IsSuccess,
                Is.True);
            ObjectPlacement contained = item.Placement;
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(_player.Identity, item.Identity,
                OtherSector, new Vector2(4, 4))).Code, Is.EqualTo(WorldInteractionResultCode.InvalidDestination));
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(_player.Identity, item.Identity,
                Sector, new Vector2(64, 4))).Code, Is.EqualTo(WorldInteractionResultCode.InvalidDestination));
            Assert.That(item.Placement, Is.EqualTo(contained));

            var otherActor = ArcanumObjectId.CreateGuid(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(otherActor, item.Identity,
                Sector, new Vector2(4, 4))).Code, Is.EqualTo(WorldInteractionResultCode.ActorNotFound));
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(_player.Identity, item.Identity,
                Sector, new Vector2(4, 4))).IsSuccess, Is.True);
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.InWorld(Sector, new Vector2(4, 4))));
        }

        [Test]
        public void NoDropFlagRejectsDropWithoutMutation()
        {
            PersistentObjectState item = Item(1, 1, 1, itemFlags: 0x20);
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.PickUp(_player.Identity, item.Identity)).IsSuccess,
                Is.True);
            ObjectPlacement before = item.Placement;
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(_player.Identity, item.Identity,
                Sector, new Vector2(3, 3))).Code, Is.EqualTo(WorldInteractionResultCode.NotDroppable));
            Assert.That(item.Placement, Is.EqualTo(before));
        }

        [Test]
        public void TransferSupportsPlayerToContainerAndBackButNotUnrelatedOwners()
        {
            PersistentObjectState item = Item(1, 1, 1);
            PersistentObjectState container = State(Source(2, ObjectType.Container, 3, 3));
            PersistentObjectState unrelated = State(Source(3, ObjectType.Container, 4, 4));
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.PickUp(_player.Identity, item.Identity)).IsSuccess,
                Is.True);
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Transfer(_player.Identity, item.Identity,
                container.Identity)).IsSuccess, Is.True);
            ObjectPlacement inContainer = item.Placement;
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Transfer(_player.Identity, item.Identity,
                unrelated.Identity)).Code, Is.EqualTo(WorldInteractionResultCode.SourceOwnerMismatch));
            Assert.That(item.Placement, Is.EqualTo(inContainer));
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Transfer(_player.Identity, item.Identity,
                _player.Identity)).IsSuccess, Is.True);
            Assert.That(item.ParentIdentity, Is.EqualTo(_player.Identity));
        }

        [Test]
        public void DynamicItemUsesIdenticalCommandPathAndRetainsIdentity()
        {
            var proto = new ObjectProtoInfo(7000, ObjectType.Food, 0x40000000u);
            _session.BindPrototypeSource(number => number == 7000 ? proto : null);
            ItemCreationResult created = _session.CreateItem(7000,
                ObjectPlacement.InWorld(Sector, new Vector2(1, 1)));
            WorldObject runtime = Runtime("DynamicItem", ObjectType.Food);
            _session.Bind(Sector, created.State, runtime);
            ArcanumObjectId identity = created.State.Identity;

            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.PickUp(_player.Identity, identity)).IsSuccess,
                Is.True);
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(_player.Identity, identity,
                Sector, new Vector2(6, 6))).IsSuccess, Is.True);
            Assert.That(created.State.Identity, Is.EqualTo(identity));
            Assert.That(identity.Type, Is.EqualTo(ArcanumObjectIdType.SessionDynamic));
        }

        [Test, Category("RealData")]
        public void RealGroundItemSurvivesPickupCrossingContainerTransferDropAndReload()
        {
            const string groundSector = "maps/arcanum1-024-fixed/101602821845.sec";
            const string groundItemKey = "G_8781D726_74FE_0846_AD0A_88EE591B6383";
            const string containerSector = "maps/arcanum1-024-fixed/101602821844.sec";
            const string containerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";

            _session.UnloadSector(Sector);
            _loader = _root.AddComponent<WorldObjectSectorLoader>();
            _loader.BindSessionAuthority();
            Assert.That(_session.SelectSector(groundSector), Is.True);
            PersistentObjectState item = _session.States.Values.Single(state => state.Identity.Key == groundItemKey);
            Assert.That(item.Type, Is.EqualTo(ObjectType.Food));
            Assert.That(item.PrototypeNumber, Is.EqualTo(10078));
            Assert.That(item.TilePosition, Is.EqualTo(new Vector2(10, 46)));
            Assert.That(item.UseScriptNum, Is.Zero);
            Assert.That(item.ItemFlags, Is.EqualTo(0x180));
            Assert.That(_session.TryGetLoadedObject(item.Identity, out _), Is.True);

            Assert.That(_session.TryTransitionPlayer(groundSector, item.TilePosition, _player.ArtId), Is.True);
            ArcanumObjectId identity = item.Identity;
            Assert.That(_session.ExecuteInteraction(
                WorldInteractionCommand.PickUp(_player.Identity, identity)).IsSuccess, Is.True);
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_player.Identity)));
            Assert.That(ProjectionCount(item), Is.Zero);
            Assert.That(_session.ReloadSelectedSector(), Is.True);
            Assert.That(item.Identity, Is.EqualTo(identity));
            Assert.That(item.ParentIdentity, Is.EqualTo(_player.Identity));
            Assert.That(ProjectionCount(item), Is.Zero, "source reload must not respawn the collected item");

            Assert.That(_session.TryTransitionPlayer(containerSector, new Vector2(36, 58), _player.ArtId), Is.True);
            PersistentObjectState container = _session.States.Values.Single(state => state.Identity.Key == containerKey);
            Assert.That(container.PrototypeNumber, Is.EqualTo(3052));
            Assert.That(_session.ExecuteInteraction(
                WorldInteractionCommand.Transfer(_player.Identity, identity, container.Identity)).IsSuccess, Is.True);
            Assert.That(item.ParentIdentity, Is.EqualTo(container.Identity));
            Assert.That(_session.ExecuteInteraction(
                WorldInteractionCommand.Transfer(_player.Identity, identity, _player.Identity)).IsSuccess, Is.True);
            Assert.That(item.ParentIdentity, Is.EqualTo(_player.Identity));

            Vector2 dropTile = new(12, 18);
            Assert.That(_session.ExecuteInteraction(
                WorldInteractionCommand.Drop(_player.Identity, identity, containerSector, dropTile)).IsSuccess, Is.True);
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.InWorld(containerSector, dropTile)));
            Assert.That(ProjectionCount(item), Is.EqualTo(1));
            _loader.RebuildVisuals();
            Assert.That(ProjectionCount(item), Is.EqualTo(1));
            Assert.That(_session.ReloadSelectedSector(), Is.True);
            Assert.That(item.Identity, Is.EqualTo(identity));
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.InWorld(containerSector, dropTile)));
            Assert.That(ProjectionCount(item), Is.EqualTo(1));
        }

        [Test]
        public void AlphaSelectionReturnsFrontmostInventoryCapableObject()
        {
            Texture2D texture = SolidTexture();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 1);
            WorldObject item = Selectable("Item", Id(10), ObjectType.Armor, sprite, 5);
            WorldObject portal = Selectable("Portal", Id(11), ObjectType.Portal, sprite, 4);
            Assert.That(WorldObjectTargetSelector.TrySelectInteractionTarget(
                new[] { item.View.GetComponent<WorldObjectSpriteOwner>(), portal.View.GetComponent<WorldObjectSpriteOwner>() },
                Vector2.zero, out ArcanumObjectId selected, out ObjectType type), Is.True);
            Assert.That(selected, Is.EqualTo(item.Identity));
            Assert.That(type, Is.EqualTo(ObjectType.Armor));
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        private PersistentObjectState Item(int id, int x, int y, int itemFlags = 0)
        {
            ObjectInstance source = Source(id, ObjectType.Generic, x, y);
            PersistentObjectState state = _session.GetOrCreate(source, source.Identity, Sector,
                source.CurrentArtId.Value, false, false, itemFlags);
            _session.Bind(Sector, state, Runtime("Item" + id, ObjectType.Generic));
            return state;
        }

        private PersistentObjectState State(ObjectInstance source)
            => _session.GetOrCreate(source, Sector, source.CurrentArtId ?? 0x40000000u, false, false);

        private int ProjectionCount(PersistentObjectState state)
            => _loader.SpriteOwners.Count(owner => owner != null && owner.WorldObject != null
                                                   && owner.WorldObject.Identity == state.Identity);

        private WorldObject Runtime(string name, ObjectType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            return runtime;
        }

        private WorldObject Selectable(string name, ArcanumObjectId identity, ObjectType type, Sprite sprite,
            int sortingOrder)
        {
            WorldObject runtime = Runtime(name, type);
            runtime.Identity = identity;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(runtime.transform);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            var owner = visual.AddComponent<WorldObjectSpriteOwner>();
            runtime.View = renderer;
            SetField(owner, "_worldObject", runtime);
            return runtime;
        }

        private static ObjectInstance Source(int id, ObjectType type, int x, int y)
            => new(type, 5000 + id, Location(x, y), 0x40000000u, 0, 0, oid: Bytes(id));

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static byte[] Bytes(int value)
        {
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            return bytes;
        }

        private static ArcanumObjectId Id(int value) => ArcanumObjectId.FromBytes(Bytes(value));

        private static Texture2D SolidTexture()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            return texture;
        }

        private static SectorNavigationMap Map(params Vector2Int[] blocked)
        {
            var mask = new bool[SectorTerrain.TileCount];
            foreach (Vector2Int tile in blocked) mask[tile.y * SectorCoordinate.Size + tile.x] = true;
            TileNameTable names = TileNameTable.FromMes(MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new SectorTerrain(new uint[SectorTerrain.TileCount]), mask, names);
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, name);
            property.SetValue(target, value);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
