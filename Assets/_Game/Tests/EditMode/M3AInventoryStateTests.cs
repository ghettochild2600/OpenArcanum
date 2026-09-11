using System;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    public sealed class M3AInventoryStateTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M3AInventoryStateTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BeginSector(Sector);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test, Category("M3A")]
        public void AuthoredParentRegistrationIsExplicitAndWorldIneligible()
        {
            PersistentObjectState owner = State(Source(1, ObjectType.Container));
            PersistentObjectState child = State(Source(2, ObjectType.Armor, parent: 1));

            Assert.That(child.AuthoredParentIdentity, Is.EqualTo(owner.Identity));
            Assert.That(child.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Contained));
            Assert.That(child.ParentIdentity, Is.EqualTo(owner.Identity));
            Assert.That(_session.ChildrenOf(owner.Identity), Is.EqualTo(new[] { child.Identity }));
            Assert.That(_session.IsWorldPresentationEligible(child, Sector), Is.False);
        }

        [Test, Category("M3A")]
        public void UnresolvedAuthoredParentsArePreservedWhileInvalidRelationshipsAreRejected()
        {
            ObjectInstance unknown = Source(2, ObjectType.Generic, parent: 99);
            Assert.That(_session.ValidateSector(Sector, new[] { unknown }, out string unresolved), Is.True, unresolved);
            PersistentObjectState unresolvedChild = State(unknown);
            Assert.That(unresolvedChild.ParentIdentity, Is.EqualTo(Id(99)));
            Assert.That(_session.ChildrenOf(Id(99)), Is.EqualTo(new[] { unresolvedChild.Identity }));

            ObjectInstance self = Source(3, ObjectType.Generic, parent: 3);
            Assert.That(_session.ValidateSector(Sector, new[] { self }, out string selfError), Is.False);
            Assert.That(selfError, Does.Contain("cannot contain itself"));

            ObjectInstance duplicate = Source(4, ObjectType.Generic);
            Assert.That(_session.ValidateSector(Sector, new[] { duplicate, duplicate }, out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("Duplicate persistent"));
            Assert.That(_session.States.Count, Is.EqualTo(1));
        }

        [Test, Category("M3A")]
        public void AtomicTransferSupportsWorldContainersAndProductionPlayer()
        {
            PersistentObjectState a = State(Source(1, ObjectType.Container));
            PersistentObjectState b = State(Source(2, ObjectType.Container));
            PersistentObjectState item = State(Source(3, ObjectType.Weapon));
            PersistentPlayerState pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(4, 5), 0x28100000u);

            AssertSuccess(_session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(a.Identity)));
            Assert.That(_session.ChildrenOf(a.Identity), Is.EqualTo(new[] { item.Identity }));
            AssertSuccess(_session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(b.Identity)));
            Assert.That(_session.ChildrenOf(a.Identity), Is.Empty);
            Assert.That(_session.ChildrenOf(b.Identity), Is.EqualTo(new[] { item.Identity }));
            AssertSuccess(_session.TransferItem(item.Identity, item.Placement, ObjectPlacement.ContainedBy(pc.Identity)));
            Assert.That(item.Identity, Is.EqualTo(Source(3, ObjectType.Weapon).Identity));
            AssertSuccess(_session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(9, 10))));
            Assert.That(item.Placement.Kind, Is.EqualTo(ObjectPlacementKind.World));
            Assert.That(item.TilePosition, Is.EqualTo(new Vector2(9, 10)));
            Assert.That(item.ParentIdentity.IsNull, Is.True);
        }

        [Test, Category("M3A")]
        public void FailedTransfersLeavePlacementAndChildrenUnchanged()
        {
            PersistentObjectState owner = State(Source(1, ObjectType.Container));
            PersistentObjectState item = State(Source(2, ObjectType.Generic, parent: 1));
            ObjectPlacement before = item.Placement;
            ArcanumObjectId[] children = _session.ChildrenOf(owner.Identity).ToArray();

            Assert.That(_session.TransferItem(item.Identity, ObjectPlacement.InWorld(Sector, Vector2.zero),
                    ObjectPlacement.ContainedBy(owner.Identity)).Code,
                Is.EqualTo(InventoryResultCode.SourceMismatch));
            Assert.That(_session.TransferItem(item.Identity, before, ObjectPlacement.ContainedBy(Id(99))).Code,
                Is.EqualTo(InventoryResultCode.ParentNotFound));
            Assert.That(_session.TransferItem(item.Identity, before, ObjectPlacement.ContainedBy(item.Identity)).Code,
                Is.EqualTo(InventoryResultCode.SelfParent));
            Assert.That(_session.TransferItem(owner.Identity, owner.Placement,
                    ObjectPlacement.InWorld(Sector, Vector2.zero)).Code,
                Is.EqualTo(InventoryResultCode.InvalidItemType));
            Assert.That(_session.TransferItem(item.Identity, before, before).Code,
                Is.EqualTo(InventoryResultCode.AlreadyAtDestination));
            Assert.That(item.Placement, Is.EqualTo(before));
            Assert.That(_session.ChildrenOf(owner.Identity), Is.EqualTo(children));
        }

        [Test, Category("M3A")]
        public void CycleAttemptRollsBack()
        {
            PersistentObjectState item = State(Source(1, ObjectType.Generic));
            PersistentObjectState malformedContainer = State(Source(2, ObjectType.Container, parent: 1));
            ObjectPlacement before = item.Placement;
            InventoryTransferResult result = _session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(malformedContainer.Identity));
            Assert.That(result.Code, Is.EqualTo(InventoryResultCode.CycleDetected));
            Assert.That(item.Placement, Is.EqualTo(before));
        }

        [Test, Category("M3A")]
        public void TransferSurvivesSectorUnloadAndSourceReload()
        {
            ObjectInstance ownerSource = Source(1, ObjectType.Container);
            ObjectInstance childSource = Source(2, ObjectType.Armor, parent: 1);
            PersistentObjectState owner = State(ownerSource);
            PersistentObjectState child = State(childSource);
            AssertSuccess(_session.TransferItem(child.Identity, child.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(7, 8))));

            _session.UnloadSector(Sector);
            _session.BeginSector(Sector);
            Assert.That(State(ownerSource), Is.SameAs(owner));
            Assert.That(State(childSource), Is.SameAs(child));
            Assert.That(child.Placement, Is.EqualTo(ObjectPlacement.InWorld(Sector, new Vector2(7, 8))));
            Assert.That(_session.ChildrenOf(owner.Identity), Is.Empty);
        }

        [Test, Category("M3A")]
        public void DynamicIdentityIsDeterministicUniqueStableAndDisjointFromAuthoredKinds()
        {
            var proto = new ObjectProtoInfo(7000, ObjectType.Food, 0x40000000u);
            _session.BindPrototypeSource(number => number == 7000 ? proto : null);
            ItemCreationResult first = _session.CreateItem(7000, ObjectPlacement.InWorld(Sector, new Vector2(1, 2)));
            ItemCreationResult second = _session.CreateItem(7000, ObjectPlacement.InWorld(Sector, new Vector2(2, 3)));
            Assert.That(first.Succeeded && second.Succeeded, Is.True);
            Assert.That(first.State.Identity.Type, Is.EqualTo(ArcanumObjectIdType.SessionDynamic));
            Assert.That(first.State.Identity.IsAuthored, Is.False);
            Assert.That(first.State.Identity, Is.Not.EqualTo(second.State.Identity));
            ArcanumObjectId retained = first.State.Identity;
            AssertSuccess(_session.TransferItem(retained, first.State.Placement, ObjectPlacement.ContainedBy(
                State(Source(20, ObjectType.Container)).Identity)));
            Assert.That(first.State.Identity, Is.EqualTo(retained));

            var otherRoot = new GameObject("M3ADeterminismReplay");
            try
            {
                var replay = otherRoot.AddComponent<WorldMapSessionCoordinator>();
                replay.BeginSector(Sector);
                replay.BindPrototypeSource(_ => proto);
                Assert.That(replay.CreateItem(7000, ObjectPlacement.InWorld(Sector, Vector2.zero)).State.Identity,
                    Is.EqualTo(retained));
            }
            finally { Object.DestroyImmediate(otherRoot); }

            Assert.That(retained, Is.Not.EqualTo(Id(1)));
            Assert.That(retained, Is.Not.EqualTo(ArcanumObjectId.CreateGuid(Guid.Empty)));
            Assert.That(retained, Is.Not.EqualTo(ArcanumObjectId.CreatePositional(0, 1, 1)));
        }

        [Test, Category("M3A"), Category("RealData")]
        public void RealContainerChildProjectsExactlyOnceAcrossTransferReloadAndPlayerCrossing()
        {
            const string realSector = "maps/arcanum1-024-fixed/101602821844.sec";
            const string adjacentSector = "maps/arcanum1-024-fixed/101602821845.sec";
            const string containerKey = "G_8F454608_E327_1341_B85B_E7A5402D4758";
            const string childKey = "G_0435F503_6600_6342_97B2_6D9E1A85A2F2";
            _session.UnloadSector(Sector);
            var loader = _root.AddComponent<WorldObjectSectorLoader>();
            loader.BindSessionAuthority();
            Assert.That(_session.SelectSector(realSector), Is.True);

            PersistentObjectState container = _session.States.Values.Single(state => state.Identity.Key == containerKey);
            PersistentObjectState child = _session.States.Values.Single(state => state.Identity.Key == childKey);
            Assert.That(container.Type, Is.EqualTo(ObjectType.Container));
            Assert.That(container.PrototypeNumber, Is.EqualTo(3052));
            Assert.That(child.Type, Is.EqualTo(ObjectType.Armor));
            Assert.That(child.PrototypeNumber, Is.EqualTo(8127));
            Assert.That(child.AuthoredParentIdentity, Is.EqualTo(container.Identity));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == child.Identity), Is.Zero);

            AssertSuccess(_session.TransferItem(child.Identity, child.Placement,
                ObjectPlacement.InWorld(realSector, new Vector2(12, 18))));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == child.Identity), Is.EqualTo(1));
            Assert.That(_session.TryGetLoadedObject(child.Identity, out WorldObject firstProjection), Is.True);
            loader.RebuildVisuals();
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == child.Identity), Is.EqualTo(1));
            Assert.That(firstProjection.Identity, Is.EqualTo(child.Identity));

            PersistentPlayerState player = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                realSector, new Vector2(36, 58), 0x28100000u);
            AssertSuccess(_session.TransferItem(child.Identity, child.Placement,
                ObjectPlacement.ContainedBy(player.Identity)));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == child.Identity), Is.Zero);
            Assert.That(_session.TryGetLoadedObject(child.Identity, out _), Is.False);

            Assert.That(_session.ReloadSelectedSector(), Is.True);
            Assert.That(child.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(player.Identity)));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == child.Identity), Is.Zero);
            Assert.That(_session.TryTransitionPlayer(adjacentSector, new Vector2(0, 58), player.ArtId), Is.True);
            Assert.That(child.ParentIdentity, Is.EqualTo(player.Identity));
            Assert.That(_session.TryTransitionPlayer(realSector, new Vector2(63, 58), player.ArtId), Is.True);
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == child.Identity), Is.Zero);

            AssertSuccess(_session.TransferItem(child.Identity, child.Placement,
                ObjectPlacement.InWorld(realSector, new Vector2(13, 18))));
            Assert.That(loader.SpriteOwners.Count(owner => owner.WorldObject.Identity == child.Identity), Is.EqualTo(1));
            Assert.That(_session.ChildrenOf(player.Identity), Is.Empty);
            Assert.That(_session.TryGetLoadedObject(child.Identity, out WorldObject restored), Is.True);
            Assert.That(restored.Identity, Is.EqualTo(child.Identity));
            Assert.That(restored.TilePosition, Is.EqualTo(new Vector2(13, 18)));
        }

        private PersistentObjectState State(ObjectInstance source)
            => _session.GetOrCreate(source, Sector, source.CurrentArtId ?? 0x40000000u, false, false);

        private static ObjectInstance Source(int id, ObjectType type, int? parent = null)
            => new(type, 5000 + id, 1L, 0x40000000u, 0, 0, oid: Bytes(id),
                parentOid: parent.HasValue ? Bytes(parent.Value) : null);

        private static byte[] Bytes(int value)
        {
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            return bytes;
        }

        private static ArcanumObjectId Id(int value) => ArcanumObjectId.FromBytes(Bytes(value));
        private static void AssertSuccess(InventoryTransferResult result)
            => Assert.That(result.Code, Is.EqualTo(InventoryResultCode.Success));
    }
}
