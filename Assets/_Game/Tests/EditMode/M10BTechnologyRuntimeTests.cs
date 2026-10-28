using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M10BTechnologyRuntime")]
    public sealed class M10BTechnologyRuntimeTests
    {
        private const string Sector = "maps/arcanum1-024-fixed/47781512457.sec";
        private const int OnfKos = 0x100;
        private GameObject _root;
        private WorldMapSessionCoordinator _session;
        private PersistentPlayerState _pc;
        private ArcanumObjectId _target;
        private readonly Dictionary<int, ObjectProtoInfo> _prototypes = new();

        [SetUp]
        public void SetUp()
        {
            _prototypes.Clear();
            _root = new GameObject(nameof(M10BTechnologyRuntimeTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new Owner(_session));
            Assert.That(_session.SelectSector(Sector), Is.True);
            _pc = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                Sector, new Vector2(1, 1), 0x28100000u);
            WorldObject pc = Runtime("Player", ObjectType.Pc, new Vector2Int(1, 1));
            _session.BindPlayer(Sector, _pc, pc);
            _target = AddNpc("G_11111111_1111_1111_1111_111111111111", new Vector2Int(2, 1), 8, 0, out WorldObject npc);
            SectorNavigationMap map = Map();
            map.Register(pc, 0); map.Register(npc, 0); map.SetControlledObject(pc);
            _session.Combat.BindNavigationMap(map);
            var salve = new ObjectProtoInfo(PhaseOneTechnologyCatalog.HealingSalvePrototype,
                ObjectType.Food, 0x50000000u) { ItemComplexity = -65, ItemDiscipline = 0,
                ItemSpell = 150, SpellMana = 1, ItemFlags = 0x180 };
            var axe = new ObjectProtoInfo(PhaseOneTechnologyCatalog.PowerAxePrototype,
                ObjectType.Weapon, 0x50000000u) { ItemComplexity = -40, ItemDiscipline = 6 };
            _prototypes.Add(salve.ProtoNumber, salve);
            _prototypes.Add(axe.ProtoNumber, axe);
            _session.BindPrototypeSource(number => _prototypes.TryGetValue(number, out ObjectProtoInfo value)
                ? value : null);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void CatalogAndSchematicIdentityMatchAuditedSource()
        {
            Assert.That(PhaseOneTechnologyCatalog.TryGetItem(10079, out TechnologyItemDefinition salve), Is.True);
            Assert.That((salve.Name, salve.Discipline, salve.EffectId, salve.Range, salve.ActionPointCost,
                    salve.Magnitude, salve.SourceCharges, salve.ConsumedOnUse),
                Is.EqualTo(("Healing Salve", TechnologyDiscipline.Herbology, 150, 2, 4, 20, 1, true)));
            Assert.That(PhaseOneTechnologyCatalog.BuiltInSchematicId(TechnologyDiscipline.Herbology,
                TechnologyDegree.Novice), Is.EqualTo(2000));
        }

        [Test]
        public void TechnicalManualCatalogMatchesRetailPrototypeDisciplineAndEffectMapping()
        {
            Assert.That(TechnologyManualCatalog.All.Select(value =>
                    (value.PrototypeNumber, value.Discipline, value.EffectId)),
                Is.EqualTo(new[]
                {
                    (15190, TechnologyDiscipline.Herbology, 168),
                    (15188, TechnologyDiscipline.Chemistry, 169),
                    (15191, TechnologyDiscipline.Electrical, 170),
                    (15189, TechnologyDiscipline.Explosives, 171),
                    (15195, TechnologyDiscipline.GunSmithy, 172),
                    (15193, TechnologyDiscipline.Mechanical, 173),
                    (15194, TechnologyDiscipline.Smithy, 174),
                    (15192, TechnologyDiscipline.Therapeutics, 175),
                }));
        }

        [Test]
        public void DirectTechnicalManualsAddInsertionIntelligenceStackAndIgnoreNestedCopies()
        {
            int intelligence = _session.Characters.GetEffectiveAttribute(_pc.Identity,
                CharacterAttribute.Intelligence);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.Zero);

            PersistentObjectState herbology = Manual(1, 15190, TechnologyDiscipline.Herbology);
            Manual(2, 15188, TechnologyDiscipline.Chemistry);
            PersistentObjectState container = Container(3);
            Manual(4, 15190, TechnologyDiscipline.Herbology, container.Identity);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.EqualTo(intelligence));
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Chemistry), Is.EqualTo(intelligence));

            Manual(5, 15190, TechnologyDiscipline.Herbology);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.EqualTo(intelligence * 2));
            Assert.That(_session.TransferItem(herbology.Identity, herbology.Placement,
                ObjectPlacement.ContainedBy(container.Identity)).Succeeded, Is.True);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.EqualTo(intelligence));
        }

        [Test]
        public void ManualInsertionSnapshotPersistsAndRemovalImmediatelyRevokesExpertise()
        {
            PersistentObjectState manual = Manual(6, 15190, TechnologyDiscipline.Herbology);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.EqualTo(8));
            _session.Characters.SetRace(_pc.Identity, CharacterRace.HalfOgre);
            Assert.That(_session.Characters.GetEffectiveAttribute(_pc.Identity,
                CharacterAttribute.Intelligence), Is.EqualTo(4));
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.EqualTo(8));

            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.EqualTo(8));
            Assert.That(_session.TryGetObjectState(manual.Identity, out PersistentObjectState restored), Is.True);
            Assert.That(_session.TransferItem(restored.Identity, restored.Placement,
                ObjectPlacement.InWorld(Sector, new Vector2(1, 1))).Succeeded, Is.True);
            Assert.That(_session.Technology.GetEffectiveLevel(_pc.Identity,
                TechnologyDiscipline.Herbology), Is.Zero);
        }

        [Test]
        public void SourceRanksAreSequentialAndEffectiveRankDowngradesWithIntelligence()
        {
            int[] spellTech = new int[25];
            spellTech[17 + (int)TechnologyDiscipline.Mechanical] = 3;
            _session.Technology.RegisterSourceCharacter(_target, spellTech, null);
            Assert.That(_session.Technology.GetLearnedDegree(_target, TechnologyDiscipline.Mechanical), Is.EqualTo(TechnologyDegree.Associate));
            Assert.That(_session.Technology.GetEffectiveDegree(_target, TechnologyDiscipline.Mechanical), Is.EqualTo(TechnologyDegree.Assistant));
            Assert.That(_session.Technology.KnowsBuiltInSchematic(_target, TechnologyDiscipline.Mechanical, TechnologyDegree.Associate), Is.False);
        }

        [Test]
        public void LearningConsumesOnePointUpdatesM4AptitudeAndEnforcesPrerequisite()
        {
            int points = _session.Progression.GetUnspentCharacterPoints(_pc.Identity);
            int aptitude = _session.DerivedStats.GetDerivedStat(_pc.Identity, CharacterDerivedStat.MagickTechAptitude);
            Assert.That(_session.Technology.LearnNextDegree(_pc.Identity, TechnologyDiscipline.Herbology).Succeeded, Is.True);
            Assert.That(_session.Technology.LearnNextDegree(_pc.Identity, TechnologyDiscipline.Herbology).Succeeded, Is.True);
            Assert.That(_session.Technology.LearnNextDegree(_pc.Identity, TechnologyDiscipline.Herbology).Failure,
                Is.EqualTo(TechnologyLearningFailure.InsufficientIntelligence));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(_pc.Identity), Is.EqualTo(points - 2));
            Assert.That(_session.DerivedStats.GetDerivedStat(_pc.Identity, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(aptitude - 11));
        }

        [Test]
        public void HealingSalveUsesExactM4HealingAndM3FinalDepletion()
        {
            PersistentObjectState salve = Item("G_22222222_2222_2222_2222_222222222222", ObjectType.Food, 10079);
            _session.Vitality.ApplyHitPointDamage(_target, 25);
            int before = _session.Vitality.GetCurrentHitPoints(_target);
            TechnologyUseResult result = _session.Technology.Use(new TechnologyUseRequest(_pc.Identity, salve.Identity, _target));
            Assert.That(result.Succeeded, Is.True);
            Assert.That((result.Magnitude, result.ChargesBefore, result.ChargesAfter, result.ItemConsumed), Is.EqualTo((20, 1, 0, true)));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(before + 20));
            Assert.That(_session.IsObjectRemoved(salve.Identity), Is.True);
        }

        [Test]
        public void InvalidTargetAndActorRejectBeforeAnyMutationOrConsumption()
        {
            PersistentObjectState salve = Item("G_33333333_3333_3333_3333_333333333333", ObjectType.Food, 10079);
            int hp = _session.Vitality.GetCurrentHitPoints(_target);
            Assert.That(_session.Technology.Use(new TechnologyUseRequest(_pc.Identity, salve.Identity, _target)).Failure,
                Is.EqualTo(TechnologyUseFailure.InvalidTarget));
            Assert.That(_session.Technology.Use(new TechnologyUseRequest(default, salve.Identity, _target)).Failure,
                Is.EqualTo(TechnologyUseFailure.InvalidActor));
            Assert.That(_session.TryGetObjectState(salve.Identity, out _), Is.True);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(hp));
        }

        [Test]
        public void MechanicalTargetRejectsTransactionally()
        {
            PersistentObjectState salve = Item("G_44444444_4444_4444_4444_444444444444", ObjectType.Food, 10079);
            ArcanumObjectId mechanical = AddNpc("G_55555555_5555_5555_5555_555555555555",
                new Vector2Int(2, 2), 8, unchecked((int)0x20000000), out WorldObject mech);
            _session.Vitality.ApplyHitPointDamage(mechanical, 5);
            _session.Combat.BindNavigationMap(MapWith(_pc.Identity, mech));
            Assert.That(_session.Technology.Use(new TechnologyUseRequest(_pc.Identity, salve.Identity, mechanical)).Failure,
                Is.EqualTo(TechnologyUseFailure.InvalidTarget));
            Assert.That(_session.TryGetObjectState(salve.Identity, out _), Is.True);
        }

        [Test]
        public void OutOfRangeTargetRejectsBeforeConsumptionOrVitalityMutation()
        {
            PersistentObjectState salve = Item("G_45454545_4545_4545_4545_454545454545", ObjectType.Food, 10079);
            ArcanumObjectId distant = AddNpc("G_56565656_5656_5656_5656_565656565656",
                new Vector2Int(5, 1), 8, 0, out WorldObject npc);
            _session.Vitality.ApplyHitPointDamage(distant, 5);
            int before = _session.Vitality.GetCurrentHitPoints(distant);
            _session.Combat.BindNavigationMap(MapWith(_pc.Identity, npc));

            Assert.That(_session.Technology.Use(new TechnologyUseRequest(_pc.Identity, salve.Identity, distant)).Failure,
                Is.EqualTo(TechnologyUseFailure.OutOfRange));
            Assert.That(_session.TryGetObjectState(salve.Identity, out _), Is.True);
            Assert.That(_session.Vitality.GetCurrentHitPoints(distant), Is.EqualTo(before));
        }

        [Test]
        public void TurnBasedUseConsumesExactFourApWithoutPrematureTurnAdvance()
        {
            PersistentObjectState salve = Item("G_66666666_6666_6666_6666_666666666666", ObjectType.Food, 10079);
            _session.Vitality.ApplyHitPointDamage(_target, 5);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_target).Succeeded, Is.True);
            int before = _session.Combat.CurrentActionPoints;
            TechnologyUseResult result = _session.Technology.Use(new TechnologyUseRequest(_pc.Identity, salve.Identity, _target));
            Assert.That(result.ActionPointCost, Is.EqualTo(4));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(before - 4));
            Assert.That(_session.Combat.CurrentParticipant, Is.EqualTo(_pc.Identity));
        }

        [Test]
        public void RealTimeUseUsesM8HSchedulerAndResolvesExactlyOnce()
        {
            PersistentObjectState salve = Item("G_77777777_7777_7777_7777_777777777777", ObjectType.Food, 10079);
            _session.Vitality.ApplyHitPointDamage(_target, 25);
            int before = _session.Vitality.GetCurrentHitPoints(_target);
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target, CombatMode.RealTime).Succeeded, Is.True);
            var request = new TechnologyUseRequest(_pc.Identity, salve.Identity, _target);
            Assert.That(_session.Technology.Use(request).Failure, Is.EqualTo(TechnologyUseFailure.RealTimeSchedulingRequired));
            Assert.That(_session.Combat.ScheduleRealTimeTechnology(request).Succeeded, Is.True);
            Assert.That(_session.Combat.ScheduleRealTimeTechnology(request).Failure, Is.EqualTo(CombatFailure.ActorBusy));
            _session.Combat.AdvanceRealTime(49);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(before));
            _session.Combat.AdvanceRealTime(1);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(before + 20));
            Assert.That(_session.Combat.LastRealTimeActionResolution?.TechnologyResult?.Succeeded, Is.True);
            _session.Combat.AdvanceRealTime(50);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(before + 20));
        }

        [Test]
        public void AptitudePowerAndCriticalFailureChanceUseExactSourceArithmetic()
        {
            PersistentObjectState axe = WeaponItem("G_88888888_8888_8888_8888_888888888888");
            _session.Characters.SetRace(_pc.Identity, CharacterRace.Elf);
            Assert.That(_session.DerivedStats.GetDerivedStat(_pc.Identity, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(15));
            Assert.That(_session.Technology.GetItemEffectivePower(axe, _pc.Identity), Is.EqualTo(-12));
            Assert.That(_session.Technology.GetItemAptitudeCriticalFailureChance(axe, _pc.Identity), Is.EqualTo(6));
            _session.Characters.SetRace(_pc.Identity, CharacterRace.Dwarf);
            Assert.That(_session.Technology.GetItemAptitudeCriticalFailureChance(axe, _pc.Identity), Is.Zero);
        }

        [Test]
        public void TechnologicalWeaponFailureReusesM8CriticalFailureTransaction()
        {
            WeaponItem("G_99999999_9999_9999_9999_999999999999");
            _session.Characters.SetRace(_pc.Identity, CharacterRace.Elf);
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target).Succeeded, Is.True);
            Assert.That(_session.Combat.EndCurrentTurn(_target).Succeeded, Is.True);
            int hp = _session.Vitality.GetCurrentHitPoints(_pc.Identity);
            int ap = _session.Combat.CurrentActionPoints;
            _session.Combat.SetRandomSource(new SequenceRandom(1, 100, 1, 100, 3));
            CombatAttackResult result = _session.Combat.Attack(_pc.Identity, _target);
            Assert.That(result.Succeeded, Is.True);
            Assert.That((result.Outcome, result.EffectTargetIdentity, result.CriticalEffect),
                Is.EqualTo((CombatAttackOutcome.CriticalFailure, _pc.Identity, CombatCriticalEffect.SelfHit)));
            Assert.That(_session.Combat.CurrentActionPoints, Is.EqualTo(ap - result.ActionPointCost));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_pc.Identity), Is.LessThan(hp));
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(_session.Vitality.GetMaximumHitPoints(_target)));
        }

        [Test]
        public void SaveV1RestoresRanksAndCommittedConsequencesButNoPendingTechnologyUse()
        {
            Assert.That(_session.Technology.LearnNextDegree(_pc.Identity, TechnologyDiscipline.Herbology).Succeeded, Is.True);
            PersistentObjectState salve = Item("G_AAAAAAAA_AAAA_AAAA_AAAA_AAAAAAAAAAAA", ObjectType.Food, 10079);
            _session.Vitality.ApplyHitPointDamage(_target, 25);
            _session.Combat.BindRealTimeTimingSource(new FixedTiming());
            Assert.That(_session.Combat.StartCombat(_pc.Identity, _target, CombatMode.RealTime).Succeeded, Is.True);
            Assert.That(_session.Combat.ScheduleRealTimeTechnology(new TechnologyUseRequest(_pc.Identity, salve.Identity, _target)).Succeeded, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            Assert.That(_session.Combat.IsActive, Is.False);
            Assert.That(_session.Technology.GetLearnedDegree(_pc.Identity, TechnologyDiscipline.Herbology), Is.EqualTo(TechnologyDegree.Novice));
            Assert.That(_session.TryGetObjectState(salve.Identity, out _), Is.True);
            Assert.That(_session.Vitality.GetCurrentHitPoints(_target), Is.EqualTo(_session.Vitality.GetMaximumHitPoints(_target) - 25));
        }

        private PersistentObjectState Item(string key, ObjectType type, int prototype,
            ArcanumObjectId owner = default)
        {
            ArcanumObjectId parent = owner.IsPersistent ? owner : _pc.Identity;
            var source = new ObjectInstance(type, prototype, null, 0x50000000u, 0, 0,
                oid: GuidBytes(key), parentOid: Bytes(parent));
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                itemFlags: prototype == 10079 ? 0x180 : 0);
        }

        private PersistentObjectState Manual(int sequence, int prototype,
            TechnologyDiscipline discipline, ArcanumObjectId owner = default)
        {
            _prototypes[prototype] = new ObjectProtoInfo(prototype, ObjectType.Generic, 0x50000000u)
                { ItemDiscipline = (int)discipline };
            return Item($"G_{sequence:X8}_0000_0000_0000_000000000000", ObjectType.Generic,
                prototype, owner);
        }

        private PersistentObjectState Container(int sequence)
        {
            int prototype = 19000 + sequence;
            _prototypes[prototype] = new ObjectProtoInfo(prototype, ObjectType.Container, 0x50000000u);
            return Item($"G_{sequence:X8}_1000_0000_0000_000000000000", ObjectType.Container,
                prototype);
        }

        private PersistentObjectState WeaponItem(string key)
        {
            var weapon = new Weapon { Skill = WeaponSkill.Melee, Range = 1, SpeedFactor = 10, MinStrength = 1 };
            weapon.DamageMin[(int)DamageType.Normal] = 3; weapon.DamageMax[(int)DamageType.Normal] = 3;
            var source = new ObjectInstance(ObjectType.Weapon, PhaseOneTechnologyCatalog.PowerAxePrototype,
                null, 0x50000000u, 0, 0, oid: GuidBytes(key), parentOid: PlayerBytes(),
                invLocation: (int)WornLocation.Weapon);
            return _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false,
                weaponData: weapon);
        }

        private ArcanumObjectId AddNpc(string key, Vector2Int tile, int intelligence, int critterFlags, out WorldObject runtime)
        {
            ArcanumObjectId id = Parse(key); const int prototype = 28001;
            var source = new ObjectInstance(ObjectType.Npc, prototype, Location(tile.x, tile.y), 0x28100000u, 0, 0, oid: GuidBytes(key));
            PersistentObjectState state = _session.GetOrCreate(source, Sector, source.CurrentArtId.Value, false, false);
            int[] stats = Stats(intelligence);
            _session.Characters.GetOrCreateSourceCharacter(id, ObjectType.Npc, prototype, stats, null);
            _session.Progression.GetOrCreateSourceCharacter(id, ObjectType.Npc, prototype,
                CharacterProgressionSource.Resolve(stats, null, null, null, null, null, 0));
            _session.DerivedStats.GetOrCreateSourceCharacter(id, ObjectType.Npc, prototype,
                CharacterDerivedSource.Resolve(stats, null, null, 0, null, null, null, 50, OnfKos, critterFlags));
            _session.Vitality.GetOrCreateSourceCharacter(id, ObjectType.Npc, prototype,
                CharacterVitalitySource.Resolve(stats, null, null, 0, null, 0, null, 0, null, 0, null, 0, null, 0));
            _session.Technology.RegisterSourceCharacter(id, new int[25], null);
            runtime = Runtime(key, ObjectType.Npc, tile); _session.Bind(Sector, state, runtime);
            _session.Combat.RegisterActorSource(new CombatActorSource(id, ObjectType.Npc, prototype, Sector, 1, OnfKos, critterFlags, 0,
                new[] { 1, 2, 0, 0, 0, 0, 0, 0, 0, 0 }));
            return id;
        }

        private WorldObject Runtime(string name, ObjectType type, Vector2Int tile)
        { var go = new GameObject(name); go.transform.SetParent(_root.transform); var value = go.AddComponent<WorldObject>(); value.Type = type; value.Tile = tile; value.TilePosition = tile; return value; }
        private static SectorNavigationMap Map() => new(new SectorTerrain(new uint[SectorTerrain.TileCount]),
            new bool[SectorTerrain.TileCount], TileNameTable.FromMes(MesReader.Read("{300}{grs}")));
        private SectorNavigationMap MapWith(ArcanumObjectId ignored, WorldObject npc)
        { SectorNavigationMap map = Map(); _session.TryGetLoadedObject(_pc.Identity, out WorldObject pc); map.Register(pc, 0); map.Register(npc, 0); map.SetControlledObject(pc); return map; }
        private static int[] Stats(int intelligence)
        { var s = new int[CharacterAttributeSet.SourceStatArrayCount]; for (int i = 0; i < CharacterAttributeSet.Count; i++) s[i] = 8; s[(int)CharacterAttribute.Intelligence] = intelligence; s[CharacterProgressionSource.LevelSourceSlot] = 1; s[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male; s[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human; return s; }
        private static long Location(int x, int y) => (uint)x | ((long)(uint)y << 32);
        private static ArcanumObjectId Parse(string key) { ArcanumObjectId.TryParsePersistent(key, out ArcanumObjectId id); return id; }
        private static byte[] GuidBytes(string key) { string c = key.Substring(2).Replace("_", ""); var b = new byte[24]; b[0] = (byte)ArcanumObjectIdType.Guid; for (int i = 0; i < 16; i++) b[8 + i] = Convert.ToByte(c.Substring(i * 2, 2), 16); return b; }
        private static byte[] PlayerBytes() { var b = new byte[24]; b[0] = (byte)ArcanumObjectIdType.Guid; Array.Copy(new Guid("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01").ToByteArray(), 0, b, 8, 16); return b; }
        private static byte[] Bytes(ArcanumObjectId identity)
        {
            string compact = identity.Key.Substring(2).Replace("_", "");
            var bytes = new byte[24]; bytes[0] = (byte)ArcanumObjectIdType.Guid;
            for (int i = 0; i < 16; i++) bytes[8 + i] = Convert.ToByte(compact.Substring(i * 2, 2), 16);
            return bytes;
        }

        private sealed class SequenceRandom : ICombatRandom
        { private readonly Queue<int> _v; public SequenceRandom(params int[] v) => _v = new Queue<int>(v); public int NextInclusive(int min, int max) { Assert.That(_v, Is.Not.Empty); int v = _v.Dequeue(); Assert.That(v, Is.InRange(min, max)); return v; } }
        private sealed class FixedTiming : ICombatRealTimeTimingSource
        { public bool TryGetTiming(CombatRealTimeTimingRequest request, out CombatRealTimeTiming timing) { timing = new CombatRealTimeTiming(50, 100, 50, 1, 2); return true; } }
        private sealed class Owner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _s; public Owner(WorldMapSessionCoordinator s) => _s = s;
            public string ConfiguredSector => Sector; public string PresentedSector { get; private set; } public bool IsSectorPresented => PresentedSector != null;
            public bool PresentSector(string p) { PresentedSector = WorldMapSessionCoordinator.NormalizeSector(p); _s.BeginSector(PresentedSector); return true; }
            public void ClearPresentedSector() { string p = PresentedSector; PresentedSector = null; if (p != null) _s.UnloadSector(p); }
        }
    }
}
