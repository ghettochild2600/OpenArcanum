using System;
using System.Collections.Generic;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M8EDeathConsequences")]
    public sealed class M8EDeathConsequencesTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/59726889458.sec";
        private const string SkeletonMob =
            "maps/arcanum1-024-fixed/g_a334de5a_fb86_407a_98c0_ddeb6335e848.mob";
        private const string SkeletonKey = "G_5ADE34A3_86FB_7A40_98C0_DDEB6335E848";
        private const string GoldKey = "G_6413F64C_29FD_4A44_8E2C_E9A414888116";
        private const string SwordKey = "G_2EB46E07_6D15_AD4C_87A1_B8543AA54F91";
        private const int SkeletonPrototype = 28460;
        private const int OnfKos = 0x00000100;
        private const int SkeletonNpcFlags = 4418;
        private const int SkeletonCritterFlags = 67149828;
        private const int SkeletonExperienceWorth = 440;
        private static readonly int[] SkeletonStats =
            { 9, 12, 8, 2, 5, 9, 5, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, -150, 0, 5, 0, 0, 0, 20, 1, 0 };
        private static readonly int[] SkeletonDamage = { 3, 9, 0, 0, 0, 0, 0, 0, 0, 0 };

        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _skeleton;
        private ArcanumObjectId _gold;
        private ArcanumObjectId _sword;
        private SectorNavigationMap _map;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("M8EDeathConsequencesTests");
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.BindPrototypeSource(Prototype);
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject pcRuntime = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, pcRuntime);
            _session.Combat.RegisterActorSource(new CombatActorSource(_pc.Identity, ObjectType.Pc, null,
                Sector, int.MaxValue, 0, 0, 0, new[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 }));

            _skeleton = ParseIdentity(SkeletonKey);
            var source = new ObjectInstance(ObjectType.Npc, SkeletonPrototype, Location(2, 1),
                0x28100000u, 0, 0, oid: GuidBytes(SkeletonKey));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            _session.Characters.GetOrCreateSourceCharacter(_skeleton, ObjectType.Npc, SkeletonPrototype,
                SkeletonStats, null);
            _session.Progression.GetOrCreateSourceCharacter(_skeleton, ObjectType.Npc, SkeletonPrototype,
                CharacterProgressionSource.Resolve(SkeletonStats, null, null, null, null, null,
                    SkeletonCritterFlags));
            _session.DerivedStats.GetOrCreateSourceCharacter(_skeleton, ObjectType.Npc, SkeletonPrototype,
                CharacterDerivedSource.Resolve(SkeletonStats, null, null, 0, new[] { 20, 20, 20, 20, 20 },
                    null, null, 50, SkeletonNpcFlags, SkeletonCritterFlags));
            _session.Vitality.GetOrCreateSourceCharacter(_skeleton, ObjectType.Npc, SkeletonPrototype,
                CharacterVitalitySource.Resolve(SkeletonStats, null, null, 0, null, 0, null, 0,
                    null, 0, null, 0, null, 0));
            WorldObject skeletonRuntime = Runtime("Greater Skeleton", ObjectType.Npc, new Vector2Int(2, 1));
            _session.Bind(Sector, state, skeletonRuntime);
            _session.Combat.RegisterActorSource(new CombatActorSource(_skeleton, ObjectType.Npc,
                SkeletonPrototype, Sector, 10, SkeletonNpcFlags, SkeletonCritterFlags, 0,
                SkeletonDamage, dyingScriptNum: 0, experienceWorth: SkeletonExperienceWorth));

            _gold = AddItem(GoldKey, ObjectType.Gold, 9076, 0, 0, 89, 1).Identity;
            _sword = AddItem(SwordKey, ObjectType.Weapon, 6050, (int)WornLocation.Weapon,
                8192, null, 80).Identity;

            _map = Map();
            _map.Register(pcRuntime, 0);
            _map.Register(skeletonRuntime, 0);
            _map.SetControlledObject(pcRuntime);
            _session.Combat.BindNavigationMap(_map);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void AuthenticFixtureHasNoDyingScriptAndCarriesRealLoot()
        {
            string module = GameDataLocator.Find("modules/Arcanum.dat");
            string protoDirectory = GameDataLocator.FindDirectory("data/proto");
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(protoDirectory))
                Assert.Ignore("The local clean Arcanum data is unavailable.");
            using var vfs = new DatVirtualFileSystem();
            vfs.MountFile(module);
            int offset = 0;
            ObjectInstance instance = ObjectInstanceReader.Read(vfs.ReadAllBytes(SkeletonMob), ref offset);
            ObjectProtoInfo prototype = new ProtoLibrary(protoDirectory).Get(instance.PrototypeNumber);

            Assert.That(instance.Identity.Key, Is.EqualTo(SkeletonKey));
            Assert.That(instance.PrototypeNumber, Is.EqualTo(SkeletonPrototype));
            Assert.That(instance.MapX, Is.EqualTo(31883));
            Assert.That(instance.MapY, Is.EqualTo(56988));
            Assert.That(instance.NpcFlags.Value & OnfKos, Is.Not.Zero);
            Assert.That(instance.DyingScriptNum, Is.Zero);
            Assert.That(prototype.DyingScriptNum, Is.Zero);
            Assert.That(prototype.ExperienceWorth, Is.EqualTo(SkeletonExperienceWorth));

            AssertChild(vfs, "maps/arcanum1-024-fixed/g_4cf61364_fd29_444a_8e2c_e9a414888116.mob",
                GoldKey, ObjectType.Gold, 9076, 0, 89);
            AssertChild(vfs, "maps/arcanum1-024-fixed/g_076eb42e_156d_4cad_87a1_b8543aa54f91.mob",
                SwordKey, ObjectType.Weapon, 6050, (int)WornLocation.Weapon, null);
        }

        [Test]
        public void LethalPcAttackAwardsFinalSourceShareExactlyOnce()
        {
            int before = _session.Progression.Get(_pc.Identity).Experience;
            KillWithPcAttack();

            Assert.That(_session.Progression.Get(_pc.Identity).Experience, Is.EqualTo(before + 88));
            Assert.That(_session.TryGetObjectState(_skeleton, out PersistentObjectState corpse), Is.True);
            Assert.That(corpse.DeathConsequencesProcessed, Is.True);
            Assert.That(_session.TryGetObjectState(_gold, out PersistentObjectState gold), Is.True);
            Assert.That(gold.ParentIdentity, Is.EqualTo(_skeleton));
            Assert.That(_session.TryGetObjectState(_sword, out PersistentObjectState sword), Is.True);
            Assert.That(sword.ParentIdentity, Is.EqualTo(_skeleton));

            DeathConsequenceResult replay = _session.DeathConsequences.Process(_pc.Identity, _skeleton);
            Assert.That(replay.Failure, Is.EqualTo(DeathConsequenceFailure.AlreadyProcessed));
            Assert.That(_session.Progression.Get(_pc.Identity).Experience, Is.EqualTo(before + 88));
        }

        [Test]
        public void UnsupportedDyingScriptRejectsLethalAttackBeforeMutation()
        {
            ArcanumObjectId scripted = AddNpc("G_11111111_1111_1111_1111_111111111111", 28460,
                new Vector2Int(2, 2), 20, dyingScript: 1658);
            _session.Vitality.ApplyHitPointDamage(scripted,
                _session.Vitality.GetCurrentHitPoints(scripted) - 1);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, scripted).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(scripted).Succeeded, Is.True);
            int actionPoints = _session.Combat.CurrentActionPoints;
            _session.Combat.SetRandomSource(new SequenceRandom(1, 4, 4));

            CombatAttackResult result = _session.Combat.Attack(_pc.Identity, scripted);

            Assert.That(result.Failure, Is.EqualTo(CombatFailure.UnresolvedDeathScript));
            Assert.That(_session.Vitality.GetCurrentHitPoints(scripted), Is.EqualTo(1));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(actionPoints));
            Assert.That(_session.TryGetObjectState(scripted, out PersistentObjectState state), Is.True);
            Assert.That(state.DeathConsequencesProcessed, Is.False);
        }

        [Test]
        public void AuthenticOrdinaryAndEquippedItemsMoveFromCorpseWithoutCloning()
        {
            KillWithPcAttack();

            CorpseLootResult gold = _session.DeathConsequences.LootItem(_skeleton, _pc.Identity, _gold);
            CorpseLootResult sword = _session.DeathConsequences.LootItem(_skeleton, _pc.Identity, _sword);

            Assert.That(gold.Succeeded, Is.True, gold.Transfer.Code.ToString());
            Assert.That(sword.Succeeded, Is.True, sword.Transfer.Code.ToString());
            Assert.That(_session.TryGetObjectState(_gold, out PersistentObjectState goldState), Is.True);
            Assert.That(goldState.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_pc.Identity)));
            Assert.That(goldState.StackQuantity, Is.EqualTo(89));
            Assert.That(_session.TryGetObjectState(_sword, out PersistentObjectState swordState), Is.True);
            Assert.That(swordState.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_pc.Identity)));
            Assert.That(_session.States.ContainsKey(_gold), Is.True);
            Assert.That(_session.States.ContainsKey(_sword), Is.True);
        }

        [Test]
        public void SaveV1RestoresProcessedMarkerAndPreventsRewardReplay()
        {
            KillWithPcAttack();
            int experience = _session.Progression.Get(_pc.Identity).Experience;
            string json = _session.SaveGames.SerializeCurrentSession();

            Assert.That(json, Does.Contain("\"version\": 1"));
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.TryGetObjectState(_skeleton, out PersistentObjectState corpse), Is.True);
            Assert.That(corpse.DeathConsequencesProcessed, Is.True);
            DeathConsequenceResult replay = _session.DeathConsequences.Process(_pc.Identity, _skeleton);
            Assert.That(replay.Failure, Is.EqualTo(DeathConsequenceFailure.AlreadyProcessed));
            Assert.That(_session.Progression.Get(_pc.Identity).Experience, Is.EqualTo(experience));
        }

        [Test]
        public void InvalidConsequenceInputsFailWithoutMutation()
        {
            int experience = _session.Progression.Get(_pc.Identity).Experience;
            ArcanumObjectId missing = ParseIdentity("G_FFFFFFFF_FFFF_FFFF_FFFF_FFFFFFFFFFFF");

            Assert.That(_session.DeathConsequences.Process(_pc.Identity, _skeleton).Failure,
                Is.EqualTo(DeathConsequenceFailure.VictimNotDead));
            Assert.That(_session.DeathConsequences.Process(_pc.Identity, missing).Failure,
                Is.EqualTo(DeathConsequenceFailure.VictimNotFound));
            Assert.That(_session.DeathConsequences.LootItem(_skeleton, _pc.Identity, _gold).Failure,
                Is.EqualTo(CorpseLootFailure.CorpseNotDead));

            _session.Vitality.ApplyHitPointDamage(_skeleton,
                _session.Vitality.GetCurrentHitPoints(_skeleton));
            Assert.That(_session.DeathConsequences.Process(missing, _skeleton).Failure,
                Is.EqualTo(DeathConsequenceFailure.InvalidKiller));
            Assert.That(_session.TryGetObjectState(_skeleton, out PersistentObjectState corpse), Is.True);
            Assert.That(corpse.DeathConsequencesProcessed, Is.False);
            Assert.That(_session.Progression.Get(_pc.Identity).Experience, Is.EqualTo(experience));
            Assert.That(_session.TryGetObjectState(_gold, out PersistentObjectState gold), Is.True);
            Assert.That(gold.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_skeleton)));
        }

        [Test]
        public void CorpseLootCapacityAndInvalidEquipmentFailuresRollBack()
        {
            ArcanumObjectId heavy = AddItem("G_22222222_2222_2222_2222_222222222222",
                ObjectType.Food, 10078, 5, 0, null, 10001).Identity;
            ArcanumObjectId noDrop = AddItem("G_33333333_3333_3333_3333_333333333333",
                ObjectType.Armor, 8300, (int)WornLocation.Ring1, 0x20, null, 1).Identity;
            KillWithPcAttack();

            CorpseLootResult tooHeavy = _session.DeathConsequences.LootItem(_skeleton, _pc.Identity, heavy);
            CorpseLootResult invalidEquipment = _session.DeathConsequences.LootItem(
                _skeleton, _pc.Identity, noDrop);

            Assert.That(tooHeavy.Failure, Is.EqualTo(CorpseLootFailure.TransferFailed));
            Assert.That(tooHeavy.Transfer.Code, Is.EqualTo(InventoryResultCode.TooHeavy));
            Assert.That(invalidEquipment.Failure, Is.EqualTo(CorpseLootFailure.TransferFailed));
            Assert.That(invalidEquipment.Transfer.Code,
                Is.EqualTo(InventoryResultCode.EquipmentCommandRequired));
            Assert.That(_session.TryGetObjectState(heavy, out PersistentObjectState heavyState), Is.True);
            Assert.That(heavyState.Placement, Is.EqualTo(ObjectPlacement.ContainedBy(_skeleton)));
            Assert.That(_session.TryGetObjectState(noDrop, out PersistentObjectState noDropState), Is.True);
            Assert.That(noDropState.Placement,
                Is.EqualTo(ObjectPlacement.EquippedBy(_skeleton, WornLocation.Ring1)));
        }

        private void KillWithPcAttack()
        {
            _session.Vitality.ApplyHitPointDamage(_skeleton,
                _session.Vitality.GetCurrentHitPoints(_skeleton) - 1);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _skeleton).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_skeleton).Succeeded, Is.True);
            _session.Combat.SetRandomSource(new SequenceRandom(1, 4, 4));
            CombatAttackResult result = _session.Combat.Attack(_pc.Identity, _skeleton);
            Assert.That(result.Succeeded && result.Hit, Is.True);
            Assert.That(_session.Vitality.IsDead(_skeleton), Is.True);
        }

        private ArcanumObjectId AddNpc(string key, int prototype, Vector2Int tile, int sourceOrder,
            int dyingScript)
        {
            ArcanumObjectId identity = ParseIdentity(key);
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y),
                0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value,
                false, false);
            _session.Characters.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                SkeletonStats, null);
            _session.Progression.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(SkeletonStats, null, null, null, null, null,
                    SkeletonCritterFlags));
            _session.DerivedStats.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(SkeletonStats, null, null, 0, new[] { 20, 20, 20, 20, 20 },
                    null, null, 50, SkeletonNpcFlags, SkeletonCritterFlags));
            _session.Vitality.GetOrCreateSourceCharacter(identity, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(SkeletonStats, null, null, 0, null, 0, null, 0,
                    null, 0, null, 0, null, 0));
            WorldObject runtime = Runtime(key, ObjectType.Npc, tile);
            _session.Bind(Sector, state, runtime);
            _map.Register(runtime, 0);
            _session.Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Npc, prototype,
                Sector, sourceOrder, SkeletonNpcFlags, SkeletonCritterFlags, 0, SkeletonDamage,
                dyingScript, SkeletonExperienceWorth));
            return identity;
        }

        private PersistentObjectState AddItem(string key, ObjectType type, int prototype, int inventoryLocation,
            int itemFlags, int? stackQuantity, int weight)
        {
            var source = new ObjectInstance(type, prototype, null, 0x50000000u, 0, 0,
                oid: GuidBytes(key), parentOid: GuidBytes(SkeletonKey), invLocation: inventoryLocation);
            if (type == ObjectType.Gold) source.GoldQuantity = stackQuantity;
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                itemFlags, null, 0, 0, stackQuantity, weight, InventoryFootprint.OneCell,
                inventoryLocation);
        }

        private static void AssertChild(DatVirtualFileSystem vfs, string path, string identity,
            ObjectType type, int prototype, int inventoryLocation, int? goldQuantity)
        {
            int offset = 0;
            ObjectInstance item = ObjectInstanceReader.Read(vfs.ReadAllBytes(path), ref offset);
            Assert.That(item.Identity.Key, Is.EqualTo(identity));
            Assert.That(item.ParentIdentity.Key, Is.EqualTo(SkeletonKey));
            Assert.That(item.Type, Is.EqualTo(type));
            Assert.That(item.PrototypeNumber, Is.EqualTo(prototype));
            Assert.That(item.InvLocation, Is.EqualTo(inventoryLocation));
            if (goldQuantity.HasValue) Assert.That(item.GoldQuantity, Is.EqualTo(goldQuantity));
        }

        private ObjectProtoInfo Prototype(int number) => number switch
        {
            SkeletonPrototype => new ObjectProtoInfo(number, ObjectType.Npc, 0x28100000u)
            {
                ExperienceWorth = SkeletonExperienceWorth,
            },
            9076 => new ObjectProtoInfo(number, ObjectType.Gold, 0x50000000u) { GoldQuantity = 89 },
            6050 => new ObjectProtoInfo(number, ObjectType.Weapon, 0x50000000u),
            _ => null,
        };

        private WorldObject Runtime(string name, ObjectType type, Vector2Int tile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            WorldObject runtime = go.AddComponent<WorldObject>();
            runtime.Type = type;
            runtime.Tile = tile;
            runtime.TilePosition = tile;
            runtime.ArtId = 0x28100000u;
            runtime.Blocks = true;
            return runtime;
        }

        private static SectorNavigationMap Map()
        {
            var names = Arcanum.Formats.Tiles.TileNameTable.FromMes(
                Arcanum.Formats.Text.MesReader.Read("{300}{grs}"));
            return new SectorNavigationMap(new Arcanum.Formats.World.SectorTerrain(
                    new uint[Arcanum.Formats.World.SectorTerrain.TileCount]),
                new bool[Arcanum.Formats.World.SectorTerrain.TileCount], names);
        }

        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);

        private static ArcanumObjectId ParseIdentity(string key)
        {
            if (!ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId identity))
                throw new InvalidOperationException("Invalid test ObjectID: " + key);
            return identity;
        }

        private static byte[] GuidBytes(string key)
        {
            string compact = key.Substring(2).Replace("_", string.Empty);
            var bytes = new byte[ArcanumObjectId.SerializedSize];
            bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int index = 0; index < 16; index++)
                bytes[8 + index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
            return bytes;
        }

        private sealed class SequenceRandom : ICombatRandom
        {
            private readonly Queue<int> _values;
            public SequenceRandom(params int[] values) => _values = new Queue<int>(values);
            public int NextInclusive(int minimum, int maximum)
            {
                Assert.That(_values, Is.Not.Empty, "combat requested unexpected RNG");
                int value = _values.Dequeue();
                Assert.That(value, Is.InRange(minimum, maximum));
                return value;
            }
        }

        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public Owner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => Sector;
            public string PresentedSector { get; private set; }
            public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string sectorPath)
            {
                PresentedSector = WorldMapSessionCoordinator.NormalizeSector(sectorPath);
                _session.BeginSector(PresentedSector);
                return true;
            }
            public void ClearPresentedSector()
            {
                string sector = PresentedSector;
                PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
