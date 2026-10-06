using System;
using System.Collections.Generic;
using System.IO;
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
using UnityEngine;

namespace Arcanum.Runtime.UI
{
    /// <summary>
    /// Source-shaped M12C production presentation. This component owns drawing, hotkeys, scrolling and pointer
    /// selection only; <see cref="GameUiController"/> delegates every gameplay command to established services.
    /// </summary>
    [RequireComponent(typeof(WorldMapSessionCoordinator), typeof(WorldObjectSectorLoader), typeof(PlayerInputGate))]
    public sealed class ProductionGameUiPresenter : MonoBehaviour, IProductionKeyboardCommands
    {
        public const float ReferenceWidth = 1024f;
        public const float ReferenceHeight = 768f;

        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PlayerInputGate _inputGate;
        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private ProductionCombatPresenter _legacyCombat;
        private RetailMainMenuView _retailMainMenu;
        private RetailGameplayHudView _retailGameplayHud;
        private RetailInventoryEquipmentView _retailInventory;
        private Vector2 _scroll;
        private Rect _panelRect;
        private bool _showHud = true;
        private bool _interactionSubscribed;
        private int _quickSlotAssignment;
        private readonly ProductionKeyboardController _keyboard = new();
        private static readonly KeyCode[] DownKeys =
        {
            KeyCode.Space, KeyCode.I, KeyCode.C, KeyCode.M, KeyCode.T, KeyCode.K, KeyCode.L, KeyCode.W,
            KeyCode.O, KeyCode.R, KeyCode.A, KeyCode.S, KeyCode.F, KeyCode.V, KeyCode.Return,
            KeyCode.KeypadEnter, KeyCode.F12, KeyCode.Numlock, KeyCode.Comma, KeyCode.Period, KeyCode.Slash,
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
        };
        private static readonly KeyCode[] UpKeys =
        {
            KeyCode.Escape, KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.E, KeyCode.Home, KeyCode.Comma, KeyCode.Period, KeyCode.Slash,
        };
        private static readonly KeyCode[] HeldKeys =
            { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };

        private GameUiController _controller;
        public GameUiController Controller
        {
            get
            {
                if (_controller == null) EnsureController();
                return _controller;
            }
            private set => _controller = value;
        }
        public float PresentationScale => Mathf.Clamp(Mathf.Min(Screen.width / ReferenceWidth,
            Screen.height / ReferenceHeight), .75f, 1.5f);
        public bool IsInputBlocked => Controller?.IsModalOpen == true;
        public ProductionKeyboardController Keyboard => _keyboard;

        private void Awake()
            => BindProductionPresentation();

        internal void BindProductionPresentation()
        {
            _session = GetComponent<WorldMapSessionCoordinator>();
            _loader = GetComponent<WorldObjectSectorLoader>();
            _inputGate = GetComponent<PlayerInputGate>();
            _navigation = GetComponent<PlayerNavigationController>();
            EnsureInteractionSubscription();
            _legacyCombat = GetComponent<ProductionCombatPresenter>();
            if (_controller == null)
                _controller = new GameUiController(_session, _session.SaveSlots, _legacyCombat?.Controller);
            _retailMainMenu = GetComponent<RetailMainMenuView>() ?? gameObject.AddComponent<RetailMainMenuView>();
            _retailMainMenu.Bind(_controller, Application.Quit);
            _retailGameplayHud = GetComponent<RetailGameplayHudView>()
                                 ?? gameObject.AddComponent<RetailGameplayHudView>();
            _retailGameplayHud.Bind(_controller, _session, Open);
            _retailInventory = GetComponent<RetailInventoryEquipmentView>()
                               ?? gameObject.AddComponent<RetailInventoryEquipmentView>();
            _retailInventory.Bind(_controller, _session, _retailGameplayHud.Resolver);
            DisableLegacyPresenters();
        }

        private void OnDisable()
        {
            Controller?.Close();
            _inputGate?.SetBlocked(false);
        }

        private void OnDestroy()
        {
            if (_interaction != null && _interactionSubscribed)
                _interaction.Resolved -= OnInteractionResolved;
            _interactionSubscribed = false;
        }

        private void OnInteractionResolved(WorldInteractionResult result)
        {
            if (!result.IsSuccess || result.Command.Type != WorldInteractionCommandType.Loot) return;
            if (Controller.BeginCorpseLoot(result.Command.Target))
            {
                _scroll = Vector2.zero;
                SyncInputGate();
            }
        }

        private void Update()
        {
            EnsureInteractionSubscription();
            EnsureController();
            Controller.Refresh();
            SyncInputGate();

            _retailMainMenu?.Synchronize();
            _retailGameplayHud?.Synchronize(_showHud);
            _retailInventory?.Synchronize();
            if (Controller.Screen == GameUiScreen.MainMenu && _retailMainMenu?.IsAvailable == true)
                return;

            if (Input.GetKeyDown(KeyCode.F10)) _showHud = !_showHud; // OpenArcanum-only HUD diagnostic
            _keyboard.SetModifiers(
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
            var context = new ProductionKeyboardContext(Controller.Screen, Controller.HasPlayer,
                Controller.Screen == GameUiScreen.CharacterCreation, _session.Combat.IsActive, _session.Combat.Mode);
            foreach (KeyCode key in UpKeys)
                if (Input.GetKeyUp(key)) _keyboard.Dispatch(key, ProductionKeyPhase.Up, context, this);
            foreach (KeyCode key in DownKeys)
                if (Input.GetKeyDown(key)) _keyboard.Dispatch(key, ProductionKeyPhase.Down, context, this);
            foreach (KeyCode key in HeldKeys)
                if (Input.GetKey(key)) _keyboard.Dispatch(key, ProductionKeyPhase.Held, context, this);

            HandleToggle(KeyCode.P, GameUiScreen.Party); // non-retail OpenArcanum convenience
            if (Input.GetKeyDown(KeyCode.B))
            {
                Open(GameUiScreen.Merchant);
                Controller.BeginMerchantTargeting();
            }
            if (Controller.Screen == GameUiScreen.Dialogue)
                for (int index = 0; index < Controller.Dialogue.AvailableResponses.Count && index < 9; index++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + index)))
                        Controller.SelectDialogueResponse(index);

