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
    [Category("M4ACharacterAttributes")]
    public sealed class M4ACharacterAttributeTests
    {
        private static readonly ArcanumObjectId FixtureIdentity = ArcanumObjectId.CreateGuid(
            Guid.Parse("065ece33-acf4-4b3a-b98e-9ab36c467e6f"));
        private CharacterStatService _service;

        [SetUp] public void SetUp() => _service = new CharacterStatService();

        [Test]
        public void EightTypedAttributesPreserveExactSourceIds()
        {
            Assert.That(CharacterStatService.AllAttributes.Select(value => (int)value),
                Is.EqualTo(Enumerable.Range(0, 8)));
            Assert.That(CharacterStatService.AllAttributes, Is.EqualTo(new[]
            {
                CharacterAttribute.Strength, CharacterAttribute.Dexterity, CharacterAttribute.Constitution,
                CharacterAttribute.Beauty, CharacterAttribute.Intelligence, CharacterAttribute.Perception,
                CharacterAttribute.Willpower, CharacterAttribute.Charisma,
            }));
        }

        [Test]
        public void RealNpcInstanceFixtureMatchesSourceBaseAndFemaleEffectiveValues()
        {
            int[] prototype = Source(new[] { 8, 8, 8, 8, 8, 8, 8, 8 }, CharacterRace.Human,
                CharacterGender.Female);
            int[] instance = Source(new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, CharacterRace.Human,
                CharacterGender.Female);

            PersistentCharacterState state = _service.GetOrCreateSourceCharacter(FixtureIdentity, ObjectType.Npc,
                17101, instance, prototype);

            Assert.That(FixtureIdentity.Key, Is.EqualTo("G_33CE5E06_F4AC_3A4B_B98E_9AB36C467E6F"));
            Assert.That(state.HasInstanceStatOverride, Is.True);
            Assert.That(state.Race, Is.EqualTo(CharacterRace.Human));
            Assert.That(state.Gender, Is.EqualTo(CharacterGender.Female));
            AssertValues(FixtureIdentity, new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, effective: false);
            AssertValues(FixtureIdentity, new[] { 9, 9, 16, 10, 10, 10, 8, 10 }, effective: true);
        }

        [Test]
        public void PrototypeFallbackIsUsedOnlyWhenWholeInstanceArrayIsAbsent()
        {
            ArcanumObjectId inherited = Identity(1);
            int[] prototype = Source(new[] { 5, 7, 6, 4, 1, 4, 3, 2 }, CharacterRace.Human,
                CharacterGender.Male);
            PersistentCharacterState fallback = _service.GetOrCreateSourceCharacter(inherited, ObjectType.Npc,
                28353, null, prototype);
            Assert.That(fallback.HasInstanceStatOverride, Is.False);
            AssertValues(inherited, new[] { 5, 7, 6, 4, 1, 4, 3, 2 }, false);

            ArcanumObjectId overridden = Identity(2);
            int[] instance = Source(new[] { 14, 17, 8, 8, 8, 13, 8, 8 }, CharacterRace.Human,
                CharacterGender.Male);
            _service.GetOrCreateSourceCharacter(overridden, ObjectType.Npc, 17098, instance, prototype);
            AssertValues(overridden, new[] { 14, 17, 8, 8, 8, 13, 8, 8 }, false);
        }

        [Test]
        public void DevelopmentPlayerBaselineIsExplicitHumanMaleSourceDefault()
        {
            ArcanumObjectId identity = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            PersistentCharacterState state = _service.GetOrCreateDevelopmentPlayer(identity);
            Assert.That(state.ObjectType, Is.EqualTo(ObjectType.Pc));
            Assert.That(state.PrototypeNumber, Is.Null);
            Assert.That(state.Race, Is.EqualTo(CharacterRace.Human));
            Assert.That(state.Gender, Is.EqualTo(CharacterGender.Male));
            AssertValues(identity, Enumerable.Repeat(8, 8).ToArray(), false);
            AssertValues(identity, Enumerable.Repeat(8, 8).ToArray(), true);
        }

        [Test]
        public void NonCritterAndMissingSourceAreRejectedExplicitly()
        {
            Assert.Throws<ArgumentException>(() => _service.GetOrCreateSourceCharacter(Identity(3), ObjectType.Food,
                10078, null, SourceDefaults()));
            Assert.Throws<InvalidOperationException>(() => _service.GetOrCreateSourceCharacter(Identity(4),
                ObjectType.Npc, 1, null, null));
            Assert.Throws<ArgumentException>(() => _service.GetOrCreateSourceCharacter(default, ObjectType.Npc, 1,
                null, SourceDefaults()));
        }

        [Test]
        public void UnknownIdentityAttributeRaceAndGenderFailExplicitly()
        {
            ArcanumObjectId identity = Identity(5);
            _service.GetOrCreateDevelopmentPlayer(identity);
            Assert.Throws<KeyNotFoundException>(() => _service.GetBaseAttribute(Identity(99),
                CharacterAttribute.Strength));
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.GetBaseAttribute(identity,
                (CharacterAttribute)8));
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.SetRace(identity, (CharacterRace)11));
            Assert.Throws<ArgumentOutOfRangeException>(() => _service.SetGender(identity, (CharacterGender)2));
        }

        [Test]
        public void SourceBaseRangesHonorRaceSpecificMaximums()
        {
            Assert.DoesNotThrow(() => new CharacterAttributeSet(
                new[] { 24, 20, 20, 20, 20, 20, 20, 20 }, CharacterRace.HalfOgre));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterAttributeSet(
                new[] { 25, 20, 20, 20, 20, 20, 20, 20 }, CharacterRace.HalfOgre));
            Assert.DoesNotThrow(() => new CharacterAttributeSet(
                new[] { 20, 22, 20, 20, 20, 20, 20, 20 }, CharacterRace.Halfling));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterAttributeSet(
                new[] { 0, 8, 8, 8, 8, 8, 8, 8 }, CharacterRace.Human));
        }

        [Test]
        public void FemaleEffectChangesEffectiveOnlyAndMaleReplacementRemovesIt()
        {
            ArcanumObjectId identity = Identity(6);
            _service.GetOrCreateDevelopmentPlayer(identity);
            _service.SetGender(identity, CharacterGender.Female);
            Assert.That(_service.GetBaseAttribute(identity, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_service.GetEffectiveAttribute(identity, CharacterAttribute.Strength), Is.EqualTo(7));
            Assert.That(_service.GetEffectiveAttribute(identity, CharacterAttribute.Constitution), Is.EqualTo(9));

            _service.SetGender(identity, CharacterGender.Male);
            Assert.That(_service.GetBaseAttribute(identity, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_service.GetEffectiveAttribute(identity, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_service.GetEffectiveAttribute(identity, CharacterAttribute.Constitution), Is.EqualTo(8));
        }

        [Test]
        public void RetailRaceEffectsAreAppliedWithoutChangingBase()
        {
            ArcanumObjectId identity = Identity(7);
            _service.GetOrCreateDevelopmentPlayer(identity);
            _service.SetRace(identity, CharacterRace.HalfOgre);
            Assert.That(_service.GetBaseAttribute(identity, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_service.GetEffectiveAttribute(identity, CharacterAttribute.Strength), Is.EqualTo(12));
            Assert.That(_service.GetEffectiveAttribute(identity, CharacterAttribute.Beauty), Is.EqualTo(7));
            Assert.That(_service.GetEffectiveAttribute(identity, CharacterAttribute.Intelligence), Is.EqualTo(4));

            _service.SetRace(identity, CharacterRace.Human);
            AssertValues(identity, Enumerable.Repeat(8, 8).ToArray(), true);
        }

        [Test]
        public void EffectiveResultUsesSourceFinalClamp()
        {
            ArcanumObjectId low = Identity(8);
            _service.GetOrCreateSourceCharacter(low, ObjectType.Npc, 1,
                Source(new[] { 1, 8, 8, 1, 1, 8, 8, 8 }, CharacterRace.Ogre, CharacterGender.Female), null);
            Assert.That(_service.GetEffectiveAttribute(low, CharacterAttribute.Strength), Is.EqualTo(6));
            Assert.That(_service.GetEffectiveAttribute(low, CharacterAttribute.Beauty), Is.EqualTo(1));
            Assert.That(_service.GetEffectiveAttribute(low, CharacterAttribute.Intelligence), Is.EqualTo(1));

            ArcanumObjectId high = Identity(9);
            _service.GetOrCreateSourceCharacter(high, ObjectType.Npc, 2,
                Source(new[] { 26, 8, 8, 8, 8, 8, 8, 8 }, CharacterRace.Ogre, CharacterGender.Male), null);
            Assert.That(_service.GetEffectiveAttribute(high, CharacterAttribute.Strength), Is.EqualTo(26));
        }

        [Test]
        public void SourceReloadReturnsSameStateAndRejectsChangedSource()
        {
            int[] source = Source(new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, CharacterRace.Human,
                CharacterGender.Female);
            PersistentCharacterState first = _service.GetOrCreateSourceCharacter(FixtureIdentity, ObjectType.Npc,
                17101, source, SourceDefaults(CharacterGender.Female));
            _service.SetGender(FixtureIdentity, CharacterGender.Male);
            PersistentCharacterState reloaded = _service.GetOrCreateSourceCharacter(FixtureIdentity, ObjectType.Npc,
                17101, source, SourceDefaults(CharacterGender.Female));
            Assert.That(reloaded, Is.SameAs(first));
            Assert.That(reloaded.Gender, Is.EqualTo(CharacterGender.Male), "session mutation survives source reload");

            int[] changed = (int[])source.Clone();
            changed[0] = 11;
            Assert.Throws<InvalidOperationException>(() => _service.GetOrCreateSourceCharacter(FixtureIdentity,
                ObjectType.Npc, 17101, changed, SourceDefaults(CharacterGender.Female)));
        }

        [Test]
        public void CoordinatorOwnsPlayerCharacterAcrossAToBToAAndPresentationDestruction()
        {
            var root = new GameObject("M4A lifecycle");
            try
            {
                WorldMapSessionCoordinator session = root.AddComponent<WorldMapSessionCoordinator>();
                ArcanumObjectId identity = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                session.GetOrCreatePlayer(identity, "maps/test/1.sec", new Vector2(4, 5), 0x28100000u);
                PersistentCharacterState attributes = session.Characters.Get(identity);
                var disposable = new GameObject("Disposable PC presentation");
                disposable.AddComponent<WorldObject>().Identity = identity;
                Object.DestroyImmediate(disposable);

                session.GetOrCreatePlayer(identity, "maps/test/2.sec", new Vector2(0, 5), 0x28100000u);
                session.GetOrCreatePlayer(identity, "maps/test/1.sec", new Vector2(63, 5), 0x28100000u);
                Assert.That(session.Characters.Get(identity), Is.SameAs(attributes));
                Assert.That(session.Characters.GetEffectiveAttribute(identity, CharacterAttribute.Strength),
                    Is.EqualTo(8));

                ArcanumObjectId otherIdentity = ArcanumObjectId.CreateGuid(
                    Guid.Parse("11111111-2222-3333-4444-555555555555"));
                Assert.Throws<InvalidOperationException>(() => session.GetOrCreatePlayer(otherIdentity,
                    "maps/test/1.sec", Vector2.zero, 0x28100000u));
                Assert.That(session.Characters.States.Count, Is.EqualTo(1),
                    "a rejected second production identity must not leave orphan character state");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void NpcCharacterSurvivesPresentationRebuildAndReloadByStableObjectId()
        {
            int[] source = Source(new[] { 10, 9, 15, 10, 10, 10, 8, 10 }, CharacterRace.Human,
                CharacterGender.Female);
            PersistentCharacterState state = _service.GetOrCreateSourceCharacter(FixtureIdentity, ObjectType.Npc,
                17101, source, SourceDefaults(CharacterGender.Female));
            var disposable = new GameObject("Disposable NPC presentation");
            disposable.AddComponent<WorldObject>().Identity = FixtureIdentity;
            Object.DestroyImmediate(disposable);

            PersistentCharacterState reloaded = _service.GetOrCreateSourceCharacter(FixtureIdentity, ObjectType.Npc,
                17101, source, SourceDefaults(CharacterGender.Female));
            Assert.That(reloaded, Is.SameAs(state));
            Assert.That(_service.States.Count, Is.EqualTo(1));
            AssertValues(FixtureIdentity, new[] { 9, 9, 16, 10, 10, 10, 8, 10 }, true);
        }

        private void AssertValues(ArcanumObjectId identity, int[] expected, bool effective)
        {
            int[] actual = CharacterStatService.AllAttributes.Select(attribute => effective
                ? _service.GetEffectiveAttribute(identity, attribute)
                : _service.GetBaseAttribute(identity, attribute)).ToArray();
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static int[] SourceDefaults(CharacterGender gender = CharacterGender.Male)
            => Source(Enumerable.Repeat(8, 8).ToArray(), CharacterRace.Human, gender);

        private static int[] Source(int[] primary, CharacterRace race, CharacterGender gender)
        {
            var values = new int[CharacterAttributeSet.SourceStatArrayCount];
            Array.Copy(primary, values, CharacterAttributeSet.Count);
            values[CharacterAttributeSet.GenderSourceSlot] = (int)gender;
            values[CharacterAttributeSet.RaceSourceSlot] = (int)race;
            return values;
        }

        private static ArcanumObjectId Identity(ulong value) => ArcanumObjectId.CreateSessionDynamic(value);
    }
}
