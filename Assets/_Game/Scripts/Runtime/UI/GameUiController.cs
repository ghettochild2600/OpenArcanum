using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using Arcanum.Runtime.Audio;

namespace Arcanum.Runtime.UI
{
    public enum GameUiScreen
    {
        None, MainMenu, CharacterCreation, PregeneratedCharacter, StartingEquipment, Inventory, Character, Skills, Magic, Technology, Crafting, Options,
        Journal, Map, Party, Merchant, SaveLoad, Dialogue, Corpse,
    }

    public enum GameUiCursorMode
    {
        Default, Move, Use, Attack, Invalid, Item, SpellTarget, TechnologyTarget, MerchantTarget,
    }

    public enum GameUiHudPanel { None, Fate, Sleep, Skills }

    public sealed class GameUiHudView
    {
        public int HitPoints { get; internal set; }
        public int MaximumHitPoints { get; internal set; }
        public int Fatigue { get; internal set; }
        public int MaximumFatigue { get; internal set; }
        public string Weapon { get; internal set; }
        public int Ammunition { get; internal set; }
        public bool CombatActive { get; internal set; }
        public CombatMode CombatMode { get; internal set; }
        public int ActionPoints { get; internal set; }
        public int MaximumActionPoints { get; internal set; }
        public string Readiness { get; internal set; }
        public int MaintainedSpellSlotCapacity { get; internal set; }
        public IReadOnlyList<ActiveSpellEffect> ActiveEffects { get; internal set; }
        public int FatePoints { get; internal set; }
        public int ContextQuantity { get; internal set; }
        public int ContextIconSourceId { get; internal set; }
        public int ExperienceGaugeValue { get; internal set; }
        public long SourceTimeMilliseconds { get; internal set; }
        public bool UsesWorldMapButton { get; internal set; }
        public HudPrimaryNotification PrimaryNotifications { get; internal set; }
        public IReadOnlyList<RecentActionBinding> RecentActions { get; internal set; }
    }

    public sealed class GameUiItemView
    {
        public ArcanumObjectId Identity { get; internal set; }
        public int PrototypeNumber { get; internal set; }
        public ObjectType Type { get; internal set; }
        public string Name { get; internal set; }
        public int Quantity { get; internal set; }
        public long Weight { get; internal set; }
        public uint? InventoryArtId { get; internal set; }
        public InventoryFootprint InventoryFootprint { get; internal set; }
        public int InventoryLocation { get; internal set; }
        public WornLocation? WornLocation { get; internal set; }
        public int? AmmoItemType { get; internal set; }
        public bool IsEquipped => WornLocation.HasValue;
    }

    /// <summary>Authoritative values drawn by the ordinary source Inventory panel.</summary>
    public sealed class GameUiInventorySummaryView
    {
        public long Weight { get; internal set; }
        public int CarryCapacity { get; internal set; }
        public int EncumbranceLevel { get; internal set; }
        public int EncumbranceThreshold { get; internal set; }
        public int Speed { get; internal set; }
        public int TotalAttack { get; internal set; }
        public int TotalDefense { get; internal set; }
        public int Gold { get; internal set; }
        public IReadOnlyList<int> Ammunition { get; internal set; }
    }

    public sealed class GameUiWorldTargetView
    {
        public ArcanumObjectId Identity { get; internal set; }
        public string Name { get; internal set; }
        public ObjectType Type { get; internal set; }
        public bool Dead { get; internal set; }
        public bool Hostile { get; internal set; }
        public int IconSourceId { get; internal set; }
        public GameUiItemInspectionView Item { get; internal set; }
    }

    /// <summary>Shared source item-examination projection used by barter and world-hover presentation.</summary>
    public sealed class GameUiItemInspectionView
    {
        public string Name { get; internal set; }
        public uint? InventoryArtId { get; internal set; }
        public IReadOnlyList<string> Lines { get; internal set; }
        public string Summary => Lines == null ? string.Empty : string.Join("   ", Lines);
    }

    public readonly struct GameUiAttributeView
    {
        public CharacterAttribute Attribute { get; }
        public int Base { get; }
        public int Effective { get; }
        public GameUiAttributeView(CharacterAttribute attribute, int baseValue, int effective)
        { Attribute = attribute; Base = baseValue; Effective = effective; }
    }

    public readonly struct GameUiSkillView
    {
        public CharacterSkill Skill { get; }
        public int PurchasedPoints { get; }
        public int EffectiveRank { get; }
        public SkillTrainingLevel Training { get; }
        public GameUiSkillView(CharacterSkill skill, int purchased, int effective, SkillTrainingLevel training)
        { Skill = skill; PurchasedPoints = purchased; EffectiveRank = effective; Training = training; }
    }

    public readonly struct GameUiDerivedStatView
    {
        public CharacterDerivedStat Stat { get; }
        public int Value { get; }
        public GameUiDerivedStatView(CharacterDerivedStat stat, int value) { Stat = stat; Value = value; }
    }

    public readonly struct GameUiResistanceView
    {
        public CharacterResistance Resistance { get; }
        public int Value { get; }
        public GameUiResistanceView(CharacterResistance resistance, int value)
        { Resistance = resistance; Value = value; }
    }

    public sealed class GameUiCharacterView
    {
        public string Name { get; internal set; }
        public CharacterRace Race { get; internal set; }
        public CharacterGender Gender { get; internal set; }
        public int Level { get; internal set; }
        public int Experience { get; internal set; }
        public int CharacterPoints { get; internal set; }
        public int ExperienceToNextLevel { get; internal set; }
        public string Background { get; internal set; }
        public int PortraitId { get; internal set; }
        public int Age { get; internal set; }
        public int HitPoints { get; internal set; }
        public int MaximumHitPoints { get; internal set; }
        public int Fatigue { get; internal set; }
        public int MaximumFatigue { get; internal set; }
        public int Alignment { get; internal set; }
        public int Aptitude { get; internal set; }
        public int ArmorClass { get; internal set; }
        public long CarriedWeight { get; internal set; }
        public int CarryCapacity { get; internal set; }
        public IReadOnlyList<GameUiAttributeView> Attributes { get; internal set; }
        public IReadOnlyList<GameUiSkillView> Skills { get; internal set; }
        public IReadOnlyList<GameUiDerivedStatView> DerivedStats { get; internal set; }
        public IReadOnlyList<GameUiResistanceView> Resistances { get; internal set; }
    }

    public sealed class GameUiSpellView
    {
        public SpellDefinition Definition { get; internal set; }
        public bool Learned { get; internal set; }
        public SpellCastFailure PreviewFailure { get; internal set; }
    }

    public sealed class GameUiTechnologyView
    {
        public TechnologyDiscipline Discipline { get; internal set; }
        public TechnologyDegree Learned { get; internal set; }
        public TechnologyDegree Effective { get; internal set; }
        public int EffectiveLevel { get; internal set; }
    }

    public sealed class GameUiSchematicView
    {
        public SchematicDefinition Definition { get; internal set; }
        public CraftingFailure PreviewFailure { get; internal set; }
        public int ProductPrototype { get; internal set; }
        public int ProductQuantity { get; internal set; }
    }

    public sealed class GameUiMerchantItemView
    {
        public GameUiItemView Item { get; internal set; }
        public EconomyTransactionDirection Direction { get; internal set; }
        public int UnitPrice { get; internal set; }
        public EconomyFailure Failure { get; internal set; }
    }

    public sealed class GameUiPartyMemberView
    {
        public ArcanumObjectId Identity { get; internal set; }
        public string Name { get; internal set; }
        public int HitPoints { get; internal set; }
        public int MaximumHitPoints { get; internal set; }
        public int Fatigue { get; internal set; }
        public int MaximumFatigue { get; internal set; }
        public bool Dead { get; internal set; }
        public bool Unconscious { get; internal set; }
        public bool Forced { get; internal set; }
    }

    /// <summary>
    /// M12C presentation/controller boundary. It owns modal, selection, cursor and feedback state only; every
    /// gameplay projection and command delegates to its established authoritative service.
    /// </summary>
    public sealed class GameUiController
    {
        private readonly WorldMapSessionCoordinator _session;
        private readonly ISessionSaveSlotOperations _saveOperations;
        private ArcanumObjectId _merchant;
        private ArcanumObjectId _corpse;
        private ArcanumObjectId _character;
        private int _pendingSpell = -1;
        private ArcanumObjectId _pendingTechnologyItem;
        private IGameAudioPresentation _audio;
        private int[] _acceptedCharacterAttributes;
        private int[] _acceptedCharacterSkills;
        private int _acceptedHitPointPoints;
        private int _acceptedFatiguePoints;
        private readonly Func<long> _utcNowTicks;
        private long _feedbackExpiresAt;

