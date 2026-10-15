using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Save;

namespace Arcanum.Runtime.World
{
    public enum ScriptEffectCause
    {
        Race = 0,
        Background = 1,
        Class = 2,
        Blessing = 3,
        Curse = 4,
        Item = 5,
        Spell = 6,
        Injury = 7,
        Technology = 8,
        Gender = 9,
    }

    public sealed class RetailScriptEffectChange
    {
        public string SourceSymbol { get; }
        public CharacterContributorTarget Target { get; }
        public CharacterContributorOperation Operation { get; }
        public int Amount { get; }

        internal RetailScriptEffectChange(string sourceSymbol, CharacterContributorTarget target,
            CharacterContributorOperation operation, int amount)
        {
            SourceSymbol = sourceSymbol;
            Target = target;
            Operation = operation;
            Amount = amount;
        }
    }

    public sealed class RetailScriptEffectDefinition
    {
        public int Id { get; }
        public string SourceDefinition { get; }
        public IReadOnlyList<RetailScriptEffectChange> Changes { get; }
        public IReadOnlyList<string> UnsupportedChanges { get; }
        public bool IsFullySupported => UnsupportedChanges.Count == 0;

        internal RetailScriptEffectDefinition(int id, string sourceDefinition,
            IReadOnlyList<RetailScriptEffectChange> changes, IReadOnlyList<string> unsupportedChanges)
        {
            Id = id;
            SourceDefinition = sourceDefinition;
            Changes = changes;
            UnsupportedChanges = unsupportedChanges;
        }
    }

    /// <summary>Parsed retail rules/effect.mes and module rules/gameeffect.mes definitions.</summary>
    public sealed class RetailScriptEffectCatalog
    {
        private readonly Dictionary<int, RetailScriptEffectDefinition> _definitions;
        public IReadOnlyDictionary<int, RetailScriptEffectDefinition> Definitions => _definitions;

        private RetailScriptEffectCatalog(Dictionary<int, RetailScriptEffectDefinition> definitions)
            => _definitions = definitions;

        public bool TryGet(int id, out RetailScriptEffectDefinition definition)
            => _definitions.TryGetValue(id, out definition);

        public static RetailScriptEffectCatalog FromMes(MesFile engine, MesFile module = null)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            var definitions = new Dictionary<int, RetailScriptEffectDefinition>();
            if (module != null)
                foreach (KeyValuePair<int, string> entry in module.Entries.Where(value => value.Key is >= 0 and < 50))
                    definitions[entry.Key] = Parse(entry.Key, entry.Value);
            foreach (KeyValuePair<int, string> entry in engine.Entries.Where(value => value.Key is >= 50 and < 400))
                definitions[entry.Key] = Parse(entry.Key, entry.Value);
            return new RetailScriptEffectCatalog(definitions);
        }

