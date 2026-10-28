using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Creation;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Focused native 800x600 proof for source HUD contextual-help behavior.</summary>
    [InitializeOnLoad]
    public static class UiCHudContextualHelpValidation
    {
        private const string MenuPath = "OpenArcanum/UI-C/Run HUD Contextual Help Physical Validation";
        private const string PendingKey = "OpenArcanum.UIC.HudContextualHelpPhysicalValidationPending";

        static UiCHudContextualHelpValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-C HUD CONTEXTUAL HELP PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            loader.StartCoroutine(Validate(loader));
        }

        private static IEnumerator Validate(WorldObjectSectorLoader loader)
        {
            ProductionGameUiPresenter presenter = Require(
                Object.FindFirstObjectByType<ProductionGameUiPresenter>(), "production UI presenter");
            WorldMapSessionCoordinator session = presenter.GetComponent<WorldMapSessionCoordinator>();
            GameUiController controller = presenter.Controller;
            session.ResetAuthoritativeSession();
            controller.Refresh();
            yield return null;

            Check(controller.BeginNewGame(), "fresh authentic New Game opens character creation");
            controller.SetCreationName("HUD Help Proof");
            controller.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
            controller.SetCreationPortrait(1005);
            controller.SetCreationBackground(3);
            controller.AdjustCreationSpell(SpellCollege.Earth, 1);
            Check(controller.CreationValidation.Succeeded, "bounded source-valid character is ready");
            Check(controller.FinalizeNewGame(), "fresh New Game enters its authoritative campaign transition");
            Check(controller.CompleteStartingEquipment(), "source Shopkeeper completion reaches START_MAP 1");
            yield return null;
            yield return null;

            presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                "production UI presenter after New Game");
            controller = presenter.Controller;
            session = presenter.GetComponent<WorldMapSessionCoordinator>();
            RetailGameplayHudView view = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "retail gameplay HUD");
            controller.CancelDialogue();
            controller.Refresh();
            view.Synchronize(true);

            Check(Screen.width == 800 && Screen.height == 600, "native 800x600 Game view");
            Check(controller.Screen == GameUiScreen.None && controller.HudPanel == GameUiHudPanel.None,
                "gameplay HUD owns the pointer routes");

            var feedbackControls = new Dictionary<string, int>
            {
                { "Character", 1005 },
                { "Logbook", 1004 },
                { "Map", 1007 },
                { "Inventory", 1006 },
                { "Fate", 1008 },
                { "Sleep", 1009 },
                { "Combat", 1002 },
                { "Skills", 1001 },
                { "Spells", 1000 },
                { "Schematics", 1003 },
            };

            GameUiScreen initialScreen = controller.Screen;
            GameUiHudPanel initialPanel = controller.HudPanel;
            bool initialCombat = controller.ProjectHud().CombatActive;
            long initialTime = session.SourceTime.ElapsedMilliseconds;
            foreach (KeyValuePair<string, int> entry in feedbackControls)
            {
                SourceHoverHintTarget hint = FindHint(view, entry.Key);
                hint.OnPointerEnter(null);
                view.Synchronize(true);
                Check(controller.ContextualHelpKey == $"mes/intgame.mes:{entry.Value}",
                    entry.Key + " uses its source intgame.mes key");
                Check(view.Resolver.Original.TryReadMessage("mes/intgame.mes", entry.Value, out string source)
                      && controller.ContextualHelp == source,
                    entry.Key + " projects exact source tooltip text");
                Check(view.IsContextualHelpVisible, entry.Key + " presents contextual help");
                Check(controller.Screen == initialScreen && controller.HudPanel == initialPanel
                      && controller.ProjectHud().CombatActive == initialCombat
                      && session.SourceTime.ElapsedMilliseconds == initialTime,
                    entry.Key + " hover does not mutate gameplay state");
                hint.OnPointerExit(null);
                view.Synchronize(true);
                Check(!view.IsContextualHelpVisible, entry.Key + " pointer exit clears contextual help");
            }

            foreach ((string name, int titleKey, int bodyKey) in new[]
                     {
                         ("Recent Action 2", 6, 26),
                         ("Recent Action 1", 5, 25),
                     })
            {
                SourceHoverHintTarget hint = FindHint(view, name);
                hint.OnPointerEnter(null);
                view.Synchronize(true);
                Check(view.Resolver.Original.TryReadMessage("mes/skill.mes", titleKey, out string title)
                      && view.Resolver.Original.TryReadMessage("mes/skill.mes", bodyKey, out string body)
                      && controller.ContextualHelpTitle == title && controller.ContextualHelp == body,
                    name + " projects exact source skill title and description");
                hint.OnPointerExit(null);
                view.Synchronize(true);
            }

            SourceHoverHintTarget character = FindHint(view, "Character");
            SourceHoverHintTarget logbook = FindHint(view, "Logbook");
            character.OnPointerEnter(null);
            logbook.OnPointerEnter(null);
            character.OnPointerExit(null);
            view.Synchronize(true);
            Check(controller.ContextualHelpKey == "mes/intgame.mes:1004",
                "direct movement replaces help and stale pointer exit cannot clear the new owner");
            logbook.OnPointerExit(null);
            view.Synchronize(true);

            SourceHoverHintTarget clock = FindHint(view, "Clock Clip");
            clock.OnPointerEnter(null);
            view.Synchronize(true);
            string clockSnapshot = controller.ContextualHelp;
            session.SourceTime.Advance(37_000);
            view.Synchronize(true);
            Check(controller.ContextualHelp == clockSnapshot,
                "clock tooltip retains the pointer-enter snapshot while the campaign clock advances");
            clock.OnPointerExit(null);
            clock.OnPointerEnter(null);
            view.Synchronize(true);
            Check(controller.ContextualHelp != clockSnapshot,
                "clock tooltip takes a new authoritative snapshot on re-enter");
            clock.OnPointerExit(null);
            view.Synchronize(true);

            SourceUiImage contextualIcon = view.GetComponentsInChildren<SourceUiImage>(true)
                .Single(value => value.name == "World Target Icon");
            SourceUiBitmapText titleText = view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Contextual Help Title");
            SourceUiBitmapText bodyText = view.GetComponentsInChildren<SourceUiBitmapText>(true)
                .Single(value => value.name == "Contextual Help");
            Check(contextualIcon.Key.SourceId == RetailGameplayHudLayout.ContextualHelpIconSourceId,
                "contextual help uses source icon ART 444");
            Check(titleText.FontSourceId == RetailGameplayHudLayout.ContextualHelpTitleFontSourceId
                  && bodyText.FontSourceId == RetailGameplayHudLayout.ContextualHelpFontSourceId,
                "skill title/body use source Morph15/Flare12 fonts");
            Check(RetailGameplayHudLayout.ContextualHelpText == new Rect(290f, 504f, 291f, 78f)
                  && RetailGameplayHudLayout.ContextualHelpTitle == new Rect(290f, 504f, 291f, 19f)
                  && RetailGameplayHudLayout.ContextualHelpBody == new Rect(290f, 528f, 291f, 54f),
                "source tooltip rectangles remain exact at 800x600");

            Debug.Log("UI-C HUD CONTEXTUAL HELP PHYSICAL VALIDATION: PASS; resolution=800x600; "
                      + "controls=Character/Log Book/Maps/Inventory/Sleep/Combat Toggle/Skills/Spells/"
                      + "Schematics/Fate Points/Prowling/Pick Pocket; ownership=replace+stale-exit-safe; "
                      + "clock=enter-snapshot+hover-frozen+re-enter-refresh; "
                      + "icon=ART444; fonts=Morph15+Flare12; gameplay-state-mutation=none. "
                      + "Play Mode remains open for native pointer proof.");
        }

        private static SourceHoverHintTarget FindHint(RetailGameplayHudView view, string name)
            => view.GetComponentsInChildren<SourceHoverHintTarget>(true)
                .First(value => value.gameObject.name == name);

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }
    }
}
