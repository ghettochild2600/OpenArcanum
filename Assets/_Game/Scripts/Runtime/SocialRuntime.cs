using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Social
{
    public readonly struct ReputationId : IEquatable<ReputationId>, IComparable<ReputationId>
    {
        public const int Minimum = 1000;
        public const int Maximum = 1999;
        public int Value { get; }

        public ReputationId(int value)
        {
            if (value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"Source reputation ids are {Minimum}..{Maximum}.");
            Value = value;
        }

        public int CompareTo(ReputationId other) => Value.CompareTo(other.Value);
        public bool Equals(ReputationId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ReputationId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
        public static bool operator ==(ReputationId left, ReputationId right) => left.Equals(right);
        public static bool operator !=(ReputationId left, ReputationId right) => !left.Equals(right);
    }

    public readonly struct ReputationEffect
    {
        public int ReactionAdjustment { get; }
        public int Origin { get; }
        public int Faction { get; }

        internal ReputationEffect(int reactionAdjustment, int origin, int faction)
        {
            ReactionAdjustment = reactionAdjustment;
            Origin = origin;
            Faction = faction;
        }

        public bool AppliesTo(int origin, int faction)
            => (Origin == 0 || Origin == origin) && (Faction == 0 || Faction == faction);
    }

    public sealed class ReputationDefinition
    {
        public ReputationId Id { get; }
        public IReadOnlyList<int> Factions { get; }
        public IReadOnlyList<ReputationEffect> Effects { get; }

        internal ReputationDefinition(ReputationId id, int[] factions, ReputationEffect[] effects)
        {
            Id = id;
            Factions = Array.AsReadOnly(factions);
            Effects = Array.AsReadOnly(effects);
        }

        public bool GrantsFaction(int faction)
            => faction != 0 && Factions.Contains(faction);
    }

    /// <summary>Source <c>rules/gamerep.mes</c> metadata.</summary>
    public sealed class ReputationCatalog
    {
        private readonly Dictionary<ReputationId, ReputationDefinition> _definitions;
        public IReadOnlyDictionary<ReputationId, ReputationDefinition> Definitions => _definitions;

        private ReputationCatalog(Dictionary<ReputationId, ReputationDefinition> definitions)
            => _definitions = definitions;

        public bool TryGet(ReputationId id, out ReputationDefinition definition)
            => _definitions.TryGetValue(id, out definition);

        public static ReputationCatalog FromMes(MesFile source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var definitions = new Dictionary<ReputationId, ReputationDefinition>();
            foreach (KeyValuePair<int, string> row in source.Entries)
            {
                if (row.Key < ReputationId.Minimum || row.Key > ReputationId.Maximum
                    || string.IsNullOrWhiteSpace(row.Value)) continue;
                string[] groups = row.Value.Split(',');
                int[] factions = Integers(groups[0]);
                if (factions.Length != 3)
                    throw new FormatException($"Reputation {row.Key} must declare exactly three factions.");
                var effects = new List<ReputationEffect>(Math.Min(5, Math.Max(0, groups.Length - 1)));
                for (int index = 1; index < groups.Length; index++)
                {
                    if (effects.Count == 5)
                        throw new FormatException($"Reputation {row.Key} declares more than five effects.");
                    int[] values = Integers(groups[index]);
                    if (values.Length != 3)
                        throw new FormatException($"Reputation {row.Key} has an incomplete effect.");
                    effects.Add(new ReputationEffect(values[0], values[1], values[2]));
                }
                var id = new ReputationId(row.Key);
                definitions[id] = new ReputationDefinition(id, factions, effects.ToArray());
            }
            return new ReputationCatalog(definitions);
        }

        private static int[] Integers(string value)
            => (value ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(token => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int number) ? (int?)number : null)
                .Where(number => number.HasValue).Select(number => number.Value).ToArray();
    }

    public readonly struct SocialAiParameters
    {
        public int ReactionAttackThreshold { get; }
        public int AlignmentDifferenceThreshold { get; }

        internal SocialAiParameters(int reactionAttackThreshold, int alignmentDifferenceThreshold)
        {
            ReactionAttackThreshold = reactionAttackThreshold;
            AlignmentDifferenceThreshold = alignmentDifferenceThreshold;
        }
    }

    /// <summary>The two source AI packet fields which decide PC social KOS.</summary>
    public sealed class SocialAiCatalog
    {
        private readonly Dictionary<int, SocialAiParameters> _parameters;
        private SocialAiCatalog(Dictionary<int, SocialAiParameters> parameters) => _parameters = parameters;
        public bool TryGet(int id, out SocialAiParameters parameters) => _parameters.TryGetValue(id, out parameters);

        public static SocialAiCatalog FromMes(params MesFile[] sources)
        {
            var result = new Dictionary<int, SocialAiParameters>();
            foreach (MesFile source in sources)
            {
                if (source == null) continue;
                foreach (KeyValuePair<int, string> row in source.Entries)
                {
                    int[] values = Integers(row.Value);
                    if (values.Length < 12 || row.Key < 0 || row.Key >= 150) continue;
                    result[row.Key] = new SocialAiParameters(values[10], values[11]);
                }
            }
            return new SocialAiCatalog(result);
        }

        private static int[] Integers(string value)
            => (value ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(token => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int number) ? (int?)number : null)
                .Where(number => number.HasValue).Select(number => number.Value).ToArray();
    }

    public enum ReactionDisposition
    {
        Love,
        Amiable,
        Courteous,
        Neutral,
        Suspicious,
        Dislike,
        Hatred,
    }

    public readonly struct SocialReactionBreakdown
    {
        public int SourceBase { get; }
        public int BeautyModifier { get; }
        public int RaceModifier { get; }
        public int PersistentModifier { get; }
        public int ReputationModifier { get; }
        public int EffectiveReaction { get; }

        internal SocialReactionBreakdown(int sourceBase, int beautyModifier, int raceModifier,
            int persistentModifier, int reputationModifier)
        {
            SourceBase = sourceBase;
            BeautyModifier = beautyModifier;
            RaceModifier = raceModifier;
            PersistentModifier = persistentModifier;
            ReputationModifier = reputationModifier;
            EffectiveReaction = checked(sourceBase + beautyModifier + raceModifier
                                        + persistentModifier + reputationModifier);
        }
    }

    public enum SocialCrimeType { Theft, Assault, Murder }
    public enum SocialCrimeFailure
    {
        None, InvalidOffender, InvalidVictim, InvalidWitness, InvalidItem, LegalAction, Undetected,
    }

    public readonly struct SocialCrimeResult
    {
        public SocialCrimeFailure Failure { get; }
        public SocialCrimeType Type { get; }
        public ArcanumObjectId Witness { get; }
        public bool HostilityChanged { get; }
        public bool Succeeded => Failure == SocialCrimeFailure.None;

        internal SocialCrimeResult(SocialCrimeFailure failure, SocialCrimeType type,
            ArcanumObjectId witness = default, bool hostilityChanged = false)
        {
            Failure = failure;
            Type = type;
            Witness = witness;
            HostilityChanged = hostilityChanged;
        }
    }

    /// <summary>Persistent M11B social authority layered over M4 character-derived reaction.</summary>
    public sealed class SocialStateService
    {
        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, Dictionary<ReputationId, long>> _reputations = new();
        private readonly HashSet<HostilityKey> _hostilities = new();
        private ReputationCatalog _reputationSource;
        private SocialAiCatalog _aiSource;

        internal SocialStateService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public void BindSources(ReputationCatalog reputations, SocialAiCatalog ai)
        {
            _reputationSource = reputations ?? throw new ArgumentNullException(nameof(reputations));
            _aiSource = ai ?? throw new ArgumentNullException(nameof(ai));
        }

        public bool HasReputation(ArcanumObjectId pc, ReputationId id)
            => _reputations.TryGetValue(pc, out Dictionary<ReputationId, long> values)
               && values.ContainsKey(id);

        public bool AddReputation(ArcanumObjectId pc, ReputationId id)
        {
            RequirePc(pc);
            if (!_reputations.TryGetValue(pc, out Dictionary<ReputationId, long> values))
                _reputations.Add(pc, values = new Dictionary<ReputationId, long>());
            if (values.ContainsKey(id)) return false;
            values.Add(id, _world.SourceTime.ElapsedMilliseconds);
            return true;
        }

        public bool RemoveReputation(ArcanumObjectId pc, ReputationId id)
        {
            RequirePc(pc);
            if (!_reputations.TryGetValue(pc, out Dictionary<ReputationId, long> values)
                || !values.Remove(id)) return false;
            if (values.Count == 0) _reputations.Remove(pc);
            return true;
        }

        public IReadOnlyList<ReputationId> GetReputations(ArcanumObjectId pc)
            => _reputations.TryGetValue(pc, out Dictionary<ReputationId, long> values)
                ? values.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key.Value).Select(pair => pair.Key).ToArray()
                : Array.Empty<ReputationId>();

        public SocialReactionBreakdown GetReactionBreakdown(ArcanumObjectId npc, ArcanumObjectId pc)
        {
            var inputs = _world.DerivedStats.GetReactionInputs(npc, pc);
            int reputation = GetReputationReactionAdjustment(pc, npc);
            return new SocialReactionBreakdown(inputs.SourceBase, inputs.BeautyModifier, inputs.RaceModifier,
                _world.DerivedStats.GetReactionAdjustment(npc, pc), reputation);
        }

        public int GetReaction(ArcanumObjectId npc, ArcanumObjectId pc)
            => GetReactionBreakdown(npc, pc).EffectiveReaction;

        public int AdjustReaction(ArcanumObjectId npc, ArcanumObjectId pc, int delta)
        {
            _world.DerivedStats.AdjustReaction(npc, pc, delta);
            return GetReaction(npc, pc);
        }

        public int SetReaction(ArcanumObjectId npc, ArcanumObjectId pc, int value)
        {
            int reputation = GetReputationReactionAdjustment(pc, npc);
            _world.DerivedStats.SetReaction(npc, pc, checked(value - reputation));
            return value;
        }

        public static ReactionDisposition TranslateReaction(int value)
        {
            if (value <= 0) return ReactionDisposition.Hatred;
            if (value <= 20) return ReactionDisposition.Dislike;
            if (value <= 40) return ReactionDisposition.Suspicious;
            if (value <= 60) return ReactionDisposition.Neutral;
            if (value <= 80) return ReactionDisposition.Courteous;
            if (value <= 100) return ReactionDisposition.Amiable;
            return ReactionDisposition.Love;
        }

        public int GetFaction(ArcanumObjectId identity)
            => _world.TryGetObjectState(identity, out PersistentObjectState state)
                && state.Type == ObjectType.Npc ? state.Faction : 0;

        public bool ReputationGrantsFaction(ArcanumObjectId pc, int faction)
        {
            if (faction == 0 || _reputationSource == null
                || !_reputations.TryGetValue(pc, out Dictionary<ReputationId, long> values)) return false;
            foreach (ReputationId id in values.Keys)
                if (_reputationSource.TryGet(id, out ReputationDefinition definition)
                    && definition.GrantsFaction(faction)) return true;
            return false;
        }

        public bool AreAllies(ArcanumObjectId left, ArcanumObjectId right)
        {
            if (left.IsNull || right.IsNull) return false;
            if (left == right) return true;
            bool leftPc = IsPc(left);
            bool rightPc = IsPc(right);
            bool leftFollower = _world.Party.IsMember(left);
            bool rightFollower = _world.Party.IsMember(right);
            if (leftFollower || rightFollower)
                return (leftPc || leftFollower) && (rightPc || rightFollower);
            if (leftPc && rightPc) return false;
            if (leftPc || rightPc)
            {
                ArcanumObjectId pc = leftPc ? left : right;
                ArcanumObjectId npc = leftPc ? right : left;
                return ReputationGrantsFaction(pc, GetFaction(npc));
            }
            int faction = GetFaction(left);
            return faction != 0 && faction == GetFaction(right);
        }

        public bool IsRememberedHostile(ArcanumObjectId source, ArcanumObjectId target)
            => !AreAllies(source, target) && _hostilities.Contains(new HostilityKey(source, target));

        public bool SetHostile(ArcanumObjectId source, ArcanumObjectId target, bool hostile = true)
        {
            if (source.IsNull || target.IsNull || source == target)
                throw new ArgumentException("Hostility requires distinct persistent identities.");
            var key = new HostilityKey(source, target);
            return hostile ? _hostilities.Add(key) : _hostilities.Remove(key);
        }

        public bool IsReactionHostile(ArcanumObjectId npc, ArcanumObjectId pc)
        {
            if (_aiSource == null || AreAllies(npc, pc)
                || !_world.TryGetObjectState(npc, out PersistentObjectState state)
                || state.Type != ObjectType.Npc || !_aiSource.TryGet(state.AiData, out SocialAiParameters ai))
                return false;
            return GetReaction(npc, pc) <= ai.ReactionAttackThreshold;
        }

        public bool IsAlignmentHostile(ArcanumObjectId npc, ArcanumObjectId target)
        {
            if (_aiSource == null || AreAllies(npc, target)
                || !_world.TryGetObjectState(npc, out PersistentObjectState state)
                || state.Type != ObjectType.Npc || !_aiSource.TryGet(state.AiData, out SocialAiParameters ai)
                || !_world.DerivedStats.TryGet(npc, out _) || !_world.DerivedStats.TryGet(target, out _))
                return false;
            return Math.Abs(_world.DerivedStats.GetAlignment(npc) - _world.DerivedStats.GetAlignment(target))
                   >= ai.AlignmentDifferenceThreshold;
        }

        /// <summary>
        /// Applies only the representable crime boundary. Detection is supplied by the future stealth/perception
        /// authority; M11B never fabricates LOS or awareness.
        /// </summary>
        public SocialCrimeResult ReportTheft(ArcanumObjectId offender, ArcanumObjectId victim,
            ArcanumObjectId item, ArcanumObjectId witness, bool detected)
        {
            if (!IsPcOrParty(offender)) return new SocialCrimeResult(SocialCrimeFailure.InvalidOffender,
                SocialCrimeType.Theft);
            if (!_world.TryGetObjectState(victim, out PersistentObjectState victimState)
                || victimState.Type != ObjectType.Npc)
                return new SocialCrimeResult(SocialCrimeFailure.InvalidVictim, SocialCrimeType.Theft);
            if (!_world.TryGetObjectState(item, out PersistentObjectState itemState))
                return new SocialCrimeResult(SocialCrimeFailure.InvalidItem, SocialCrimeType.Theft);
            if (itemState.ParentIdentity != victim)
                return new SocialCrimeResult(SocialCrimeFailure.LegalAction, SocialCrimeType.Theft);
            if (!detected)
                return new SocialCrimeResult(SocialCrimeFailure.Undetected, SocialCrimeType.Theft);
            if (!_world.TryGetObjectState(witness, out PersistentObjectState witnessState)
                || witnessState.Type != ObjectType.Npc || _world.Party.IsMember(witness)
                || (_world.Vitality.TryGet(witness, out _) && (_world.Vitality.IsDead(witness)
                                                              || _world.Vitality.IsUnconscious(witness))))
                return new SocialCrimeResult(SocialCrimeFailure.InvalidWitness, SocialCrimeType.Theft);
            ArcanumObjectId pc = _world.PlayerState.Identity;
            bool changed = SetHostile(witness, pc);
            return new SocialCrimeResult(SocialCrimeFailure.None, SocialCrimeType.Theft, witness, changed);
        }

        public SocialSaveData ExportSaveData()
        {
            var reputations = new List<SocialReputationSaveData>();
            foreach (var pc in _reputations.OrderBy(pair => pair.Key.Key, StringComparer.Ordinal))
                foreach (var value in pc.Value.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key.Value))
                    reputations.Add(new SocialReputationSaveData
                    {
                        PcIdentity = pc.Key.Key,
                        ReputationId = value.Key.Value,
                        AcquiredAtMilliseconds = value.Value,
                    });
            return new SocialSaveData
            {
                Reputations = reputations,
                Hostilities = _hostilities.OrderBy(value => value.Source.Key, StringComparer.Ordinal)
                    .ThenBy(value => value.Target.Key, StringComparer.Ordinal)
                    .Select(value => new SocialHostilitySaveData
                    { SourceIdentity = value.Source.Key, TargetIdentity = value.Target.Key }).ToList(),
            };
        }

        internal void RestoreSaveData(SocialSaveData data)
        {
            if (data == null) return;
            foreach (SocialReputationSaveData value in data.Reputations)
            {
                ArcanumObjectId.TryParsePersistent(value.PcIdentity, out ArcanumObjectId pc);
                var id = new ReputationId(value.ReputationId);
                if (!_reputations.TryGetValue(pc, out Dictionary<ReputationId, long> entries))
                    _reputations.Add(pc, entries = new Dictionary<ReputationId, long>());
                entries.Add(id, value.AcquiredAtMilliseconds);
            }
            foreach (SocialHostilitySaveData value in data.Hostilities)
            {
                ArcanumObjectId.TryParsePersistent(value.SourceIdentity, out ArcanumObjectId source);
                ArcanumObjectId.TryParsePersistent(value.TargetIdentity, out ArcanumObjectId target);
                _hostilities.Add(new HostilityKey(source, target));
            }
        }

        internal Snapshot CaptureSnapshot() => new(this);
        internal void RestoreSnapshot(Snapshot snapshot) => snapshot.Restore(this);

        internal sealed class Snapshot
        {
            private readonly Dictionary<ArcanumObjectId, Dictionary<ReputationId, long>> _reputations = new();
            private readonly HashSet<HostilityKey> _hostilities;

            internal Snapshot(SocialStateService source)
            {
                foreach (var pair in source._reputations)
                    _reputations.Add(pair.Key, new Dictionary<ReputationId, long>(pair.Value));
                _hostilities = new HashSet<HostilityKey>(source._hostilities);
            }

            internal void Restore(SocialStateService target)
            {
                target._reputations.Clear();
                foreach (var pair in _reputations)
                    target._reputations.Add(pair.Key, new Dictionary<ReputationId, long>(pair.Value));
                target._hostilities.Clear();
                foreach (HostilityKey value in _hostilities) target._hostilities.Add(value);
            }
        }

        private int GetReputationReactionAdjustment(ArcanumObjectId pc, ArcanumObjectId npc)
        {
            if (_reputationSource == null || !_reputations.TryGetValue(pc,
                    out Dictionary<ReputationId, long> reputations)
                || !_world.TryGetObjectState(npc, out PersistentObjectState state)) return 0;
            int value = 0;
            foreach (ReputationId id in reputations.Keys)
                if (_reputationSource.TryGet(id, out ReputationDefinition definition))
                    foreach (ReputationEffect effect in definition.Effects)
                        if (effect.AppliesTo(state.Origin, state.Faction))
                            value = checked(value + effect.ReactionAdjustment);
            return value;
        }

        private bool IsPc(ArcanumObjectId identity)
            => _world.PlayerState?.Identity == identity
               || _world.Characters.TryGet(identity, out var state) && state.ObjectType == ObjectType.Pc;

        private bool IsPcOrParty(ArcanumObjectId identity)
            => IsPc(identity) || _world.Party.IsMember(identity);

        private void RequirePc(ArcanumObjectId identity)
        {
            if (!IsPc(identity)) throw new ArgumentException("Reputations belong only to PCs.", nameof(identity));
        }

        private readonly struct HostilityKey : IEquatable<HostilityKey>
        {
            public ArcanumObjectId Source { get; }
            public ArcanumObjectId Target { get; }
            public HostilityKey(ArcanumObjectId source, ArcanumObjectId target)
            { Source = source; Target = target; }
            public bool Equals(HostilityKey other) => Source == other.Source && Target == other.Target;
            public override bool Equals(object obj) => obj is HostilityKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Source, Target);
        }
    }
}