        public GameUiScreen Screen { get; private set; }
        public GameUiCursorMode CursorMode { get; private set; } = GameUiCursorMode.Default;
        public ArcanumObjectId SelectedItem { get; private set; }
        public ArcanumObjectId SelectedWorldTarget { get; private set; }
        public ArcanumObjectId Corpse => _corpse;
        public ArcanumObjectId MerchantTarget => _merchant;
        public ArcanumObjectId CharacterTarget => _character.IsNull ? Player : _character;
        public bool CharacterReadOnly => CharacterTarget != Player;
        public string Feedback { get; private set; } = string.Empty;
        public CombatUiController Combat { get; }
        public SaveLoadPanelController SaveLoad { get; }
        public CharacterCreationSpecification CreationDraft { get; private set; }
        public int PregeneratedCharacterIndex { get; private set; } = 1;
        public CharacterCreationValidationResult CreationValidation { get; private set; }
        public bool HasPlayer => _session.PlayerState != null;
        public ArcanumObjectId Player => _session.PlayerState?.Identity ?? default;
        public bool IsModalOpen => Screen != GameUiScreen.None;
        public bool BlocksWorldInput => Screen != GameUiScreen.None && Screen != GameUiScreen.Dialogue;
        public GameUiHudPanel HudPanel { get; private set; }
        public SleepLocationMode ActiveSleepMode { get; private set; }
        public bool IsHudPanelOpen => HudPanel != GameUiHudPanel.None;

        public GameUiController(WorldMapSessionCoordinator session, ISessionSaveSlotOperations saveOperations = null,
            CombatUiController combat = null, Func<long> utcNowTicks = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _saveOperations = saveOperations ?? session.SaveSlots;
            Combat = combat ?? new CombatUiController(session);
            _utcNowTicks = utcNowTicks ?? (() => DateTime.UtcNow.Ticks);
            SaveLoad = new SaveLoadPanelController(_saveOperations);
            Screen = session.PlayerState == null ? GameUiScreen.MainMenu : GameUiScreen.None;
        }

        public void BindAudioPresentation(IGameAudioPresentation audio) => _audio = audio;

        public void Refresh()
        {
            Combat.Refresh();
            if (_feedbackExpiresAt > 0 && _utcNowTicks() >= _feedbackExpiresAt)
            {
                Feedback = string.Empty;
                _feedbackExpiresAt = 0;
            }
            if (!HasPlayer)
            {
                HudPanel = GameUiHudPanel.None;
                if (Screen is not (GameUiScreen.MainMenu or GameUiScreen.CharacterCreation
                    or GameUiScreen.PregeneratedCharacter or GameUiScreen.SaveLoad
                    or GameUiScreen.Options))
                    Screen = GameUiScreen.MainMenu;
                return;
            }
            bool dialogue = _session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice;
            if (dialogue && Screen != GameUiScreen.Dialogue)
            {
                SaveLoad.Close();
                Screen = GameUiScreen.Dialogue;
                CursorMode = GameUiCursorMode.Default;
            }
            else if (!dialogue && Screen == GameUiScreen.Dialogue)
                Close();
        }

        public bool Open(GameUiScreen screen)
        {
            Refresh();
            if (screen == GameUiScreen.None) { Close(); return true; }
            if (!HasPlayer && screen is not (GameUiScreen.MainMenu or GameUiScreen.CharacterCreation
                or GameUiScreen.SaveLoad or GameUiScreen.Options)) return Reject("Start or load a game first.");
            if (_session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice
                && screen != GameUiScreen.Dialogue)
                return Reject("Finish the current conversation first.");
            if (_session.Combat.IsActive && _session.Combat.Mode == CombatMode.TurnBased
                && _session.Combat.CurrentParticipant != Player
                && screen is not (GameUiScreen.Dialogue or GameUiScreen.SaveLoad))
                return Reject("The interface is locked while another combatant acts.");
            SaveLoad.Close();
            HudPanel = GameUiHudPanel.None;
            Screen = screen;
            CursorMode = GameUiCursorMode.Default;
            SelectedItem = default;
            SelectedWorldTarget = default;
            if (screen != GameUiScreen.Corpse) _corpse = default;
            _character = screen is GameUiScreen.Character or GameUiScreen.Skills ? Player : default;
            if (screen == GameUiScreen.Character) CaptureAcceptedCharacterEditorState();
            else ClearAcceptedCharacterEditorState();
            Feedback = string.Empty;
            if (screen == GameUiScreen.SaveLoad) SaveLoad.Open(SaveLoadPanelMode.Save);
            ClearPrimaryNotification(screen);
            _audio?.PresentInterface(screen is GameUiScreen.Journal or GameUiScreen.Map
                ? InterfaceAudioCue.BookOpen : InterfaceAudioCue.WindowOpen);
            return true;
        }

        public bool BeginNewGame()
        {
            CharacterCreationCatalog catalog = _session.CharacterCreation.Catalog;
            if (catalog == null) return Reject("Character-creation source data is unavailable.");
            CreationDraft = new CharacterCreationSpecification();
            PortraitDefinition portrait = catalog.Portraits.Values.FirstOrDefault(value =>
                value.Race == CreationDraft.Race && value.Gender == CreationDraft.Gender);
            BackgroundDefinition background = catalog.Backgrounds.Values.OrderBy(value => value.Id)
                .FirstOrDefault(value => value.IsLegal(CreationDraft.Race, CreationDraft.Gender)
                                         && !value.HasUnsupportedEffects);
            if (portrait == null || background == null) return Reject("No legal retail creation choices are available.");
            CreationDraft.PortraitId = portrait.Id;
            CreationDraft.BackgroundId = background.Id;
            Screen = GameUiScreen.CharacterCreation;
            Feedback = string.Empty;
            RefreshCreationValidation();
            return true;
        }

        public bool BeginPregeneratedCharacter()
        {
            IReadOnlyList<PregeneratedCharacterDefinition> values = _session.CharacterCreation.Catalog?.PregeneratedCharacters;
            if (values == null || values.Count == 0)
                return Reject("Pregenerated-character source data is unavailable.");
            CreationDraft = null;
            PregeneratedCharacterIndex = values[0].SourceIndex;
            Screen = GameUiScreen.PregeneratedCharacter;
            Feedback = string.Empty;
            return true;
        }

        public void CyclePregeneratedCharacter(int direction)
        {
            IReadOnlyList<PregeneratedCharacterDefinition> values = _session.CharacterCreation.Catalog?.PregeneratedCharacters;
            if (Screen != GameUiScreen.PregeneratedCharacter || values == null || values.Count == 0) return;
            int index = values.ToList().FindIndex(value => value.SourceIndex == PregeneratedCharacterIndex);
            index = (index + direction) % values.Count;
            if (index < 0) index += values.Count;
            PregeneratedCharacterIndex = values[index].SourceIndex;
            Feedback = string.Empty;
        }

        public bool FinalizePregeneratedCharacter()
        {
            if (Screen != GameUiScreen.PregeneratedCharacter)
                return Reject("No pregenerated character is selected.");
            CharacterCreationFinalizeResult result = _session.CharacterCreation
                .FinalizePregeneratedCharacter(PregeneratedCharacterIndex);
            if (!result.Succeeded) return Reject(result.Message ?? result.Failure.ToString());
            Screen = GameUiScreen.None;
            Feedback = string.Empty;
            return true;
        }

        public void SetCreationIdentity(CharacterRace race, CharacterGender gender)
        {
            if (CreationDraft == null) return;
            CreationDraft.Race = race;
            CreationDraft.Gender = gender;
            CharacterCreationCatalog catalog = _session.CharacterCreation.Catalog;
            PortraitDefinition portrait = catalog?.FindFirstPortrait(race, gender);
            if (portrait != null) CreationDraft.PortraitId = portrait.Id;
            // mainmenu_ui_new_char_set_gender/set_race always clear the prior background before refreshing the
            // newly legal identity. Re-evaluate from the first source-ordered legal entry even when the old entry
            // happened to be valid for both identities.
            if (catalog != null)
            {
                BackgroundDefinition legal = catalog.Backgrounds.Values.OrderBy(value => value.Id)
                    .FirstOrDefault(value => value.IsLegal(race, gender) && !value.HasUnsupportedEffects);
                if (legal != null) CreationDraft.BackgroundId = legal.Id;
            }
            RefreshCreationValidation();
        }

