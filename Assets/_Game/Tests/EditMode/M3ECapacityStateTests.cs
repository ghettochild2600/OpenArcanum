using System;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M3ECapacity")]
    public sealed class M3ECapacityStateTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _player;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M3ECapacityStateTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BeginSector(Sector);
            _player = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void EffectiveStoredWeightAndAuditedInventoryFootprintsAreRetained()
        {
            PersistentObjectState food = State(Source(1, ObjectType.Food, 10078, weight: 7),
                unitWeight: 50, footprint: new InventoryFootprint(1, 2));
            PersistentObjectState ammo = State(Source(2, ObjectType.Ammo, 7059, quantity: 60),
                unitWeight: 1, footprint: new InventoryFootprint(2, 1));

            Assert.That(food.UnitWeight, Is.EqualTo(50));
            Assert.That(food.InventoryFootprint, Is.EqualTo(new InventoryFootprint(1, 2)));
            Assert.That(ammo.UnitWeight, Is.EqualTo(1));
            Assert.That(ammo.InventoryFootprint, Is.EqualTo(new InventoryFootprint(2, 1)));
        }

        [Test]
        public void OrdinaryAmmoAndGoldFollowSourceWeightUnitsAndQuantityRules()
        {
            PersistentObjectState food = State(Source(1, ObjectType.Food, 10078), unitWeight: 50);
            PersistentObjectState ammo = State(Source(2, ObjectType.Ammo, 7059, quantity: 60), unitWeight: 1);
            PersistentObjectState tinyAmmo = State(Source(3, ObjectType.Ammo, 7059, quantity: 3), unitWeight: 25);
            PersistentObjectState gold = State(Source(4, ObjectType.Gold, 9056, quantity: 4000), unitWeight: 99);
            PersistentObjectState weightless = State(Source(5, ObjectType.Food, 10079), unitWeight: 0);

            Assert.That(_session.InventoryCapacity.GetTotalWeight(food.Identity), Is.EqualTo(50));
            Assert.That(_session.InventoryCapacity.GetTotalWeight(ammo.Identity), Is.EqualTo(15));
            Assert.That(_session.InventoryCapacity.GetTotalWeight(tinyAmmo.Identity), Is.Zero);
            Assert.That(_session.InventoryCapacity.GetTotalWeight(gold.Identity), Is.Zero);
            Assert.That(_session.InventoryCapacity.GetTotalWeight(weightless.Identity), Is.Zero);
        }

        [Test]
        public void InventoryLoadIncludesDirectContainedAndEquippedItems()
        {
            PersistentObjectState contained = Owned(Source(1, ObjectType.Food, 10078), unitWeight: 50);
            PersistentObjectState equipped = Owned(Source(2, ObjectType.Armor, 8000, invLocation: 1008),
                unitWeight: 200);
            State(Source(3, ObjectType.Food, 10078), unitWeight: 999);

            Assert.That(equipped.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Equipped));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(250));
            Assert.That(_session.ChildrenOf(_player.Identity), Is.EqualTo(new[] { contained.Identity }));
        }

        [Test]
        public void CarryCapacityUsesEffectiveStrengthAndSourceClamp()
        {
            Assert.That(_session.InventoryCapacity.GetCarryCapacity(_player.Identity), Is.EqualTo(4000));
            _session.Characters.SetGender(_player.Identity, CharacterGender.Female);
            Assert.That(_session.InventoryCapacity.GetCarryCapacity(_player.Identity), Is.EqualTo(3500));

            PersistentObjectState npc = State(Source(20, ObjectType.Npc, 17101));
            _session.Characters.GetOrCreateSourceCharacter(npc.Identity, ObjectType.Npc, npc.PrototypeNumber,
                StatSource(9, CharacterRace.Human, CharacterGender.Male), null);
            Assert.That(_session.InventoryCapacity.GetCarryCapacity(npc.Identity), Is.EqualTo(4500));

            PersistentObjectState ogre = State(Source(21, ObjectType.Npc, 17102));
            _session.Characters.GetOrCreateSourceCharacter(ogre.Identity, ObjectType.Npc, ogre.PrototypeNumber,
                StatSource(20, CharacterRace.Ogre, CharacterGender.Male), null);
            Assert.That(_session.InventoryCapacity.GetCarryCapacity(ogre.Identity), Is.EqualTo(10000));
        }

        [Test]
        public void ContainerCapacityIsFixedGridAndLoadCountsOccupiedCellsOncePerStack()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3052));
            OwnedBy(Source(1, ObjectType.Food, 10078), container.Identity, 50, new InventoryFootprint(1, 2), 0);
            OwnedBy(Source(2, ObjectType.Ammo, 7059, quantity: 60), container.Identity, 1,
                new InventoryFootprint(2, 1), 3);

            Assert.That(_session.InventoryCapacity.GetContainerCapacity(container.Identity), Is.EqualTo(960));
            Assert.That(_session.InventoryCapacity.GetContainerLoad(container.Identity), Is.EqualTo(4));
        }

        [Test]
        public void CapacityQueriesRejectNonOwnersAndNonContainers()
        {
            PersistentObjectState item = State(Source(1, ObjectType.Food, 10078));
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3052));

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => _session.InventoryCapacity.GetInventoryLoad(item.Identity));
            Assert.Throws<ArgumentException>(() => _session.InventoryCapacity.GetCarryCapacity(container.Identity));
            Assert.Throws<ArgumentException>(() =>
                _session.InventoryCapacity.GetContainerCapacity(_player.Identity));
            Assert.Throws<ArgumentException>(() => _session.InventoryCapacity.GetContainerLoad(_player.Identity));
        }

        [Test]
        public void TransferBelowCarryBoundaryPreservesIdentityAndAssignsFirstGridCell()
        {
            PersistentObjectState item = State(Source(1, ObjectType.Food, 10078), unitWeight: 3999,
                footprint: new InventoryFootprint(2, 2));

            InventoryTransferResult result = _session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(_player.Identity));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(item.Identity, Is.EqualTo(Id(1)));
            Assert.That(item.ParentIdentity, Is.EqualTo(_player.Identity));
            Assert.That(item.InventoryLocation, Is.Zero);
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(3999));
        }

        [Test]
        public void TransferAtExactCarryBoundarySucceeds()
        {
            PersistentObjectState item = State(Source(1, ObjectType.Food, 10078), unitWeight: 4000);
            InventoryTransferResult result = _session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(_player.Identity));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(4000));
            Assert.That(item.InventoryLocation, Is.EqualTo(0));
        }

        [Test]
        public void OverweightTransferRejectsAtomicallyBeforeObservers()
        {
            PersistentObjectState item = State(Source(1, ObjectType.Food, 10078), unitWeight: 4001);
            ObjectPlacement before = item.Placement;
            int observations = 0;
            _session.ObjectPlacementChanged += (_, _, _) => observations++;

            InventoryTransferResult result = _session.TransferItem(item.Identity, before,
                ObjectPlacement.ContainedBy(_player.Identity));

            Assert.That(result.Code, Is.EqualTo(InventoryResultCode.TooHeavy));
            Assert.That(item.Placement, Is.EqualTo(before));
            Assert.That(item.InventoryLocation, Is.EqualTo(-1));
            Assert.That(_session.ChildrenOf(_player.Identity), Is.Empty);
            Assert.That(observations, Is.Zero);
        }

        [Test]
        public void SourceMismatchStillRejectsBeforeCapacityOrMutation()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3052));
            PersistentObjectState item = OwnedBy(Source(1, ObjectType.Food, 10078), container.Identity, 1,
                InventoryFootprint.OneCell, 0);
            int observations = 0;
            _session.ObjectPlacementChanged += (_, _, _) => observations++;

            InventoryTransferResult result = _session.TransferItem(item.Identity,
                ObjectPlacement.InWorld(Sector, Vector2.zero), ObjectPlacement.ContainedBy(_player.Identity));

            Assert.That(result.Code, Is.EqualTo(InventoryResultCode.SourceMismatch));
            Assert.That(item.ParentIdentity, Is.EqualTo(container.Identity));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.Zero);
            Assert.That(observations, Is.Zero);
        }

        [Test]
        public void WorldCommandSurfacesSpecificTooHeavyFailure()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3052));
            PersistentObjectState item = OwnedBy(Source(1, ObjectType.Food, 10078), container.Identity, 4001,
                InventoryFootprint.OneCell, 0);

            WorldInteractionResult result = _session.ExecuteInteraction(
                WorldInteractionCommand.Transfer(_player.Identity, item.Identity, _player.Identity));

            Assert.That(result.Code, Is.EqualTo(WorldInteractionResultCode.TooHeavy));
            Assert.That(result.InventoryStatus, Is.EqualTo(InventoryResultCode.TooHeavy));
            Assert.That(item.ParentIdentity, Is.EqualTo(container.Identity));
        }

        [Test]
        public void FullCritterGridRejectsTransferWithoutMutation()
        {
            Owned(Source(1, ObjectType.Food, 10078), 0,
                new InventoryFootprint(10, InventoryCapacityService.CritterGridRows));
            PersistentObjectState incoming = State(Source(2, ObjectType.Food, 10079), footprint:
                InventoryFootprint.OneCell);

            InventoryTransferResult result = _session.TransferItem(incoming.Identity, incoming.Placement,
                ObjectPlacement.ContainedBy(_player.Identity));

            Assert.That(result.Code, Is.EqualTo(InventoryResultCode.NoRoom));
            Assert.That(incoming.Placement.Kind, Is.EqualTo(ObjectPlacementKind.World));
        }

        [Test]
        public void FullContainerGridRejectsTransferWithoutMutation()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3052));
            OwnedBy(Source(1, ObjectType.Food, 10078), container.Identity, 0,
                new InventoryFootprint(10, InventoryCapacityService.ContainerGridRows), 0);
            PersistentObjectState incoming = State(Source(2, ObjectType.Food, 10079));

            InventoryTransferResult result = _session.TransferItem(incoming.Identity, incoming.Placement,
                ObjectPlacement.ContainedBy(container.Identity));

            Assert.That(result.Code, Is.EqualTo(InventoryResultCode.NoRoom));
            Assert.That(_session.InventoryCapacity.GetContainerLoad(container.Identity), Is.EqualTo(960));
            Assert.That(incoming.Placement.Kind, Is.EqualTo(ObjectPlacementKind.World));
        }

        [Test]
        public void CompatibleStackTransferUsesNoNewGridFootprint()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3052));
            PersistentObjectState destination = OwnedBy(Source(1, ObjectType.Ammo, 7059, quantity: 4),
                container.Identity, 1, new InventoryFootprint(10, 96), 0);
            PersistentObjectState incoming = State(Source(2, ObjectType.Ammo, 7059, quantity: 4), unitWeight: 1,
                footprint: InventoryFootprint.OneCell);

            InventoryTransferResult result = _session.TransferItem(incoming.Identity, incoming.Placement,
                ObjectPlacement.ContainedBy(container.Identity));

            Assert.That(result.Succeeded && result.SourceConsumed, Is.True);
            Assert.That(result.MergedIntoIdentity, Is.EqualTo(destination.Identity));
            Assert.That(destination.StackQuantity, Is.EqualTo(8));
            Assert.That(_session.InventoryCapacity.GetContainerLoad(container.Identity), Is.EqualTo(960));
        }

        [Test]
        public void OrdinaryContainerTransferUsesFootprintAndPreservesIdentity()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3052));
            PersistentObjectState item = Owned(Source(1, ObjectType.Food, 10078), 50,
                new InventoryFootprint(2, 3));

            InventoryTransferResult result = _session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(container.Identity));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(item.Identity, Is.EqualTo(Id(1)));
            Assert.That(item.ParentIdentity, Is.EqualTo(container.Identity));
            Assert.That(item.InventoryLocation, Is.Zero);
            Assert.That(_session.InventoryCapacity.GetContainerLoad(container.Identity), Is.EqualTo(6));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.Zero);
        }

        [Test]
        public void MergeChecksCommittedFinalAmmoWeightBeforeMutation()
        {
            Owned(Source(10, ObjectType.Food, 10078), 4000);
            PersistentObjectState source = Owned(Source(1, ObjectType.Ammo, 7059, quantity: 3), 1);
            PersistentObjectState destination = Owned(Source(2, ObjectType.Ammo, 7059, quantity: 3), 1);

            StackMergeResult result = _session.MergeStacks(source.Identity, destination.Identity);

            Assert.That(result.Code, Is.EqualTo(StackResultCode.TooHeavy));
            Assert.That(source.StackQuantity, Is.EqualTo(3));
            Assert.That(destination.StackQuantity, Is.EqualTo(3));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(4000));
        }

        [Test]
        public void SuccessfulAmmoSplitAndFullMergeUseCommittedQuantitiesWithoutTombstoneLoad()
        {
            PersistentObjectState source = Owned(Source(1, ObjectType.Ammo, 7059, quantity: 6), 4,
                new InventoryFootprint(2, 1));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(4));

            StackSplitResult split = _session.SplitStack(source.Identity, 3);
            Assert.That(split.Succeeded, Is.True);
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.Zero);
            Assert.That(_session.TryGetObjectState(split.CreatedState.Identity,
                out PersistentObjectState created), Is.True);

            StackMergeResult merge = _session.MergeStacks(created.Identity, source.Identity);
            Assert.That(merge.Succeeded, Is.True);
            Assert.That(source.StackQuantity, Is.EqualTo(6));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(4));
            Assert.That(_session.TryGetObjectState(created.Identity, out _), Is.False);
            Assert.That(_session.IsObjectRemoved(created.Identity), Is.True);
        }

        [Test]
        public void SplitRequiresSecondFootprintBeforeIdentityAllocation()
        {
            Owned(Source(10, ObjectType.Food, 10078), 0, new InventoryFootprint(9, 12));
            PersistentObjectState source = Owned(Source(1, ObjectType.Ammo, 7059, quantity: 20), 1,
                new InventoryFootprint(1, 12));

            StackSplitResult result = _session.SplitStack(source.Identity, 4);

            Assert.That(result.Code, Is.EqualTo(StackResultCode.NoRoom));
            Assert.That(source.StackQuantity, Is.EqualTo(20));
            var prototype = new ObjectProtoInfo(10079, ObjectType.Food, 0x50000000u, weight: 1);
            _session.BindPrototypeSource(number => number == prototype.ProtoNumber ? prototype : null);
            Assert.That(_session.CreateItem(10079, ObjectPlacement.InWorld(Sector, Vector2.zero)).State.Identity.Key,
                Is.EqualTo("D_0000000000000001"));
        }

        [Test]
        public void EquipmentRoundTripPreservesLoadAndAssignsGridLocations()
        {
            PersistentObjectState boots = Owned(Source(1, ObjectType.Armor, 8000, invAid: ArmorAid(4)), 200,
                new InventoryFootprint(2, 3));
            long before = _session.InventoryCapacity.GetInventoryLoad(_player.Identity);

            Assert.That(_session.EquipItem(_player.Identity, boots.Identity, WornLocation.Boots).Succeeded, Is.True);
            Assert.That(boots.InventoryLocation, Is.EqualTo((int)WornLocation.Boots));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(before));
            Assert.That(_session.UnequipItem(_player.Identity, WornLocation.Boots).Succeeded, Is.True);
            Assert.That(boots.InventoryLocation, Is.EqualTo(0));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(before));
        }

        [Test]
        public void DynamicCreationUsesSameCapacityPathAndRejectedCreationConsumesNoIdentity()
        {
            var tooHeavy = new ObjectProtoInfo(10078, ObjectType.Food, 0x50000000u, weight: 4001);
            var accepted = new ObjectProtoInfo(10079, ObjectType.Food, 0x50000001u, weight: 50,
                invAid: 0x60000000u);
            _session.BindPrototypeSource(number => number == tooHeavy.ProtoNumber ? tooHeavy
                : number == accepted.ProtoNumber ? accepted : null);
            _session.BindInventoryFootprintSource(_ => new InventoryFootprint(2, 3));

            ItemCreationResult rejected = _session.CreateItem(tooHeavy.ProtoNumber,
                ObjectPlacement.ContainedBy(_player.Identity));
            ItemCreationResult created = _session.CreateItem(accepted.ProtoNumber,
                ObjectPlacement.ContainedBy(_player.Identity));

            Assert.That(rejected.Code, Is.EqualTo(InventoryResultCode.TooHeavy));
            Assert.That(created.State.Identity.Key, Is.EqualTo("D_0000000000000001"));
            Assert.That(created.State.UnitWeight, Is.EqualTo(50));
            Assert.That(created.State.InventoryFootprint, Is.EqualTo(new InventoryFootprint(2, 3)));
            Assert.That(created.State.InventoryLocation, Is.Zero);
        }

        [Test]
        public void CapacityStateSurvivesSectorLifecycleWithoutPresentationAuthority()
        {
            PersistentObjectState item = Owned(Source(1, ObjectType.Food, 10078), 50,
                new InventoryFootprint(1, 2));
            ArcanumObjectId identity = item.Identity;
            _session.UnloadSector(Sector);
            _session.BeginSector(Sector);

            Assert.That(_session.TryGetObjectState(identity, out PersistentObjectState retained), Is.True);
            Assert.That(retained, Is.SameAs(item));
            Assert.That(retained.UnitWeight, Is.EqualTo(50));
            Assert.That(retained.InventoryFootprint, Is.EqualTo(new InventoryFootprint(1, 2)));
            Assert.That(_session.InventoryCapacity.GetInventoryLoad(_player.Identity), Is.EqualTo(50));
            Assert.That(_root.GetComponentsInChildren<WorldObject>(true), Is.Empty);
        }

        private PersistentObjectState Owned(ObjectInstance source, int unitWeight = 0,
            InventoryFootprint? footprint = null)
            => OwnedBy(source, _player.Identity, unitWeight, footprint ?? InventoryFootprint.OneCell,
                source.InvLocation >= 0 ? source.InvLocation : 0);

        private PersistentObjectState OwnedBy(ObjectInstance source, ArcanumObjectId owner, int unitWeight,
            InventoryFootprint footprint, int inventoryLocation)
        {
            var owned = new ObjectInstance(source.Type, source.PrototypeNumber, source.Location, source.CurrentArtId,
                source.OffsetX, source.OffsetY, oid: source.Oid, parentOid: Bytes(owner),
                invLocation: inventoryLocation, invAid: source.InvAid, weight: source.Weight)
            {
                AmmoQuantity = source.AmmoQuantity,
                GoldQuantity = source.GoldQuantity,
                ItemFlags = source.ItemFlags,
            };
            return State(owned, unitWeight, footprint, inventoryLocation);
        }

        private PersistentObjectState State(ObjectInstance source, int? unitWeight = null,
            InventoryFootprint? footprint = null, int? inventoryLocation = null)
        {
            int? quantity = source.Type switch
            {
                ObjectType.Ammo => source.AmmoQuantity,
                ObjectType.Gold => source.GoldQuantity,
                _ => null,
            };
            return _session.GetOrCreate(source, Sector, source.CurrentArtId ?? 0x50000000u, false, false,
                source.ItemFlags ?? 0, source.InvAid, source.WeaponFlags ?? 0, source.GenericFlags ?? 0, quantity,
                unitWeight, footprint, inventoryLocation);
        }

        private static ObjectInstance Source(int id, ObjectType type, int prototype, int? quantity = null,
            int invLocation = -1, uint? invAid = null, int? weight = null)
        {
            var source = new ObjectInstance(type, prototype, 1L, 0x50000000u, 0, 0, oid: Bytes(Id(id)),
                invLocation: invLocation, invAid: invAid, weight: weight);
            if (type == ObjectType.Ammo) source.AmmoQuantity = quantity;
            if (type == ObjectType.Gold) source.GoldQuantity = quantity;
            return source;
        }

        private static int[] StatSource(int strength, CharacterRace race, CharacterGender gender)
        {
            var values = Enumerable.Repeat(8, CharacterAttributeSet.SourceStatArrayCount).ToArray();
            values[0] = strength;
            values[CharacterAttributeSet.RaceSourceSlot] = (int)race;
            values[CharacterAttributeSet.GenderSourceSlot] = (int)gender;
            return values;
        }

        private static uint ArmorAid(int coverage) => (uint)coverage << 14;

        private static ArcanumObjectId Id(int value) => ArcanumObjectId.FromBytes(AuthoredBytes(value));

        private static byte[] AuthoredBytes(int value)
        {
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            return bytes;
        }

        private static byte[] Bytes(ArcanumObjectId identity)
        {
            if (identity.Type == ArcanumObjectIdType.Authored)
                return AuthoredBytes(int.Parse(identity.Key.Substring(2), System.Globalization.NumberStyles.HexNumber));
            if (identity == ProductionPlayerLifecycle.DefaultPlayerIdentity)
            {
                var bytes = new byte[24];
                bytes[0] = (byte)ArcanumObjectIdType.Guid;
                Array.Copy(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01").ToByteArray(), 0, bytes, 8, 16);
                return bytes;
            }
            throw new ArgumentException($"Unsupported test identity {identity}.", nameof(identity));
        }
    }
}
