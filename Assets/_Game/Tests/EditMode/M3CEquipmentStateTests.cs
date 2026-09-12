using System;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M3C")]
    public sealed class M3CEquipmentStateTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _player;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M3CEquipmentStateTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BeginSector(Sector);
            _player = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void SourceWornLocationsRetainExactValuesAndRejectUnknownValues()
        {
            Assert.That(Enum.GetValues(typeof(WornLocation)).Cast<WornLocation>().Select(value => (int)value),
                Is.EqualTo(Enumerable.Range(1000, 9)));
            Assert.That(WornLocations.TryFromSource(1000, out WornLocation helmet), Is.True);
            Assert.That(helmet, Is.EqualTo(WornLocation.Helmet));
            Assert.That(WornLocations.TryFromSource(1008, out WornLocation boots), Is.True);
            Assert.That(boots, Is.EqualTo(WornLocation.Boots));
            Assert.That(WornLocations.TryFromSource(999, out _), Is.False);
            Assert.That(WornLocations.TryFromSource(1009, out _), Is.False);
        }

        [Test]
        public void AuthoredWornLocationBecomesTypedEquipmentAndNeverWorldPresentation()
        {
            ObjectInstance source = Source(1, ObjectType.Armor, parent: 20, invLocation: 1008,
                invAid: ArmorAid(4));
            PersistentObjectState item = State(source);
            Assert.That(item.Placement,
                Is.EqualTo(ObjectPlacement.EquippedBy(Id(20), WornLocation.Boots)));
            Assert.That(item.ParentIdentity, Is.EqualTo(Id(20)));
            Assert.That(_session.IsWorldPresentationEligible(item, Sector), Is.False);
            Assert.That(_session.ChildrenOf(Id(20)), Is.Empty, "ordinary containment is enumerated separately");
            Assert.That(_session.EquippedItems(Id(20)).Select(state => state.Identity),
                Is.EqualTo(new[] { item.Identity }));
        }

        [Test]
        public void EquipAndUnequipPreserveIdentityParentAndOrdinaryContainment()
        {
            PersistentObjectState boots = Owned(Source(1, ObjectType.Armor, invAid: ArmorAid(4)));
            ArcanumObjectId identity = boots.Identity;
            Assert.That(_session.EquipItem(_player.Identity, identity, WornLocation.Boots).Succeeded, Is.True);
            Assert.That(boots.Identity, Is.EqualTo(identity));
            Assert.That(boots.Placement, Is.EqualTo(ObjectPlacement.EquippedBy(_player.Identity, WornLocation.Boots)));
            Assert.That(_session.ChildrenOf(_player.Identity), Is.Empty);
            Assert.That(_session.TryGetWornLocation(identity, out WornLocation location), Is.True);
            Assert.That(location, Is.EqualTo(WornLocation.Boots));

            Assert.That(_session.UnequipItem(_player.Identity, WornLocation.Boots).Succeeded, Is.True);
            Assert.That(boots.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_player.Identity)));
            Assert.That(_session.ChildrenOf(_player.Identity), Is.EqualTo(new[] { identity }));
            Assert.That(_session.TryGetEquippedItem(_player.Identity, WornLocation.Boots, out _), Is.False);
        }

        [Test]
        public void WorldItemMustBePickedUpBeforeEquip()
        {
            PersistentObjectState weapon = State(Source(1, ObjectType.Weapon));
            ObjectPlacement before = weapon.Placement;
            Assert.That(_session.EquipItem(_player.Identity, weapon.Identity, WornLocation.Weapon).Code,
                Is.EqualTo(EquipmentResultCode.ItemNotOwned));
            Assert.That(weapon.Placement, Is.EqualTo(before));
            Assert.That(_session.TransferItem(weapon.Identity, before,
                ObjectPlacement.ContainedBy(_player.Identity)).Succeeded, Is.True);
            Assert.That(_session.EquipItem(_player.Identity, weapon.Identity, WornLocation.Weapon).Succeeded, Is.True);
        }

        [Test]
        public void InvalidActorItemOwnerSlotAndCompatibilityFailWithoutMutation()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container));
            PersistentObjectState boots = Owned(Source(1, ObjectType.Armor, invAid: ArmorAid(4)));
            PersistentObjectState other = State(Source(2, ObjectType.Armor, parent: 20, invAid: ArmorAid(4)));
            ObjectPlacement before = boots.Placement;
            Assert.That(_session.EquipItem(Id(99), boots.Identity, WornLocation.Boots).Code,
                Is.EqualTo(EquipmentResultCode.ActorNotFound));
            Assert.That(_session.EquipItem(container.Identity, boots.Identity, WornLocation.Boots).Code,
                Is.EqualTo(EquipmentResultCode.InvalidActorType));
            Assert.That(_session.EquipItem(_player.Identity, Id(99), WornLocation.Boots).Code,
                Is.EqualTo(EquipmentResultCode.ItemNotFound));
            Assert.That(_session.EquipItem(_player.Identity, other.Identity, WornLocation.Boots).Code,
                Is.EqualTo(EquipmentResultCode.ItemNotOwned));
            Assert.That(_session.EquipItem(_player.Identity, boots.Identity, (WornLocation)999).Code,
                Is.EqualTo(EquipmentResultCode.InvalidWornLocation));
            Assert.That(_session.EquipItem(_player.Identity, boots.Identity, WornLocation.Helmet).Code,
                Is.EqualTo(EquipmentResultCode.IncompatibleWornLocation));
            Assert.That(boots.Placement, Is.EqualTo(before));
        }

        [Test]
        public void RingEligibilitySupportsBothDistinctLocationsAndEnumerationIsDeterministic()
        {
            PersistentObjectState second = Owned(Source(2, ObjectType.Armor, invAid: ArmorAid(5)));
            PersistentObjectState first = Owned(Source(1, ObjectType.Armor, invAid: ArmorAid(5)));
            Assert.That(_session.EquipItem(_player.Identity, second.Identity, WornLocation.Ring2).Succeeded, Is.True);
            Assert.That(_session.EquipItem(_player.Identity, first.Identity, WornLocation.Ring1).Succeeded, Is.True);
            Assert.That(_session.EquippedItems(_player.Identity).Select(state => state.Identity),
                Is.EqualTo(new[] { first.Identity, second.Identity }));
            Assert.That(_session.TryGetEquippedItem(_player.Identity, WornLocation.Ring1, out var left), Is.True);
            Assert.That(_session.TryGetEquippedItem(_player.Identity, WornLocation.Ring2, out var right), Is.True);
            Assert.That(left.Identity, Is.EqualTo(first.Identity));
            Assert.That(right.Identity, Is.EqualTo(second.Identity));
        }

        [Test]
        public void OccupiedSlotReplacementCommitsBothPlacementsBeforeObserversRun()
        {
            PersistentObjectState oldItem = Owned(Source(1, ObjectType.Weapon));
            PersistentObjectState newItem = Owned(Source(2, ObjectType.Weapon));
            Assert.That(_session.EquipItem(_player.Identity, oldItem.Identity, WornLocation.Weapon).Succeeded, Is.True);
            int events = 0;
            bool observersSawFinalPair = true;
            _session.ObjectPlacementChanged += (_, _, _) =>
            {
                events++;
                observersSawFinalPair &= oldItem.Placement == ObjectPlacement.ContainedBy(_player.Identity)
                                         && newItem.Placement == ObjectPlacement.EquippedBy(
                                             _player.Identity, WornLocation.Weapon);
            };

            EquipmentTransactionResult result = _session.EquipItem(_player.Identity, newItem.Identity,
                WornLocation.Weapon);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.DisplacedItemIdentity, Is.EqualTo(oldItem.Identity));
            Assert.That(events, Is.EqualTo(2));
            Assert.That(observersSawFinalPair, Is.True);
            Assert.That(_session.ChildrenOf(_player.Identity), Is.EqualTo(new[] { oldItem.Identity }));
        }

        [Test]
        public void OccupiedSlotReplacementRollsBackWhenOldItemCannotBeRemoved()
        {
            ObjectInstance oldSource = new(ObjectType.Weapon, 5001, 1L, 0x40000000u, 0, 0,
                oid: Bytes(1), parentOid: PlayerBytes(), invLocation: (int)WornLocation.Weapon)
            {
                ItemFlags = 0x20,
                WeaponFlags = 0,
                Weapon = new WeaponFields(),
            };
            PersistentObjectState oldItem = State(oldSource);
            PersistentObjectState newItem = Owned(Source(2, ObjectType.Weapon));
            ObjectPlacement oldBefore = oldItem.Placement;
            ObjectPlacement newBefore = newItem.Placement;
            int events = 0;
            _session.ObjectPlacementChanged += (_, _, _) => events++;

            EquipmentTransactionResult result = _session.EquipItem(_player.Identity, newItem.Identity,
                WornLocation.Weapon);
            Assert.That(result.Code, Is.EqualTo(EquipmentResultCode.NotRemovable));
            Assert.That(oldItem.Placement, Is.EqualTo(oldBefore));
            Assert.That(newItem.Placement, Is.EqualTo(newBefore));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void DuplicateAuthoredEquipmentMembershipIsRejectedBeforeRegistration()
        {
            ObjectInstance first = new(ObjectType.Weapon, 5001, 1L, 0x40000000u, 0, 0,
                oid: Bytes(1), parentOid: PlayerBytes(), invLocation: (int)WornLocation.Weapon);
            ObjectInstance second = new(ObjectType.Weapon, 5002, 1L, 0x40000000u, 0, 0,
                oid: Bytes(2), parentOid: PlayerBytes(), invLocation: (int)WornLocation.Weapon);
            Assert.That(_session.ValidateSector(Sector, new[] { first, second }, out string error), Is.False);
            Assert.That(error, Does.Contain("is occupied by both"));
            Assert.That(_session.States, Is.Empty);
        }

        [Test]
        public void FixedTwoHandedWeaponAndShieldConflictFailsAtomically()
        {
            PersistentObjectState shield = Owned(Source(1, ObjectType.Armor, invAid: ArmorAid(1)));
            PersistentObjectState twoHanded = Owned(Source(2, ObjectType.Weapon, weaponFlags: 0x0C));
            Assert.That(_session.EquipItem(_player.Identity, shield.Identity, WornLocation.Shield).Succeeded, Is.True);
            Assert.That(_session.EquipItem(_player.Identity, twoHanded.Identity, WornLocation.Weapon).Code,
                Is.EqualTo(EquipmentResultCode.NoFreeHand));
            Assert.That(twoHanded.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_player.Identity)));
            Assert.That(_session.TryGetEquippedItem(_player.Identity, WornLocation.Shield, out var retained), Is.True);
            Assert.That(retained.Identity, Is.EqualTo(shield.Identity));
        }

        [Test]
        public void GenericTorchUsesShieldLocationAndEquippedItemsRequireEquipmentCommands()
        {
            PersistentObjectState torch = Owned(Source(1, ObjectType.Generic, genericFlags: 1));
            Assert.That(_session.EquipItem(_player.Identity, torch.Identity, WornLocation.Shield).Succeeded, Is.True);
            Assert.That(_session.TransferItem(torch.Identity, torch.Placement,
                    ObjectPlacement.InWorld(Sector, Vector2.zero)).Code,
                Is.EqualTo(InventoryResultCode.EquipmentCommandRequired));
            Assert.That(torch.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Equipped));
            Assert.That(_session.UnequipItem(_player.Identity, WornLocation.Shield).Succeeded, Is.True);
        }

        [Test]
        public void DynamicItemUsesSameEquipmentApiAndIdentitySurvivesUnloadReload()
        {
            var prototype = new ObjectProtoInfo(7000, ObjectType.Armor, 0x40000000u, invAid: ArmorAid(4));
            _session.BindPrototypeSource(number => number == 7000 ? prototype : null);
            ItemCreationResult created = _session.CreateItem(7000, ObjectPlacement.ContainedBy(_player.Identity));
            ArcanumObjectId identity = created.State.Identity;
            Assert.That(_session.EquipItem(_player.Identity, identity, WornLocation.Boots).Succeeded, Is.True);
            _session.UnloadSector(Sector);
            _session.BeginSector(Sector);
            Assert.That(created.State.Identity, Is.EqualTo(identity));
            Assert.That(created.State.Placement,
                Is.EqualTo(ObjectPlacement.EquippedBy(_player.Identity, WornLocation.Boots)));
            Assert.That(_session.EquippedItems(_player.Identity).Single().Identity, Is.EqualTo(identity));
        }

        [Test, Category("RealData")]
        public void RealAuthoredArmorSurvivesEquipTraversalReloadUnequipAndDrop()
        {
            const string fixtureSector = "maps/arcanum1-024-fixed/101602821844.sec";
            const string adjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
            const string containerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
            const string itemKey = "G_0435F503_6600_6342_97B2_6D9E1A85A2F2";
            var worldTile = new Vector2(12, 18);
            _session.UnloadSector(Sector);
            var loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.BindSessionAuthority();
            Assert.That(_session.SelectSector(fixtureSector), Is.True);
            PersistentObjectState container = _session.States.Values.Single(state => state.Identity.Key == containerKey);
            PersistentObjectState item = _session.States.Values.Single(state => state.Identity.Key == itemKey);
            Assert.That(container.PrototypeNumber, Is.EqualTo(3052));
            Assert.That(item.Type, Is.EqualTo(ObjectType.Armor));
            Assert.That(item.PrototypeNumber, Is.EqualTo(8127));
            Assert.That(item.InventoryArtId, Is.EqualTo(0x60041082u));
            Assert.That(item.ItemFlags, Is.Zero);
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(container.Identity)));
            Assert.That(WorldMapSessionCoordinator.TryGetNaturalWornLocation(item, out WornLocation location), Is.True);
            Assert.That(location, Is.EqualTo(WornLocation.Armor));

            Assert.That(_session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.InWorld(fixtureSector, worldTile)).Succeeded, Is.True);
            Assert.That(_session.TryTransitionPlayer(fixtureSector, worldTile, _player.ArtId), Is.True);
            Assert.That(_session.ExecuteInteraction(
                WorldInteractionCommand.PickUp(_player.Identity, item.Identity)).IsSuccess, Is.True);
            ArcanumObjectId identity = item.Identity;
            Assert.That(_session.EquipItem(_player.Identity, identity, location).Succeeded, Is.True);
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == identity), Is.Zero);
            loader.RebuildVisuals();
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.EquippedBy(_player.Identity, location)));

            Assert.That(_session.TryTransitionPlayer(adjacentSector, new Vector2(0, 18), _player.ArtId), Is.True);
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.EquippedBy(_player.Identity, location)));
            Assert.That(_session.TryTransitionPlayer(fixtureSector, new Vector2(63, 18), _player.ArtId), Is.True);
            Assert.That(_session.ReloadSelectedSector(), Is.True);
            Assert.That(item.Identity, Is.EqualTo(identity));
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.EquippedBy(_player.Identity, location)));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == identity), Is.Zero);

            Assert.That(_session.UnequipItem(_player.Identity, location).Succeeded, Is.True);
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_player.Identity)));
            Vector2 dropTile = _session.PlayerState.TilePosition;
            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(_player.Identity, identity,
                fixtureSector, dropTile)).IsSuccess, Is.True);
            Assert.That(item.Placement, Is.EqualTo(ObjectPlacement.InWorld(fixtureSector, dropTile)));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == identity), Is.EqualTo(1));
            Assert.That(_session.States.Values.Count(state => state.Identity == identity), Is.EqualTo(1));
        }

        private PersistentObjectState Owned(ObjectInstance source)
        {
            PersistentObjectState state = State(source);
            Assert.That(_session.TransferItem(state.Identity, state.Placement,
                ObjectPlacement.ContainedBy(_player.Identity)).Succeeded, Is.True);
            return state;
        }

        private PersistentObjectState State(ObjectInstance source)
            => _session.GetOrCreate(source, Sector, source.CurrentArtId ?? 0x40000000u, false, false,
                source.ItemFlags ?? 0, source.InvAid, source.WeaponFlags ?? 0, source.GenericFlags ?? 0);

        private static ObjectInstance Source(int id, ObjectType type, int? parent = null, int invLocation = -1,
            uint? invAid = null, int itemFlags = 0, int weaponFlags = 0, int genericFlags = 0)
            => new ObjectInstance(type, 5000 + id, 1L, 0x40000000u, 0, 0, oid: Bytes(id),
                parentOid: parent.HasValue ? Bytes(parent.Value) : null, invLocation: invLocation, invAid: invAid)
            {
                ItemFlags = itemFlags,
                WeaponFlags = type == ObjectType.Weapon ? weaponFlags : null,
                Weapon = type == ObjectType.Weapon ? new WeaponFields { Flags = weaponFlags } : null,
                GenericFlags = type == ObjectType.Generic ? genericFlags : null,
            };

        private static uint ArmorAid(int coverage) => (uint)(coverage << 14);

        private static byte[] Bytes(int value)
        {
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            return bytes;
        }

        private static byte[] PlayerBytes()
        {
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            Array.Copy(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01").ToByteArray(), 0, bytes, 8, 16);
            return bytes;
        }

        private static ArcanumObjectId Id(int value) => ArcanumObjectId.FromBytes(Bytes(value));
    }
}
