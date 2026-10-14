using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.Character
{
    public enum CharacterContributorDomain
    {
        Base = 0,
        Race = 10,
        Sex = 20,
        Background = 30,
        Equipment = 40,
        Training = 50,
        Spell = 60,
        Technology = 70,
        Status = 80,
        Environment = 90,
        Time = 100,
        Other = 110,
    }

    public enum CharacterContributorTargetKind
    {
        Attribute,
        Skill,
        DerivedStat,
        Resistance,
        MagickPoints,
        TechnologyPoints,
    }

    /// <summary>Source effect operators in the order used by <c>effect_adjust_func</c>.</summary>
    public enum CharacterContributorOperation
    {
        Percent = 0,
        Multiply = 1,
        Divide = 2,
        Add = 3,
        Minimum = 4,
        Maximum = 5,
    }

    public readonly struct CharacterContributorTarget : IEquatable<CharacterContributorTarget>
    {
        public CharacterContributorTargetKind Kind { get; }
        public int SourceId { get; }

        private CharacterContributorTarget(CharacterContributorTargetKind kind, int sourceId)
        {
            Kind = kind;
            SourceId = sourceId;
        }

        public static CharacterContributorTarget Attribute(CharacterAttribute attribute)
        {
            CharacterAttributeRules.ValidateAttribute(attribute);
            return new CharacterContributorTarget(CharacterContributorTargetKind.Attribute, (int)attribute);
        }

        public static CharacterContributorTarget Skill(CharacterSkill skill)
        {
            CharacterSkillRules.ValidateSkill(skill);
            return new CharacterContributorTarget(CharacterContributorTargetKind.Skill, (int)skill);
        }

        public static CharacterContributorTarget DerivedStat(CharacterDerivedStat stat)
        {
            CharacterDerivedStatRules.ValidateStat(stat);
            return new CharacterContributorTarget(CharacterContributorTargetKind.DerivedStat, (int)stat);
        }

        public static CharacterContributorTarget Resistance(CharacterResistance resistance)
        {
            CharacterDerivedStatRules.ValidateResistance(resistance);
            return new CharacterContributorTarget(CharacterContributorTargetKind.Resistance, (int)resistance);
        }

        public static CharacterContributorTarget MagickPoints { get; } =
            new(CharacterContributorTargetKind.MagickPoints, CharacterDerivedStatRules.MagickPointsSourceSlot);

        public static CharacterContributorTarget TechnologyPoints { get; } =
            new(CharacterContributorTargetKind.TechnologyPoints, CharacterDerivedStatRules.TechPointsSourceSlot);

        public bool Equals(CharacterContributorTarget other) => Kind == other.Kind && SourceId == other.SourceId;
        public override bool Equals(object obj) => obj is CharacterContributorTarget other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, SourceId);
        public static bool operator ==(CharacterContributorTarget left, CharacterContributorTarget right)
            => left.Equals(right);
        public static bool operator !=(CharacterContributorTarget left, CharacterContributorTarget right)
            => !left.Equals(right);
        public override string ToString() => $"{Kind}:{SourceId}";
    }

    /// <summary>One inspectable source effect contribution owned by a gameplay domain.</summary>
    public sealed class CharacterContributor
    {
        public string Owner { get; }
        public CharacterContributorDomain Domain { get; }
        public int SourceId { get; }
        public string EffectIdentity { get; }
        public CharacterContributorTarget Target { get; }
        public CharacterContributorOperation Operation { get; }
        public int Amount { get; }
        public int SourceOrder { get; }

        public CharacterContributor(string owner, CharacterContributorDomain domain, int sourceId,
            string effectIdentity, CharacterContributorTarget target, CharacterContributorOperation operation,
            int amount, int sourceOrder = 0)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Contributor owner is required.", nameof(owner));
            if (string.IsNullOrWhiteSpace(effectIdentity))
                throw new ArgumentException("Contributor effect identity is required.", nameof(effectIdentity));
            if (!Enum.IsDefined(typeof(CharacterContributorDomain), domain))
                throw new ArgumentOutOfRangeException(nameof(domain));
            if (!Enum.IsDefined(typeof(CharacterContributorOperation), operation))
                throw new ArgumentOutOfRangeException(nameof(operation));
            if (operation == CharacterContributorOperation.Divide && amount == 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "A source effect cannot divide by zero.");
            Owner = owner;
            Domain = domain;
            SourceId = sourceId;
            EffectIdentity = effectIdentity;
            Target = target;
            Operation = operation;
            Amount = amount;
            SourceOrder = sourceOrder;
        }
    }

    /// <summary>
    /// Character-owned effect graph. Owners are replaced atomically so restoring or reapplying an effect is
    /// idempotent, while source operator ordering remains deterministic and inspectable.
    /// </summary>
    public sealed class CharacterContributorService
    {
        private readonly Dictionary<ArcanumObjectId, Dictionary<string, CharacterContributor[]>> _owners = new();

        public event Action<ArcanumObjectId> Changed;

        public IReadOnlyList<CharacterContributor> GetOrderedContributors(ArcanumObjectId identity)
        {
            if (!_owners.TryGetValue(identity, out Dictionary<string, CharacterContributor[]> owners))
                return Array.Empty<CharacterContributor>();
            return owners.Values.SelectMany(value => value)
                .OrderBy(value => (int)value.Domain)
                .ThenBy(value => value.SourceOrder)
                .ThenBy(value => value.SourceId)
                .ThenBy(value => value.Owner, StringComparer.Ordinal)
                .ThenBy(value => value.EffectIdentity, StringComparer.Ordinal)
                .ToArray();
        }

        public void SetOwnerContributors(ArcanumObjectId identity, string owner,
            IEnumerable<CharacterContributor> contributors)
        {
            if (!identity.IsPersistent)
                throw new ArgumentException("Contributor identity must be persistent.", nameof(identity));
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Contributor owner is required.", nameof(owner));
            CharacterContributor[] values = (contributors ?? throw new ArgumentNullException(nameof(contributors)))
                .ToArray();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterContributor value in values)
            {
                if (value == null) throw new ArgumentException("Contributor collections cannot contain null.", nameof(contributors));
                if (!string.Equals(value.Owner, owner, StringComparison.Ordinal))
                    throw new ArgumentException("Every contributor must name the owner being replaced.", nameof(contributors));
                if (!identities.Add(value.EffectIdentity))
                    throw new ArgumentException($"Contributor effect {value.EffectIdentity} is duplicated for owner {owner}.",
                        nameof(contributors));
            }

            if (!_owners.TryGetValue(identity, out Dictionary<string, CharacterContributor[]> owners))
            {
                if (values.Length == 0) return;
                _owners.Add(identity, owners = new Dictionary<string, CharacterContributor[]>(StringComparer.Ordinal));
            }
            bool changed = owners.Remove(owner);
            if (values.Length != 0)
            {
                owners.Add(owner, values);
                changed = true;
            }
            if (owners.Count == 0) _owners.Remove(identity);
            if (changed) Changed?.Invoke(identity);
        }

        public void ReplaceDomainContributors(ArcanumObjectId identity, CharacterContributorDomain domain,
            string owner, IEnumerable<CharacterContributor> contributors)
        {
            CharacterContributor[] values = (contributors ?? throw new ArgumentNullException(nameof(contributors)))
                .ToArray();
            bool removed = false;
            if (_owners.TryGetValue(identity, out Dictionary<string, CharacterContributor[]> owners))
            {
                string[] remove = owners.Where(pair => pair.Value.Any(value => value.Domain == domain))
                    .Select(pair => pair.Key).ToArray();
                foreach (string key in remove) removed |= owners.Remove(key);
                if (owners.Count == 0) _owners.Remove(identity);
            }
            SetOwnerContributors(identity, owner, values);
            if (removed && values.Length == 0) Changed?.Invoke(identity);
        }

        public bool RemoveOwner(ArcanumObjectId identity, string owner)
        {
            if (!_owners.TryGetValue(identity, out Dictionary<string, CharacterContributor[]> owners)
                || !owners.Remove(owner)) return false;
            if (owners.Count == 0) _owners.Remove(identity);
            Changed?.Invoke(identity);
            return true;
        }

        public int Evaluate(ArcanumObjectId identity, CharacterContributorTarget target, int value)
        {
            IReadOnlyList<CharacterContributor> contributors = GetOrderedContributors(identity);
            int percent = 0;
            int multiply = 1;
            int divide = 1;
            int add = 0;
            int? minimum = null;
            int? maximum = null;
            foreach (CharacterContributor contributor in contributors)
            {
                if (contributor.Target != target) continue;
                switch (contributor.Operation)
                {
                    case CharacterContributorOperation.Percent:
                        percent = checked(percent + contributor.Amount);
                        break;
                    case CharacterContributorOperation.Multiply:
                        multiply = checked(multiply * contributor.Amount);
                        break;
                    case CharacterContributorOperation.Divide:
                        divide = checked(divide * contributor.Amount);
                        break;
                    case CharacterContributorOperation.Add:
                        add = checked(add + contributor.Amount);
                        break;
                    case CharacterContributorOperation.Minimum:
                        minimum = !minimum.HasValue || minimum.Value < contributor.Amount
                            ? contributor.Amount : minimum;
                        break;
                    case CharacterContributorOperation.Maximum:
                        maximum = !maximum.HasValue || maximum.Value > contributor.Amount
                            ? contributor.Amount : maximum;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            if (percent != 0) value = checked(value * (percent + 100) / 100);
            if (multiply != 1) value = checked(value * multiply);
            if (divide != 1) value /= divide;
            if (add != 0) value = checked(value + add);
            if (minimum.HasValue && value < minimum.Value) value = minimum.Value;
            if (maximum.HasValue && value > maximum.Value) value = maximum.Value;
            return value;
        }
    }
}
