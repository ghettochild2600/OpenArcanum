using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Dialogue;
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
        InvalidPregeneratedSource,
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

    /// <summary>Read-only source Initial Stats projection used by both new and pregenerated character screens.</summary>
    public readonly struct CharacterCreationInitialStats
    {
        private readonly int[] _attributes;
        private readonly int[] _resistances;

        public int CarryWeight { get; }
        public int DamageBonus { get; }
        public int ArmorClassAdjustment { get; }
        public int Speed { get; }
        public int HealRate { get; }
        public int PoisonRecovery { get; }
        public int ReactionModifier { get; }
        public int MaximumFollowers { get; }
        public int MagickTechAptitude { get; }

        internal CharacterCreationInitialStats(int[] attributes)
            : this(attributes, CharacterRace.Human, null, 0, 0)
        {
        }

        internal CharacterCreationInitialStats(int[] attributes, CharacterRace race,
            IReadOnlyDictionary<CharacterResistance, int> resistanceModifiers,
            int magickPoints, int technologyPoints)
        {
            if (attributes == null || attributes.Length < CharacterAttributeSet.Count)
                throw new ArgumentException("A complete source attribute projection is required.", nameof(attributes));
            _attributes = attributes.ToArray();
            int strength = attributes[(int)CharacterAttribute.Strength];
            int constitution = attributes[(int)CharacterAttribute.Constitution];
            int dexterity = attributes[(int)CharacterAttribute.Dexterity];
            int beauty = attributes[(int)CharacterAttribute.Beauty];
            CarryWeight = CharacterDerivedStatRules.CarryWeightFromStrength(strength);
            DamageBonus = CharacterDerivedStatRules.MeleeDamageBonusFromStrength(strength);
            ArmorClassAdjustment = CharacterDerivedStatRules.ArmorClassAdjustmentFromDexterity(dexterity);
            Speed = CharacterDerivedStatRules.SpeedFromDexterity(dexterity);
            HealRate = CharacterDerivedStatRules.HealRateFromConstitution(constitution);
            PoisonRecovery = CharacterDerivedStatRules.PoisonRecoveryFromConstitution(constitution);
            ReactionModifier = CharacterDerivedStatRules.BeautyReactionModifierFromBeauty(beauty);
            MaximumFollowers = Math.Max(1, Math.Min(7,
                attributes[(int)CharacterAttribute.Charisma] / 4));
            int effectiveMagick = Math.Max(0, Math.Min(CharacterDerivedStatRules.MaximumAptitudePoints,
                magickPoints + CharacterDerivedStatRules.AptitudePointAdjustment(race, true)));
            int effectiveTechnology = Math.Max(0, Math.Min(CharacterDerivedStatRules.MaximumAptitudePoints,
                technologyPoints + CharacterDerivedStatRules.AptitudePointAdjustment(race, false)));
            MagickTechAptitude = Math.Max(-100, Math.Min(100,
                (50 * effectiveMagick - 55 * effectiveTechnology) / 10));
            _resistances = new int[CharacterDerivedStatRules.ResistanceCount];
            foreach (CharacterResistance resistance in CharacterDerivedStatRules.AllResistances)
            {
                int value = resistanceModifiers?.GetValueOrDefault(resistance) ?? 0;
                value += CharacterDerivedStatRules.ResistanceAdjustment(race, resistance);
                if (resistance == CharacterResistance.Poison)
                    value += Math.Max(0, 5 * (constitution - 4));
                _resistances[(int)resistance] = Math.Max(0, Math.Min(95, value));
            }
        }

        public int GetAttribute(CharacterAttribute attribute)
        {
            CharacterAttributeRules.ValidateAttribute(attribute);
            return _attributes[(int)attribute];
        }

        public int GetResistance(CharacterResistance resistance)
        {
            CharacterDerivedStatRules.ValidateResistance(resistance);
            return _resistances[(int)resistance];
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
        public int HitPointPoints { get; set; }
        public int FatiguePoints { get; set; }
        public int AutoLevelScheme { get; set; }

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
        public const int CrudeLockpicksPrototype = 15178;
        public const int BandagesPrototype = 15179;
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

        /// <summary>Source <c>item_generate_inventory</c> race-size/gender clothing selection.</summary>
        public static int DefaultClothingPrototype(CharacterRace race, CharacterGender gender)
        {
            bool small = race is CharacterRace.Dwarf or CharacterRace.Gnome or CharacterRace.Halfling;
            bool large = race == CharacterRace.HalfOgre;
            if (gender == CharacterGender.Female)
                return small ? 8134 : large ? 8135 : 8133;
            return small ? 8065 : large ? 8069 : 8049;
        }

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

    /// <summary>
    /// One retail pregenerated PC. Prototype, scheme, portrait, inventory-source and background numbers come from
    /// proto.c's BP_MERWIN_TUMBLEBROOK..BP_KEREE_MELANGE cases; the display name comes from gamelevelname.mes.
    /// </summary>
    public sealed class PregeneratedCharacterDefinition
    {
        public int SourceIndex { get; }
        public int PrototypeNumber { get; }
        public int AutoLevelScheme { get; }
        public string Name { get; }
        public CharacterRace Race { get; }
        public CharacterGender Gender { get; }
        public int PortraitId { get; }
        public int InventorySourceId { get; }
        public int BackgroundId { get; }
        public int BackgroundTextId { get; }
        public int SelectionGlobalFlag { get; }

        internal PregeneratedCharacterDefinition(int sourceIndex, string name, CharacterRace race,
            CharacterGender gender, int portraitId, int inventorySourceId, int backgroundId,
            int selectionGlobalFlag)
        {
            SourceIndex = sourceIndex;
            PrototypeNumber = checked(CharacterCreationRules.PlayerPrototypeNumber + sourceIndex);
            AutoLevelScheme = 75 + sourceIndex;
            Name = name;
            Race = race;
            Gender = gender;
            PortraitId = portraitId;
            InventorySourceId = inventorySourceId;
            BackgroundId = backgroundId;
            BackgroundTextId = 1000 + backgroundId;
            SelectionGlobalFlag = selectionGlobalFlag;
        }
    }

    public sealed class AutoLevelSchemeDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public string Rule { get; }
        public bool HasRule => !string.IsNullOrWhiteSpace(Rule);
        internal AutoLevelSchemeDefinition(int id, string name, string rule)
        { Id = id; Name = name; Rule = rule?.Trim(); }
    }

    /// <summary>Parsed retail creation content. Background and portrait coverage expands with the mounted data.</summary>
    public sealed class CharacterCreationCatalog
    {
        private readonly Dictionary<int, BackgroundDefinition> _backgrounds;
        private readonly Dictionary<int, PortraitDefinition> _portraits;
        public IReadOnlyDictionary<int, BackgroundDefinition> Backgrounds => _backgrounds;
        public IReadOnlyDictionary<int, PortraitDefinition> Portraits => _portraits;
        public IReadOnlyList<PregeneratedCharacterDefinition> PregeneratedCharacters { get; }
        public IReadOnlyList<AutoLevelSchemeDefinition> AutoLevelSchemes { get; }
        public MapListEntry StartMap { get; }
        public string StartSector { get; }
        public Vector2 StartTile { get; }
        public MapListEntry? ShoppingMap { get; }
        public string ShoppingSector { get; }
        public Vector2 ShoppingTile { get; }
        public bool HasShoppingMap => ShoppingMap.HasValue;

        private CharacterCreationCatalog(Dictionary<int, BackgroundDefinition> backgrounds,
            Dictionary<int, PortraitDefinition> portraits, MapListEntry startMap, MapListEntry? shoppingMap,
            IReadOnlyList<PregeneratedCharacterDefinition> pregeneratedCharacters,
            IReadOnlyList<AutoLevelSchemeDefinition> autoLevelSchemes)
        {
            _backgrounds = backgrounds;
            _portraits = portraits;
            StartMap = startMap;
            string mapPath = "maps/" + startMap.Name.ToLowerInvariant();
            var global = new Vector2(startMap.X, startMap.Y);
            SectorCoordinate sector = SectorCoordinate.FromGlobal(mapPath, global);
            StartSector = sector.Path;
            StartTile = sector.ToLocal(global);
            ShoppingMap = shoppingMap;
            if (shoppingMap.HasValue)
            {
                MapListEntry shop = shoppingMap.Value;
                string shopPath = "maps/" + shop.Name.ToLowerInvariant();
                var shopGlobal = new Vector2(shop.X, shop.Y);
                SectorCoordinate shopSector = SectorCoordinate.FromGlobal(shopPath, shopGlobal);
                ShoppingSector = shopSector.Path;
                ShoppingTile = shopSector.ToLocal(shopGlobal);
            }
            PregeneratedCharacters = pregeneratedCharacters;
            AutoLevelSchemes = autoLevelSchemes;
        }

        public bool TryGetBackground(int id, out BackgroundDefinition background)
            => _backgrounds.TryGetValue(id, out background);
        public bool TryGetAutoLevelScheme(int id, out AutoLevelSchemeDefinition scheme)
        {
            scheme = AutoLevelSchemes.FirstOrDefault(value => value.Id == id);
            return scheme != null;
        }
        public bool IsPortraitLegal(int id, CharacterRace race, CharacterGender gender)
            => _portraits.TryGetValue(id, out PortraitDefinition portrait)
               && portrait.Race == race && portrait.Gender == gender;

        /// <summary>Retail <c>portrait_find_first</c> searches engine portraits in ascending source ID.</summary>
        public PortraitDefinition FindFirstPortrait(CharacterRace race, CharacterGender gender)
            => _portraits.Values.OrderBy(value => value.Id)
                .FirstOrDefault(value => value.Race == race && value.Gender == gender);

        public IReadOnlyList<PortraitDefinition> GetLegalPortraits(CharacterRace race, CharacterGender gender)
            => _portraits.Values.Where(value => value.Race == race && value.Gender == gender)
                .OrderBy(value => value.Id).ToArray();

        public static CharacterCreationCatalog FromMes(MesFile backgroundRules, MesFile backgroundText,
            MesFile effects, MesFile portraits, MapList maps, MesFile gameLevelNames = null,
            MesFile mainMenu = null, MesFile gameLevelRules = null)
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
            MapListEntry? shopping = maps.Entries.Where(entry => entry.Type == MapType.ShoppingMap)
                .Select(entry => (MapListEntry?)entry).SingleOrDefault();
            return new CharacterCreationCatalog(definitions, portraitDefinitions, start, shopping,
                BuildPregeneratedCharacters(gameLevelNames, mainMenu),
                BuildAutoLevelSchemes(gameLevelNames, gameLevelRules));
        }

        private static IReadOnlyList<AutoLevelSchemeDefinition> BuildAutoLevelSchemes(MesFile names, MesFile rules)
            => names?.Entries.Where(value => value.Key is >= 0 and < 200
                                             && !string.IsNullOrWhiteSpace(value.Value))
                .OrderBy(value => value.Key)
                .Select(value => new AutoLevelSchemeDefinition(value.Key, value.Value.Trim(), rules?.Get(value.Key)))
                .ToArray() ?? Array.Empty<AutoLevelSchemeDefinition>();

        private static IReadOnlyList<PregeneratedCharacterDefinition> BuildPregeneratedCharacters(
            MesFile gameLevelNames, MesFile mainMenu)
        {
            (string fallback, CharacterRace race, CharacterGender gender, int portrait, int inventory, int background)[]
                source =
                {
                    ("Merwin Tumblebrook", CharacterRace.Halfling, CharacterGender.Male, 1017, 108, 39),
                    ("Godfrey Castleburger", CharacterRace.Gnome, CharacterGender.Male, 1002, 24, 40),
                    ("Horace McGinley", CharacterRace.HalfElf, CharacterGender.Male, 1032, 20, 41),
                    ("Muck Evil Eye", CharacterRace.HalfOgre, CharacterGender.Male, 1027, 25, 42),
                    ("Dren Lelor", CharacterRace.Elf, CharacterGender.Male, 1033, 20, 43),
                    ("Solomon Doone", CharacterRace.Human, CharacterGender.Male, 1005, 22, 44),
                    ("Merik Luggerton", CharacterRace.HalfOrc, CharacterGender.Male, 1030, 20, 45),
                    ("Lugard Bloodstone", CharacterRace.Dwarf, CharacterGender.Male, 1020, 24, 46),
                    ("Victoria Warrington", CharacterRace.Human, CharacterGender.Female, 1025, 23, 47),
                    ("Belan Tseraa", CharacterRace.Elf, CharacterGender.Female, 1024, 22, 48),
                    ("Clarisse Vorak", CharacterRace.HalfOrc, CharacterGender.Female, 1031, 26, 49),
                    ("Keree Melange", CharacterRace.HalfElf, CharacterGender.Female, 1029, 26, 50),
                };
            var result = new List<PregeneratedCharacterDefinition>(source.Length);
            for (int index = 0; index < source.Length; index++)
            {
                int sourceIndex = index + 1;
                int scheme = 75 + sourceIndex;
                string name = gameLevelNames?.Get(scheme);
                if (string.IsNullOrWhiteSpace(name)) name = source[index].fallback;
                int.TryParse(mainMenu?.Get(551 + index), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int flag);
                var value = source[index];
                result.Add(new PregeneratedCharacterDefinition(sourceIndex, name.Trim(), value.race,
                    value.gender, value.portrait, value.inventory, value.background, Math.Max(0, flag)));
            }
            return result.AsReadOnly();
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
                "HA" => CharacterRace.Halfling,
                "GN" => CharacterRace.Gnome,
                "HE" => CharacterRace.HalfElf,
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
        public int HitPointPoints { get; }
        public int FatiguePoints { get; }
        public int SpentCharacterPoints { get; }
        public int RemainingCharacterPoints { get; }
        public int PregeneratedSourceIndex { get; }
        public int AutoLevelScheme { get; }
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
            HitPointPoints = source.HitPointPoints; FatiguePoints = source.FatiguePoints;
            SpentCharacterPoints = spent;
            RemainingCharacterPoints = CharacterCreationRules.StartingCharacterPoints - spent;
            PregeneratedSourceIndex = 0;
            AutoLevelScheme = source.AutoLevelScheme;
            StartSector = catalog.StartSector; StartTile = catalog.StartTile;
            ArtId = CharacterCreationRules.BuildPlayerArt(Race, Gender);
        }

        internal FinalizedCharacterSpecification(PregeneratedCharacterDefinition source,
            BackgroundDefinition background, CharacterCreationCatalog catalog, ObjectProtoInfo prototype,
            CharacterProgressionSource progression)
        {
            Attributes = new int[CharacterAttributeSet.Count];
            Array.Copy(prototype.StatBase, Attributes, Attributes.Length);
            Skills = new int[CharacterSkillRules.SkillCount];
            foreach (CharacterSkill skill in CharacterSkillRules.AllSkills)
                Skills[(int)skill] = progression.GetPacked(skill) & 63;
            Spells = new int[16];
            Technology = new int[8];
            if (prototype.SpellTech != null)
            {
                Array.Copy(prototype.SpellTech, Spells, Math.Min(Spells.Length, prototype.SpellTech.Length));
                if (prototype.SpellTech.Length > 17)
                    Array.Copy(prototype.SpellTech, 17, Technology, 0,
                        Math.Min(Technology.Length, prototype.SpellTech.Length - 17));
            }
            Name = source.Name;
            Race = source.Race;
            Gender = source.Gender;
            Background = background;
            PortraitId = source.PortraitId;
            Age = prototype.StatBase.Length > 25 ? prototype.StatBase[25] : CharacterCreationRules.SourceStartingAge;
            HitPointPoints = Math.Max(0, prototype.HpPoints.GetValueOrDefault());
            FatiguePoints = Math.Max(0, prototype.FatiguePoints.GetValueOrDefault());
            SpentCharacterPoints = Math.Max(0,
                CharacterCreationRules.StartingCharacterPoints - progression.UnspentCharacterPoints);
            RemainingCharacterPoints = progression.UnspentCharacterPoints;
            PregeneratedSourceIndex = source.SourceIndex;
            AutoLevelScheme = source.AutoLevelScheme;
            StartSector = catalog.StartSector;
            StartTile = catalog.StartTile;
            ArtId = prototype.CurrentArtId != 0 ? prototype.CurrentArtId
                : CharacterCreationRules.BuildPlayerArt(Race, Gender);
        }
    }

    /// <summary>Coordinator-owned M12B validation and atomic New Game finalization.</summary>
    public sealed class CharacterCreationStateService
    {
        private readonly WorldMapSessionCoordinator _world;
        private CharacterProgressionService _autoLevelProgression;
        public CharacterCreationCatalog Catalog { get; private set; }
        public bool IsFinalized => Finalized != null;
        public FinalizedCharacterSpecification Finalized { get; private set; }
        public bool StartingEquipmentActive { get; private set; }
        public ArcanumObjectId StartingEquipmentMerchant { get; private set; }
        public bool OpeningDialogueStarted { get; private set; }

        internal CharacterCreationStateService(WorldMapSessionCoordinator world,
            CharacterCreationCatalog catalog = null)
        { _world = world ?? throw new ArgumentNullException(nameof(world)); Catalog = catalog; }

        internal void BindCatalog(CharacterCreationCatalog catalog)
            => Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        internal void ClearFinalized()
        {
            if (_autoLevelProgression != null)
                _autoLevelProgression.LevelChanged -= OnLevelChanged;
            _autoLevelProgression = null;
            Finalized = null;
            StartingEquipmentActive = false;
            StartingEquipmentMerchant = default;
            OpeningDialogueStarted = false;
        }

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
            if (source.HitPointPoints < 0 || source.FatiguePoints < 0)
                return Fail(CharacterCreationFailure.InvalidAttribute,
                    "HP and Fatigue point purchases cannot be negative.");
            spent = checked(spent + source.HitPointPoints + source.FatiguePoints);
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
            foreach (int prototype in RequiredStartingPrototypes(source, background))
                if (_world.ResolvePrototype(prototype) == null)
                    return new CharacterCreationFinalizeResult(CharacterCreationFailure.MissingStartingPrototype,
                        $"Starting prototype {prototype} is absent.");

            var finalized = new FinalizedCharacterSpecification(source, background, Catalog,
                validation.SpentCharacterPoints);
            try
            {
                _world.ResetAuthoritativeSession();
                // Reset replaces all authoritative domains, including the session-owned HUD observer. Recreate it
                // before starting items, progression, quests, or map discovery can raise source notification events.
                _ = _world.GameplayHud;
                bool shopping = Catalog.HasShoppingMap;
                Commit(finalized, shopping ? Catalog.ShoppingSector : finalized.StartSector,
                    shopping ? Catalog.ShoppingTile : finalized.StartTile);
                Finalized = finalized;
                BindAutoLevelProgression();
                string initialSector = shopping ? Catalog.ShoppingSector : finalized.StartSector;
                if (!_world.SelectSector(initialSector))
                    throw new InvalidOperationException(shopping
                        ? "The authentic SHOPPING_MAP sector could not be presented."
                        : "The authentic START_MAP sector could not be presented.");
                if (shopping) BeginStartingEquipment();
                else TryStartOpeningDialogue();
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.None, character: finalized);
            }
            catch (Exception ex)
            {
                _world.ResetAuthoritativeSession();
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.CommitFailed, ex.Message);
            }
        }

        private void BeginStartingEquipment()
        {
            PersistentObjectState merchant = _world.States.Values
                .Where(value => value.Type == ObjectType.Npc
                                && value.Placement.Kind == ObjectPlacementKind.World
                                && string.Equals(value.Placement.Sector, _world.SelectedSector,
                                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(value => value.Identity.Key, StringComparer.Ordinal)
                .FirstOrDefault(value => _world.Economy.TryResolveMerchant(value.Identity, out _, out _));
            if (merchant == null)
                throw new InvalidOperationException("The authentic SHOPPING_MAP Human Shopkeeper is unavailable.");
            if (!_world.Economy.RestockNow(merchant.Identity, out string failure))
                throw new InvalidOperationException(failure ?? "The Human Shopkeeper inventory could not be generated.");
            StartingEquipmentMerchant = merchant.Identity;
            StartingEquipmentActive = true;
        }

        public bool CompleteStartingEquipment(out string failure)
        {
            failure = null;
            if (!StartingEquipmentActive || Finalized == null || _world.PlayerState == null)
            {
                failure = "No starting-equipment transaction is active.";
                return false;
            }

            string previousSector = _world.SelectedSector;
            Vector2 previousTile = _world.PlayerState.TilePosition;
            uint previousArt = _world.PlayerState.ArtId;
            _world.ClearSelectedSector();
            _world.PlayerState.EnterSector(Finalized.StartSector, Finalized.StartTile, previousArt);
            if (!_world.SelectSector(Finalized.StartSector))
            {
                _world.PlayerState.EnterSector(previousSector, previousTile, previousArt);
                _world.SelectSector(previousSector);
                failure = "The authentic START_MAP sector could not be presented.";
                return false;
            }

            StartingEquipmentActive = false;
            StartingEquipmentMerchant = default;
            TryStartOpeningDialogue();
            return true;
        }

        public bool CancelStartingEquipment(out string failure)
        {
            failure = null;
            if (!StartingEquipmentActive)
            {
                failure = "No starting-equipment transaction is active.";
                return false;
            }
            _world.ResetAuthoritativeSession();
            return true;
        }

        public CharacterCreationFinalizeResult FinalizePregeneratedCharacter(int sourceIndex)
        {
            if (IsFinalized)
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.AlreadyFinalized,
                    "This session already owns a finalized new character.");
            PregeneratedCharacterDefinition selected = Catalog?.PregeneratedCharacters
                .FirstOrDefault(value => value.SourceIndex == sourceIndex);
            if (selected == null)
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.InvalidPregeneratedSource,
                    "Unknown retail pregenerated character.");
            ObjectProtoInfo prototype = ResolvePregeneratedPrototype(selected);
            if (prototype == null || prototype.Type != ObjectType.Pc || prototype.StatBase == null
                || prototype.StatBase.Length < CharacterAttributeSet.SourceStatArrayCount)
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.InvalidPregeneratedSource,
                    $"Retail PC prototype {selected.PrototypeNumber} is unavailable or incomplete.");
            CharacterAttributeSet attributes;
            CharacterRace race;
            CharacterGender gender;
            try { attributes = CharacterAttributeSet.FromSourceStatArray(prototype.StatBase, out race, out gender); }
            catch (Exception exception)
            {
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.InvalidPregeneratedSource,
                    exception.Message);
            }
            bool hasBackground = Catalog.TryGetBackground(selected.BackgroundId, out BackgroundDefinition background);
            if (race != selected.Race || gender != selected.Gender
                || prototype.Portrait != selected.PortraitId
                || !hasBackground
                || background.TextId != selected.BackgroundTextId)
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.InvalidPregeneratedSource,
                    "The pregenerated prototype does not match its retail identity metadata.");

            CharacterProgressionSource progression = CharacterProgressionSource.Resolve(null, prototype.StatBase,
                null, prototype.BasicSkills, null, prototype.TechSkills, prototype.CritterFlags ?? 0);
            CharacterDerivedSource derived = CharacterDerivedSource.Resolve(null, prototype.StatBase,
                null, prototype.BaseArmorClass, null, prototype.Resistances, null, prototype.ReactionBase,
                0, prototype.CritterFlags ?? 0);
            CharacterVitalitySource vitality = CharacterVitalitySource.Resolve(null, prototype.StatBase,
                null, prototype.HpPoints, null, prototype.HpAdjustment, null, prototype.HpDamage,
                null, prototype.FatiguePoints, null, prototype.FatigueAdjustment, null, prototype.FatigueDamage);
            int[] spellTech = prototype.SpellTech != null ? (int[])prototype.SpellTech.Clone() : new int[25];
            var finalized = new FinalizedCharacterSpecification(selected, background, Catalog, prototype, progression);
            try
            {
                _world.ResetAuthoritativeSession();
                _ = _world.GameplayHud;
                ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
                _world.InitializeCreatedPlayer(pc, finalized.StartSector, finalized.StartTile, finalized.ArtId,
                    attributes, race, gender, progression, derived, vitality, spellTech, prototype);
                ApplyPersistentModifiers(pc, background);
                if (!_world.Economy.PopulateStartingInventory(pc, selected.InventorySourceId, out string inventoryFailure))
                    throw new InvalidOperationException(inventoryFailure);
                foreach (ArcanumObjectId item in _world.ChildrenOf(pc).ToArray())
                    if (_world.TryGetObjectState(item, out PersistentObjectState state)
                        && WorldMapSessionCoordinator.TryGetNaturalWornLocation(state, out WornLocation location)
                        && !_world.TryGetEquippedItem(pc, location, out _))
                        _world.EquipItem(pc, item, location);
                foreach (PregeneratedCharacterDefinition value in Catalog.PregeneratedCharacters)
                    if (value.SelectionGlobalFlag > 0)
                        _world.Campaign.SetFlag(value.SelectionGlobalFlag,
                            value.SourceIndex == selected.SourceIndex ? 1 : 0);
                Finalized = finalized;
                BindAutoLevelProgression();
                if (!_world.SelectSector(finalized.StartSector))
                    throw new InvalidOperationException("The authentic START_MAP sector could not be presented.");
                TryStartOpeningDialogue();
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.None, null, finalized);
            }
            catch (Exception exception)
            {
                _world.ResetAuthoritativeSession();
                Finalized = null;
                return new CharacterCreationFinalizeResult(CharacterCreationFailure.CommitFailed,
                    exception.Message);
            }
        }

        private void Commit(FinalizedCharacterSpecification value, string initialSector, Vector2 initialTile)
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
            var vitality = new CharacterVitalitySource(1, value.HitPointPoints, 0, 0,
                value.FatiguePoints, 0, 0);
            var spellTech = new int[25];
            Array.Copy(value.Spells, spellTech, value.Spells.Length);
            spellTech[16] = -1;
            Array.Copy(value.Technology, 0, spellTech, 17, value.Technology.Length);

            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            _world.InitializeCreatedPlayer(pc, initialSector, initialTile, value.ArtId,
                attributes, value.Race, value.Gender, progression, derived, vitality, spellTech);
            ApplyPersistentModifiers(pc, value.Background);
            if (value.Background.StartingGold > 0) _world.AddGold(pc, value.Background.StartingGold);
            foreach (int prototype in value.Background.StartingItems)
            {
                ItemCreationResult created = _world.CreateItem(prototype, ObjectPlacement.ContainedBy(pc));
                if (!created.Succeeded) throw new InvalidOperationException($"Starting item {prototype}: {created.Code}.");
            }
            if (!BackgroundProvidesArmor(value.Background))
                CreateStartingItem(pc, CharacterCreationRules.DefaultClothingPrototype(value.Race, value.Gender));
            if (value.Skills[(int)CharacterSkill.PickLocks] > 0)
                CreateStartingItem(pc, CharacterCreationRules.CrudeLockpicksPrototype);
            if (value.Skills[(int)CharacterSkill.Heal] > 0)
                CreateStartingItem(pc, CharacterCreationRules.BandagesPrototype);
        }

        private IEnumerable<int> RequiredStartingPrototypes(CharacterCreationSpecification source,
            BackgroundDefinition background)
        {
            foreach (int prototype in background.StartingItems) yield return prototype;
            yield return 9056;
            if (!BackgroundProvidesArmor(background))
                yield return CharacterCreationRules.DefaultClothingPrototype(source.Race, source.Gender);
            if (source.GetSkillPoints(CharacterSkill.PickLocks) > 0)
                yield return CharacterCreationRules.CrudeLockpicksPrototype;
            if (source.GetSkillPoints(CharacterSkill.Heal) > 0)
                yield return CharacterCreationRules.BandagesPrototype;
        }

        private bool BackgroundProvidesArmor(BackgroundDefinition background)
            => background.StartingItems.Any(prototype =>
            {
                ObjectProtoInfo item = _world.ResolvePrototype(prototype);
                return item?.Type == ObjectType.Armor
                       && item.InvAid.HasValue
                       && ((item.InvAid.Value >> 14) & 0x7) == 0;
            });

        private void CreateStartingItem(ArcanumObjectId owner, int prototype)
        {
            ItemCreationResult created = _world.CreateItem(prototype, ObjectPlacement.ContainedBy(owner));
            if (!created.Succeeded) throw new InvalidOperationException($"Starting item {prototype}: {created.Code}.");
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
                PregeneratedSourceIndex = Finalized.PregeneratedSourceIndex,
                AutoLevelScheme = Finalized.AutoLevelScheme,
                OpeningDialogueStarted = OpeningDialogueStarted,
            };

        internal void RestoreSaveData(CharacterCreationSaveData data)
        {
            Finalized = null;
            if (data == null) return;
            if (Catalog == null || !Catalog.TryGetBackground(data.BackgroundId, out BackgroundDefinition background))
                throw new InvalidOperationException("The saved creation background is not available.");
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            PersistentCharacterState character = _world.Characters.Get(pc);
            if (background.TextId != data.BackgroundTextId)
                throw new InvalidOperationException("The saved creation content does not match its source catalog.");
            FinalizedCharacterSpecification restored;
            bool identityMatches;
            if (data.PregeneratedSourceIndex > 0)
            {
                PregeneratedCharacterDefinition selected = Catalog.PregeneratedCharacters.FirstOrDefault(value =>
                    value.SourceIndex == data.PregeneratedSourceIndex);
                identityMatches = selected != null
                                  && selected.Name == data.Name
                                  && selected.BackgroundId == data.BackgroundId
                                  && selected.PortraitId == data.PortraitId
                                  && selected.PrototypeNumber == character.PrototypeNumber
                                  && selected.Race == character.Race
                                  && selected.Gender == character.Gender;
                if (!identityMatches)
                    throw new InvalidOperationException("The saved creation content does not match its source catalog.");
                ObjectProtoInfo prototype = ResolvePregeneratedPrototype(selected);
                restored = new FinalizedCharacterSpecification(selected, background, Catalog, prototype,
                    _world.Progression.Get(pc).Source);
                if (restored.Age != data.Age || restored.SpentCharacterPoints != data.SpentCharacterPoints)
                    throw new InvalidOperationException("The saved pregenerated character does not match its prototype.");
            }
            else
            {
                identityMatches = Catalog.IsPortraitLegal(data.PortraitId, character.Race, character.Gender);
                var source = new CharacterCreationSpecification
                {
                    Name = data.Name, BackgroundId = data.BackgroundId, PortraitId = data.PortraitId, Age = data.Age,
                    Race = character.Race, Gender = character.Gender, AutoLevelScheme = data.AutoLevelScheme,
                };
                restored = new FinalizedCharacterSpecification(source, background, Catalog,
                    data.SpentCharacterPoints);
            }
            if (!identityMatches)
                throw new InvalidOperationException("The saved creation content does not match its source catalog.");
            Finalized = restored;
            OpeningDialogueStarted = data.OpeningDialogueStarted;
            BindAutoLevelProgression();
            ApplyPersistentModifiers(pc, background);
        }

        private void BindAutoLevelProgression()
        {
            CharacterProgressionService current = _world.Progression;
            if (ReferenceEquals(_autoLevelProgression, current)) return;
            if (_autoLevelProgression != null)
                _autoLevelProgression.LevelChanged -= OnLevelChanged;
            _autoLevelProgression = current;
            _autoLevelProgression.LevelChanged += OnLevelChanged;
        }

        private void OnLevelChanged(ArcanumObjectId identity)
        {
            if (Finalized == null || identity != ProductionPlayerLifecycle.DefaultPlayerIdentity
                || Finalized.AutoLevelScheme == 0
                || !Catalog.TryGetAutoLevelScheme(Finalized.AutoLevelScheme,
                    out AutoLevelSchemeDefinition scheme)
                || !scheme.HasRule) return;
            ApplyAutoLevelRule(identity, scheme.Rule);
        }

        internal bool ApplyAutoLevelRule(ArcanumObjectId identity, string rule)
        {
            if (string.IsNullOrWhiteSpace(rule)) return false;
            foreach (string source in rule.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = source.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int target) || target < 0
                    || !ApplyAutoLevelTarget(identity, parts[0], target)) return false;
            }
            return true;
        }

        private bool ApplyAutoLevelTarget(ArcanumObjectId identity, string sourceToken, int target)
        {
            string token = sourceToken.ToLowerInvariant();
            CharacterAttribute? attribute = token switch
            {
                "st" => CharacterAttribute.Strength,
                "dx" => CharacterAttribute.Dexterity,
                "cn" => CharacterAttribute.Constitution,
                "be" => CharacterAttribute.Beauty,
                "in" => CharacterAttribute.Intelligence,
                "pe" => CharacterAttribute.Perception,
                "wp" => CharacterAttribute.Willpower,
                "ch" => CharacterAttribute.Charisma,
                _ => null,
            };
            if (attribute.HasValue) return RaiseAttribute(identity, attribute.Value, target);

            CharacterSkill? skill = token switch
            {
                "bow" => CharacterSkill.Bow,
                "dodge" => CharacterSkill.Dodge,
                "melee" => CharacterSkill.Melee,
                "throwing" => CharacterSkill.Throwing,
                "backstab" => CharacterSkill.Backstab,
                "pickpocket" => CharacterSkill.PickPocket,
                "prowling" => CharacterSkill.Prowling,
                "spottrap" => CharacterSkill.SpotTrap,
                "gambling" => CharacterSkill.Gambling,
                "haggle" => CharacterSkill.Haggle,
                "heal" => CharacterSkill.Heal,
                "persuasion" => CharacterSkill.Persuasion,
                "repair" => CharacterSkill.Repair,
                "firearms" => CharacterSkill.Firearms,
                "picklock" => CharacterSkill.PickLocks,
                "armtrap" => CharacterSkill.DisarmTraps,
                _ => null,
            };
            if (skill.HasValue) return RaiseSkill(identity, skill.Value, target);

            SpellCollege? college = token switch
            {
                "conveyance" => SpellCollege.Conveyance,
                "divination" => SpellCollege.Divination,
                "air" => SpellCollege.Air,
                "earth" => SpellCollege.Earth,
                "fire" => SpellCollege.Fire,
                "water" => SpellCollege.Water,
                "force" => SpellCollege.Force,
                "mental" => SpellCollege.Mental,
                "meta" => SpellCollege.Meta,
                "morph" => SpellCollege.Morph,
                "nature" => SpellCollege.Nature,
                "necro_evil" => SpellCollege.NecromanticBlack,
                "necro_good" => SpellCollege.NecromanticWhite,
                "phantasm" => SpellCollege.Phantasm,
                "summoning" => SpellCollege.Summoning,
                "temporal" => SpellCollege.Temporal,
                _ => null,
            };
            if (college.HasValue) return RaiseSpellCollege(identity, college.Value, target);

            TechnologyDiscipline? technology = token switch
            {
                "anatomical" => TechnologyDiscipline.Herbology,
                "chemistry" => TechnologyDiscipline.Chemistry,
                "electric" => TechnologyDiscipline.Electrical,
                "explosives" => TechnologyDiscipline.Explosives,
                "gun_smithy" => TechnologyDiscipline.GunSmithy,
                "mechanical" => TechnologyDiscipline.Mechanical,
                "smithy" => TechnologyDiscipline.Smithy,
                "therapeutics" => TechnologyDiscipline.Therapeutics,
                _ => null,
            };
            if (technology.HasValue) return RaiseTechnology(identity, technology.Value, target);
            if (token == "maxhps") return RaiseVitality(identity, true, target);
            if (token == "maxfatigue") return RaiseVitality(identity, false, target);
            return false;
        }

        private bool RaiseAttribute(ArcanumObjectId identity, CharacterAttribute attribute, int target)
        {
            while (_world.Characters.GetBaseAttribute(identity, attribute) < target)
            {
                int current = _world.Characters.GetBaseAttribute(identity, attribute);
                if (_world.Progression.AdjustAttribute(identity, attribute, 1, current)
                    != RuntimeCharacterAdjustmentResult.Success) return false;
            }
            return true;
        }

        private bool RaiseSkill(ArcanumObjectId identity, CharacterSkill skill, int target)
        {
            while (_world.Progression.GetEffectiveSkillRank(identity, skill) < target)
            {
                SkillIncreaseResult result = _world.Progression.IncreaseSkill(identity, skill);
                if (result == SkillIncreaseResult.Success) continue;
                if (result != SkillIncreaseResult.GoverningAttributeTooLow
                    || !RaiseAttribute(identity, CharacterSkillRules.GoverningAttribute(skill),
                        _world.Characters.GetBaseAttribute(identity,
                            CharacterSkillRules.GoverningAttribute(skill)) + 1)) return false;
            }
            return true;
        }

        private bool RaiseSpellCollege(ArcanumObjectId identity, SpellCollege college, int target)
        {
            target = Math.Min(5, target);
            while (_world.Magic.GetKnownCollegeRank(identity, college) < target)
            {
                int next = _world.Magic.GetKnownCollegeRank(identity, college) + 1;
                if (_world.Progression.GetUnspentCharacterPoints(identity) < 1
                    || _world.Progression.GetLevel(identity) < CharacterCreationRules.SpellLevelRequirement(next))
                    return false;
                if (_world.Characters.GetEffectiveAttribute(identity, CharacterAttribute.Intelligence) < 5)
                {
                    if (!RaiseAttribute(identity, CharacterAttribute.Intelligence,
                            _world.Characters.GetBaseAttribute(identity, CharacterAttribute.Intelligence) + 1))
                        return false;
                    continue;
                }
                if (_world.Characters.GetEffectiveAttribute(identity, CharacterAttribute.Willpower)
                    < CharacterCreationRules.SpellWillpowerRequirement(next))
                {
                    if (!RaiseAttribute(identity, CharacterAttribute.Willpower,
                            _world.Characters.GetBaseAttribute(identity, CharacterAttribute.Willpower) + 1))
                        return false;
                    continue;
                }
                _world.Magic.SetKnownCollegeRank(identity, college, next);
                _world.Progression.SpendCharacterPoint(identity);
                _world.DerivedStats.AddMagickPoint(identity);
            }
            return true;
        }

        private bool RaiseTechnology(ArcanumObjectId identity, TechnologyDiscipline discipline, int target)
        {
            target = Math.Min(7, target);
            while ((int)_world.Technology.GetLearnedDegree(identity, discipline) < target)
            {
                TechnologyLearningResult result = _world.Technology.LearnNextDegree(identity, discipline);
                if (result.Succeeded) continue;
                if (result.Failure != TechnologyLearningFailure.InsufficientIntelligence
                    || !RaiseAttribute(identity, CharacterAttribute.Intelligence,
                        _world.Characters.GetBaseAttribute(identity, CharacterAttribute.Intelligence) + 1))
                    return false;
            }
            return true;
        }

        private bool RaiseVitality(ArcanumObjectId identity, bool hitPoints, int target)
        {
            Func<ArcanumObjectId, int> current = hitPoints
                ? _world.Vitality.GetMaximumHitPoints
                : _world.Vitality.GetMaximumFatigue;
            while (current(identity) < target)
            {
                if (_world.Progression.GetUnspentCharacterPoints(identity) < 1) return false;
                _world.Vitality.AdjustPurchasedPoint(identity, hitPoints, 1);
                _world.Progression.SpendCharacterPoint(identity);
            }
            return true;
        }

        private bool TryStartOpeningDialogue()
        {
            if (OpeningDialogueStarted || Finalized == null || _world.PlayerState == null
                || !string.Equals(_world.SelectedSector, Finalized.StartSector,
                    StringComparison.OrdinalIgnoreCase)) return false;
            PersistentObjectState virgil = _world.States.Values
                .Where(value => value.Type == ObjectType.Npc && value.DialogNum == 1324 && !value.Off
                                && value.Placement.Kind == ObjectPlacementKind.World
                                && string.Equals(value.Placement.Sector, Finalized.StartSector,
                                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(value => value.Identity.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (virgil == null) return false;
            DialogueStartStatus status = _world.Dialogue.Start(_world.PlayerState.Identity, virgil.Identity);
            if (status != DialogueStartStatus.Started) return false;
            OpeningDialogueStarted = true;
            return true;
        }

        /// <summary>
        /// Resolves a retail pregenerated PC from a loose override when one exists, otherwise reconstructs the
        /// engine-owned basic prototype created by <c>proto.c</c>. The shipped pregenerated PCs are basic prototypes,
        /// not loose <c>data/proto</c> overrides, so treating a missing loose file as missing source data is incorrect.
        /// </summary>
        internal ObjectProtoInfo ResolvePregeneratedPrototype(PregeneratedCharacterDefinition selected)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            ObjectProtoInfo prototype = _world.ResolvePrototype(selected.PrototypeNumber);
            if (prototype != null) return prototype;

            return ObjectProtoInfo.CreateEngineOwnedPc(selected.PrototypeNumber,
                CharacterCreationRules.BuildPlayerArt(selected.Race, selected.Gender), selected.PortraitId,
                (int)selected.Gender, (int)selected.Race, CharacterCreationRules.SourceStartingAge);
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

        /// <summary>Candidate stat projection after the currently selected source background effect.</summary>
        public int GetEffectiveAttribute(CharacterCreationSpecification source, CharacterAttribute attribute)
        {
            if (source == null) return 0;
            if (Catalog == null || !Catalog.TryGetBackground(source.BackgroundId,
                    out BackgroundDefinition background)) return source.GetAttribute(attribute);
            return EffectiveAttribute(source, background, attribute);
        }

        public CharacterCreationInitialStats ProjectInitialStats(CharacterCreationSpecification source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var attributes = new int[CharacterAttributeSet.Count];
            foreach (CharacterAttribute attribute in CharacterStatService.AllAttributes)
                attributes[(int)attribute] = GetEffectiveAttribute(source, attribute);
            Catalog.TryGetBackground(source.BackgroundId, out BackgroundDefinition background);
            int magickPoints = source.CopySpells().Sum()
                                + (background?.MagicPointModifier ?? 0);
            int technologyPoints = source.CopyTechnology().Sum()
                                   + (background?.TechnologyPointModifier ?? 0);
            return new CharacterCreationInitialStats(attributes, source.Race,
                background?.ResistanceModifiers, magickPoints, technologyPoints);
        }

        public CharacterCreationInitialStats ProjectPregeneratedInitialStats(
            PregeneratedCharacterDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ObjectProtoInfo prototype = ResolvePregeneratedPrototype(source);
            if (prototype?.StatBase == null || prototype.StatBase.Length < CharacterAttributeSet.Count)
                throw new InvalidOperationException($"Retail PC prototype {source.PrototypeNumber} is unavailable or incomplete.");
            BackgroundDefinition background = null;
            if (Catalog != null) Catalog.TryGetBackground(source.BackgroundId, out background);
            var attributes = new int[CharacterAttributeSet.Count];
            foreach (CharacterAttribute attribute in CharacterStatService.AllAttributes)
            {
                int value = prototype.StatBase[(int)attribute]
                            + CharacterAttributeSet.SourceAdjustment(source.Race, source.Gender, attribute)
                            + (background?.AttributeModifiers.GetValueOrDefault(attribute) ?? 0);
                attributes[(int)attribute] = Math.Max(CharacterAttributeSet.SourceMinimum,
                    Math.Min(CharacterAttributeSet.SourceMaximumFor(source.Race, attribute), value));
            }
            var resistances = new Dictionary<CharacterResistance, int>();
            if (prototype.Resistances != null)
                foreach (CharacterResistance resistance in CharacterDerivedStatRules.AllResistances)
                    if ((int)resistance < prototype.Resistances.Length)
                        resistances[resistance] = prototype.Resistances[(int)resistance];
            if (background != null)
                foreach (var pair in background.ResistanceModifiers)
                    resistances[pair.Key] = resistances.GetValueOrDefault(pair.Key) + pair.Value;
            int magickPoints = prototype.StatBase.Length > CharacterDerivedStatRules.MagickPointsSourceSlot
                ? prototype.StatBase[CharacterDerivedStatRules.MagickPointsSourceSlot] : 0;
            int technologyPoints = prototype.StatBase.Length > CharacterDerivedStatRules.TechPointsSourceSlot
                ? prototype.StatBase[CharacterDerivedStatRules.TechPointsSourceSlot] : 0;
            return new CharacterCreationInitialStats(attributes, source.Race, resistances,
                magickPoints, technologyPoints);
        }

        public int GetEffectiveSkillRank(CharacterCreationSpecification source, CharacterSkill skill)
        {
            if (source == null) return 0;
            int modifier = Catalog != null && Catalog.TryGetBackground(source.BackgroundId,
                out BackgroundDefinition background) ? background.SkillModifiers.GetValueOrDefault(skill) : 0;
            return Math.Max(0, checked(source.GetSkillPoints(skill) * CharacterSkillRules.SkillUnitsPerPoint
                                       + modifier));
        }

        /// <summary>Creation-candidate projection of source <c>tech_degree_level_get</c>.</summary>
        public int GetEffectiveTechnologyLevel(CharacterCreationSpecification source,
            TechnologyDiscipline discipline)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return PhaseOneTechnologyCatalog.EffectiveLevel(
                (TechnologyDegree)source.GetTechnologyRank(discipline));
        }

        private static CharacterCreationValidationResult Fail(CharacterCreationFailure failure, string message,
            int spent = 0) => new(failure, message, spent,
            Math.Max(0, CharacterCreationRules.StartingCharacterPoints - spent));
    }
}
