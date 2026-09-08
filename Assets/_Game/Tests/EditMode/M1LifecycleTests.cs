using System;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M1Lifecycle")]
    public sealed class M1LifecycleTests
    {
        private const string Sector = "maps/test/1.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M1LifecycleTest");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void OneNormalizedSelectionDrivesTerrainAndObjects()
        {
            var terrain = new FakeOwner();
            var objects = new FakeOwner();
            _session.RegisterTerrainOwner(terrain);
            _session.RegisterObjectOwner(objects);

            Assert.That(_session.SelectSector(" Maps\\Test\\1.SEC "), Is.True);
            Assert.That(_session.SelectedSector, Is.EqualTo(Sector));
            Assert.That(terrain.PresentedSector, Is.EqualTo(Sector));
            Assert.That(objects.PresentedSector, Is.EqualTo(Sector));
            Assert.That(terrain.PresentCalls, Is.EqualTo(1));
            Assert.That(objects.PresentCalls, Is.EqualTo(1));
            Assert.That(_session.SelectSector(Sector), Is.True);
            Assert.That(terrain.PresentCalls, Is.EqualTo(1), "an already-coherent selection is idempotent");
        }

        [Test]
        public void FailedOwnerCannotLeaveSplitSectorPresentation()
        {
            var terrain = new FakeOwner();
            var objects = new FakeOwner { AllowPresent = false };
            _session.RegisterTerrainOwner(terrain);
            _session.RegisterObjectOwner(objects);

            Assert.That(_session.SelectSector(Sector), Is.False);
            Assert.That(_session.HasSelectedSector, Is.False);
            Assert.That(terrain.IsSectorPresented, Is.False);
            Assert.That(objects.IsSectorPresented, Is.False);
            Assert.That(terrain.ClearCalls, Is.EqualTo(1));
            Assert.That(objects.ClearCalls, Is.EqualTo(1));
        }

        [Test]
        public void ReloadClearsAndReachesBothOwnersAgain()
        {
            var terrain = new FakeOwner();
            var objects = new FakeOwner();
            _session.RegisterTerrainOwner(terrain);
            _session.RegisterObjectOwner(objects);
            Assert.That(_session.SelectSector(Sector), Is.True);

            Assert.That(_session.ReloadSelectedSector(), Is.True);
            Assert.That(_session.SelectedSector, Is.EqualTo(Sector));
            Assert.That(terrain.PresentCalls, Is.EqualTo(2));
            Assert.That(objects.PresentCalls, Is.EqualTo(2));
            Assert.That(terrain.ClearCalls, Is.EqualTo(1));
            Assert.That(objects.ClearCalls, Is.EqualTo(1));
        }

        [Test]
        public void ProductionPlayerIdentityIsStablePersistentGuid()
        {
            ArcanumObjectId first = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            ArcanumObjectId second = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Type, Is.EqualTo(ArcanumObjectIdType.Guid));
            Assert.That(first.IsPersistent, Is.True);
        }

        [Test]
        public void ProductionPlayerPresentationIdentityUsesNormalCritterResolver()
        {
            const uint presentationArtId = 0x28100000u;
            Assert.That(ArtId.Type(presentationArtId), Is.EqualTo(ArtId.TypeCritter));
            Assert.That(CritterArtResolver.Create().Resolve(presentationArtId),
                Is.EqualTo("art/critter/hmm/hmmv1xaa.art"));
        }

        [Test]
        public void PlayerStateSurvivesBindMovementUnloadAndReload()
        {
            _session.BeginSector(Sector);
            ArcanumObjectId identity = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            uint stand = ((uint)ArtId.TypeCritter << 28) | (1u << 27) | (4u << 11);
            PersistentPlayerState state = _session.GetOrCreatePlayer(identity, Sector, new Vector2(4, 5), stand);
            WorldObject runtime = Runtime();
            _session.BindPlayer(Sector, state, runtime);
            uint walk = CritterArtResolver.WithAnimRotation(stand, 1, 6);

            Assert.That(_session.SetMovementState(identity, new Vector2(8.5f, 9.25f), walk, true), Is.True);
            _session.UnloadSector(Sector);
            Object.DestroyImmediate(runtime.gameObject);
            _session.BeginSector(Sector);
            Assert.That(_session.GetOrCreatePlayer(identity, Sector, Vector2.zero, 0), Is.SameAs(state));
            WorldObject restored = Runtime();
            _session.BindPlayer(Sector, state, restored);

            Assert.That(restored.Identity, Is.EqualTo(identity));
            Assert.That(restored.Type, Is.EqualTo(ObjectType.Pc));
            Assert.That(restored.TilePosition, Is.EqualTo(new Vector2(8.5f, 9.25f)));
            Assert.That((restored.ArtId >> 6) & 0x1F, Is.Zero);
            Assert.That(CritterArtResolver.RotationOf(restored.ArtId), Is.EqualTo(6));
            Assert.That(_session.LoadedSectorCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfiguredPlayerSelectionNeverSubstitutesNpc()
        {
            WorldObject npc = Runtime(ObjectType.Npc, ArcanumObjectId.CreateGuid(Guid.Parse("2dd6faae-7f39-4cd2-89af-59210773e950")));
            WorldObject pc = Runtime(ObjectType.Pc, ProductionPlayerLifecycle.DefaultPlayerIdentity);
            Assert.That(PlayerNavigationController.SelectProductionPlayer(new[] { npc }, null), Is.Null);
            Assert.That(PlayerNavigationController.SelectProductionPlayer(new[] { npc }, npc.Oid), Is.Null);
            Assert.That(PlayerNavigationController.SelectProductionPlayer(new[] { npc, pc }, pc.Oid), Is.SameAs(pc));
        }

        private WorldObject Runtime(ObjectType type = ObjectType.Pc, ArcanumObjectId identity = default)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(_root.transform);
            var runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            runtime.Identity = identity;
            runtime.Oid = identity.IsPersistent ? identity.Key : null;
            return runtime;
        }

        private sealed class FakeOwner : ISectorPresentationOwner
        {
            public bool AllowPresent = true;
            public int PresentCalls;
            public int ClearCalls;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;

            public bool PresentSector(string sectorPath)
            {
                PresentCalls++;
                if (!AllowPresent) return false;
                PresentedSector = sectorPath;
                return true;
            }

            public void ClearPresentedSector()
            {
                ClearCalls++;
                PresentedSector = null;
            }
        }
    }
}
