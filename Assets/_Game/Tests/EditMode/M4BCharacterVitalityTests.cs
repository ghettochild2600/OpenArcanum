using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M4BCharacterVitality")]
    public sealed class M4BCharacterVitalityTests
    {
        private CharacterStatService _characters;
        private CharacterVitalityService _vitality;

        [SetUp]
        public void SetUp()
        {
            _characters = new CharacterStatService();
            _vitality = new CharacterVitalityService(_characters);
        }

        [Test]
        public void DevelopmentPlayerHasExplicitThirtyThirtyBaseline()
        {
            ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _characters.GetOrCreateDevelopmentPlayer(id);
            PersistentCharacterVitalityState state = _vitality.GetOrCreateDevelopmentPlayer(id);
            Assert.That(state.Source.Level, Is.EqualTo(1));
            Assert.That(state.MaximumHitPoints, Is.EqualTo(30));
            Assert.That(state.CurrentHitPoints, Is.EqualTo(30));
            Assert.That(state.MaximumFatigue, Is.EqualTo(30));
            Assert.That(state.CurrentFatigue, Is.EqualTo(30));
        }

        [Test]
        public void SourceFormulaUsesEffectiveStrengthConstitutionWillpowerAndLevel()
        {
            ArcanumObjectId id = Identity(1);
            int[] stats = Source(21, new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, CharacterGender.Female);
            _characters.GetOrCreateSourceCharacter(id, ObjectType.Npc, 17101, stats, null);
            var source = new CharacterVitalitySource(21, 0, 4, 0, 0, 0, 4);
            PersistentCharacterVitalityState state = _vitality.GetOrCreateSourceCharacter(id, ObjectType.Npc,
                17101, source);
            Assert.That(state.MaximumHitPoints, Is.EqualTo(76));
            Assert.That(state.CurrentHitPoints, Is.EqualTo(76));
            Assert.That(state.MaximumFatigue, Is.EqualTo(86));
            Assert.That(state.CurrentFatigue, Is.EqualTo(82));
        }

        [Test]
        public void ScalarInputsInheritIndependentlyAndZeroIsAnOverride()
        {
            int[] instance = Source(4, Defaults(), CharacterGender.Male);
            int[] prototype = Source(20, Defaults(), CharacterGender.Male);
            CharacterVitalitySource source = CharacterVitalitySource.Resolve(instance, prototype,
                0, 9, null, -3, null, 6, null, 7, 0, 8, null, 10);
            Assert.That(source.Level, Is.EqualTo(4), "the stat array is inherited as a whole");
            Assert.That(source.HitPointPoints, Is.Zero);
            Assert.That(source.HitPointAdjustment, Is.EqualTo(-3));
            Assert.That(source.HitPointDamage, Is.EqualTo(6));
            Assert.That(source.FatiguePoints, Is.EqualTo(7));
            Assert.That(source.FatigueAdjustment, Is.Zero);
            Assert.That(source.FatigueDamage, Is.EqualTo(10));
        }

        [TestCase(-4, 0)]
        [TestCase(99, 51)]
        public void ResolvedSourceLevelUsesEngineBounds(int raw, int expected)
        {
            int[] stats = Source(raw, Defaults(), CharacterGender.Male);
            CharacterVitalitySource source = CharacterVitalitySource.Resolve(stats, null,
                null, null, null, null, null, null, null, null, null, null, null, null);
            Assert.That(source.Level, Is.EqualTo(expected));
        }

        [Test]
        public void ResolvedSourceNormalizesNegativePointsAndDamageLikeFieldSetters()
        {
            CharacterVitalitySource source = CharacterVitalitySource.Resolve(Source(1, Defaults(), CharacterGender.Male),
                null, -1, null, -7, null, -2, null, -3, null, -8, null, -4, null);
            Assert.That(source.HitPointPoints, Is.Zero);
            Assert.That(source.HitPointAdjustment, Is.EqualTo(-7));
            Assert.That(source.HitPointDamage, Is.Zero);
            Assert.That(source.FatiguePoints, Is.Zero);
            Assert.That(source.FatigueAdjustment, Is.EqualTo(-8));
            Assert.That(source.FatigueDamage, Is.Zero);
        }

        [Test]
        public void ExplicitSourceRejectsOutOfRangeLevelPointsAndDamage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterVitalitySource(52, 0, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterVitalitySource(1, -1, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterVitalitySource(1, 0, 0, -1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterVitalitySource(1, 0, 0, 0, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterVitalitySource(1, 0, 0, 0, 0, 0, -1));
        }

        [Test]
        public void VitalityCannotExistWithoutMatchingCharacterAuthority()
        {
            Assert.Throws<KeyNotFoundException>(() => _vitality.GetOrCreateDevelopmentPlayer(Identity(2)));
            ArcanumObjectId id = Identity(3);
            _characters.GetOrCreateSourceCharacter(id, ObjectType.Npc, 5,
                Source(1, Defaults(), CharacterGender.Male), null);
            Assert.Throws<InvalidOperationException>(() => _vitality.GetOrCreateSourceCharacter(id, ObjectType.Pc,
                5, CharacterVitalitySource.DevelopmentPlayer));
        }

        [Test]
        public void SourceReloadReturnsSameMutatedState()
        {
            ArcanumObjectId id = RegisterPlayer();
            PersistentCharacterVitalityState first = _vitality.GetOrCreateDevelopmentPlayer(id);
            _vitality.ApplyHitPointDamage(id, 7);
            PersistentCharacterVitalityState reloaded = _vitality.GetOrCreateDevelopmentPlayer(id);
            Assert.That(reloaded, Is.SameAs(first));
            Assert.That(reloaded.HitPointDamage, Is.EqualTo(7));
        }

        [Test]
        public void ChangedSourceForSameIdentityIsRejected()
        {
            ArcanumObjectId id = RegisterNpc(4, 1);
            _vitality.GetOrCreateSourceCharacter(id, ObjectType.Npc, 1,
                CharacterVitalitySource.DevelopmentPlayer);
            Assert.Throws<InvalidOperationException>(() => _vitality.GetOrCreateSourceCharacter(id, ObjectType.Npc,
                1, new CharacterVitalitySource(1, 1, 0, 0, 0, 0, 0)));
        }

        [Test]
        public void HitPointDamageCanReachAndExceedMaximumWithoutAddingDeathPolicy()
        {
            ArcanumObjectId id = RegisterPlayer();
            _vitality.GetOrCreateDevelopmentPlayer(id);
            _vitality.ApplyHitPointDamage(id, 35);
            Assert.That(_vitality.Get(id).HitPointDamage, Is.EqualTo(35));
            Assert.That(_vitality.GetCurrentHitPoints(id), Is.EqualTo(-5));
        }

        [Test]
        public void HitPointRestoreFloorsDamageAtZero()
        {
            ArcanumObjectId id = RegisterPlayer();
            _vitality.GetOrCreateDevelopmentPlayer(id);
            _vitality.ApplyHitPointDamage(id, 9);
            _vitality.RestoreHitPoints(id, 4);
            Assert.That(_vitality.GetCurrentHitPoints(id), Is.EqualTo(25));
            _vitality.RestoreHitPoints(id, 100);
            Assert.That(_vitality.Get(id).HitPointDamage, Is.Zero);
            Assert.That(_vitality.GetCurrentHitPoints(id), Is.EqualTo(30));
        }

        [Test]
        public void FatigueDamageAndRestoreAreIndependentFromHitPoints()
        {
            ArcanumObjectId id = RegisterPlayer();
            _vitality.GetOrCreateDevelopmentPlayer(id);
            _vitality.ApplyHitPointDamage(id, 3);
            _vitality.ApplyFatigueDamage(id, 11);
            _vitality.RestoreFatigue(id, 5);
            Assert.That(_vitality.Get(id).HitPointDamage, Is.EqualTo(3));
            Assert.That(_vitality.Get(id).FatigueDamage, Is.EqualTo(6));
            Assert.That(_vitality.GetCurrentFatigue(id), Is.EqualTo(24));
        }

        [Test]
        public void NegativeMutationAmountsAreRejectedWithoutChangingState()
        {
            ArcanumObjectId id = RegisterPlayer();
            PersistentCharacterVitalityState state = _vitality.GetOrCreateDevelopmentPlayer(id);
            Assert.Throws<ArgumentOutOfRangeException>(() => _vitality.ApplyHitPointDamage(id, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _vitality.RestoreHitPoints(id, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _vitality.ApplyFatigueDamage(id, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _vitality.RestoreFatigue(id, -1));
            Assert.That(state.HitPointDamage, Is.Zero);
            Assert.That(state.FatigueDamage, Is.Zero);
        }

        [Test]
        public void DamageOverflowIsRejectedAtomically()
        {
            ArcanumObjectId id = RegisterNpc(5, 1);
            var source = new CharacterVitalitySource(1, 0, 0, int.MaxValue, 0, 0, int.MaxValue);
            PersistentCharacterVitalityState state = _vitality.GetOrCreateSourceCharacter(id, ObjectType.Npc, 1,
                source);
            Assert.Throws<OverflowException>(() => _vitality.ApplyHitPointDamage(id, 1));
            Assert.Throws<OverflowException>(() => _vitality.ApplyFatigueDamage(id, 1));
            Assert.That(state.HitPointDamage, Is.EqualTo(int.MaxValue));
            Assert.That(state.FatigueDamage, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void RaceMaximumIncreasePreservesBothCurrentValues()
        {
            ArcanumObjectId id = RegisterPlayer();
            _vitality.GetOrCreateDevelopmentPlayer(id);
            _vitality.ApplyHitPointDamage(id, 5);
            _vitality.ApplyFatigueDamage(id, 5);
            _characters.SetRace(id, CharacterRace.HalfOgre);
            Assert.That(_vitality.GetMaximumHitPoints(id), Is.EqualTo(38));
            Assert.That(_vitality.GetCurrentHitPoints(id), Is.EqualTo(25));
            Assert.That(_vitality.GetCurrentFatigue(id), Is.EqualTo(25));
        }

        [Test]
        public void GenderMaximumChangesPreserveCurrentOrCapAtNewMaximum()
        {
            ArcanumObjectId damaged = RegisterNpc(6, 1);
            _vitality.GetOrCreateSourceCharacter(damaged, ObjectType.Npc, 1,
                CharacterVitalitySource.DevelopmentPlayer);
            _vitality.ApplyHitPointDamage(damaged, 5);
            _vitality.ApplyFatigueDamage(damaged, 5);
            _characters.SetGender(damaged, CharacterGender.Female);
            Assert.That(_vitality.GetMaximumHitPoints(damaged), Is.EqualTo(28));
            Assert.That(_vitality.GetCurrentHitPoints(damaged), Is.EqualTo(25));
            Assert.That(_vitality.GetMaximumFatigue(damaged), Is.EqualTo(32));
            Assert.That(_vitality.GetCurrentFatigue(damaged), Is.EqualTo(25));

            ArcanumObjectId full = RegisterNpc(7, 1);
            _vitality.GetOrCreateSourceCharacter(full, ObjectType.Npc, 1,
                CharacterVitalitySource.DevelopmentPlayer);
            _characters.SetGender(full, CharacterGender.Female);
            Assert.That(_vitality.GetCurrentHitPoints(full), Is.EqualTo(28), "current caps when the new max is lower");
            Assert.That(_vitality.GetCurrentFatigue(full), Is.EqualTo(30));
        }

        [Test]
        public void MultipleCharactersKeepIndependentVitality()
        {
            ArcanumObjectId first = RegisterNpc(8, 1);
            ArcanumObjectId second = RegisterNpc(9, 2);
            _vitality.GetOrCreateSourceCharacter(first, ObjectType.Npc, 1, CharacterVitalitySource.DevelopmentPlayer);
            _vitality.GetOrCreateSourceCharacter(second, ObjectType.Npc, 2, CharacterVitalitySource.DevelopmentPlayer);
            _vitality.ApplyHitPointDamage(first, 12);
            Assert.That(_vitality.GetCurrentHitPoints(first), Is.EqualTo(18));
            Assert.That(_vitality.GetCurrentHitPoints(second), Is.EqualTo(30));
            Assert.That(_vitality.States.Count, Is.EqualTo(2));
        }

        [Test]
        public void CoordinatorPreservesPlayerVitalityAcrossTraversalAndPresentationDestruction()
        {
            var root = new GameObject("M4B lifecycle");
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                session.GetOrCreatePlayer(id, "maps/test/1.sec", new Vector2(4, 5), 0x28100000u);
                PersistentCharacterVitalityState vitality = session.Vitality.Get(id);
                session.Vitality.ApplyHitPointDamage(id, 6);
                session.Vitality.ApplyFatigueDamage(id, 8);
                var disposable = new GameObject("Disposable PC presentation");
                disposable.AddComponent<WorldObject>().Identity = id;
                Object.DestroyImmediate(disposable);

                session.GetOrCreatePlayer(id, "maps/test/2.sec", new Vector2(0, 5), 0x28100000u);
                session.GetOrCreatePlayer(id, "maps/test/1.sec", new Vector2(63, 5), 0x28100000u);
                Assert.That(session.Vitality.Get(id), Is.SameAs(vitality));
                Assert.That(session.Vitality.GetCurrentHitPoints(id), Is.EqualTo(24));
                Assert.That(session.Vitality.GetCurrentFatigue(id), Is.EqualTo(22));
                Assert.That(session.Vitality.States.Count, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnknownIdentityQueriesFailExplicitly()
        {
            Assert.Throws<KeyNotFoundException>(() => _vitality.Get(Identity(99)));
            Assert.That(_vitality.TryGet(Identity(99), out _), Is.False);
        }

        private ArcanumObjectId RegisterPlayer()
        {
            ArcanumObjectId id = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _characters.GetOrCreateDevelopmentPlayer(id);
            return id;
        }

        private ArcanumObjectId RegisterNpc(ulong identity, int prototype)
        {
            ArcanumObjectId id = Identity(identity);
            _characters.GetOrCreateSourceCharacter(id, ObjectType.Npc, prototype,
                Source(1, Defaults(), CharacterGender.Male), null);
            return id;
        }

        private static int[] Defaults() => new[] { 8, 8, 8, 8, 8, 8, 8, 8 };

        private static int[] Source(int level, int[] primary, CharacterGender gender)
        {
            var values = new int[CharacterAttributeSet.SourceStatArrayCount];
            Array.Copy(primary, values, CharacterAttributeSet.Count);
            values[CharacterVitalitySource.LevelSourceSlot] = level;
            values[CharacterAttributeSet.GenderSourceSlot] = (int)gender;
            values[CharacterAttributeSet.RaceSourceSlot] = (int)CharacterRace.Human;
            return values;
        }

        private static ArcanumObjectId Identity(ulong value) => ArcanumObjectId.CreateSessionDynamic(value);
    }
}
