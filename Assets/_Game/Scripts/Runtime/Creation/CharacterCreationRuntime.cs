using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Creation
{
    public enum CharacterCreationFailure
    {
        None,
        SourceUnavailable,
        AlreadyFinalized,
        InvalidName,
        InvalidRace,
        InvalidGender,
        InvalidRaceGender,
        InvalidPortrait,
        InvalidBackground,
        BackgroundRestricted,
        UnsupportedBackgroundEffect,
        InvalidAttribute,
        InvalidSkill,
        InvalidSpell,
        InvalidTechnology,
        OverspentCharacterPoints,
        MissingStartingPrototype,
        CommitFailed,
    }

    public readonly struct CharacterCreationValidationResult
    {
        public CharacterCreationFailure Failure { get; }
        public string Message { get; }
        public int SpentCharacterPoints { get; }
        public int RemainingCharacterPoints { get; }
        public bool Succeeded => Failure == CharacterCreationFailure.None;

        internal CharacterCreationValidationResult(CharacterCreationFailure failure, string message,
            int spent = 0, int remaining = 0)
        {
            Failure = failure;
            Message = message;
            SpentCharacterPoints = spent;
            RemainingCharacterPoints = remaining;
        }
    }

    public readonly struct CharacterCreationFinalizeResult
    {
        public CharacterCreationFailure Failure { get; }
        public string Message { get; }
        public FinalizedCharacterSpecification Character { get; }
        public bool Succeeded => Failure == CharacterCreationFailure.None;

        internal CharacterCreationFinalizeResult(CharacterCreationFailure failure, string message = null,
            FinalizedCharacterSpecification character = null)
        {
            Failure = failure;
            Message = message;
            Character = character;
        }
    }

    /// <summary>UI-independent, reversible source choices. This editor state is never saved in Session V1.</summary>
    public sealed class CharacterCreationSpecification
    {
        private readonly int[] _attributes = Enumerable.Repeat(CharacterAttributeSet.SourceDefault,
            CharacterAttributeSet.Count).ToArray();
        private readonly int[] _skills = new int[CharacterSkillRules.SkillCount];
        private readonly int[] _spells = new int[16];
        private readonly int[] _technology = new int[8];

        public string Name { get; set; } = string.Empty;
        public CharacterRace Race { get; set; } = CharacterRace.Human;
        public CharacterGender Gender { get; set; } = CharacterGender.Male;
        public int BackgroundId { get; set; }
        public int PortraitId { get; set; } = 1005;
        public int Age { get; set; } = CharacterCreationRules.SourceStartingAge;

        public int GetAttribute(CharacterAttribute attribute) => _attributes[(int)attribute];
        public void SetAttribute(CharacterAttribute attribute, int value) => _attributes[(int)attribute] = value;
        public int GetSkillPoints(CharacterSkill skill) => _skills[(int)skill];
        public void SetSkillPoints(CharacterSkill skill, int value) => _skills[(int)skill] = value;
        public int GetSpellRank(SpellCollege college) => _spells[(int)college];
        public void SetSpellRank(SpellCollege college, int value) => _spells[(int)college] = value;
        public int GetTechnologyRank(TechnologyDiscipline discipline) => _technology[(int)discipline];
        public void SetTechnologyRank(TechnologyDiscipline discipline, int value) => _technology[(int)discipline] = value;

        internal int[] CopyAttributes() => (int[])_attributes.Clone();
        internal int[] CopySkills() => (int[])_skills.Clone();
        internal int[] CopySpells() => (int[])_spells.Clone();
        internal int[] CopyTechnology() => (int[])_technology.Clone();
    }

    public static class CharacterCreationRules
    {
        public const int StartingCharacterPoints = 5;
        public const int SourceStartingAge = 20;
        public const int MaximumNameLength = 23;
        public const int PlayerPrototypeNumber = 16066;
        private static readonly int[] SpellWillpower = { 0, 6, 9, 12, 15, 18 };
        private static readonly int[] SpellLevel = { 0, 1, 1, 5, 10, 15 };
        private static readonly int[] TechnologyIntelligence = { 0, 5, 8, 11, 13, 15, 17, 19 };

        public static bool IsPlayableRace(CharacterRace race)
            => race >= CharacterRace.Human && race <= CharacterRace.HalfOgre;

        public static bool IsGenderLegal(CharacterRace race, CharacterGender gender)
            => gender == CharacterGender.Male || gender == CharacterGender.Female
               && race is CharacterRace.Human or CharacterRace.Elf or CharacterRace.HalfElf
                   or CharacterRace.HalfOrc;

        public static int SpellWillpowerRequirement(int rank) => SpellWillpower[rank];
        public static int SpellLevelRequirement(int rank) => SpellLevel[rank];
        public static int TechnologyIntelligenceRequirement(int rank) => TechnologyIntelligence[rank];

        public static uint BuildPlayerArt(CharacterRace race, CharacterGender gender)
        {
            int body = RaceArt.BodyType((Race)(int)race);
            return ((uint)ArtId.TypeCritter << 28)
                   | ((uint)(gender == CharacterGender.Male ? 1 : 0) << 27)
                   | ((uint)body << 24)
                   | (1u << 20); // retail creation begins in ordinary villager clothing
        }
    }

    public sealed class BackgroundDefinition
    {
        private readonly Dictionary<CharacterAttribute, int> _attributes;
        private readonly Dictionary<CharacterSkill, int> _skills;
        private readonly Dictionary<CharacterResistance, int> _resistances;
        private readonly int[] _items;

        public int Id { get; }
        public int TextId { get; }
        public string Name { get; }
        public string Description { get; }
        public int EffectId { get; }
        public string RawEffect { get; }
        public string Conditions { get; }
        public int StartingGold { get; }
        public int MagicPointModifier { get; }
        public int TechnologyPointModifier { get; }
        public bool HasUnsupportedEffects { get; }
        public IReadOnlyDictionary<CharacterAttribute, int> AttributeModifiers => _attributes;
        public IReadOnlyDictionary<CharacterSkill, int> SkillModifiers => _skills;
        public IReadOnlyDictionary<CharacterResistance, int> ResistanceModifiers => _resistances;
        public IReadOnlyList<int> StartingItems => _items;

        internal BackgroundDefinition(int id, int textId, string name, string description, int effectId,
            string rawEffect, string conditions, int startingGold,
            Dictionary<CharacterAttribute, int> attributes, Dictionary<CharacterSkill, int> skills,
            Dictionary<CharacterResistance, int> resistances, int magicPoints, int techPoints,
            int[] items, bool unsupported)
        {
            Id = id;
            TextId = textId;
            Name = name;
            Description = description;
            EffectId = effectId;
            RawEffect = rawEffect;
            Conditions = conditions;
            StartingGold = startingGold;
            _attributes = attributes;
            _skills = skills;
            _resistances = resistances;
            MagicPointModifier = magicPoints;
            TechnologyPointModifier = techPoints;
            _items = items;
            HasUnsupportedEffects = unsupported;
        }

        public bool IsLegal(CharacterRace race, CharacterGender gender)
        {
            if (string.IsNullOrWhiteSpace(Conditions)
                || string.Equals(Conditions.Trim(), "ANY", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (string token in Conditions.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                if (CharacterCreationCatalog.TryParseIdentityToken(token, out CharacterRace candidate,
                        out CharacterGender candidateGender)
                    && candidate == race && candidateGender == gender) return true;
            return false;
        }
    }

    public sealed class PortraitDefinition
    {
        public int Id { get; }
        public string SourceName { get; }
        public CharacterRace Race { get; }
        public CharacterGender Gender { get; }

        internal PortraitDefinition(int id, string sourceName, CharacterRace race, CharacterGender gender)
        { Id = id; SourceName = sourceName; Race = race; Gender = gender; }
    }

    /// <summary>Parsed retail creation content. Background and portrait coverage expands with the mounted data.</summary>
    public sealed class CharacterCreationCatalog
    {
        private readonly Dictionary<int, BackgroundDefinition> _backgrounds;
        private readonly Dictionary<int, PortraitDefinition> _portraits;
        public IReadOnlyDictionary<int, BackgroundDefinition> Backgrounds => _backgrounds;
        public IReadOnlyDictionary<int, PortraitDefinition> Portraits => _portraits;
        public MapListEntry StartMap { get; }
        public string StartSector { get; }
        public Vector2 StartTile { get; }

        private CharacterCreationCatalog(Dictionary<int, BackgroundDefinition> backgrounds,
            Dictionary<int, PortraitDefinition> portraits, MapListEntry startMap)
        {
            _backgrounds = backgrounds;
            _portraits = portraits;
            StartMap = startMap;
            string mapPath = "maps/" + startMap.Name.ToLowerInvariant();
            var global = new Vector2(startMap.X, startMap.Y);
            SectorCoordinate sector = SectorCoordinate.FromGlobal(mapPath, global);
            StartSector = sector.Path;
            StartTile = sector.ToLocal(global);
        }

        public bool TryGetBackground(int id, out BackgroundDefinition background)
            => _backgrounds.TryGetValue(id, out background);
        public bool IsPortraitLegal(int id, CharacterRace race, CharacterGender gender)
            => _portraits.TryGetValue(id, out PortraitDefinition portrait)
               && portrait.Race == race && portrait.Gender == gender;

        public static CharacterCreationCatalog FromMes(MesFile backgroundRules, MesFile backgroundText,
            MesFile effects, MesFile portraits, MapList maps)
        {
            if (backgroundRules == null || backgroundText == null || effects == null || portraits == null
                || maps == null) throw new ArgumentNullException("A complete creation source set is required.");
            var definitions = new Dictionary<int, BackgroundDefinition>();
            for (int id = 0; ; id++)
            {
                int key = id * 10;
                if (!backgroundRules.TryGet(key, out string textValue))
                {
                    if (id > 100) break;
                    continue;
                }
                if (!int.TryParse(textValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int textId)) continue;
                int.TryParse(backgroundRules.Get(key + 1), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int effectId);
                string conditions = backgroundRules.Get(key + 2) ?? string.Empty;
                int.TryParse(backgroundRules.Get(key + 3), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int gold);
                string text = backgroundText.Get(textId) ?? string.Empty;
                string[] lines = text.Replace("\r", string.Empty).Split('\n');
                string name = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? $"Background {id}";
                string rawEffect = effects.Get(effectId) ?? string.Empty;
                ParseEffect(rawEffect, out Dictionary<CharacterAttribute, int> attributes,
                    out Dictionary<CharacterSkill, int> skills,
                    out Dictionary<CharacterResistance, int> resistances,
                    out int magicPoints, out int techPoints, out bool unsupported);
                definitions[id] = new BackgroundDefinition(id, textId, name, text, effectId, rawEffect,
                    conditions, Math.Max(0, gold), attributes, skills, resistances, magicPoints, techPoints,
                    ParseItems(backgroundRules.Get(key + 4)), unsupported);
            }

            var portraitDefinitions = new Dictionary<int, PortraitDefinition>();
            foreach (KeyValuePair<int, string> entry in portraits.Entries)
                if (TryParseIdentityToken(entry.Value, out CharacterRace race, out CharacterGender gender))
                    portraitDefinitions[entry.Key] = new PortraitDefinition(entry.Key, entry.Value.Trim(), race, gender);

            MapListEntry start = maps.Entries.Single(entry => entry.Type == MapType.StartMap);
            return new CharacterCreationCatalog(definitions, portraitDefinitions, start);
        }

        internal static bool TryParseIdentityToken(string token, out CharacterRace race,
            out CharacterGender gender)
        {
            race = default;
            gender = default;
            string value = token?.Trim().ToUpperInvariant();
            if (value == null || value.Length < 3) return false;
            race = value.Substring(0, 2) switch
            {
                "HU" => CharacterRace.Human,
                "DW" => CharacterRace.Dwarf,
                "EL" => CharacterRace.Elf,
                "HA" => CharacterRace.HalfElf,
                "GN" => CharacterRace.Gnome,
                "HE" => CharacterRace.Halfling,
                "HO" => CharacterRace.HalfOrc,
                "HG" => CharacterRace.HalfOgre,
                _ => (CharacterRace)(-1),
            };
            gender = value[2] == 'F' ? CharacterGender.Female
                : value[2] == 'M' ? CharacterGender.Male : (CharacterGender)(-1);
            return CharacterCreationRules.IsPlayableRace(race)
                   && gender is CharacterGender.Female or CharacterGender.Male;
        }

        private static int[] ParseItems(string source)
            => string.IsNullOrWhiteSpace(source) ? Array.Empty<int>()
                : source.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int item) ? item : 0).Where(item => item > 0).ToArray();

        private static void ParseEffect(string source,
            out Dictionary<CharacterAttribute, int> attributes,
            out Dictionary<CharacterSkill, int> skills,
            out Dictionary<CharacterResistance, int> resistances,
            out int magicPoints, out int techPoints, out bool unsupported)
        {
            attributes = new Dictionary<CharacterAttribute, int>();
            skills = new Dictionary<CharacterSkill, int>();
            resistances = new Dictionary<CharacterResistance, int>();
            magicPoints = 0;
            techPoints = 0;
            unsupported = false;
            foreach (string raw in (source ?? string.Empty).Split(','))
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;
                string[] parts = token.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int amount))
                { unsupported = true; continue; }
                string key = parts[0].ToLowerInvariant();
                if (TryAttribute(key, out CharacterAttribute attribute)) Add(attributes, attribute, amount);
                else if (TrySkill(key, out CharacterSkill skill)) Add(skills, skill, amount);
                else if (TryResistance(key, out CharacterResistance resistance)) Add(resistances, resistance, amount);
                else if (key == "magicpts") magicPoints = checked(magicPoints + amount);
                else if (key == "techpts") techPoints = checked(techPoints + amount);
                else unsupported = true;
            }
        }

        private static void Add<T>(Dictionary<T, int> values, T key, int amount)
            => values[key] = checked(values.GetValueOrDefault(key) + amount);

        private static bool TryAttribute(string key, out CharacterAttribute value)
        {
            value = key switch
            {
                "st" => CharacterAttribute.Strength, "dx" => CharacterAttribute.Dexterity,
                "cn" => CharacterAttribute.Constitution, "be" => CharacterAttribute.Beauty,
                "in" => CharacterAttribute.Intelligence, "pe" => CharacterAttribute.Perception,
                "wp" => CharacterAttribute.Willpower, "ch" => CharacterAttribute.Charisma,
                _ => (CharacterAttribute)(-1),
            };
            return (int)value >= 0;
        }

        private static bool TrySkill(string key, out CharacterSkill value)
        {
            value = key switch
            {
                "bow" => CharacterSkill.Bow, "dodge" => CharacterSkill.Dodge,
                "melee" => CharacterSkill.Melee, "throwing" => CharacterSkill.Throwing,
                "backstab" => CharacterSkill.Backstab, "pickpocket" => CharacterSkill.PickPocket,
                "prowling" => CharacterSkill.Prowling, "spottrap" => CharacterSkill.SpotTrap,
                "gambling" => CharacterSkill.Gambling, "haggle" => CharacterSkill.Haggle,
                "heal" => CharacterSkill.Heal, "persuasion" => CharacterSkill.Persuasion,
                "repair" => CharacterSkill.Repair, "firearms" => CharacterSkill.Firearms,
                "picklock" => CharacterSkill.PickLocks, "armtrap" => CharacterSkill.DisarmTraps,
                _ => (CharacterSkill)(-1),
            };
            return (int)value >= 0;
        }

        private static bool TryResistance(string key, out CharacterResistance value)
        {
            value = key switch
            {
                "resistdamage" => CharacterResistance.Normal, "resistfire" => CharacterResistance.Fire,
                "resistelectrical" => CharacterResistance.Electrical,
                "resistpoison" => CharacterResistance.Poison, "resistmagic" => CharacterResistance.Magic,
                _ => (CharacterResistance)(-1),
            };
            return (int)value >= 0;
        }
    }

    public sealed class FinalizedCharacterSpecification
    {
        internal int[] Attributes { get; }
        internal int[] Skills { get; }
        internal int[] Spells { get; }
        internal int[] Technology { get; }
        public string Name { get; }
        public CharacterRace Race { get; }
        public CharacterGender Gender { get; }
        public BackgroundDefinition Background { get; }
        public int PortraitId { get; }
        public int Age { get; }
        public int SpentCharacterPoints { get; }
        public int RemainingCharacterPoints { get; }
        public string StartSector { get; }
        public Vector2 StartTile { get; }
        public uint ArtId { get; }

        internal FinalizedCharacterSpecification(CharacterCreationSpecification source,
            BackgroundDefinition background, CharacterCreationCatalog catalog, int spent)
        {
            Attributes = source.CopyAttributes(); Skills = source.CopySkills();
            Spells = source.CopySpells(); Technology = source.CopyTechnology();
            Name = source.Name.Trim(); Race = source.Race; Gender = source.Gender;
            Background = background; PortraitId = source.PortraitId; Age = source.Age;
            SpentCharacterPoints = spent;
            RemainingCharacterPoints = CharacterCreationRules.StartingCharacterPoints - spent;
            StartSector = catalog.StartSector; StartTile = catalog.StartTile;
            ArtId = CharacterCreationRules.BuildPlayerArt(Race, Gender);
        }
    }

    /// <summary>Coordinator-owned M12B validation and atomic New Game finalization.</summary>
    public sealed class CharacterCreationStateService
    {
        private readonly WorldMapSessionCoordinator _world;
        public CharacterCreationCatalog Catalog { get; private set; }
        public bool IsFinalized => Finalized != null;
        public FinalizedCharacterSpecification Finalized { get; private set; }

        internal CharacterCreationStateService(WorldMapSessionCoordinator world,
            CharacterCreationCatalog catalog = null)
        { _world = world ?? throw new ArgumentNullException(nameof(world)); Catalog = catalog; }

        internal void BindCatalog(CharacterCreationCatalog catalog)
            => Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        internal void ClearFinalized() => Finalized = null;

        public CharacterCreationValidationResult Validate(CharacterCreationSpecification source)
        {
            if (Catalog == null) return Fail(CharacterCreationFailure.SourceUnavailable, "Creation sources are not bound.");
            if (source == null) return Fail(CharacterCreationFailure.InvalidName, "A creation specification is required.");
            if (string.IsNullOrWhiteSpace(source.Name) || source.Name.Trim().Length > CharacterCreationRules.MaximumNameLength
                || string.Equals(source.Name.Trim(), "Unnamed", StringComparison.OrdinalIgnoreCase))
                return Fail(CharacterCreationFailure.InvalidName, "The source name must contain 1..23 characters.");
            if (!CharacterCreationRules.IsPlayableRace(source.Race))
                return Fail(CharacterCreationFailure.InvalidRace, "Only the eight retail PC races are selectable.");
            if (source.Gender is not CharacterGender.Female and not CharacterGender.Male)
                return Fail(CharacterCreationFailure.InvalidGender, "Unknown source gender.");
            if (!CharacterCreationRules.IsGenderLegal(source.Race, source.Gender))
                return Fail(CharacterCreationFailure.InvalidRaceGender, "That race/gender combination has no retail PC body.");
            if (source.Age != CharacterCreationRules.SourceStartingAge)
                return Fail(CharacterCreationFailure.InvalidAttribute, "Retail creation starts at age 20.");
            if (!Catalog.IsPortraitLegal(source.PortraitId, source.Race, source.Gender))
                return Fail(CharacterCreationFailure.InvalidPortrait, "The portrait does not match race and gender.");
            if (!Catalog.TryGetBackground(source.BackgroundId, out BackgroundDefinition background))
                return Fail(CharacterCreationFailure.InvalidBackground, "Unknown background.");
            if (!background.IsLegal(source.Race, source.Gender))
                return Fail(CharacterCreationFailure.BackgroundRestricted, "The background excludes this race/gender.");
            if (background.HasUnsupportedEffects)
                return Fail(CharacterCreationFailure.UnsupportedBackgroundEffect,
                    "This background contains an effect outside the bounded M12B runtime.");

            int spent = 0;
            for (int i = 0; i < CharacterAttributeSet.Count; i++)
            {
                int value = source.GetAttribute((CharacterAttribute)i);
                if (value < CharacterAttributeSet.SourceDefault
                    || value > CharacterAttributeSet.SourceMaximumFor(source.Race, (CharacterAttribute)i))
                    return Fail(CharacterCreationFailure.InvalidAttribute, "An attribute is outside its creation bounds.");
                spent = checked(spent + value - CharacterAttributeSet.SourceDefault);
            }
            for (int i = 0; i < CharacterSkillRules.SkillCount; i++)
            {
                CharacterSkill skill = (CharacterSkill)i;
                int points = source.GetSkillPoints(skill);
                if (points < 0 || points > CharacterSkillRules.MaximumPurchasedPoints)
                    return Fail(CharacterCreationFailure.InvalidSkill, "A skill purchase is outside 0..5 points.");
                int effectiveAttribute = EffectiveAttribute(source, background,
                    CharacterSkillRules.GoverningAttribute(skill));
                int modifier = background.SkillModifiers.GetValueOrDefault(skill);
                if (points * CharacterSkillRules.SkillUnitsPerPoint + modifier
                    > CharacterSkillRules.MaximumRankForAttribute(effectiveAttribute))
                    return Fail(CharacterCreationFailure.InvalidSkill, "A skill exceeds its governing-attribute cap.");
                spent = checked(spent + points);
            }
            int intelligence = EffectiveAttribute(source, background, CharacterAttribute.Intelligence);
            int willpower = EffectiveAttribute(source, background, CharacterAttribute.Willpower);
            for (int i = 0; i < 16; i++)
            {
                int rank = source.GetSpellRank((SpellCollege)i);
                if (rank < 0 || rank > 5 || rank > 0 && (intelligence < 5
                    || willpower < CharacterCreationRules.SpellWillpowerRequirement(rank)
                    || 1 < CharacterCreationRules.SpellLevelRequirement(rank)))
                    return Fail(CharacterCreationFailure.InvalidSpell, "A spell college violates its sequential prerequisite.");
                spent = checked(spent + rank);
            }
            for (int i = 0; i < 8; i++)
            {
                int rank = source.GetTechnologyRank((TechnologyDiscipline)i);
                if (rank < 0 || rank > 7
                    || intelligence < CharacterCreationRules.TechnologyIntelligenceRequirement(rank))
                    return Fail(CharacterCreationFailure.InvalidTechnology,
                        "A technology discipline violates its Intelligence prerequisite.");
                spent = checked(spent + rank);
            }
            if (spent > CharacterCreationRules.StartingCharacterPoints)
                return Fail(CharacterCreationFailure.OverspentCharacterPoints,
                    "More than five starting Character Points were spent.", spent);
            return new CharacterCreationValidationResult(CharacterCreationFailure.None, null, spent,
                CharacterCreationRules.StartingCharacterPoints - spent);
        }

        public CharacterCreationFinalizeResult FinalizeNewGame(CharacterCreationSpecification source)
        {
            if (IsFinalized)
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.AlreadyFinalized,
                    "This session already owns a finalized new character.");
            CharacterCreationValidationResult validation = Validate(source);
            if (!validation.Succeeded)
                return new CharacterCreationFinalizeResult(validation.Failure, validation.Message);
            Catalog.TryGetBackground(source.BackgroundId, out BackgroundDefinition background);
            foreach (int prototype in background.StartingItems.Append(9056))
                if (_world.ResolvePrototype(prototype) == null)
                    return new CharacterCreationFinalizeResult(CharacterCreationFailure.MissingStartingPrototype,
                        $"Starting prototype {prototype} is absent.");

            var finalized = new FinalizedCharacterSpecification(source, background, Catalog,
                validation.SpentCharacterPoints);
            try
            {
                _world.ResetAuthoritativeSession();
                Commit(finalized);
                Finalized = finalized;
                if (!_world.SelectSector(finalized.StartSector))
                    throw new InvalidOperationException("The authentic START_MAP sector could not be presented.");
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.None, character: finalized);
            }
            catch (Exception ex)
            {
                _world.ResetAuthoritativeSession();
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.CommitFailed, ex.Message);
            }
        }

        private void Commit(FinalizedCharacterSpecification value)
        {
            var attributes = new CharacterAttributeSet(value.Attributes, value.Race);
            var basic = new int[CharacterSkillRules.BasicSkillCount];
            var technical = new int[CharacterSkillRules.TechnicalSkillCount];
            for (int i = 0; i < value.Skills.Length; i++)
                if (i < basic.Length) basic[i] = value.Skills[i]; else technical[i - basic.Length] = value.Skills[i];
            var progression = new CharacterProgressionSource(1, 0, value.RemainingCharacterPoints,
                basic, technical, hasInstanceStatOverride: true,
                hasInstanceBasicSkillOverride: true, hasInstanceTechnicalSkillOverride: true);
            var resistances = new int[CharacterDerivedStatRules.ResistanceCount];
            foreach (var pair in value.Background.ResistanceModifiers) resistances[(int)pair.Key] = pair.Value;
            int magicPoints = Math.Max(0, Math.Min(CharacterDerivedStatRules.MaximumAptitudePoints,
                value.Spells.Sum() + value.Background.MagicPointModifier));
            int techPoints = Math.Max(0, Math.Min(CharacterDerivedStatRules.MaximumAptitudePoints,
                value.Technology.Sum() + value.Background.TechnologyPointModifier));
            var derived = new CharacterDerivedSource(0, resistances, 0, magicPoints, techPoints,
                hasInstanceStatOverride: true, hasInstanceResistanceOverride: true);
            var vitality = new CharacterVitalitySource(1, 0, 0, 0, 0, 0, 0);
            var spellTech = new int[25];
            Array.Copy(value.Spells, spellTech, value.Spells.Length);
            spellTech[16] = -1;
            Array.Copy(value.Technology, 0, spellTech, 17, value.Technology.Length);

            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _world.InitializeCreatedPlayer(pc, value.StartSector, value.StartTile, value.ArtId,
                attributes, value.Race, value.Gender, progression, derived, vitality, spellTech);
            ApplyPersistentModifiers(pc, value.Background);
            if (value.Background.StartingGold > 0) _world.AddGold(pc, value.Background.StartingGold);
            foreach (int prototype in value.Background.StartingItems)
            {
                ItemCreationResult created = _world.CreateItem(prototype, ObjectPlacement.ContainedBy(pc));
                if (!created.Succeeded) throw new InvalidOperationException($"Starting item {prototype}: {created.Code}.");
                if (WorldMapSessionCoordinator.TryGetNaturalWornLocation(created.State, out WornLocation location)
                    && !_world.TryGetEquippedItem(pc, location, out _))
                    _world.EquipItem(pc, created.State.Identity, location);
            }
        }

        internal CharacterCreationSaveData ExportSaveData()
            => Finalized == null ? null : new CharacterCreationSaveData
            {
                Identity = ProductionPlayerLifecycle.DefaultPlayerIdentity.Key,
                Name = Finalized.Name,
                BackgroundId = Finalized.Background.Id,
                BackgroundTextId = Finalized.Background.TextId,
                PortraitId = Finalized.PortraitId,
                Age = Finalized.Age,
                SpentCharacterPoints = Finalized.SpentCharacterPoints,
            };

        internal void RestoreSaveData(CharacterCreationSaveData data)
        {
            Finalized = null;
            if (data == null) return;
            if (Catalog == null || !Catalog.TryGetBackground(data.BackgroundId, out BackgroundDefinition background))
                throw new InvalidOperationException("The saved creation background is not available.");
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            PersistentCharacterState character = _world.Characters.Get(pc);
            if (background.TextId != data.BackgroundTextId
                || !Catalog.IsPortraitLegal(data.PortraitId, character.Race, character.Gender))
                throw new InvalidOperationException("The saved creation content does not match its source catalog.");
            var source = new CharacterCreationSpecification
            {
                Name = data.Name, BackgroundId = data.BackgroundId, PortraitId = data.PortraitId, Age = data.Age,
                Race = character.Race, Gender = character.Gender,
            };
            Finalized = new FinalizedCharacterSpecification(source, background, Catalog,
                data.SpentCharacterPoints);
            ApplyPersistentModifiers(pc, background);
        }

        private void ApplyPersistentModifiers(ArcanumObjectId identity, BackgroundDefinition background)
        {
            foreach (var pair in background.AttributeModifiers)
                _world.Characters.SetEffectModifier(identity,
                    $"creation-background:{background.Id}:attribute:{(int)pair.Key}", pair.Key, pair.Value);
            foreach (var pair in background.SkillModifiers)
                _world.Progression.SetEffectModifier(identity,
                    $"creation-background:{background.Id}:skill:{(int)pair.Key}", pair.Key, pair.Value);
        }

        private static int EffectiveAttribute(CharacterCreationSpecification source,
            BackgroundDefinition background, CharacterAttribute attribute)
        {
            int value = source.GetAttribute(attribute)
                        + CharacterAttributeSet.SourceAdjustment(source.Race, source.Gender, attribute)
                        + background.AttributeModifiers.GetValueOrDefault(attribute);
            return Math.Max(CharacterAttributeSet.SourceMinimum,
                Math.Min(CharacterAttributeSet.SourceMaximumFor(source.Race, attribute), value));
        }

        private static CharacterCreationValidationResult Fail(CharacterCreationFailure failure, string message,
            int spent = 0) => new(failure, message, spent,
            Math.Max(0, CharacterCreationRules.StartingCharacterPoints - spent));
    }
}
