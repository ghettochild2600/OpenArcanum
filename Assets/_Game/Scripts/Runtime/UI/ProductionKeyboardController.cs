using System;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.UI
{
    public enum ProductionKeyPhase { Down, Up, Held }

    public readonly struct ProductionKeyboardContext
    {
        public GameUiScreen Screen { get; }
        public bool HasPlayer { get; }
        public bool TextEntryFocused { get; }
        public bool CombatActive { get; }
        public CombatMode CombatMode { get; }
        public bool HudPanelOpen { get; }

        public ProductionKeyboardContext(GameUiScreen screen, bool hasPlayer, bool textEntryFocused,
            bool combatActive, CombatMode combatMode, bool hudPanelOpen = false)
        {
            Screen = screen;
            HasPlayer = hasPlayer;
            TextEntryFocused = textEntryFocused;
            CombatActive = combatActive;
            CombatMode = combatMode;
            HudPanelOpen = hudPanelOpen;
        }
    }

    public interface IProductionKeyboardCommands
    {
        void Escape();
        void ToggleScreen(GameUiScreen screen);
        void CloseInterface();
        void ToggleCombatMode();
        void EndTurn();
        void ToggleAttackTalkMode();
        void ActivateQuickSlot(int index);
        void ActivateRecentAction();
        void ToggleSleep();
        void ToggleFate();
        void SetCalledLocation(CombatCalledLocation location);
        void IssuePartyOrder(int sourceIndex);
        void AutoSave();
        void AutoLoad();
        void CaptureScreenshot();
        void ShowVersion();
        void CenterCamera();
        void ScrollCamera(Vector2 direction);
        void Unsupported(string sourceAction);
    }

    /// <summary>
    /// Deterministic retail-default keyboard map. It interprets key state and context only; every command is
    /// delegated to the established UI, party, combat, save, movement or camera boundary.
    /// </summary>
    public sealed class ProductionKeyboardController
    {
        public bool AlwaysRun { get; private set; }
        public bool ControlHeld { get; private set; }
        public bool ShiftHeld { get; private set; }
        public bool AltHeld { get; private set; }
        public bool EffectiveRun => AlwaysRun ^ ControlHeld;

        public void SetModifiers(bool control, bool shift, bool alt)
        {
            ControlHeld = control;
            ShiftHeld = shift;
            AltHeld = alt;
        }

        public bool Dispatch(KeyCode key, ProductionKeyPhase phase, ProductionKeyboardContext context,
            IProductionKeyboardCommands commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (key == KeyCode.Escape && phase == ProductionKeyPhase.Up)
            {
                commands.Escape();
                return true;
            }
            if (context.TextEntryFocused) return false;

            if (context.Screen == GameUiScreen.Dialogue && phase == ProductionKeyPhase.Down
                && TryQuickSlotIndex(key, out int dialogueIndex))
                return false; // dialogue owns its numbered responses

            if (key == KeyCode.Space && phase == ProductionKeyPhase.Down)
            {
                if (context.Screen != GameUiScreen.None || context.HudPanelOpen) commands.CloseInterface();
                else commands.ToggleCombatMode();
                return true;
            }

            if (phase == ProductionKeyPhase.Held)
            {
                Vector2 direction = key switch
                {
                    KeyCode.UpArrow => Vector2.up,
                    KeyCode.DownArrow => Vector2.down,
                    KeyCode.LeftArrow => Vector2.left,
                    KeyCode.RightArrow => Vector2.right,
                    _ => Vector2.zero,
                };
                if (direction == Vector2.zero) return false;
                commands.ScrollCamera(direction);
                return true;
            }

            if (phase == ProductionKeyPhase.Up && key is >= KeyCode.F1 and <= KeyCode.F6)
            {
                commands.IssuePartyOrder((int)key - (int)KeyCode.F1);
                return true;
            }
            if (phase == ProductionKeyPhase.Up)
            {
                switch (key)
                {
                    case KeyCode.F7: commands.AutoSave(); return true;
                    case KeyCode.F8: commands.AutoLoad(); return true;
                    case KeyCode.E:
                        if (context.CombatActive && context.CombatMode == CombatMode.TurnBased) commands.EndTurn();
                        return context.CombatActive;
                    case KeyCode.Home: commands.CenterCamera(); return true;
                    case KeyCode.Comma:
                    case KeyCode.Period:
                    case KeyCode.Slash:
                        commands.SetCalledLocation(CombatCalledLocation.Torso);
                        return context.CombatActive;
                }
                return false;
            }

            if (phase != ProductionKeyPhase.Down) return false;
            if (TryQuickSlotIndex(key, out int slot))
            {
                commands.ActivateQuickSlot(slot);
                return true;
            }

            switch (key)
            {
                case KeyCode.I: commands.ToggleScreen(GameUiScreen.Inventory); return true;
                case KeyCode.C: commands.ToggleScreen(GameUiScreen.Character); return true;
                case KeyCode.M: commands.ToggleScreen(GameUiScreen.Magic); return true;
                case KeyCode.T: commands.ToggleScreen(GameUiScreen.Technology); return true;
                case KeyCode.K: commands.ToggleScreen(GameUiScreen.Skills); return true;
                case KeyCode.L: commands.ToggleScreen(GameUiScreen.Journal); return true;
                case KeyCode.W: commands.ToggleScreen(GameUiScreen.Map); return true;
                case KeyCode.O: commands.ToggleScreen(GameUiScreen.Options); return true;
                case KeyCode.R: commands.ToggleAttackTalkMode(); return true;
                case KeyCode.A: commands.ActivateRecentAction(); return true;
                case KeyCode.S: commands.ToggleSleep(); return true;
                case KeyCode.F: commands.ToggleFate(); return true;
                case KeyCode.V: commands.ShowVersion(); return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: commands.Unsupported("Broadcast/chat"); return true;
                case KeyCode.F12: commands.CaptureScreenshot(); return true;
                case KeyCode.Numlock:
                    AlwaysRun = !AlwaysRun;
                    return true;
                case KeyCode.Comma:
                    if (context.CombatActive) commands.SetCalledLocation(CombatCalledLocation.Head);
                    return context.CombatActive;
                case KeyCode.Period:
                    if (context.CombatActive) commands.SetCalledLocation(CombatCalledLocation.Arm);
                    return context.CombatActive;
                case KeyCode.Slash:
                    if (context.CombatActive) commands.SetCalledLocation(CombatCalledLocation.Leg);
                    return context.CombatActive;
                default: return false;
            }
        }

        private static bool TryQuickSlotIndex(KeyCode key, out int index)
        {
            if (key is >= KeyCode.Alpha1 and <= KeyCode.Alpha9)
            {
                index = (int)key - (int)KeyCode.Alpha1;
                return true;
            }
            if (key == KeyCode.Alpha0) { index = 9; return true; }
            index = -1;
            return false;
        }
    }

    public enum QuickSlotKind { Empty, Item, Spell }

    public readonly struct QuickSlotBinding
    {
        public QuickSlotKind Kind { get; }
        public ArcanumObjectId PreferredItem { get; }
        public int SourceId { get; }

        public QuickSlotBinding(QuickSlotKind kind, ArcanumObjectId preferredItem, int sourceId)
        { Kind = kind; PreferredItem = preferredItem; SourceId = sourceId; }
    }

    /// <summary>Coordinator-owned authoritative references for the ten retail hotkey-bank slots.</summary>
    public sealed class ProductionShortcutState
    {
        public const int SlotCount = 10;
        private readonly WorldMapSessionCoordinator _world;
        private readonly QuickSlotBinding[] _slots = new QuickSlotBinding[SlotCount];
        public int ActiveSlot { get; private set; } = -1;
        public ProductionShortcutState(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public QuickSlotBinding Get(int index) => IsIndex(index) ? _slots[index] : default;

        public bool AssignItem(int index, ArcanumObjectId item)
        {
            if (!IsIndex(index) || _world.PlayerState == null
                || !_world.TryGetObjectState(item, out PersistentObjectState state)
                || state.ParentIdentity != _world.PlayerState.Identity
                || state.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped))
                return false;
            _slots[index] = new QuickSlotBinding(QuickSlotKind.Item, item, state.PrototypeNumber);
            return true;
        }

        public bool AssignSpell(int index, int spellId)
        {
            if (!IsIndex(index) || _world.PlayerState == null
                || !_world.Magic.KnowsSpell(_world.PlayerState.Identity, spellId)) return false;
            _slots[index] = new QuickSlotBinding(QuickSlotKind.Spell, default, spellId);
            return true;
        }

        public bool TryResolveItem(int index, out ArcanumObjectId item)
        {
            item = default;
            if (!IsIndex(index) || _slots[index].Kind != QuickSlotKind.Item || _world.PlayerState == null)
                return false;
            QuickSlotBinding binding = _slots[index];
            if (IsOwned(binding.PreferredItem, binding.SourceId)) { item = binding.PreferredItem; return true; }
            foreach (PersistentObjectState candidate in _world.States.Values)
                if (IsOwned(candidate.Identity, binding.SourceId)
                    && (item.IsNull || string.CompareOrdinal(candidate.Identity.Key, item.Key) < 0))
                    item = candidate.Identity;
            return !item.IsNull;
        }

        public void MarkActivated(int index)
        {
            if (IsIndex(index)) ActiveSlot = index;
        }

        private bool IsOwned(ArcanumObjectId item, int prototype)
            => _world.TryGetObjectState(item, out PersistentObjectState state)
               && state.PrototypeNumber == prototype && state.ParentIdentity == _world.PlayerState.Identity
               && state.Placement.Kind is ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped;

        private static bool IsIndex(int index) => index >= 0 && index < SlotCount;
    }
}
