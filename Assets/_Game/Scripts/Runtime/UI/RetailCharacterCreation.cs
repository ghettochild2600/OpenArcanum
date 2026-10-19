using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Crafting;
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

    public readonly struct RetailMessageWindowProjection
    {
        public string Title { get; }
        public string Body { get; }

        public RetailMessageWindowProjection(string title, string body)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public static RetailMessageWindowProjection FromSource(string value)
        {
            string normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            int newline = normalized.IndexOf('\n');
            return newline >= 0
                ? new RetailMessageWindowProjection(normalized[..newline].Trim(), normalized[(newline + 1)..].Trim())
                : new RetailMessageWindowProjection(string.Empty, normalized.Trim());
        }
    }

    /// <summary>Source coordinates proven by mainmenu_ui.c and charedit_ui.c.</summary>
    public static class RetailCharacterCreationLayout
    {
        public const int IdentityBackgroundSourceId = 765;
        public const int TopCoverSourceId = RetailPartialMainMenuLayout.TopCoverSourceId;
        public const int BottomCoverSourceId = RetailPartialMainMenuLayout.BottomCoverSourceId;
        public const int EditorBackgroundSourceId = 22;
        public const int SkillsPanelSourceId = 29;
        public const int TechnologyPanelSourceId = 30;
        public const int MagicPanelSourceId = 31;
        public const int MinusSourceId = 23;
        public const int PlusSourceId = 24;
        public const int FeedbackIconSourceId = 444;
        public const int AlignmentMarkerSourceId = 254;
        public const int AptitudeMarkerSourceId = 255;

        public const float MainMenuWindowTop = 41f;
        public const float BottomBarTop = 441f;
        public static readonly Rect Window = new(0f, MainMenuWindowTop, 800f, 400f);
        public static readonly Rect BottomBar = new(0f, BottomBarTop, 800f, 159f);
        public static readonly Vector2 BackButton = new(69f, 496f);
        public static readonly Vector2 ContinueButton = new(675f, 496f);
        public static readonly Rect Portrait = new(95f, 28f, 64f, 64f);
        public static readonly Rect PortraitDraw = new(95f, 28f, 128f, 128f);
        public static readonly Rect Name = new(46f, 183f, 228f, 19f);
        public static readonly Rect Gender = new(66f, 243f, 186f, 19f);
        public static readonly Rect Race = new(66f, 293f, 186f, 19f);
        public static readonly Rect Background = new(66f, 343f, 186f, 19f);
        public static readonly Rect InitialStatsTitle = new(339f, 23f, 409f, 19f);
        public static readonly Rect DescriptionTitle = new(350f, 159f, 395f, 19f);
        public static readonly Rect DescriptionBody = new(350f, 185f, 395f, 182f);
        public static readonly Rect EditorPortrait = new(141f, 21f, 66f, 66f);
        // The independent source subwindows begin at desktop y=104 while the
        // Character Editor center window begins at y=41.
        public static readonly Rect EditorSubpanel = new(503f, 63f, 235f, 325f);
        public static readonly Rect EditorName = new(212f, 15f, 270f, 19f);
        public static readonly Rect EditorLevel = new(212f, 34f, 120f, 19f);
        public static readonly Rect EditorPoints = new(212f, 53f, 120f, 19f);
        public static readonly Rect EditorExperienceNext = new(212f, 72f, 150f, 19f);
        public static readonly Rect EditorRace = new(337f, 34f, 150f, 19f);
        public static readonly Rect EditorGender = new(337f, 53f, 150f, 19f);
        public static readonly Rect EditorAge = new(337f, 72f, 120f, 19f);
        // intgame_message_window_draw_image paints feedback ART at (217, 69) in the
        // bottom interface window. These are bottom-window-local coordinates.
        public static readonly Rect EditorMessageIcon = new(217f, 69f, 64f, 64f);
        public static readonly Rect EditorMessageTitle = new(290f, 63f, 291f, 19f);
        public static readonly Rect EditorMessageBody = new(290f, 87f, 291f, 54f);

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

        // charedit_refresh_stat formats " %d ". Native 800x600 pixel capture shows that the
        // left oval's visible interior needs its own optical center; the right column already
        // matches the source horizontally; both columns share the source-rendered baseline.
        public static readonly float[] AttributeValueCenterX =
            { 206f, 206f, 206f, 206f, 350f, 350f, 350f, 350f };
        public const float AttributeValueTopOffsetFromPlus = -3f;
        public const float VitalityValueCenterX = 447f;
        public const float AlignmentValueCenterX = 57f;

        public static readonly int[] RequiredSourceIds =
        {
            336, 335, 765, 321, 323, 283, 284, 757, 758,
            22, 23, 24, 29, 30, 31, 34, 35, 36, 568,
            302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313,
            624, 625, 647, 648, 444, 254, 255,
        };
    }

    internal sealed class RetailNameInputField : InputField
    {
        public Action<bool> FocusChanged { get; set; }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            FocusChanged?.Invoke(true);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            FocusChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// Source-backed two-stage Character Creation view. It owns only stage, subpanel, hover/selection and input
    /// focus; every choice and final transaction is submitted to the existing GameUiController/M12B authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailCharacterCreationView : MonoBehaviour
    {
        private enum IdentityContext { Resting, Background, Gender, Race }

        private static readonly Color SourceWhite = new Color32(250, 250, 250, 255);
        private static readonly Color SourceRed = new Color32(255, 50, 50, 255);
        private static readonly Color SourceYellow = new Color32(255, 210, 0, 255);
        private static readonly Color SourceBlue = new Color32(108, 155, 215, 255);
        private static readonly Color SourceGray = new Color32(135, 135, 135, 255);
        private static readonly Color TechnologyWhite = new Color32(255, 255, 255, 255);
        private static readonly Color TechnologyYellow = new Color32(255, 255, 0, 255);
        private static readonly Color TechnologyBlue = new Color32(0, 0, 255, 255);
        private static readonly Color TechnologyGray = new Color32(128, 128, 128, 255);
        private static readonly Color MagicWhite = new Color32(255, 255, 255, 255);
        private static readonly Color MagicGray = new Color32(128, 128, 128, 255);
        private static readonly CharacterAttribute[] Attributes =
        {
            CharacterAttribute.Strength, CharacterAttribute.Dexterity, CharacterAttribute.Constitution,
            CharacterAttribute.Beauty, CharacterAttribute.Intelligence, CharacterAttribute.Perception,
            CharacterAttribute.Willpower, CharacterAttribute.Charisma,
        };
        private static readonly CharacterAttribute[] InitialAttributeOrder =
        {
            CharacterAttribute.Strength, CharacterAttribute.Constitution, CharacterAttribute.Dexterity,
            CharacterAttribute.Beauty, CharacterAttribute.Intelligence, CharacterAttribute.Willpower,
            CharacterAttribute.Perception, CharacterAttribute.Charisma,
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
        private static readonly int[] TechnologyDegreeIcons = { 653, 649, 650, 651, 652, 654, 655, 656 };

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _topRegion;
        private RectTransform _identity;
        private RectTransform _editor;
        private RectTransform _identityNavigation;
        private RectTransform _editorNavigation;
        private RectTransform _identityDescription;
        private RectTransform _editorPanel;
        private SourceUiImage _identityBackground;
        private SourceUiImage _editorBackground;
        private SourceUiImage _topBackground;
        private SourceUiImage _identityBottomBackground;
        private SourceUiImage _editorBottomBackground;
        private InputField _nameInput;
        private SourceUiBitmapText _nameText;
        private Image _identityPortrait;
        private Image _editorPortrait;
        private SourceUiBitmapText _genderText;
        private SourceUiBitmapText _raceText;
        private SourceUiBitmapText _backgroundText;
        private SourceUiBitmapText _descriptionTitle;
        private SourceUiBitmapText _identityMessageText;
        private SourceUiBitmapText _identityMessageTitle;
        private RectTransform _identityMessageBody;
        private SourceUiImage _identityMessageIcon;
        private SourceUiBitmapText _pointsText;
        private SourceUiBitmapText _editorNameText;
        private SourceUiBitmapText _editorLevelText;
        private SourceUiBitmapText _editorExperienceText;
        private SourceUiBitmapText _editorRaceText;
        private SourceUiBitmapText _editorGenderText;
        private SourceUiBitmapText _editorAgeText;
        private SourceUiBitmapText _hitPointValue;
        private SourceUiBitmapText _fatigueValue;
        private (SourceUiButton Minus, SourceUiButton Plus) _hitPointButtons;
        private (SourceUiButton Minus, SourceUiButton Plus) _fatigueButtons;
        private SourceUiBitmapText _editorMessageTitle;
        private SourceUiBitmapText _editorMessageRequirement;
        private RectTransform _editorMessageBody;
        private SourceUiImage _editorMessageIcon;
        private SourceUiImage _aptitudeMarker;
        private SourceUiBitmapText _magickAptitudeText;
        private SourceUiBitmapText _technologyAptitudeText;
        private readonly List<SourceUiBitmapText> _editorDerivedStats = new();
        private readonly List<SourceUiBitmapText> _editorResistances = new();
        private readonly List<SourceUiBitmapText> _identityStats = new();
        private readonly List<SourceUiBitmapText> _identityDerivedStats = new();
        private readonly Dictionary<CharacterAttribute, SourceUiBitmapText> _attributeValues = new();
        private readonly Dictionary<CharacterAttribute, (SourceUiButton Minus, SourceUiButton Plus)> _attributeButtons = new();
        private readonly Dictionary<RetailCreationEditorPage, SourceUiButton> _pageButtons = new();
        private readonly Dictionary<SourceUiBitmapText, Rect> _textBounds = new();
        private readonly Dictionary<CharacterSkill, Image> _skillGauges = new();
        private bool _wasVisible;
        private int _identitySignature = int.MinValue;
        private int _editorSignature = int.MinValue;
        private int _skillGroup;
        private SpellCollege _selectedCollege;
        private TechnologyDiscipline _selectedTechnology;
        private bool _autoLevelSchemesOpen;
        private int _autoLevelSchemeOffset;
        private bool _nameEditing;
        private IdentityContext _identityContext = IdentityContext.Resting;
        private string _identityValidation = string.Empty;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public RetailCharacterCreationStage Stage { get; private set; }
        public RetailCreationEditorPage EditorPage { get; private set; }
        public int SkillGroup => _skillGroup;
        public SpellCollege SelectedCollege => _selectedCollege;
        public TechnologyDiscipline SelectedTechnology => _selectedTechnology;
        public RectTransform IdentityRoot => _identity;
        public RectTransform EditorRoot => _editor;
        public RectTransform TopRegionRoot => _topRegion;
        public RectTransform IdentityBottomRegionRoot => _identityNavigation;
        public RectTransform EditorBottomRegionRoot => _editorNavigation;
        public RectTransform IdentityNavigationRoot => _identityNavigation;
        public RectTransform EditorNavigationRoot => _editorNavigation;
        public RectTransform EditorPanelRoot => _editorPanel;
        public InputField NameInput => _nameInput;
        public UiResolvedAsset IdentityBackgroundAsset => _identityBackground?.CurrentAsset;
        public UiResolvedAsset EditorBackgroundAsset => _editorBackground?.CurrentAsset;
        public UiResolvedAsset TopBackgroundAsset => _topBackground?.CurrentAsset;
        public UiResolvedAsset IdentityBottomBackgroundAsset => _identityBottomBackground?.CurrentAsset;
        public Image IdentityPortraitImage => _identityPortrait;
        public Image EditorPortraitImage => _editorPortrait;
        public IReadOnlyDictionary<CharacterSkill, Image> SkillGauges => _skillGauges;

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
                _identityContext = IdentityContext.Resting;
                _identityValidation = string.Empty;
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
                if (_autoLevelSchemesOpen)
                {
                    _autoLevelSchemesOpen = false;
                    _editorSignature = int.MinValue;
                    Synchronize();
                    return;
                }
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
            if (!_controller.CreationValidation.Succeeded)
            {
                _identityValidation = _controller.CreationValidation.Failure == CharacterCreationFailure.InvalidName
                    ? ReadMainMenuMessage(506, "You must choose a name.")
                    : _controller.CreationValidation.Message;
                RefreshIdentity(force: true);
                return;
            }
            _identityValidation = string.Empty;
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
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            RetailPartialMainMenuComposition.ConfigureNativeCanvas(_canvasRoot, _resolver, 990);
            RectTransform canvas = (RectTransform)_canvasRoot.transform;
            RetailPartialMainMenuRegion top = RetailPartialMainMenuComposition.CreateTop(canvas, _resolver,
                "Character Creation Top Region");
            RetailPartialMainMenuRegion identity = RetailPartialMainMenuComposition.CreateCenter(canvas, _resolver,
                RetailCharacterCreationLayout.IdentityBackgroundSourceId, "Identity Center Region");
            RetailPartialMainMenuRegion editor = RetailPartialMainMenuComposition.CreateCenter(canvas, _resolver,
                RetailCharacterCreationLayout.EditorBackgroundSourceId, "Editor Center Region");
            RetailPartialMainMenuRegion identityBottom = RetailPartialMainMenuComposition.CreateBottom(canvas,
                _resolver, "Identity Bottom Region");
            RetailPartialMainMenuRegion editorBottom = RetailPartialMainMenuComposition.CreateBottom(canvas,
                _resolver, "Editor Bottom Region");
            _topRegion = top.Root;
            _topBackground = top.Background;
            _identity = identity.Root;
            _identityBackground = identity.Background;
            _editor = editor.Root;
            _editorBackground = editor.Background;
            _identityNavigation = identityBottom.Root;
            _identityBottomBackground = identityBottom.Background;
            _editorNavigation = editorBottom.Root;
            _editorBottomBackground = editorBottom.Background;
            BuildIdentity();
            BuildEditor();

            var cursor = new GameObject("Character Creation Cursor",
                typeof(RectTransform), typeof(Image), typeof(SourceUiImage), typeof(RetailMainMenuCursorView));
            cursor.transform.SetParent(_canvasRoot.transform, false);
            cursor.GetComponent<RetailMainMenuCursorView>().Bind((RectTransform)_canvasRoot.transform, _resolver);
        }

        private void BuildIdentity()
        {
            // The portrait is a 128x128 draw into a 64x64 invalidation region. Source buttons are widgets
            // painted above that draw, so create the portrait before the overlapping arrow controls.
            _identityPortrait = AddPortraitImage("Portrait", _identity, RetailCharacterCreationLayout.PortraitDraw);
            SourceUiButton continueButton = AddSourceButtonTopLeft("Identity Continue", _identityNavigation, 321,
                BottomBarLocal(RetailCharacterCreationLayout.ContinueButton), ContinueToEditor);
            SourceUiButton backButton = AddSourceButtonTopLeft("Identity Back", _identityNavigation, 323,
                BottomBarLocal(RetailCharacterCreationLayout.BackButton),
                () => _controller.Open(GameUiScreen.MainMenu));
            BindNavigationHint(continueButton.gameObject, true, true);
            BindNavigationHint(backButton.gameObject, false, true);
            AddSourceButtonTopLeft("Previous Portrait", _identity, 283, MainWindowLocal(new Vector2(43f, 118f)),
                () => CyclePortrait(-1));
            AddSourceButtonTopLeft("Next Portrait", _identity, 284, MainWindowLocal(new Vector2(244f, 118f)),
                () => CyclePortrait(1));
            SourceUiButton previousRace = AddSourceButtonTopLeft("Previous Race", _identity, 757,
                MainWindowLocal(new Vector2(42f, 331f)), () => CycleRace(-1));
            SourceUiButton nextRace = AddSourceButtonTopLeft("Next Race", _identity, 758,
                MainWindowLocal(new Vector2(255f, 332f)), () => CycleRace(1));
            SourceUiButton previousBackground = AddSourceButtonTopLeft("Previous Background", _identity, 757,
                MainWindowLocal(new Vector2(42f, 381f)),
                () => CycleBackground(-1));
            SourceUiButton nextBackground = AddSourceButtonTopLeft("Next Background", _identity, 758,
                MainWindowLocal(new Vector2(255f, 382f)),
                () => CycleBackground(1));
            SourceUiButton previousGender = AddSourceButtonTopLeft("Previous Gender", _identity, 757,
                MainWindowLocal(new Vector2(42f, 281f)), () => CycleGender(-1));
            SourceUiButton nextGender = AddSourceButtonTopLeft("Next Gender", _identity, 758,
                MainWindowLocal(new Vector2(255f, 282f)), () => CycleGender(1));

            _nameInput = AddNameInput(_identity, RetailCharacterCreationLayout.Name);
            _genderText = AddText("Gender", _identity, RetailCharacterCreationLayout.Gender, string.Empty,
                SourceUiBitmapText.Flare12FontSourceId, SourceRed);
            _raceText = AddText("Race", _identity, RetailCharacterCreationLayout.Race, string.Empty,
                SourceUiBitmapText.Flare12FontSourceId, SourceRed);
            _backgroundText = AddText("Background", _identity, RetailCharacterCreationLayout.Background,
                string.Empty, SourceUiBitmapText.Flare12FontSourceId, SourceRed);
            Button genderContext = AddTransparentButton("Gender Context", _identity,
                new Rect(22f, 231f, 276f, 39f), () => { });
            Button raceContext = AddTransparentButton("Race Context", _identity,
                new Rect(22f, 281f, 276f, 40f), () => { });
            Button backgroundContext = AddTransparentButton("Background Context", _identity,
                new Rect(22f, 331f, 276f, 50f), () => { });
            foreach (Button context in new[] { genderContext, raceContext, backgroundContext })
                context.transform.SetAsFirstSibling();
            BindIdentityHint(genderContext.gameObject, IdentityContext.Gender);
            BindIdentityHint(raceContext.gameObject, IdentityContext.Race);
            BindIdentityHint(backgroundContext.gameObject, IdentityContext.Background);
            foreach (SourceUiButton button in new[] { previousGender, nextGender })
                BindIdentityHint(button.gameObject, IdentityContext.Gender);
            foreach (SourceUiButton button in new[] { previousRace, nextRace })
                BindIdentityHint(button.gameObject, IdentityContext.Race);
            foreach (SourceUiButton button in new[] { previousBackground, nextBackground })
                BindIdentityHint(button.gameObject, IdentityContext.Background);
            SourceUiBitmapText initialStatsTitle = AddText("Initial Stats", _identity,
                RetailCharacterCreationLayout.InitialStatsTitle, string.Empty,
                SourceUiBitmapText.Morph15FontSourceId, SourceWhite);
            SetCenteredText(initialStatsTitle, "Initial Stats", SourceWhite);

            Rect[] stats =
            {
                new(346f, 46f, 50f, 15f), new(346f, 66f, 50f, 15f),
                new(346f, 86f, 50f, 15f), new(346f, 106f, 50f, 15f),
                new(406f, 46f, 50f, 15f), new(406f, 66f, 50f, 15f),
                new(406f, 86f, 50f, 15f), new(406f, 106f, 50f, 15f),
            };
            for (int index = 0; index < stats.Length; index++)
                _identityStats.Add(AddText($"Initial {InitialAttributeOrder[index]}", _identity, stats[index],
                    string.Empty, SourceUiBitmapText.Flare12FontSourceId, SourceWhite));
            Rect[] derivedStats =
            {
                new(472f, 46f, 139f, 15f), new(472f, 66f, 139f, 15f),
                new(472f, 86f, 139f, 15f), new(472f, 106f, 139f, 15f),
                new(618f, 46f, 142f, 15f), new(618f, 65f, 147f, 15f),
                new(618f, 86f, 147f, 15f),
            };
            for (int index = 0; index < derivedStats.Length; index++)
                _identityDerivedStats.Add(AddText($"Initial Derived {index}", _identity, derivedStats[index],
                    string.Empty, SourceUiBitmapText.Flare12FontSourceId, SourceWhite));
            _descriptionTitle = AddText("Description Title", _identity,
                RetailCharacterCreationLayout.DescriptionTitle, string.Empty,
                SourceUiBitmapText.Morph15FontSourceId, SourceWhite);
            SetCenteredText(_descriptionTitle, "Background", SourceWhite);
            _identityDescription = AddRect("Description Body", _identity,
                RetailCharacterCreationLayout.DescriptionBody);
            _identityMessageIcon = AddSourceImage("Identity Message Icon", _identityNavigation,
                RetailCharacterCreationLayout.FeedbackIconSourceId, RetailCharacterCreationLayout.EditorMessageIcon);
            _identityMessageTitle = AddText("Identity Message Title", _identityNavigation,
                RetailCharacterCreationLayout.EditorMessageTitle, string.Empty, 27, SourceWhite);
            _identityMessageBody = AddRect("Identity Message Body", _identityNavigation,
                RetailCharacterCreationLayout.EditorMessageBody);
            _identityMessageText = AddText("Identity Validation", _identityNavigation,
                new Rect(205f, 78f, 390f, 19f), string.Empty, 26, SourceWhite);
            SetIdentityBottomMessage(string.Empty, string.Empty, false);
        }

        private void BuildEditor()
        {
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
                float x = RetailCharacterCreationLayout.AttributeValueCenterX[index];
                float y = RetailCharacterCreationLayout.AttributePlus[index].y
                          + RetailCharacterCreationLayout.AttributeValueTopOffsetFromPlus;
                _attributeValues[attribute] = AddText($"{attribute} Value", _editor,
                    new Rect(x, y, 0f, 18f), string.Empty, 27, SourceWhite);
                BindHint(minus.gameObject, AttributeHintId(attribute), () => 1);
                BindHint(plus.gameObject, AttributeHintId(attribute), () => 1);
                BindHint(_attributeValues[attribute].gameObject, AttributeHintId(attribute), () => 1);
            }

            SourceUiButton hpMinus = AddSourceButtonTopLeft("Decrease Hit Points", _editor, 23,
                new Vector2(409f, 144f), () => AdjustVitality(true, -1));
            SourceUiButton hpPlus = AddSourceButtonTopLeft("Increase Hit Points", _editor, 24,
                new Vector2(465f, 144f), () => AdjustVitality(true, 1));
            SourceUiButton fatigueMinus = AddSourceButtonTopLeft("Decrease Fatigue", _editor, 23,
                new Vector2(409f, 214f), () => AdjustVitality(false, -1));
            SourceUiButton fatiguePlus = AddSourceButtonTopLeft("Increase Fatigue", _editor, 24,
                new Vector2(465f, 214f), () => AdjustVitality(false, 1));
            _hitPointButtons = (hpMinus, hpPlus);
            _fatigueButtons = (fatigueMinus, fatiguePlus);
            _hitPointValue = AddText("Hit Point Value", _editor, new Rect(RetailCharacterCreationLayout.VitalityValueCenterX, 143f, 0f, 18f),
                string.Empty, 27, SourceWhite);
            _fatigueValue = AddText("Fatigue Value", _editor, new Rect(RetailCharacterCreationLayout.VitalityValueCenterX, 213f, 0f, 18f),
                string.Empty, 27, SourceWhite);
            foreach (GameObject target in new[] { hpMinus.gameObject, hpPlus.gameObject, _hitPointValue.gameObject })
                BindHint(target, 106, () => 1);
            foreach (GameObject target in new[] { fatigueMinus.gameObject, fatiguePlus.gameObject, _fatigueValue.gameObject })
                BindHint(target, 107, () => 1);

            _pageButtons[RetailCreationEditorPage.Skills] = AddSourceButtonTopLeft("Skills", _editor, 34,
                new Vector2(527f, 11f), () => SelectPage(RetailCreationEditorPage.Skills), selected: true);
            _pageButtons[RetailCreationEditorPage.Technology] = AddSourceButtonTopLeft("Technology", _editor, 35,
                new Vector2(595f, 11f), () => SelectPage(RetailCreationEditorPage.Technology), selected: true);
            _pageButtons[RetailCreationEditorPage.Magic] = AddSourceButtonTopLeft("Magic", _editor, 36,
                new Vector2(663f, 11f), () => SelectPage(RetailCreationEditorPage.Magic), selected: true);
            BindHint(_pageButtons[RetailCreationEditorPage.Skills].gameObject, 132, () => 0);
            BindHint(_pageButtons[RetailCreationEditorPage.Technology].gameObject, 133, () => 0);
            BindHint(_pageButtons[RetailCreationEditorPage.Magic].gameObject, 134, () => 0);
            SourceUiButton autoLevel = AddSourceButtonTopLeft("Auto-Level Schemes", _editor, 568,
                new Vector2(754f, 21f), OpenAutoLevelSchemes);
            BindHint(autoLevel.gameObject, 140, () => 0);
            SourceUiButton continueButton = AddSourceButtonTopLeft("Editor Continue", _editorNavigation, 321,
                BottomBarLocal(RetailCharacterCreationLayout.ContinueButton), FinalizeCharacter);
            SourceUiButton backButton = AddSourceButtonTopLeft("Editor Back", _editorNavigation, 323,
                BottomBarLocal(RetailCharacterCreationLayout.BackButton), HandleEscape);
            BindNavigationHint(continueButton.gameObject, true, false);
            BindNavigationHint(backButton.gameObject, false, false);

            _editorNameText = AddText("Editor Name", _editor, RetailCharacterCreationLayout.EditorName,
                string.Empty, 27, SourceYellow);
            _editorLevelText = AddText("Editor Level", _editor, RetailCharacterCreationLayout.EditorLevel,
                string.Empty, 27, SourceWhite);
            _pointsText = AddText("Character Points", _editor, RetailCharacterCreationLayout.EditorPoints,
                string.Empty, 27, SourceWhite);
            _editorExperienceText = AddText("Editor Experience Next", _editor,
                RetailCharacterCreationLayout.EditorExperienceNext, string.Empty, 27, SourceWhite);
            _editorRaceText = AddText("Editor Race", _editor, RetailCharacterCreationLayout.EditorRace,
                string.Empty, 27, SourceWhite);
            _editorGenderText = AddText("Editor Gender", _editor, RetailCharacterCreationLayout.EditorGender,
                string.Empty, 27, SourceWhite);
            _editorAgeText = AddText("Editor Age", _editor, RetailCharacterCreationLayout.EditorAge,
                string.Empty, 27, SourceWhite);

            SourceUiBitmapText derivedStatsHeading = AddText("Derived Stats Heading", _editor,
                new Rect(178f, 282f, 0f, 18f), string.Empty,
                SourceUiBitmapText.Morph15FontSourceId, SourceWhite);
            SetSourceIntegerCenteredText(derivedStatsHeading, "Derived Stats", 178f, SourceWhite);
            SourceUiBitmapText resistanceHeading = AddText("Resistance Heading", _editor,
                new Rect(383f, 282f, 0f, 18f), string.Empty,
                SourceUiBitmapText.Morph15FontSourceId, SourceWhite);
            SetSourceIntegerCenteredText(resistanceHeading, "Resistance", 383f, SourceWhite);

            Rect[] derivedStats =
            {
                new(49f, 308f, 126f, 15f), new(49f, 326f, 126f, 15f),
                new(49f, 344f, 126f, 15f), new(49f, 362f, 126f, 15f),
                new(178f, 308f, 140f, 15f), new(178f, 326f, 140f, 15f),
                new(178f, 344f, 140f, 15f), new(178f, 362f, 140f, 15f),
            };
            for (int index = 0; index < derivedStats.Length; index++)
                _editorDerivedStats.Add(AddText($"Editor Derived {index}", _editor, derivedStats[index],
                    string.Empty, SourceUiBitmapText.Flare12FontSourceId, SourceWhite));
            Rect[] resistances =
            {
                new(322f, 301f, 105f, 15f), new(322f, 317f, 105f, 15f),
                new(322f, 333f, 105f, 15f), new(322f, 349f, 105f, 15f),
                new(322f, 365f, 105f, 15f),
            };
            for (int index = 0; index < resistances.Length; index++)
                _editorResistances.Add(AddText($"Editor Resistance {index}", _editor, resistances[index],
                    string.Empty, SourceUiBitmapText.Flare12FontSourceId, SourceWhite));

            AddSourceImageTopLeft("Alignment Marker", _editor,
                RetailCharacterCreationLayout.AlignmentMarkerSourceId, new Vector2(78f, 236f));
            _aptitudeMarker = AddSourceImageTopLeft("Aptitude Marker", _editor,
                RetailCharacterCreationLayout.AptitudeMarkerSourceId, new Vector2(754f, 221f));
            SourceUiBitmapText alignmentValue = AddText("Alignment Value", _editor,
                new Rect(RetailCharacterCreationLayout.AlignmentValueCenterX, 198f, 0f, 18f),
                string.Empty, SourceUiBitmapText.Cloister18FontSourceId, SourceWhite);
            SetSourceIntegerCenteredText(alignmentValue, "0",
                RetailCharacterCreationLayout.AlignmentValueCenterX, SourceWhite);
            _magickAptitudeText = AddText("Magick Aptitude Value", _editor,
                new Rect(752f, 66f, 35f, 18f), string.Empty, 27, SourceWhite);
            _technologyAptitudeText = AddText("Technology Aptitude Value", _editor,
                new Rect(752f, 365f, 35f, 18f), string.Empty, 27, SourceWhite);

            _editorMessageIcon = AddSourceImage("Editor Message Icon", _editorNavigation,
                RetailCharacterCreationLayout.FeedbackIconSourceId, RetailCharacterCreationLayout.EditorMessageIcon);
            _editorMessageTitle = AddText("Editor Message Title", _editorNavigation,
                RetailCharacterCreationLayout.EditorMessageTitle, string.Empty, 27, SourceWhite);
            _editorMessageRequirement = AddText("Editor Message Requirement", _editorNavigation,
                RetailCharacterCreationLayout.EditorMessageTitle, string.Empty, 27,
                new Color32(244, 43, 25, 255));
            _editorMessageBody = AddRect("Editor Message Body", _editorNavigation,
                RetailCharacterCreationLayout.EditorMessageBody);
            _editorPanel = AddRect("Editor Subpanel", _editor, RetailCharacterCreationLayout.EditorSubpanel);
            RebuildEditorMessage(string.Empty);
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
            SetNamePresentation(draft.Name);
            SetCenteredText(_genderText, Friendly(draft.Gender), SourceRed);
            SetCenteredText(_raceText, Friendly(draft.Race), SourceRed);
            BackgroundDefinition background = CurrentBackground(draft);
            SetCenteredText(_backgroundText, background?.Name ?? "Unavailable", SourceRed);
            SetPortrait(_identityPortrait, draft);
            CharacterCreationInitialStats initial = _session.CharacterCreation.ProjectInitialStats(draft);
            for (int index = 0; index < InitialAttributeOrder.Length; index++)
            {
                CharacterAttribute attribute = InitialAttributeOrder[index];
                SetText(_identityStats[index], $"{Short(attribute)}: {initial.GetAttribute(attribute)}");
            }
            SetInitialDerivedStats(_identityDerivedStats, initial);
            RefreshIdentityContext(draft, background);
            if (!string.IsNullOrEmpty(_identityValidation))
                SetIdentityBottomValidation(_identityValidation);
            else
                SetIdentityBottomMessage(string.Empty, string.Empty, false);
        }

        private void RefreshEditor()
        {
            CharacterCreationSpecification draft = _controller.CreationDraft;
            if (draft == null) return;
            int signature = DraftSignature(draft) * 31 + (int)EditorPage * 7 + _skillGroup * 11
                            + (int)_selectedCollege * 13 + (int)_selectedTechnology * 17
                            + (_autoLevelSchemesOpen ? 1009 : 0) + _autoLevelSchemeOffset * 1013;
            if (signature == _editorSignature) return;
            _editorSignature = signature;
            SetPortrait(_editorPortrait, draft);
            CharacterCreationInitialStats initial = _session.CharacterCreation.ProjectInitialStats(draft);
            SetText(_editorNameText, draft.Name ?? string.Empty);
            SetText(_editorLevelText, "LEVEL: 1");
            SetText(_pointsText, $"POINTS: {_controller.CreationValidation.RemainingCharacterPoints}");
            SetText(_editorExperienceText, "XP NEXT: 2100");
            SetText(_editorRaceText, $"RACE: {Friendly(draft.Race).ToUpperInvariant()}");
            SetText(_editorGenderText, $"GENDER: {Friendly(draft.Gender).ToUpperInvariant()}");
            SetText(_editorAgeText, $"AGE: {draft.Age}");
            for (int index = 0; index < Attributes.Length; index++)
            {
                CharacterAttribute attribute = Attributes[index];
                int effective = _session.CharacterCreation.GetEffectiveAttribute(draft, attribute);
                SetSourceIntegerCenteredText(_attributeValues[attribute], $" {effective} ",
                    RetailCharacterCreationLayout.AttributeValueCenterX[index], SourceWhite);
                (SourceUiButton minus, SourceUiButton plus) = _attributeButtons[attribute];
                minus.SetInteractable(CanAdjustAttribute(draft, attribute, -1));
                plus.SetInteractable(CanAdjustAttribute(draft, attribute, 1));
            }
            SetSourceIntegerCenteredText(_hitPointValue, $" {ProjectMaximumHitPoints(draft)} ",
                RetailCharacterCreationLayout.VitalityValueCenterX, SourceWhite);
            SetSourceIntegerCenteredText(_fatigueValue, $" {ProjectMaximumFatigue(draft)} ",
                RetailCharacterCreationLayout.VitalityValueCenterX, SourceWhite);
            _hitPointButtons.Minus.SetInteractable(CanAdjustVitality(draft, true, -1));
            _hitPointButtons.Plus.SetInteractable(CanAdjustVitality(draft, true, 1));
            _fatigueButtons.Minus.SetInteractable(CanAdjustVitality(draft, false, -1));
            _fatigueButtons.Plus.SetInteractable(CanAdjustVitality(draft, false, 1));
            foreach (var pair in _pageButtons) pair.Value.SetSelected(pair.Key == EditorPage);
            SetEditorDerivedStats(initial);
            SetEditorResistances(initial);
            int aptitude = initial.MagickTechAptitude;
            SetCenteredText(_magickAptitudeText, Math.Max(0, aptitude).ToString(), SourceWhite);
            SetCenteredText(_technologyAptitudeText, Math.Max(0, -aptitude).ToString(), SourceWhite);
            if (_aptitudeMarker != null)
                ((RectTransform)_aptitudeMarker.transform).anchoredPosition =
                    new Vector2(754f, -(221f - aptitude * 128f / 100f));
            RebuildEditorMessage(_controller.Feedback);
            RebuildEditorPanel(draft);
        }

        private void OpenAutoLevelSchemes()
        {
            _autoLevelSchemesOpen = true;
            _autoLevelSchemeOffset = 0;
            _editorSignature = int.MinValue;
            Synchronize();
        }

        private void RebuildEditorPanel(CharacterCreationSpecification draft)
        {
            ClearChildren(_editorPanel);
            _skillGauges.Clear();
            if (_autoLevelSchemesOpen)
            {
                BuildAutoLevelSchemesPanel(draft);
                return;
            }
            switch (EditorPage)
            {
                case RetailCreationEditorPage.Skills: BuildSkillsPanel(draft); break;
                case RetailCreationEditorPage.Technology: BuildTechnologyPanel(draft); break;
                case RetailCreationEditorPage.Magic: BuildMagicPanel(draft); break;
            }
        }

        private void BuildAutoLevelSchemesPanel(CharacterCreationSpecification draft)
        {
            AddSourceImage("Auto-Level Schemes Window", _editorPanel, 567, new Rect(0f, 0f, 235f, 325f));
            IReadOnlyList<AutoLevelSchemeDefinition> schemes = _session.CharacterCreation.Catalog.AutoLevelSchemes;
            AutoLevelSchemeDefinition selected = schemes.FirstOrDefault(value => value.Id == draft.AutoLevelScheme);
            SourceUiBitmapText heading = AddText("Selected Auto-Level Scheme", _editorPanel,
                new Rect(23f, 18f, 177f, 18f), selected?.Name ?? string.Empty, 229, SourceBlue);
            SetCenteredText(heading, heading.Text);
            int maximumOffset = Math.Max(0, schemes.Count - 15);
            _autoLevelSchemeOffset = Mathf.Clamp(_autoLevelSchemeOffset, 0, maximumOffset);
            for (int row = 0; row < 15 && row + _autoLevelSchemeOffset < schemes.Count; row++)
            {
                AutoLevelSchemeDefinition scheme = schemes[row + _autoLevelSchemeOffset];
                int captured = scheme.Id;
                Rect rect = new(23f, 66f + row * 16f, 177f, 15f);
                SourceUiBitmapText label = AddText($"Auto-Level Scheme {scheme.Id}", _editorPanel, rect,
                    scheme.Name, 229, scheme.Id == draft.AutoLevelScheme ? SourceBlue : SourceWhite);
                Button button = AddTransparentButton($"Select Auto-Level Scheme {scheme.Id}", _editorPanel,
                    rect, () =>
                    {
                        draft.AutoLevelScheme = captured;
                        _autoLevelSchemesOpen = false;
                        _editorSignature = int.MinValue;
                        Synchronize();
                    });
                button.transform.SetAsLastSibling();
            }
            RectTransform scrollArea = AddRect("Auto-Level Scheme Scroll", _editorPanel,
                new Rect(209f, 60f, 17f, 255f));
            Image scrollHit = scrollArea.gameObject.AddComponent<Image>();
            scrollHit.color = Color.clear;
            scrollHit.raycastTarget = true;
            scrollArea.gameObject.AddComponent<RetailStartingEquipmentScrollTarget>().Bind(delta =>
            {
                int next = Mathf.Clamp(_autoLevelSchemeOffset - (int)Mathf.Sign(delta), 0, maximumOffset);
                if (next == _autoLevelSchemeOffset) return;
                _autoLevelSchemeOffset = next;
                _editorSignature = int.MinValue;
                Synchronize();
            });
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
                SourceUiButton minus = AddSourceButtonTopLeft($"Decrease {skill}", _editorPanel, 625,
                    new Vector2(18f, buttonY), () => AdjustSkill(skill, -1));
                SourceUiButton plus = AddSourceButtonTopLeft($"Increase {skill}", _editorPanel, 624,
                    new Vector2(192f, buttonY), () => AdjustSkill(skill, 1));
                minus.SetInteractable(CanAdjustSkill(draft, skill, -1));
                plus.SetInteractable(CanAdjustSkill(draft, skill, 1));
                SourceUiBitmapText name = AddText($"{skill} Name", _editorPanel,
                    new Rect(17f, 63f + 66f * row, 170f, 18f), Friendly(skill), 27, SourceWhite);
                foreach (GameObject target in new[] { minus.gameObject, plus.gameObject, name.gameObject })
                    BindSkillHint(target, skill);
                int sourceLevel = draft.GetSkillPoints(skill) * CharacterSkillRules.SkillUnitsPerPoint
                                  + (CurrentBackground(draft)?.SkillModifiers.GetValueOrDefault(skill) ?? 0);
                sourceLevel = Mathf.Clamp(sourceLevel, 0, 20);
                _skillGauges[skill] = AddSkillGauge($"{skill} Gauge", _editorPanel,
                    new Rect(59f, 87f + 66f * row, 120f, 27f), sourceLevel);
                BindSkillHint(_skillGauges[skill].gameObject, skill);
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
                BindTechnologyDisciplineHint(button.gameObject, discipline);
            }
            int rank = draft.GetTechnologyRank(_selectedTechnology);
            TechnologyDegree learnedDegree = (TechnologyDegree)rank;
            AddText("Technology Discipline Icon", _editorPanel, new Rect(17f, 69f, 18f, 19f),
                ((char)('1' + (int)_selectedTechnology)).ToString(), 300, TechnologyWhite);
            SourceUiBitmapText technologyName = AddText("Technology Name", _editorPanel,
                new Rect(35f, 69f, 166f, 19f),
                $"{Friendly(_selectedTechnology)} {Friendly(learnedDegree)}", 301, TechnologyWhite);
            SetSourceIntegerCenteredText(technologyName, technologyName.Text, 118f, TechnologyWhite);
            AddText("Technology Expertise", _editorPanel, new Rect(204f, 72f, 31f, 15f),
                _session.CharacterCreation.GetEffectiveTechnologyLevel(draft, _selectedTechnology).ToString(),
                229, TechnologyBlue);
            IReadOnlyList<SchematicDefinition> schematics = _session.Crafting.ProjectBuiltIn(_selectedTechnology);
            for (int index = 0; index < schematics.Count; index++)
            {
                SchematicDefinition schematic = schematics[index];
                int degree = (int)schematic.BuiltInDegree.Value;
                Color tint = degree <= rank
                    ? TechnologyWhite : degree == rank + 1 ? TechnologyYellow : TechnologyGray;
                if (degree <= rank)
                    AddSourceImageTopLeft($"Degree {degree} Icon", _editorPanel,
                        TechnologyDegreeIcons[(int)_selectedTechnology], new Vector2(8f, 96f + 33f * index));
                SourceUiBitmapText name = AddText($"Schematic {degree}", _editorPanel,
                    new Rect(46f, 94f + 33f * index, 175f, 16f),
                    schematic.Name, SourceUiBitmapText.Nick16FontSourceId, tint);
                string first = _session.ResolvePrototypeDisplayName(schematic.ComponentOneAliases[0]);
                string second = _session.ResolvePrototypeDisplayName(schematic.ComponentTwoAliases[0]);
                SourceUiBitmapText components = AddText($"Schematic Components {degree}", _editorPanel,
                    new Rect(46f, 109f + 33f * index, 175f, 14f), $"[{first}]+[{second}]",
                    SourceUiBitmapText.Garmond9FontSourceId, tint);
                BindTechnologyHint(name.gameObject, _selectedTechnology, (TechnologyDegree)degree);
                BindTechnologyHint(components.gameObject, _selectedTechnology, (TechnologyDegree)degree);
            }
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
                BindSpellCollegeHint(button.gameObject, college);
            }
            int rank = draft.GetSpellRank(_selectedCollege);
            for (int level = 0; level < 5; level++)
            {
                int spellId = (int)_selectedCollege * 5 + level;
                Color tint = level < rank ? MagicWhite : level == rank ? SourceYellow : MagicGray;
                SourceUiImage icon = AddSourceImageTopLeft($"Spell {spellId} Icon", _editorPanel,
                    SpellPresentationCatalog.IconSourceId(spellId), new Vector2(18f, 82f + 48f * level));
                if (level >= rank)
                    icon.SetPaletteTransform(SourceUiPaletteTransform.TigGrayscale);
                AddText($"Spell {level + 1}", _editorPanel, new Rect(59f, 76f + 48f * level, 160f, 18f),
                    _session.SpellPresentation?.SpellName(spellId) ?? string.Empty,
                    SourceUiBitmapText.Morph15FontSourceId, tint);
                AddText($"Spell Level {level + 1}", _editorPanel,
                    new Rect(59f, 97f + 48f * level, 160f, 14f),
                    $"Minimum Level: {SpellPresentationCatalog.MinimumLevel(level + 1)}",
                    SourceUiBitmapText.Pork12FontSourceId, tint);
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
                .Where(value => CharacterCreationRules.IsPlayableRace(value)
                                && (draft.Gender != CharacterGender.Female
                                    || CharacterCreationRules.IsGenderLegal(value, CharacterGender.Female)))
                .ToList();
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
            IReadOnlyList<BackgroundDefinition> values = _session.CharacterCreation.Catalog
                .FindSelectableBackgrounds(draft.Race, draft.Gender);
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
            _autoLevelSchemesOpen = false;
            EditorPage = page;
            _editorSignature = int.MinValue;
            Synchronize();
        }

        private void AdjustAttribute(CharacterAttribute attribute, int delta)
        { _controller.AdjustCreationAttribute(attribute, delta); _editorSignature = int.MinValue; Synchronize(); }
        private void AdjustVitality(bool hitPoints, int delta)
        { _controller.AdjustCreationVitality(hitPoints, delta); _editorSignature = int.MinValue; Synchronize(); }
        private void AdjustSkill(CharacterSkill skill, int delta)
        { _controller.AdjustCreationSkill(skill, delta); _editorSignature = int.MinValue; Synchronize(); }
        private void AdjustSpell(int delta)
        { _controller.AdjustCreationSpell(_selectedCollege, delta); _editorSignature = int.MinValue; Synchronize(); }
        private void AdjustTechnology(int delta)
        { _controller.AdjustCreationTechnology(_selectedTechnology, delta); _editorSignature = int.MinValue; Synchronize(); }

        private bool CanAdjustAttribute(CharacterCreationSpecification draft, CharacterAttribute attribute, int delta)
            => ValidateProspective(draft, copy => copy.SetAttribute(attribute, copy.GetAttribute(attribute) + delta));
        private bool CanAdjustVitality(CharacterCreationSpecification draft, bool hitPoints, int delta)
            => ValidateProspective(draft, copy =>
            {
                if (hitPoints) copy.HitPointPoints += delta; else copy.FatiguePoints += delta;
            });
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
                HitPointPoints = source.HitPointPoints, FatiguePoints = source.FatiguePoints,
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

        private int ProjectMaximumHitPoints(CharacterCreationSpecification draft)
        {
            int strength = _session.CharacterCreation.GetEffectiveAttribute(draft, CharacterAttribute.Strength);
            int willpower = _session.CharacterCreation.GetEffectiveAttribute(draft, CharacterAttribute.Willpower);
            return checked(4 * draft.HitPointPoints + willpower + 2 * (strength + 1) + 4);
        }

        private int ProjectMaximumFatigue(CharacterCreationSpecification draft)
        {
            int constitution = _session.CharacterCreation.GetEffectiveAttribute(draft, CharacterAttribute.Constitution);
            int willpower = _session.CharacterCreation.GetEffectiveAttribute(draft, CharacterAttribute.Willpower);
            return checked(4 * draft.FatiguePoints + 2 * (1 + constitution) + willpower + 4);
        }

        private void BindHint(GameObject target, int sourceMessageId, Func<int> cost)
        {
            if (target.GetComponent<Graphic>() == null)
            {
                Image hitArea = target.AddComponent<Image>();
                hitArea.color = Color.clear;
                hitArea.raycastTarget = true;
            }
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowHint(sourceMessageId, cost?.Invoke() ?? 0), ClearHint);
        }

        private void BindSkillHint(GameObject target, CharacterSkill skill)
        {
            if (target.GetComponent<Graphic>() == null)
            {
                Image hitArea = target.AddComponent<Image>();
                hitArea.color = Color.clear;
                hitArea.raycastTarget = true;
            }
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowSkillHint(skill), ClearHint);
        }

        private void BindTechnologyHint(GameObject target, TechnologyDiscipline discipline,
            TechnologyDegree degree)
        {
            if (target.GetComponent<Graphic>() == null)
            {
                Image hitArea = target.AddComponent<Image>();
                hitArea.color = Color.clear;
                hitArea.raycastTarget = true;
            }
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowTechnologyHint(discipline, degree), ClearHint);
        }

        private void BindTechnologyDisciplineHint(GameObject target, TechnologyDiscipline discipline)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowTechnologyDisciplineHint(discipline), ClearHint);
        }

        private void ShowTechnologyDisciplineHint(TechnologyDiscipline discipline)
        {
            int sourceIndex = (int)discipline;
            string title = _resolver.Original.TryReadMessage("mes/tech.mes", sourceIndex,
                out string sourceName) ? sourceName.Trim() : Friendly(discipline);
            string body = _resolver.Original.TryReadMessage("mes/tech.mes", 16 + sourceIndex,
                out string sourceDescription) ? sourceDescription.Trim() : string.Empty;
            RebuildEditorMessage(title, body, string.Empty, true);
        }

        private void BindSpellCollegeHint(GameObject target, SpellCollege college)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() => ShowSpellCollegeHint(college), ClearHint);
        }

        private void ShowSpellCollegeHint(SpellCollege college)
        {
            int sourceIndex = (int)college;
            string title = _resolver.Original.TryReadMessage("mes/spell.mes", 500 + sourceIndex,
                out string sourceName) ? sourceName.Trim() : Friendly(college);
            string body = _resolver.Original.TryReadMessage("mes/spell.mes", 8000 + sourceIndex,
                out string sourceDescription) ? sourceDescription.Trim() : string.Empty;
            RebuildEditorMessage(title, body, string.Empty, true, 446 + sourceIndex);
        }

        private void ShowTechnologyHint(TechnologyDiscipline discipline, TechnologyDegree degree)
        {
            int rank = (int)degree;
            string title = _resolver.Original.TryReadMessage("mes/tech.mes", 8 + rank,
                out string sourceTitle) ? sourceTitle.Trim() : Friendly(degree);
            int descriptionId = 24 + (int)discipline * 8 + rank;
            string body = _resolver.Original.TryReadMessage("mes/tech.mes", descriptionId,
                out string sourceBody) ? sourceBody.Trim() : string.Empty;
            string requirement = $"Minimum IN: {CharacterCreationRules.TechnologyIntelligenceRequirement(rank)}";
            RebuildEditorMessage(title, body, requirement, true);
        }

        private void BindNavigationHint(GameObject target, bool next, bool identity)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() =>
            {
                string title = ReadMainMenuMessage(6999, "Action:");
                string body = ReadMainMenuMessage(next ? 7003 : 7002,
                    next ? "Next Screen" : "Previous Screen");
                if (identity) SetIdentityBottomMessage(title, body, true);
                else RebuildEditorMessage(title, body, string.Empty, true);
            }, () =>
            {
                if (identity)
                {
                    if (!string.IsNullOrEmpty(_identityValidation)) SetIdentityBottomValidation(_identityValidation);
                    else SetIdentityBottomMessage(string.Empty, string.Empty, false);
                }
                else ClearHint();
            });
        }

        private void BindIdentityHint(GameObject target, IdentityContext context)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            hint.Bind(() =>
            {
                _identityContext = context;
                CharacterCreationSpecification draft = _controller?.CreationDraft;
                if (draft != null) RefreshIdentityContext(draft, CurrentBackground(draft));
            }, () =>
            {
                _identityContext = IdentityContext.Resting;
                CharacterCreationSpecification draft = _controller?.CreationDraft;
                if (draft != null) RefreshIdentityContext(draft, CurrentBackground(draft));
            });
        }

        private void ShowHint(int sourceMessageId, int cost)
        {
            string value = _resolver.Original.TryReadMessage("mes/charedit.mes", sourceMessageId,
                out string source) ? source : string.Empty;
            RetailMessageWindowProjection message = RetailMessageWindowProjection.FromSource(value);
            RebuildEditorMessage(message.Title, message.Body, string.Empty, true);
        }

        private void ShowSkillHint(CharacterSkill skill)
        {
            int sourceIndex = (int)skill;
            string title = _resolver.Original.TryReadMessage("mes/skill.mes", sourceIndex,
                out string sourceName) ? sourceName.Trim() : Friendly(skill);
            const int firstSkillDescriptionId = CharacterSkillRules.SkillCount
                                                + (int)SkillTrainingLevel.Master + 1;
            string body = _resolver.Original.TryReadMessage("mes/skill.mes",
                firstSkillDescriptionId + sourceIndex, out string sourceDescription)
                ? sourceDescription.Trim()
                : string.Empty;
            CharacterCreationSpecification draft = _controller?.CreationDraft;
            int current = draft == null ? 0 : _session.CharacterCreation.GetEffectiveSkillRank(draft, skill);
            int required = CharacterSkillRules.MinimumAttributeForRank(
                Math.Min(CharacterSkillRules.MaximumEffectiveRank,
                    current + CharacterSkillRules.SkillUnitsPerPoint));
            string requirement = $"Minimum {Short(CharacterSkillRules.GoverningAttribute(skill))} to raise: {required}";
            RebuildEditorMessage(string.IsNullOrEmpty(title) ? Friendly(skill) : title,
                body, requirement, true);
        }

        private void ClearHint() => RebuildEditorMessage(_controller?.Feedback ?? string.Empty);

        private string ReadMainMenuMessage(int id, string fallback)
            => _resolver.Original.TryReadMessage(RetailMainMenuSource.MessageTable, id, out string source)
               && !string.IsNullOrWhiteSpace(source) ? source.Trim() : fallback;

        private void RefreshIdentityContext(CharacterCreationSpecification draft, BackgroundDefinition background)
        {
            // mainmenu_ui_new_char_create starts in the source's idle mode. Its shared refresh displays the
            // selected background only for source text rows above 1000; row 1000 falls back to Race.
            IdentityContext context = _identityContext == IdentityContext.Resting
                ? background?.TextId > 1000 ? IdentityContext.Background : IdentityContext.Race
                : _identityContext;
            switch (context)
            {
                case IdentityContext.Gender:
                    SetCenteredText(_descriptionTitle, string.Empty, SourceWhite);
                    RebuildSourceWrappedText(_identityDescription,
                        ReadMainMenuMessage(742 + (int)draft.Gender, string.Empty), 12);
                    break;
                case IdentityContext.Race:
                    SetCenteredText(_descriptionTitle, ReadMainMenuMessage(745, "Race"), SourceWhite);
                    RebuildSourceWrappedText(_identityDescription,
                        ReadMainMenuMessage(770 + 2 * (int)draft.Race + (int)draft.Gender, string.Empty),
                        12);
                    break;
                default:
                    RetailMessageWindowProjection projection = RetailMessageWindowProjection.FromSource(
                        background?.Description ?? string.Empty);
                    SetCenteredText(_descriptionTitle, background?.Name ?? projection.Title, SourceWhite);
                    RebuildSourceWrappedText(_identityDescription,
                        string.IsNullOrEmpty(projection.Body) ? projection.Title : projection.Body, 12);
                    break;
            }
        }

        private void SetIdentityBottomValidation(string value)
        {
            if (_identityMessageIcon != null) _identityMessageIcon.gameObject.SetActive(false);
            SetText(_identityMessageTitle, string.Empty);
            if (_identityMessageBody != null) ClearChildren(_identityMessageBody);
            SetCenteredText(_identityMessageText, value ?? string.Empty, SourceWhite);
        }

        private void SetIdentityBottomMessage(string title, string body, bool showIcon)
        {
            if (_identityMessageIcon != null) _identityMessageIcon.gameObject.SetActive(showIcon);
            SetText(_identityMessageText, string.Empty);
            SetText(_identityMessageTitle, title ?? string.Empty);
            if (_identityMessageBody != null) RebuildWrappedText(_identityMessageBody, body, 44, 3);
        }

        private InputField AddNameInput(RectTransform parent, Rect rect)
        {
            RectTransform root = AddRect("Name Entry", parent, rect);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = Color.black;
            RetailNameInputField input = root.gameObject.AddComponent<RetailNameInputField>();
            input.characterLimit = CharacterCreationRules.MaximumNameLength;
            input.lineType = InputField.LineType.SingleLine;

            RectTransform textRect = AddRect("Text", root, new Rect(3f, 0f, rect.width - 6f, rect.height));
            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.clear;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            input.textComponent = text;
            input.customCaretColor = true;
            input.caretColor = SourceRed;
            input.caretWidth = 1;

            _nameText = AddText("Name Bitmap Value", root, new Rect(0f, 0f, rect.width, rect.height),
                string.Empty, SourceUiBitmapText.Morph15FontSourceId, SourceRed);
            input.FocusChanged = editing =>
            {
                _nameEditing = editing;
                SetNamePresentation(input.text);
            };
            input.onValueChanged.AddListener(value =>
            {
                _controller?.SetCreationName(value);
                SetNamePresentation(value);
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

        private SourceUiImage AddSourceImageTopLeft(string name, RectTransform parent, int sourceId,
            Vector2 topLeft)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(16f, 16f);
            return AddSourceImage(name, parent, sourceId, new Rect(topLeft, size));
        }

        private Image AddSkillGauge(string name, RectTransform parent, Rect rect, int sourceLevel)
        {
            SourceUiImage source = AddSourceImage(name, parent, 623, rect);
            Image image = source.Image;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = Mathf.Clamp01(sourceLevel / 20f);
            image.enabled = sourceLevel != 0;
            return image;
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
            _textBounds[text] = rect;
            text.Bind(_resolver, value ?? string.Empty, color, fontSourceId);
            return text;
        }

        private void SetCenteredText(SourceUiBitmapText text, string value, Color? tint = null)
        {
            RectTransform rect = (RectTransform)text.transform;
            Rect bounds = _textBounds.TryGetValue(text, out Rect immutable)
                ? immutable : new Rect(rect.anchoredPosition.x, -rect.anchoredPosition.y,
                    rect.sizeDelta.x, rect.sizeDelta.y);
            text.Bind(_resolver, value ?? string.Empty, tint ?? SourceWhite, text.FontSourceId);
            Vector2 size = text.LogicalSize;
            rect.anchoredPosition = new Vector2(bounds.x + Mathf.Max(0f, (bounds.width - size.x) * .5f),
                -bounds.y);
        }

        private void SetSourceIntegerCenteredText(SourceUiBitmapText text, string value, float centerX, Color tint)
        {
            RectTransform rect = (RectTransform)text.transform;
            Rect bounds = _textBounds.TryGetValue(text, out Rect immutable)
                ? immutable : new Rect(rect.anchoredPosition.x, -rect.anchoredPosition.y,
                    rect.sizeDelta.x, rect.sizeDelta.y);
            text.Bind(_resolver, value ?? string.Empty, tint, text.FontSourceId);
            // sub_55B880 uses x = -authoredX - tig_font_measure(...).width / 2.
            // Both operands are integers, so odd widths truncate toward zero and every glyph begins on a pixel.
            int measuredWidth = text.SourceMeasuredWidth;
            rect.anchoredPosition = new Vector2(Mathf.RoundToInt(centerX) - measuredWidth / 2, -bounds.y);
        }

        private void SetNamePresentation(string value)
            => SetCenteredText(_nameText,
                string.IsNullOrEmpty(value) && !_nameEditing ? "Choose Name" : value,
                SourceRed);

        private void SetRightAlignedText(SourceUiBitmapText text, string value, Rect bounds, Color tint)
        {
            if (text == null) return;
            text.Bind(_resolver, value ?? string.Empty, tint, text.FontSourceId);
            RectTransform rect = (RectTransform)text.transform;
            rect.anchoredPosition = new Vector2(bounds.xMax - rect.sizeDelta.x, -bounds.y);
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
                    lines[index], 229, SourceWhite);
        }

        private void RebuildSourceWrappedText(RectTransform parent, string value, int maxLines)
        {
            ClearChildren(parent);
            List<string> lines = WrapSourceText(value, Mathf.RoundToInt(parent.rect.width), maxLines);
            for (int index = 0; index < lines.Count; index++)
                AddText($"Line {index + 1}", parent, new Rect(0f, index * 15f, parent.rect.width, 15f),
                    lines[index], SourceUiBitmapText.Flare12FontSourceId, SourceWhite);
        }

        private List<string> WrapSourceText(string value, int maxWidth, int maxLines)
        {
            var lines = new List<string>();
            string text = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            int offset = 0;
            while (offset < text.Length && lines.Count < maxLines)
            {
                int relativeBreak = FindSourceLineBreak(text, offset, maxWidth);
                int remaining = text.Length - offset;
                bool reachedEnd = relativeBreak >= remaining;
                int consumed = reachedEnd ? remaining : Math.Max(1, relativeBreak + 1);
                string line = text.Substring(offset, consumed).TrimEnd(' ', '\t', '\n');
                lines.Add(line);
                offset += consumed;
            }
            return lines;
        }

        private int FindSourceLineBreak(string text, int offset, int maxWidth)
        {
            int totalWidth = 0;
            int previousBreak = -1;
            int position = 0;
            int remaining = text.Length - offset;
            while (position < remaining)
            {
                char character = text[offset + position];
                if (character == '\n')
                {
                    previousBreak = position;
                    break;
                }

                if (!TryGetGlyphMetrics(character, out int glyphWidth, out int advance))
                    return Math.Max(1, position);
                if (character == ' ') previousBreak = position;
                int width = position + 1 < remaining ? advance : glyphWidth;
                if (totalWidth + width > maxWidth) break;
                totalWidth += advance;
                if (character == '-') previousBreak = position;
                position++;
            }

            if (position < remaining && previousBreak != position && previousBreak >= 0)
                position = previousBreak;
            return position;
        }

        private bool TryGetGlyphMetrics(char character, out int glyphWidth, out int advance)
        {
            int frame = (byte)character - 31;
            if (frame >= 0 && _resolver.TryResolve(
                    new UiAssetKey(SourceUiBitmapText.Flare12FontSourceId, frame: frame),
                    out UiResolvedAsset asset))
            {
                glyphWidth = asset.LogicalSize.x;
                advance = asset.Hotspot.x > 0 ? asset.Hotspot.x : glyphWidth;
                if (character == '\t') advance += 20;
                return true;
            }

            glyphWidth = 0;
            advance = 0;
            return false;
        }

        private void RebuildEditorMessage(string value)
        {
            RetailMessageWindowProjection message = RetailMessageWindowProjection.FromSource(value);
            RebuildEditorMessage(message.Title, message.Body, string.Empty, false);
        }

        private void RebuildEditorMessage(string title, string body, string requirement, bool showIcon,
            int iconSourceId = RetailCharacterCreationLayout.FeedbackIconSourceId)
        {
            if (_editorMessageIcon != null)
            {
                if (showIcon) _editorMessageIcon.SetKey(new UiAssetKey(iconSourceId));
                _editorMessageIcon.gameObject.SetActive(showIcon);
            }
            SetText(_editorMessageTitle, title ?? string.Empty);
            SetRightAlignedText(_editorMessageRequirement, requirement ?? string.Empty,
                RetailCharacterCreationLayout.EditorMessageTitle,
                new Color32(244, 43, 25, 255));
            if (_editorMessageBody != null) RebuildWrappedText(_editorMessageBody, body, 44, 3);
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
                hash = hash * 31 + value.HitPointPoints;
                hash = hash * 31 + value.FatiguePoints;
                hash = hash * 31 + value.AutoLevelScheme;
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

        private static int AttributeHintId(CharacterAttribute attribute) => attribute switch
        {
            CharacterAttribute.Strength => 109,
            CharacterAttribute.Constitution => 110,
            CharacterAttribute.Dexterity => 111,
            CharacterAttribute.Beauty => 112,
            CharacterAttribute.Intelligence => 113,
            CharacterAttribute.Willpower => 114,
            CharacterAttribute.Perception => 115,
            CharacterAttribute.Charisma => 116,
            _ => -1,
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
            _topRegion = null;
            _identity = null;
            _editor = null;
            _identityNavigation = null;
            _editorNavigation = null;
            _identityDescription = null;
            _editorPanel = null;
            _identityMessageText = null;
            _identityMessageTitle = null;
            _identityMessageBody = null;
            _identityMessageIcon = null;
            _editorMessageTitle = null;
            _editorMessageRequirement = null;
            _editorMessageBody = null;
            _editorMessageIcon = null;
            _aptitudeMarker = null;
            _identityBackground = null;
            _editorBackground = null;
            _topBackground = null;
            _identityBottomBackground = null;
            _editorBottomBackground = null;
            _nameEditing = false;
            _nameInput = null;
            _nameText = null;
            _identityPortrait = null;
            _editorPortrait = null;
            _identityStats.Clear();
            _identityDerivedStats.Clear();
            _editorDerivedStats.Clear();
            _editorResistances.Clear();
            _attributeValues.Clear();
            _attributeButtons.Clear();
            _pageButtons.Clear();
            _textBounds.Clear();
            _skillGauges.Clear();
            _controller = null;
            _session = null;
            _resolver = null;
            _wasVisible = false;
            _identitySignature = int.MinValue;
            _editorSignature = int.MinValue;
            _identityContext = IdentityContext.Resting;
            _identityValidation = string.Empty;
        }

        private void SetInitialDerivedStats(IReadOnlyList<SourceUiBitmapText> targets,
            CharacterCreationInitialStats stats)
        {
            string[] values =
            {
                $"Carry Weight: {stats.CarryWeight}", $"Damage Bonus: {stats.DamageBonus}",
                $"AC Adjustment: {stats.ArmorClassAdjustment}", $"Speed: {stats.Speed}",
                $"Heal Rate: {stats.HealRate}", $"Poison Recovery: {stats.PoisonRecovery}",
                $"Reaction Modifier: {stats.ReactionModifier}",
            };
            for (int index = 0; index < targets.Count; index++)
                SetText(targets[index], values[index]);
        }

        private void SetEditorDerivedStats(CharacterCreationInitialStats stats)
        {
            string[] values =
            {
                $"Carry Weight: {stats.CarryWeight}", $"Damage Bonus: {stats.DamageBonus}",
                $"AC Adjustment: {stats.ArmorClassAdjustment}", $"Speed: {stats.Speed}",
                $"Heal Rate: {stats.HealRate}", $"Poison Recovery: {stats.PoisonRecovery}",
                $"Reaction Modifier: {stats.ReactionModifier}", $"Max Followers: {stats.MaximumFollowers}",
            };
            for (int index = 0; index < _editorDerivedStats.Count; index++)
                SetText(_editorDerivedStats[index], values[index]);
        }

        private void SetEditorResistances(CharacterCreationInitialStats stats)
        {
            string[] values =
            {
                $"Damage: {stats.GetResistance(CharacterResistance.Normal)}",
                $"Magick: {stats.GetResistance(CharacterResistance.Magic)}",
                $"Fire: {stats.GetResistance(CharacterResistance.Fire)}",
                $"Poison: {stats.GetResistance(CharacterResistance.Poison)}",
                $"Electrical: {stats.GetResistance(CharacterResistance.Electrical)}",
            };
            for (int index = 0; index < _editorResistances.Count; index++)
                SetText(_editorResistances[index], values[index]);
        }
    }
}