        public void SetCreationName(string name)
        {
            if (CreationDraft == null) return;
            CreationDraft.Name = name ?? string.Empty;
            RefreshCreationValidation();
        }

        public void SetCreationBackground(int id)
        {
            if (CreationDraft == null) return;
            CreationDraft.BackgroundId = id;
            RefreshCreationValidation();
        }

        public void SetCreationPortrait(int id)
        {
            if (CreationDraft == null) return;
            CreationDraft.PortraitId = id;
            RefreshCreationValidation();
        }

        public void AdjustCreationAttribute(CharacterAttribute attribute, int delta)
        {
            if (CreationDraft == null) return;
            CreationDraft.SetAttribute(attribute, CreationDraft.GetAttribute(attribute) + delta);
            RefreshCreationValidation();
        }

        public void AdjustCreationSkill(CharacterSkill skill, int delta)
        {
            if (CreationDraft == null) return;
            CreationDraft.SetSkillPoints(skill, CreationDraft.GetSkillPoints(skill) + delta);
            RefreshCreationValidation();
        }

        public void AdjustCreationSpell(SpellCollege college, int delta)
        {
            if (CreationDraft == null) return;
            CreationDraft.SetSpellRank(college, CreationDraft.GetSpellRank(college) + delta);
            RefreshCreationValidation();
        }

        public void AdjustCreationTechnology(TechnologyDiscipline discipline, int delta)
        {
            if (CreationDraft == null) return;
            CreationDraft.SetTechnologyRank(discipline, CreationDraft.GetTechnologyRank(discipline) + delta);
            RefreshCreationValidation();
        }

        public void AdjustCreationVitality(bool hitPoints, int delta)
        {
            if (CreationDraft == null || delta is not (-1 or 1)) return;
            if (hitPoints)
                CreationDraft.HitPointPoints = Math.Max(0, CreationDraft.HitPointPoints + delta);
            else
                CreationDraft.FatiguePoints = Math.Max(0, CreationDraft.FatiguePoints + delta);
            RefreshCreationValidation();
        }

        public bool FinalizeNewGame()
        {
            if (CreationDraft == null) return Reject("No character is being created.");
            CharacterCreationFinalizeResult result = _session.CharacterCreation.FinalizeNewGame(CreationDraft);
            if (!result.Succeeded) return Reject(result.Message ?? result.Failure.ToString());
            CreationValidation = default;
            if (_session.CharacterCreation.StartingEquipmentActive)
            {
                _merchant = _session.CharacterCreation.StartingEquipmentMerchant;
                Screen = GameUiScreen.StartingEquipment;
            }
            else
            {
                CreationDraft = null;
                Screen = GameUiScreen.None;
            }
            Feedback = string.Empty;
            return true;
        }

        public bool CompleteStartingEquipment()
        {
            if (Screen != GameUiScreen.StartingEquipment)
                return Reject("No starting-equipment transaction is active.");
            if (!_session.CharacterCreation.CompleteStartingEquipment(out string failure))
                return Reject(failure);
            CreationDraft = null;
            _merchant = default;
            Screen = GameUiScreen.None;
            Feedback = string.Empty;
            return true;
        }

        public bool ReturnToCharacterEditor()
        {
            if (Screen != GameUiScreen.StartingEquipment || CreationDraft == null)
                return Reject("No finalized character can return to the Character Editor.");
            if (!_session.CharacterCreation.CancelStartingEquipment(out string failure))
                return Reject(failure);
            _merchant = default;
            Screen = GameUiScreen.CharacterCreation;
            RefreshCreationValidation();
            return true;
        }

        private void RefreshCreationValidation()
        {
            CreationValidation = _session.CharacterCreation.Validate(CreationDraft);
            Feedback = CreationValidation.Succeeded ? string.Empty : CreationValidation.Message ?? string.Empty;
        }

        public void Close()
        {
            if (HudPanel != GameUiHudPanel.None)
            {
                HudPanel = GameUiHudPanel.None;
                Feedback = string.Empty;
                return;
            }
            bool wasOpen = Screen != GameUiScreen.None;
            SaveLoad.Close();
            Screen = GameUiScreen.None;
            CursorMode = GameUiCursorMode.Default;
            SelectedItem = default;
            SelectedWorldTarget = default;
            _merchant = default;
            _corpse = default;
            _character = default;
            _pendingSpell = -1;
            _pendingTechnologyItem = default;
            ClearAcceptedCharacterEditorState();
            Feedback = string.Empty;
            if (HasPlayer) CreationDraft = null;
            if (wasOpen) _audio?.PresentInterface(InterfaceAudioCue.WindowClose);
        }

        public void ShowFeedback(string message)
        {
            Feedback = message ?? string.Empty;
            _feedbackExpiresAt = string.IsNullOrWhiteSpace(Feedback)
                ? 0 : _utcNowTicks() + TimeSpan.FromSeconds(3).Ticks;
        }

        public bool ToggleSkillsPanel()
        {
            if (!HasPlayer) return Reject("Start or load a game first.");
            if (Screen != GameUiScreen.None) Close();
            HudPanel = HudPanel == GameUiHudPanel.Skills ? GameUiHudPanel.None : GameUiHudPanel.Skills;
            Feedback = string.Empty;
            return true;
        }

        public bool ActivateSourceSkillAction(int sourceIndex)
        {
            string action = sourceIndex switch
            {
                0 => "Prowling",
                1 => "Pick Pocket",
                2 => "Disarm Traps",
                3 => "Repair",
                _ => null,
            };
            return action == null
                ? Reject("Unknown source skill action.")
                : Reject($"{action} targeting is not available in the current runtime.");
        }

        public bool AutoSave()
        {
            SessionSaveSlotResult result = _saveOperations.SaveSlot("auto");
            return Complete(result.Succeeded, result.Succeeded ? "Auto-saved." : result.Message ?? result.Failure.ToString());
        }

        public bool AutoLoad()
        {
            SessionSaveSlotResult result = _saveOperations.LoadSlot("auto");
            if (result.Succeeded)
            {
                HudPanel = GameUiHudPanel.None;
                Refresh();
            }
            return Complete(result.Succeeded, result.Succeeded ? "Auto-save loaded." : result.Message ?? result.Failure.ToString());
        }

        public bool ToggleCombatMode()
        {
            CombatResult result = _session.Combat.ToggleCombatReadiness(Player);
            Combat.Refresh();
            if (result.Succeeded)
            {
                CursorMode = _session.Combat.IsCombatModeActive ? GameUiCursorMode.Attack : GameUiCursorMode.Default;
                _session.SetCombatReadyPresentation(Player, _session.Combat.IsCombatModeActive);
            }
            return Complete(result.Succeeded, result.Succeeded
                ? (_session.Combat.IsCombatModeActive ? "Combat ready." : "Combat mode ended.")
                : result.Failure.ToString());
        }

        public bool ToggleAttackTalkMode()
        {
            if (_session.Combat.IsActive)
            {
                CombatResult ended = _session.Combat.EndCombat(Player);
                Combat.Refresh();
                return Complete(ended.Succeeded, ended.Succeeded ? "Talk mode." : ended.Failure.ToString());
            }
            CursorMode = CursorMode == GameUiCursorMode.Attack ? GameUiCursorMode.Default : GameUiCursorMode.Attack;
            Feedback = CursorMode == GameUiCursorMode.Attack ? "Attack mode." : "Talk mode.";
            return true;
        }

        public bool StartAttack(ArcanumObjectId target, bool forceAttack)
        {
            if (!target.IsPersistent) return Reject("Select a valid attack target.");
            if (!_session.Combat.IsActive)
            {
                CombatResult started = _session.Combat.StartCombat(Player, target, CombatMode.TurnBased, forceAttack);
                if (!started.Succeeded) return Reject(started.Failure.ToString());
            }
            Combat.Refresh();
            bool selected = Combat.SelectTarget(target);
            if (selected) CursorMode = GameUiCursorMode.Attack;
            return selected;
        }

        public bool AssignQuickSlotItem(int index, ArcanumObjectId item)
        {
            bool assigned = _session.Shortcuts.AssignItem(index, item);
            return Complete(assigned, assigned ? $"Assigned item to slot {SlotLabel(index)}."
                : "That item cannot be assigned.");
        }

        public bool AssignQuickSlotSpell(int index, int spellId)
        {
            bool assigned = _session.Shortcuts.AssignSpell(index, spellId);
            return Complete(assigned, assigned ? $"Assigned spell to slot {SlotLabel(index)}."
                : "That spell cannot be assigned.");
        }

