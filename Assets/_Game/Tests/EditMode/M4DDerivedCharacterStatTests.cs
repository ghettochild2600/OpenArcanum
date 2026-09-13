using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M4DDerivedCharacterStats")]
    public sealed class M4DDerivedCharacterStatTests
    {
        private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
            Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));

        private CharacterStatService _characters;
        private CharacterProgressionService _progression;
        private CharacterDerivedStatService _derived;

        [SetUp]
        public void SetUp()
        {
            _characters = new CharacterStatService();
            _progression = new CharacterProgressionService(_characters);
            _derived = new CharacterDerivedStatService(_characters, _progression);
        }

        [Test]
        public void TypedDerivedAndResistanceIdsMatchSource()
        {
            Assert.That(CharacterDerivedStatRules.AllStats.Select(value => (int)value),
                Is.EqualTo(Enumerable.Range(8, 9)));
            Assert.That(CharacterDerivedStatRules.AllResistances.Select(value => (int)value),
                Is.EqualTo(Enumerable.Range(0, 5)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _derived.GetDerivedStat(Identity(99),
                (CharacterDerivedStat)17));
            Assert.Throws<ArgumentOutOfRangeException>(() => _derived.GetResistance(Identity(99),
                (CharacterResistance)5));
        }

        [Test]
        public void SourceResolutionUsesWholeFieldInstancePrecedenceAndFlags()
        {
            int[] instanceStats = Stats(new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, CharacterRace.Human,
                CharacterGender.Female, alignment: 100, magickPoints: 0, techPoints: 1);
            int[] prototypeStats = Stats(Defaults(), CharacterRace.Dwarf, CharacterGender.Male,
                alignment: -200, magickPoints: 9, techPoints: 8);
            CharacterDerivedSource source = CharacterDerivedSource.Resolve(instanceStats, prototypeStats,
                7, 3, new[] { 1, 2, 3, 4, 5 }, new[] { 9, 9, 9, 9, 9 }, 60, 40,
                CharacterDerivedSource.AloofNpcFlag, CharacterProgressionSource.MonstrousCritterFlags);
            Assert.That(source.BaseArmorClass, Is.EqualTo(7));
            Assert.That(source.Alignment, Is.EqualTo(100));
            Assert.That(source.MagickPoints, Is.Zero);
            Assert.That(source.TechPoints, Is.EqualTo(1));
            Assert.That(source.ReactionBase, Is.EqualTo(60));
            Assert.That(source.IsAloof && source.IsMonstrous, Is.True);
            Assert.That(source.HasInstanceStatOverride && source.HasInstanceArmorClassOverride
                        && source.HasInstanceResistanceOverride && source.HasInstanceReactionOverride, Is.True);
            Assert.That(CharacterDerivedStatRules.AllResistances.Select(source.GetResistance),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void SourceResolutionFallsBackToPrototypeAndDefaults()
        {
            CharacterDerivedSource source = CharacterDerivedSource.Resolve(null,
                Stats(Defaults(), CharacterRace.Human, CharacterGender.Male, alignment: -25,
                    magickPoints: 4, techPoints: 6),
                null, 11, null, new[] { 6, 7, 8, 9, 10 }, null, null, 0, 0);
            Assert.That(source.BaseArmorClass, Is.EqualTo(11));
            Assert.That(source.Alignment, Is.EqualTo(-25));
            Assert.That(source.MagickPoints, Is.EqualTo(4));
            Assert.That(source.TechPoints, Is.EqualTo(6));
            Assert.That(source.ReactionBase, Is.EqualTo(50));
            Assert.That(source.HasInstanceStatOverride || source.HasInstanceArmorClassOverride
                        || source.HasInstanceResistanceOverride || source.HasInstanceReactionOverride, Is.False);
            Assert.That(CharacterDerivedStatRules.AllResistances.Select(source.GetResistance),
                Is.EqualTo(new[] { 6, 7, 8, 9, 10 }));
        }

        [Test]
        public void DevelopmentPlayerHasExplicitDeterministicBaseline()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            PersistentCharacterDerivedState state = _derived.Get(id);
            Assert.That(state.ObjectType, Is.EqualTo(ObjectType.Pc));
            Assert.That(state.PrototypeNumber, Is.Null);
            Assert.That(state.Source, Is.SameAs(CharacterDerivedSource.DevelopmentPlayer));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MeleeDamageBonus), Is.EqualTo(-1));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.ArmorClassAdjustment), Is.EqualTo(-2));
            Assert.That(_derived.GetArmorClass(id), Is.Zero);
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.Speed), Is.EqualTo(8));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.HealRate), Is.EqualTo(3));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.PoisonRecovery), Is.EqualTo(8));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.BeautyReactionModifier), Is.EqualTo(-7));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MaximumFollowers), Is.EqualTo(2));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MagickTechAptitude), Is.Zero);
            Assert.That(_derived.GetAlignment(id), Is.Zero);
            Assert.That(_derived.GetResistance(id, CharacterResistance.Poison), Is.EqualTo(20));
        }

        [Test]
        public void RealFixtureMatchesAuditedDerivedValues()
        {
            ArcanumObjectId id = RegisterSource(FixtureIdentity, ObjectType.Npc, 17101,
                Stats(new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, CharacterRace.Human,
                    CharacterGender.Female, alignment: 100, magickPoints: 0, techPoints: 1));
            Assert.That(id.Key, Is.EqualTo("G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F"));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MeleeDamageBonus), Is.Zero);
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.ArmorClassAdjustment), Is.EqualTo(-1));
            Assert.That(_derived.GetArmorClass(id), Is.Zero);
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.Speed), Is.EqualTo(9));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.HealRate), Is.EqualTo(5));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.PoisonRecovery), Is.EqualTo(16));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.BeautyReactionModifier), Is.Zero);
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MaximumFollowers), Is.EqualTo(2));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(-5));
            Assert.That(_derived.GetAlignment(id), Is.EqualTo(100));
            Assert.That(_derived.GetResistance(id, CharacterResistance.Poison), Is.EqualTo(60));
        }

        [Test]
        public void NegativeDamageUsesSourceIntegerTruncation()
        {
            ArcanumObjectId one = RegisterSource(Identity(1), ObjectType.Npc, 1001,
                Stats(Primary(strength: 1), CharacterRace.Human, CharacterGender.Male));
            ArcanumObjectId nine = RegisterSource(Identity(2), ObjectType.Npc, 1002,
                Stats(Primary(strength: 9), CharacterRace.Human, CharacterGender.Male));
            Assert.That(_derived.GetDerivedStat(one, CharacterDerivedStat.MeleeDamageBonus), Is.EqualTo(-4));
            Assert.That(_derived.GetDerivedStat(nine, CharacterDerivedStat.MeleeDamageBonus), Is.Zero);
        }

        [Test]
        public void ExtraordinaryStrengthDexterityAndBeautyBranchesMatchSource()
        {
            ArcanumObjectId id = RegisterSource(Identity(3), ObjectType.Pc, 1003,
                Stats(new[] { 20, 20, 8, 20, 8, 8, 8, 8 }, CharacterRace.Human, CharacterGender.Male));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MeleeDamageBonus), Is.EqualTo(20));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.ArmorClassAdjustment), Is.EqualTo(10));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.Speed), Is.EqualTo(25));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.BeautyReactionModifier), Is.EqualTo(100));
        }

        [Test]
        public void NaturalLowAndHighInputsObserveEveryDerivedSourceBound()
        {
            ArcanumObjectId low = RegisterSource(Identity(30), ObjectType.Pc, 1030,
                Stats(new[] { 1, 1, 1, 1, 1, 1, 1, 1 }, CharacterRace.Human, CharacterGender.Male));
            Assert.That(new[]
            {
                _derived.GetDerivedStat(low, CharacterDerivedStat.MeleeDamageBonus),
                _derived.GetDerivedStat(low, CharacterDerivedStat.ArmorClassAdjustment),
                _derived.GetDerivedStat(low, CharacterDerivedStat.Speed),
                _derived.GetDerivedStat(low, CharacterDerivedStat.HealRate),
                _derived.GetDerivedStat(low, CharacterDerivedStat.PoisonRecovery),
                _derived.GetDerivedStat(low, CharacterDerivedStat.BeautyReactionModifier),
                _derived.GetDerivedStat(low, CharacterDerivedStat.MaximumFollowers),
            }, Is.EqualTo(new[] { -4, -9, 1, 0, 1, -65, 1 }));

            ArcanumObjectId high = RegisterSource(Identity(31), ObjectType.Pc, 1031,
                Stats(Enumerable.Repeat(20, 8).ToArray(), CharacterRace.Human, CharacterGender.Male));
            Assert.That(new[]
            {
                _derived.GetDerivedStat(high, CharacterDerivedStat.MeleeDamageBonus),
                _derived.GetDerivedStat(high, CharacterDerivedStat.ArmorClassAdjustment),
                _derived.GetDerivedStat(high, CharacterDerivedStat.Speed),
                _derived.GetDerivedStat(high, CharacterDerivedStat.HealRate),
                _derived.GetDerivedStat(high, CharacterDerivedStat.PoisonRecovery),
                _derived.GetDerivedStat(high, CharacterDerivedStat.BeautyReactionModifier),
                _derived.GetDerivedStat(high, CharacterDerivedStat.MaximumFollowers),
            }, Is.EqualTo(new[] { 20, 10, 25, 6, 20, 100, 5 }));
        }

        [Test]
        public void AptitudeUsesExactSignConventionAndFinalClamp()
        {
            ArcanumObjectId magick = RegisterSource(Identity(32), ObjectType.Pc, 1032,
                Stats(Defaults(), CharacterRace.Human, CharacterGender.Male, magickPoints: 210));
            ArcanumObjectId tech = RegisterSource(Identity(33), ObjectType.Pc, 1033,
                Stats(Defaults(), CharacterRace.Human, CharacterGender.Male, techPoints: 210));
            Assert.That(_derived.GetDerivedStat(magick, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(100));
            Assert.That(_derived.GetDerivedStat(tech, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(-100));
        }

        [Test]
        public void SourceAlignmentAndAptitudeInputsClampBeforePersistentInitialization()
        {
            CharacterDerivedSource source = CharacterDerivedSource.Resolve(
                Stats(Defaults(), CharacterRace.Human, CharacterGender.Male, alignment: int.MaxValue,
                    magickPoints: int.MaxValue, techPoints: int.MinValue),
                null, null, null, null, null, null, null, 0, 0);
            Assert.That(source.Alignment, Is.EqualTo(CharacterDerivedStatRules.MaximumAlignment));
            Assert.That(source.MagickPoints, Is.EqualTo(CharacterDerivedStatRules.MaximumAptitudePoints));
            Assert.That(source.TechPoints, Is.Zero);
        }

        [Test]
        public void ArmorClassCombinesStoredBaseAndDexterityThenClamps()
        {
            ArcanumObjectId low = RegisterSource(Identity(4), ObjectType.Npc, 1004,
                Stats(Primary(dexterity: 1), CharacterRace.Human, CharacterGender.Male), baseArmorClass: 3);
            ArcanumObjectId high = RegisterSource(Identity(5), ObjectType.Npc, 1005,
                Stats(Primary(dexterity: 20), CharacterRace.Human, CharacterGender.Male), baseArmorClass: 90);
            Assert.That(_derived.GetArmorClass(low), Is.Zero);
            Assert.That(_derived.GetArmorClass(high), Is.EqualTo(95));
        }

        [Test]
        public void PersuasionExpertAddsExactlyOneFollowerSlot()
        {
            int[] basic = new int[CharacterSkillRules.BasicSkillCount];
            basic[CharacterSkillRules.SourceGroupIndex(CharacterSkill.Persuasion)] = 2 | (2 << 6);
            ArcanumObjectId id = RegisterSource(Identity(6), ObjectType.Pc, 1006,
                Stats(Primary(charisma: 12), CharacterRace.Human, CharacterGender.Male), basic: basic);
            Assert.That(_progression.GetTrainingLevel(id, CharacterSkill.Persuasion),
                Is.EqualTo(SkillTrainingLevel.Expert));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MaximumFollowers), Is.EqualTo(4));
        }

        [Test]
        public void AptitudeUsesStoredPointsAndAuditedRaceEffects()
        {
            ArcanumObjectId dwarf = RegisterSource(Identity(7), ObjectType.Pc, 1007,
                Stats(Defaults(), CharacterRace.Dwarf, CharacterGender.Male, magickPoints: 0, techPoints: 0));
            ArcanumObjectId elf = RegisterSource(Identity(8), ObjectType.Pc, 1008,
                Stats(Defaults(), CharacterRace.Elf, CharacterGender.Male, magickPoints: 0, techPoints: 0));
            Assert.That(_derived.GetStoredTechPoints(dwarf), Is.Zero);
            Assert.That(_derived.GetEffectiveTechPoints(dwarf), Is.EqualTo(3));
            Assert.That(_derived.GetDerivedStat(dwarf, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(-16));
            Assert.That(_derived.GetEffectiveMagickPoints(elf), Is.EqualTo(3));
            Assert.That(_derived.GetDerivedStat(elf, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(15));
        }

        [Test]
        public void ResistanceOrderingAddsInnateRaceAndPoisonConstitutionBeforeClamp()
        {
            ArcanumObjectId orc = RegisterSource(Identity(9), ObjectType.Npc, 1009,
                Stats(Primary(constitution: 8), CharacterRace.Orc, CharacterGender.Male),
                resistances: new[] { 0, 1, 2, 3, 4 });
            Assert.That(_derived.GetResistance(orc, CharacterResistance.Normal), Is.Zero);
            Assert.That(_derived.GetResistance(orc, CharacterResistance.Fire), Is.EqualTo(1));
            Assert.That(_derived.GetResistance(orc, CharacterResistance.Electrical), Is.EqualTo(2));
            Assert.That(_derived.GetResistance(orc, CharacterResistance.Poison), Is.EqualTo(53));
            Assert.That(_derived.GetResistance(orc, CharacterResistance.Magic), Is.EqualTo(4));
        }

        [Test]
        public void MonstrousNpcResistancePreservesSourceNoFinalClampException()
        {
            ArcanumObjectId ordinary = RegisterSource(Identity(10), ObjectType.Npc, 1010,
                Stats(Primary(constitution: 20), CharacterRace.Human, CharacterGender.Male),
                resistances: new[] { 120, 0, 0, 90, 0 });
            ArcanumObjectId monster = RegisterSource(Identity(11), ObjectType.Npc, 1011,
                Stats(Primary(constitution: 20), CharacterRace.Human, CharacterGender.Male),
                resistances: new[] { 120, 0, 0, 90, 0 }, isMonstrous: true);
            Assert.That(_derived.GetResistance(ordinary, CharacterResistance.Normal), Is.EqualTo(95));
            Assert.That(_derived.GetResistance(ordinary, CharacterResistance.Poison), Is.EqualTo(95));
            Assert.That(_derived.GetResistance(monster, CharacterResistance.Normal), Is.EqualTo(120));
            Assert.That(_derived.GetResistance(monster, CharacterResistance.Poison), Is.EqualTo(170));
        }

        [Test]
        public void AlignmentMutationClampsAndReportsOnlyActualChanges()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            int events = 0;
            int prior = 0;
            int current = 0;
            _derived.AlignmentChanged += (changed, previous, next) =>
            {
                Assert.That(changed, Is.EqualTo(id));
                events++;
                prior = previous;
                current = next;
            };
            Assert.That(_derived.SetAlignment(id, 1200), Is.EqualTo(1000));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(prior, Is.Zero);
            Assert.That(current, Is.EqualTo(1000));
            Assert.That(_derived.AdjustAlignment(id, int.MinValue), Is.EqualTo(-1000));
            Assert.That(events, Is.EqualTo(2));
            Assert.That(_derived.SetAlignment(id, -1000), Is.EqualTo(-1000));
            Assert.That(events, Is.EqualTo(2));
        }

        [Test]
        public void ReactionInputsUseNpcBasePlayerBeautyAndExactRaceMatrix()
        {
            ArcanumObjectId pc = RegisterDevelopmentPlayer();
            ArcanumObjectId npc = RegisterSource(FixtureIdentity, ObjectType.Npc, 17101,
                Stats(new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, CharacterRace.Human,
                    CharacterGender.Female), reactionBase: 50);
            CharacterReactionInputs fixture = _derived.GetReactionInputs(npc, pc);
            Assert.That(fixture.CharacterModifiersApply, Is.True);
            Assert.That(fixture.SourceBase, Is.EqualTo(50));
            Assert.That(fixture.BeautyModifier, Is.EqualTo(-7));
            Assert.That(fixture.RaceModifier, Is.Zero);
            Assert.That(fixture.Subtotal, Is.EqualTo(43));

            _characters.SetRace(npc, CharacterRace.Elf);
            _characters.SetRace(pc, CharacterRace.Dwarf);
            CharacterReactionInputs matrix = _derived.GetReactionInputs(npc, pc);
            Assert.That(matrix.RaceModifier, Is.EqualTo(-20));
            Assert.That(matrix.Subtotal, Is.EqualTo(23));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void AloofOrMonstrousNpcSuppressesCharacterReactionModifiers(bool aloof, bool monstrous)
        {
            ArcanumObjectId pc = RegisterDevelopmentPlayer();
            ArcanumObjectId npc = RegisterSource(Identity(12), ObjectType.Npc, 1012,
                Stats(Defaults(), CharacterRace.Orc, CharacterGender.Male), reactionBase: 71,
                isAloof: aloof, isMonstrous: monstrous);
            CharacterReactionInputs inputs = _derived.GetReactionInputs(npc, pc);
            Assert.That(inputs.CharacterModifiersApply, Is.False);
            Assert.That(inputs.BeautyModifier, Is.Zero);
            Assert.That(inputs.RaceModifier, Is.Zero);
            Assert.That(inputs.Subtotal, Is.EqualTo(71));
        }

        [Test]
        public void RaceAndGenderChangesRecomputeWithoutReplacingPersistentState()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            PersistentCharacterDerivedState state = _derived.Get(id);
            _characters.SetRace(id, CharacterRace.HalfOgre);
            _characters.SetGender(id, CharacterGender.Female);
            Assert.That(_derived.Get(id), Is.SameAs(state));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.MeleeDamageBonus), Is.EqualTo(1));
            Assert.That(_derived.GetDerivedStat(id, CharacterDerivedStat.HealRate), Is.EqualTo(3));
            Assert.That(_derived.GetResistance(id, CharacterResistance.Normal), Is.EqualTo(10));
        }

        [Test]
        public void CarryWeightDelegatesToExistingInventoryCapacityAuthority()
        {
            var root = new GameObject("M4D carry delegation");
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                session.GetOrCreatePlayer(id, "maps/test/1.sec", Vector2.zero, 0x28100000u);
                Assert.That(session.DerivedStats.GetDerivedStat(id, CharacterDerivedStat.CarryWeight),
                    Is.EqualTo(session.InventoryCapacity.GetCarryCapacity(id)));
                Assert.That(session.DerivedStats.GetDerivedStat(id, CharacterDerivedStat.CarryWeight),
                    Is.EqualTo(4000));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void InventoryAndEquipmentStateRemainNeutralFutureModifierStages()
        {
            var root = new GameObject("M4D neutral equipment");
            try
            {
                const string sector = "maps/test/1.sec";
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                session.BeginSector(sector);
                ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                session.GetOrCreatePlayer(pc, sector, Vector2.zero, 0x28100000u);
                int[] before = ImplementedStats(session.DerivedStats, pc);

                var source = new ObjectInstance(ObjectType.Armor, 7001, 1L, 0x40000000u, 0, 0,
                    oid: AuthoredBytes(44), invAid: (uint)(4 << 14));
                PersistentObjectState boots = session.GetOrCreate(source, sector, source.CurrentArtId.Value,
                    false, false, inventoryArtId: source.InvAid);
                Assert.That(session.TransferItem(boots.Identity, boots.Placement,
                    ObjectPlacement.ContainedBy(pc)).Succeeded, Is.True);
                Assert.That(session.EquipItem(pc, boots.Identity, WornLocation.Boots).Succeeded, Is.True);
                Assert.That(ImplementedStats(session.DerivedStats, pc), Is.EqualTo(before));
                Assert.That(session.UnequipItem(pc, WornLocation.Boots).Succeeded, Is.True);
                Assert.That(ImplementedStats(session.DerivedStats, pc), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReloadTraversalAndPresentationDestructionKeepOneDerivedRecord()
        {
            var root = new GameObject("M4D lifecycle");
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                session.GetOrCreatePlayer(id, "maps/test/1.sec", new Vector2(4, 5), 0x28100000u);
                PersistentCharacterDerivedState state = session.DerivedStats.Get(id);
                session.DerivedStats.SetAlignment(id, 375);
                var disposable = new GameObject("Disposable PC presentation");
                disposable.AddComponent<WorldObject>().Identity = id;
                Object.DestroyImmediate(disposable);
                session.GetOrCreatePlayer(id, "maps/test/2.sec", new Vector2(0, 5), 0x28100000u);
                session.GetOrCreatePlayer(id, "maps/test/1.sec", new Vector2(63, 5), 0x28100000u);
                Assert.That(session.DerivedStats.Get(id), Is.SameAs(state));
                Assert.That(session.DerivedStats.GetAlignment(id), Is.EqualTo(375));
                Assert.That(session.DerivedStats.States.Count, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SourceReloadPreservesAlignmentAndRejectsChangedSource()
        {
            int[] stats = Stats(Defaults(), CharacterRace.Human, CharacterGender.Male, alignment: 25);
            ArcanumObjectId id = RegisterSource(Identity(13), ObjectType.Npc, 1013, stats);
            PersistentCharacterDerivedState state = _derived.Get(id);
            _derived.SetAlignment(id, 400);
            CharacterDerivedSource same = CharacterDerivedSource.Resolve(stats, null, 0, null, null, null,
                50, null, 0, 0);
            Assert.That(_derived.GetOrCreateSourceCharacter(id, ObjectType.Npc, 1013, same), Is.SameAs(state));
            Assert.That(_derived.GetAlignment(id), Is.EqualTo(400));

            int[] changed = (int[])stats.Clone();
            changed[CharacterDerivedStatRules.AlignmentSourceSlot] = 26;
            CharacterDerivedSource different = CharacterDerivedSource.Resolve(changed, null, 0, null, null,
                null, 50, null, 0, 0);
            Assert.Throws<InvalidOperationException>(() => _derived.GetOrCreateSourceCharacter(id, ObjectType.Npc,
                1013, different));
            Assert.That(_derived.GetAlignment(id), Is.EqualTo(400));
        }

        [Test]
        public void UnknownIdentityAndInvalidPairwiseRolesFailExplicitly()
        {
            Assert.Throws<KeyNotFoundException>(() => _derived.Get(Identity(99)));
            ArcanumObjectId pc = RegisterDevelopmentPlayer();
            Assert.Throws<ArgumentException>(() => _derived.GetReactionInputs(pc, pc));
            Assert.Throws<InvalidOperationException>(() => _derived.GetDerivedStat(pc,
                CharacterDerivedStat.CarryWeight));
        }

        private ArcanumObjectId RegisterDevelopmentPlayer()
        {
            ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _characters.GetOrCreateDevelopmentPlayer(id);
            _progression.GetOrCreateDevelopmentPlayer(id);
            _derived.GetOrCreateDevelopmentPlayer(id);
            return id;
        }

        private ArcanumObjectId RegisterSource(ArcanumObjectId id, ObjectType type, int prototype, int[] stats,
            int baseArmorClass = 0, int[] resistances = null, int reactionBase = 50, int[] basic = null,
            bool isAloof = false, bool isMonstrous = false)
        {
            _characters.GetOrCreateSourceCharacter(id, type, prototype, stats, null);
            int critterFlags = isMonstrous ? CharacterProgressionSource.MonstrousCritterFlags : 0;
            _progression.GetOrCreateSourceCharacter(id, type, prototype, CharacterProgressionSource.Resolve(
                stats, null, basic, null, null, null, critterFlags));
            CharacterDerivedSource source = CharacterDerivedSource.Resolve(stats, null, baseArmorClass, null,
                resistances, null, reactionBase, null,
                isAloof ? CharacterDerivedSource.AloofNpcFlag : 0, critterFlags);
            _derived.GetOrCreateSourceCharacter(id, type, prototype, source);
            return id;
        }

        private static int[] Stats(int[] primary, CharacterRace race, CharacterGender gender,
            int alignment = 0, int magickPoints = 0, int techPoints = 0)
        {
            var values = new int[CharacterAttributeSet.SourceStatArrayCount];
            Array.Copy(primary, values, CharacterAttributeSet.Count);
            values[CharacterProgressionSource.LevelSourceSlot] = 1;
            values[CharacterDerivedStatRules.AlignmentSourceSlot] = alignment;
            values[CharacterDerivedStatRules.MagickPointsSourceSlot] = magickPoints;
            values[CharacterDerivedStatRules.TechPointsSourceSlot] = techPoints;
            values[CharacterAttributeSet.GenderSourceSlot] = (int)gender;
            values[CharacterAttributeSet.RaceSourceSlot] = (int)race;
            return values;
        }

        private static int[] Primary(int strength = 8, int dexterity = 8, int constitution = 8,
            int beauty = 8, int charisma = 8)
            => new[] { strength, dexterity, constitution, beauty, 8, 8, 8, charisma };

        private static int[] Defaults() => Primary();
        private static int[] ImplementedStats(CharacterDerivedStatService service, ArcanumObjectId identity)
            => CharacterDerivedStatRules.AllStats.Where(stat => stat != CharacterDerivedStat.CarryWeight)
                .Select(stat => service.GetDerivedStat(identity, stat)).ToArray();

        private static byte[] AuthoredBytes(int value)
        {
            var bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Authored;
            Array.Copy(BitConverter.GetBytes(value), 0, bytes, 8, 4);
            return bytes;
        }

        private static ArcanumObjectId Identity(ulong value) => ArcanumObjectId.CreateSessionDynamic(value);
    }
}
