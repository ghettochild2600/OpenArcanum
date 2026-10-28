using System;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    [Category("PoisonRuntime")]
    public sealed class CharacterPoisonRuntimeTests
    {
        private ArcanumObjectId _identity;
        private CharacterStatService _characters;
        private CharacterVitalityService _vitality;
        private CharacterDerivedStatService _derived;
        private SourceTimeService _time;
        private CharacterPoisonService _poison;

        [SetUp]
        public void SetUp()
        {
            _identity = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _characters = new CharacterStatService();
            CharacterProgressionService progression = new(_characters);
            _vitality = new CharacterVitalityService(_characters, progression);
            _derived = new CharacterDerivedStatService(_characters, progression);
            _characters.GetOrCreateDevelopmentPlayer(_identity);
            progression.GetOrCreateDevelopmentPlayer(_identity);
            _vitality.GetOrCreateDevelopmentPlayer(_identity);
            _derived.GetOrCreateDevelopmentPlayer(_identity);
            _time = new SourceTimeService();
            _poison = new CharacterPoisonService(_characters, _derived, _vitality, _time);
        }

        [TearDown]
        public void TearDown() => _poison.Dispose();

        [Test]
        public void ApplicationUsesExistingResistanceAccumulatesAndClampsAtSourceMaximum()
        {
            PoisonApplicationResult first = _poison.Apply(_identity, 125);
            PoisonApplicationResult second = _poison.Apply(_identity, 125);
            PoisonApplicationResult capped = _poison.Apply(_identity, int.MaxValue);

            Assert.That((first.RequestedAmount, first.ResistedAmount, first.AppliedAmount, first.ResultingLevel),
                Is.EqualTo((125, 25, 100, 100)), "CN 8 contributes the existing 20% poison resistance");
            Assert.That(second.ResultingLevel, Is.EqualTo(200));
            Assert.That(capped.ResultingLevel, Is.EqualTo(CharacterPoisonService.MaximumLevel));
        }

        [Test]
        public void SourceTimeDrivesDamageRecoveryAndPoisonAttributePenalty()
        {
            _poison.Apply(_identity, 250); // 20% resistance -> source poison level 200.
            Assert.That(_characters.GetEffectiveAttribute(_identity, CharacterAttribute.Strength), Is.EqualTo(6));
            Assert.That(_characters.GetEffectiveAttribute(_identity, CharacterAttribute.Dexterity), Is.EqualTo(6));

            _time.Advance(CharacterPoisonService.DamageIntervalMilliseconds - 1);
            Assert.That(_vitality.Get(_identity).HitPointDamage, Is.Zero);
            _time.Advance(1);
            Assert.That(_vitality.Get(_identity).HitPointDamage, Is.EqualTo(2));

            _time.Advance(CharacterPoisonService.RecoveryIntervalMilliseconds
                          - CharacterPoisonService.DamageIntervalMilliseconds);
            Assert.That(_vitality.GetCurrentHitPoints(_identity), Is.EqualTo(10),
                "eight damage events at 15..120 seconds run before the simultaneous recovery event");
            Assert.That(_poison.GetLevel(_identity), Is.EqualTo(192));
            Assert.That(_characters.GetEffectiveAttribute(_identity, CharacterAttribute.Strength), Is.EqualTo(7));
        }

        [Test]
        public void CureFloorsAtZeroCancelsEventsAndRemovesContributors()
        {
            _poison.Apply(_identity, 250);
            Assert.That(_poison.Cure(_identity, 500), Is.EqualTo(200));
            Assert.That(_poison.GetLevel(_identity), Is.Zero);
            Assert.That(_characters.GetEffectiveAttribute(_identity, CharacterAttribute.Strength), Is.EqualTo(8));
            int damageAfterCure = _vitality.Get(_identity).HitPointDamage;
            _time.Advance(CharacterPoisonService.RecoveryIntervalMilliseconds * 2);
            Assert.That(_vitality.Get(_identity).HitPointDamage, Is.EqualTo(damageAfterCure));
        }

        [Test]
        public void ExtraordinaryConstitutionForcesPoisonToZero()
        {
            _characters.SetBaseAttribute(_identity, CharacterAttribute.Constitution, 20);
            PoisonApplicationResult result = _poison.Apply(_identity, 500);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.AppliedAmount, Is.Zero);
            Assert.That(_poison.GetLevel(_identity), Is.Zero);
        }

        [Test]
        public void DeadAndInvalidTargetsRejectApplicationAndDeadTargetsTakeNoFurtherTicks()
        {
            Assert.That(_poison.Apply(ArcanumObjectId.CreateSessionDynamic(99), 10).Failure,
                Is.EqualTo(PoisonApplicationFailure.InvalidTarget));
            _poison.Apply(_identity, 125);
            _vitality.ApplyHitPointDamage(_identity, _vitality.GetCurrentHitPoints(_identity));
            int damage = _vitality.Get(_identity).HitPointDamage;
            Assert.That(_poison.Apply(_identity, 10).Failure, Is.EqualTo(PoisonApplicationFailure.DeadTarget));
            _time.Advance(CharacterPoisonService.DamageIntervalMilliseconds);
            Assert.That(_vitality.Get(_identity).HitPointDamage, Is.EqualTo(damage));
            Assert.That(_poison.GetLevel(_identity), Is.EqualTo(100));
        }

        [Test]
        public void ExportRestorePreservesBothEventPhases()
        {
            _poison.Apply(_identity, 250);
            _time.Advance(14_999);
            PoisonSaveData data = _poison.ExportSaveData();
            _poison.Dispose();
            _poison = new CharacterPoisonService(_characters, _derived, _vitality, _time);
            _poison.RestoreSaveData(data);
            _poison.FinalizeRestore();

            _time.Advance(1);
            Assert.That(_vitality.Get(_identity).HitPointDamage, Is.EqualTo(2));
            Assert.That(_poison.GetLevel(_identity), Is.EqualTo(200));
            Assert.That(data.Characters[0].NextRecoveryAtMilliseconds, Is.EqualTo(120_000));
        }
    }
}