        public bool ActivateQuickSlot(int index)
        {
            QuickSlotBinding binding = _session.Shortcuts.Get(index);
            bool succeeded;
            ArcanumObjectId resolvedItem = default;
            switch (binding.Kind)
            {
                case QuickSlotKind.Item when _session.Shortcuts.TryResolveItem(index, out ArcanumObjectId item):
                    resolvedItem = item;
                    if (!_session.TryGetObjectState(item, out PersistentObjectState state))
                        return Reject("The assigned item is unavailable.");
                    succeeded = PhaseOneTechnologyCatalog.TryGetItem(state.PrototypeNumber, out _)
                        ? UseTechnology(item, Player)
                        : WorldMapSessionCoordinator.TryGetNaturalWornLocation(state, out _) && Equip(item);
                    break;
                case QuickSlotKind.Spell when PhaseOneSpellCatalog.TryGet(binding.SourceId, out SpellDefinition spell):
                    if (spell.AllowsSelf) succeeded = CastSpell(binding.SourceId, Player);
                    else { BeginSpellTargeting(binding.SourceId); succeeded = true; }
                    break;
                case QuickSlotKind.Empty:
                    return Reject($"Quick slot {SlotLabel(index)} is empty.");
                default:
                    return Reject("The assigned action is unavailable.");
            }
            if (succeeded)
            {
                _session.Shortcuts.MarkActivated(index);
                _session.GameplayHud.RecordRecentAction(binding, resolvedItem);
            }
            return succeeded;
        }

        public bool ActivateRecentAction(int index = 0)
        {
            if (index < 0 || index >= _session.GameplayHud.RecentActions.Count)
                return Reject("That recent action is unavailable.");
            RecentActionBinding binding = _session.GameplayHud.RecentActions[index];
            switch (binding.Kind)
            {
                case RecentActionKind.Item when _session.GameplayHud.TryResolveRecentItem(index, out ArcanumObjectId item):
                    if (!_session.TryGetObjectState(item, out PersistentObjectState state))
                        return Reject("The recent item is unavailable.");
                    bool itemResult = PhaseOneTechnologyCatalog.TryGetItem(state.PrototypeNumber, out _)
                        ? UseTechnology(item, Player)
                        : WorldMapSessionCoordinator.TryGetNaturalWornLocation(state, out _) && Equip(item);
                    if (itemResult) _session.GameplayHud.RecordRecentAction(
                        new QuickSlotBinding(QuickSlotKind.Item, item, state.PrototypeNumber), item);
                    return itemResult;
                case RecentActionKind.Spell when PhaseOneSpellCatalog.TryGet(binding.SourceId, out SpellDefinition spell):
                    bool spellResult;
                    if (spell.AllowsSelf) spellResult = CastSpell(spell.Id, Player);
                    else { BeginSpellTargeting(spell.Id); spellResult = true; }
                    if (spellResult) _session.GameplayHud.RecordRecentAction(
                        new QuickSlotBinding(QuickSlotKind.Spell, default, spell.Id));
                    return spellResult;
                case RecentActionKind.Skill:
                    return Reject("That source skill action is not available in the current runtime.");
                default:
                    return Reject("The recent action is unavailable.");
            }
        }

        public bool ToggleFatePanel()
        {
            if (HudPanel == GameUiHudPanel.Fate) { HudPanel = GameUiHudPanel.None; return true; }
            if (!HasPlayer || !_session.Vitality.IsAlive(Player)) return Reject("Fate is unavailable.");
            CloseScreenForHudPanel();
            HudPanel = GameUiHudPanel.Fate;
            Feedback = string.Empty;
            return true;
        }

        public bool ActivateFate(FateChoice choice)
        {
            FateResult result = _session.GameplayHud.ActivateFate(choice);
            return Complete(result.Succeeded, result.Succeeded ? "Fate invoked." : result.Failure switch
            {
                FateFailure.InsufficientPoints => "No Fate Points remain.",
                FateFailure.UnsupportedDeferredEffect => "That deferred Fate effect has no exact runtime resolution path.",
                _ => result.Failure.ToString(),
            });
        }

        public bool ToggleSleepPanel()
        {
            if (HudPanel == GameUiHudPanel.Sleep)
            { HudPanel = GameUiHudPanel.None; ActiveSleepMode = SleepLocationMode.Unavailable; return true; }
            SleepFailure preview = _session.GameplayHud.PreviewSleep(out SleepLocationMode mode);
            if (preview != SleepFailure.None) return Reject(SleepFailureMessage(preview));
            CloseScreenForHudPanel();
            ActiveSleepMode = mode;
            HudPanel = GameUiHudPanel.Sleep;
            Feedback = string.Empty;
            return true;
        }

        public bool Sleep(SleepOption option)
        {
            SleepResult result = _session.GameplayHud.Sleep(option);
            if (!result.Succeeded) return Reject(SleepFailureMessage(result.Failure));
            HudPanel = GameUiHudPanel.None;
            ActiveSleepMode = SleepLocationMode.Unavailable;
            string verb = result.Mode == SleepLocationMode.TownWait ? "Waited" : "Rested";
            return Complete(true, $"{verb} for {result.HoursAdvanced} hour{(result.HoursAdvanced == 1 ? string.Empty : "s")}.");
        }

        private static string SlotLabel(int index) => index == 9 ? "0" : (index + 1).ToString();

        public void RebuildPresentation()
        {
            GameUiScreen previous = Screen;
            ArcanumObjectId previousCorpse = _corpse;
            Close();
            Combat.ResetTransient();
            if (!HasPlayer || previous == GameUiScreen.None) return;
            if (previous == GameUiScreen.Corpse && !previousCorpse.IsNull)
                BeginCorpseLoot(previousCorpse);
            else Open(previous);
        }

        public GameUiHudView ProjectHud()
        {
            if (!HasPlayer || !_session.Vitality.TryGet(Player, out PersistentCharacterVitalityState vitality))
                return null;
            string weapon = "Unarmed";
            int ammunition = 0;
            int contextQuantity = _session.GetGold(Player);
            int contextIcon = 474;
            if (_session.TryGetEquippedItem(Player, WornLocation.Weapon, out PersistentObjectState item))
            {
                weapon = ItemName(item);
                if (item.WeaponData?.UsesAmmo == true
                    && _session.TryGetAmmo(Player, item.WeaponData.AmmoType, 1, out PersistentObjectState ammo))
                {
                    ammunition = ammo.StackQuantity.GetValueOrDefault();
                    contextQuantity = ammunition;
                    contextIcon = item.WeaponData.AmmoType is >= 0 and <= 3
                        ? 250 + item.WeaponData.AmmoType : 474;
                }
                else if (item.WeaponData?.UsesAmmo == false)
                {
                    int mana = _session.ResolvePrototype(item.PrototypeNumber)?.SpellMana ?? 0;
                    if (mana > 0) { contextQuantity = mana; contextIcon = 469; }
                }
            }
            string readiness = "READY";
            if (!_session.Combat.IsActive && _session.Combat.IsCombatModeActive)
                readiness = "READY";
            else if (_session.Combat.IsActive && _session.Combat.Mode == CombatMode.TurnBased)
                readiness = _session.Combat.CurrentParticipant == Player ? "READY" : "WAIT";
            else if (_session.Combat.IsActive)
                readiness = Combat.IsRealTimeBusy ? "BUSY" : Combat.IsRealTimeReady ? "READY" : "RECOVERING";
            PersistentCharacterProgressionState progression = _session.Progression.Get(Player);
            int gauge = ExperienceGaugeValue(progression);
            bool worldMap = _session.TryGetCurrentMapId(out int currentMapId) && currentMapId == 1;
            return new GameUiHudView
            {
                HitPoints = vitality.CurrentHitPoints, MaximumHitPoints = vitality.MaximumHitPoints,
                Fatigue = vitality.CurrentFatigue, MaximumFatigue = vitality.MaximumFatigue,
                Weapon = weapon, Ammunition = ammunition,
                CombatActive = _session.Combat.IsCombatModeActive, CombatMode = _session.Combat.Mode,
                ActionPoints = _session.Combat.IsActive ? _session.Combat.CurrentActionPoints : 0,
                MaximumActionPoints = _session.Combat.IsActive ? _session.Combat.MaximumActionPoints : 0,
                Readiness = readiness,
                MaintainedSpellSlotCapacity = Math.Clamp(_session.Characters.GetEffectiveAttribute(
                    Player, CharacterAttribute.Intelligence) / 4, 0, 5),
                ActiveEffects = _session.Magic.ActiveEffects.Where(value => value.Caster == Player
                        && PhaseOneSpellCatalog.TryGet(value.SpellId, out SpellDefinition spell) && spell.Maintained)
                    .OrderBy(value => value.Id).ToArray(),
                FatePoints = _session.GameplayHud.FatePoints,
                ContextQuantity = contextQuantity,
                ContextIconSourceId = contextIcon,
                ExperienceGaugeValue = gauge,
                SourceTimeMilliseconds = _session.SourceTime.ElapsedMilliseconds,
                UsesWorldMapButton = worldMap,
                PrimaryNotifications = _session.GameplayHud.Notifications,
                RecentActions = _session.GameplayHud.RecentActions.ToArray(),
            };
        }