        private static RetailScriptEffectDefinition Parse(int id, string source)
        {
            var changes = new List<RetailScriptEffectChange>();
            var unsupported = new List<string>();
            foreach (string raw in (source ?? string.Empty).Split(','))
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;
                int split = token.IndexOfAny(new[] { ' ', '\t' });
                string symbol = split < 0 ? token : token.Substring(0, split);
                string operation = split < 0 ? string.Empty : token.Substring(split).Trim();
                if (!TryTarget(symbol, out CharacterContributorTarget target)
                    || !TryOperation(operation, out CharacterContributorOperation parsed, out int amount))
                {
                    unsupported.Add(token);
                    continue;
                }
                changes.Add(new RetailScriptEffectChange(symbol, target, parsed, amount));
            }
            return new RetailScriptEffectDefinition(id, source ?? string.Empty,
                changes.AsReadOnly(), unsupported.AsReadOnly());
        }

        private static bool TryOperation(string source, out CharacterContributorOperation operation, out int amount)
        {
            operation = default;
            amount = 0;
            string value = source.Trim();
            if (value.StartsWith("min ", StringComparison.OrdinalIgnoreCase))
                return ParseAmount(value.Substring(4), CharacterContributorOperation.Minimum, out operation, out amount);
            if (value.StartsWith("max ", StringComparison.OrdinalIgnoreCase))
                return ParseAmount(value.Substring(4), CharacterContributorOperation.Maximum, out operation, out amount);
            if (value.EndsWith("%", StringComparison.Ordinal))
                return ParseAmount(value.Substring(0, value.Length - 1), CharacterContributorOperation.Percent,
                    out operation, out amount);
            if (value.StartsWith("*", StringComparison.Ordinal))
                return ParseAmount(value.Substring(1), CharacterContributorOperation.Multiply, out operation, out amount);
            if (value.StartsWith("/", StringComparison.Ordinal))
                return ParseAmount(value.Substring(1), CharacterContributorOperation.Divide, out operation, out amount)
                       && amount != 0;
            if (value.StartsWith("+", StringComparison.Ordinal) || value.StartsWith("-", StringComparison.Ordinal))
                return ParseAmount(value, CharacterContributorOperation.Add, out operation, out amount);
            return false;
        }

        private static bool ParseAmount(string source, CharacterContributorOperation value,
            out CharacterContributorOperation operation, out int amount)
        {
            operation = value;
            return int.TryParse(source.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }

        private static bool TryTarget(string source, out CharacterContributorTarget target)
        {
            string key = source.ToLowerInvariant();
            switch (key)
            {
                case "st": target = CharacterContributorTarget.Attribute(CharacterAttribute.Strength); return true;
                case "dx": target = CharacterContributorTarget.Attribute(CharacterAttribute.Dexterity); return true;
                case "cn": target = CharacterContributorTarget.Attribute(CharacterAttribute.Constitution); return true;
                case "be": target = CharacterContributorTarget.Attribute(CharacterAttribute.Beauty); return true;
                case "in": target = CharacterContributorTarget.Attribute(CharacterAttribute.Intelligence); return true;
                case "pe": target = CharacterContributorTarget.Attribute(CharacterAttribute.Perception); return true;
                case "wp": target = CharacterContributorTarget.Attribute(CharacterAttribute.Willpower); return true;
                case "ch": target = CharacterContributorTarget.Attribute(CharacterAttribute.Charisma); return true;
                case "carry": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.CarryWeight); return true;
                case "damage": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.MeleeDamageBonus); return true;
                case "ac": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.ArmorClassAdjustment); return true;
                case "speed": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.Speed); return true;
                case "healrate": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.HealRate); return true;
                case "poisonrate": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.PoisonRecovery); return true;
                case "beautyreaction": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.BeautyReactionModifier); return true;
                case "maxfollowers": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.MaximumFollowers); return true;
                case "aptitude": target = CharacterContributorTarget.DerivedStat(CharacterDerivedStat.MagickTechAptitude); return true;
                case "alignment": target = CharacterContributorTarget.Alignment; return true;
                case "magicpts": target = CharacterContributorTarget.MagickPoints; return true;
                case "techpts": target = CharacterContributorTarget.TechnologyPoints; return true;
                case "maxhps": target = CharacterContributorTarget.MaximumHitPoints; return true;
                case "maxfatigue": target = CharacterContributorTarget.MaximumFatigue; return true;
                case "reaction": target = CharacterContributorTarget.Reaction; return true;
                case "bow": target = CharacterContributorTarget.Skill(CharacterSkill.Bow); return true;
                case "dodge": target = CharacterContributorTarget.Skill(CharacterSkill.Dodge); return true;
                case "melee": target = CharacterContributorTarget.Skill(CharacterSkill.Melee); return true;
                case "throwing": target = CharacterContributorTarget.Skill(CharacterSkill.Throwing); return true;
                case "backstab": target = CharacterContributorTarget.Skill(CharacterSkill.Backstab); return true;
                case "pickpocket": target = CharacterContributorTarget.Skill(CharacterSkill.PickPocket); return true;
                case "prowling": target = CharacterContributorTarget.Skill(CharacterSkill.Prowling); return true;
                case "spottrap": target = CharacterContributorTarget.Skill(CharacterSkill.SpotTrap); return true;
                case "gambling": target = CharacterContributorTarget.Skill(CharacterSkill.Gambling); return true;
                case "haggle": target = CharacterContributorTarget.Skill(CharacterSkill.Haggle); return true;
                case "heal": target = CharacterContributorTarget.Skill(CharacterSkill.Heal); return true;
                case "persuasion": target = CharacterContributorTarget.Skill(CharacterSkill.Persuasion); return true;
                case "repair": target = CharacterContributorTarget.Skill(CharacterSkill.Repair); return true;
                case "firearms": target = CharacterContributorTarget.Skill(CharacterSkill.Firearms); return true;
                case "picklock": target = CharacterContributorTarget.Skill(CharacterSkill.PickLocks); return true;
                case "armtrap": target = CharacterContributorTarget.Skill(CharacterSkill.DisarmTraps); return true;
                case "resistdamage": target = CharacterContributorTarget.Resistance(CharacterResistance.Normal); return true;
                case "resistfire": target = CharacterContributorTarget.Resistance(CharacterResistance.Fire); return true;
                case "resistelectrical": target = CharacterContributorTarget.Resistance(CharacterResistance.Electrical); return true;
                case "resistpoison": target = CharacterContributorTarget.Resistance(CharacterResistance.Poison); return true;
                case "resistmagic": target = CharacterContributorTarget.Resistance(CharacterResistance.Magic); return true;
                default: target = default; return false;
            }
        }
    }

    public readonly struct ScriptEffectInstance
    {
        public long Sequence { get; }
        public ArcanumObjectId Target { get; }
        public int EffectId { get; }
        public int Cause { get; }

        internal ScriptEffectInstance(long sequence, ArcanumObjectId target, int effectId, int cause)
        {
            Sequence = sequence;
            Target = target;
            EffectId = effectId;
            Cause = cause;
        }
    }

    /// <summary>Persistent source OBJ_F_CRITTER_EFFECTS / OBJ_F_CRITTER_EFFECT_CAUSE authority.</summary>
    public sealed class ScriptEffectAuthority : IDisposable
    {
        private readonly WorldMapSessionCoordinator _session;
        private readonly List<ScriptEffectInstance> _effects = new();
        private RetailScriptEffectCatalog _catalog;
        private long _nextSequence = 1;

        internal ScriptEffectAuthority(WorldMapSessionCoordinator session, RetailScriptEffectCatalog catalog)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _catalog = catalog;
            _session.ObjectStateRemoved += OnObjectRemoved;
        }

        public IReadOnlyList<ScriptEffectInstance> Effects => _effects;

        internal void BindCatalog(RetailScriptEffectCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            foreach (ScriptEffectInstance effect in _effects) Apply(effect);
        }

        public bool Add(ArcanumObjectId target, int effectId, int cause)
        {
            if (!_session.Characters.TryGet(target, out _) || effectId is < 0 or >= 400) return false;
            var effect = new ScriptEffectInstance(_nextSequence++, target, effectId, cause);
            _effects.Add(effect);
            Apply(effect);
            return true;
        }

        public bool RemoveFirst(ArcanumObjectId target, int effectId)
        {
            int index = _effects.FindIndex(value => value.Target == target && value.EffectId == effectId);
            if (index < 0) return false;
            ScriptEffectInstance effect = _effects[index];
            _effects.RemoveAt(index);
            _session.Characters.Contributors.RemoveOwner(target, Owner(effect.Sequence));
            return true;
        }

        internal ScriptEffectSaveData ExportSaveData() => new()
        {
            NextSequence = _nextSequence,
            Effects = _effects.Select(value => new ScriptEffectInstanceSaveData
            {
                Sequence = value.Sequence,
                TargetIdentity = value.Target.Key,
                EffectId = value.EffectId,
                Cause = value.Cause,
            }).ToList(),
        };

        internal void RestoreSaveData(ScriptEffectSaveData data)
        {
            foreach (ScriptEffectInstance value in _effects)
                _session.Characters.Contributors.RemoveOwner(value.Target, Owner(value.Sequence));
            _effects.Clear();
            _nextSequence = Math.Max(1, data?.NextSequence ?? 1);
            if (data?.Effects == null) return;
            foreach (ScriptEffectInstanceSaveData value in data.Effects.OrderBy(value => value.Sequence))
            {
                ArcanumObjectId.TryParsePersistent(value.TargetIdentity, out ArcanumObjectId target);
                var effect = new ScriptEffectInstance(value.Sequence, target, value.EffectId, value.Cause);
                _effects.Add(effect);
                Apply(effect);
            }
        }

        public void Dispose() => _session.ObjectStateRemoved -= OnObjectRemoved;

        private void Apply(ScriptEffectInstance effect)
        {
            if (_catalog == null || !_catalog.TryGet(effect.EffectId, out RetailScriptEffectDefinition definition)
                || !_session.Characters.TryGet(effect.Target, out _)) return;
            string owner = Owner(effect.Sequence);
            CharacterContributorDomain domain = Domain(effect.Cause);
            _session.Characters.Contributors.SetOwnerContributors(effect.Target, owner,
                definition.Changes.Select((change, index) => new CharacterContributor(owner, domain,
                    effect.EffectId, $"{owner}:{index}:{change.SourceSymbol}", change.Target,
                    change.Operation, change.Amount, index)));
        }

        private void OnObjectRemoved(PersistentObjectState state, ObjectPlacement placement)
        {
            for (int index = _effects.Count - 1; index >= 0; index--)
            {
                if (_effects[index].Target != state.Identity) continue;
                _session.Characters.Contributors.RemoveOwner(state.Identity, Owner(_effects[index].Sequence));
                _effects.RemoveAt(index);
            }
        }

        private static string Owner(long sequence) => $"script-effect:{sequence}";

        private static CharacterContributorDomain Domain(int cause) => cause switch
        {
            (int)ScriptEffectCause.Race => CharacterContributorDomain.Race,
            (int)ScriptEffectCause.Background => CharacterContributorDomain.Background,
            (int)ScriptEffectCause.Gender => CharacterContributorDomain.Sex,
            (int)ScriptEffectCause.Spell => CharacterContributorDomain.Spell,
            (int)ScriptEffectCause.Technology => CharacterContributorDomain.Technology,
            (int)ScriptEffectCause.Injury => CharacterContributorDomain.Status,
            _ => CharacterContributorDomain.Other,
        };
    }
}
