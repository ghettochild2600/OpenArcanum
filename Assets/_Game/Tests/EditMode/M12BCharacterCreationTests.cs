using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
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
            _session.BindPrototypeSource(number => number == 9056 ? gold : number == 8157 ? armor : null);
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
            Assert.That(_session.SaveGames.LoadJson(json).Succeeded, Is.True);
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
        public void AuthenticStartingGoldItemAndNaturalEquipmentUseM3Authority()
        {
            CharacterCreationSpecification source = Default(); source.BackgroundId = 3;
            Assert.That(_session.CharacterCreation.FinalizeNewGame(source).Succeeded, Is.True);
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            Assert.That(_session.GetGold(pc), Is.EqualTo(400));
            PersistentObjectState armor = _session.States.Values.Single(value => value.PrototypeNumber == 8157);
            Assert.That(armor.ParentIdentity, Is.EqualTo(pc));
            Assert.That(armor.Placement.Kind, Is.EqualTo(ObjectPlacementKind.Equipped));
            Assert.That(armor.Placement.WornLocation, Is.EqualTo(WornLocation.Armor));
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

        private WorldMapSessionCoordinator NewSession()
        {
            var root = new GameObject("secondary");
            var session = root.AddComponent<WorldMapSessionCoordinator>();
            session.BindCharacterCreationSource(CreateCatalog());
            return session;
        }

        private static CharacterCreationSpecification Default(string name = "Arbuckle")
            => new() { Name = name, BackgroundId = 0, PortraitId = 1005 };

        private static CharacterCreationCatalog CreateCatalog()
        {
            MesFile rules = Mes(
                (0, "1000"), (1, "86"), (2, ""), (3, "400"), (4, ""),
                (10, "1001"), (11, "87"), (12, ""), (13, "400"), (14, ""),
                (30, "1003"), (31, "89"), (32, "HUF HUM"), (33, "400"), (34, "8157"),
                (50, "1005"), (51, "200"), (52, ""), (53, "400"), (54, ""));
            MesFile text = Mes((1000, "No significant background\n\nNothing happened."),
                (1001, "Raised by Snake Handlers\n\nPoison and Beauty."),
                (1003, "Raised by Elves\n\nElven mail."),
                (1005, "Unsupported\n\nUnsupported test."));
            MesFile effects = Mes((86, ""), (87, "resistpoison +20, be -1"),
                (89, "repair -1, firearms -1, picklock -1, armtrap -1"), (200, "speed +1"));
            MesFile portraits = Mes((1001, "DWM1"), (1005, "HUM1"), (1006, "ELF1"),
                (1009, "HGM1"), (1010, "HEM1"), (1003, "HAM1"), (1004, "HOM1"),
                (1002, "GNM1"), (1007, "HOF1"), (1008, "HUF1"), (1011, "HEF1"));
            MapList maps = MapList.Read(Mes((5000,
                "Arcanum1-024-fixed, 92958, 82592, Type: START_MAP, WorldMap: 0")));
            return CharacterCreationCatalog.FromMes(rules, text, effects, portraits, maps);
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