        private static int ExperienceGaugeValue(PersistentCharacterProgressionState progression)
        {
            if (progression.Level < 1) return 0;
            if (progression.Level >= CharacterProgressionService.MaximumPlayableLevel) return 0;
            int current = CharacterProgressionService.GetExperienceForLevel(progression.Level);
            int next = CharacterProgressionService.GetExperienceForLevel(progression.Level + 1);
            if (next <= current) return 999;
            int progress = Math.Clamp(1000 * (progression.Experience - current) / (next - current), 0, 999);
            return 11 * progress / 10;
        }

        private void CloseScreenForHudPanel()
        {
            if (Screen == GameUiScreen.None) return;
            SaveLoad.Close();
            Screen = GameUiScreen.None;
            CursorMode = GameUiCursorMode.Default;
            SelectedItem = default;
            SelectedWorldTarget = default;
        }

        private void ClearPrimaryNotification(GameUiScreen screen)
        {
            HudPrimaryNotification notification = screen switch
            {
                GameUiScreen.Character => HudPrimaryNotification.Character,
                GameUiScreen.Journal => HudPrimaryNotification.Logbook,
                GameUiScreen.Map => HudPrimaryNotification.TownMap | HudPrimaryNotification.WorldMap,
                GameUiScreen.Inventory => HudPrimaryNotification.Inventory,
                _ => HudPrimaryNotification.None,
            };
            if (notification != HudPrimaryNotification.None) _session.GameplayHud.ClearNotification(notification);
        }

        private static string SleepFailureMessage(SleepFailure failure) => failure switch
        {
            SleepFailure.Dead => "The dead cannot sleep.",
            SleepFailure.Unconscious => "You are unconscious already.",
            SleepFailure.CombatActive => "You cannot sleep with enemies near.",
            SleepFailure.UnsupportedLocation => "You cannot wait here.",
            SleepFailure.InvalidOption => "That rest option is unavailable here.",
            _ => failure.ToString(),
        };

        public IReadOnlyList<GameUiItemView> ProjectInventory(ArcanumObjectId owner = default)
        {
            if (owner.IsNull) owner = Player;
            return _session.States.Values.Where(value => value.ParentIdentity == owner
                    && value.Placement.Kind is ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped)
                .OrderBy(value => value.Placement.Kind == ObjectPlacementKind.Equipped ? 0 : 1)
                .ThenBy(value => value.InventoryLocation).ThenBy(value => value.Identity.Key, StringComparer.Ordinal)
                .Select(ProjectItem).ToArray();
        }

        public GameUiWorldTargetView ProjectWorldTarget(ArcanumObjectId identity)
        {
            if (!_session.TryGetObjectState(identity, out PersistentObjectState state)) return null;
            if (state.Type is not (ObjectType.Pc or ObjectType.Npc))
            {
                GameUiItemInspectionView item = ProjectItemInspection(identity);
                return item == null ? null : new GameUiWorldTargetView
                {
                    Identity = identity,
                    Name = item.Name,
                    Type = state.Type,
                    Item = item,
                };
            }
            if (!_session.Characters.TryGet(identity, out PersistentCharacterState character)) return null;
            bool dead = _session.Vitality.TryGet(identity, out _) && _session.Vitality.IsDead(identity);
            bool hostile = state.Type == ObjectType.Npc && HasPlayer && !dead
                           && (_session.Social.IsRememberedHostile(identity, Player)
                               || _session.Social.IsReactionHostile(identity, Player)
                               || _session.Social.IsAlignmentHostile(identity, Player));
            return new GameUiWorldTargetView
            {
                Identity = identity,
                Name = _session.ResolveDisplayName(state),
                Type = state.Type,
                Dead = dead,
                Hostile = hostile,
                IconSourceId = character.Race switch
                {
                    CharacterRace.Human => 375,
                    CharacterRace.Dwarf => 378,
                    CharacterRace.Elf => 376,
                    CharacterRace.HalfElf => 377,
                    CharacterRace.Gnome => 379,
                    CharacterRace.Halfling => 380,
                    CharacterRace.HalfOrc => 381,
                    CharacterRace.HalfOgre => 382,
                    _ => 375,
                },
            };
        }

        public GameUiItemInspectionView ProjectItemInspection(ArcanumObjectId identity)
        {
            if (!_session.TryGetObjectState(identity, out PersistentObjectState state)
                || state.Type is ObjectType.Pc or ObjectType.Npc) return null;
            var lines = new List<string>();
            if (state.WeaponData != null)
            {
                int normal = (int)DamageType.Normal;
                int fatigue = (int)DamageType.Fatigue;
                lines.Add($"D:{state.WeaponData.DamageMin[normal]}-{state.WeaponData.DamageMax[normal]}");
                lines.Add($"FT:{state.WeaponData.DamageMin[fatigue]}-{state.WeaponData.DamageMax[fatigue]}");
                lines.Add($"Speed:{state.WeaponData.SpeedFactor}");
            }
            ObjectProtoInfo prototype = _session.ResolvePrototype(state.PrototypeNumber);
            if (state.Type == ObjectType.Armor && prototype != null)
            {
                lines.Add($"AC:{prototype.ArmorAc.GetValueOrDefault()}");
                int[] resistance = prototype.ArmorResist;
                if (resistance != null && resistance.Length > 0)
                    lines.Add($"DR:{resistance[0]}");
            }
            lines.Add($"Weight: {state.UnitWeight} stone");
            if (state.MaximumHitPoints > 0)
                lines.Add($"{Math.Max(0, state.MaximumHitPoints - state.HitPointDamage)}/{state.MaximumHitPoints}");
            if (state.StackQuantity.GetValueOrDefault() > 1)
                lines.Add($"Quantity: {state.StackQuantity.Value}");
            return new GameUiItemInspectionView
            {
                Name = ItemName(state),
                InventoryArtId = state.InventoryArtId,
                Lines = lines,
            };
        }

        public GameUiInventorySummaryView ProjectInventorySummary(ArcanumObjectId owner = default)
        {
            if (owner.IsNull) owner = Player;
            if (!owner.IsPersistent || !_session.Characters.TryGet(owner, out _)) return null;
            long weight = _session.InventoryCapacity.GetInventoryLoad(owner);
            int capacity = _session.InventoryCapacity.GetCarryCapacity(owner);
            int[] ratios = { 15, 30, 45, 60, 75, 90, 100 };
            int level = ratios.Length - 1;
            for (int index = 0; index < ratios.Length; index++)
                if (weight < (long)capacity * ratios[index] / 100)
                {
                    level = index;
                    break;
                }
            var ammunition = new int[4];
            foreach (PersistentObjectState item in _session.States.Values)
                if (item.ParentIdentity == owner && item.Placement.Kind == ObjectPlacementKind.Contained
                    && item.Type == ObjectType.Ammo && item.AmmoItemType is >= 0 and <= 3)
                    ammunition[item.AmmoItemType.Value] = checked(ammunition[item.AmmoItemType.Value]
                                                               + item.StackQuantity.GetValueOrDefault());
            return new GameUiInventorySummaryView
            {
                Weight = weight,
                CarryCapacity = capacity,
                EncumbranceLevel = level,
                EncumbranceThreshold = ratios[level] * capacity / 100,
                Speed = _session.DerivedStats.GetDerivedStat(owner, CharacterDerivedStat.Speed),
                TotalAttack = ProjectTotalAttack(owner),
                TotalDefense = ProjectTotalDefense(owner),
                Gold = _session.GetGold(owner),
                Ammunition = ammunition,
            };
        }

        public bool MoveInventoryItem(ArcanumObjectId item, int location)
        {
            bool moved = _session.TryMoveItemToInventory(Player, item, location);
            string name = _session.TryGetObjectState(item, out PersistentObjectState state)
                ? ItemName(state) : "item";
            return Complete(moved, moved ? $"Moved {name}." : $"{name} does not fit there.");
        }