            HandleWorldPointer();
            SyncInputGate();
        }

        void IProductionKeyboardCommands.Escape()
        {
            if (Controller.Screen == GameUiScreen.Dialogue) Controller.CancelDialogue();
            else if (Controller.SaveLoad.Confirmation != SaveLoadConfirmation.None)
                Controller.SaveLoad.CancelConfirmation();
            else if (Controller.IsModalOpen) Controller.Close();
            else Open(GameUiScreen.MainMenu);
        }

        void IProductionKeyboardCommands.ToggleScreen(GameUiScreen screen)
        {
            if (Controller.Screen == screen) Controller.Close(); else Open(screen);
        }

        void IProductionKeyboardCommands.CloseInterface() => Controller.Close();
        void IProductionKeyboardCommands.ToggleCombatMode() => Controller.ToggleCombatMode();
        void IProductionKeyboardCommands.EndTurn() => Controller.Combat.EndTurn();
        void IProductionKeyboardCommands.ToggleAttackTalkMode() => Controller.ToggleAttackTalkMode();
        void IProductionKeyboardCommands.ActivateQuickSlot(int index) => Controller.ActivateQuickSlot(index);
        void IProductionKeyboardCommands.ActivateRecentAction() => Controller.ActivateRecentAction();
        void IProductionKeyboardCommands.SetCalledLocation(CombatCalledLocation location)
            => Controller.Combat.SetCalledLocation(location);
        void IProductionKeyboardCommands.AutoSave() => Controller.AutoSave();
        void IProductionKeyboardCommands.AutoLoad() => Controller.AutoLoad();
        void IProductionKeyboardCommands.ShowVersion()
            => Controller.ShowFeedback($"OpenArcanum {Application.version}");
        void IProductionKeyboardCommands.Unsupported(string sourceAction)
            => Controller.ShowFeedback($"{sourceAction} is not available in the current runtime.");

        void IProductionKeyboardCommands.CaptureScreenshot()
        {
            string directory = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"OpenArcanum-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            Controller.ShowFeedback($"Screenshot saved: {path}");
        }

