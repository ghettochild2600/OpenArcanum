using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    public sealed class WorldSessionStateTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;

        [SetUp] public void SetUp()
        {
            _root = new GameObject("SessionTest");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BeginSector(Sector);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        private static byte[] Id(int number)
        {
            var bytes = new byte[24]; bytes[0] = 1;
            Array.Copy(BitConverter.GetBytes(number), 0, bytes, 8, 4);
            return bytes;
        }
        private static ObjectInstance Source(int number = 1, uint artId = 0x30000000, ObjectType type = ObjectType.Portal)
            => new(type, 2001, 1L, artId, 0, 0, oid: Id(number));
        private PersistentObjectState State(ObjectInstance source)
            => _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, true);
        private WorldObject Runtime(PersistentObjectState state)
        {
            var go = new GameObject("Instance"); go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>(); runtime.Type = state.Type;
            _session.Bind(Sector, state, runtime);
            return runtime;
        }

        [Test] public void InitialStateUsesAuthoredValuesAndTypedKey()
        {
            var source = Source(1, 0x30018000);
            var state = State(source);
            Assert.That(state.PortalOpen, Is.True);
            Assert.That(state.ArtId, Is.EqualTo(source.CurrentArtId));
            Assert.That(state.Identity, Is.EqualTo(source.Identity));
            Assert.That(state.AuthoredLocation, Is.EqualTo(1));
            Assert.That(state.Locked, Is.True);
        }

        [Test] public void LookupIgnoresPaddingAndDoesNotDuplicateState()
        {
            var state = State(Source());
            var id = Id(1); id[3] = 99; id[20] = 77;
            var source = new ObjectInstance(ObjectType.Portal, 2001, 1, 0x30000000, 0, 0, oid: id);
            Assert.That(State(source), Is.SameAs(state));
            Assert.That(_session.States.Count, Is.EqualTo(1));
        }

        [Test] public void DuplicateAuthoredRecordsAreRejectedBeforeLoading()
        {
            Assert.That(_session.ValidateSector(Sector, new[] { Source(), Source() }, out var error), Is.False);
            Assert.That(error, Does.Contain("Duplicate persistent"));
            Assert.That(_session.States.Count, Is.Zero);
        }

        [Test] public void ConflictingSourceMetadataCannotOverwriteState()
        {
            var state = State(Source());
            var conflicting = new ObjectInstance(ObjectType.Portal, 2002, 1, 0x30000000, 0, 0, oid: Id(1));
            Assert.Throws<InvalidOperationException>(() => State(conflicting));
            Assert.That(_session.States[state.Identity], Is.SameAs(state));
        }

        [Test] public void NullAndRuntimeIdentitiesNeverReceivePersistentRecords()
        {
            var bytes = Id(1); bytes[0] = 254; bytes[1] = 255;
            Assert.That(State(new ObjectInstance(ObjectType.Portal, 2001, 1, 0u, 0, 0)), Is.Null);
            Assert.That(State(new ObjectInstance(ObjectType.Portal, 2001, 1, 0u, 0, 0, oid: bytes)), Is.Null);
            Assert.That(_session.States.Count, Is.Zero);
        }

        [Test] public void AbsentAndSerializedNullHaveEqualHashes()
        {
            ArcanumObjectId absent = default;
            var serialized = ArcanumObjectId.FromBytes(new byte[24]);
            Assert.That(absent, Is.EqualTo(serialized));
            Assert.That(absent.GetHashCode(), Is.EqualTo(serialized.GetHashCode()));
        }

        [Test] public void PositionalIdentityIncludesLocationLoadOrderAndMap()
        {
            const long location = 0x00017A850001B52A;
            var identity = ArcanumObjectId.CreatePositional(location, 59, 1);
            Assert.That(identity.Type, Is.EqualTo(ArcanumObjectIdType.Positional));
            Assert.That(identity.Key, Is.EqualTo("P_0001B52A_00017A85_0000003B_00000001"));
            Assert.That(identity.MapNumber, Is.EqualTo(1));
            Assert.That(ArcanumObjectId.CreatePositional(location, 60, 1), Is.Not.EqualTo(identity));
            Assert.That(ArcanumObjectId.CreatePositional(location, 59, 2), Is.Not.EqualTo(identity));
        }

        [Test] public void InventoryParentIdentityIsRetainedWithoutPresentation()
        {
            var source = new ObjectInstance(ObjectType.Portal, 2001, 1, 0u, 0, 0, oid: Id(1), parentOid: Id(2));
            var state = State(source);
            Assert.That(state.ParentIdentity, Is.EqualTo(ArcanumObjectId.FromBytes(Id(2))));
            _session.UnloadSector(Sector);
            Assert.That(State(source).ParentIdentity, Is.EqualTo(state.ParentIdentity));
        }

        [Test] public void CaptureAndReloadSurviveGameObjectDestruction()
        {
            var source = Source(type: ObjectType.Scenery);
            var state = State(source); var runtime = Runtime(state);
            runtime.ArtId = 1234; runtime.Off = true; runtime.Locked = false;
            _session.UnloadSector(Sector);
            Object.DestroyImmediate(runtime.gameObject);
            _session.BeginSector(Sector);
            var restored = Runtime(State(source));
            Assert.That(restored.ArtId, Is.EqualTo(1234));
            Assert.That(restored.Off, Is.True);
            Assert.That(restored.Locked, Is.False);
            Assert.That(restored.Identity, Is.EqualTo(source.Identity));
            Assert.That(_session.States.Count, Is.EqualTo(1));
        }

        [TestCase(0, 4, 5, 6)] [TestCase(1, 4, 5, 6)] [TestCase(2, 1, 2, 3)]
        [TestCase(3, 1, 2, 3)] [TestCase(4, 1, 2, 3)] [TestCase(5, 1, 2, 3)]
        [TestCase(6, 4, 5, 6)] [TestCase(7, 4, 5, 6)]
        public void OpenCloseFollowExactFacingFramesAndTiming(int rotation, int first, int middle, int last)
        {
            var state = State(Source(1, 0x30000000u | ((uint)rotation << 11)));
            var frames = new List<int>();
            var scheduler = _session.Portals;
            scheduler.Bind(state, 7, 8, (aid, _) => frames.Add(PortalTransitionScheduler.Frame(aid)));
            Assert.That(scheduler.Request(state.Identity, true), Is.True);
            Assert.That(frames, Is.EqualTo(new[] { first }));
            Assert.That(state.PortalOpen, Is.False);
            scheduler.Tick(.124); Assert.That(frames.Count, Is.EqualTo(1));
            scheduler.Tick(.001); Assert.That(frames, Is.EqualTo(new[] { first, middle }));
            scheduler.Tick(.125); Assert.That(frames, Is.EqualTo(new[] { first, middle, last }));
            Assert.That(state.PortalOpen, Is.True);
            Assert.That(scheduler.ActiveCount, Is.Zero);
            frames.Clear(); Assert.That(scheduler.Request(state.Identity, false), Is.True);
            scheduler.Tick(.25);
            Assert.That(frames, Is.EqualTo(new[] { middle, first, 0 }));
            Assert.That(state.PortalOpen, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void InterruptedTransitionRestoresLastStableStateBeforeCapture(bool initiallyOpen)
        {
            uint aid = initiallyOpen ? 0x30018000u : 0x30000000u;
            var state = State(Source(1, aid)); var runtime = Runtime(state);
            _session.BindPortal(state, runtime, 7, 8);
            Assert.That(runtime.RequestPortalOpen(!initiallyOpen), Is.True);
            Assert.That(_session.Portals.ActiveCount, Is.EqualTo(1));
            _session.UnloadSector(Sector);
            Assert.That(runtime.ArtId, Is.EqualTo(aid));
            Assert.That(state.ArtId, Is.EqualTo(aid));
            Assert.That(state.PortalOpen, Is.EqualTo(initiallyOpen));
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
            Assert.That(_session.Portals.BoundCount, Is.Zero);
            Object.DestroyImmediate(runtime.gameObject);
            _session.BeginSector(Sector);
            var restored = Runtime(State(Source(1, aid)));
            Assert.That(restored.IsOpen, Is.EqualTo(initiallyOpen));
            _session.Portals.Tick(100);
            Assert.That(restored.ArtId, Is.EqualTo(aid));
        }

        [Test] public void CancelIsIdempotentAndRejectsOverlappingRequests()
        {
            var state = State(Source());
            _session.Portals.Bind(state, 7, 8, null);
            Assert.That(_session.Portals.Request(state.Identity, true), Is.True);
            Assert.That(_session.Portals.Request(state.Identity, false), Is.False);
            Assert.That(_session.Portals.Cancel(state.Identity), Is.True);
            Assert.That(_session.Portals.Cancel(state.Identity), Is.False);
            _session.Portals.Tick(5);
            Assert.That(state.PortalOpen, Is.False);
        }

        [Test] public void WindowsSwitchDirectlyWithoutScheduledWork()
        {
            var state = State(Source(1, 0x30000400));
            _session.Portals.Bind(state, 2, 8, null);
            Assert.That(_session.Portals.Request(state.Identity, true), Is.True);
            Assert.That(state.PortalOpen, Is.True);
            Assert.That(_session.Portals.ActiveCount, Is.Zero);
            Assert.That(_session.Portals.Request(state.Identity, false), Is.True);
            Assert.That(state.PortalOpen, Is.False);
        }

        [TestCase(1, 8)] [TestCase(6, 8)] [TestCase(7, 0)]
        public void InvalidMetadataNeverInventsFramesOrTiming(int count, int fps)
        {
            var state = State(Source()); _session.Portals.Bind(state, count, fps, null);
            Assert.That(_session.Portals.Request(state.Identity, true), Is.False);
            Assert.That(state.ArtId, Is.EqualTo(0x30000000));
        }

        [Test] public void PresentationCannotDriveManagedPortalState()
        {
            var state = State(Source()); var runtime = Runtime(state);
            _session.BindPortal(state, runtime, 7, 8);
            runtime.SetVisualFrame = _ => true;
            Assert.That(runtime.TrySetPortalVisualFrame(6), Is.False);
            Assert.That(state.PortalOpen, Is.False);
            Assert.That(runtime.ArtId, Is.EqualTo(state.ArtId));
        }

        [Test] public void UnrelatedObjectStateIsNotMutatedByPortalScheduler()
        {
            var state = State(Source()); var other = State(Source(2));
            _session.Portals.Bind(state, 7, 8, null);
            _session.Portals.Request(state.Identity, true); _session.Portals.Tick(1);
            Assert.That(other.ArtId, Is.EqualTo(0x30000000));
            Assert.That(other.PortalOpen, Is.False);
        }
    }
}