        public bool ArrangeInventory()
            => Complete(_session.ArrangeInventory(Player), "Inventory arranged.");

        public bool BeginCorpseLoot(ArcanumObjectId corpse)
        {
            if (!_session.TryGetObjectState(corpse, out PersistentObjectState state)
                || state.Type != ObjectType.Npc || !_session.TryGetLoadedObject(corpse, out _)
                || !_session.Vitality.TryGet(corpse, out _) || !_session.Vitality.IsDead(corpse))
                return Reject("That corpse is unavailable.");
            if (!Open(GameUiScreen.Corpse)) return false;
            _corpse = corpse;
            return true;
        }

        public IReadOnlyList<GameUiItemView> ProjectCorpseInventory()
            => _corpse.IsNull ? Array.Empty<GameUiItemView>() : ProjectInventory(_corpse);

        public bool LootCorpseItem(ArcanumObjectId item)
        {
            if (_corpse.IsNull) return Reject("No corpse is open.");
            string name = _session.TryGetObjectState(item, out PersistentObjectState state)
                ? ItemName(state) : "item";
            CorpseLootResult result = _session.DeathConsequences.LootItem(_corpse, Player, item);
            return Complete(result.Succeeded,
                result.Succeeded ? $"Looted {name}." : result.Failure.ToString());
        }

        public bool SelectItem(ArcanumObjectId item)
        {
            if (!_session.TryGetObjectState(item, out PersistentObjectState state)
                || state.ParentIdentity != Player
                || state.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped))
                return Reject("That item is not owned by the player.");
            SelectedItem = item;
            CursorMode = GameUiCursorMode.Item;
            Feedback = ItemName(state);
            return true;
        }

