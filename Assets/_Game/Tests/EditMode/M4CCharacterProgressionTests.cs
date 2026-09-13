using System;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M4CCharacterProgression")]
    public sealed class M4CCharacterProgressionTests
    {
        private CharacterStatService _characters;
        private CharacterProgressionService _progression;

        [SetUp]
        public void SetUp()
        {
            _characters = new CharacterStatService();
            _progression = new CharacterProgressionService(_characters);
        }

        [Test]
        public void TypedSkillIdsAndGroupsMatchSource()
        {
            Assert.That(CharacterSkillRules.AllSkills.Count, Is.EqualTo(16));
            Assert.That((int)CharacterSkill.Bow, Is.Zero);
            Assert.That((int)CharacterSkill.Persuasion, Is.EqualTo(11));
            Assert.That((int)CharacterSkill.Repair, Is.EqualTo(12));
            Assert.That((int)CharacterSkill.DisarmTraps, Is.EqualTo(15));
            Assert.That(CharacterSkillRules.IsTechnical(CharacterSkill.Persuasion), Is.False);
            Assert.That(CharacterSkillRules.IsTechnical(CharacterSkill.Repair), Is.True);
            Assert.That(CharacterSkillRules.SourceGroupIndex(CharacterSkill.DisarmTraps), Is.EqualTo(3));
        }

        [TestCase(CharacterSkill.Bow, CharacterAttribute.Dexterity)]
        [TestCase(CharacterSkill.Prowling, CharacterAttribute.Perception)]
        [TestCase(CharacterSkill.Gambling, CharacterAttribute.Intelligence)]
        [TestCase(CharacterSkill.Haggle, CharacterAttribute.Willpower)]
        [TestCase(CharacterSkill.Persuasion, CharacterAttribute.Charisma)]
        [TestCase(CharacterSkill.Repair, CharacterAttribute.Intelligence)]
        [TestCase(CharacterSkill.Firearms, CharacterAttribute.Perception)]
        [TestCase(CharacterSkill.PickLocks, CharacterAttribute.Dexterity)]
        public void GoverningAttributeMatchesSource(CharacterSkill skill, CharacterAttribute expected)
            => Assert.That(CharacterSkillRules.GoverningAttribute(skill), Is.EqualTo(expected));

        [Test]
        public void RealFixtureSourceUsesWholeInstanceArrays()
        {
            int[] instanceStats = Stats(21, 162500, 0, new[] { 10, 9, 15, 10, 10, 10, 8, 10 });
            int[] prototypeStats = Stats(1, 0, 5, Defaults());
            int[] instanceBasic = { 2, 0, 2, 1, 0, 0, 0, 0, 2, 1, 1, 1 };
            int[] instanceTech = { 0, 1, 0, 0 };
            CharacterProgressionSource source = CharacterProgressionSource.Resolve(instanceStats, prototypeStats,
                instanceBasic, new int[12], instanceTech, new int[4], 0);
            ArcanumObjectId id = Identity(1);
            _characters.GetOrCreateSourceCharacter(id, ObjectType.Npc, 17101, instanceStats, prototypeStats);
            PersistentCharacterProgressionState state = _progression.GetOrCreateSourceCharacter(id, ObjectType.Npc,
                17101, source);
            Assert.That(state.Level, Is.EqualTo(21));
            Assert.That(state.Experience, Is.EqualTo(162500));
            Assert.That(state.UnspentCharacterPoints, Is.Zero);
            Assert.That(_progression.GetBaseSkillRank(id, CharacterSkill.Bow), Is.EqualTo(8));
            Assert.That(_progression.GetBaseSkillRank(id, CharacterSkill.Throwing), Is.EqualTo(4));
            Assert.That(_progression.GetBaseSkillRank(id, CharacterSkill.Firearms), Is.EqualTo(4));
            Assert.That(source.HasInstanceStatOverride && source.HasInstanceBasicSkillOverride
                        && source.HasInstanceTechnicalSkillOverride, Is.True);
        }

        [Test]
        public void DevelopmentPlayerHasExplicitSourceBaseline()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            Assert.That(_progression.GetLevel(id), Is.EqualTo(1));
            Assert.That(_progression.GetExperience(id), Is.Zero);
            Assert.That(_progression.GetUnspentCharacterPoints(id), Is.EqualTo(5));
            foreach (CharacterSkill skill in CharacterSkillRules.AllSkills)
            {
                Assert.That(_progression.GetPurchasedSkillPoints(id, skill), Is.Zero);
                Assert.That(_progression.GetTrainingLevel(id, skill), Is.EqualTo(SkillTrainingLevel.None));
            }
        }

        [Test]
        public void PackedSkillEntrySeparatesPointsAndTraining()
        {
            ArcanumObjectId id = RegisterSourcePlayer(2, 31, 385100, 41, HighAttributes(),
                PackedBasic(CharacterSkill.Dodge, 131), PackedTechnical(CharacterSkill.Repair, 66));
            Assert.That(_progression.GetPurchasedSkillPoints(id, CharacterSkill.Dodge), Is.EqualTo(3));
            Assert.That(_progression.GetBaseSkillRank(id, CharacterSkill.Dodge), Is.EqualTo(12));
            Assert.That(_progression.GetTrainingLevel(id, CharacterSkill.Dodge),
                Is.EqualTo(SkillTrainingLevel.Expert));
            Assert.That(_progression.GetPurchasedSkillPoints(id, CharacterSkill.Repair), Is.EqualTo(2));
            Assert.That(_progression.GetTrainingLevel(id, CharacterSkill.Repair),
                Is.EqualTo(SkillTrainingLevel.Apprentice));
        }

        [TestCase(1, 0)]
        [TestCase(2, 2100)]
        [TestCase(21, 162500)]
        [TestCase(50, 1300000)]
        public void ExperienceThresholdsMatchRetailData(int level, int threshold)
            => Assert.That(CharacterProgressionService.GetExperienceForLevel(level), Is.EqualTo(threshold));

        [Test]
        public void AwardBelowThresholdChangesOnlyExperience()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            ExperienceAwardResult result = _progression.AwardExperience(id, 2099);
            Assert.That(result.Level, Is.EqualTo(1));
            Assert.That(result.CharacterPointsAwarded, Is.Zero);
            Assert.That(_progression.GetExperience(id), Is.EqualTo(2099));
            Assert.That(_progression.GetUnspentCharacterPoints(id), Is.EqualTo(5));
        }

        [Test]
        public void AwardCrossesOneThresholdAndAwardsOnePoint()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            ExperienceAwardResult result = _progression.AwardExperience(id, 2100);
            Assert.That(result.PreviousLevel, Is.EqualTo(1));
            Assert.That(result.Level, Is.EqualTo(2));
            Assert.That(result.CharacterPointsAwarded, Is.EqualTo(1));
            Assert.That(_progression.GetUnspentCharacterPoints(id), Is.EqualTo(6));
        }

        [Test]
        public void AwardCrossesMultipleThresholdsAndAppliesFifthLevelBonus()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            ExperienceAwardResult result = _progression.AwardExperience(id, 11400);
            Assert.That(result.Level, Is.EqualTo(5));
            Assert.That(result.CharacterPointsAwarded, Is.EqualTo(5));
            Assert.That(_progression.GetUnspentCharacterPoints(id), Is.EqualTo(10));
        }

        [Test]
        public void MaximumLevelAndSourcePoolBoundsAreEnforced()
        {
            ArcanumObjectId id = RegisterSourcePlayer(3, 49, 1229000, 0, HighAttributes());
            ExperienceAwardResult result = _progression.AwardExperience(id, int.MaxValue);
            Assert.That(result.Level, Is.EqualTo(50));
            Assert.That(result.CharacterPointsAwarded, Is.EqualTo(2));
            Assert.That(result.Experience, Is.EqualTo(CharacterProgressionService.MaximumExperience));
            _progression.AwardExperience(id, int.MaxValue);
            Assert.That(_progression.GetLevel(id), Is.EqualTo(50));
            Assert.That(_progression.GetUnspentCharacterPoints(id), Is.EqualTo(2));
        }

        [Test]
        public void InvalidExperienceMutationsAreAtomicAndNpcAwardsAreRejected()
        {
            ArcanumObjectId pc = RegisterDevelopmentPlayer();
            Assert.Throws<ArgumentOutOfRangeException>(() => _progression.AwardExperience(pc, -1));
            Assert.That(_progression.GetExperience(pc), Is.Zero);
            Assert.That(_progression.GetLevel(pc), Is.EqualTo(1));
            ArcanumObjectId npc = RegisterSourceNpc(4, 21, 162500, 0, Defaults());
            Assert.Throws<InvalidOperationException>(() => _progression.AwardExperience(npc, 1));
            Assert.That(_progression.GetExperience(npc), Is.EqualTo(162500));
        }

        [Test]
        public void SuccessfulSkillIncreaseConsumesExactlyOnePoint()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            Assert.That(_progression.IncreaseSkill(id, CharacterSkill.Bow), Is.EqualTo(SkillIncreaseResult.Success));
            Assert.That(_progression.GetPurchasedSkillPoints(id, CharacterSkill.Bow), Is.EqualTo(1));
            Assert.That(_progression.GetBaseSkillRank(id, CharacterSkill.Bow), Is.EqualTo(4));
            Assert.That(_progression.GetEffectiveSkillRank(id, CharacterSkill.Bow), Is.EqualTo(4));
            Assert.That(_progression.GetUnspentCharacterPoints(id), Is.EqualTo(4));
        }

        [Test]
        public void GoverningAttributeFailureRollsBackRankAndPoints()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            Assert.That(_progression.IncreaseSkill(id, CharacterSkill.Bow), Is.EqualTo(SkillIncreaseResult.Success));
            Assert.That(_progression.IncreaseSkill(id, CharacterSkill.Bow),
                Is.EqualTo(SkillIncreaseResult.GoverningAttributeTooLow));
            Assert.That(_progression.GetPurchasedSkillPoints(id, CharacterSkill.Bow), Is.EqualTo(1));
            Assert.That(_progression.GetUnspentCharacterPoints(id), Is.EqualTo(4));
            _characters.SetRace(id, CharacterRace.Halfling);
            Assert.That(_progression.IncreaseSkill(id, CharacterSkill.Bow), Is.EqualTo(SkillIncreaseResult.Success));
            Assert.That(_progression.GetEffectiveSkillRank(id, CharacterSkill.Bow), Is.EqualTo(8));
        }

        [Test]
        public void InsufficientPointsAndMaximumRankDoNotMutate()
        {
            ArcanumObjectId empty = RegisterSourcePlayer(5, 1, 0, 0, HighAttributes());
            Assert.That(_progression.IncreaseSkill(empty, CharacterSkill.Melee),
                Is.EqualTo(SkillIncreaseResult.InsufficientCharacterPoints));
            ArcanumObjectId maximum = RegisterSourcePlayer(6, 1, 0, 5, HighAttributes(),
                PackedBasic(CharacterSkill.Melee, 5));
            Assert.That(_progression.IncreaseSkill(maximum, CharacterSkill.Melee),
                Is.EqualTo(SkillIncreaseResult.MaximumRank));
            Assert.That(_progression.GetUnspentCharacterPoints(maximum), Is.EqualTo(5));
        }

        [Test]
        public void TrainingRequiresSequentialTransitionsAndExactRanks()
        {
            ArcanumObjectId id = RegisterSourcePlayer(7, 1, 0, 5, HighAttributes(),
                PackedBasic(CharacterSkill.Melee, 5));
            Assert.That(_progression.SetTrainingLevel(id, CharacterSkill.Melee, SkillTrainingLevel.Master),
                Is.EqualTo(TrainingAssignmentResult.NonSequentialIncrease));
            Assert.That(_progression.SetTrainingLevel(id, CharacterSkill.Melee, SkillTrainingLevel.Apprentice),
                Is.EqualTo(TrainingAssignmentResult.Success));
            Assert.That(_progression.SetTrainingLevel(id, CharacterSkill.Melee, SkillTrainingLevel.Expert),
                Is.EqualTo(TrainingAssignmentResult.Success));
            Assert.That(_progression.SetTrainingLevel(id, CharacterSkill.Melee, SkillTrainingLevel.Master),
                Is.EqualTo(TrainingAssignmentResult.Success));
            Assert.That(_progression.GetTrainingLevel(id, CharacterSkill.Melee),
                Is.EqualTo(SkillTrainingLevel.Master));
            Assert.That(_progression.SetTrainingLevel(id, CharacterSkill.Melee, SkillTrainingLevel.None),
                Is.EqualTo(TrainingAssignmentResult.Success), "source permits direct downward assignment");
        }

        [Test]
        public void TrainingRankFailureDoesNotMutate()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            Assert.That(_progression.SetTrainingLevel(id, CharacterSkill.Bow, SkillTrainingLevel.Apprentice),
                Is.EqualTo(TrainingAssignmentResult.InsufficientSkillRank));
            Assert.That(_progression.GetTrainingLevel(id, CharacterSkill.Bow), Is.EqualTo(SkillTrainingLevel.None));
        }

        [Test]
        public void MonstrousMeleeRankAndTrainingAreDerived()
        {
            ArcanumObjectId id = RegisterSourcePlayer(8, 30, 356800, 0, HighAttributes(),
                isMonstrous: true);
            Assert.That(_progression.GetBaseSkillRank(id, CharacterSkill.Melee), Is.Zero);
            Assert.That(_progression.GetEffectiveSkillRank(id, CharacterSkill.Melee), Is.EqualTo(20));
            Assert.That(_progression.GetTrainingLevel(id, CharacterSkill.Melee), Is.EqualTo(SkillTrainingLevel.Master));
            Assert.That(_progression.SetTrainingLevel(id, CharacterSkill.Melee, SkillTrainingLevel.Apprentice),
                Is.EqualTo(TrainingAssignmentResult.DerivedMonstrousMelee));
        }

        [Test]
        public void LevelChangeUpdatesVitalityAndPreservesCurrentDamageSemantics()
        {
            ArcanumObjectId id = RegisterDevelopmentPlayer();
            var vitality = new CharacterVitalityService(_characters, _progression);
            PersistentCharacterVitalityState state = vitality.GetOrCreateDevelopmentPlayer(id);
            vitality.ApplyHitPointDamage(id, 5);
            vitality.ApplyFatigueDamage(id, 7);
            int currentHp = state.CurrentHitPoints;
            int currentFatigue = state.CurrentFatigue;
            _progression.AwardExperience(id, 2100);
            Assert.That(vitality.GetMaximumHitPoints(id), Is.EqualTo(32));
            Assert.That(vitality.GetMaximumFatigue(id), Is.EqualTo(32));
            Assert.That(vitality.GetCurrentHitPoints(id), Is.EqualTo(currentHp));
            Assert.That(vitality.GetCurrentFatigue(id), Is.EqualTo(currentFatigue));
        }

        [Test]
        public void ReloadAndTraversalKeepOneAuthoritativeProgressionRecord()
        {
            var root = new GameObject("M4C lifecycle");
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                session.GetOrCreatePlayer(id, "maps/test/1.sec", new Vector2(4, 5), 0x28100000u);
                PersistentCharacterProgressionState state = session.Progression.Get(id);
                session.Progression.AwardExperience(id, 2100);
                session.Progression.IncreaseSkill(id, CharacterSkill.Bow);
                session.GetOrCreatePlayer(id, "maps/test/2.sec", new Vector2(0, 5), 0x28100000u);
                session.GetOrCreatePlayer(id, "maps/test/1.sec", new Vector2(63, 5), 0x28100000u);
                Assert.That(session.Progression.Get(id), Is.SameAs(state));
                Assert.That(session.Progression.GetLevel(id), Is.EqualTo(2));
                Assert.That(session.Progression.GetBaseSkillRank(id, CharacterSkill.Bow), Is.EqualTo(4));
                Assert.That(session.Progression.States.Count, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private ArcanumObjectId RegisterDevelopmentPlayer()
        {
            ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _characters.GetOrCreateDevelopmentPlayer(id);
            _progression.GetOrCreateDevelopmentPlayer(id);
            return id;
        }

        private ArcanumObjectId RegisterSourcePlayer(ulong key, int level, int experience, int unspent,
            int[] primary, int[] basic = null, int[] technical = null, bool isMonstrous = false)
            => RegisterSource(key, ObjectType.Pc, level, experience, unspent, primary, basic, technical,
                isMonstrous);

        private ArcanumObjectId RegisterSourceNpc(ulong key, int level, int experience, int unspent, int[] primary)
            => RegisterSource(key, ObjectType.Npc, level, experience, unspent, primary);

        private ArcanumObjectId RegisterSource(ulong key, ObjectType type, int level, int experience, int unspent,
            int[] primary, int[] basic = null, int[] technical = null, bool isMonstrous = false)
        {
            ArcanumObjectId id = Identity(key);
            int[] stats = Stats(level, experience, unspent, primary);
            int prototype = checked((int)key + 1000);
            _characters.GetOrCreateSourceCharacter(id, type, prototype, stats, null);
            var source = new CharacterProgressionSource(level, experience, unspent,
                basic ?? new int[12], technical ?? new int[4], isMonstrous, true, basic != null,
                technical != null);
            _progression.GetOrCreateSourceCharacter(id, type, prototype, source);
            return id;
        }

        private static int[] Stats(int level, int experience, int unspent, int[] primary)
        {
            var values = new int[CharacterAttributeSet.SourceStatArrayCount];
            Array.Copy(primary, values, CharacterAttributeSet.Count);
            values[CharacterProgressionSource.LevelSourceSlot] = level;
            values[CharacterProgressionSource.ExperienceSourceSlot] = experience;
            values[CharacterProgressionSource.UnspentPointsSourceSlot] = unspent;
            values[CharacterAttributeSet.GenderSourceSlot] = (int)CharacterGender.Male;
            values[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return values;
        }

        private static int[] PackedBasic(CharacterSkill skill, int value)
        {
            var values = new int[12];
            values[CharacterSkillRules.SourceGroupIndex(skill)] = value;
            return values;
        }

        private static int[] PackedTechnical(CharacterSkill skill, int value)
        {
            var values = new int[4];
            values[CharacterSkillRules.SourceGroupIndex(skill)] = value;
            return values;
        }

        private static int[] Defaults() => new[] { 8, 8, 8, 8, 8, 8, 8, 8 };
        private static int[] HighAttributes() => new[] { 18, 18, 18, 18, 18, 18, 18, 18 };
        private static ArcanumObjectId Identity(ulong value) => ArcanumObjectId.CreateSessionDynamic(value);
    }
}
