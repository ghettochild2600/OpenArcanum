using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Production Play Mode proof for the bounded UI-D.1/UI-B.1/UI-C.5 correction pass.</summary>
    [InitializeOnLoad]
    public static class UiD1PlayerFacingValidation
    {
        private const string MenuPath = "OpenArcanum/UI-D.1/Run Physical PlayMode Validation";
        private const string PendingKey = "OpenArcanum.UID1.PhysicalValidationPending";

        static UiD1PlayerFacingValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-D.1 PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
                return;
            }
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.EraseBool(PendingKey);
            EditorApplication.delayCall += StartValidation;
        }

        private static void StartValidation()
        {
            WorldObjectSectorLoader loader = Require(Object.FindFirstObjectByType<WorldObjectSectorLoader>(),
                "production sector loader");
            loader.StartCoroutine(Validate());
        }

        private static IEnumerator Validate()
        {
            ProductionGameUiPresenter presenter = Require(
                Object.FindFirstObjectByType<ProductionGameUiPresenter>(), "production UI presenter");
            WorldMapSessionCoordinator session = presenter.GetComponent<WorldMapSessionCoordinator>();
            GameUiController controller = presenter.Controller;
            RetailGameplayHudView hud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "retail gameplay HUD");

            session.ResetAuthoritativeSession();
            controller.Refresh();
            yield return null;

            Check(controller.BeginNewGame(), "New Game opens production Character Creation");
            yield return null;
            RetailCharacterCreationView creation = Require(
                Object.FindFirstObjectByType<RetailCharacterCreationView>(), "retail Character Creation view");
            creation.Synchronize();
            Check(((RectTransform)creation.IdentityPortraitImage.transform).sizeDelta == new Vector2(128f, 128f),
                "portrait_draw_128x128 uses a 128x128 draw surface");

            creation.NameInput.text = "UI-D.1 Proof";
            RectTransform gender = (RectTransform)creation.IdentityRoot.Find("Gender");
            RectTransform race = (RectTransform)creation.IdentityRoot.Find("Race");
            RectTransform background = (RectTransform)creation.IdentityRoot.Find("Background");
            Vector2 genderCenter = Center(gender);
            for (int index = 0; index < 100; index++)
            {
                FindButton(creation, "Next Gender").onClick.Invoke();
                FindButton(creation, "Next Race").onClick.Invoke();
                FindButton(creation, "Next Background").onClick.Invoke();
            }
            creation.Synchronize();
            Check(CenteredWithin(gender, RetailCharacterCreationLayout.Gender)
                  && CenteredWithin(race, RetailCharacterCreationLayout.Race)
                  && CenteredWithin(background, RetailCharacterCreationLayout.Background),
                "100 selector cycles redraw inside immutable source rectangles without accumulated drift");
            Check(CharacterCreationRules.IsGenderLegal(CharacterRace.Human, CharacterGender.Female)
                  && CharacterCreationRules.IsGenderLegal(CharacterRace.Elf, CharacterGender.Female)
                  && CharacterCreationRules.IsGenderLegal(CharacterRace.HalfElf, CharacterGender.Female)
                  && CharacterCreationRules.IsGenderLegal(CharacterRace.HalfOrc, CharacterGender.Female)
                  && !CharacterCreationRules.IsGenderLegal(CharacterRace.Dwarf, CharacterGender.Female)
                  && !CharacterCreationRules.IsGenderLegal(CharacterRace.Gnome, CharacterGender.Female)
                  && !CharacterCreationRules.IsGenderLegal(CharacterRace.Halfling, CharacterGender.Female)
                  && !CharacterCreationRules.IsGenderLegal(CharacterRace.HalfOgre, CharacterGender.Female),
                "all eight source race/gender restrictions remain authoritative");

            controller.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            creation.Synchronize();
            FindButton(creation, "Previous Gender").onClick.Invoke();
            creation.Synchronize();
            Check(controller.CreationDraft.Gender == CharacterGender.Female
                  && session.CharacterCreation.Catalog.IsPortraitLegal(controller.CreationDraft.PortraitId,
                      CharacterRace.Human, CharacterGender.Female),
                "Human Female selects and resets to the first legal portrait");
            controller.SetCreationIdentity(CharacterRace.Gnome, CharacterGender.Male);
            creation.Synchronize();
            FindButton(creation, "Previous Gender").onClick.Invoke();
            Check(controller.CreationDraft.Gender == CharacterGender.Male,
                "Gnome rejects Female through the production selector");
            controller.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            creation.Synchronize();
            Check(Center(gender).y == genderCenter.y,
                "identity redraw preserves the source text row after identity changes");

            FindButton(creation, "Identity Continue").onClick.Invoke();
            creation.Synchronize();
            SourceUiBitmapText strengthText = creation.GetComponentsInChildren<SourceUiBitmapText>(true)
                .First(value => value.name == "Strength Value" && value.gameObject.activeInHierarchy);
            Check(SourceIntegerCenteredHorizontally(strengthText,
                    RetailCharacterCreationLayout.AttributeValueCenterX[0]),
                "one-digit attribute value uses the source integer-measured center");
            FindButton(creation, "Increase Strength").onClick.Invoke();
            FindButton(creation, "Increase Strength").onClick.Invoke();
            creation.Synchronize();
            Check(SourceIntegerCenteredHorizontally(strengthText,
                    RetailCharacterCreationLayout.AttributeValueCenterX[0]),
                "two-digit attribute value uses the same source integer-measured center");
            FindButton(creation, "Decrease Strength").onClick.Invoke();
            FindButton(creation, "Decrease Strength").onClick.Invoke();
            Image bowGauge = creation.SkillGauges[CharacterSkill.Bow];
            Check(!bowGauge.enabled, "zero Bow restores the underlying source gauge region");
            FindButton(creation, "Increase Bow").onClick.Invoke();
            creation.Synchronize();
            bowGauge = creation.SkillGauges[CharacterSkill.Bow];
            Check(bowGauge.enabled && Mathf.Abs(bowGauge.fillAmount - .2f) < .001f
                  && ((RectTransform)bowGauge.transform).sizeDelta == new Vector2(120f, 27f),
                "Bow level 4/20 draws one fifth of source gauge ID 623 at 120x27");
            FindButton(creation, "Decrease Bow").onClick.Invoke();
            creation.Synchronize();
            bowGauge = creation.SkillGauges[CharacterSkill.Bow];
            Check(!bowGauge.enabled, "refunding Bow clears the gauge immediately");

            session.ResetAuthoritativeSession();
            controller.Refresh();
            Check(controller.BeginPregeneratedCharacter(), "Pick Character opens the source-backed selector");
            yield return null;
            RetailPregeneratedCharacterView pregen = Require(
                Object.FindFirstObjectByType<RetailPregeneratedCharacterView>(), "pregenerated-character view");
            pregen.Synchronize();
            Check(pregen.IsVisible && pregen.BackgroundAsset?.Key.SourceId == 766
                  && ((RectTransform)pregen.PortraitImage.transform).sizeDelta == new Vector2(128f, 128f),
                "Pick Character uses source window 766 and portrait_draw_128x128 geometry");
            IReadOnlyList<PregeneratedCharacterDefinition> pregens = session.CharacterCreation.Catalog
                .PregeneratedCharacters;
            Check(pregens.Count == 12, "the retail corpus exposes exactly twelve pregenerated PCs");
            int[][] expectedRetailStats =
            {
                new[] { 5, 8, 10, 8, 8, 8, 8, 8, 2500, -2, 0, 10, 3, 8, -7 },
                new[] { 8, 6, 8, 8, 8, 10, 8, 8, 4000, -1, -2, 8, 2, 6, -7 },
                new[] { 8, 7, 9, 9, 8, 8, 8, 8, 4000, -1, -1, 9, 2, 7, -3 },
                new[] { 12, 8, 8, 7, 4, 8, 8, 8, 6000, 2, -2, 8, 3, 8, -12 },
                new[] { 7, 6, 9, 9, 8, 9, 8, 8, 3500, -1, -1, 9, 2, 6, -3 },
                new[] { 8, 8, 8, 8, 8, 8, 8, 8, 4000, -1, -2, 8, 3, 8, -7 },
                new[] { 9, 9, 8, 6, 8, 8, 8, 6, 4500, 0, -2, 8, 3, 9, -18 },
                new[] { 9, 9, 7, 8, 8, 8, 8, 7, 4500, 0, -3, 7, 3, 9, -7 },
                new[] { 7, 9, 8, 8, 8, 8, 8, 8, 3500, -1, -2, 8, 3, 9, -7 },
                new[] { 6, 7, 9, 9, 8, 9, 8, 8, 3000, -2, -1, 9, 2, 7, -3 },
                new[] { 8, 10, 8, 6, 8, 8, 8, 6, 4000, -1, -2, 8, 3, 10, -18 },
                new[] { 7, 8, 9, 9, 8, 8, 8, 8, 3500, -1, -1, 9, 3, 8, -3 },
            };
            CharacterAttribute[] initialOrder =
            {
                CharacterAttribute.Strength, CharacterAttribute.Constitution, CharacterAttribute.Dexterity,
                CharacterAttribute.Beauty, CharacterAttribute.Intelligence, CharacterAttribute.Willpower,
                CharacterAttribute.Perception, CharacterAttribute.Charisma,
            };
            string[] initialAbbreviations = { "ST", "CN", "DX", "BE", "IN", "WP", "PE", "CH" };
            var visited = new HashSet<int>();
            for (int index = 0; index < pregens.Count; index++)
            {
                PregeneratedCharacterDefinition selected = pregens.Single(value =>
                    value.SourceIndex == pregen.SelectedSourceIndex);
                Check(selected.PrototypeNumber == CharacterCreationRules.PlayerPrototypeNumber + selected.SourceIndex
                      && !string.IsNullOrWhiteSpace(selected.Name),
                    $"pregenerated source {selected.SourceIndex} retains its retail prototype and name mapping");
                int[] expected = expectedRetailStats[selected.SourceIndex - pregens[0].SourceIndex];
                CharacterCreationInitialStats initial = session.CharacterCreation
                    .ProjectPregeneratedInitialStats(selected);
                SourceUiBitmapText[] displayed = pregen.GetComponentsInChildren<SourceUiBitmapText>(true);
                SourceUiBitmapText[] displayedNames = displayed
                    .Where(value => value.name == "Name")
                    .ToArray();
                Check(displayedNames.Any(value => value.Text == selected.Name),
                    $"pregenerated source {selected.SourceIndex} displays retail name {selected.Name}");
                Check(pregen.PortraitImage.sprite != null,
                    $"pregenerated source {selected.SourceIndex} displays its retail portrait");
                for (int attributeIndex = 0; attributeIndex < initialOrder.Length; attributeIndex++)
                {
                    Check(initial.GetAttribute(initialOrder[attributeIndex]) == expected[attributeIndex]
                          && displayed.Single(value => value.name == $"Stat {attributeIndex}").Text
                          == $"{initialAbbreviations[attributeIndex]}:{expected[attributeIndex]}",
                        $"pregenerated source {selected.SourceIndex} displays retail initial stat {initialAbbreviations[attributeIndex]}");
                }
                string[] expectedDerived =
                {
                    $"Carry Weight:{expected[8]}", $"Damage Bonus:{expected[9]}",
                    $"AC Adjustment:{expected[10]}", $"Speed:{expected[11]}",
                    $"Heal Rate:{expected[12]}", $"Poison Recovery:{expected[13]}",
                    $"Reaction Modifier:{expected[14]}",
                };
                for (int derivedIndex = 0; derivedIndex < expectedDerived.Length; derivedIndex++)
                {
                    Check(displayed.Single(value => value.name == $"Derived Stat {derivedIndex}").Text
                          == expectedDerived[derivedIndex],
                        $"pregenerated source {selected.SourceIndex} displays retail derived stat {derivedIndex}");
                }
                visited.Add(selected.SourceIndex);
                FindButton(pregen, "Next Portrait").onClick.Invoke();
                pregen.Synchronize();
                yield return null;
            }
            Check(visited.Count == pregens.Count && pregen.SelectedSourceIndex == pregens[0].SourceIndex,
                "Next cycles every pregenerated PC exactly once and wraps deterministically");
            FindButton(pregen, "Previous Portrait").onClick.Invoke();
            pregen.Synchronize();
            Check(pregen.SelectedSourceIndex == pregens[^1].SourceIndex,
                "Previous wraps from the first pregenerated PC to the twelfth source entry");
            FindButton(pregen, "Next Portrait").onClick.Invoke();
            pregen.Synchronize();
            PregeneratedCharacterDefinition firstStarted = pregens.Single(value =>
                value.SourceIndex == pregen.SelectedSourceIndex);
            FindButton(pregen, "Start").onClick.Invoke();
            yield return null;
            ArcanumObjectId pc = ProductionPlayerLifecycle.DefaultPlayerIdentity;
            bool ownsStartingInventory = session.ChildrenOf(pc).Any() || session.EquippedItems(pc).Any();
            Check(session.PlayerState?.Identity == pc,
                "first pregenerated Start creates the one authoritative production PC; feedback="
                + controller.Feedback);
            Check(session.Characters.Get(pc).PrototypeNumber == firstStarted.PrototypeNumber,
                "first pregenerated Start retains the selected retail PC prototype");
            Check(session.SelectedSector?.Contains("arcanum1-024-fixed") == true,
                "first pregenerated Start enters authentic START_MAP 1");
            Check(ownsStartingInventory,
                "first pregenerated Start creates the authentic owned or equipped inventory");
            string json = session.SaveGames.SerializeCurrentSession();
            Check(json.Contains("\"pregeneratedSourceIndex\"") && session.SaveGames.LoadJson(json).Succeeded
                  && session.CharacterCreation.Finalized.PregeneratedSourceIndex == firstStarted.SourceIndex
                  && session.Characters.Get(pc).PrototypeNumber == firstStarted.PrototypeNumber,
                "pregenerated prototype identity, inventory, campaign state, and selection metadata round-trip in V1");

            hud.Synchronize(true);
            Check(hud.IsVisible, "pregenerated campaign start reveals the production gameplay HUD");
            Button skills = FindButton(hud, "Skills");
            skills.onClick.Invoke();
            hud.Synchronize(true);
            Check(controller.HudPanel == GameUiHudPanel.Skills && controller.Screen == GameUiScreen.None,
                "Skills opens the source rotating window rather than a modal screen");
            SourceUiButton[] actions = hud.GetComponentsInChildren<SourceUiButton>(true)
                .Where(value => value.name.StartsWith("Source Skill Action", StringComparison.Ordinal))
                .OrderBy(value => value.name).ToArray();
            Check(actions.Length == 4, "the rotating window exposes the four audited source skill families");
            int objectsBefore = session.States.Count;
            foreach (SourceUiButton action in actions)
            {
                action.OnPointerEnter(null);
                action.OnPointerDown(null);
                Check(action.CurrentPresentationKey.Frame == 1,
                    action.name + " uses the second and final source frame for pressed presentation");
                action.OnPointerUp(null);
                action.OnPointerExit(null);
                action.Button.onClick.Invoke();
            }
            Check(session.States.Count == objectsBefore && controller.Feedback.Contains("not available"),
                "unimplemented skill authorities fail closed without world mutation or fabricated success");
            skills.onClick.Invoke();
            FindButton(hud, "Spells").onClick.Invoke();
            Check(controller.Screen == GameUiScreen.Magic, "Spells routes to the authoritative Magic screen");
            controller.Close();
            FindButton(hud, "Schematics").onClick.Invoke();
            Check(controller.Screen == GameUiScreen.Crafting,
                "Schematics routes to the authoritative Crafting screen");
            controller.Close();
            FindButton(hud, "Combat").onClick.Invoke();
            Check(session.Combat.IsCombatModeActive && controller.Feedback.Contains("Combat ready"),
                "Combat reaches the current combat-readiness authority rather than a dead control");

            session.ResetAuthoritativeSession();
            controller.Refresh();
            Check(controller.BeginPregeneratedCharacter(), "second Pick Character run opens after reset");
            controller.CyclePregeneratedCharacter(1);
            int secondIndex = controller.PregeneratedCharacterIndex;
            Check(controller.FinalizePregeneratedCharacter(), "a second distinct pregenerated PC starts");
            Check(secondIndex != firstStarted.SourceIndex
                  && session.CharacterCreation.Finalized.PregeneratedSourceIndex == secondIndex
                  && session.Characters.Get(pc).PrototypeNumber
                  == CharacterCreationRules.PlayerPrototypeNumber + secondIndex,
                "second start uses a distinct authentic prototype without duplicate player authority");

            foreach ((int width, int height) in new[]
                     {
                         (800, 600), (1024, 768), (1920, 1080), (2560, 1440), (3840, 2160),
                     })
            {
                UiLogicalMapping mapping = UiLogicalMapping.ForResolution(width, height);
                Rect creationWindow = mapping.LogicalToScreen(RetailCharacterCreationLayout.Window);
                Rect bottom = mapping.LogicalToScreen(RetailGameplayHudLayout.BottomInterface);
                Check(creationWindow.xMin >= 0f && creationWindow.xMax <= width + .001f
                      && bottom.xMin >= 0f && bottom.xMax <= width + .001f
                      && bottom.yMin >= 0f && bottom.yMax <= height + .001f,
                    $"{width}x{height} preserves the source Character Creation and HUD compositions");
            }

            Check(Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                "the full correction pass retains one presenter and one EventSystem");
            Debug.Log("UI-D.1 PHYSICAL VALIDATION: PASS; portrait=128x128; selectors=100 cycles/no drift; "
                      + "gender=Human/Elf/Half-Elf/Half-Orc female and Dwarf/Gnome/Halfling/Half-Ogre male-only; "
                      + "attribute=one/two digit centered; gauge=ID623 120x27 sourceLevel/20 and refund; "
                      + "pregen=ID766/12 entries/exact retail initial+derived stats/all prototype-backed/first+second start/V1 round-trip; "
                      + "HUD=Skills rotating window/Spells/Schematics/Combat authority; "
                      + "skill icons=IDs279/280/282/278 frames0..1 only; unsupported gameplay authorities=fail closed/no mutation; "
                      + "resolutions=800x600/1024x768/1080p/1440p/4K; presenters=1; EventSystems=1. "
                      + "Play Mode remains open on the second pregenerated campaign for visual inspection.");
        }

        private static Button FindButton(Component root, string name)
            => root.GetComponentsInChildren<Button>(true)
                .Last(value => value.name == name && value.gameObject.activeInHierarchy);

        private static Vector2 Center(RectTransform value)
            => value.anchoredPosition + new Vector2(value.sizeDelta.x * .5f, -value.sizeDelta.y * .5f);

        private static bool CenteredWithin(RectTransform value, Rect bounds)
            => Mathf.Abs(Center(value).x - bounds.center.x) <= .501f;

        private static bool SourceIntegerCenteredHorizontally(SourceUiBitmapText value, float centerX)
        {
            RectTransform rect = (RectTransform)value.transform;
            return rect.anchoredPosition.x == Mathf.Round(rect.anchoredPosition.x)
                   && Mathf.Approximately(rect.anchoredPosition.x,
                       Mathf.RoundToInt(centerX) - value.SourceMeasuredWidth / 2);
        }

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }
    }
}