        public bool InspectItem(ArcanumObjectId item)
        {
            if (!_session.TryGetObjectState(item, out PersistentObjectState state)
                || state.ParentIdentity != Player
                || state.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped))
                return false;
            Feedback = ItemName(state);
            return true;
        }

        public bool Equip(ArcanumObjectId item)
        {
            if (!_session.TryGetObjectState(item, out PersistentObjectState state)
                || !WorldMapSessionCoordinator.TryGetNaturalWornLocation(state, out WornLocation location))
                return Reject("That item cannot be equipped.");
            EquipmentTransactionResult result = _session.EquipItem(Player, item, location);
            return Complete(result.Succeeded, result.Succeeded ? $"Equipped {ItemName(state)}." : result.Code.ToString());
        }

        public bool Unequip(WornLocation location)
        {
            EquipmentTransactionResult result = _session.UnequipItem(Player, location);
            return Complete(result.Succeeded, result.Succeeded ? $"Unequipped {location}." : result.Code.ToString());
        }

        public bool MoveItem(ArcanumObjectId item, ArcanumObjectId destinationOwner)
        {
            if (!_session.TryGetObjectState(item, out PersistentObjectState state)) return Reject("Item not found.");
            InventoryTransferResult result = _session.TransferItem(item, state.Placement,
                ObjectPlacement.ContainedBy(destinationOwner));
            return Complete(result.Succeeded, result.Succeeded ? "Item moved." : result.Code.ToString());
        }

        public GameUiCharacterView ProjectCharacter()
        {
            ArcanumObjectId target = CharacterTarget;
            if (!HasPlayer || target.IsNull
                || !_session.Characters.TryGet(target, out PersistentCharacterState character)
                || !_session.Progression.TryGet(target, out PersistentCharacterProgressionState progression)) return null;
            bool isPlayer = target == Player;
            FinalizedCharacterSpecification finalized = isPlayer ? _session.CharacterCreation.Finalized : null;
            int nextExperience = progression.Level >= CharacterProgressionService.MaximumPlayableLevel
                ? progression.Experience
                : CharacterProgressionService.GetExperienceForLevel(progression.Level + 1);
            _session.Vitality.TryGet(target, out PersistentCharacterVitalityState vitality);
            return new GameUiCharacterView
            {
                Name = isPlayer ? finalized?.Name ?? "Player" : ObjectName(target),
                Race = character.Race, Gender = character.Gender,
                Level = progression.Level, Experience = progression.Experience,
                ExperienceToNextLevel = Math.Max(0, nextExperience - progression.Experience),
                CharacterPoints = progression.UnspentCharacterPoints,
                Background = finalized?.Background?.Name ?? string.Empty,
                PortraitId = finalized?.PortraitId ?? -1,
                Age = finalized?.Age ?? 0,
                HitPoints = vitality?.CurrentHitPoints ?? 0,
                MaximumHitPoints = vitality?.MaximumHitPoints ?? 0,
                Fatigue = vitality?.CurrentFatigue ?? 0,
                MaximumFatigue = vitality?.MaximumFatigue ?? 0,
                Alignment = _session.DerivedStats.GetAlignment(target),
                Aptitude = _session.DerivedStats.GetDerivedStat(target, CharacterDerivedStat.MagickTechAptitude),
                ArmorClass = _session.DerivedStats.GetArmorClass(target),
                CarriedWeight = _session.InventoryCapacity.GetInventoryLoad(target),
                CarryCapacity = _session.InventoryCapacity.GetCarryCapacity(target),
                Attributes = Enum.GetValues(typeof(CharacterAttribute)).Cast<CharacterAttribute>()
                    .Select(value => new GameUiAttributeView(value,
                        _session.Characters.GetBaseAttribute(target, value),
                        _session.Characters.GetEffectiveAttribute(target, value))).ToArray(),
                Skills = CharacterSkillRules.AllSkills.Select(value => new GameUiSkillView(value,
                    _session.Progression.GetPurchasedSkillPoints(target, value),
                    _session.Progression.GetEffectiveSkillRank(target, value),
                    _session.Progression.GetTrainingLevel(target, value))).ToArray(),
                DerivedStats = CharacterDerivedStatRules.AllStats.Select(value => new GameUiDerivedStatView(value,
                    _session.DerivedStats.GetDerivedStat(target, value))).ToArray(),
                Resistances = CharacterDerivedStatRules.AllResistances.Select(value => new GameUiResistanceView(value,
                    _session.DerivedStats.GetResistance(target, value))).ToArray(),
            };
        }

        public bool AdjustAttribute(CharacterAttribute attribute, int delta)
        {
            if (CharacterReadOnly) return Reject("This character examination is read-only.");
            EnsureAcceptedCharacterEditorState();
            RuntimeCharacterAdjustmentResult result = _session.Progression.AdjustAttribute(Player, attribute, delta,
                _acceptedCharacterAttributes[(int)attribute]);
            return Complete(result == RuntimeCharacterAdjustmentResult.Success,
                result == RuntimeCharacterAdjustmentResult.Success ? $"Adjusted {attribute}." : result.ToString());
        }

        public bool IncreaseSkill(CharacterSkill skill)
        {
            if (CharacterReadOnly) return Reject("This character examination is read-only.");
            SkillIncreaseResult result = _session.Progression.IncreaseSkill(Player, skill);
            return Complete(result == SkillIncreaseResult.Success,
                result == SkillIncreaseResult.Success ? $"Increased {skill}." : result.ToString());
        }

        public bool DecreaseSkill(CharacterSkill skill)
        {
            if (CharacterReadOnly) return Reject("This character examination is read-only.");
            EnsureAcceptedCharacterEditorState();
            RuntimeCharacterAdjustmentResult result = _session.Progression.DecreaseSkill(Player, skill,
                _acceptedCharacterSkills[(int)skill]);
            return Complete(result == RuntimeCharacterAdjustmentResult.Success,
                result == RuntimeCharacterAdjustmentResult.Success ? $"Decreased {skill}." : result.ToString());
        }

        public bool AdjustVitalityPoint(bool hitPoints, int delta)
        {
            if (CharacterReadOnly) return Reject("This character examination is read-only.");
            if (delta is not (-1 or 1)) return Reject("Invalid vitality adjustment.");
            EnsureAcceptedCharacterEditorState();
            PersistentCharacterVitalityState vitality = _session.Vitality.Get(Player);
            int current = hitPoints ? vitality.Source.HitPointPoints : vitality.Source.FatiguePoints;
            int accepted = hitPoints ? _acceptedHitPointPoints : _acceptedFatiguePoints;
            if (delta < 0 && current <= accepted) return Reject(RuntimeCharacterAdjustmentResult.AcceptedBaseline.ToString());
            if (delta > 0 && _session.Progression.GetUnspentCharacterPoints(Player) < 1)
                return Reject(RuntimeCharacterAdjustmentResult.InsufficientCharacterPoints.ToString());
            _session.Vitality.AdjustPurchasedPoint(Player, hitPoints, delta);
            if (delta > 0) _session.Progression.SpendCharacterPoint(Player);
            else _session.Progression.RefundCharacterPoint(Player);
            return Complete(true, $"Adjusted {(hitPoints ? "Hit Points" : "Fatigue")}.");
        }

        private void CaptureAcceptedCharacterEditorState()
        {
            if (!HasPlayer || !_session.Characters.TryGet(Player, out _)
                || !_session.Progression.TryGet(Player, out _)) return;
            _acceptedCharacterAttributes = CharacterStatService.AllAttributes
                .Select(value => _session.Characters.GetBaseAttribute(Player, value)).ToArray();
            _acceptedCharacterSkills = CharacterSkillRules.AllSkills
                .Select(value => _session.Progression.GetPurchasedSkillPoints(Player, value)).ToArray();
            PersistentCharacterVitalityState vitality = _session.Vitality.Get(Player);
            _acceptedHitPointPoints = vitality.Source.HitPointPoints;
            _acceptedFatiguePoints = vitality.Source.FatiguePoints;
        }

        private void EnsureAcceptedCharacterEditorState()
        {
            if (_acceptedCharacterAttributes == null || _acceptedCharacterSkills == null)
                CaptureAcceptedCharacterEditorState();
        }

        private void ClearAcceptedCharacterEditorState()
        {
            _acceptedCharacterAttributes = null;
            _acceptedCharacterSkills = null;
            _acceptedHitPointPoints = 0;
            _acceptedFatiguePoints = 0;
        }

        public IReadOnlyList<GameUiSpellView> ProjectSpells(ArcanumObjectId target = default)
        {
            if (target.IsNull) target = Player;
            return PhaseOneSpellCatalog.All.OrderBy(value => value.Id).Select(value => new GameUiSpellView
            {
                Definition = value,
                Learned = _session.Magic.KnowsSpell(Player, value.Id),
                PreviewFailure = _session.Magic.Preview(new SpellCastRequest(Player, value.Id, target)).Failure,
            }).ToArray();
        }

        public void BeginSpellTargeting(int spellId)
        {
            _pendingSpell = spellId;
            CursorMode = GameUiCursorMode.SpellTarget;
            Feedback = "Select a spell target.";
        }

        public bool CastSpell(int spellId, ArcanumObjectId target)
        {
            var request = new SpellCastRequest(Player, spellId, target);
            if (_session.Combat.IsActive && _session.Combat.Mode == CombatMode.RealTime)
            {
                CombatResult scheduled = _session.Combat.ScheduleRealTimeSpell(request);
                return Complete(scheduled.Succeeded, scheduled.Succeeded ? "Spell scheduled." : scheduled.Failure.ToString());
            }
            SpellCastResult result = _session.Magic.Cast(request);
            _audio?.PresentSpellCast(request, result.Succeeded);
            return Complete(result.Succeeded, result.Succeeded ? "Spell cast." : result.Failure.ToString());
        }

        public bool CancelEffect(long effectId)
        {
            int spellId = -1;
            foreach (ActiveSpellEffect effect in _session.Magic.ActiveEffects)
                if (effect.Id == effectId) { spellId = effect.SpellId; break; }
            bool cancelled = _session.Magic.CancelMaintainedEffect(Player, effectId);
            if (cancelled && spellId >= 0) _audio?.PresentSpellEnd(spellId);
            return Complete(cancelled, cancelled ? "Maintained spell ended." : "The effect could not be cancelled.");
        }

        public IReadOnlyList<GameUiTechnologyView> ProjectTechnology()
            => Enum.GetValues(typeof(TechnologyDiscipline)).Cast<TechnologyDiscipline>()
                .Select(value => new GameUiTechnologyView
                {
                    Discipline = value, Learned = _session.Technology.GetLearnedDegree(Player, value),
                    Effective = _session.Technology.GetEffectiveDegree(Player, value),
                    EffectiveLevel = _session.Technology.GetEffectiveLevel(Player, value),
                }).ToArray();

        public IReadOnlyList<GameUiItemView> ProjectUsableTechnologyItems()
            => ProjectInventory().Where(value => PhaseOneTechnologyCatalog.TryGetItem(value.PrototypeNumber, out _)).ToArray();

        public void BeginTechnologyTargeting(ArcanumObjectId item)
        {
            _pendingTechnologyItem = item;
            CursorMode = GameUiCursorMode.TechnologyTarget;
            Feedback = "Select a technological-item target.";
        }

        public bool UseTechnology(ArcanumObjectId item, ArcanumObjectId target)
        {
            var request = new TechnologyUseRequest(Player, item, target);
            if (_session.Combat.IsActive && _session.Combat.Mode == CombatMode.RealTime)
            {
                CombatResult scheduled = _session.Combat.ScheduleRealTimeTechnology(request);
                return Complete(scheduled.Succeeded, scheduled.Succeeded ? "Technology use scheduled." : scheduled.Failure.ToString());
            }
            TechnologyUseResult result = _session.Technology.Use(request);
            _audio?.PresentTechnology(request, result.Succeeded);
            return Complete(result.Succeeded, result.Succeeded ? "Technology item used." : result.Failure.ToString());
        }

        public IReadOnlyList<GameUiSchematicView> ProjectSchematics()
            => _session.Crafting.ProjectKnown(Player).Select(value =>
            {
                CraftingResult preview = _session.Crafting.Preview(new CraftingRequest(Player, value.Id));
                return new GameUiSchematicView
                {
                    Definition = value, PreviewFailure = preview.Failure,
                    ProductPrototype = preview.ProductPrototype, ProductQuantity = preview.ProductQuantity,
                };
            }).ToArray();

        public bool Craft(SchematicId id)
        {
            CraftingResult result = _session.Crafting.Execute(new CraftingRequest(Player, id));
            return Complete(result.Succeeded, result.Succeeded
                ? $"Crafted prototype {result.ProductPrototype} x{result.ProductQuantity}." : result.Failure.ToString());
        }

        public bool BeginMerchant(ArcanumObjectId merchant)
        {
            if (!_session.Economy.TryResolveMerchant(merchant, out _, out _))
                return Reject(EconomyFailure.InvalidMerchant.ToString());
            _merchant = merchant;
            Screen = GameUiScreen.Merchant;
            CursorMode = GameUiCursorMode.Default;
            Feedback = string.Empty;
            return true;
        }

        public int PlayerGold => HasPlayer ? _session.GetGold(Player) : 0;
        public int MerchantGold => _merchant.IsPersistent ? _session.GetGold(_merchant) : 0;

        public IReadOnlyList<GameUiMerchantItemView> ProjectMerchantWares()
            => !_merchant.IsPersistent ? Array.Empty<GameUiMerchantItemView>()
                : _session.Economy.GetMerchantWares(_merchant).Select(value => MerchantView(value,
                    EconomyTransactionDirection.BuyFromMerchant)).ToArray();

        public IReadOnlyList<GameUiMerchantItemView> ProjectPlayerSaleItems()
            => !_merchant.IsPersistent ? Array.Empty<GameUiMerchantItemView>()
                : ProjectInventory().Where(value => !value.IsEquipped).Select(value =>
                    MerchantView(_session.States[value.Identity], EconomyTransactionDirection.SellToMerchant)).ToArray();

        public IReadOnlyList<GameUiItemView> ProjectMerchantEquipment()
            => !_merchant.IsPersistent ? Array.Empty<GameUiItemView>()
                : ProjectInventory(_merchant).Where(value => value.IsEquipped).ToArray();

        public bool Trade(EconomyTransactionDirection direction, ArcanumObjectId item, int quantity = 1)
        {
            EconomyTransactionResult result = _session.Economy.Execute(
                new EconomyTransactionRequest(direction, _merchant, Player, item, quantity));
            return Complete(result.Succeeded, result.Succeeded
                ? $"Transaction complete: {result.GoldTransferred} Gold." : result.Failure.ToString());
        }

        public IReadOnlyList<QuestJournalEntry> ProjectJournal()
        {
            if (!HasPlayer || !_session.Journal.HasSource) return Array.Empty<QuestJournalEntry>();
            bool lowIntelligence = _session.Characters.GetEffectiveAttribute(Player,
                CharacterAttribute.Intelligence) <= 4;
            return _session.Journal.ProjectAll(lowIntelligence);
        }

        public IReadOnlyList<WorldMapDestination> ProjectDestinations(out WorldMapDestinationFailure failure)
        {
            if (_session.WorldMapDestinations.TryProjectVisible(out IReadOnlyList<WorldMapDestination> values,
                    out failure)) return values;
            return Array.Empty<WorldMapDestination>();
        }

        public bool TravelTo(Arcanum.Formats.World.AreaId area)
        {
            WorldMapSelectionResult selection = _session.WorldMapDestinations.TrySelectWorldArea(area);
            if (!selection.Succeeded) return Reject(selection.Failure.ToString());
            WorldMapTravelResult travel = _session.RequestWorldMapTravel(Player, selection.Request);
            return Complete(travel.Succeeded, travel.Succeeded
                ? $"Arrived at {selection.Destination.DisplayName}." : travel.Failure.ToString());
        }

        public IReadOnlyList<GameUiPartyMemberView> ProjectParty()
        {
            var result = new List<GameUiPartyMemberView>();
            foreach (PartyMember member in _session.Party.Members)
            {
                if (!_session.Vitality.TryGet(member.Identity, out PersistentCharacterVitalityState vitality)) continue;
                result.Add(new GameUiPartyMemberView
                {
                    Identity = member.Identity, Name = ObjectName(member.Identity),
                    HitPoints = vitality.CurrentHitPoints, MaximumHitPoints = vitality.MaximumHitPoints,
                    Fatigue = vitality.CurrentFatigue, MaximumFatigue = vitality.MaximumFatigue,
                    Dead = _session.Vitality.IsDead(member.Identity),
                    Unconscious = _session.Vitality.IsUnconscious(member.Identity), Forced = member.Forced,
                });
            }
            return result;
        }

        public bool DismissFollower(ArcanumObjectId follower)
        {
            PartyMutationResult result = _session.Party.Remove(follower);
            return Complete(result.Succeeded, result.Succeeded ? "Follower dismissed." : result.Failure.ToString());
        }

        public ProductionDialogueSession Dialogue => _session.Dialogue;
        public bool SelectDialogueResponse(int index)
        {
            if (_session.Dialogue.Phase != DialogueSessionPhase.AwaitingPlayerChoice
                || index < 0 || index >= _session.Dialogue.AvailableResponses.Count)
                return Reject("That response is not available.");
            DialogueChoiceStatus status = _session.Dialogue.SelectResponse(index);
            bool accepted = status is DialogueChoiceStatus.Advanced or DialogueChoiceStatus.Completed;
            _audio?.PresentInterface(InterfaceAudioCue.DialogueResponse, accepted);
            ArcanumObjectId examination = _session.Dialogue.SpecialView == DialogueSpecialView.CharacterExamination
                ? _session.Dialogue.SpecialViewTarget : default;
            if (accepted && !examination.IsNull)
            {
                Close();
                if (Open(GameUiScreen.Character)) _character = examination;
            }
            else Refresh();
            return Complete(accepted, accepted ? string.Empty
                : _session.Dialogue.LastFailure ?? status.ToString());
        }

        public void CancelDialogue()
        {
            if (_session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice)
                _session.Dialogue.Cancel("Cancelled by player.");
            Refresh();
        }

        public bool SubmitWorldTarget(ArcanumObjectId target)
        {
            SelectedWorldTarget = target;
            if (target.IsNull) return Reject("No authoritative object is under the cursor.");
            if (CursorMode == GameUiCursorMode.SpellTarget && _pendingSpell >= 0)
            {
                int spell = _pendingSpell;
                _pendingSpell = -1;
                CursorMode = GameUiCursorMode.Default;
                return CastSpell(spell, target);
            }
            if (CursorMode == GameUiCursorMode.TechnologyTarget && !_pendingTechnologyItem.IsNull)
            {
                ArcanumObjectId item = _pendingTechnologyItem;
                _pendingTechnologyItem = default;
                CursorMode = GameUiCursorMode.Default;
                return UseTechnology(item, target);
            }
            if (CursorMode == GameUiCursorMode.MerchantTarget) return BeginMerchant(target);
            return Complete(true, $"Selected {ObjectName(target)}.");
        }

        public void BeginMerchantTargeting()
        {
            CursorMode = GameUiCursorMode.MerchantTarget;
            Feedback = "Select a merchant in the world.";
        }

        public void OpenSaveLoad(SaveLoadPanelMode mode)
        {
            Open(GameUiScreen.SaveLoad);
            SaveLoad.SetMode(mode);
        }

        public void RequestLoad()
        {
            SaveLoad.RequestLoad();
            if (string.IsNullOrEmpty(SaveLoad.ErrorMessage))
            {
                Close();
                Combat.ResetTransient();
            }
        }

        private GameUiItemView ProjectItem(PersistentObjectState value)
            => new()
            {
                Identity = value.Identity, PrototypeNumber = value.PrototypeNumber, Type = value.Type,
                Name = ItemName(value), Quantity = value.StackQuantity ?? 1,
                Weight = _session.InventoryCapacity.GetTotalWeight(value.Identity),
                InventoryArtId = value.InventoryArtId, InventoryFootprint = value.InventoryFootprint,
                InventoryLocation = value.InventoryLocation,
                AmmoItemType = value.AmmoItemType,
                WornLocation = value.Placement.Kind == ObjectPlacementKind.Equipped
                    ? value.Placement.WornLocation : null,
            };

        private int ProjectTotalAttack(ArcanumObjectId owner)
        {
            Weapon weapon = _session.TryGetEquippedItem(owner, WornLocation.Weapon,
                                out PersistentObjectState equipped)
                            && equipped.Type == ObjectType.Weapon && equipped.WeaponData != null
                ? equipped.WeaponData : Weapon.Unarmed();
            CharacterSkill skill = weapon.Skill switch
            {
                WeaponSkill.Bow => CharacterSkill.Bow,
                WeaponSkill.Throwing => CharacterSkill.Throwing,
                WeaponSkill.Firearms => CharacterSkill.Firearms,
                _ => CharacterSkill.Melee,
            };
            int effectiveness = checked(5 * _session.Progression.GetEffectiveSkillRank(owner, skill) + 25);
            if (_session.Characters.GetEffectiveAttribute(owner, CharacterAttribute.Intelligence) >= 20)
                effectiveness += 10;
            int weighted = 15 * effectiveness;
            int divisor = 15;
            int[] weights = { 12, 12, 18, 15, 12 };
            int meleeBonus = weapon.Skill == WeaponSkill.Melee
                ? _session.DerivedStats.GetDerivedStat(owner, CharacterDerivedStat.MeleeDamageBonus) : 0;
            for (int index = 0; index < Weapon.DamageTypeCount; index++)
            {
                int minimum = weapon.DamageMin[index] + (index == (int)DamageType.Normal ? meleeBonus : 0);
                int maximum = weapon.DamageMax[index] + (index == (int)DamageType.Normal ? meleeBonus : 0);
                weighted += weights[index] * ((minimum + maximum) / 2);
                divisor += weights[index];
            }
            return weighted / (divisor + 6);
        }

        private int ProjectTotalDefense(ArcanumObjectId owner)
        {
            int weighted = 24 * _session.DerivedStats.GetArmorClass(owner);
            int divisor = 24;
            int[] weights = { 25, 15, 10, 16, 15 };
            for (int index = 0; index < weights.Length; index++)
            {
                weighted += weights[index] * _session.DerivedStats.GetResistance(owner,
                    (CharacterResistance)index);
                divisor += weights[index];
            }
            return 2 * (weighted / (divisor + 6));
        }

        private GameUiMerchantItemView MerchantView(PersistentObjectState value,
            EconomyTransactionDirection direction)
        {
            EconomyTransactionResult preview = _session.Economy.Preview(
                new EconomyTransactionRequest(direction, _merchant, Player, value.Identity, 1));
            return new GameUiMerchantItemView
            {
                Item = ProjectItem(value), Direction = direction,
                UnitPrice = preview.Price.UnitPrice, Failure = preview.Failure,
            };
        }

        private string ItemName(PersistentObjectState value)
            => _session.ResolveDisplayName(value);

        private string ObjectName(ArcanumObjectId identity)
            => _session.TryGetObjectState(identity, out PersistentObjectState state)
                ? _session.ResolveDisplayName(state)
                : identity.ToString();

        private bool Complete(bool success, string message)
        {
            ShowFeedback(message);
            if (!success) CursorMode = GameUiCursorMode.Invalid;
            else if (CursorMode == GameUiCursorMode.Invalid) CursorMode = GameUiCursorMode.Default;
            return success;
        }

        private bool Reject(string message)
        {
            _audio?.PresentInterface(InterfaceAudioCue.InvalidAction, false);
            return Complete(false, message);
        }
    }
}
