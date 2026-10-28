using System;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    [Category("CharacterContributors")]
    public sealed class CharacterContributorFrameworkTests
    {
        [Test]
        public void SourceOperatorsUsePercentMultiplyDivideAddMinMaxOrder()
        {
            var service = new CharacterContributorService();
            ArcanumObjectId id = Identity(1);
            CharacterContributorTarget target = CharacterContributorTarget.Attribute(CharacterAttribute.Strength);
            service.SetOwnerContributors(id, "mixed", new[]
            {
                Value("mixed", CharacterContributorDomain.Spell, "max", target,
                    CharacterContributorOperation.Maximum, 18, 6),
                Value("mixed", CharacterContributorDomain.Spell, "add", target,
                    CharacterContributorOperation.Add, 5, 4),
                Value("mixed", CharacterContributorDomain.Spell, "percent", target,
                    CharacterContributorOperation.Percent, 50, 1),
                Value("mixed", CharacterContributorDomain.Spell, "minimum", target,
                    CharacterContributorOperation.Minimum, 20, 5),
                Value("mixed", CharacterContributorDomain.Spell, "divide", target,
                    CharacterContributorOperation.Divide, 3, 3),
                Value("mixed", CharacterContributorDomain.Spell, "multiply", target,
                    CharacterContributorOperation.Multiply, 2, 2),
            });

            Assert.That(service.Evaluate(id, target, 10), Is.EqualTo(18));
            Assert.That(service.GetOrderedContributors(id).Select(value => value.SourceOrder),
                Is.EqualTo(Enumerable.Range(1, 6)));
        }

        [Test]
        public void ReapplyingOwnerReplacesWithoutStackingAndOwnerRemovalIsExact()
        {
            var service = new CharacterContributorService();
            ArcanumObjectId id = Identity(2);
            CharacterContributorTarget target = CharacterContributorTarget.Attribute(CharacterAttribute.Beauty);
            service.SetOwnerContributors(id, "background", new[] { Add("background", "first", target, -1) });
            service.SetOwnerContributors(id, "background", new[] { Add("background", "first", target, -1) });
            Assert.That(service.Evaluate(id, target, 8), Is.EqualTo(7));

            service.SetOwnerContributors(id, "spell", new[]
            {
                Add("spell", "spell", target, 4, CharacterContributorDomain.Spell),
            });
            Assert.That(service.Evaluate(id, target, 8), Is.EqualTo(11));
            Assert.That(service.RemoveOwner(id, "background"), Is.True);
            Assert.That(service.Evaluate(id, target, 8), Is.EqualTo(12));
            Assert.That(service.RemoveOwner(id, "background"), Is.False);
        }

        [Test]
        public void ReplacingBackgroundDomainRemovesPreviousEffectIdentity()
        {
            var service = new CharacterContributorService();
            ArcanumObjectId id = Identity(3);
            CharacterContributorTarget beauty = CharacterContributorTarget.Attribute(CharacterAttribute.Beauty);
            CharacterContributorTarget speed = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.Speed);
            service.SetOwnerContributors(id, "old-background", new[]
            {
                Add("old-background", "beauty", beauty, -1),
            });
            service.ReplaceDomainContributors(id, CharacterContributorDomain.Background, "new-background", new[]
            {
                Add("new-background", "speed", speed, 1),
            });

            Assert.That(service.Evaluate(id, beauty, 8), Is.EqualTo(8));
            Assert.That(service.Evaluate(id, speed, 8), Is.EqualTo(9));
            Assert.That(service.GetOrderedContributors(id).Select(value => value.Owner),
                Is.EqualTo(new[] { "new-background" }));
        }

        [Test]
        public void RaceSexAndBackgroundContributorsComposeBeforeDerivedFormulasAndClamps()
        {
            ArcanumObjectId id = Identity(4);
            var characters = new CharacterStatService();
            characters.GetOrCreateCreatedPlayer(id,
                new CharacterAttributeSet(Enumerable.Repeat(8, CharacterAttributeSet.Count).ToArray(),
                    CharacterRace.Elf), CharacterRace.Elf, CharacterGender.Female);
            var progression = new CharacterProgressionService(characters);
            progression.GetOrCreateCreatedPlayer(id, CharacterProgressionSource.DevelopmentPlayer);
            var derived = new CharacterDerivedStatService(characters, progression);
            derived.GetOrCreateCreatedPlayer(id, CharacterDerivedSource.DevelopmentPlayer);
            characters.Contributors.ReplaceDomainContributors(id, CharacterContributorDomain.Background,
                "creation-background", new[]
                {
                    Add("creation-background", "strength", CharacterContributorTarget.Attribute(
                        CharacterAttribute.Strength), 2),
                    Add("creation-background", "speed", CharacterContributorTarget.DerivedStat(
                        CharacterDerivedStat.Speed), 1),
                    Add("creation-background", "poison", CharacterContributorTarget.Resistance(
                        CharacterResistance.Poison), 5),
                    Add("creation-background", "magick", CharacterContributorTarget.MagickPoints, 2),
                });

            Assert.That(characters.GetEffectiveAttribute(id, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(characters.GetEffectiveAttribute(id, CharacterAttribute.Constitution), Is.EqualTo(7));
            Assert.That(derived.GetDerivedStat(id, CharacterDerivedStat.MeleeDamageBonus), Is.EqualTo(-1));
            Assert.That(derived.GetDerivedStat(id, CharacterDerivedStat.Speed), Is.EqualTo(10));
            Assert.That(derived.GetResistance(id, CharacterResistance.Poison), Is.EqualTo(20));
            Assert.That(derived.GetEffectiveMagickPoints(id), Is.EqualTo(5));
            Assert.That(derived.GetEffectiveTechPoints(id), Is.Zero);
            Assert.That(derived.GetDerivedStat(id, CharacterDerivedStat.MagickTechAptitude), Is.EqualTo(25));
        }

        [Test]
        public void PersuasionTrainingReconstructsOwnedFollowerContribution()
        {
            ArcanumObjectId id = Identity(5);
            var attributes = Enumerable.Repeat(8, CharacterAttributeSet.Count).ToArray();
            attributes[(int)CharacterAttribute.Charisma] = 12;
            var characters = new CharacterStatService();
            characters.GetOrCreateCreatedPlayer(id, new CharacterAttributeSet(attributes, CharacterRace.Human),
                CharacterRace.Human, CharacterGender.Male);
            var basic = new int[CharacterSkillRules.BasicSkillCount];
            basic[(int)CharacterSkill.Persuasion] = 2 | ((int)SkillTrainingLevel.Expert << 6);
            var progression = new CharacterProgressionService(characters);
            progression.GetOrCreateCreatedPlayer(id, new CharacterProgressionSource(1, 0, 0, basic,
                new int[CharacterSkillRules.TechnicalSkillCount]));
            var derived = new CharacterDerivedStatService(characters, progression);
            derived.GetOrCreateCreatedPlayer(id, CharacterDerivedSource.DevelopmentPlayer);

            CharacterContributor contribution = characters.Contributors.GetOrderedContributors(id)
                .Single(value => value.Domain == CharacterContributorDomain.Training);
            Assert.That((contribution.Target, contribution.Amount),
                Is.EqualTo((CharacterContributorTarget.DerivedStat(CharacterDerivedStat.MaximumFollowers), 1)));
            Assert.That(derived.GetDerivedStat(id, CharacterDerivedStat.MaximumFollowers), Is.EqualTo(4));
        }

        [Test]
        public void CharacterWithoutContributorsRetainsExistingSourceValues()
        {
            ArcanumObjectId id = Identity(6);
            var characters = new CharacterStatService();
            characters.GetOrCreateDevelopmentPlayer(id);
            var progression = new CharacterProgressionService(characters);
            progression.GetOrCreateDevelopmentPlayer(id);
            var derived = new CharacterDerivedStatService(characters, progression);
            derived.GetOrCreateDevelopmentPlayer(id);

            Assert.That(characters.Contributors.GetOrderedContributors(id), Is.Empty);
            Assert.That(characters.GetEffectiveAttribute(id, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(derived.GetDerivedStat(id, CharacterDerivedStat.Speed), Is.EqualTo(8));
            Assert.That(derived.GetDerivedStat(id, CharacterDerivedStat.MaximumFollowers), Is.EqualTo(2));
            Assert.That(derived.GetDerivedStat(id, CharacterDerivedStat.MagickTechAptitude), Is.Zero);
        }

        private static CharacterContributor Add(string owner, string identity, CharacterContributorTarget target,
            int amount, CharacterContributorDomain domain = CharacterContributorDomain.Background)
            => Value(owner, domain, identity, target, CharacterContributorOperation.Add, amount, 0);

        private static CharacterContributor Value(string owner, CharacterContributorDomain domain, string identity,
            CharacterContributorTarget target, CharacterContributorOperation operation, int amount, int order)
            => new(owner, domain, 100, identity, target, operation, amount, order);

        private static ArcanumObjectId Identity(int value)
            => ArcanumObjectId.CreateGuid(new Guid(value, 0, 0, new byte[8]));
    }
}
