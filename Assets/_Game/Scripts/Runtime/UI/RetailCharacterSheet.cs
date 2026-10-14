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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Arcanum.Runtime.UI
{
    /// <summary>Source coordinates from the active <c>charedit_open</c> path.</summary>
    public static class RetailCharacterSheetLayout
    {
        public const int BackgroundSourceId = 22;
        public const int SkillsPanelSourceId = 29;
        public const int TechnologyPanelSourceId = 30;
        public const int MagicPanelSourceId = 31;
        public const int MinusSourceId = 23;
        public const int PlusSourceId = 24;
        public static readonly Rect Window = new(0f, 41f, 800f, 400f);
        public static readonly Rect Portrait = new(12f, 10f, 89f, 89f);
        public static readonly Rect SkillsPanel = new(503f, 63f, 235f, 325f);
        public static readonly Rect MagicPanel = SkillsPanel;
        public static readonly Rect TechnologyPanel = SkillsPanel;

        public static readonly Vector2[] TechnologyPositions =
        {
            new(8f, 7f), new(34f, 29f), new(61f, 7f), new(88f, 28f),
            new(116f, 7f), new(142f, 29f), new(171f, 7f), new(198f, 29f),
        };
        public static readonly int[] TechnologyDegreeIcons = { 653, 649, 650, 651, 652, 654, 655, 656 };

        public static readonly int[] SpellCollegeIcons =
            { 11, 44, 38, 92, 50, 86, 56, 98, 62, 68, 74, 104, 110, 80, 116, 122 };
        public static readonly Vector2[] SpellCollegePositions =
        {
            new(13f, 15f), new(38f, 15f), new(63f, 15f), new(88f, 15f),
            new(113f, 15f), new(138f, 15f), new(162f, 15f), new(187f, 15f),
            new(25f, 40f), new(50f, 40f), new(75f, 40f), new(100f, 40f),
            new(124f, 40f), new(149f, 40f), new(174f, 40f), new(199f, 40f),
        };

        public static readonly CharacterAttribute[] Attributes =
        {
            CharacterAttribute.Strength, CharacterAttribute.Dexterity, CharacterAttribute.Constitution,
            CharacterAttribute.Beauty, CharacterAttribute.Intelligence, CharacterAttribute.Perception,
            CharacterAttribute.Willpower, CharacterAttribute.Charisma,
        };

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
            { 22, 23, 24, 29, 30, 31, 34, 35, 36, 568, 302, 303, 304, 305,
                306, 307, 308, 309, 310, 311, 312, 313, 624, 625, 647, 648 };

        public static Rect ScreenRect(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            return new Rect((width - Window.width) * .5f, 41f + (height - 600f) * .5f,
                Window.width, Window.height);
        }
    }

    /// <summary>
    /// Source-backed active Character editor. The view retains only its selected skill group; all character values,
    /// accepted baselines and transactions belong to the controller and M4 services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetailCharacterSheetView : MonoBehaviour
    {
        private static readonly Color SourceWhite = new Color32(231, 217, 174, 255);
        private static readonly Color SourceYellow = new Color32(245, 201, 65, 255);
        private static readonly Color SourceBlue = new Color32(108, 155, 215, 255);
        private static readonly Color SourceGray = new Color32(135, 135, 135, 255);

        private GameUiController _controller;
        private WorldMapSessionCoordinator _session;
        private UiSkinResolver _resolver;
        private GameObject _canvasRoot;
        private RectTransform _window;
        private RectTransform _skillsPanel;
        private Image _portrait;
        private SourceUiImage _background;
        private readonly Dictionary<CharacterAttribute, SourceUiBitmapText> _attributeValues = new();
        private readonly Dictionary<CharacterAttribute, (SourceUiButton Minus, SourceUiButton Plus)> _attributeButtons = new();
        private readonly Dictionary<SourceUiBitmapText, Rect> _textBounds = new();
        private readonly Dictionary<CharacterSkill, Image> _skillGauges = new();
        private SourceUiBitmapText _points;
        private SourceUiBitmapText _level;
        private SourceUiBitmapText _experience;
        private SourceUiBitmapText _hitPointValue;
        private SourceUiBitmapText _fatigueValue;
        private SourceUiBitmapText _name;
        private SourceUiBitmapText _race;
        private SourceUiBitmapText _gender;
        private SourceUiBitmapText _age;
        private SourceUiBitmapText _feedback;
        private SourceUiButton _skillsButton;
        private SourceUiButton _technologyButton;
        private SourceUiButton _magicButton;
        private bool _dirty = true;
        private int _signature = int.MinValue;
        private int _skillGroup;
        private SpellCollege _selectedCollege;
        private TechnologyDiscipline _selectedTechnology;

        public bool IsAvailable => _canvasRoot != null && _resolver != null;
        public bool IsVisible => IsAvailable && _canvasRoot.activeSelf;
        public int SkillGroup => _skillGroup;
        public SpellCollege SelectedCollege => _selectedCollege;
        public TechnologyDiscipline SelectedTechnology => _selectedTechnology;
        public RectTransform WindowTransform => _window;
        public RectTransform SkillsPanelTransform => _skillsPanel;
        public IReadOnlyDictionary<CharacterSkill, Image> SkillGauges => _skillGauges;
        public UiResolvedAsset BackgroundAsset => _background?.CurrentAsset;
        public GameUiCharacterView LastProjection { get; private set; }

        public void Bind(GameUiController controller, WorldMapSessionCoordinator session, UiSkinResolver resolver)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (_controller == controller && ReferenceEquals(_resolver, resolver) && IsAvailable) return;
            int preservedGroup = _skillGroup;
            Cleanup();
            _controller = controller;
            _session = session;
            _resolver = resolver;
            _skillGroup = preservedGroup;
            _resolver.PresentationInvalidated += Invalidate;
            BuildPresentation();
            Synchronize();
        }

        public void Synchronize()
        {
            if (!IsAvailable || _controller == null) return;
            bool visible = _controller.HasPlayer
                           && _controller.Screen is GameUiScreen.Character or GameUiScreen.Magic
                               or GameUiScreen.Technology;
            if (_canvasRoot.activeSelf != visible) _canvasRoot.SetActive(visible);
            if (!visible) return;
            GameUiCharacterView projection = _controller.ProjectCharacter();
            if (projection == null) return;
            int signature = HashCode.Combine(ProjectionSignature(projection), _controller.Screen,
                _selectedCollege, _controller.Screen == GameUiScreen.Magic
                    ? _session.Magic.GetKnownCollegeRank(_controller.Player, _selectedCollege)
                    : _controller.Screen == GameUiScreen.Technology
                        ? (int)_session.Technology.GetLearnedDegree(_controller.Player, _selectedTechnology) : 0);
            if (_dirty || signature != _signature)
            {
                LastProjection = projection;
                _signature = signature;
                _dirty = false;
                RefreshCharacter(projection);
                if (_controller.Screen == GameUiScreen.Magic) RebuildMagic();
                else if (_controller.Screen == GameUiScreen.Technology) RebuildTechnology();
                else RebuildSkills(projection);
            }
            _skillsButton?.SetSelected(_controller.Screen == GameUiScreen.Character);
            _technologyButton?.SetSelected(_controller.Screen == GameUiScreen.Technology);
            _magicButton?.SetSelected(_controller.Screen == GameUiScreen.Magic);
            SetText(_feedback, _controller.Feedback);
        }

        public bool ContainsScreenPoint(Vector2 screenBottomLeft)
        {
            if (!IsVisible) return false;
            Vector2 topLeft = new(screenBottomLeft.x, Screen.height - screenBottomLeft.y);
            return RetailCharacterSheetLayout.ScreenRect(Screen.width, Screen.height).Contains(topLeft);
        }

        public void SelectSkillGroup(int group)
        {
            _skillGroup = Mathf.Clamp(group, 0, 3);
            _dirty = true;
            Synchronize();
        }

        public void SelectCollege(SpellCollege college)
        {
            _selectedCollege = college;
            _dirty = true;
            Synchronize();
        }

        public void SelectTechnology(TechnologyDiscipline discipline)
        {
            _selectedTechnology = discipline;
            _dirty = true;
            Synchronize();
        }

        private void BuildPresentation()
        {
            _canvasRoot = new GameObject("Source-Faithful Character Sheet",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(SourceUiRuntime));
            _canvasRoot.transform.SetParent(transform, false);
            _canvasRoot.GetComponent<SourceUiRuntime>().BindResolver(_resolver);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            CanvasScaler scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 1f;

            _window = AddRect("Active Character Editor", (RectTransform)_canvasRoot.transform,
                new Rect(0f, 0f, 800f, 400f));
            _window.anchorMin = _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = new Vector2(0f, 59f);
            _background = AddSourceImage("Character Editor Base", _window, 22,
                new Rect(0f, 0f, 800f, 400f));
            RectTransform blocker = AddRect("Character Editor Input", _window, new Rect(0f, 0f, 800f, 400f));
            Image blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            _portrait = AddPortrait("Portrait", _window, RetailCharacterSheetLayout.Portrait);
            // charedit_ui.c stores these basic-info rectangles in desktop coordinates;
            // sub_55B880 subtracts the big-window origin (y=41) before drawing.
            _points = AddText("Character Points", _window, new Rect(212f, 53f, 120f, 19f), 27, SourceWhite);
            _level = AddText("Level", _window, new Rect(212f, 34f, 120f, 19f), 27, SourceWhite);
            _experience = AddText("Experience", _window, new Rect(212f, 72f, 150f, 19f), 27, SourceWhite);
            _name = AddText("Name", _window, new Rect(212f, 15f, 270f, 19f), 27, SourceYellow);
            _race = AddText("Race", _window, new Rect(337f, 34f, 150f, 18f), 27, SourceWhite);
            _gender = AddText("Gender", _window, new Rect(337f, 53f, 150f, 18f), 27, SourceWhite);
            _age = AddText("Age", _window, new Rect(337f, 72f, 120f, 19f), 27, SourceWhite);

            for (int index = 0; index < RetailCharacterSheetLayout.Attributes.Length; index++)
            {
                CharacterAttribute attribute = RetailCharacterSheetLayout.Attributes[index];
                SourceUiButton minus = AddSourceButtonTopLeft($"Decrease {attribute}", _window, 23,
                    RetailCharacterSheetLayout.AttributeMinus[index], () => AdjustAttribute(attribute, -1));
                SourceUiButton plus = AddSourceButtonTopLeft($"Increase {attribute}", _window, 24,
                    RetailCharacterSheetLayout.AttributePlus[index], () => AdjustAttribute(attribute, 1));
                _attributeButtons.Add(attribute, (minus, plus));
                float x = index < 4 ? 194f : 337f;
                float y = RetailCharacterSheetLayout.AttributePlus[index].y + 1f;
                _attributeValues.Add(attribute, AddText($"{attribute} Value", _window,
                    new Rect(x, y, 36f, 18f), 27, SourceWhite));
                BindHint(minus.gameObject, AttributeHintId(attribute), () => 1);
                BindHint(plus.gameObject, AttributeHintId(attribute), () => 1);
                BindHint(_attributeValues[attribute].gameObject, AttributeHintId(attribute), () => 1);
            }

            SourceUiButton hpMinus = AddSourceButtonTopLeft("Decrease Hit Points", _window, 23,
                new Vector2(409f, 144f), () => AdjustVitality(true, -1));
            SourceUiButton hpPlus = AddSourceButtonTopLeft("Increase Hit Points", _window, 24,
                new Vector2(465f, 144f), () => AdjustVitality(true, 1));
            SourceUiButton fatigueMinus = AddSourceButtonTopLeft("Decrease Fatigue", _window, 23,
                new Vector2(409f, 214f), () => AdjustVitality(false, -1));
            SourceUiButton fatiguePlus = AddSourceButtonTopLeft("Increase Fatigue", _window, 24,
                new Vector2(465f, 214f), () => AdjustVitality(false, 1));
            _hitPointValue = AddText("Hit Point Value", _window, new Rect(429f, 143f, 36f, 18f), 27, SourceWhite);
            _fatigueValue = AddText("Fatigue Value", _window, new Rect(429f, 213f, 36f, 18f), 27, SourceWhite);
            foreach (GameObject target in new[] { hpMinus.gameObject, hpPlus.gameObject, _hitPointValue.gameObject })
                BindHint(target, 106, () => 1);
            foreach (GameObject target in new[] { fatigueMinus.gameObject, fatiguePlus.gameObject, _fatigueValue.gameObject })
                BindHint(target, 107, () => 1);

            _skillsButton = AddSourceButtonTopLeft("Skills", _window, 34, new Vector2(527f, 11f),
                () => _controller.Open(GameUiScreen.Character));
            _technologyButton = AddSourceButtonTopLeft("Technology", _window, 35, new Vector2(595f, 11f),
                () => _controller.Open(GameUiScreen.Technology));
            _magicButton = AddSourceButtonTopLeft("Magic", _window, 36, new Vector2(663f, 11f),
                () => _controller.Open(GameUiScreen.Magic));
            _skillsButton.SetSelected(true);
            BindHint(_skillsButton.gameObject, 132, () => 0);
            BindHint(_technologyButton.gameObject, 133, () => 0);
            BindHint(_magicButton.gameObject, 134, () => 0);
            AddSourceButtonTopLeft("Auto-Level Schemes", _window, 568, new Vector2(754f, 21f),
                () => _controller.ShowFeedback("Auto-level schemes are not available in the current runtime."));

            BuildDerivedLabels();
            _skillsPanel = AddRect("Skills Subpanel", _window, RetailCharacterSheetLayout.SkillsPanel);
        }

        private void BuildDerivedLabels()
        {
            string[] names = { "Carry", "Damage", "Armor", "Speed", "Heal", "Poison Recovery", "Reaction", "Followers" };
            CharacterDerivedStat[] stats =
            {
                CharacterDerivedStat.CarryWeight, CharacterDerivedStat.MeleeDamageBonus,
                CharacterDerivedStat.ArmorClassAdjustment, CharacterDerivedStat.Speed,
                CharacterDerivedStat.HealRate, CharacterDerivedStat.PoisonRecovery,
                CharacterDerivedStat.BeautyReactionModifier, CharacterDerivedStat.MaximumFollowers,
            };
            for (int index = 0; index < stats.Length; index++)
            {
                float x = index < 4 ? 49f : 178f;
                float y = 308f + 18f * (index % 4);
                float width = index < 4 ? 126f : 140f;
                AddText($"Derived {stats[index]}", _window, new Rect(x, y, width, 15f), 26, SourceWhite)
                    .gameObject.name = names[index];
            }
            CharacterResistance[] resistances =
            {
                CharacterResistance.Normal, CharacterResistance.Magic, CharacterResistance.Fire,
                CharacterResistance.Poison, CharacterResistance.Electrical,
            };
            for (int index = 0; index < resistances.Length; index++)
                AddText($"Resistance {resistances[index]}", _window,
                    new Rect(322f, 301f + 16f * index, 105f, 15f), 26, SourceWhite);
        }

        private void RefreshCharacter(GameUiCharacterView view)
        {
            SetPortrait(view.PortraitId);
            SetText(_name, view.Name);
            SetText(_points, $"POINTS: {view.CharacterPoints}");
            SetText(_level, $"LEVEL: {view.Level}");
            SetText(_experience, $"XP NEXT: {view.ExperienceToNextLevel}");
            SetText(_race, $"RACE: {Friendly(view.Race).ToUpperInvariant()}");
            SetText(_gender, $"GENDER: {Friendly(view.Gender).ToUpperInvariant()}");
            SetText(_age, view.Age > 0 ? $"AGE: {view.Age}" : string.Empty);
            foreach (GameUiAttributeView attribute in view.Attributes)
                SetCenteredText(_attributeValues[attribute.Attribute], attribute.Base == attribute.Effective
                    ? attribute.Base.ToString() : $"{attribute.Base}/{attribute.Effective}");
            SetCenteredText(_hitPointValue, view.MaximumHitPoints.ToString());
            SetCenteredText(_fatigueValue, view.MaximumFatigue.ToString());

            foreach (GameUiDerivedStatView stat in view.DerivedStats)
            {
                Transform target = _window.Find(stat.Stat switch
                {
                    CharacterDerivedStat.CarryWeight => "Carry",
                    CharacterDerivedStat.MeleeDamageBonus => "Damage",
                    CharacterDerivedStat.ArmorClassAdjustment => "Armor",
                    CharacterDerivedStat.Speed => "Speed",
                    CharacterDerivedStat.HealRate => "Heal",
                    CharacterDerivedStat.PoisonRecovery => "Poison Recovery",
                    CharacterDerivedStat.BeautyReactionModifier => "Reaction",
                    CharacterDerivedStat.MaximumFollowers => "Followers",
                    _ => string.Empty,
                });
                if (target != null) SetText(target.GetComponent<SourceUiBitmapText>(), $"{Friendly(stat.Stat)}: {stat.Value}");
            }
            foreach (GameUiResistanceView resistance in view.Resistances)
            {
                Transform target = _window.Find($"Resistance {resistance.Resistance}");
                if (target != null) SetText(target.GetComponent<SourceUiBitmapText>(),
                    $"{Friendly(resistance.Resistance)}: {resistance.Value}%");
            }
        }

        private void RebuildSkills(GameUiCharacterView view)
        {
            ClearChildren(_skillsPanel);
            _skillGauges.Clear();
            AddSourceImage("Skills Window", _skillsPanel, 29, new Rect(0f, 0f, 235f, 325f));
            for (int group = 0; group < 4; group++)
            {
                int captured = group;
                SourceUiButton button = AddSourceButtonTopLeft($"Skill Group {group}", _skillsPanel,
                    302 + group, new Vector2(new[] { 21f, 75f, 129f, 185f }[group], 17f),
                    () => SelectSkillGroup(captured));
                button.SetSelected(group == _skillGroup);
            }
            for (int row = 0; row < 4; row++)
            {
                GameUiSkillView skill = view.Skills[_skillGroup * 4 + row];
                float y = 84f + 66f * row;
                AddSourceButtonTopLeft($"Decrease {skill.Skill}", _skillsPanel, 625, new Vector2(18f, y),
                    () => AdjustSkill(skill.Skill, -1));
                AddSourceButtonTopLeft($"Increase {skill.Skill}", _skillsPanel, 624, new Vector2(192f, y),
                    () => AdjustSkill(skill.Skill, 1));
                string training = skill.Training == SkillTrainingLevel.None
                    ? string.Empty : $" ({Friendly(skill.Training)})";
                AddText($"{skill.Skill} Name", _skillsPanel, new Rect(17f, 63f + 66f * row, 175f, 18f),
                    27, SourceWhite).Bind(_resolver, Friendly(skill.Skill) + training, SourceWhite, 27);
                _skillGauges[skill.Skill] = AddSkillGauge($"{skill.Skill} Gauge", _skillsPanel,
                    new Rect(59f, 87f + 66f * row, 120f, 27f), skill.EffectiveRank);
            }
        }

        private void RebuildMagic()
        {
            ClearChildren(_skillsPanel);
            _skillGauges.Clear();
            AddSourceImage("Magic Window", _skillsPanel, RetailCharacterSheetLayout.MagicPanelSourceId,
                new Rect(0f, 0f, 235f, 325f));
            for (int index = 0; index < RetailCharacterSheetLayout.SpellCollegePositions.Length; index++)
            {
                SpellCollege college = (SpellCollege)index;
                SourceUiButton button = AddSourceButtonTopLeft(college.ToString(), _skillsPanel,
                    RetailCharacterSheetLayout.SpellCollegeIcons[index],
                    RetailCharacterSheetLayout.SpellCollegePositions[index], () => SelectCollege(college));
                button.SetSelected(college == _selectedCollege);
                BindMagicCollegeHint(button.gameObject, college);
            }

            int rank = _session.Magic.GetKnownCollegeRank(_controller.Player, _selectedCollege);
            string collegeName = _session.SpellPresentation?.CollegeName(_selectedCollege) ?? string.Empty;
            SourceUiBitmapText collegeTitle = AddText("College Name", _skillsPanel,
                new Rect(18f, 69f, 205f, 19f), 27, SourceBlue);
            SetCenteredText(collegeTitle, collegeName, SourceBlue);
            for (int level = 0; level < 5; level++)
            {
                int spellId = (int)_selectedCollege * 5 + level;
                bool learned = level < rank;
                Color tint = learned ? SourceWhite : level == rank ? SourceYellow : SourceGray;
                SourceUiImage icon = AddSourceImage($"Spell {spellId} Icon", _skillsPanel,
                    SpellPresentationCatalog.IconSourceId(spellId), new Rect(18f, 82f + 48f * level, 32f, 32f));
                if (!learned) icon.Image.color = SourceGray;
                AddText($"Spell {level + 1}", _skillsPanel,
                    new Rect(59f, 76f + 48f * level, 160f, 18f), 27, tint)
                    .Bind(_resolver, _session.SpellPresentation?.SpellName(spellId) ?? string.Empty, tint, 27);
                AddText($"Spell Level {level + 1}", _skillsPanel,
                    new Rect(59f, 97f + 48f * level, 160f, 14f), 28, tint)
                    .Bind(_resolver, $"Minimum Level: {SpellPresentationCatalog.MinimumLevel(level + 1)}", tint, 28);
                BindMagicSpellHint(AddHintArea($"Spell {spellId} Hover", _skillsPanel,
                    new Rect(18f, 73f + 48f * level, 199f, 39f)), spellId);
            }

            if (rank < 5)
                AddSourceButtonTopLeft("Increase Spell", _skillsPanel, 647,
                        new Vector2(18f, 82f + 48f * rank), () => AdjustSpell(1))
                    .SetInteractable(CanAdjustSpell(1));
            if (rank > 0)
                AddSourceButtonTopLeft("Decrease Spell", _skillsPanel, 648,
                        new Vector2(18f, 82f + 48f * (rank - 1)), () => AdjustSpell(-1))
                    .SetInteractable(CanAdjustSpell(-1));
        }

        private void RebuildTechnology()
        {
            ClearChildren(_skillsPanel);
            _skillGauges.Clear();
            AddSourceImage("Technology Window", _skillsPanel,
                RetailCharacterSheetLayout.TechnologyPanelSourceId, new Rect(0f, 0f, 235f, 325f));
            for (int index = 0; index < RetailCharacterSheetLayout.TechnologyPositions.Length; index++)
            {
                TechnologyDiscipline discipline = (TechnologyDiscipline)index;
                SourceUiButton button = AddSourceButtonTopLeft(discipline.ToString(), _skillsPanel,
                    306 + index, RetailCharacterSheetLayout.TechnologyPositions[index],
                    () => SelectTechnology(discipline));
                button.SetSelected(discipline == _selectedTechnology);
                BindTechnologyDisciplineHint(button.gameObject, discipline);
            }

            TechnologyDegree learned = _session.Technology.GetLearnedDegree(_controller.Player, _selectedTechnology);
            int rank = (int)learned;
            AddText("Technology Discipline Icon", _skillsPanel, new Rect(17f, 69f, 18f, 19f),
                    300, SourceWhite)
                .Bind(_resolver, ((char)('1' + (int)_selectedTechnology)).ToString(), SourceWhite, 300);
            SourceUiBitmapText technologyName = AddText("Technology Name", _skillsPanel,
                new Rect(35f, 69f, 159f, 19f), 301, SourceWhite);
            SetCenteredText(technologyName, $"{Friendly(_selectedTechnology)} {Friendly(learned)}", SourceWhite);
            AddText("Technology Expertise", _skillsPanel, new Rect(194f, 72f, 31f, 15f), 229, SourceBlue)
                .Bind(_resolver, _session.Technology.GetEffectiveLevel(_controller.Player, _selectedTechnology)
                    .ToString(), SourceBlue, 229);

            IReadOnlyList<SchematicDefinition> schematics = _session.Crafting.ProjectBuiltIn(_selectedTechnology);
            for (int index = 0; index < schematics.Count; index++)
            {
                SchematicDefinition schematic = schematics[index];
                int degree = (int)schematic.BuiltInDegree.Value;
                Color tint = degree <= rank ? SourceWhite : degree == rank + 1 ? SourceYellow : SourceGray;
                if (degree <= rank)
                    AddSourceImageTopLeft($"Degree {degree} Icon", _skillsPanel,
                        RetailCharacterSheetLayout.TechnologyDegreeIcons[(int)_selectedTechnology],
                        new Vector2(8f, 96f + 33f * index));
                SourceUiBitmapText name = AddText($"Schematic {degree}", _skillsPanel,
                    new Rect(46f, 94f + 33f * index, 175f, 16f), 301, tint);
                name.Bind(_resolver, schematic.Name ?? string.Empty, tint, 301);
                string first = _session.ResolvePrototypeDisplayName(schematic.ComponentOneAliases[0]);
                string second = _session.ResolvePrototypeDisplayName(schematic.ComponentTwoAliases[0]);
                SourceUiBitmapText components = AddText($"Schematic Components {degree}", _skillsPanel,
                    new Rect(46f, 109f + 33f * index, 175f, 14f), 483, tint);
                components.Bind(_resolver, $"[{first}]+[{second}]", tint, 483);
                BindTechnologyDegreeHint(name.gameObject, _selectedTechnology, (TechnologyDegree)degree, schematic);
                BindTechnologyDegreeHint(components.gameObject, _selectedTechnology,
                    (TechnologyDegree)degree, schematic);
            }

            if (rank < 7)
                AddSourceButtonTopLeft("Increase Technology", _skillsPanel, 647,
                        new Vector2(8f, 96f + rank * 33f), () => AdjustTechnology(1))
                    .SetInteractable(_session.Technology.PreviewLearnNextDegree(
                        _controller.Player, _selectedTechnology).Succeeded);
            if (rank > 0)
                AddSourceButtonTopLeft("Decrease Technology", _skillsPanel, 648,
                    new Vector2(8f, 96f + (rank - 1) * 33f), () => AdjustTechnology(-1));
        }

        private void AdjustTechnology(int delta)
        {
            _controller.AdjustTechnologyDiscipline(_selectedTechnology, delta);
            _dirty = true;
            Synchronize();
        }

        private void BindTechnologyDisciplineHint(GameObject target, TechnologyDiscipline discipline)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            string owner = "technology-discipline:" + (int)discipline;
            hint.Bind(() =>
            {
                string message = ReadMessage("mes/charedit.mes", 4000 + (int)discipline,
                    Friendly(discipline));
                RetailMessageWindowProjection projection = RetailMessageWindowProjection.FromSource(message);
                _controller.ShowContextualHelp(owner, projection.Body, projection.Title);
            }, () => _controller.ClearContextualHelp(owner));
        }

        private void BindTechnologyDegreeHint(GameObject target, TechnologyDiscipline discipline,
            TechnologyDegree degree, SchematicDefinition schematic)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            string owner = $"technology-degree:{(int)discipline}:{(int)degree}";
            hint.Bind(() =>
            {
                int rank = (int)degree;
                string title = ReadMessage("mes/tech.mes", 8 + rank, Friendly(degree)).Trim();
                string body = ReadMessage("mes/tech.mes", 24 + (int)discipline * 8 + rank,
                    schematic.Description ?? string.Empty).Trim();
                string requirement = $"Minimum IN: {TechnologyIntelligenceRequirement(rank)}";
                _controller.ShowContextualHelp(owner,
                    string.IsNullOrWhiteSpace(body) ? requirement : requirement + "\n" + body, title);
            }, () => _controller.ClearContextualHelp(owner));
        }

        private string ReadMessage(string path, int key, string fallback)
            => _resolver.Original.TryReadMessage(path, key, out string value) ? value : fallback;

        private static int TechnologyIntelligenceRequirement(int rank)
            => new[] { 0, 5, 8, 11, 13, 15, 17, 19 }[Mathf.Clamp(rank, 0, 7)];

        private void AdjustSpell(int delta)
        {
            _controller.AdjustSpellCollege(_selectedCollege, delta);
            _dirty = true;
            Synchronize();
        }

        private bool CanAdjustSpell(int delta)
        {
            int rank = _session.Magic.GetKnownCollegeRank(_controller.Player, _selectedCollege);
            if (delta < 0) return rank > 0;
            if (rank >= 5 || _session.Progression.GetUnspentCharacterPoints(_controller.Player) < 1) return false;
            int[] willpowerRequirements = { 6, 9, 12, 15, 18 };
            int[] levelRequirements = { 1, 1, 5, 10, 15 };
            return _session.Characters.GetEffectiveAttribute(_controller.Player, CharacterAttribute.Intelligence) >= 5
                   && _session.Characters.GetEffectiveAttribute(_controller.Player, CharacterAttribute.Willpower)
                   >= willpowerRequirements[rank]
                   && _session.Progression.GetLevel(_controller.Player) >= levelRequirements[rank];
        }

        private GameObject AddHintArea(string name, RectTransform parent, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            return target.gameObject;
        }

        private void BindMagicCollegeHint(GameObject target, SpellCollege college)
        {
            SourceHoverHintTarget hint = target.GetComponent<SourceHoverHintTarget>()
                                         ?? target.AddComponent<SourceHoverHintTarget>();
            string owner = "magic-college:" + (int)college;
            hint.Bind(() => _controller.ShowContextualHelp(owner,
                    _session.SpellPresentation?.CollegeDescription(college) ?? string.Empty,
                    _session.SpellPresentation?.CollegeName(college) ?? string.Empty, 446 + (int)college),
                () => _controller.ClearContextualHelp(owner));
        }

        private void BindMagicSpellHint(GameObject target, int spellId)
        {
            SourceHoverHintTarget hint = target.AddComponent<SourceHoverHintTarget>();
            string owner = "magic-spell:" + spellId;
            int rank = spellId % 5;
            string requirements = $"Willpower: {new[] { 6, 9, 12, 15, 18 }[rank]}";
            if (PhaseOneSpellCatalog.TryGet(spellId, out SpellDefinition runtime))
                requirements += $"    Fatigue: {runtime.BaseFatigueCost}";
            string description = _session.SpellPresentation?.SpellDescription(spellId) ?? string.Empty;
            string body = string.IsNullOrWhiteSpace(description)
                ? requirements : requirements + "\n" + description;
            hint.Bind(() => _controller.ShowContextualHelp(owner, body,
                    _session.SpellPresentation?.SpellName(spellId) ?? string.Empty, 446 + spellId / 5),
                () => _controller.ClearContextualHelp(owner));
        }

        private void AdjustAttribute(CharacterAttribute attribute, int delta)
        {
            _controller.AdjustAttribute(attribute, delta);
            _dirty = true;
            Synchronize();
        }

        private void AdjustSkill(CharacterSkill skill, int delta)
        {
            if (delta > 0) _controller.IncreaseSkill(skill); else _controller.DecreaseSkill(skill);
            _dirty = true;
            Synchronize();
        }

        private void AdjustVitality(bool hitPoints, int delta)
        {
            _controller.AdjustVitalityPoint(hitPoints, delta);
            _dirty = true;
            Synchronize();
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

        private void ShowHint(int sourceMessageId, int cost)
        {
            string value = _resolver.Original.TryReadMessage("mes/charedit.mes", sourceMessageId,
                out string source) ? source : string.Empty;
            if (cost > 0 && sourceMessageId is >= 106 and <= 116)
                value = string.IsNullOrEmpty(value) ? $"CP cost: {cost}" : $"{value}  CP cost: {cost}";
            SetText(_feedback, value);
        }

        private void ClearHint() => SetText(_feedback, _controller?.Feedback ?? string.Empty);

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

        private void SetPortrait(int portraitId)
        {
            _portrait.sprite = null;
            CharacterCreationCatalog catalog = _session.CharacterCreation.Catalog;
            if (catalog == null || !catalog.Portraits.TryGetValue(portraitId, out PortraitDefinition portrait)) return;
            if (_resolver.Original.TryResolvePortrait(portrait.SourceName, out RetailPortraitAsset asset))
                _portrait.sprite = asset.Sprite;
        }

        private void Invalidate()
        {
            _dirty = true;
            if (this != null && IsVisible) Synchronize();
        }

        private static int ProjectionSignature(GameUiCharacterView view)
        {
            unchecked
            {
                int hash = HashCode.Combine(view.Name, view.Level, view.Experience, view.CharacterPoints,
                    view.Alignment, view.Aptitude, view.HitPoints, view.Fatigue);
                foreach (GameUiAttributeView value in view.Attributes) hash = hash * 31 + HashCode.Combine(value.Base, value.Effective);
                foreach (GameUiSkillView value in view.Skills) hash = hash * 31 + HashCode.Combine(value.PurchasedPoints, value.EffectiveRank, value.Training);
                return hash;
            }
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
            image.fillAmount = Mathf.Clamp01(Mathf.Clamp(sourceLevel, 0, 20) / 20f);
            image.enabled = sourceLevel != 0;
            return image;
        }

        private SourceUiButton AddSourceButtonTopLeft(string name, RectTransform parent, int sourceId,
            Vector2 topLeft, Action clicked)
        {
            Vector2 size = _resolver.TryResolve(new UiAssetKey(sourceId), out UiResolvedAsset asset)
                ? asset.LogicalSize : new Vector2(32f, 32f);
            RectTransform target = AddRect(name, parent, new Rect(topLeft.x, topLeft.y, size.x, size.y));
            target.gameObject.AddComponent<Image>();
            target.gameObject.AddComponent<Button>();
            target.gameObject.AddComponent<SourceUiImage>();
            SourceUiButton source = target.gameObject.AddComponent<SourceUiButton>();
            source.Bind(_resolver, new UiAssetKey(sourceId, frame: 0), new UiAssetKey(sourceId, frame: 1),
                new UiAssetKey(sourceId, frame: 2), new UiAssetKey(sourceId, frame: 0),
                new UiAssetKey(sourceId, frame: 2));
            source.Button.onClick.AddListener(() => clicked());
            return source;
        }

        private Image AddPortrait(string name, RectTransform parent, Rect rect)
        {
            RectTransform target = AddRect(name, parent, rect);
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private SourceUiBitmapText AddText(string name, RectTransform parent, Rect rect, int font, Color color)
        {
            RectTransform target = AddRect(name, parent, rect);
            SourceUiBitmapText text = target.gameObject.AddComponent<SourceUiBitmapText>();
            _textBounds[text] = rect;
            text.Bind(_resolver, string.Empty, color, font);
            return text;
        }

        private void SetCenteredText(SourceUiBitmapText text, string value, Color? tint = null)
        {
            if (text == null) return;
            RectTransform rect = (RectTransform)text.transform;
            Rect bounds = _textBounds.TryGetValue(text, out Rect immutable)
                ? immutable : new Rect(rect.anchoredPosition.x, -rect.anchoredPosition.y,
                    rect.sizeDelta.x, rect.sizeDelta.y);
            text.Bind(_resolver, value ?? string.Empty, tint ?? SourceWhite, text.FontSourceId);
            rect.anchoredPosition = new Vector2(
                bounds.x + Mathf.Max(0f, (bounds.width - text.LogicalSize.x) * .5f), -bounds.y);
        }

        private void SetText(SourceUiBitmapText text, string value)
        {
            if (text == null || text.Text == (value ?? string.Empty)) return;
            text.Bind(_resolver, value ?? string.Empty, SourceWhite, text.FontSourceId);
        }

        private static RectTransform AddRect(string name, RectTransform parent, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
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
                Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }

        private static string Friendly(object value)
        {
            string text = value?.ToString() ?? string.Empty;
            return string.Concat(text.Select((character, index) => index > 0 && char.IsUpper(character)
                ? " " + character : character.ToString()));
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (_resolver != null) _resolver.PresentationInvalidated -= Invalidate;
            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                if (Application.isPlaying) Object.Destroy(_canvasRoot); else Object.DestroyImmediate(_canvasRoot);
            }
            _canvasRoot = null;
            _window = null;
            _skillsPanel = null;
            _portrait = null;
            _background = null;
            _skillsButton = null;
            _technologyButton = null;
            _magicButton = null;
            _attributeValues.Clear();
            _attributeButtons.Clear();
            _textBounds.Clear();
            _skillGauges.Clear();
            LastProjection = null;
            _signature = int.MinValue;
            _dirty = true;
        }
    }
}
