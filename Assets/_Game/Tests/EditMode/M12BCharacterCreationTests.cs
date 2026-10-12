using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using Arcanum.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M12BCharacterCreationNewGame")]
    public sealed class M12BCharacterCreationTests
    {
        private const string StartSector = "maps/arcanum1-024-fixed/86570436012.sec";
        private GameObject _root;
        private WorldMapSessionCoordinator _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(M12BCharacterCreationTests));
            _session = _root.AddComponent<WorldMapSessionCoordinator>();
            _session.RegisterObjectOwner(new FakeOwner(_session));
            _session.BindInventoryFootprintSource(_ => InventoryFootprint.OneCell);
            var gold = new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3) { GoldQuantity = 1 };
            var armor = new ObjectProtoInfo(8157, ObjectType.Armor, 0x70000000u, invAid: 0);
            var pregenerated = PregeneratedPrototype();
            _session.BindPrototypeSource(number => number == 9056 ? gold
                : number == 8157 ? armor : StartingPrototype(number) ?? (number == 16067 ? pregenerated : null));
            _session.BindEconomySource(InventorySourceCatalog.FromMes(
                Mes((108, "Merwin: 0 0 100 8137")), null));
            _session.BindCharacterCreationSource(CreateCatalog());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void DefaultHumanSpecificationIsValidAndCostsNothing()
        {
            CharacterCreationValidationResult result = _session.CharacterCreation.Validate(Default());
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.SpentCharacterPoints, Is.Zero);
            Assert.That(result.RemainingCharacterPoints, Is.EqualTo(5));
        }

        [Test]
        public void UnsupportedRaceGenderAndRestrictedBackgroundFailBeforeMutation()
        {
            CharacterCreationSpecification source = Default();
            source.Race = CharacterRace.Dwarf; source.Gender = CharacterGender.Female; source.PortraitId = 1001;
            Assert.That(_session.CharacterCreation.Validate(source).Failure,
                Is.EqualTo(CharacterCreationFailure.InvalidRaceGender));
            source.Gender = CharacterGender.Male; source.BackgroundId = 3;
            Assert.That(_session.CharacterCreation.Validate(source).Failure,
                Is.EqualTo(CharacterCreationFailure.BackgroundRestricted));
            Assert.That(_session.PlayerState, Is.Null);
        }

        [Test]
        public void PointAccountingIncludesAttributesSkillsSpellsAndTechnology()
        {
            CharacterCreationSpecification source = Default();
            source.SetAttribute(CharacterAttribute.Willpower, 9);
            source.SetSkillPoints(CharacterSkill.Melee, 1);
            source.SetSpellRank(SpellCollege.Earth, 1);
            source.SetTechnologyRank(TechnologyDiscipline.Herbology, 1);
            CharacterCreationValidationResult result = _session.CharacterCreation.Validate(source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.SpentCharacterPoints, Is.EqualTo(4));
            Assert.That(result.RemainingCharacterPoints, Is.EqualTo(1));
            source.SetAttribute(CharacterAttribute.Strength, 10);
            Assert.That(_session.CharacterCreation.Validate(source).Failure,
                Is.EqualTo(CharacterCreationFailure.OverspentCharacterPoints));
        }

        [Test]
        public void RacialAndGenderModifiersRemainOwnedByM4()
        {
            CharacterCreationSpecification source = Default("Doran");
            source.Race = CharacterRace.Dwarf; source.PortraitId = 1001;
            CharacterCreationFinalizeResult result = _session.CharacterCreation.FinalizeNewGame(source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(_session.Characters.GetBaseAttribute(pc, CharacterAttribute.Strength), Is.EqualTo(8));
            Assert.That(_session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Strength), Is.EqualTo(9));
            Assert.That(_session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Dexterity), Is.EqualTo(7));
        }

        [Test]
        public void AuthenticBackgroundAppliesPermanentEffectsOnce()
        {
            CharacterCreationSpecification source = Default(); source.BackgroundId = 1;
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(_session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Beauty), Is.EqualTo(7));
            Assert.That(_session.DerivedStats.GetResistance(pc, CharacterResistance.Poison), Is.EqualTo(40),
                "the authentic +20 background contribution composes with M4D's Constitution-derived 20");
            string json = _session.SaveGames.SerializeCurrentSession();
            var load = _session.SaveGames.LoadJson(json);
            Assert.That(load.Succeeded, Is.True, load.Message);
            Assert.That(_session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Beauty), Is.EqualTo(7));
            Assert.That(_session.DerivedStats.GetResistance(pc, CharacterResistance.Poison), Is.EqualTo(40));
        }

        [Test]
        public void UnsupportedBackgroundEffectsFailClosed()
        {
            CharacterCreationSpecification source = Default(); source.BackgroundId = 5;
            Assert.That(_session.CharacterCreation.Validate(source).Failure,
                Is.EqualTo(CharacterCreationFailure.UnsupportedBackgroundEffect));
        }

        [Test]
        public void HyperactiveSpeedIsTypedReversibleAndMatchesFinalizedAuthority()
        {
            CharacterCreationSpecification source = Default();
            Assert.That(_session.CharacterCreation.ProjectInitialStats(source).Speed, Is.EqualTo(8));
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(source, CharacterAttribute.Charisma),
                Is.EqualTo(8));

            source.BackgroundId = 25;
            CharacterCreationInitialStats hyperactive = _session.CharacterCreation.ProjectInitialStats(source);
            Assert.That(hyperactive.Speed, Is.EqualTo(9));
            Assert.That(_session.CharacterCreation.GetEffectiveAttribute(source, CharacterAttribute.Charisma),
                Is.EqualTo(7));
            Assert.That(_session.CharacterCreation.Validate(source).Succeeded, Is.True);

            source.BackgroundId = 0;
            Assert.That(_session.CharacterCreation.ProjectInitialStats(source).Speed, Is.EqualTo(8));
            source.BackgroundId = 25;
            Assert.That(_session.CharacterCreation.ProjectInitialStats(source).Speed, Is.EqualTo(9),
                "cycling away and back must not stack the Background-owned contribution");

            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(_session.DerivedStats.GetDerivedStat(pc, CharacterDerivedStat.Speed), Is.EqualTo(9));
            Assert.That(_session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Charisma), Is.EqualTo(7));
        }

        [Test]
        public void SkillPurchaseUsesGoverningAttributeCapAndFinalRuntimeState()
        {
            CharacterCreationSpecification source = Default(); source.SetSkillPoints(CharacterSkill.Melee, 1);
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            Assert.That(_session.Progression.GetPurchasedSkillPoints(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                CharacterSkill.Melee), Is.EqualTo(1));

            var invalid = Default(); invalid.SetSkillPoints(CharacterSkill.Melee, 2);
            var other = NewSession();
            Assert.That(other.CharacterCreation.Validate(invalid).Failure,
                Is.EqualTo(CharacterCreationFailure.InvalidSkill));
            Object.DestroyImmediate(other.gameObject);
        }

        [Test]
        public void SpellSelectionRequiresSourceWillpowerAndBecomesM10AKnowledge()
        {
            CharacterCreationSpecification invalid = Default(); invalid.SetSpellRank(SpellCollege.Earth, 2);
            Assert.That(_session.CharacterCreation.Validate(invalid).Failure,
                Is.EqualTo(CharacterCreationFailure.InvalidSpell));
            CharacterCreationSpecification valid = Default(); valid.SetSpellRank(SpellCollege.Earth, 1);
            Assert.That(_session.CharacterCreation.FinalizeNewGame(valid).Succeeded, Is.True);
            Assert.That(_session.Magic.KnowsSpell(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                PhaseOneSpellCatalog.StrengthOfEarth), Is.True);
        }

        [Test]
        public void TechnologySelectionRequiresIntelligenceAndBecomesM10BKnowledge()
        {
            CharacterCreationSpecification invalid = Default();
            invalid.SetTechnologyRank(TechnologyDiscipline.Herbology, 3);
            Assert.That(_session.CharacterCreation.Validate(invalid).Failure,
                Is.EqualTo(CharacterCreationFailure.InvalidTechnology));
            CharacterCreationSpecification valid = Default();
            valid.SetTechnologyRank(TechnologyDiscipline.Herbology, 1);
            Assert.That(_session.CharacterCreation.FinalizeNewGame(valid).Succeeded, Is.True);
            Assert.That(_session.Technology.GetLearnedDegree(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                TechnologyDiscipline.Herbology), Is.EqualTo(TechnologyDegree.Novice));
        }

        [Test]
        public void AuthenticStartingGoldAndBackgroundItemRemainInTheShopInventory()
        {
            CharacterCreationSpecification source = Default(); source.BackgroundId = 3;
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(_session.GetGold(pc), Is.EqualTo(400));
            PersistentObjectState armor = _session.States.Values.Single(value => value.PrototypeNumber == 8157);
            Assert.That(armor.ParentIdentity, Is.EqualTo(pc));
            Assert.That(armor.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Contained));
        }

        [TestCase(CharacterRace.Human, CharacterGender.Male, 8049)]
        [TestCase(CharacterRace.Human, CharacterGender.Female, 8133)]
        [TestCase(CharacterRace.Dwarf, CharacterGender.Male, 8065)]
        [TestCase(CharacterRace.Gnome, CharacterGender.Male, 8065)]
        [TestCase(CharacterRace.HalfOrc, CharacterGender.Male, 8049)]
        [TestCase(CharacterRace.HalfOrc, CharacterGender.Female, 8133)]
        [TestCase(CharacterRace.HalfOgre, CharacterGender.Male, 8069)]
        public void SourceRaceGenderClothingMatrixMatchesItemGenerateInventory(CharacterRace race,
            CharacterGender gender, int expectedPrototype)
            => Assert.That(CharacterCreationRules.DefaultClothingPrototype(race, gender),
                Is.EqualTo(expectedPrototype));

        [TestCase(8049, ObjectType.Armor)]
        [TestCase(8065, ObjectType.Armor)]
        [TestCase(8069, ObjectType.Armor)]
        [TestCase(8133, ObjectType.Armor)]
        [TestCase(8134, ObjectType.Armor)]
        [TestCase(8135, ObjectType.Armor)]
        [TestCase(15178, ObjectType.Generic)]
        [TestCase(15179, ObjectType.Generic)]
        public void SourceBaseStartingPrototypesResolveWithoutLooseOverrides(int prototype, ObjectType type)
        {
            ObjectProtoInfo resolved = new ProtoLibrary(string.Empty).Get(prototype);
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved.Type, Is.EqualTo(type));
            Assert.That(resolved.Description, Is.EqualTo(prototype));
            Assert.That(resolved.InvAid, Is.Not.Null);
        }

        [Test]
        public void SourceBaseNiceSuitAndSkillItemsPreserveProtoCArtAndItemFields()
        {
            var library = new ProtoLibrary(string.Empty);
            ObjectProtoInfo suit = library.Get(8049);
            Assert.That(suit.CurrentArtId, Is.EqualTo(0x60000202u));
            Assert.That(suit.InvAid, Is.EqualTo(0x60001202u));
            Assert.That(suit.ArmorAc, Is.EqualTo(2));
            Assert.That(suit.ArmorResist[0], Is.EqualTo(1));
            Assert.That(library.Get(8133).InvAid, Is.EqualTo(0x600A1202u), "Plain Dress art");
            Assert.That(library.Get(8134).InvAid, Is.EqualTo(0x600A1202u), "small women's clothing art");
            Assert.That(library.Get(8135).InvAid, Is.EqualTo(0x600A1202u), "large women's clothing art");
            ObjectProtoInfo lockpicks = library.Get(15178);
            Assert.That(lockpicks.CurrentArtId, Is.EqualTo(0x60140009u));
            Assert.That(lockpicks.InvAid, Is.EqualTo(0x60141009u));
            Assert.That(lockpicks.ItemComplexity, Is.EqualTo(-20));
            Assert.That(lockpicks.ItemFlags, Is.EqualTo(0x180));
            ObjectProtoInfo bandages = library.Get(15179);
            Assert.That(bandages.CurrentArtId, Is.EqualTo(0x61600009u));
            Assert.That(bandages.InvAid, Is.EqualTo(0x61601009u));
            Assert.That(bandages.GenericFlags, Is.EqualTo(0x8));
        }

        [Test]
        public void SourceBasePlainDressWinsOverTheShippedSerializedPrototypeDelta()
        {
            string protoDirectory = GameDataLocator.FindDirectory("data/proto");
            Assert.That(protoDirectory, Is.Not.Null.And.Not.Empty);

            ObjectProtoInfo dress = new ProtoLibrary(protoDirectory).Get(8133);

            Assert.That(dress.Description, Is.EqualTo(8133));
            Assert.That(dress.CurrentArtId, Is.EqualTo(0x600A0202u));
            Assert.That(dress.InvAid, Is.EqualTo(0x600A1202u));
            Assert.That(dress.Weight, Is.EqualTo(40));
            Assert.That(dress.HpPoints, Is.EqualTo(30));
            Assert.That(dress.ArmorAc, Is.EqualTo(2));
            Assert.That(dress.ArmorResist[0], Is.EqualTo(1));
        }

        [Test]
        public void EngineOwnedSchematicComponentsResolveDescriptionIdentityWithoutLooseOverrides()
        {
            ObjectProtoInfo capacitor = new ProtoLibrary(string.Empty).Get(15072);
            Assert.That(capacitor, Is.Not.Null);
            Assert.That(capacitor.Type, Is.EqualTo(ObjectType.Generic));
            Assert.That(capacitor.Description, Is.EqualTo(15072));
        }

        [Test]
        public void SourceAutoLevelNamesAreCatalogAuthorityAndSelectionSurvivesFinalization()
        {
            Assert.That(_session.CharacterCreation.Catalog.AutoLevelSchemes.Select(value => value.Id),
                Does.Contain(76));
            CharacterCreationSpecification source = Default();
            source.AutoLevelScheme = 76;
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            Assert.That(_session.CharacterCreation.Finalized.AutoLevelScheme, Is.EqualTo(76));
        }

        [Test]
        public void SourceAutoLevelRuleSpendsLevelUpPointsInRuleOrderAndSurvivesSaveLoad()
        {
            CharacterCreationSpecification source = Default();
            source.AutoLevelScheme = 1;
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);

            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.Progression.AwardExperience(pc, CharacterProgressionService.GetExperienceForLevel(2));

            Assert.That(_session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.Melee), Is.EqualTo(1));
            Assert.That(_session.Characters.GetBaseAttribute(pc, CharacterAttribute.Strength), Is.EqualTo(9));
            Assert.That(_session.Progression.GetUnspentCharacterPoints(pc), Is.EqualTo(4));
            Assert.That(_session.CharacterCreation.Finalized.AutoLevelScheme, Is.EqualTo(1));
        }

        [Test]
        public void Scheme76MalformedSegmentUsesSourceTokenizerAndScheme77SegmentRemainsValid()
        {
            using var vfs = new DatVirtualFileSystem();
            foreach (string file in new[]
                     {
                         "modules/Arcanum.dat", "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat",
                     })
            {
                string archive = GameDataLocator.Find(file);
                if (!string.IsNullOrEmpty(archive)) vfs.MountFile(archive);
            }
            Assert.That(vfs.Exists("rules/gamelevel.mes"), Is.True,
                "the clean retail auto-level source is required for this compatibility regression");
            MesFile retailRules = MesReader.Read(vfs.ReadAllBytes("rules/gamelevel.mes"));
            string malformed = retailRules.Get(76).Split(',')
                .Single(value => value.Trim() == "dx 20 pickpocket 20").Trim();
            string neighboring = retailRules.Get(77).Split(',')
                .Select(value => value.Trim())
                .First(value => value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length == 2);

            Assert.That(_session.CharacterCreation.FinalizeNewGame(Default()).Succeeded, Is.True);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _session.Progression.AwardExperience(pc, CharacterProgressionService.GetExperienceForLevel(20));
            int pickPocketBefore = _session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.PickPocket);

            Assert.That(_session.CharacterCreation.ApplyAutoLevelRule(pc, malformed), Is.True);
            Assert.That(_session.Characters.GetBaseAttribute(pc, CharacterAttribute.Dexterity), Is.EqualTo(20));
            Assert.That(_session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.PickPocket),
                Is.EqualTo(pickPocketBefore), "CE consumes Scheme 76's trailing malformed text without applying it");
            Assert.That(_session.CharacterCreation.ApplyAutoLevelRule(pc, neighboring), Is.True,
                "a valid comma-delimited segment from neighboring Scheme 77 must retain its prior behavior");
        }

        [Test]
        public void DefaultHumanClothingIsCreatedContainedForStartingEquipment()
        {
            Assert.That(_session.CharacterCreation.FinalizeNewGame(Default()).Succeeded, Is.True);
            PersistentObjectState clothing = _session.States.Values.Single(value => value.PrototypeNumber == 8049);
            Assert.That(clothing.ParentIdentity, Is.EqualTo(ProductionPlayerLifecycle.DefaultPlayerIdentity));
            Assert.That(clothing.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Contained));
        }

        [Test]
        public void PurchasedPickLocksAndHealAddTheirSourceStartingItems()
        {
            CharacterCreationSpecification source = Default();
            source.SetSkillPoints(CharacterSkill.PickLocks, 1);
            source.SetSkillPoints(CharacterSkill.Heal, 1);
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            Assert.That(_session.States.Values.Any(value =>
                value.PrototypeNumber == CharacterCreationRules.CrudeLockpicksPrototype), Is.True);
            Assert.That(_session.States.Values.Any(value =>
                value.PrototypeNumber == CharacterCreationRules.BandagesPrototype), Is.True);
        }

        [Test]
        public void FinalizationIsExactlyOnceAndInvalidRequestLeavesExistingStateUntouched()
        {
            Assert.That(_session.SelectSector("maps/test/1.sec"), Is.True);
            PersistentPlayerState previous = _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                "maps/test/1.sec", Vector2.one, 0x28100000u);
            CharacterCreationSpecification invalid = Default(); invalid.Name = string.Empty;
            Assert.That(_session.CharacterCreation.FinalizeNewGame(invalid).Failure,
                Is.EqualTo(CharacterCreationFailure.InvalidName));
            Assert.That(_session.PlayerState, Is.SameAs(previous));

            Assert.That(_session.CharacterCreation.FinalizeNewGame(Default()).Succeeded, Is.True);
            Assert.That(_session.CharacterCreation.FinalizeNewGame(Default()).Failure,
                Is.EqualTo(CharacterCreationFailure.AlreadyFinalized));
        }

        [Test]
        public void NewGameClearsStaleSessionAndUsesAuthenticCampaignStart()
        {
            Assert.That(_session.SelectSector("maps/test/1.sec"), Is.True);
            _session.GetOrCreatePlayer(ProductionPlayerLifecycle.DefaultPlayerIdentity,
                "maps/test/1.sec", Vector2.one, 0x28100000u);
            _session.Campaign.SetFlag(77, 1);
            _session.AddGold(ProductionPlayerLifecycle.DefaultPlayerIdentity, 50);
            CharacterCreationFinalizeResult result = _session.CharacterCreation.FinalizeNewGame(Default());
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_session.Campaign.GetFlag(77), Is.Zero);
            Assert.That(_session.SelectedSector, Is.EqualTo(StartSector));
            Assert.That(_session.PlayerState.TilePosition, Is.EqualTo(new Vector2(30, 32)));
            Assert.That(_session.Party.Members, Is.Empty);
            Assert.That(_session.Combat.IsActive, Is.False);
        }

        [Test]
        public void ImmediateSaveLoadPreservesIdentityCharacterWorldMagicTechnologyAndInventory()
        {
            CharacterCreationSpecification source = Default("Ada");
            source.BackgroundId = 1;
            source.SetSpellRank(SpellCollege.Earth, 1);
            source.SetTechnologyRank(TechnologyDiscipline.Herbology, 1);
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"characterCreation\""));
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(_session.CharacterCreation.Finalized.Name, Is.EqualTo("Ada"));
            Assert.That(_session.CharacterCreation.Finalized.PortraitId, Is.EqualTo(1005));
            Assert.That(_session.CharacterCreation.Finalized.SpentCharacterPoints, Is.EqualTo(2));
            Assert.That(_session.CharacterCreation.Finalized.RemainingCharacterPoints, Is.EqualTo(3));
            Assert.That(_session.Characters.GetEffectiveAttribute(pc, CharacterAttribute.Beauty), Is.EqualTo(7));
            Assert.That(_session.Magic.KnowsSpell(pc, PhaseOneSpellCatalog.StrengthOfEarth), Is.True);
            Assert.That(_session.Technology.GetLearnedDegree(pc, TechnologyDiscipline.Herbology),
                Is.EqualTo(TechnologyDegree.Novice));
            Assert.That(_session.GetGold(pc), Is.EqualTo(400));
            Assert.That(_session.SelectedSector, Is.EqualTo(StartSector));
        }

        [Test]
        public void PregeneratedBasicPrototypeReconstructsEngineOwnedSourceDefaultsWithoutLooseOverride()
        {
            var gold = new ObjectProtoInfo(9056, ObjectType.Gold, 0x60000003u, invAid: 3) { GoldQuantity = 1 };
            var armor = new ObjectProtoInfo(8157, ObjectType.Armor, 0x70000000u, invAid: 0);
            _session.BindPrototypeSource(number => number == 9056 ? gold : number == 8157 ? armor : null);
            PregeneratedCharacterDefinition selected = _session.CharacterCreation.Catalog.PregeneratedCharacters
                .Single(value => value.SourceIndex == 1);
            ObjectProtoInfo prototype = _session.CharacterCreation.ResolvePregeneratedPrototype(selected);

            Assert.That(prototype.ProtoNumber, Is.EqualTo(16067));
            Assert.That(prototype.Type, Is.EqualTo(ObjectType.Pc));
            Assert.That(prototype.Portrait, Is.EqualTo(1017));
            Assert.That(prototype.StatBase.Take(CharacterAttributeSet.Count),
                Is.EqualTo(Enumerable.Repeat(CharacterAttributeSet.SourceDefault, CharacterAttributeSet.Count)));
            Assert.That(prototype.StatBase[CharacterProgressionSource.LevelSourceSlot], Is.EqualTo(1));
            Assert.That(prototype.StatBase[CharacterAttributeSet.GenderSourceSlot], Is.EqualTo((int)CharacterGender.Male));
            Assert.That(prototype.StatBase[CharacterAttributeSet.RaceSourceSlot], Is.EqualTo((int)CharacterRace.Halfling));
            Assert.That(prototype.CurrentArtId,
                Is.EqualTo(CharacterCreationRules.BuildPlayerArt(CharacterRace.Halfling, CharacterGender.Male)));

            CharacterCreationFinalizeResult result = _session.CharacterCreation.FinalizePregeneratedCharacter(
                selected.SourceIndex);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_session.Characters.Get(ProductionPlayerLifecycle.DefaultPlayerIdentity).PrototypeNumber,
                Is.EqualTo(selected.PrototypeNumber));
        }

        [Test]
        public void PregeneratedInitialStatsApplyRaceGenderBackgroundAndSharedDerivedRules()
        {
            PregeneratedCharacterDefinition selected = _session.CharacterCreation.Catalog.PregeneratedCharacters
                .Single(value => value.SourceIndex == 1);
            CharacterCreationInitialStats stats = _session.CharacterCreation
                .ProjectPregeneratedInitialStats(selected);

            Assert.That(new[]
            {
                stats.GetAttribute(CharacterAttribute.Strength),
                stats.GetAttribute(CharacterAttribute.Constitution),
                stats.GetAttribute(CharacterAttribute.Dexterity),
                stats.GetAttribute(CharacterAttribute.Beauty),
                stats.GetAttribute(CharacterAttribute.Intelligence),
                stats.GetAttribute(CharacterAttribute.Willpower),
                stats.GetAttribute(CharacterAttribute.Perception),
                stats.GetAttribute(CharacterAttribute.Charisma),
            }, Is.EqualTo(new[] { 5, 8, 10, 8, 8, 8, 8, 8 }));
            Assert.That(new[]
            {
                stats.CarryWeight, stats.DamageBonus, stats.ArmorClassAdjustment, stats.Speed,
                stats.HealRate, stats.PoisonRecovery, stats.ReactionModifier,
            }, Is.EqualTo(new[] { 2500, -2, 0, 10, 3, 8, -7 }));
        }

        [Test]
        public void PregeneratedPrototypeFinalizesExactlyOnceWithSourceIdentityInventoryFlagAndSaveLoad()
        {
            CharacterCreationFinalizeResult result = _session.CharacterCreation.FinalizePregeneratedCharacter(1);
            Assert.That(result.Succeeded, Is.True, result.Message);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(_session.CharacterCreation.Finalized.Name, Is.EqualTo("Merwin Tumblebrook"));
            Assert.That(_session.CharacterCreation.Finalized.PortraitId, Is.EqualTo(1017));
            Assert.That(_session.Characters.Get(pc).PrototypeNumber, Is.EqualTo(16067));
            Assert.That(_session.Characters.Get(pc).Race, Is.EqualTo(CharacterRace.Halfling));
            Assert.That(_session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.Melee), Is.EqualTo(2));
            Assert.That(_session.Campaign.GetFlag(123), Is.EqualTo(1));
            PersistentObjectState armor = _session.States.Values.Single(value => value.PrototypeNumber == 8157);
            Assert.That(armor.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Equipped));
            Assert.That(_session.CharacterCreation.FinalizePregeneratedCharacter(1).Failure,
                Is.EqualTo(CharacterCreationFailure.AlreadyFinalized));

            string json = _session.SaveGames.SerializeCurrentSession();
            Assert.That(json, Does.Contain("\"pregeneratedSourceIndex\": 1"));
            var load = _session.SaveGames.LoadJson(json);
            Assert.That(load.Succeeded, Is.True, load.Message);
            Assert.That(_session.CharacterCreation.Finalized.Name, Is.EqualTo("Merwin Tumblebrook"));
            Assert.That(_session.Characters.Get(pc).Race, Is.EqualTo(CharacterRace.Halfling));
            Assert.That(_session.Campaign.GetFlag(123), Is.EqualTo(1));
        }

        private WorldMapSessionCoordinator NewSession()
        {
            var root = new GameObject("secondary");
            var session = root.AddComponent<WorldMapSessionCoordinator>();
            session.BindCharacterCreationSource(CreateCatalog());
            return session;
        }

        private static CharacterCreationSpecification Default(string name = "Arbuckle")
            => new() { Name = name, BackgroundId = 0, PortraitId = 1005 };

        [Test]
        public void RetailPortraitPrefixesDifferentiateHalflingFromHalfElf()
        {
            CharacterCreationCatalog catalog = CreateCatalog();

            Assert.That(catalog.Portraits[1003].Race, Is.EqualTo(CharacterRace.Halfling));
            Assert.That(catalog.Portraits[1010].Race, Is.EqualTo(CharacterRace.HalfElf));
            Assert.That(catalog.Portraits[1011].Race, Is.EqualTo(CharacterRace.HalfElf));
            Assert.That(catalog.Portraits[1011].Gender, Is.EqualTo(CharacterGender.Female));
        }

        private static CharacterCreationCatalog CreateCatalog()
        {
            MesFile rules = Mes(
                (0, "1000"), (1, "86"), (2, ""), (3, "400"), (4, ""),
                (10, "1001"), (11, "87"), (12, ""), (13, "400"), (14, ""),
                (30, "1003"), (31, "89"), (32, "HUF HUM"), (33, "400"), (34, "8157"),
                (390, "1039"), (391, "86"), (392, "HEM"), (393, "0"), (394, ""),
                (50, "1005"), (51, "200"), (52, ""), (53, "400"), (54, ""),
                (250, "1025"), (251, "201"), (252, ""), (253, "400"), (254, ""));
            MesFile text = Mes((1000, "No significant background\n\nNothing happened."),
                (1001, "Raised by Snake Handlers\n\nPoison and Beauty."),
                (1003, "Raised by Elves\n\nElven mail."),
                (1039, "Merwin's Background\n\nRetail pregenerated source."),
                (1005, "Unsupported\n\nUnsupported test."),
                (1025, "Hyperactive\n\nSpeed and Charisma."));
            MesFile effects = Mes((86, ""), (87, "resistpoison +20, be -1"),
                (89, "repair -1, firearms -1, picklock -1, armtrap -1"), (200, "reaction +1"),
                (201, "speed +1, ch -1"));
            MesFile portraits = Mes((1001, "DWM1"), (1005, "HUM1"), (1006, "ELF1"),
                (1009, "HGM1"), (1010, "HEM1"), (1003, "HAM1"), (1004, "HOM1"),
                (1002, "GNM1"), (1007, "HOF1"), (1008, "HUF1"), (1011, "HEF1"),
                (1017, "HAM2"));
            MapList maps = MapList.Read(Mes((5000,
                "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0")));
            return CharacterCreationCatalog.FromMes(rules, text, effects, portraits, maps,
                Mes((1, "Melee Specialist"), (76, "Merwin Tumblebrook")), Mes((551, "123")),
                Mes((1, "melee 4, st 9")));
        }

        private static ObjectProtoInfo PregeneratedPrototype()
        {
            var stats = Enumerable.Repeat(8, CharacterAttributeSet.SourceStatArrayCount).ToArray();
            stats[17] = 1;
            stats[18] = 0;
            stats[21] = 3;
            stats[25] = 20;
            stats[26] = (int)CharacterGender.Male;
            stats[27] = (int)CharacterRace.Halfling;
            var basic = new int[CharacterSkillRules.BasicSkillCount];
            basic[(int)CharacterSkill.Melee] = 2;
            return new ObjectProtoInfo(16067, ObjectType.Pc, 0x2D100000u)
            {
                StatBase = stats,
                BasicSkills = basic,
                TechSkills = new int[CharacterSkillRules.TechnicalSkillCount],
                SpellTech = new int[25],
                Resistances = new int[CharacterDerivedStatRules.ResistanceCount],
                Portrait = 1017,
                InventorySource = 108,
                HpPoints = 0,
                FatiguePoints = 0,
            };
        }

        private static ObjectProtoInfo StartingPrototype(int number)
        {
            int[] clothing = { 8049, 8065, 8069, 8133, 8134, 8135 };
            if (clothing.Contains(number))
                return new ObjectProtoInfo(number, ObjectType.Armor, 0x70000000u,
                    description: number, invAid: (uint)number);
            if (number is 15178 or 15179)
                return new ObjectProtoInfo(number, ObjectType.Generic, 0x70000000u,
                    description: number, invAid: (uint)number);
            return null;
        }

        private static MesFile Mes(params (int key, string value)[] values)
            => new(values.Select(value => new KeyValuePair<int, string>(value.key, value.value)).ToList());

        private sealed class FakeOwner : ISectorPresentationOwner
        {
            private readonly WorldMapSessionCoordinator _session;
            public FakeOwner(WorldMapSessionCoordinator session) => _session = session;
            public string ConfiguredSector => StartSector;
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
                string sector = PresentedSector; PresentedSector = null;
                if (sector != null) _session.UnloadSector(sector);
            }
        }
    }
}
