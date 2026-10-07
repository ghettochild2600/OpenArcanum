using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.World;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.UI
{
    [Flags]
    public enum HudPrimaryNotification
    {
        None = 0,
        Character = 1 << 0,
        Logbook = 1 << 1,
        TownMap = 1 << 2,
        WorldMap = 1 << 3,
        Inventory = 1 << 4,
    }

    public enum FateChoice
    {
        FullHeal,
        ForceGoodReaction,
        CriticalHit,
        CriticalMiss,
        SaveAgainstMagick,
        SpellAtMaximum,
        CriticalSuccessGambling,
        CriticalSuccessHeal,
        CriticalSuccessPickPocket,
        CriticalSuccessRepair,
        CriticalSuccessPickLocks,
        CriticalSuccessDisarmTraps,
    }

    public enum FateFailure
    {
        None,
        InvalidChoice,
        NoPlayer,
        PlayerUnavailable,
        InsufficientPoints,
        AlreadyActive,
        UnsupportedDeferredEffect,
    }

    public readonly struct FateResult
    {
        public bool Succeeded => Failure == FateFailure.None;
        public FateFailure Failure { get; }
        internal FateResult(FateFailure failure) => Failure = failure;
    }

    public enum FateResolutionKind
    {
        ImmediateFullHeal,
        DialogueReaction,
        AttackerCriticalHit,
        DefenderCriticalMiss,
        MagickSavingThrow,
        SpellAtMaximum,
        SkillCriticalSuccess,
    }

    public readonly struct FateDefinition
    {
        public FateChoice Choice { get; }
        public string DisplayIdentity { get; }
        public int Cost { get; }
        public FateResolutionKind Resolution { get; }
        public CharacterSkill? Skill { get; }
        public bool IsImmediate => Choice == FateChoice.FullHeal;
        public bool HasRuntimeResolver { get; }

        internal FateDefinition(FateChoice choice, string displayIdentity, FateResolutionKind resolution,
            bool hasRuntimeResolver, CharacterSkill? skill = null)
        {
            Choice = choice;
            DisplayIdentity = displayIdentity;
            Cost = 1;
            Resolution = resolution;
            HasRuntimeResolver = hasRuntimeResolver;
            Skill = skill;
        }
    }

    /// <summary>Retail fate.c table plus the exact fate_resolve call-site classification.</summary>
    public static class FateCatalog
    {
        private static readonly FateDefinition[] Definitions =
        {
            new(FateChoice.FullHeal, "Full Heal", FateResolutionKind.ImmediateFullHeal, true),
            new(FateChoice.ForceGoodReaction, "Force Good Reaction", FateResolutionKind.DialogueReaction, true),
            new(FateChoice.CriticalHit, "Critical Hit", FateResolutionKind.AttackerCriticalHit, true),
            new(FateChoice.CriticalMiss, "Critical Miss", FateResolutionKind.DefenderCriticalMiss, true),
            new(FateChoice.SaveAgainstMagick, "Save Against Magick", FateResolutionKind.MagickSavingThrow, true),
            new(FateChoice.SpellAtMaximum, "Spell At Maximum", FateResolutionKind.SpellAtMaximum, true),
            new(FateChoice.CriticalSuccessGambling, "Critical Success: Gambling",
                FateResolutionKind.SkillCriticalSuccess, false, CharacterSkill.Gambling),
            new(FateChoice.CriticalSuccessHeal, "Critical Success: Heal",
                FateResolutionKind.SkillCriticalSuccess, false, CharacterSkill.Heal),
            new(FateChoice.CriticalSuccessPickPocket, "Critical Success: Pick Pocket",
                FateResolutionKind.SkillCriticalSuccess, false, CharacterSkill.PickPocket),
            new(FateChoice.CriticalSuccessRepair, "Critical Success: Repair",
                FateResolutionKind.SkillCriticalSuccess, false, CharacterSkill.Repair),
            new(FateChoice.CriticalSuccessPickLocks, "Critical Success: Pick Locks",
                FateResolutionKind.SkillCriticalSuccess, false, CharacterSkill.PickLocks),
            new(FateChoice.CriticalSuccessDisarmTraps, "Critical Success: Disarm Traps",
                FateResolutionKind.SkillCriticalSuccess, false, CharacterSkill.DisarmTraps),
        };

        public static IReadOnlyList<FateDefinition> All => Definitions;

        public static bool TryGet(FateChoice choice, out FateDefinition definition)
        {
            int index = (int)choice;
            if ((uint)index < (uint)Definitions.Length && Definitions[index].Choice == choice)
            { definition = Definitions[index]; return true; }
            definition = default;
            return false;
        }
    }

    public enum SleepOption
    {
        OneHour,
        TwoHours,
        FourHours,
        EightHours,
        OneDay,
        UntilMorning,
        UntilEvening,
        UntilHealed,
    }

    public enum SleepFailure
    {
        None,
        NoPlayer,
        Dead,
        Unconscious,
        CombatActive,
        UnsupportedLocation,
        InvalidOption,
    }

    public readonly struct SleepResult
    {
        public bool Succeeded => Failure == SleepFailure.None;
        public SleepFailure Failure { get; }
        public int HoursAdvanced { get; }
        public SleepLocationMode Mode { get; }
        internal SleepResult(SleepFailure failure, int hoursAdvanced = 0,
            SleepLocationMode mode = SleepLocationMode.Unavailable)
        { Failure = failure; HoursAdvanced = hoursAdvanced; Mode = mode; }
    }

    public enum RecentActionKind { Skill, Item, Spell }

    public readonly struct RecentActionBinding
    {
        public RecentActionKind Kind { get; }
        public int SourceId { get; }
        public int IconSourceId { get; }
        public ArcanumObjectId PreferredItem { get; }

        internal RecentActionBinding(RecentActionKind kind, int sourceId, int iconSourceId,
            ArcanumObjectId preferredItem = default)
        {
            Kind = kind;
            SourceId = sourceId;
            IconSourceId = iconSourceId;
            PreferredItem = preferredItem;
        }
    }

    /// <summary>
    /// Session-owned source HUD state. It owns saved notification/fate/recent-action facts and the bounded
    /// source sleep transaction; Unity presentation remains a disposable projection.
    /// </summary>
    public sealed class GameplayHudStateService : IDisposable
    {
        public const int MaximumFatePoints = 100;
        public const int SourceHourMilliseconds = 3_600_000;
        private const int SupportedFateMask = 0x07FF;
        private readonly WorldMapSessionCoordinator _world;
        private readonly CharacterProgressionService _progression;
        private readonly CampaignStateService _campaign;
        private readonly RecentActionBinding[] _recent = new RecentActionBinding[2];
        private HudPrimaryNotification _notifications;
        private int _fatePoints;
        private int _activeFateFlags;

        public GameplayHudStateService(WorldMapSessionCoordinator world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            // hotkey_ui_init: recent slot 0 starts at skill data 1, slot 1 at skill data 0.
            _recent[0] = new RecentActionBinding(RecentActionKind.Skill, 1, 280);
            _recent[1] = new RecentActionBinding(RecentActionKind.Skill, 0, 279);
            _progression = _world.Progression;
            _campaign = _world.Campaign;
            _progression.LevelChanged += OnLevelChanged;
            _campaign.PcQuestStateChanged += OnQuestStateChanged;
            _campaign.AreaDiscovered += OnAreaDiscovered;
            _world.ObjectPlacementChanged += OnObjectPlacementChanged;
            _world.ObjectStateRemoved += OnObjectStateRemoved;
        }

        public void Dispose()
        {
            _progression.LevelChanged -= OnLevelChanged;
            _campaign.PcQuestStateChanged -= OnQuestStateChanged;
            _campaign.AreaDiscovered -= OnAreaDiscovered;
            _world.ObjectPlacementChanged -= OnObjectPlacementChanged;
            _world.ObjectStateRemoved -= OnObjectStateRemoved;
        }

        public int FatePoints => _fatePoints;
        public int ActiveFateFlags => _activeFateFlags;
        public HudPrimaryNotification Notifications => _notifications;
        public IReadOnlyList<RecentActionBinding> RecentActions => _recent;

        public bool HasNotification(HudPrimaryNotification notification)
            => (_notifications & notification) != 0;

        public void Notify(HudPrimaryNotification notification)
            => _notifications |= notification;

        public void ClearNotification(HudPrimaryNotification notification)
            => _notifications &= ~notification;

        public void GrantFatePoint()
            => _fatePoints = Math.Min(MaximumFatePoints, checked(_fatePoints + 1));

        public bool IsFateActive(FateChoice choice)
        {
            int flag = FateFlag(choice);
            return flag != 0 && (_activeFateFlags & flag) != 0;
        }

        public FateResult ActivateFate(FateChoice choice)
        {
            if (!FateCatalog.TryGet(choice, out FateDefinition definition))
                return new FateResult(FateFailure.InvalidChoice);
            if (_world.PlayerState == null) return new FateResult(FateFailure.NoPlayer);
            ArcanumObjectId player = _world.PlayerState.Identity;
            if (!_world.Vitality.TryGet(player, out PersistentCharacterVitalityState vitality)
                || vitality.CurrentHitPoints <= 0)
                return new FateResult(FateFailure.PlayerUnavailable);
            int flag = FateFlag(choice);
            if (flag != 0 && (_activeFateFlags & flag) != 0)
                return new FateResult(FateFailure.AlreadyActive);
            if (!definition.HasRuntimeResolver)
                return new FateResult(FateFailure.UnsupportedDeferredEffect);
            if (_fatePoints < definition.Cost) return new FateResult(FateFailure.InsufficientPoints);
            _fatePoints -= definition.Cost;
            if (definition.IsImmediate)
            {
                _world.Vitality.RestoreHitPoints(player, int.MaxValue);
                _world.Vitality.RestoreFatigue(player, int.MaxValue);
            }
            else
            {
                _activeFateFlags |= flag;
            }
            return new FateResult(FateFailure.None);
        }

        public FateResult DeactivateFate(FateChoice choice)
        {
            if (!FateCatalog.TryGet(choice, out FateDefinition definition) || definition.IsImmediate)
                return new FateResult(FateFailure.InvalidChoice);
            int flag = FateFlag(choice);
            if (flag == 0 || (_activeFateFlags & flag) == 0)
                return new FateResult(FateFailure.UnsupportedDeferredEffect);
            _activeFateFlags &= ~flag;
            GrantFatePoint();
            return new FateResult(FateFailure.None);
        }

        /// <summary>Consumes one armed deferred fate at its source-proven resolution call site.</summary>
        public bool TryResolveFate(FateChoice choice)
        {
            if (!FateCatalog.TryGet(choice, out FateDefinition definition)
                || definition.IsImmediate || !definition.HasRuntimeResolver)
                return false;
            int flag = FateFlag(choice);
            if ((_activeFateFlags & flag) == 0) return false;
            _activeFateFlags &= ~flag;
            return true;
        }

        public SleepFailure PreviewSleep() => PreviewSleep(out _);

        public SleepFailure PreviewSleep(out SleepLocationMode mode)
        {
            mode = SleepLocationMode.Unavailable;
            if (_world.PlayerState == null) return SleepFailure.NoPlayer;
            ArcanumObjectId player = _world.PlayerState.Identity;
            if (!_world.Vitality.TryGet(player, out PersistentCharacterVitalityState vitality)
                || vitality.CurrentHitPoints <= 0) return SleepFailure.Dead;
            if (vitality.CurrentFatigue <= 0) return SleepFailure.Unconscious;
            if (_world.Combat.IsActive) return SleepFailure.CombatActive;
            mode = _world.SleepLocations?.Resolve(_world.SelectedSector) ?? SleepLocationMode.Unavailable;
            return mode == SleepLocationMode.Unavailable ? SleepFailure.UnsupportedLocation : SleepFailure.None;
        }

        public SleepResult Sleep(SleepOption option)
        {
            SleepFailure preview = PreviewSleep(out SleepLocationMode mode);
            if (preview != SleepFailure.None) return new SleepResult(preview);
            if (mode == SleepLocationMode.TownWait && option == SleepOption.UntilHealed)
                return new SleepResult(SleepFailure.InvalidOption, mode: mode);
            int hours = ResolveSleepHours(option);
            if (hours < 1) return new SleepResult(SleepFailure.InvalidOption, mode: mode);

            ArcanumObjectId player = _world.PlayerState.Identity;
            foreach (ActiveSpellEffect effect in _world.Magic.ActiveEffects
                         .Where(value => value.Caster == player)
                         .OrderBy(value => value.Id).ToArray())
                if (PhaseOneSpellCatalog.TryGet(effect.SpellId, out SpellDefinition spell) && spell.Maintained)
                    _world.Magic.CancelMaintainedEffect(player, effect.Id);

            for (int hour = 0; hour < hours; hour++)
            {
                _world.SourceTime.Advance(SourceHourMilliseconds);
                if (mode == SleepLocationMode.WildernessSleep)
                {
                    RestingHeal(player, 1);
                    foreach (PartyMember member in _world.Party.Members) RestingHeal(member.Identity, 1);
                }
            }
            return new SleepResult(SleepFailure.None, hours, mode);
        }

        public void RecordRecentAction(QuickSlotBinding binding, ArcanumObjectId resolvedItem = default)
        {
            RecentActionBinding recent;
            switch (binding.Kind)
            {
                case QuickSlotKind.Item:
                    recent = new RecentActionBinding(RecentActionKind.Item, binding.SourceId, binding.SourceId,
                        resolvedItem.IsNull ? binding.PreferredItem : resolvedItem);
                    break;
                case QuickSlotKind.Spell when PhaseOneSpellCatalog.TryGet(binding.SourceId, out SpellDefinition spell):
                    recent = new RecentActionBinding(RecentActionKind.Spell, spell.Id, spell.IconSourceId);
                    break;
                default:
                    return;
            }
            if (Same(_recent[0], recent)) return;
            _recent[1] = _recent[0];
            _recent[0] = recent;
        }

        public bool TryResolveRecentItem(int index, out ArcanumObjectId item)
        {
            item = default;
            if (index < 0 || index >= _recent.Length || _recent[index].Kind != RecentActionKind.Item
                || _world.PlayerState == null) return false;
            RecentActionBinding binding = _recent[index];
            if (IsOwned(binding.PreferredItem, binding.SourceId))
            { item = binding.PreferredItem; return true; }
            foreach (PersistentObjectState candidate in _world.States.Values)
                if (IsOwned(candidate.Identity, binding.SourceId)
                    && (item.IsNull || string.CompareOrdinal(candidate.Identity.Key, item.Key) < 0))
                    item = candidate.Identity;
            return !item.IsNull;
        }

        internal GameplayHudSaveData ExportSaveData()
            => new()
            {
                FatePoints = _fatePoints,
                ActiveFateFlags = _activeFateFlags,
                PrimaryNotifications = (int)_notifications,
                RecentActions = _recent.Select(value => new RecentActionSaveData
                {
                    Kind = (int)value.Kind,
                    SourceId = value.SourceId,
                    IconSourceId = value.IconSourceId,
                    PreferredItemIdentity = value.PreferredItem.IsNull ? null : value.PreferredItem.Key,
                }).ToList(),
            };

        internal void RestoreSaveData(GameplayHudSaveData data)
        {
            if (data == null) return;
            _fatePoints = data.FatePoints;
            _activeFateFlags = data.ActiveFateFlags;
            _notifications = (HudPrimaryNotification)data.PrimaryNotifications;
            if (data.RecentActions?.Count != 2) return;
            for (int index = 0; index < 2; index++)
            {
                RecentActionSaveData value = data.RecentActions[index];
                ArcanumObjectId preferred = default;
                if (!string.IsNullOrWhiteSpace(value.PreferredItemIdentity))
                    ArcanumObjectId.TryParsePersistent(value.PreferredItemIdentity, out preferred);
                _recent[index] = new RecentActionBinding((RecentActionKind)value.Kind,
                    value.SourceId, value.IconSourceId, preferred);
            }
        }

        internal static bool ValidateSaveData(GameplayHudSaveData data,
            IReadOnlyDictionary<ArcanumObjectId, PersistentObjectState> objects,
            ArcanumObjectId player, out string error)
        {
            error = null;
            if (data == null) return true;
            int notificationMask = (int)(HudPrimaryNotification.Character | HudPrimaryNotification.Logbook
                | HudPrimaryNotification.TownMap | HudPrimaryNotification.WorldMap | HudPrimaryNotification.Inventory);
            if (data.FatePoints < 0 || data.FatePoints > MaximumFatePoints
                || (data.ActiveFateFlags & ~SupportedFateMask) != 0
                || (data.PrimaryNotifications & ~notificationMask) != 0
                || data.RecentActions == null || data.RecentActions.Count != 2)
            { error = "The saved gameplay HUD state is invalid."; return false; }
            foreach (RecentActionSaveData value in data.RecentActions)
            {
                if (value == null || !Enum.IsDefined(typeof(RecentActionKind), value.Kind)
                    || value.SourceId < 0 || value.IconSourceId < 0)
                { error = "A saved recent action is invalid."; return false; }
                if (string.IsNullOrWhiteSpace(value.PreferredItemIdentity)) continue;
                if (!ArcanumObjectId.TryParsePersistent(value.PreferredItemIdentity, out ArcanumObjectId identity)
                    || !objects.TryGetValue(identity, out PersistentObjectState state)
                    || state.ParentIdentity != player || state.PrototypeNumber != value.SourceId)
                { error = "A saved recent item action is unavailable."; return false; }
            }
            return true;
        }

        private int ResolveSleepHours(SleepOption option)
        {
            switch (option)
            {
                case SleepOption.OneHour: return 1;
                case SleepOption.TwoHours: return 2;
                case SleepOption.FourHours: return 4;
                case SleepOption.EightHours: return 8;
                case SleepOption.OneDay: return 24;
                case SleepOption.UntilMorning: return HoursUntil(7);
                case SleepOption.UntilEvening: return HoursUntil(20);
                case SleepOption.UntilHealed:
                    PersistentCharacterVitalityState vitality = _world.Vitality.Get(_world.PlayerState.Identity);
                    int damage = vitality.MaximumHitPoints - vitality.CurrentHitPoints;
                    int healRate = Math.Max(1, _world.DerivedStats.GetDerivedStat(
                        _world.PlayerState.Identity, CharacterDerivedStat.HealRate));
                    return Math.Max(1, (damage + healRate - 1) / healRate);
                default: return 0;
            }
        }

        private int HoursUntil(int targetHour)
        {
            int current = (int)((12 + _world.SourceTime.ElapsedMilliseconds / SourceHourMilliseconds) % 24);
            int hours = (targetHour - current + 24) % 24;
            return hours == 0 ? 24 : hours;
        }

        private void RestingHeal(ArcanumObjectId identity, int hours)
        {
            if (!_world.Vitality.TryGet(identity, out PersistentCharacterVitalityState vitality)
                || vitality.CurrentHitPoints <= 0) return;
            int healRate = Math.Max(0, _world.DerivedStats.GetDerivedStat(identity,
                CharacterDerivedStat.HealRate));
            _world.Vitality.RestoreHitPoints(identity, checked(healRate * hours));
            _world.Vitality.RestoreFatigue(identity, checked(3 * healRate * hours));
        }

        private void OnObjectPlacementChanged(PersistentObjectState item, ObjectPlacement previous,
            ObjectPlacement current)
        {
            if (_world.PlayerState == null || item.Type is ObjectType.Pc or ObjectType.Npc) return;
            ArcanumObjectId player = _world.PlayerState.Identity;
            bool wasOwned = previous.ParentIdentity == player;
            bool isOwned = current.ParentIdentity == player;
            if (wasOwned != isOwned) Notify(HudPrimaryNotification.Inventory);
        }

        private void OnObjectStateRemoved(PersistentObjectState item, ObjectPlacement previous)
        {
            if (_world.PlayerState != null && previous.ParentIdentity == _world.PlayerState.Identity)
                Notify(HudPrimaryNotification.Inventory);
        }

        private void OnLevelChanged(ArcanumObjectId _)
            => Notify(HudPrimaryNotification.Character);

        private void OnQuestStateChanged(int _, QuestState __, QuestState ___)
            => Notify(HudPrimaryNotification.Logbook);

        private void OnAreaDiscovered(AreaId _)
            => Notify(HudPrimaryNotification.WorldMap);

        private bool IsOwned(ArcanumObjectId identity, int prototype)
            => _world.TryGetObjectState(identity, out PersistentObjectState state)
               && state.PrototypeNumber == prototype && state.ParentIdentity == _world.PlayerState.Identity
               && state.Placement.Kind is ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped;

        private static bool Same(RecentActionBinding left, RecentActionBinding right)
            => left.Kind == right.Kind && left.SourceId == right.SourceId
               && (left.Kind != RecentActionKind.Item || left.PreferredItem == right.PreferredItem);

        private static int FateFlag(FateChoice choice)
            => choice == FateChoice.FullHeal ? 0 : 1 << ((int)choice - 1);
    }
}