        void IProductionKeyboardCommands.IssuePartyOrder(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex > 5) return;
            PartyOrder order = (PartyOrder)sourceIndex;
            Vector2Int? location = null;
            ArcanumObjectId target = default;
            if (order == PartyOrder.Walk && TryGetPointerWorld(out Vector3 world))
                location = WorldToGlobalTile(world);
            if (order == PartyOrder.Attack && TryGetPointerWorld(out world))
                WorldObjectTargetSelector.TrySelectCombatTarget(_loader.SpriteOwners, world, out target);
            bool accepted = _session.Party.IssueOrder(order, location, target);
            if (accepted && order == PartyOrder.Attack) Controller.StartAttack(target, true);
            if (accepted && order == PartyOrder.BackOff && _session.Combat.IsActive)
                foreach (PartyMember member in _session.Party.Members.ToArray())
                    if (_session.Combat.IsParticipantEngaged(member.Identity))
                        _session.Combat.RemoveParticipant(member.Identity);
            Controller.ShowFeedback(accepted ? $"Followers: {order}." : $"Followers cannot {order} here.");
        }

        void IProductionKeyboardCommands.CenterCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || _session.PlayerState == null) return;
            ArcanumObjectId identity = _session.PlayerState.Identity;
            if (_session.Combat.IsActive && _session.Combat.Mode == CombatMode.TurnBased
                && _session.TryGetLoadedObject(identity, out WorldObject player)
                && Vector2.Distance(camera.transform.position, player.transform.position) < .05f)
                identity = _session.Combat.CurrentParticipant;
            if (!_session.TryGetLoadedObject(identity, out WorldObject target)) return;
            Vector3 position = target.transform.position;
            position.z = camera.transform.position.z;
            camera.transform.position = position;
        }

        void IProductionKeyboardCommands.ScrollCamera(Vector2 direction)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            camera.transform.position += (Vector3)(direction.normalized * (12f * Time.unscaledDeltaTime));
        }

        private bool TryGetPointerWorld(out Vector3 world)
        {
            world = default;
            Camera camera = Camera.main;
            if (camera == null || _loader == null) return false;
            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(camera.transform.position.z - _loader.transform.position.z);
            world = camera.ScreenToWorldPoint(screen);
            return true;
        }

        private Vector2Int WorldToGlobalTile(Vector3 world)
        {
            Vector2Int local = IsoProjection.WorldToTile(_loader.transform.InverseTransformPoint(world), _loader.PixelsPerUnit);
            return SectorCoordinate.TryParse(_session.SelectedSector, out SectorCoordinate sector)
                ? Vector2Int.RoundToInt(sector.ToGlobal(local)) : local;
        }

        internal void EnsureInteractionSubscription()
        {
            PlayerInteractionController next = GetComponent<PlayerInteractionController>();
            if (next == _interaction && (_interactionSubscribed || next == null)) return;
            if (_interaction != null && _interactionSubscribed)
                _interaction.Resolved -= OnInteractionResolved;
            _interaction = next;
            _interactionSubscribed = false;
            if (_interaction == null) return;
            _interaction.Resolved += OnInteractionResolved;
            _interactionSubscribed = true;
        }

        private void HandleToggle(KeyCode key, GameUiScreen screen)
        {
            if (!Input.GetKeyDown(key)) return;
            if (Controller.Screen == screen) Controller.Close();
            else Open(screen);
        }

        private void Open(GameUiScreen screen)
        {
            if (!Controller.Open(screen)) return;
            _scroll = Vector2.zero;
            _navigation?.CancelRoute();
            _interaction?.CancelPending();
            SyncInputGate();
        }

        private void HandleWorldPointer()
        {
            if (!Input.GetMouseButtonUp(0)) return;
            GameUiCursorMode cursor = Controller.CursorMode;
            bool targeting = cursor is GameUiCursorMode.SpellTarget or GameUiCursorMode.TechnologyTarget
                or GameUiCursorMode.MerchantTarget;
            if (!targeting || PointerOverInterface(Input.mousePosition)) return;
            Camera camera = Camera.main;
            if (camera == null || _loader == null) return;
            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(camera.transform.position.z - _loader.transform.position.z);
            Vector3 world = camera.ScreenToWorldPoint(screen);
            if (WorldObjectTargetSelector.TrySelectCombatTarget(_loader.SpriteOwners, world,
                    out ArcanumObjectId target)) Controller.SubmitWorldTarget(target);
            else Controller.SubmitWorldTarget(default);
        }

        internal bool PointerOverInterface(Vector3 mouse)
        {
            if (_retailInventory?.ContainsScreenPoint(mouse) == true) return true;
            if (_retailGameplayHud?.ContainsScreenPoint(mouse) == true) return true;
            Vector2 guiPoint = new(mouse.x, Screen.height - mouse.y);
            if (_retailGameplayHud?.IsAvailable == true) return _panelRect.Contains(guiPoint);
            float hudHeight = 154f * PresentationScale;
            return guiPoint.y >= Screen.height - hudHeight || _panelRect.Contains(guiPoint);
        }

        private void OnGUI()
        {
            EnsureController();
            Controller.Refresh();
            _retailMainMenu?.Synchronize();
            _retailGameplayHud?.Synchronize(_showHud);
            _retailInventory?.Synchronize();
            bool sourceMainMenu = Controller.Screen == GameUiScreen.MainMenu
                                  && _retailMainMenu?.IsAvailable == true;
            bool sourceInventory = Controller.Screen == GameUiScreen.Inventory
                                   && _retailInventory?.IsAvailable == true;
            GUI.depth = -120;
            if (_showHud && _retailGameplayHud?.IsAvailable != true) DrawHud();
            if (Controller.IsModalOpen && !sourceMainMenu && !sourceInventory) DrawScreen();
            DrawCursorLabel();
        }

        private void DrawHud()
        {
            GameUiHudView hud = Controller.ProjectHud();
            if (hud == null) return;
            float scale = PresentationScale;
            float height = 154f * scale;
            var area = new Rect(0f, Screen.height - height, Screen.width, height);
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.25f, .16f, .08f, .98f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUI.backgroundColor = previous;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(225f * scale));
            GUILayout.Label($"{hud.Readiness}  {hud.Weapon}");
            DrawMeter("Health", hud.HitPoints, hud.MaximumHitPoints, new Color(.55f, .08f, .05f));
            DrawMeter("Fatigue", hud.Fatigue, hud.MaximumFatigue, new Color(.1f, .25f, .55f));
            GUILayout.Label($"Ammo {hud.Ammunition}  |  AP {hud.ActionPoints}/{hud.MaximumActionPoints}");
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            HudButton("INV [I]", GameUiScreen.Inventory);
            HudButton("CHAR [C]", GameUiScreen.Character);
            HudButton("MAGIC [M]", GameUiScreen.Magic);
            HudButton("TECH [T]", GameUiScreen.Technology);
            HudButton("SKILLS [K]", GameUiScreen.Skills);
            HudButton("CRAFT", GameUiScreen.Crafting);
            HudButton("LOG [L]", GameUiScreen.Journal);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            HudButton("MAP [W]", GameUiScreen.Map);
            HudButton("PARTY [P]", GameUiScreen.Party);
            if (GUILayout.Button("BARTER [B]", GUILayout.Height(30f * scale)))
            { Open(GameUiScreen.Merchant); Controller.BeginMerchantTargeting(); }
            if (GUILayout.Button("SAVE/LOAD", GUILayout.Height(30f * scale)))
                Controller.OpenSaveLoad(SaveLoadPanelMode.Save);
            GUILayout.EndHorizontal();

            if (hud.ActiveEffects.Count > 0)
                GUILayout.Label("Maintained: " + string.Join(", ", System.Linq.Enumerable.Select(
                    hud.ActiveEffects, effect => PhaseOneSpellCatalog.TryGet(effect.SpellId, out SpellDefinition spell)
                        ? spell.Name : $"Spell {effect.SpellId}")));
            if (!string.IsNullOrEmpty(Controller.Feedback)) GUILayout.Label(Controller.Feedback, GUI.skin.box);
            GUILayout.EndVertical();

            if (hud.CombatActive) DrawCombatControls(scale);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawMeter(string label, int value, int maximum, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(200f, 20f);
            GUI.Box(rect, $"{label} {value}/{maximum}");
            if (maximum <= 0) return;
            Rect fill = rect;
            fill.width *= Mathf.Clamp01((float)value / maximum);
            Color previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, .65f);
            GUI.Box(fill, GUIContent.none);
            GUI.color = previous;
        }

        private void HudButton(string text, GameUiScreen screen)
        {
            if (GUILayout.Button(text, GUILayout.Height(30f * PresentationScale)))
            {
                if (Controller.Screen == screen) Controller.Close(); else Open(screen);
            }
        }

        private void DrawCombatControls(float scale)
        {
            CombatUiController combat = Controller.Combat;
            GUILayout.BeginVertical(GUILayout.Width(275f * scale));
            GUILayout.Label(combat.HasSelectedTarget ? $"Target: {combat.SelectedTarget}" : "Select a combat target");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("MELEE")) { combat.SetAttackMode(CombatAttackMode.BasicMelee); combat.SubmitAttack(); }
            if (GUILayout.Button("BOW")) { combat.SetAttackMode(CombatAttackMode.BasicRanged); combat.SubmitAttack(); }
            if (combat.Mode == CombatMode.TurnBased && GUILayout.Button("END TURN")) combat.EndTurn();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (CombatCalledLocation location in new[] { CombatCalledLocation.Torso,
                         CombatCalledLocation.Head, CombatCalledLocation.Arm, CombatCalledLocation.Leg })
                if (GUILayout.Button(location.ToString())) combat.SetCalledLocation(location);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(combat.Feedback)) GUILayout.Label(combat.Feedback);
            GUILayout.EndVertical();
        }

        private void DrawScreen()
        {
            float scale = PresentationScale;
            float width = Mathf.Min(920f * scale, Screen.width - 28f);
            float availableHeight = Screen.height - (_showHud ? 180f * scale : 28f);
            float height = Mathf.Min(650f * scale, availableHeight);
            _panelRect = new Rect((Screen.width - width) * .5f, 14f, width, height);
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.3f, .2f, .09f, .98f);
            GUILayout.BeginArea(_panelRect, GUI.skin.window);
            GUI.backgroundColor = previous;
            GUILayout.BeginHorizontal();
            GUILayout.Label(ScreenTitle(Controller.Screen), GUILayout.ExpandWidth(true));
            if (Controller.Screen != GameUiScreen.Dialogue && GUILayout.Button("X", GUILayout.Width(38f)))
                Controller.Close();
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            switch (Controller.Screen)
            {
                case GameUiScreen.MainMenu: DrawMainMenu(); break;
                case GameUiScreen.CharacterCreation: DrawCharacterCreation(); break;
                case GameUiScreen.Inventory: DrawInventory(); break;
                case GameUiScreen.Character: DrawCharacter(); break;
                case GameUiScreen.Skills: DrawSkills(); break;
                case GameUiScreen.Magic: DrawMagic(); break;
                case GameUiScreen.Technology: DrawTechnology(); break;
                case GameUiScreen.Crafting: DrawCrafting(); break;
                case GameUiScreen.Options: DrawOptions(); break;
                case GameUiScreen.Journal: DrawJournal(); break;
                case GameUiScreen.Map: DrawMap(); break;
                case GameUiScreen.Party: DrawParty(); break;
                case GameUiScreen.Merchant: DrawMerchant(); break;
                case GameUiScreen.SaveLoad: DrawSaveLoad(); break;
                case GameUiScreen.Dialogue: DrawDialogue(); break;
                case GameUiScreen.Corpse: DrawCorpse(); break;
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(Controller.Feedback)) GUILayout.Label(Controller.Feedback, GUI.skin.box);
            GUILayout.EndArea();
        }

        private void DrawMainMenu()
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("OPENARCANUM", GUI.skin.box);
            GUILayout.Label("Of Steamworks & Magick Obscura");
            GUILayout.Space(24f);
            if (GUILayout.Button("NEW GAME", GUILayout.Height(48f))) Controller.BeginNewGame();
            if (GUILayout.Button("LOAD GAME", GUILayout.Height(48f)))
                Controller.OpenSaveLoad(SaveLoadPanelMode.Load);
