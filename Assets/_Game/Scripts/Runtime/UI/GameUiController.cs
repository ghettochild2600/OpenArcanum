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

namespace Arcanum.Runtime.UI
{
    public enum GameUiScreen
    {
        None, MainMenu, CharacterCreation, Inventory, Character, Magic, Technology, Crafting,
        Journal, Map, Party, Merchant, SaveLoad, Dialogue,
    }

    public enum GameUiCursorMode
    {
        Default, Move, Use, Attack, Invalid, Item, SpellTarget, TechnologyTarget, MerchantTarget,
    }

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
        public IReadOnlyList<ActiveSpellEffect> ActiveEffects { get; internal set; }
    }

    public sealed class GameUiItemView
    {
        public ArcanumObjectId Identity { get; internal set; }
        public int PrototypeNumber { get; internal set; }
        public ObjectType Type { get; internal set; }
        public string Name { get; internal set; }
        public int Quantity { get; internal set; }
        public long Weight { get; internal set; }
        public int InventoryLocation { get; internal set; }
        public WornLocation? WornLocation { get; internal set; }
        public bool IsEquipped => WornLocation.HasValue;
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

    public sealed class GameUiCharacterView
    {
        public string Name { get; internal set; }
        public CharacterRace Race { get; internal set; }
        public CharacterGender Gender { get; internal set; }
        public int Level { get; internal set; }
        public int Experience { get; internal set; }
        public int CharacterPoints { get; internal set; }
        public int Alignment { get; internal set; }
        public int Aptitude { get; internal set; }
        public int ArmorClass { get; internal set; }
        public long CarriedWeight { get; internal set; }
        public int CarryCapacity { get; internal set; }
        public IReadOnlyList<GameUiAttributeView> Attributes { get; internal set; }
        public IReadOnlyList<GameUiSkillView> Skills { get; internal set; }
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
        private ArcanumObjectId _merchant;
        private int _pendingSpell = -1;
        private ArcanumObjectId _pendingTechnologyItem;

        public GameUiScreen Screen { get; private set; }
        public GameUiCursorMode CursorMode { get; private set; } = GameUiCursorMode.Default;
        public ArcanumObjectId SelectedItem { get; private set; }
        public ArcanumObjectId SelectedWorldTarget { get; private set; }
        public string Feedback { get; private set; } = string.Empty;
        public CombatUiController Combat { get; }
        public SaveLoadPanelController SaveLoad { get; }
        public CharacterCreationSpecification CreationDraft { get; private set; }
        public CharacterCreationValidationResult CreationValidation { get; private set; }
        public bool HasPlayer => _session.PlayerState != null;
        public ArcanumObjectId Player => _session.PlayerState?.Identity ?? default;
        public bool IsModalOpen => Screen != GameUiScreen.None;

        public GameUiController(WorldMapSessionCoordinator session, ISessionSaveSlotOperations saveOperations = null,
            CombatUiController combat = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Combat = combat ?? new CombatUiController(session);
            SaveLoad = new SaveLoadPanelController(saveOperations ?? session.SaveSlots);
            Screen = session.PlayerState == null ? GameUiScreen.MainMenu : GameUiScreen.None;
        }

        public void Refresh()
        {
            Combat.Refresh();
            if (!HasPlayer)
            {
                if (Screen is not (GameUiScreen.MainMenu or GameUiScreen.CharacterCreation or GameUiScreen.SaveLoad))
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
                or GameUiScreen.SaveLoad)) return Reject("Start or load a game first.");
            if (_session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice
                && screen != GameUiScreen.Dialogue)
                return Reject("Finish the current conversation first.");
            if (_session.Combat.IsActive && _session.Combat.Mode == CombatMode.TurnBased
                && _session.Combat.CurrentParticipant != Player
                && screen is not (GameUiScreen.Dialogue or GameUiScreen.SaveLoad))
                return Reject("The interface is locked while another combatant acts.");
            SaveLoad.Close();
            Screen = screen;
            CursorMode = GameUiCursorMode.Default;
            SelectedItem = default;
            SelectedWorldTarget = default;
            Feedback = string.Empty;
            if (screen == GameUiScreen.SaveLoad) SaveLoad.Open(SaveLoadPanelMode.Save);
            return true;
        }

        public bool BeginNewGame()
        {
            CharacterCreationCatalog catalog = _session.CharacterCreation.Catalog;
            if (catalog == null) return Reject("Character-creation source data is unavailable.");
            CreationDraft = new CharacterCreationSpecification { Name = "Player" };
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

        public void SetCreationIdentity(CharacterRace race, CharacterGender gender)
        {
            if (CreationDraft == null) return;
            CreationDraft.Race = race;
            CreationDraft.Gender = gender;
            CharacterCreationCatalog catalog = _session.CharacterCreation.Catalog;
            PortraitDefinition portrait = catalog?.Portraits.Values.FirstOrDefault(value =>
                value.Race == race && value.Gender == gender);
            if (portrait != null) CreationDraft.PortraitId = portrait.Id;
            if (catalog != null && (!catalog.TryGetBackground(CreationDraft.BackgroundId, out BackgroundDefinition background)
                                    || !background.IsLegal(race, gender) || background.HasUnsupportedEffects))
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

        public bool FinalizeNewGame()
        {
            if (CreationDraft == null) return Reject("No character is being created.");
            CharacterCreationFinalizeResult result = _session.CharacterCreation.FinalizeNewGame(CreationDraft);
            if (!result.Succeeded) return Reject(result.Message ?? result.Failure.ToString());
            CreationDraft = null;
            CreationValidation = default;
            Screen = GameUiScreen.None;
            Feedback = string.Empty;
            return true;
        }

        private void RefreshCreationValidation()
        {
            CreationValidation = _session.CharacterCreation.Validate(CreationDraft);
            Feedback = CreationValidation.Succeeded ? string.Empty : CreationValidation.Message ?? string.Empty;
        }

        public void Close()
        {
            SaveLoad.Close();
            Screen = GameUiScreen.None;
            CursorMode = GameUiCursorMode.Default;
            SelectedItem = default;
            SelectedWorldTarget = default;
            _merchant = default;
            _pendingSpell = -1;
            _pendingTechnologyItem = default;
            Feedback = string.Empty;
            if (HasPlayer) CreationDraft = null;
        }

        public void RebuildPresentation()
        {
            GameUiScreen previous = Screen;
            Close();
            Combat.ResetTransient();
            if (previous != GameUiScreen.None && HasPlayer) Open(previous);
        }

        public GameUiHudView ProjectHud()
        {
            if (!HasPlayer || !_session.Vitality.TryGet(Player, out PersistentCharacterVitalityState vitality))
                return null;
            string weapon = "Unarmed";
            int ammunition = 0;
            if (_session.TryGetEquippedItem(Player, WornLocation.Weapon, out PersistentObjectState item))
            {
                weapon = ItemName(item);
                if (item.WeaponData?.UsesAmmo == true
                    && _session.TryGetAmmo(Player, item.WeaponData.AmmoType, 1, out PersistentObjectState ammo))
                    ammunition = ammo.StackQuantity.GetValueOrDefault();
            }
            string readiness = "READY";
            if (_session.Combat.IsActive && _session.Combat.Mode == CombatMode.TurnBased)
                readiness = _session.Combat.CurrentParticipant == Player ? "READY" : "WAIT";
            else if (_session.Combat.IsActive)
                readiness = Combat.IsRealTimeBusy ? "BUSY" : Combat.IsRealTimeReady ? "READY" : "RECOVERING";
            return new GameUiHudView
            {
                HitPoints = vitality.CurrentHitPoints, MaximumHitPoints = vitality.MaximumHitPoints,
                Fatigue = vitality.CurrentFatigue, MaximumFatigue = vitality.MaximumFatigue,
                Weapon = weapon, Ammunition = ammunition,
                CombatActive = _session.Combat.IsActive, CombatMode = _session.Combat.Mode,
                ActionPoints = _session.Combat.IsActive ? _session.Combat.CurrentActionPoints : 0,
                MaximumActionPoints = _session.Combat.IsActive ? _session.Combat.MaximumActionPoints : 0,
                Readiness = readiness,
                ActiveEffects = _session.Magic.ActiveEffects.Where(value => value.Target == Player).ToArray(),
            };
        }

        public IReadOnlyList<GameUiItemView> ProjectInventory(ArcanumObjectId owner = default)
        {
            if (owner.IsNull) owner = Player;
            return _session.States.Values.Where(value => value.ParentIdentity == owner
                    && value.Placement.Kind is ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped)
                .OrderBy(value => value.Placement.Kind == ObjectPlacementKind.Equipped ? 0 : 1)
                .ThenBy(value => value.InventoryLocation).ThenBy(value => value.Identity.Key, StringComparer.Ordinal)
                .Select(ProjectItem).ToArray();
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
            if (!HasPlayer || !_session.Characters.TryGet(Player, out PersistentCharacterState character)
                || !_session.Progression.TryGet(Player, out PersistentCharacterProgressionState progression)) return null;
            return new GameUiCharacterView
            {
                Name = _session.CharacterCreation.Finalized?.Name ?? "Player",
                Race = character.Race, Gender = character.Gender,
                Level = progression.Level, Experience = progression.Experience,
                CharacterPoints = progression.UnspentCharacterPoints,
                Alignment = _session.DerivedStats.GetAlignment(Player),
                Aptitude = _session.DerivedStats.GetDerivedStat(Player, CharacterDerivedStat.MagickTechAptitude),
                ArmorClass = _session.DerivedStats.GetArmorClass(Player),
                CarriedWeight = _session.InventoryCapacity.GetInventoryLoad(Player),
                CarryCapacity = _session.InventoryCapacity.GetCarryCapacity(Player),
                Attributes = Enum.GetValues(typeof(CharacterAttribute)).Cast<CharacterAttribute>()
                    .Select(value => new GameUiAttributeView(value,
                        _session.Characters.GetBaseAttribute(Player, value),
                        _session.Characters.GetEffectiveAttribute(Player, value))).ToArray(),
                Skills = CharacterSkillRules.AllSkills.Select(value => new GameUiSkillView(value,
                    _session.Progression.GetPurchasedSkillPoints(Player, value),
                    _session.Progression.GetEffectiveSkillRank(Player, value),
                    _session.Progression.GetTrainingLevel(Player, value))).ToArray(),
            };
        }

        public bool IncreaseSkill(CharacterSkill skill)
        {
            SkillIncreaseResult result = _session.Progression.IncreaseSkill(Player, skill);
            return Complete(result == SkillIncreaseResult.Success,
                result == SkillIncreaseResult.Success ? $"Increased {skill}." : result.ToString());
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
            return Complete(result.Succeeded, result.Succeeded ? "Spell cast." : result.Failure.ToString());
        }

        public bool CancelEffect(long effectId)
        {
            bool cancelled = _session.Magic.CancelMaintainedEffect(Player, effectId);
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
            Refresh();
            return Complete(accepted, accepted ? "Response selected."
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
                InventoryLocation = value.InventoryLocation,
                WornLocation = value.Placement.Kind == ObjectPlacementKind.Equipped
                    ? value.Placement.WornLocation : null,
            };

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
            => $"{value.Type} {value.PrototypeNumber}";

        private string ObjectName(ArcanumObjectId identity)
            => _session.TryGetObjectState(identity, out PersistentObjectState state)
                ? $"{state.Type} {state.PrototypeNumber}"
                : identity.ToString();

        private bool Complete(bool success, string message)
        {
            Feedback = message ?? string.Empty;
            if (!success) CursorMode = GameUiCursorMode.Invalid;
            return success;
        }

        private bool Reject(string message) => Complete(false, message);
    }
}
