using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M3D")]
    public sealed class M3DStackStateTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _player;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M3DStackStateTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BeginSector(Sector);
            _player = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void SourceQuantityFieldsDecodeAsInt32AndQuantityOneIsAStack()
        {
            byte[] ammoRecord = BuildInstance(ObjectType.Ammo, (142, 37));
            byte[] goldRecord = BuildInstance(ObjectType.Gold, (165, 4200));
            int ammoOffset = 0;
            int goldOffset = 0;
            ObjectInstance ammoSource = ObjectInstanceReader.Read(ammoRecord, ref ammoOffset);
            ObjectInstance goldSource = ObjectInstanceReader.Read(goldRecord, ref goldOffset);

            Assert.That(ammoSource.AmmoQuantity, Is.EqualTo(37));
            Assert.That(goldSource.GoldQuantity, Is.EqualTo(4200));
            Assert.That(ammoOffset, Is.EqualTo(ammoRecord.Length));
            Assert.That(goldOffset, Is.EqualTo(goldRecord.Length));

            PersistentObjectState one = State(Source(1, ObjectType.Ammo, 6001, quantity: 1));
            Assert.That(one.StackQuantity, Is.EqualTo(1));
        }

        [Test]
        public void OnlyAmmoAndGoldHaveStackQuantityAndInvalidSourceQuantityFailsExplicitly()
        {
            PersistentObjectState ammo = State(Source(1, ObjectType.Ammo, 6001, quantity: 10));
            PersistentObjectState gold = State(Source(2, ObjectType.Gold, 9056, quantity: 1));
            PersistentObjectState food = State(Source(3, ObjectType.Food, 10078));
            Assert.That(ammo.StackQuantity, Is.EqualTo(10));
            Assert.That(gold.StackQuantity, Is.EqualTo(1));
            Assert.That(food.StackQuantity, Is.Null);
            Assert.That(() => State(Source(4, ObjectType.Ammo, 6001, quantity: 0)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CompatibilityRequiresExactPrototypeButNotInstanceFlags()
        {
            PersistentObjectState left = Owned(Source(1, ObjectType.Ammo, 6001, quantity: 10, itemFlags: 0));
            PersistentObjectState same = Owned(Source(2, ObjectType.Ammo, 6001, quantity: 20, itemFlags: 0x40));
            PersistentObjectState otherProto = Owned(Source(3, ObjectType.Ammo, 6002, quantity: 20));
            PersistentObjectState gold = Owned(Source(4, ObjectType.Gold, 9056, quantity: 20));
            Assert.That(_session.CanStack(left.Identity, same.Identity), Is.True);
            Assert.That(_session.CanStack(left.Identity, otherProto.Identity), Is.False);
            Assert.That(_session.CanStack(left.Identity, gold.Identity), Is.False);
            Assert.That(_session.CanStack(left.Identity, left.Identity), Is.False);
        }

        [Test]
        public void PartialMergeIsAtomicAndPreservesBothIdentities()
        {
            PersistentObjectState source = Owned(Source(1, ObjectType.Ammo, 6001, quantity: 10));
            PersistentObjectState destination = Owned(Source(2, ObjectType.Ammo, 6001, quantity: 20));
            int observations = 0;
            _session.ObjectQuantityChanged += (_, _, _) =>
            {
                observations++;
                Assert.That(source.StackQuantity, Is.EqualTo(6));
                Assert.That(destination.StackQuantity, Is.EqualTo(24));
            };

            StackMergeResult result = _session.MergeStacks(source.Identity, destination.Identity, 4);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SourceConsumed, Is.False);
            Assert.That(result.SourceQuantity, Is.EqualTo(6));
            Assert.That(result.DestinationQuantity, Is.EqualTo(24));
            Assert.That(_session.TryGetObjectState(source.Identity, out PersistentObjectState retained), Is.True);
            Assert.That(retained, Is.SameAs(source));
            Assert.That(observations, Is.EqualTo(2));
        }

        [Test]
        public void FullMergePreservesDestinationAndTombstonesAuthoredSource()
        {
            ObjectInstance sourceRecord = Source(1, ObjectType.Gold, 9056, quantity: 40);
            PersistentObjectState source = Owned(sourceRecord);
            PersistentObjectState destination = Owned(Source(2, ObjectType.Gold, 9056, quantity: 2));
            bool observedAtomic = false;
            _session.ObjectQuantityChanged += (state, _, _) =>
            {
                if (state != destination) return;
                observedAtomic = !_session.TryGetObjectState(source.Identity, out _)
                                 && _session.IsObjectRemoved(source.Identity)
                                 && destination.StackQuantity == 42;
            };

            StackMergeResult result = _session.MergeStacks(source.Identity, destination.Identity);
            Assert.That(result.Succeeded && result.SourceConsumed, Is.True);
            Assert.That(destination.StackQuantity, Is.EqualTo(42));
            Assert.That(_session.TryGetObjectState(source.Identity, out _), Is.False);
            Assert.That(_session.IsObjectRemoved(source.Identity), Is.True);
            Assert.That(StateOrNull(sourceRecord), Is.Null, "authored source must not resurrect");
            Assert.That(observedAtomic, Is.True);
            Assert.That(_session.ChildrenOf(_player.Identity), Is.EqualTo(new[] { destination.Identity }));
        }

        [Test]
        public void MergeFailuresAndMaximumOverflowLeaveAllStateUnchanged()
        {
            PersistentObjectState source = Owned(Source(1, ObjectType.Ammo, 6001, quantity: 10));
            PersistentObjectState destination = Owned(Source(2, ObjectType.Ammo, 6001,
                quantity: WorldMapSessionCoordinator.MaxStackQuantity));
            PersistentObjectState other = Owned(Source(3, ObjectType.Ammo, 6002, quantity: 10));
            ObjectPlacement sourcePlacement = source.Placement;

            Assert.That(_session.MergeStacks(source.Identity, destination.Identity).Code,
                Is.EqualTo(StackResultCode.QuantityOverflow));
            Assert.That(_session.MergeStacks(source.Identity, other.Identity).Code,
                Is.EqualTo(StackResultCode.Incompatible));
            Assert.That(_session.MergeStacks(source.Identity, destination.Identity, 0).Code,
                Is.EqualTo(StackResultCode.InvalidQuantity));
            Assert.That(_session.MergeStacks(source.Identity, destination.Identity, 11).Code,
                Is.EqualTo(StackResultCode.InvalidQuantity));
            Assert.That(_session.MergeStacks(source.Identity, source.Identity).Code,
                Is.EqualTo(StackResultCode.SameObject));
            Assert.That(_session.MergeStacks(Id(99), destination.Identity).Code,
                Is.EqualTo(StackResultCode.SourceNotFound));
            Assert.That(_session.MergeStacks(source.Identity, Id(99)).Code,
                Is.EqualTo(StackResultCode.DestinationNotFound));
            Assert.That(source.StackQuantity, Is.EqualTo(10));
            Assert.That(destination.StackQuantity, Is.EqualTo(WorldMapSessionCoordinator.MaxStackQuantity));
            Assert.That(other.StackQuantity, Is.EqualTo(10));
            Assert.That(source.Placement, Is.EqualTo(sourcePlacement));
        }

        [Test]
        public void SplitPreservesOriginalAndCreatesOneMonotonicDynamicIdentity()
        {
            PersistentObjectState source = Owned(Source(1, ObjectType.Ammo, 6001, quantity: 30));
            StackSplitResult split = _session.SplitStack(source.Identity, 12);
            Assert.That(split.Succeeded, Is.True);
            Assert.That(source.Identity, Is.EqualTo(Id(1)));
            Assert.That(source.StackQuantity, Is.EqualTo(18));
            Assert.That(split.CreatedState.Identity.Key, Is.EqualTo("D_0000000000000001"));
            Assert.That(split.CreatedState.StackQuantity, Is.EqualTo(12));
            Assert.That(split.CreatedState.PrototypeNumber, Is.EqualTo(source.PrototypeNumber));
            Assert.That(split.CreatedState.Placement, Is.EqualTo(source.Placement));
            Assert.That(_session.ChildrenOf(_player.Identity),
                Is.EqualTo(new[] { source.Identity, split.CreatedState.Identity }));
        }

        [Test]
        public void DynamicSplitMergeUsesExistingAllocatorAndDestinationIdentityContract()
        {
            var prototype = new ObjectProtoInfo(6001, ObjectType.Ammo, 0x50000000u)
                { AmmoQuantity = 40, ItemFlags = 0 };
            _session.BindPrototypeSource(number => number == 6001 ? prototype : null);
            ItemCreationResult original = _session.CreateItem(6001, ObjectPlacement.ContainedBy(_player.Identity));
            StackSplitResult split = _session.SplitStack(original.State.Identity, 15);
            Assert.That(original.State.Identity.Key, Is.EqualTo("D_0000000000000001"));
            Assert.That(split.CreatedState.Identity.Key, Is.EqualTo("D_0000000000000002"));
            Assert.That(_session.MergeStacks(split.CreatedState.Identity, original.State.Identity).SourceConsumed,
                Is.True);
            Assert.That(original.State.StackQuantity, Is.EqualTo(40));
            Assert.That(_session.IsObjectRemoved(split.CreatedState.Identity), Is.True);
            Assert.That(_session.CreateItem(6001, ObjectPlacement.ContainedBy(_player.Identity)).State.Identity.Key,
                Is.EqualTo("D_0000000000000003"));
        }

        [Test]
        public void AllocatorSkipsAnExistingDynamicIdentityWithoutCollision()
        {
            ObjectInstance blockerSource = Source(1, ObjectType.Ammo, 6001, quantity: 3);
            PersistentObjectState blocker = _session.GetOrCreate(blockerSource,
                ArcanumObjectId.CreateSessionDynamic(1), Sector, 0x50000000u, false, false,
                stackQuantity: 3);
            PersistentObjectState source = Owned(Source(2, ObjectType.Ammo, 6001, quantity: 10));
            StackSplitResult split = _session.SplitStack(source.Identity, 2);
            Assert.That(blocker.Identity.Key, Is.EqualTo("D_0000000000000001"));
            Assert.That(split.CreatedState.Identity.Key, Is.EqualTo("D_0000000000000002"));
            Assert.That(_session.States.Keys.Distinct().Count(), Is.EqualTo(_session.States.Count));
        }

        [Test]
        public void SplitFailuresLeaveQuantityAllocatorAndMembershipUnchanged()
        {
            PersistentObjectState source = Owned(Source(1, ObjectType.Gold, 9056, quantity: 10));
            PersistentObjectState food = Owned(Source(2, ObjectType.Food, 10078));
            ArcanumObjectId[] before = _session.ChildrenOf(_player.Identity).ToArray();
            Assert.That(_session.SplitStack(source.Identity, 0).Code, Is.EqualTo(StackResultCode.InvalidQuantity));
            Assert.That(_session.SplitStack(source.Identity, 10).Code, Is.EqualTo(StackResultCode.InvalidQuantity));
            Assert.That(_session.SplitStack(source.Identity, 11).Code, Is.EqualTo(StackResultCode.InvalidQuantity));
            Assert.That(_session.SplitStack(food.Identity, 1).Code, Is.EqualTo(StackResultCode.NotStackable));
            Assert.That(_session.SplitStack(Id(99), 1).Code, Is.EqualTo(StackResultCode.SourceNotFound));
            Assert.That(source.StackQuantity, Is.EqualTo(10));
            Assert.That(_session.ChildrenOf(_player.Identity), Is.EqualTo(before));

            Assert.That(_session.TransferItem(source.Identity, source.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(2, 2))).Succeeded, Is.True);
            Assert.That(_session.SplitStack(source.Identity, 1).Code, Is.EqualTo(StackResultCode.InvalidPlacement));
            Assert.That(source.StackQuantity, Is.EqualTo(10));
        }

        [Test]
        public void EquippedStackAndExhaustedIdentityAllocationRejectWithoutMutation()
        {
            PersistentObjectState equipped = State(Source(1, ObjectType.Ammo, 6001,
                parent: 20, quantity: 10, invLocation: (int)WornLocation.Weapon));
            PersistentObjectState contained = State(Source(2, ObjectType.Ammo, 6001,
                parent: 20, quantity: 5));
            Assert.That(equipped.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Equipped));
            Assert.That(_session.SplitStack(equipped.Identity, 2).Code,
                Is.EqualTo(StackResultCode.InvalidPlacement));
            Assert.That(_session.MergeStacks(equipped.Identity, contained.Identity).Code,
                Is.EqualTo(StackResultCode.InvalidPlacement));

            PersistentObjectState owned = Owned(Source(3, ObjectType.Ammo, 6001, quantity: 8));
            ArcanumObjectId[] before = _session.States.Keys.OrderBy(value => value.Key).ToArray();
            typeof(WorldMapSessionCoordinator)
                .GetField("_nextDynamicIdentity", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(_session, 0UL);
            StackSplitResult exhausted = _session.SplitStack(owned.Identity, 3);
            Assert.That(exhausted.Code, Is.EqualTo(StackResultCode.IdentityExhausted));
            Assert.That(owned.StackQuantity, Is.EqualTo(8));
            Assert.That(_session.States.Keys.OrderBy(value => value.Key), Is.EqualTo(before));
        }

        [Test]
        public void MergeRejectsDifferentOwnersAndWorldPlacement()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3020));
            PersistentObjectState source = Owned(Source(1, ObjectType.Ammo, 6001, quantity: 10));
            PersistentObjectState otherOwner = State(Source(2, ObjectType.Ammo, 6001, parent: 20, quantity: 5));
            Assert.That(_session.MergeStacks(source.Identity, otherOwner.Identity).Code,
                Is.EqualTo(StackResultCode.InvalidPlacement));
            Assert.That(_session.TransferItem(source.Identity, source.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(2, 2))).Succeeded, Is.True);
            Assert.That(_session.TransferItem(otherOwner.Identity, otherOwner.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(3, 2))).Succeeded, Is.True);
            Assert.That(_session.MergeStacks(source.Identity, otherOwner.Identity).Code,
                Is.EqualTo(StackResultCode.InvalidPlacement));
            Assert.That(container.Identity, Is.EqualTo(Id(20)));
        }

        [Test]
        public void ContainerStackCanSplitAndMergeWithoutUnityPresentationAuthority()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3020));
            PersistentObjectState source = State(Source(1, ObjectType.Gold, 9056, parent: 20, quantity: 50));
            StackSplitResult split = _session.SplitStack(source.Identity, 20);
            Assert.That(split.Succeeded, Is.True);
            Assert.That(split.CreatedState.ParentIdentity, Is.EqualTo(container.Identity));
            Assert.That(_session.MergeStacks(split.CreatedState.Identity, source.Identity).Succeeded, Is.True);
            Assert.That(source.StackQuantity, Is.EqualTo(50));
        }

        [Test]
        public void OrdinaryTransferPreservesQuantityWhenNoDestinationStackExists()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3020));
            PersistentObjectState stack = Owned(Source(1, ObjectType.Ammo, 6001, quantity: 33));
            InventoryTransferResult toContainer = _session.TransferItem(stack.Identity, stack.Placement,
                ObjectPlacement.ContainedBy(container.Identity));
            Assert.That(toContainer.Succeeded && !toContainer.SourceConsumed, Is.True);
            Assert.That(stack.StackQuantity, Is.EqualTo(33));
            Assert.That(stack.ParentIdentity, Is.EqualTo(container.Identity));

            Assert.That(_session.TransferItem(stack.Identity, stack.Placement,
                ObjectPlacement.ContainedBy(_player.Identity)).Succeeded, Is.True);
            Assert.That(stack.StackQuantity, Is.EqualTo(33));
        }

        [Test]
        public void OwnerTransferAutomaticallyMergesAndConsumesIncomingIdentity()
        {
            PersistentObjectState container = State(Source(20, ObjectType.Container, 3020));
            PersistentObjectState destination = Owned(Source(1, ObjectType.Ammo, 6001, quantity: 10));
            PersistentObjectState incoming = State(Source(2, ObjectType.Ammo, 6001, parent: 20, quantity: 7));
            InventoryTransferResult transfer = _session.TransferItem(incoming.Identity, incoming.Placement,
                ObjectPlacement.ContainedBy(_player.Identity));
            Assert.That(transfer.Succeeded && transfer.SourceConsumed, Is.True);
            Assert.That(transfer.MergedIntoIdentity, Is.EqualTo(destination.Identity));
            Assert.That(destination.StackQuantity, Is.EqualTo(17));
            Assert.That(_session.TryGetObjectState(incoming.Identity, out _), Is.False);
            Assert.That(_session.IsObjectRemoved(incoming.Identity), Is.True);
            Assert.That(_session.ChildrenOf(container.Identity), Is.Empty);
            Assert.That(_session.ChildrenOf(_player.Identity), Is.EqualTo(new[] { destination.Identity }));
        }

        [Test]
        public void PickupDropTraversalAndReloadPreserveQuantityAndIdentity()
        {
            PersistentObjectState stack = State(Source(1, ObjectType.Ammo, 6001, quantity: 25));
            ArcanumObjectId identity = stack.Identity;
            Assert.That(_session.TransferItem(identity, stack.Placement,
                ObjectPlacement.ContainedBy(_player.Identity)).Succeeded, Is.True);
            Assert.That(stack.StackQuantity, Is.EqualTo(25));
            Assert.That(_session.TransferItem(identity, stack.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(5, 6))).Succeeded, Is.True);
            Assert.That(stack.StackQuantity, Is.EqualTo(25));
            _session.UnloadSector(Sector);
            _session.BeginSector(Sector);
            Assert.That(StateOrNull(Source(1, ObjectType.Ammo, 6001, quantity: 25)), Is.SameAs(stack));
            Assert.That(stack.Identity, Is.EqualTo(identity));
            Assert.That(stack.StackQuantity, Is.EqualTo(25));
            Assert.That(stack.Placement, Is.EqualTo(ObjectPlacement.InWorld(Sector, new Vector2(5, 6))));
        }

        [Test, Category("RealData")]
        public void RealAmmoStackPreservesSourceQuantityThroughPickupSplitTransferMergeDropAndTraversal()
        {
            const string realSector = "maps/arcanum1-024-fixed/101602821844.sec";
            const string adjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
            const string containerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
            const string stackKey = "G_9239E097_A8D2_C147_9F58_76077340C60E";
            const string incompatibleKey = "G_FBFA4631_D97D_D740_9636_F131B2FD9F7B";
            _session.UnloadSector(Sector);
            var loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.BindSessionAuthority();
            Assert.That(_session.SelectSector(realSector), Is.True);
            PersistentObjectState container = _session.States.Values.Single(state => state.Identity.Key == containerKey);
            PersistentObjectState stack = _session.States.Values.Single(state => state.Identity.Key == stackKey);
            PersistentObjectState incompatible = _session.States.Values.Single(state => state.Identity.Key == incompatibleKey);
            Assert.That(container.Type, Is.EqualTo(ObjectType.Container));
            Assert.That(container.PrototypeNumber, Is.EqualTo(3052));
            Assert.That(stack.Type, Is.EqualTo(ObjectType.Ammo));
            Assert.That(stack.PrototypeNumber, Is.EqualTo(7059));
            Assert.That(stack.StackQuantity, Is.EqualTo(60), "instance quantity overrides the prototype default");
            Assert.That(stack.ItemFlags, Is.Zero);
            Assert.That(stack.UseScriptNum, Is.Zero);
            Assert.That(stack.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(container.Identity)));
            Assert.That(incompatible.Type, Is.EqualTo(ObjectType.Ammo));
            Assert.That(incompatible.PrototypeNumber, Is.EqualTo(7058));
            Assert.That(incompatible.StackQuantity, Is.EqualTo(70));
            Assert.That(_session.CanStack(stack.Identity, incompatible.Identity), Is.False);

            var pickupTile = new Vector2(12, 18);
            Assert.That(_session.TransferItem(stack.Identity, stack.Placement,
                ObjectPlacement.InWorld(realSector, pickupTile)).Succeeded, Is.True);
            Assert.That(_session.TryTransitionPlayer(realSector, pickupTile, _player.ArtId), Is.True);
            Assert.That(_session.ExecuteInteraction(
                WorldInteractionCommand.PickUp(_player.Identity, stack.Identity)).IsSuccess, Is.True);
            ArcanumObjectId retainedIdentity = stack.Identity;
            Assert.That(stack.StackQuantity, Is.EqualTo(60));

            StackSplitResult split = _session.SplitStack(stack.Identity, 25);
            Assert.That(split.Succeeded, Is.True);
            Assert.That(stack.StackQuantity, Is.EqualTo(35));
            Assert.That(split.CreatedState.Identity.Key, Is.EqualTo("D_0000000000000001"));
            Assert.That(split.CreatedState.StackQuantity, Is.EqualTo(25));
            Assert.That(_session.TransferItem(split.CreatedState.Identity, split.CreatedState.Placement,
                ObjectPlacement.ContainedBy(container.Identity)).Succeeded, Is.True);
            InventoryTransferResult merged = _session.TransferItem(split.CreatedState.Identity,
                split.CreatedState.Placement, ObjectPlacement.ContainedBy(_player.Identity));
            Assert.That(merged.Succeeded && merged.SourceConsumed, Is.True);
            Assert.That(merged.MergedIntoIdentity, Is.EqualTo(retainedIdentity));
            Assert.That(stack.StackQuantity, Is.EqualTo(60));
            Assert.That(_session.IsObjectRemoved(split.CreatedState.Identity), Is.True);

            Assert.That(_session.ExecuteInteraction(WorldInteractionCommand.Drop(_player.Identity,
                stack.Identity, realSector, pickupTile)).IsSuccess, Is.True);
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == retainedIdentity),
                Is.EqualTo(1));
            Assert.That(_session.TryGetLoadedObject(retainedIdentity, out WorldObject dropped), Is.True);
            Assert.That(dropped.AmmoQuantity, Is.EqualTo(60));
            Assert.That(_session.ReloadSelectedSector(), Is.True);
            Assert.That(stack.Identity, Is.EqualTo(retainedIdentity));
            Assert.That(stack.StackQuantity, Is.EqualTo(60));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == retainedIdentity),
                Is.EqualTo(1));
            Assert.That(_session.TryGetLoadedObject(retainedIdentity, out WorldObject reloaded), Is.True);
            Assert.That(reloaded.AmmoQuantity, Is.EqualTo(60));
            Assert.That(_session.TryTransitionPlayer(adjacentSector, new Vector2(0, 18), _player.ArtId), Is.True);
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == retainedIdentity), Is.Zero);
            Assert.That(_session.TryTransitionPlayer(realSector, new Vector2(63, 18), _player.ArtId), Is.True);
            Assert.That(stack.Identity, Is.EqualTo(retainedIdentity));
            Assert.That(stack.StackQuantity, Is.EqualTo(60));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == retainedIdentity),
                Is.EqualTo(1));
        }

        [Test, Category("RealData")]
        public void RealWorldPickupAutoMergeRemovesPresentationAndPreventsAuthoredResurrection()
        {
            const string realSector = "maps/arcanum1-024-fixed/101602821844.sec";
            const string stackKey = "G_9239E097_A8D2_C147_9F58_76077340C60E";
            _session.UnloadSector(Sector);
            var loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.BindSessionAuthority();
            Assert.That(_session.SelectSector(realSector), Is.True);
            PersistentObjectState source = _session.States.Values.Single(state => state.Identity.Key == stackKey);
            var pickupTile = new Vector2(12, 18);
            Assert.That(_session.TransferItem(source.Identity, source.Placement,
                ObjectPlacement.InWorld(realSector, pickupTile)).Succeeded, Is.True);
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == source.Identity),
                Is.EqualTo(1));
            Assert.That(_session.TryTransitionPlayer(realSector, pickupTile, _player.ArtId), Is.True);

            ItemCreationResult destination = _session.CreateItem(source.PrototypeNumber,
                ObjectPlacement.ContainedBy(_player.Identity));
            Assert.That(destination.Succeeded, Is.True);
            Assert.That(destination.State.StackQuantity, Is.EqualTo(10));
            Assert.That(_session.ExecuteInteraction(
                WorldInteractionCommand.PickUp(_player.Identity, source.Identity)).IsSuccess, Is.True);
            Assert.That(destination.State.StackQuantity, Is.EqualTo(70));
            Assert.That(_session.TryGetObjectState(source.Identity, out _), Is.False);
            Assert.That(_session.IsObjectRemoved(source.Identity), Is.True);
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == source.Identity), Is.Zero);

            Assert.That(_session.ReloadSelectedSector(), Is.True);
            Assert.That(_session.TryGetObjectState(source.Identity, out _), Is.False);
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == source.Identity), Is.Zero);
            Assert.That(_session.ChildrenOf(_player.Identity), Is.EqualTo(new[] { destination.State.Identity }));
        }

        private PersistentObjectState Owned(ObjectInstance source)
        {
            var ownedSource = new ObjectInstance(source.Type, source.PrototypeNumber, source.Location,
                source.CurrentArtId, source.OffsetX, source.OffsetY, source.Description, source.Oid,
                PlayerBytes(), source.Flags, source.DialogNum, invLocation: 0, invAid: source.InvAid)
            {
                ItemFlags = source.ItemFlags,
                AmmoQuantity = source.AmmoQuantity,
                GoldQuantity = source.GoldQuantity,
            };
            return State(ownedSource);
        }

        private PersistentObjectState State(ObjectInstance source) => StateOrNull(source);

        private PersistentObjectState StateOrNull(ObjectInstance source)
        {
            int? quantity = source.Type switch
            {
                ObjectType.Ammo => source.AmmoQuantity,
                ObjectType.Gold => source.GoldQuantity,
                _ => null,
            };
            return _session.GetOrCreate(source, Sector, source.CurrentArtId ?? 0x50000000u, false, false,
                source.ItemFlags ?? 0, source.InvAid, source.WeaponFlags ?? 0, source.GenericFlags ?? 0, quantity);
        }

        private static ObjectInstance Source(int id, ObjectType type, int prototype, int? parent = null,
            int? quantity = null, int itemFlags = 0, int invLocation = 0)
        {
            var source = new ObjectInstance(type, prototype, 1L, 0x50000000u, 0, 0, oid: Bytes(id),
                parentOid: parent.HasValue ? Bytes(parent.Value) : null, invLocation: invLocation)
                { ItemFlags = itemFlags };
            if (type == ObjectType.Ammo) source.AmmoQuantity = quantity;
            if (type == ObjectType.Gold) source.GoldQuantity = quantity;
            return source;
        }

        private static byte[] BuildInstance(ObjectType type, params (int field, int value)[] fields)
        {
            int rawType = (int)type;
            var bitmap = new uint[ObjectFieldEngine.DwordCount[rawType]];
            foreach (var entry in fields)
                bitmap[ObjectFieldEngine.ChangeIdx[entry.field]] |= ObjectFieldEngine.Mask[entry.field];
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(119);
            writer.Write(new byte[24]);
            writer.Write(new byte[24]);
            writer.Write(rawType);
            writer.Write((short)0);
            foreach (uint word in bitmap) writer.Write(word);
            foreach (var entry in fields) writer.Write(entry.value);
            return stream.ToArray();
        }

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