#if !UNITY_EDITOR
            if (GUILayout.Button("QUIT", GUILayout.Height(48f))) Application.Quit();
#endif
            GUILayout.FlexibleSpace();
        }

        private void DrawCharacterCreation()
        {
            CharacterCreationSpecification draft = Controller.CreationDraft;
            CharacterCreationCatalog catalog = _session.CharacterCreation.Catalog;
            if (draft == null || catalog == null)
            {
                GUILayout.Label("Character-creation source data is unavailable.", GUI.skin.box);
                if (GUILayout.Button("BACK")) Controller.Open(GameUiScreen.MainMenu);
                return;
            }

            GUILayout.Label("Name");
            string name = GUILayout.TextField(draft.Name ?? string.Empty,
                CharacterCreationRules.MaximumNameLength);
            if (name != draft.Name) Controller.SetCreationName(name);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(360f));
            GUILayout.Label("Race and gender");
            foreach (CharacterRace race in Enum.GetValues(typeof(CharacterRace)))
            {
                if (!CharacterCreationRules.IsPlayableRace(race)) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(race.ToString(), GUILayout.Width(110f));
                foreach (CharacterGender gender in new[] { CharacterGender.Male, CharacterGender.Female })
                {
                    GUI.enabled = CharacterCreationRules.IsGenderLegal(race, gender);
                    string marker = draft.Race == race && draft.Gender == gender ? "● " : string.Empty;
                    if (GUILayout.Button(marker + gender, GUILayout.Width(100f)))
                        Controller.SetCreationIdentity(race, gender);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("Portrait");
            foreach (PortraitDefinition portrait in catalog.Portraits.Values.Where(value =>
                         value.Race == draft.Race && value.Gender == draft.Gender).OrderBy(value => value.Id))
                if (GUILayout.Button($"{(draft.PortraitId == portrait.Id ? "● " : string.Empty)}"
                                     + $"{portrait.SourceName} [{portrait.Id}]"))
                    Controller.SetCreationPortrait(portrait.Id);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Label("Background");
            foreach (BackgroundDefinition background in catalog.Backgrounds.Values.OrderBy(value => value.Id))
            {
                bool legal = background.IsLegal(draft.Race, draft.Gender) && !background.HasUnsupportedEffects;
                GUI.enabled = legal;
                if (GUILayout.Button($"{(draft.BackgroundId == background.Id ? "● " : string.Empty)}"
                                     + $"{background.Name} — {background.Description}"))
                    Controller.SetCreationBackground(background.Id);
                GUI.enabled = true;
            }

            GUILayout.BeginHorizontal();
            DrawCreationAttributes(draft);
            DrawCreationSkills(draft);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawCreationSpells(draft);
            DrawCreationTechnology(draft);
            GUILayout.EndHorizontal();

            CharacterCreationValidationResult validation = Controller.CreationValidation;
            GUILayout.Label(validation.Succeeded
                ? $"Character Points: {validation.RemainingCharacterPoints} remaining"
                : $"INVALID: {validation.Failure} — {validation.Message}", GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("BACK", GUILayout.Height(40f))) Controller.Open(GameUiScreen.MainMenu);
            GUI.enabled = validation.Succeeded;
            if (GUILayout.Button("BEGIN GAME", GUILayout.Height(40f))) Controller.FinalizeNewGame();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawCreationAttributes(CharacterCreationSpecification draft)
        {
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Attributes");
            foreach (CharacterAttribute attribute in Enum.GetValues(typeof(CharacterAttribute)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(attribute.ToString(), GUILayout.Width(115f));
                int value = draft.GetAttribute(attribute);
                GUI.enabled = value > CharacterAttributeSet.SourceDefault;
                if (GUILayout.Button("-", GUILayout.Width(30f))) Controller.AdjustCreationAttribute(attribute, -1);
                GUI.enabled = true;
                GUILayout.Label(value.ToString(), GUILayout.Width(26f));
                if (GUILayout.Button("+", GUILayout.Width(30f))) Controller.AdjustCreationAttribute(attribute, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawCreationSkills(CharacterCreationSpecification draft)
        {
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Skills");
            foreach (CharacterSkill skill in CharacterSkillRules.AllSkills)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(skill.ToString(), GUILayout.Width(125f));
                int value = draft.GetSkillPoints(skill);
                GUI.enabled = value > 0;
                if (GUILayout.Button("-", GUILayout.Width(30f))) Controller.AdjustCreationSkill(skill, -1);
                GUI.enabled = true;
                GUILayout.Label(value.ToString(), GUILayout.Width(26f));
                if (GUILayout.Button("+", GUILayout.Width(30f))) Controller.AdjustCreationSkill(skill, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawCreationSpells(CharacterCreationSpecification draft)
        {
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Spell colleges");
            foreach (SpellCollege college in Enum.GetValues(typeof(SpellCollege)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(college.ToString(), GUILayout.Width(130f));
                int value = draft.GetSpellRank(college);
                GUI.enabled = value > 0;
                if (GUILayout.Button("-", GUILayout.Width(30f))) Controller.AdjustCreationSpell(college, -1);
                GUI.enabled = true;
                GUILayout.Label(value.ToString(), GUILayout.Width(26f));
                if (GUILayout.Button("+", GUILayout.Width(30f))) Controller.AdjustCreationSpell(college, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawCreationTechnology(CharacterCreationSpecification draft)
        {
            GUILayout.BeginVertical(GUILayout.Width(300f));
            GUILayout.Label("Technology disciplines");
            foreach (TechnologyDiscipline discipline in Enum.GetValues(typeof(TechnologyDiscipline)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(discipline.ToString(), GUILayout.Width(130f));
                int value = draft.GetTechnologyRank(discipline);
                GUI.enabled = value > 0;
                if (GUILayout.Button("-", GUILayout.Width(30f))) Controller.AdjustCreationTechnology(discipline, -1);
                GUI.enabled = true;
                GUILayout.Label(value.ToString(), GUILayout.Width(26f));
                if (GUILayout.Button("+", GUILayout.Width(30f))) Controller.AdjustCreationTechnology(discipline, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawInventory()
        {
            DrawQuickSlotAssignmentPicker();
            foreach (GameUiItemView item in Controller.ProjectInventory())
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{item.Name} x{item.Quantity}  [{item.Weight} st]", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("SELECT", GUILayout.Width(76f))) Controller.SelectItem(item.Identity);
                if (GUILayout.Button("BIND", GUILayout.Width(62f)))
                    Controller.AssignQuickSlotItem(_quickSlotAssignment, item.Identity);
                if (item.IsEquipped)
                {
                    GUILayout.Label(item.WornLocation.ToString(), GUILayout.Width(86f));
                    if (GUILayout.Button("UNEQUIP", GUILayout.Width(82f))) Controller.Unequip(item.WornLocation.Value);
                }
                else if (GUILayout.Button("EQUIP", GUILayout.Width(82f))) Controller.Equip(item.Identity);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCorpse()
        {
            IReadOnlyList<GameUiItemView> items = Controller.ProjectCorpseInventory();
            if (items.Count == 0)
            {
                GUILayout.Label("This corpse carries nothing.");
                return;
            }
            foreach (GameUiItemView item in items)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{item.Name} x{item.Quantity}  [{item.Weight} st]", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("LOOT", GUILayout.Width(82f))) Controller.LootCorpseItem(item.Identity);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCharacter()
        {
            GameUiCharacterView view = Controller.ProjectCharacter();
            if (view == null) { GUILayout.Label("No active character."); return; }
            GUILayout.Label($"{view.Name} — {view.Race} {view.Gender} — Level {view.Level}");
            if (Controller.CharacterReadOnly) GUILayout.Label("Character examination (read-only)", GUI.skin.box);
            GUILayout.Label($"XP {view.Experience}  CP {view.CharacterPoints}  Alignment {view.Alignment}  "
                          + $"Aptitude {view.Aptitude}  AC {view.ArmorClass}");
            GUILayout.Label($"Carry {view.CarriedWeight}/{view.CarryCapacity}");
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(320f));
            GUILayout.Label("Attributes");
            foreach (GameUiAttributeView attribute in view.Attributes)
                GUILayout.Label($"{attribute.Attribute}: {attribute.Base} ({attribute.Effective})");
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Skills");
            foreach (GameUiSkillView skill in view.Skills)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{skill.Skill}: {skill.EffectiveRank}  {skill.Training}", GUILayout.ExpandWidth(true));
                GUI.enabled = !Controller.CharacterReadOnly && view.CharacterPoints > 0;
                if (GUILayout.Button("+", GUILayout.Width(34f))) Controller.IncreaseSkill(skill.Skill);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawSkills()
        {
            GameUiCharacterView view = Controller.ProjectCharacter();
            if (view == null) { GUILayout.Label("No active character."); return; }
            GUILayout.Label($"{view.Name} — character points {view.CharacterPoints}");
            foreach (GameUiSkillView skill in view.Skills)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{skill.Skill}: {skill.EffectiveRank}  {skill.Training}", GUILayout.ExpandWidth(true));
                GUI.enabled = view.CharacterPoints > 0;
                if (GUILayout.Button("+", GUILayout.Width(34f))) Controller.IncreaseSkill(skill.Skill);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawMagic()
        {
            DrawQuickSlotAssignmentPicker();
            GUILayout.Label("Known spells and maintained effects");
            foreach (GameUiSpellView spell in Controller.ProjectSpells())
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{spell.Definition.Name} — {spell.Definition.College} {spell.Definition.Rank} "
                              + $"— fatigue {spell.Definition.BaseFatigueCost}", GUILayout.ExpandWidth(true));
                GUI.enabled = spell.Learned;
                if (GUILayout.Button("BIND", GUILayout.Width(62f)))
                    Controller.AssignQuickSlotSpell(_quickSlotAssignment, spell.Definition.Id);
                if (spell.Definition.AllowsSelf && GUILayout.Button("SELF", GUILayout.Width(64f)))
                    Controller.CastSpell(spell.Definition.Id, Controller.Player);
                if (GUILayout.Button("TARGET", GUILayout.Width(72f)))
                    Controller.BeginSpellTargeting(spell.Definition.Id);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            foreach (ActiveSpellEffect effect in Controller.ProjectHud()?.ActiveEffects ?? Array.Empty<ActiveSpellEffect>())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Active spell {effect.SpellId}, magnitude {effect.Magnitude}");
                if (GUILayout.Button("CANCEL", GUILayout.Width(78f))) Controller.CancelEffect(effect.Id);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawQuickSlotAssignmentPicker()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Assign quick slot:", GUILayout.Width(120f));
            for (int index = 0; index < ProductionShortcutState.SlotCount; index++)
            {
                int slot = index;
                GUI.enabled = _quickSlotAssignment != slot;
                if (GUILayout.Button(slot == 9 ? "0" : (slot + 1).ToString(), GUILayout.Width(30f)))
                    _quickSlotAssignment = slot;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawOptions()
        {
            GUILayout.Label("Presentation options");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Master volume: {Mathf.RoundToInt(AudioListener.volume * 100f)}%", GUILayout.Width(180f));
            AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f);
            GUILayout.EndHorizontal();
            bool fullScreen = GUILayout.Toggle(Screen.fullScreen, "Fullscreen");
            if (fullScreen != Screen.fullScreen) Screen.fullScreen = fullScreen;
            GUILayout.Label("Input rebinding is not yet exposed; retail default bindings are active.", GUI.skin.box);
        }

        private void DrawTechnology()
        {
            foreach (GameUiTechnologyView discipline in Controller.ProjectTechnology())
                GUILayout.Label($"{discipline.Discipline}: learned {discipline.Learned}, effective "
                              + $"{discipline.Effective} ({discipline.EffectiveLevel})");
            GUILayout.Space(8f);
            foreach (GameUiItemView item in Controller.ProjectUsableTechnologyItems())
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(item.Name, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("USE ON SELF", GUILayout.Width(110f)))
                    Controller.UseTechnology(item.Identity, Controller.Player);
                if (GUILayout.Button("TARGET", GUILayout.Width(76f)))
                    Controller.BeginTechnologyTargeting(item.Identity);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawCrafting()
        {
            foreach (GameUiSchematicView schematic in Controller.ProjectSchematics())
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{schematic.Definition.Name} — {schematic.Definition.Acquisition}");
                GUILayout.Label(schematic.Definition.Description ?? string.Empty);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Product {schematic.ProductPrototype} x{schematic.ProductQuantity}", GUILayout.ExpandWidth(true));
                GUI.enabled = schematic.PreviewFailure == CraftingFailure.None;
                if (GUILayout.Button("CRAFT", GUILayout.Width(90f))) Controller.Craft(schematic.Definition.Id);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (schematic.PreviewFailure != CraftingFailure.None) GUILayout.Label(schematic.PreviewFailure.ToString());
                GUILayout.EndVertical();
            }
        }

        private void DrawJournal()
        {
            IReadOnlyList<QuestJournalEntry> entries = Controller.ProjectJournal();
            if (entries.Count == 0) GUILayout.Label("No known journal entries.", GUI.skin.box);
            foreach (QuestJournalEntry entry in entries)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"Day {entry.Timestamp.Days + 1} — {entry.StateLabel}");
                GUILayout.Label(entry.Description ?? string.Empty);
                GUILayout.EndVertical();
            }
        }

        private void DrawMap()
        {
            GUILayout.Label($"Current sector: {_loader?.CurrentSector ?? "Unknown"}");
            IReadOnlyList<WorldMapDestination> destinations = Controller.ProjectDestinations(out WorldMapDestinationFailure failure);
            if (failure != WorldMapDestinationFailure.None) GUILayout.Label("Unavailable: " + failure, GUI.skin.box);
            foreach (WorldMapDestination destination in destinations)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label($"{destination.DisplayName} — {destination.WorldTile}");
                GUILayout.Label(destination.Description ?? string.Empty);
                GUILayout.EndVertical();
                if (GUILayout.Button("TRAVEL", GUILayout.Width(90f), GUILayout.Height(42f)))
                    Controller.TravelTo(destination.AreaId);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawParty()
        {
            foreach (GameUiPartyMemberView member in Controller.ProjectParty())
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label(member.Name + (member.Dead ? " [DEAD]" : member.Unconscious ? " [UNCONSCIOUS]" : string.Empty));
                GUILayout.Label($"HP {member.HitPoints}/{member.MaximumHitPoints}  Fatigue {member.Fatigue}/{member.MaximumFatigue}");
                GUILayout.EndVertical();
                GUI.enabled = !member.Forced;
                if (GUILayout.Button("DISMISS", GUILayout.Width(90f), GUILayout.Height(40f)))
                    Controller.DismissFollower(member.Identity);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawMerchant()
        {
            if (Controller.CursorMode == GameUiCursorMode.MerchantTarget)
            {
                GUILayout.Label("Select a merchant in the world.", GUI.skin.box);
                return;
            }
            GUILayout.Label($"Player gold: {Controller.PlayerGold}   Merchant gold: {Controller.MerchantGold}");
            GUILayout.Label("Merchant wares");
            foreach (GameUiMerchantItemView item in Controller.ProjectMerchantWares()) DrawTradeRow(item);
            GUILayout.Label("Your items");
            foreach (GameUiMerchantItemView item in Controller.ProjectPlayerSaleItems()) DrawTradeRow(item);
        }

        private void DrawTradeRow(GameUiMerchantItemView view)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"{view.Item.Name} x{view.Item.Quantity} — {view.UnitPrice} Gold", GUILayout.ExpandWidth(true));
            GUI.enabled = view.Failure == EconomyFailure.None;
            if (GUILayout.Button(view.Direction == EconomyTransactionDirection.BuyFromMerchant ? "BUY" : "SELL",
                    GUILayout.Width(72f))) Controller.Trade(view.Direction, view.Item.Identity);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawSaveLoad()
        {
            SaveLoadPanelController save = Controller.SaveLoad;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SAVE")) save.SetMode(SaveLoadPanelMode.Save);
            if (GUILayout.Button("LOAD")) save.SetMode(SaveLoadPanelMode.Load);
            GUILayout.EndHorizontal();
            foreach (SaveLoadSlotView slot in save.Slots)
            {
                bool selected = save.SelectedSlotId == slot.SlotId;
                if (GUILayout.Button($"{(selected ? "> " : string.Empty)}{slot.SlotId} — {slot.Timestamp} — "
                                   + $"Level {slot.Level} — {slot.Location}")) save.SelectSlot(slot.SlotId);
                if (!slot.IsValid) GUILayout.Label(slot.Error ?? "Invalid save", GUI.skin.box);
            }
            if (save.Confirmation != SaveLoadConfirmation.None)
            {
                GUILayout.Label(save.ConfirmationMessage, GUI.skin.box);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("CONFIRM")) save.Confirm();
                if (GUILayout.Button("CANCEL")) save.CancelConfirmation();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (save.Mode == SaveLoadPanelMode.Save && GUILayout.Button(
                        string.IsNullOrEmpty(save.SelectedSlotId) ? $"CREATE {save.SuggestedSlotId}" : "SAVE / OVERWRITE"))
                    save.RequestSave();
                if (save.Mode == SaveLoadPanelMode.Load && GUILayout.Button("LOAD SELECTED")) Controller.RequestLoad();
                if (!string.IsNullOrEmpty(save.SelectedSlotId) && GUILayout.Button("DELETE")) save.RequestDelete();
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(save.StatusMessage)) GUILayout.Label(save.StatusMessage, GUI.skin.box);
            if (!string.IsNullOrEmpty(save.ErrorMessage)) GUILayout.Label(save.ErrorMessage, GUI.skin.box);
        }

        private void DrawDialogue()
        {
            ProductionDialogueSession dialogue = Controller.Dialogue;
            GUILayout.Label(dialogue.NpcText ?? string.Empty, GUI.skin.textArea);
            for (int index = 0; index < dialogue.AvailableResponses.Count; index++)
            {
                int choice = index;
                if (GUILayout.Button($"{index + 1}. {dialogue.AvailableResponses[index].Text}", GUILayout.MinHeight(34f)))
                    Controller.SelectDialogueResponse(choice);
            }
            if (!string.IsNullOrEmpty(dialogue.LastFailure)) GUILayout.Label(dialogue.LastFailure, GUI.skin.box);
        }

        private void DrawCursorLabel()
        {
            Vector3 mouse = Event.current.mousePosition;
            if (Controller.CursorMode == GameUiCursorMode.Default)
            {
                if (!TryGetCorpseHoverLabel(out string corpseLabel)) return;
                GUI.Box(new Rect(mouse.x + 16f, mouse.y + 16f, 82f, 24f), corpseLabel);
                return;
            }
            string label = Controller.CursorMode switch
            {
                GameUiCursorMode.SpellTarget => "SPELL",
                GameUiCursorMode.TechnologyTarget => "TECH",
                GameUiCursorMode.MerchantTarget => "BARTER",
                GameUiCursorMode.Invalid => "INVALID",
                GameUiCursorMode.Attack => "ATTACK",
                GameUiCursorMode.Use => "USE",
                GameUiCursorMode.Item => "ITEM",
                _ => Controller.CursorMode.ToString().ToUpperInvariant(),
            };
            GUI.Box(new Rect(mouse.x + 16f, mouse.y + 16f, 82f, 24f), label);
        }

        private bool TryGetCorpseHoverLabel(out string label)
        {
            label = null;
            if (Controller.IsModalOpen || _loader == null || PointerOverInterface(Input.mousePosition)) return false;
            Camera camera = Camera.main;
            if (camera == null) return false;
            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(camera.transform.position.z - _loader.transform.position.z);
            Vector3 world = camera.ScreenToWorldPoint(screen);
            if (!WorldObjectTargetSelector.TrySelectInteractionTarget(_loader.SpriteOwners, world,
                    out ArcanumObjectId target, out ObjectType type)
                || type != ObjectType.Npc || !_session.Vitality.TryGet(target, out _)
                || !_session.Vitality.IsDead(target)) return false;
            label = "LOOT";
            return true;
        }

        private void SyncInputGate()
        {
            _inputGate ??= GetComponent<PlayerInputGate>();
            _inputGate?.SetBlocked(Controller?.IsModalOpen == true);
        }

        private void EnsureController()
        {
            _session ??= GetComponent<WorldMapSessionCoordinator>();
            _loader ??= GetComponent<WorldObjectSectorLoader>();
            _inputGate ??= GetComponent<PlayerInputGate>();
            if (_controller == null && _session != null)
                _controller = new GameUiController(_session, _session.SaveSlots, _legacyCombat?.Controller);
        }

        private void DisableLegacyPresenters()
        {
            Disable(GetComponent<ProductionDialoguePresenter>());
            Disable(GetComponent<ProductionJournalPresenter>());
            Disable(GetComponent<ProductionSaveLoadPresenter>());
            Disable(GetComponent<ProductionWorldMapDestinationPresenter>());
            Disable(_legacyCombat);
        }

        private static void Disable(Behaviour behaviour)
        {
            if (behaviour != null) behaviour.enabled = false;
        }

        private static string ScreenTitle(GameUiScreen screen) => screen switch
        {
            GameUiScreen.MainMenu => "MAIN MENU",
            GameUiScreen.CharacterCreation => "CHARACTER CREATION",
            GameUiScreen.Inventory => "INVENTORY",
            GameUiScreen.Character => "CHARACTER",
            GameUiScreen.Skills => "SKILLS",
            GameUiScreen.Magic => "MAGIC",
            GameUiScreen.Technology => "TECHNOLOGY",
            GameUiScreen.Crafting => "SCHEMATICS",
            GameUiScreen.Options => "OPTIONS",
            GameUiScreen.Journal => "LOGBOOK",
            GameUiScreen.Map => "WORLD MAP",
            GameUiScreen.Party => "FOLLOWERS",
            GameUiScreen.Merchant => "BARTER",
            GameUiScreen.SaveLoad => "SAVE / LOAD",
            GameUiScreen.Dialogue => "DIALOGUE",
            GameUiScreen.Corpse => "CORPSE",
            _ => "OPENARCANUM",
        };
    }
}
