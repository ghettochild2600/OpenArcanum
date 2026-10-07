using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    public enum RetailCharacterCreationStage { Identity, Editor }
    public enum RetailCreationEditorPage { Skills, Technology, Magic }

    /// <summary>Source coordinates proven by mainmenu_ui.c and charedit_ui.c.</summary>
    public static class RetailCharacterCreationLayout
    {
        public const int IdentityBackgroundSourceId = 765;
        public const int EditorBackgroundSourceId = 22;
        public const int SkillsPanelSourceId = 29;
        public const int TechnologyPanelSourceId = 30;
        public const int MagicPanelSourceId = 31;
        public const int MinusSourceId = 23;
        public const int PlusSourceId = 24;

        public const float MainMenuWindowTop = 41f;
        public const float BottomBarTop = 441f;
        public static readonly Rect Window = new(0f, MainMenuWindowTop, 800f, 400f);
        public static readonly Rect BottomBar = new(0f, BottomBarTop, 800f, 159f);
        public static readonly Vector2 BackButton = new(69f, 496f);
        public static readonly Vector2 ContinueButton = new(675f, 496f);
        public static readonly Rect Portrait = new(95f, 28f, 64f, 64f);
        public static readonly Rect Name = new(46f, 183f, 228f, 18f);
        public static readonly Rect Gender = new(66f, 243f, 186f, 19f);
        public static readonly Rect Race = new(66f, 293f, 186f, 19f);
        public static readonly Rect Background = new(66f, 343f, 186f, 19f);
        public static readonly Rect InitialStatsTitle = new(339f, 23f, 409f, 19f);
        public static readonly Rect DescriptionTitle = new(350f, 159f, 395f, 19f);
        public static readonly Rect DescriptionBody = new(350f, 185f, 395f, 182f);
        public static readonly Rect EditorPortrait = new(12f, 10f, 89f, 89f);
        public static readonly Rect EditorSubpanel = new(503f, 4f, 235f, 325f);

        public static readonly Vector2[] AttributePlus =
        {
            new(224f, 117f), new(224f, 197f), new(224f, 157f), new(224f, 237f),
            new(367f, 117f), new(367f, 197f), new(367f, 157f), new(367f, 237f),
        };

        public static readonly Vector2[] AttributeMinus =
        {
            new(170f, 117f), new(170f, 197f), new(170f, 157f), new(170f, 237f),
            new(313f, 117f), new(313f, 197f), new(313f, 157f), new(313f, 237f),
        };

        public static readonly int[] RequiredSourceIds =
        {
            765, 321, 323, 283, 284, 757, 758,
            22, 23, 24, 29, 30, 31, 34, 35, 36, 568,
            302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313,
            624, 625, 647, 648,
        };
    }

    /// <summary>
    /// Source-backed two-stage Character Creation view. It owns only stage, subpanel, hover/selection and input
    /// focus; every choice and final transaction is submitted to the existing GameUiController/M12B authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailCharacterCreationView : MonoBehaviour
    {
        private static readonly Color SourceWhite = new Color32(231, 217, 174, 255);
        private static readonly Color SourceYellow = new Color32(245, 201, 65, 255);
        private static readonly Color SourceBlue = new Color32(108, 155, 215, 255);
        private static readonly CharacterAttribute[] Attributes =
        {
            CharacterAttribute.Strength, CharacterAttribute.Dexterity, CharacterAttribute.Constitution,
            CharacterAttribute.Beauty, CharacterAttribute.Intelligence, CharacterAttribute.Perception,
            CharacterAttribute.Willpower, CharacterAttribute.Charisma,
        };
        private static readonly int[] SpellCollegeIcons =
            { 11, 44, 38, 92, 50, 86, 56, 98, 62, 68, 74, 104, 110, 80, 116, 122 };
        private static readonly Vector2[] SpellCollegePositions =
        {
            new(13f, 15f), new(38f, 15f), new(63f, 15f), new(88f, 15f),
            new(113f, 15f), new(138f, 15f), new(162f, 15f), new(187f, 15f),
            new(25f, 40f), new(50f, 40f), new(75f, 40f), new(100f, 40f),
            new(124f, 40f), new(149f, 40f), new(174f, 40f), new(199f, 40f),
        };
        private static readonly Vector2[] TechnologyPositions =
        {
            new(8f, 7f), new(34f, 29f), new(61f, 7f), new(88f, 28f),
            new(116f, 7f), new(142f, 29f), new(171f, 7f), new(198f, 29f),
        };

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _identity;
        private RectTransform _editor;
        private RectTransform _identityNavigation;
        private RectTransform _editorNavigation;
        private RectTransform _identityDescription;
        private RectTransform _editorPanel;
        private SourceUiImage _identityBackground;
        private SourceUiImage _editorBackground;
        private InputField _nameInput;
        private Image _identityPortrait;
        private Image _editorPortrait;
        private SourceUiBitmapText _genderText;
        private SourceUiBitmapText _raceText;
        private SourceUiBitmapText _backgroundText;
        private SourceUiBitmapText _descriptionTitle;
        private SourceUiBitmapText _pointsText;
        private SourceUiBitmapText _identityFeedbackText;
        private SourceUiBitmapText _editorFeedbackText;
        private readonly List<SourceUiBitmapText> _identityStats = new();
        private readonly Dictionary<CharacterAttribute, SourceUiBitmapText> _attributeValues = new();
        private readonly Dictionary<CharacterAttribute, (SourceUiButton Minus, SourceUiButton Plus)> _attributeButtons = new();
        private readonly Dictionary<RetailCreationEditorPage, SourceUiButton> _pageButtons = new();
        private bool _wasVisible;
        private int _identitySignature = int.MinValue;
        private int _editorSignature = int.MinValue;
        private int _skillGroup;
        private SpellCollege _selectedCollege;
        private TechnologyDiscipline _selectedTechnology;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RetailCharacterCreationStage Stage { get; private set; }
        public RetailCreationEditorPage EditorPage { get; private set; }
        public int SkillGroup => _skillGroup;
        public SpellCollege SelectedCollege => _selectedCollege;
        public TechnologyDiscipline SelectedTechnology => _selectedTechnology;
        public RectTransform IdentityRoot => _identity;
        public RectTransform EditorRoot => _editor;
        public RectTransform IdentityNavigationRoot => _identityNavigation;
        public RectTransform EditorNavigationRoot => _editorNavigation;
        public RectTransform EditorPanelRoot => _editorPanel;
        public InputField NameInput => _nameInput;
        public UiResolvedAsset IdentityBackgroundAsset => _identityBackground?.CurrentAsset;
        public UiResolvedAsset EditorBackgroundAsset => _editorBackground?.CurrentAsset;

        public void Bind(GameUiController controller, WorldMapSessionCoordinator session, UiSkinResolver resolver)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (_controller == controller && ReferenceEquals(_resolver, resolver) && IsAvailable) return;
            bool preserve = _controller == controller && _controller?.Screen == GameUiScreen.CharacterCreation
                            && _controller.CreationDraft != null;
            RetailCharacterCreationStage stage = Stage;
            RetailCreationEditorPage page = EditorPage;
            int skillGroup = _skillGroup;
            SpellCollege college = _selectedCollege;
            TechnologyDiscipline technology = _selectedTechnology;
            Cleanup();
            _controller = controller;
            _session = session;
            _resolver = resolver;
            try
            {
                BuildPresentation();
                if (preserve)
                {
                    Stage = stage;
                    EditorPage = page;
                    _skillGroup = skillGroup;
                    _selectedCollege = college;
                    _selectedTechnology = technology;
                    _wasVisible = true;
                }
                Synchronize();
            }
            catch (Exception exception)
            {
                Debug.LogError("OpenArcanum UI-D Character Creation could not initialize: " + exception.Message);
                Cleanup();
            }
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.Screen == GameUiScreen.CharacterCreation
                           && _controller.CreationDraft != null;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible)
            {
                _wasVisible = false;
                return;
            }

            if (!_wasVisible)
            {
                Stage = RetailCharacterCreationStage.Identity;
                EditorPage = RetailCreationEditorPage.Skills;
                _skillGroup = 0;
                _selectedCollege = SpellCollege.Conveyance;
                _selectedTechnology = TechnologyDiscipline.Herbology;
                _identitySignature = int.MinValue;
                _editorSignature = int.MinValue;
            }
            _wasVisible = true;
            _identity.gameObject.SetActive(Stage == RetailCharacterCreationStage.Identity);
            _editor.gameObject.SetActive(Stage == RetailCharacterCreationStage.Editor);
            _identityNavigation.gameObject.SetActive(Stage == RetailCharacterCreationStage.Identity);
            _editorNavigation.gameObject.SetActive(Stage == RetailCharacterCreationStage.Editor);
            if (Stage == RetailCharacterCreationStage.Identity) RefreshIdentity();
            else RefreshEditor();
        }

        public void HandleEscape()
        {
            if (!IsVisible) return;
            if (Stage == RetailCharacterCreationStage.Editor)
            {
                Stage = RetailCharacterCreationStage.Identity;
                _editorSignature = int.MinValue;
                Synchronize();
            }
            else
                _controller.Open(GameUiScreen.MainMenu);
        }

        public void ContinueToEditor()
        {
            if (!IsVisible || Stage != RetailCharacterCreationStage.Identity) return;
            _controller.SetCreationName(_nameInput?.text ?? string.Empty);
            if (!_controller.CreationValidation.Succeeded) { RefreshIdentity(force: true); return; }
            Stage = RetailCharacterCreationStage.Editor;
            _editorSignature = int.MinValue;
            Synchronize();
        }

        public void FinalizeCharacter()
        {
            if (!IsVisible || Stage != RetailCharacterCreationStage.Editor) return;
            _controller.FinalizeNewGame();
            Synchronize();
        }

        private void Update()
        {
            Synchronize();
            if (!IsVisible || !(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) return;
            if (_nameInput != null && _nameInput.isFocused) return;
            if (Stage == RetailCharacterCreationStage.Identity) ContinueToEditor(); else FinalizeCharacter();
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Character Creation",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiPresentationRoot), typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            SourceUiPresentationRoot presentation = _canvasRoot.GetComponent<SourceUiPresentationRoot>();
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            _canvasRoot.GetComponent<Canvas>().sortingOrder = 990;
            RectTransform modal = presentation.GetLayer(SourceUiLayer.Modal);

            _identity = AddRect("Identity Stage", modal, RetailCharacterCreationLayout.Window);
            _editor = AddRect("Editor Stage", modal, RetailCharacterCreationLayout.Window);
            _identityNavigation = AddRect("Identity Bottom-Bar Controls", modal,
                RetailCharacterCreationLayout.BottomBar);
            _editorNavigation = AddRect("Editor Bottom-Bar Controls", modal,
                RetailCharacterCreationLayout.BottomBar);
            BuildIdentity();
            BuildEditor();

            var cursor = new GameObject("Character Creation Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_canvasRoot.transform, false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind((RectTransform)_canvasRoot.transform, _resolver);
        }

        private void BuildIdentity()
        {
            _identityBackground = AddSourceImage("Create Character Base", _identity,
                RetailCharacterCreationLayout.IdentityBackgroundSourceId, new Rect(0f, 0f, 800f, 400f));
            AddSourceButtonTopLeft("Identity Continue", _identityNavigation, 321,
                BottomBarLocal(RetailCharacterCreationLayout.ContinueButton), ContinueToEditor);
            AddSourceButtonTopLeft("Identity Back", _identityNavigation, 323,
                BottomBarLocal(RetailCharacterCreationLayout.BackButton),
                () => _controller.Open(GameUiScreen.MainMenu));
            AddSourceButtonTopLeft("Previous Portrait", _identity, 283, MainWindowLocal(new Vector2(43f, 118f)),
                () => CyclePortrait(-1));
            AddSourceButtonTopLeft("Next Portrait", _identity, 284, MainWindowLocal(new Vector2(244f, 118f)),
                () => CyclePortrait(1));
            AddSourceButtonTopLeft("Previous Race", _identity, 757, MainWindowLocal(new Vector2(42f, 331f)),
                () => CycleRace(-1));
            AddSourceButtonTopLeft("Next Race", _identity, 758, MainWindowLocal(new Vector2(255f, 332f)),
                () => CycleRace(1));
            AddSourceButtonTopLeft("Previous Background", _identity, 757,
                MainWindowLocal(new Vector2(42f, 381f)),
                () => CycleBackground(-1));
            AddSourceButtonTopLeft("Next Background", _identity, 758,
                MainWindowLocal(new Vector2(255f, 382f)),
                () => CycleBackground(1));
            AddSourceButtonTopLeft("Previous Gender", _identity, 757, MainWindowLocal(new Vector2(42f, 281f)),
                () => CycleGender(-1));
            AddSourceButtonTopLeft("Next Gender", _identity, 758, MainWindowLocal(new Vector2(255f, 282f)),
                () => CycleGender(1));

            _identityPortrait = AddPortraitImage("Portrait", _identity, RetailCharacterCreationLayout.Portrait);
            _nameInput = AddNameInput(_identity, RetailCharacterCreationLayout.Name);
            _genderText = AddText("Gender", _identity, RetailCharacterCreationLayout.Gender, string.Empty, 27,
                SourceWhite);
            _raceText = AddText("Race", _identity, RetailCharacterCreationLayout.Race, string.Empty, 27,
                SourceWhite);
            _backgroundText = AddText("Background", _identity, RetailCharacterCreationLayout.Background,
                string.Empty, 27, SourceWhite);
            AddText("Initial Stats", _identity, RetailCharacterCreationLayout.InitialStatsTitle,
                "Initial Stats", 27, SourceYellow);

            Rect[] stats =
            {
                new(346f, 46f, 50f, 15f), new(346f, 66f, 50f, 15f),
                new(346f, 86f, 50f, 15f), new(346f, 106f, 50f, 15f),
                new(406f, 46f, 50f, 15f), new(406f, 66f, 50f, 15f),
                new(406f, 86f, 50f, 15f), new(406f, 106f, 50f, 15f),
            };
            for (int index = 0; index < stats.Length; index++)
                _identityStats.Add(AddText($"Initial {Attributes[index]}", _identity, stats[index],
                    string.Empty, 26, SourceWhite));
            _descriptionTitle = AddText("Description Title", _identity,
                RetailCharacterCreationLayout.DescriptionTitle, "Background", 27, SourceYellow);
            _identityDescription = AddRect("Description Body", _identity,
                RetailCharacterCreationLayout.DescriptionBody);
            _identityFeedbackText = AddText("Creation Feedback", _identity, new Rect(350f, 371f, 395f, 18f),
                string.Empty, 26, new Color32(245, 130, 95, 255));
        }

        private void BuildEditor()
        {
            _editorBackground = AddSourceImage("Character Editor Base", _editor,
                RetailCharacterCreationLayout.EditorBackgroundSourceId, new Rect(0f, 0f, 800f, 400f));
            _editorPortrait = AddPortraitImage("Editor Portrait", _editor,
                RetailCharacterCreationLayout.EditorPortrait);

            for (int index = 0; index < Attributes.Length; index++)
            {
                CharacterAttribute attribute = Attributes[index];
                SourceUiButton minus = AddSourceButtonTopLeft($"Decrease {attribute}", _editor, 23,
                    RetailCharacterCreationLayout.AttributeMinus[index],
                    () => AdjustAttribute(attribute, -1));
                SourceUiButton plus = AddSourceButtonTopLeft($"Increase {attribute}", _editor, 24,
                    RetailCharacterCreationLayout.AttributePlus[index],
                    () => AdjustAttribute(attribute, 1));
                _attributeButtons[attribute] = (minus, plus);
                float x = index < 4 ? 194f : 337f;
                float y = RetailCharacterCreationLayout.AttributePlus[index].y + 1f;
                _attributeValues[attribute] = AddText($"{attribute} Value", _editor,
                    new Rect(x, y, 25f, 18f), string.Empty, 27, SourceWhite);
            }

            _pageButtons[RetailCreationEditorPage.Skills] = AddSourceButtonTopLeft("Skills", _editor, 34,
                new Vector2(527f, 11f), () => SelectPage(RetailCreationEditorPage.Skills), selected: true);
            _pageButtons[RetailCreationEditorPage.Technology] = AddSourceButtonTopLeft("Technology", _editor, 35,
                new Vector2(595f, 11f), () => SelectPage(RetailCreationEditorPage.Technology), selected: true);
            _pageButtons[RetailCreationEditorPage.Magic] = AddSourceButtonTopLeft("Magic", _editor, 36,
                new Vector2(663f, 11f), () => SelectPage(RetailCreationEditorPage.Magic), selected: true);
            AddSourceButtonTopLeft("Auto-Level Schemes", _editor, 568, new Vector2(754f, 21f),
                RejectAutoLevelSchemes);
            AddSourceButtonTopLeft("Editor Continue", _editorNavigation, 321,
                BottomBarLocal(RetailCharacterCreationLayout.ContinueButton), FinalizeCharacter);
            AddSourceButtonTopLeft("Editor Back", _editorNavigation, 323,
                BottomBarLocal(RetailCharacterCreationLayout.BackButton), HandleEscape);

            _pointsText = AddText("Character Points", _editor, new Rect(212f, 56f, 220f, 19f),
                string.Empty, 27, SourceYellow);
            _editorFeedbackText = AddText("Editor Feedback", _editor, new Rect(49f, 323f, 430f, 18f),
                string.Empty, 26, new Color32(245, 130, 95, 255));
            _editorPanel = AddRect("Editor Subpanel", _editor, RetailCharacterCreationLayout.EditorSubpanel);
        }

        private void RefreshIdentity(bool force = false)
        {
            CharacterCreationSpecification draft = _controller.CreationDraft;
            if (draft == null) return;
            int signature = DraftSignature(draft);
            if (!force && signature == _identitySignature) return;
            _identitySignature = signature;

            if (_nameInput != null && !_nameInput.isFocused && _nameInput.text != draft.Name)
                _nameInput.SetTextWithoutNotify(draft.Name ?? string.Empty);
            SetCenteredText(_genderText, Friendly(draft.Gender));
            SetCenteredText(_raceText, Friendly(draft.Race));
            BackgroundDefinition background = CurrentBackground(draft);
            SetCenteredText(_backgroundText, background?.Name ?? "Unavailable");
            SetPortrait(_identityPortrait, draft);
            for (int index = 0; index < Attributes.Length; index++)
            {
                CharacterAttribute attribute = Attributes[index];
                int effective = draft.GetAttribute(attribute)
                                + CharacterAttributeSet.SourceAdjustment(draft.Race, draft.Gender, attribute);
                SetText(_identityStats[index], $"{Short(attribute)}:{effective}");
            }
            SetText(_descriptionTitle, background?.Name ?? "Background");
            RebuildWrappedText(_identityDescription, background?.Description ?? string.Empty, 52, 11);
            SetText(_identityFeedbackText, _controller.Feedback);
        }

        private void RefreshEditor()
        {
            CharacterCreationSpecification draft = _controller.CreationDraft;
            if (draft == null) return;
            int signature = DraftSignature(draft) * 31 + (int)EditorPage * 7 + _skillGroup * 11
                            + (int)_selectedCollege * 13 + (int)_selectedTechnology * 17;
            if (signature == _editorSignature) return;
            _editorSignature = signature;
            SetPortrait(_editorPortrait, draft);
            foreach (CharacterAttribute attribute in Attributes)
            {
                SetText(_attributeValues[attribute], draft.GetAttribute(attribute).ToString());
                (SourceUiButton minus, SourceUiButton plus) = _attributeButtons[attribute];
                minus.SetInteractable(CanAdjustAttribute(draft, attribute, -1));
                plus.SetInteractable(CanAdjustAttribute(draft, attribute, 1));
            }
            foreach (var pair in _pageButtons) pair.Value.SetSelected(pair.Key == EditorPage);
            SetText(_pointsText, $"Character Points: {_controller.CreationValidation.RemainingCharacterPoints}");
            SetText(_editorFeedbackText, _controller.Feedback);
            RebuildEditorPanel(draft);
        }

        private void RejectAutoLevelSchemes()
        {
            _controller.ShowFeedback("Auto-level schemes are not available in the current runtime.");
            _editorSignature = int.MinValue;
            Synchronize();
        }

        private void RebuildEditorPanel(CharacterCreationSpecification draft)
        {
            ClearChildren(_editorPanel);
            switch (EditorPage)
            {
                case RetailCreationEditorPage.Skills: BuildSkillsPanel(draft); break;
                case RetailCreationEditorPage.Technology: BuildTechnologyPanel(draft); break;
                case RetailCreationEditorPage.Magic: BuildMagicPanel(draft); break;
            }
        }

        private void BuildSkillsPanel(CharacterCreationSpecification draft)
        {
            AddSourceImage("Skills Window", _editorPanel, 29, new Rect(0f, 0f, 235f, 325f));
            for (int group = 0; group < 4; group++)
            {
                int captured = group;
                SourceUiButton button = AddSourceButtonTopLeft($"Skill Group {group}", _editorPanel,
                    302 + group, new Vector2(new[] { 21f, 75f, 129f, 185f }[group], 17f),
                    () => { _skillGroup = captured; _editorSignature = int.MinValue; Synchronize(); }, true);
                button.SetSelected(group == _skillGroup);
            }
            for (int row = 0; row < 4; row++)
            {
                CharacterSkill skill = (CharacterSkill)(_skillGroup * 4 + row);
                float buttonY = 84f + 66f * row;
                AddSourceButtonTopLeft($"Decrease {skill}", _editorPanel, 625, new Vector2(18f, buttonY),
                    () => AdjustSkill(skill, -1)).SetInteractable(CanAdjustSkill(draft, skill, -1));
                AddSourceButtonTopLeft($"Increase {skill}", _editorPanel, 624, new Vector2(192f, buttonY),
                    () => AdjustSkill(skill, 1)).SetInteractable(CanAdjustSkill(draft, skill, 1));
                AddText($"{skill} Name", _editorPanel, new Rect(17f, 63f + 66f * row, 170f, 18f),
                    Friendly(skill), 27, SourceWhite);
                AddText($"{skill} Rank", _editorPanel, new Rect(104f, 90f + 66f * row, 40f, 18f),
                    draft.GetSkillPoints(skill).ToString(), 27, SourceYellow);
            }
        }

        private void BuildTechnologyPanel(CharacterCreationSpecification draft)
        {
            AddSourceImage("Technology Window", _editorPanel, 30, new Rect(0f, 0f, 235f, 325f));
            for (int index = 0; index < TechnologyPositions.Length; index++)
            {
                TechnologyDiscipline discipline = (TechnologyDiscipline)index;
                SourceUiButton button = AddSourceButtonTopLeft(discipline.ToString(), _editorPanel, 306 + index,
                    TechnologyPositions[index],
                    () => { _selectedTechnology = discipline; _editorSignature = int.MinValue; Synchronize(); }, true);
                button.SetSelected(discipline == _selectedTechnology);
            }
            int rank = draft.GetTechnologyRank(_selectedTechnology);
            AddText("Technology Name", _editorPanel, new Rect(17f, 69f, 205f, 19f),
                Friendly(_selectedTechnology), 27, SourceBlue);
            string[] degrees = { "Layman", "Novice", "Assistant", "Associate", "Technician", "Engineer", "Professor", "Doctorate" };
            for (int degree = 0; degree < degrees.Length; degree++)
                AddText($"Degree {degree}", _editorPanel, new Rect(46f, 91f + 33f * degree, 175f, 16f),
                    degrees[degree], 26, degree == rank ? SourceYellow : SourceWhite);
            if (rank < 7)
                AddSourceButtonTopLeft("Increase Technology", _editorPanel, 647,
                    new Vector2(8f, 96f + rank * 33f), () => AdjustTechnology(1))
                    .SetInteractable(CanAdjustTechnology(draft, _selectedTechnology, 1));
            if (rank > 0)
                AddSourceButtonTopLeft("Decrease Technology", _editorPanel, 648,
                    new Vector2(8f, 96f + (rank - 1) * 33f), () => AdjustTechnology(-1))
                    .SetInteractable(CanAdjustTechnology(draft, _selectedTechnology, -1));
        }

        private void BuildMagicPanel(CharacterCreationSpecification draft)
        {
            AddSourceImage("Magic Window", _editorPanel, 31, new Rect(0f, 0f, 235f, 325f));
            for (int index = 0; index < SpellCollegePositions.Length; index++)
            {
                SpellCollege college = (SpellCollege)index;
                SourceUiButton button = AddSourceButtonTopLeft(college.ToString(), _editorPanel,
                    SpellCollegeIcons[index], SpellCollegePositions[index],
                    () => { _selectedCollege = college; _editorSignature = int.MinValue; Synchronize(); }, true);
                button.SetSelected(college == _selectedCollege);
            }
            int rank = draft.GetSpellRank(_selectedCollege);
            AddText("College Name", _editorPanel, new Rect(18f, 69f, 205f, 19f),
                Friendly(_selectedCollege), 27, SourceBlue);
            for (int level = 0; level < 5; level++)
            {
                string name = PhaseOneSpellCatalog.TryGet((int)_selectedCollege * 5 + level,
                    out SpellDefinition spell) ? spell.Name : $"Spell {level + 1}";
                AddText($"Spell {level + 1}", _editorPanel, new Rect(59f, 76f + 48f * (level + 1), 160f, 18f),
                    name, 27, level < rank ? SourceWhite : level == rank ? SourceYellow : new Color32(135, 135, 135, 255));
                AddText($"Spell Level {level + 1}", _editorPanel,
                    new Rect(59f, 97f + 48f * (level + 1), 160f, 14f),
                    $"Minimum Level: {new[] { 1, 1, 5, 10, 15 }[level]}", 28, SourceWhite);
            }
            if (rank < 5)
                AddSourceButtonTopLeft("Increase Spell", _editorPanel, 647,
                    new Vector2(18f, 82f + 48f * rank), () => AdjustSpell(1))
                    .SetInteractable(CanAdjustSpell(draft, _selectedCollege, 1));
            if (rank > 0)
                AddSourceButtonTopLeft("Decrease Spell", _editorPanel, 648,
                    new Vector2(18f, 82f + 48f * (rank - 1)), () => AdjustSpell(-1))
                    .SetInteractable(CanAdjustSpell(draft, _selectedCollege, -1));
        }

        private void CyclePortrait(int direction)
        {
            CharacterCreationSpecification draft = _controller.CreationDraft;
            List<PortraitDefinition> values = _session.CharacterCreation.Catalog.Portraits.Values
                .Where(value => value.Race == draft.Race && value.Gender == draft.Gender)
                .OrderBy(value => value.Id).ToList();
            Cycle(values, value => value.Id, draft.PortraitId, direction, _controller.SetCreationPortrait);
            _identitySignature = int.MinValue;
            Synchronize();
        }

        private void CycleRace(int direction)
        {
            CharacterCreationSpecification draft = _controller.CreationDraft;
            List<CharacterRace> values = Enum.GetValues(typeof(CharacterRace)).Cast<CharacterRace>()
                .Where(CharacterCreationRules.IsPlayableRace).ToList();
            int index = values.IndexOf(draft.Race);
            CharacterRace race = values[Wrap(index + direction, values.Count)];
            CharacterGender gender = CharacterCreationRules.IsGenderLegal(race, draft.Gender)
                ? draft.Gender : CharacterGender.Male;
            _controller.SetCreationIdentity(race, gender);
            _identitySignature = int.MinValue;
            Synchronize();
        }

        private void CycleGender(int direction)
        {
            CharacterCreationSpecification draft = _controller.CreationDraft;
            List<CharacterGender> values = new[] { CharacterGender.Female, CharacterGender.Male }
                .Where(value => CharacterCreationRules.IsGenderLegal(draft.Race, value)).ToList();
            int index = values.IndexOf(draft.Gender);
            _controller.SetCreationIdentity(draft.Race, values[Wrap(index + direction, values.Count)]);
            _identitySignature = int.MinValue;
            Synchronize();
        }

        private void CycleBackground(int direction)
        {
            CharacterCreationSpecification draft = _controller.CreationDraft;
            List<BackgroundDefinition> values = _session.CharacterCreation.Catalog.Backgrounds.Values
                .Where(value => value.IsLegal(draft.Race, draft.Gender) && !value.HasUnsupportedEffects)
                .OrderBy(value => value.Id).ToList();
            Cycle(values, value => value.Id, draft.BackgroundId, direction, _controller.SetCreationBackground);
            _identitySignature = int.MinValue;
            Synchronize();
        }

        private static void Cycle<T>(IReadOnlyList<T> values, Func<T, int> id, int selected, int direction,
            Action<int> submit)
        {
            if (values.Count == 0) return;
            int index = -1;
            for (int i = 0; i < values.Count; i++) if (id(values[i]) == selected) { index = i; break; }
            submit(id(values[Wrap(index + direction, values.Count)]));
        }

        private void SelectPage(RetailCreationEditorPage page)
        {
            EditorPage = page;
            _editorSignature = int.MinValue;
            Synchronize();
        }

        private void AdjustAttribute(CharacterAttribute attribute, int delta)
        { _controller.AdjustCreationAttribute(attribute, delta); _editorSignature = int.MinValue; Synchronize(); }
        private void AdjustSkill(CharacterSkill skill, int delta)
        { _controller.AdjustCreationSkill(skill, delta); _editorSignature = int.MinValue; Synchronize(); }
        private void AdjustSpell(int delta)
        { _controller.AdjustCreationSpell(_selectedCollege, delta); _editorSignature = int.MinValue; Synchronize(); }
        private void AdjustTechnology(int delta)
        { _controller.AdjustCreationTechnology(_selectedTechnology, delta); _editorSignature = int.MinValue; Synchronize(); }

        private bool CanAdjustAttribute(CharacterCreationSpecification draft, CharacterAttribute attribute, int delta)
            => ValidateProspective(draft, copy => copy.SetAttribute(attribute, copy.GetAttribute(attribute) + delta));
        private bool CanAdjustSkill(CharacterCreationSpecification draft, CharacterSkill skill, int delta)
            => ValidateProspective(draft, copy => copy.SetSkillPoints(skill, copy.GetSkillPoints(skill) + delta));
        private bool CanAdjustSpell(CharacterCreationSpecification draft, SpellCollege college, int delta)
            => ValidateProspective(draft, copy => copy.SetSpellRank(college, copy.GetSpellRank(college) + delta));
        private bool CanAdjustTechnology(CharacterCreationSpecification draft, TechnologyDiscipline discipline, int delta)
            => ValidateProspective(draft,
                copy => copy.SetTechnologyRank(discipline, copy.GetTechnologyRank(discipline) + delta));

        private bool ValidateProspective(CharacterCreationSpecification source,
            Action<CharacterCreationSpecification> mutate)
        {
            CharacterCreationSpecification copy = Clone(source);
            mutate(copy);
            return _session.CharacterCreation.Validate(copy).Succeeded;
        }

        private static CharacterCreationSpecification Clone(CharacterCreationSpecification source)
        {
            var copy = new CharacterCreationSpecification
            {
                Name = source.Name, Race = source.Race, Gender = source.Gender,
                BackgroundId = source.BackgroundId, PortraitId = source.PortraitId, Age = source.Age,
            };
            foreach (CharacterAttribute value in Attributes) copy.SetAttribute(value, source.GetAttribute(value));
            foreach (CharacterSkill value in CharacterSkillRules.AllSkills)
                copy.SetSkillPoints(value, source.GetSkillPoints(value));
            for (int index = 0; index < 16; index++)
                copy.SetSpellRank((SpellCollege)index, source.GetSpellRank((SpellCollege)index));
            for (int index = 0; index < 8; index++)
                copy.SetTechnologyRank((TechnologyDiscipline)index,
                    source.GetTechnologyRank((TechnologyDiscipline)index));
            return copy;
        }

        private void SetPortrait(Image image, CharacterCreationSpecification draft)
        {
            if (image == null) return;
            image.sprite = null;
            if (!_session.CharacterCreation.Catalog.Portraits.TryGetValue(draft.PortraitId,
                    out PortraitDefinition portrait)) return;
            if (_resolver.Original.TryResolvePortrait(portrait.SourceName, out RetailPortraitAsset asset))
                image.sprite = asset.Sprite;
        }

        private BackgroundDefinition CurrentBackground(CharacterCreationSpecification draft)
            => _session.CharacterCreation.Catalog.TryGetBackground(draft.BackgroundId, out BackgroundDefinition value)
                ? value : null;

        private InputField AddNameInput(RectTransform parent, Rect rect)
        {
            RectTransform root = AddRect("Name Entry", parent, rect);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = Color.black;
            InputField input = root.gameObject.AddComponent<InputField>();
            input.characterLimit = CharacterCreationRules.MaximumNameLength;
            input.lineType = InputField.LineType.SingleLine;

            RectTransform textRect = AddRect("Text", root, new Rect(3f, 0f, rect.width - 6f, rect.height));
            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = SourceWhite;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            input.textComponent = text;

            RectTransform placeholderRect = AddRect("Placeholder", root,
                new Rect(3f, 0f, rect.width - 6f, rect.height));
            Text placeholder = placeholderRect.gameObject.AddComponent<Text>();
            placeholder.font = text.font;
            placeholder.fontSize = 14;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color32(135, 135, 135, 255);
            placeholder.alignment = TextAnchor.MiddleCenter;
            placeholder.text = "Choose Name";
            input.placeholder = placeholder;
            input.onValueChanged.AddListener(value =>
            {
                _controller?.SetCreationName(value);
                _identitySignature = int.MinValue;
            });
            return input;
        }

        private Image AddPortraitImage(string name, RectTransform parent, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private SourceUiImage AddSourceImage(string name, RectTransform parent, int sourceId, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            SourceUiImage source = target.gameObject.AddComponent<SourceUiImage>();
            source.Bind(_resolver, new UiAssetKey(sourceId));
            return source;
        }

        private SourceUiButton AddSourceButtonTopLeft(string name, RectTransform parent, int sourceId,
            Vector2 topLeft, Action clicked, bool selected = false)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(32f, 32f);
            return AddSourceButton(name, parent, sourceId,
                new Rect(topLeft.x, topLeft.y, size.x, size.y), clicked, selected);
        }

        private SourceUiButton AddSourceButton(string name, RectTransform parent, int sourceId, Rect rect,
            Action clicked, bool selected)
        {
            RectTransform target = AddRect(name, parent, rect);
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            source.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 2), new UiAssetKey(sourceId, frame: 0),
                selected ? new UiAssetKey(sourceId, frame: 2) : null);
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private static Vector2 MainWindowLocal(Vector2 sourceTopLeft)
            => new(sourceTopLeft.x, sourceTopLeft.y - RetailCharacterCreationLayout.MainMenuWindowTop);

        private static Vector2 BottomBarLocal(Vector2 sourceTopLeft)
            => new(sourceTopLeft.x, sourceTopLeft.y - RetailCharacterCreationLayout.BottomBarTop);

        private static Button AddTransparentButton(string name, RectTransform parent, Rect rect, Action clicked)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            Button button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked());
            return button;
        }

        private SourceUiBitmapText AddText(string name, RectTransform parent, Rect rect, string value,
            int fontSourceId, Color color)
        {
            RectTransform target = AddRect(name, parent, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            text.Bind(_resolver, value ?? string.Empty, color, fontSourceId);
            return text;
        }

        private void SetCenteredText(SourceUiBitmapText text, string value)
        {
            RectTransform rect = (RectTransform)text.transform;
            Vector2 anchored = rect.anchoredPosition;
            Vector2 bounds = rect.sizeDelta;
            text.Bind(_resolver, value ?? string.Empty, SourceWhite, text.FontSourceId);
            Vector2 size = text.LogicalSize;
            rect.anchoredPosition = new Vector2(anchored.x + Mathf.Max(0f, (bounds.x - size.x) * .5f), anchored.y);
        }

        private void SetText(SourceUiBitmapText text, string value)
        {
            if (text == null || text.Text == (value ?? string.Empty)) return;
            text.Bind(_resolver, value ?? string.Empty, SourceWhite, text.FontSourceId);
        }

        private void RebuildWrappedText(RectTransform parent, string value, int columns, int maxLines)
        {
            ClearChildren(parent);
            List<string> lines = WrapText(value, columns, maxLines);
            for (int index = 0; index < lines.Count; index++)
                AddText($"Line {index + 1}", parent, new Rect(0f, index * 15f, parent.rect.width, 15f),
                    lines[index], 26, SourceWhite);
        }

        private static List<string> WrapText(string value, int columns, int maxLines)
        {
            var lines = new List<string>();
            var current = string.Empty;
            foreach (string word in (value ?? string.Empty).Split((char[])null,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current.Length == 0 ? word : current + " " + word;
                if (next.Length <= columns) current = next;
                else
                {
                    if (current.Length > 0) lines.Add(current);
                    current = word;
                    if (lines.Count >= maxLines) break;
                }
            }
            if (current.Length > 0 && lines.Count < maxLines) lines.Add(current);
            return lines;
        }

        private static RectTransform AddRect(string name, RectTransform parent, Rect rect)
        {
            GameObject go = new(name, typeof(RectTransform));
            RectTransform target = go.GetComponent<RectTransform>();
            target.SetParent(parent, false);
            target.anchorMin = target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
            return target;
        }

        private static void ClearChildren(RectTransform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }

        private static int DraftSignature(CharacterCreationSpecification value)
        {
            unchecked
            {
                int hash = value.Name?.GetHashCode() ?? 0;
                hash = hash * 31 + (int)value.Race;
                hash = hash * 31 + (int)value.Gender;
                hash = hash * 31 + value.PortraitId;
                hash = hash * 31 + value.BackgroundId;
                foreach (CharacterAttribute item in Attributes) hash = hash * 31 + value.GetAttribute(item);
                foreach (CharacterSkill item in CharacterSkillRules.AllSkills)
                    hash = hash * 31 + value.GetSkillPoints(item);
                for (int index = 0; index < 16; index++)
                    hash = hash * 31 + value.GetSpellRank((SpellCollege)index);
                for (int index = 0; index < 8; index++)
                    hash = hash * 31 + value.GetTechnologyRank((TechnologyDiscipline)index);
                return hash;
            }
        }

        private static int Wrap(int value, int count) => count == 0 ? 0 : (value % count + count) % count;
        private static string Short(CharacterAttribute value) => value switch
        {
            CharacterAttribute.Strength => "ST", CharacterAttribute.Dexterity => "DX",
            CharacterAttribute.Constitution => "CN", CharacterAttribute.Beauty => "BE",
            CharacterAttribute.Intelligence => "IN", CharacterAttribute.Perception => "PE",
            CharacterAttribute.Willpower => "WP", CharacterAttribute.Charisma => "CH", _ => "?",
        };

        private static string Friendly(Enum value)
        {
            string text = value.ToString();
            for (int index = text.Length - 1; index > 0; index--)
                if (char.IsUpper(text[index]) && !char.IsUpper(text[index - 1])) text = text.Insert(index, " ");
            return text;
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _identity = null;
            _editor = null;
            _identityDescription = null;
            _editorPanel = null;
            _identityBackground = null;
            _editorBackground = null;
            _nameInput = null;
            _identityPortrait = null;
            _editorPortrait = null;
            _identityStats.Clear();
            _attributeValues.Clear();
            _attributeButtons.Clear();
            _pageButtons.Clear();
            _controller = null;
            _session = null;
            _resolver = null;
            _wasVisible = false;
            _identitySignature = int.MinValue;
            _editorSignature = int.MinValue;
        }
    }
}
