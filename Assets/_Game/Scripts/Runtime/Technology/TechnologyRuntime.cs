using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Technology
{
    public enum TechnologyDiscipline { Herbology, Chemistry, Electrical, Explosives, GunSmithy, Mechanical, Smithy, Therapeutics }
    public enum TechnologyDegree
    {
        Invalid = -1,
        Layman,
        Novice,
        Assistant,
        Associate,
        Technician,
        Engineer,
        Professor,
        Doctorate,
    }
    public enum TechnologyLearningFailure { None, InvalidCharacter, MaximumDegree, InsufficientIntelligence, InsufficientCharacterPoints }

    public readonly struct TechnologyLearningResult
    {
        public TechnologyLearningFailure Failure { get; }
        public bool Succeeded => Failure == TechnologyLearningFailure.None;
        public TechnologyDiscipline Discipline { get; }
        public TechnologyDegree Degree { get; }
        internal TechnologyLearningResult(TechnologyLearningFailure failure, TechnologyDiscipline discipline, TechnologyDegree degree)
        { Failure = failure; Discipline = discipline; Degree = degree; }
    }

    public enum TechnologyTargetClass { DamagedLivingOrganicCritter }
    public enum TechnologyEffectFamily { Healing }

    public sealed class TechnologyItemDefinition
    {
        public int PrototypeNumber { get; }
        public string Name { get; }
        public TechnologyDiscipline Discipline { get; }
        public TechnologyTargetClass TargetClass { get; }
        public TechnologyEffectFamily EffectFamily { get; }
        public int EffectId { get; }
        public int Range { get; }
        public int ActionPointCost { get; }
        public int Magnitude { get; }
        public int SourceCharges { get; }
        public bool ConsumedOnUse { get; }
        internal TechnologyItemDefinition(int prototypeNumber, string name, TechnologyDiscipline discipline,
            TechnologyTargetClass targetClass, TechnologyEffectFamily effectFamily, int effectId, int range,
            int actionPointCost, int magnitude, int sourceCharges, bool consumedOnUse)
        {
            PrototypeNumber = prototypeNumber; Name = name; Discipline = discipline; TargetClass = targetClass;
            EffectFamily = effectFamily; EffectId = effectId; Range = range; ActionPointCost = actionPointCost;
            Magnitude = magnitude; SourceCharges = sourceCharges; ConsumedOnUse = consumedOnUse;
        }
    }

    public static class PhaseOneTechnologyCatalog
    {
        public const int HealingSalvePrototype = 10079;
        public const int HealingSalveEffect = 150;
        public const int PowerAxePrototype = 6088;
        public const int TrapSpringerPrototype = 15122;
        public const int SourceItemUseActionPointCost = 4;
        private static readonly TechnologyItemDefinition HealingSalve = new(HealingSalvePrototype,
            "Healing Salve", TechnologyDiscipline.Herbology, TechnologyTargetClass.DamagedLivingOrganicCritter,
            TechnologyEffectFamily.Healing, HealingSalveEffect, 2, SourceItemUseActionPointCost, 20, 1, true);
        public static IEnumerable<TechnologyItemDefinition> All { get { yield return HealingSalve; } }
        public static bool TryGetItem(int prototypeNumber, out TechnologyItemDefinition definition)
        { definition = prototypeNumber == HealingSalvePrototype ? HealingSalve : null; return definition != null; }
        public static int BuiltInSchematicId(TechnologyDiscipline discipline, TechnologyDegree degree)
        {
            if (!Enum.IsDefined(typeof(TechnologyDiscipline), discipline)) throw new ArgumentOutOfRangeException(nameof(discipline));
            if (degree < TechnologyDegree.Novice || degree > TechnologyDegree.Doctorate) throw new ArgumentOutOfRangeException(nameof(degree));
            return 1990 + 200 * (int)discipline + 10 * (int)degree;
        }
    }

    public readonly struct TechnologyUseRequest
    {
        public ArcanumObjectId Actor { get; }
        public ArcanumObjectId Item { get; }
        public ArcanumObjectId Target { get; }
        public TechnologyUseRequest(ArcanumObjectId actor, ArcanumObjectId item, ArcanumObjectId target)
        { Actor = actor; Item = item; Target = target; }
    }

    public enum TechnologyUseFailure
    {
        None, InvalidActor, ActorUnavailable, ItemNotFound, UnsupportedItem, ItemNotOwned,
        InvalidTarget, TargetUnavailable, OutOfRange, CombatRejected, RealTimeSchedulingRequired,
    }

    public readonly struct TechnologyUseResult
    {
        public bool Succeeded => Failure == TechnologyUseFailure.None;
        public TechnologyUseFailure Failure { get; }
        public TechnologyUseRequest Request { get; }
        public int ActionPointCost { get; }
        public int Magnitude { get; }
        public int ChargesBefore { get; }
        public int ChargesAfter { get; }
        public bool ItemConsumed { get; }
        internal TechnologyUseResult(TechnologyUseFailure failure, TechnologyUseRequest request, int actionPointCost = 0,
            int magnitude = 0, int chargesBefore = 0, int chargesAfter = 0, bool itemConsumed = false)
        { Failure = failure; Request = request; ActionPointCost = actionPointCost; Magnitude = magnitude;
          ChargesBefore = chargesBefore; ChargesAfter = chargesAfter; ItemConsumed = itemConsumed; }
    }

    /// <summary>Authoritative M10B discipline, aptitude and technological item-use runtime.</summary>
    public sealed class TechnologyStateService
    {
        private static readonly int[] IntelligenceRequirements = { 0, 5, 8, 11, 13, 15, 17, 19 };
        private readonly WorldMapSessionCoordinator _world;
        private readonly Dictionary<ArcanumObjectId, int[]> _disciplineRanks = new();
        private readonly HashSet<ArcanumObjectId> _restoredCharacters = new();
        public TechnologyStateService(WorldMapSessionCoordinator world) => _world = world ?? throw new ArgumentNullException(nameof(world));

        public void RegisterSourceCharacter(ArcanumObjectId identity, int[] instanceSpellTech, int[] prototypeSpellTech)
        {
            if (_restoredCharacters.Contains(identity)) return;
            int[] source = instanceSpellTech ?? prototypeSpellTech ?? new int[25];
            if (source.Length < 25) throw new InvalidOperationException($"Character {identity} spell-tech source is incomplete.");
            var ranks = new int[8];
            for (int i = 0; i < 8; i++) ranks[i] = Math.Max(0, Math.Min(7, source[17 + i]));
            _disciplineRanks[identity] = ranks;
        }

        public TechnologyDegree GetLearnedDegree(ArcanumObjectId identity, TechnologyDiscipline discipline)
            => (TechnologyDegree)GetRanks(identity)[(int)discipline];
        public TechnologyDegree GetEffectiveDegree(ArcanumObjectId identity, TechnologyDiscipline discipline)
        {
            int learned = (int)GetLearnedDegree(identity, discipline);
            int intelligence = _world.Characters.GetEffectiveAttribute(identity, CharacterAttribute.Intelligence);
            while (learned > 0 && intelligence < IntelligenceRequirements[learned]) learned--;
            return (TechnologyDegree)learned;
        }
        public bool KnowsBuiltInSchematic(ArcanumObjectId identity, TechnologyDiscipline discipline, TechnologyDegree degree)
            => degree >= TechnologyDegree.Novice && degree <= GetEffectiveDegree(identity, discipline);

        public TechnologyLearningResult PreviewLearnNextDegree(ArcanumObjectId identity, TechnologyDiscipline discipline)
        {
            if (!_world.Characters.TryGet(identity, out _) || !_world.Progression.TryGet(identity, out _)
                || !_world.DerivedStats.TryGet(identity, out _)) return LearnFail(TechnologyLearningFailure.InvalidCharacter, discipline, TechnologyDegree.Invalid);
            int current = GetRanks(identity)[(int)discipline];
            if (current >= 7) return LearnFail(TechnologyLearningFailure.MaximumDegree, discipline, (TechnologyDegree)current);
            int next = current + 1;
            if (_world.Characters.GetEffectiveAttribute(identity, CharacterAttribute.Intelligence) < IntelligenceRequirements[next])
                return LearnFail(TechnologyLearningFailure.InsufficientIntelligence, discipline, (TechnologyDegree)next);
            if (_world.Progression.GetUnspentCharacterPoints(identity) < 1)
                return LearnFail(TechnologyLearningFailure.InsufficientCharacterPoints, discipline, (TechnologyDegree)next);
            return new TechnologyLearningResult(TechnologyLearningFailure.None, discipline, (TechnologyDegree)next);
        }
        public TechnologyLearningResult LearnNextDegree(ArcanumObjectId identity, TechnologyDiscipline discipline)
        {
            TechnologyLearningResult preview = PreviewLearnNextDegree(identity, discipline);
            if (!preview.Succeeded) return preview;
            int[] ranks = GetRanks(identity);
            _world.Progression.SpendCharacterPoint(identity);
            ranks[(int)discipline]++;
            _world.DerivedStats.AddTechnologyPoint(identity);
            return preview;
        }

        public TechnologyUseResult PreviewUse(TechnologyUseRequest request) => PreviewUseInternal(request, false);
        public TechnologyUseResult Use(TechnologyUseRequest request) => UseInternal(request, false);
        internal TechnologyUseResult PreviewScheduledUse(TechnologyUseRequest request) => PreviewUseInternal(request, true);
        internal TechnologyUseResult ResolveScheduledUse(TechnologyUseRequest request) => UseInternal(request, true);

        private TechnologyUseResult UseInternal(TechnologyUseRequest request, bool scheduledRealTime)
        {
            TechnologyUseResult preview = PreviewUseInternal(request, scheduledRealTime);
            if (!preview.Succeeded) return preview;
            _world.TryGetObjectState(request.Item, out PersistentObjectState item);
            PhaseOneTechnologyCatalog.TryGetItem(item.PrototypeNumber, out TechnologyItemDefinition definition);
            if (!_world.ConsumeSingularItem(request.Item, request.Actor)) return Fail(TechnologyUseFailure.ItemNotOwned, request);
            if (_world.Combat.IsActive && _world.Combat.Mode == CombatMode.TurnBased)
                _world.Combat.CommitTurnBasedTechnologyAction(request.Actor, definition.ActionPointCost);
            int before = _world.Vitality.GetCurrentHitPoints(request.Target);
            _world.Vitality.RestoreHitPoints(request.Target, definition.Magnitude);
            int magnitude = _world.Vitality.GetCurrentHitPoints(request.Target) - before;
            return new TechnologyUseResult(TechnologyUseFailure.None, request,
                _world.Combat.IsActive && _world.Combat.Mode == CombatMode.TurnBased ? definition.ActionPointCost : 0,
                magnitude, definition.SourceCharges, 0, true);
        }

        private TechnologyUseResult PreviewUseInternal(TechnologyUseRequest request, bool scheduledRealTime)
        {
            if (!_world.Characters.TryGet(request.Actor, out _) || !_world.Vitality.TryGet(request.Actor, out _)) return Fail(TechnologyUseFailure.InvalidActor, request);
            if (_world.Vitality.IsDead(request.Actor) || _world.Vitality.IsUnconscious(request.Actor)) return Fail(TechnologyUseFailure.ActorUnavailable, request);
            if (!_world.TryGetObjectState(request.Item, out PersistentObjectState item)) return Fail(TechnologyUseFailure.ItemNotFound, request);
            if (!PhaseOneTechnologyCatalog.TryGetItem(item.PrototypeNumber, out TechnologyItemDefinition definition)) return Fail(TechnologyUseFailure.UnsupportedItem, request);
            if (item.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped) || item.ParentIdentity != request.Actor)
                return Fail(TechnologyUseFailure.ItemNotOwned, request);
            if (!_world.Characters.TryGet(request.Target, out _) || !_world.Vitality.TryGet(request.Target, out _)) return Fail(TechnologyUseFailure.InvalidTarget, request);
            if (_world.Vitality.IsDead(request.Target) || _world.Vitality.IsUnconscious(request.Target)) return Fail(TechnologyUseFailure.TargetUnavailable, request);
            if (_world.Combat.HasCritterFlag(request.Target, unchecked((int)0x20000000))
                || _world.Vitality.GetCurrentHitPoints(request.Target) >= _world.Vitality.GetMaximumHitPoints(request.Target))
                return Fail(TechnologyUseFailure.InvalidTarget, request);
            if (!_world.Combat.TryGetSpellTraversal(request.Actor, request.Target, out int distance, out _) || distance > definition.Range)
                return Fail(TechnologyUseFailure.OutOfRange, request);
            if (_world.Combat.IsActive)
            {
                if (_world.Combat.Mode == CombatMode.RealTime && !scheduledRealTime) return Fail(TechnologyUseFailure.RealTimeSchedulingRequired, request);
                if (_world.Combat.PreviewTechnologyAction(request.Actor, definition.ActionPointCost) != CombatFailure.None)
                    return Fail(TechnologyUseFailure.CombatRejected, request);
            }
            return new TechnologyUseResult(TechnologyUseFailure.None, request);
        }

        public int GetItemAptitudeCriticalFailureChance(PersistentObjectState item, ArcanumObjectId owner)
        {
            if (item == null || !_world.Characters.TryGet(owner, out _)) return 0;
            int complexity = _world.ResolvePrototype(item.PrototypeNumber)?.ItemComplexity ?? 0;
            int aptitude = _world.DerivedStats.GetDerivedStat(owner, CharacterDerivedStat.MagickTechAptitude);
            return complexity < 0 && aptitude > 0 ? Math.Min(100, -complexity * aptitude / 100) : 0;
        }
        public int GetItemEffectivePower(PersistentObjectState item, ArcanumObjectId owner)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            int complexity = _world.ResolvePrototype(item.PrototypeNumber)?.ItemComplexity ?? 0;
            int aptitude = _world.DerivedStats.GetDerivedStat(owner, CharacterDerivedStat.MagickTechAptitude);
            int effective = (complexity + aptitude) / 2;
            if (complexity < 0) return effective > 0 ? 0 : Math.Max(complexity, effective);
            return effective;
        }

        public TechnologySaveData ExportSaveData() => new()
        {
            Characters = _disciplineRanks.OrderBy(p => p.Key.Key, StringComparer.Ordinal).Select(p => new TechnologyCharacterSaveData
            { Identity = p.Key.Key, DisciplineRanks = (int[])p.Value.Clone() }).ToList(),
        };
        internal void RestoreSaveData(TechnologySaveData data)
        {
            if (data == null) return;
            foreach (TechnologyCharacterSaveData value in data.Characters)
            {
                ArcanumObjectId.TryParsePersistent(value.Identity, out ArcanumObjectId identity);
                int[] ranks = (int[])value.DisciplineRanks.Clone();
                _disciplineRanks.Add(identity, ranks); _restoredCharacters.Add(identity);
            }
        }

        private int[] GetRanks(ArcanumObjectId identity)
        { if (!_disciplineRanks.TryGetValue(identity, out int[] ranks)) throw new KeyNotFoundException($"No technology state exists for {identity}."); return ranks; }
        private static TechnologyLearningResult LearnFail(TechnologyLearningFailure f, TechnologyDiscipline d, TechnologyDegree r) => new(f, d, r);
        private static TechnologyUseResult Fail(TechnologyUseFailure f, TechnologyUseRequest r) => new(f, r);
    }
}
