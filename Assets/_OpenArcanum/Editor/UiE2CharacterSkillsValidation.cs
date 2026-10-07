using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.Rendering;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Representative production Play Mode proof for UI-E.2 Character/Skills.</summary>
    [InitializeOnLoad]
    public static class UiE2CharacterSkillsValidation
    {
        private const string MenuPath = "OpenArcanum/UI-E.2/Run Physical PlayMode Validation";
        private const string PendingKey = "OpenArcanum.UIE2.PhysicalValidationPending";

        static UiE2CharacterSkillsValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-E.2 PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            ProductionGameUiPresenter presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                "production UI presenter");
            presenter.StartCoroutine(Validate(presenter));
        }

        private static IEnumerator Validate(ProductionGameUiPresenter presenter)
        {
            yield return null;
            yield return null;
            WorldMapSessionCoordinator session = presenter.GetComponent<WorldMapSessionCoordinator>();
            GameUiController controller = presenter.Controller;
            session.ResetAuthoritativeSession();
            controller.Refresh();
            yield return null;
            Check(controller.BeginNewGame(), "fresh production New Game opens Character Creation");
            controller.SetCreationName("UI-E.2 Human");
            Check(controller.FinalizeNewGame(), "source-valid creation finalizes through Player Startup");
            ProductionPlayerLifecycle lifecycle = Require(
                Object.FindFirstObjectByType<ProductionPlayerLifecycle>(), "production player lifecycle");
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            yield return null;
            Check(controller.HasPlayer, "production Player Startup has authoritative PC state");
            ArcanumObjectId pc = controller.Player;
            RetailCharacterSheetView character = Require(Object.FindFirstObjectByType<RetailCharacterSheetView>(),
                "retail Character view");
            RetailInventoryEquipmentView inventory = Require(
                Object.FindFirstObjectByType<RetailInventoryEquipmentView>(), "retail Inventory view");
            RetailGameplayHudView hud = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "retail gameplay HUD");

            int initialPoints = session.Progression.GetUnspentCharacterPoints(pc);
            int initialStrength = session.Characters.GetBaseAttribute(pc, CharacterAttribute.Strength);
            Check(controller.Open(GameUiScreen.Character), "Character HUD route opens active editor");
            yield return null;
            character.Synchronize();
            hud.Synchronize(true);
            Check(character.IsVisible && hud.IsVisible && character.BackgroundAsset?.Key.SourceId == 22,
                "C/Character composes source ID 22 between the retained HUD bars");
            Check(character.SkillsPanelTransform.GetComponentInChildren<SourceUiImage>(true)?.Key.SourceId == 29,
                "active editor mounts source Skills child window ID 29");
            Check(character.LastProjection.Attributes.Count == 8 && character.LastProjection.Skills.Count == 16
                  && character.LastProjection.DerivedStats.Count == 9
                  && character.LastProjection.Resistances.Count == 5,
                "production projection contains attributes, skills, derived stats, and resistances");

            FindButton(character, "Increase Strength").onClick.Invoke();
            yield return null;
            Check(session.Characters.GetBaseAttribute(pc, CharacterAttribute.Strength) == initialStrength + 1
                  && session.Progression.GetUnspentCharacterPoints(pc) == initialPoints - 1,
                "source plus commits one M4 primary-stat point atomically");
            FindButton(character, "Decrease Strength").onClick.Invoke();
            yield return null;
            Check(session.Characters.GetBaseAttribute(pc, CharacterAttribute.Strength) == initialStrength
                  && session.Progression.GetUnspentCharacterPoints(pc) == initialPoints,
                "source minus refunds only the same-session primary-stat point");

            FindButton(character, "Increase Melee").onClick.Invoke();
            yield return null;
            Check(session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.Melee) == 1
                  && session.Progression.GetUnspentCharacterPoints(pc) == initialPoints - 1,
                "source Skills plus routes through M4C point and prerequisite authority");
            Check(session.Progression.SetTrainingLevel(pc, CharacterSkill.Melee, SkillTrainingLevel.Apprentice)
                  == TrainingAssignmentResult.Success, "existing M4C training authority remains authoritative");
            character.Synchronize();
            Check(character.LastProjection.Skills.Single(value => value.Skill == CharacterSkill.Melee).Training
                  == SkillTrainingLevel.Apprentice, "Skills panel projects M4C training state");

            GraphicsMode originalMode = OpenArcanumGraphicsSettings.Mode;
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
            hud.Resolver.InvalidatePresentation();
            yield return null;
            character.Synchronize();
            OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
            hud.Resolver.InvalidatePresentation();
            yield return null;
            character.Synchronize();
            Check(character.IsVisible
                  && session.Progression.GetPurchasedSkillPoints(pc, CharacterSkill.Melee) == 1,
                "Original to Enhanced to Original rebuild preserves character authority and presentation ownership");
            OpenArcanumGraphicsSettings.SetRuntimeMode(originalMode);

            controller.Close();
            var keyboardContext = new ProductionKeyboardContext(GameUiScreen.None, true, false,
                session.Combat.IsActive, session.Combat.Mode);
            Check(presenter.Keyboard.Dispatch(KeyCode.K, ProductionKeyPhase.Down, keyboardContext,
                    (IProductionKeyboardCommands)presenter), "K dispatches the Skills source route");
            yield return null;
            hud.Synchronize(true);
            Check(controller.Screen == GameUiScreen.None && controller.HudPanel == GameUiHudPanel.Skills,
                "K opens the Skills HUD rotating window without modal authority");
            Check(hud.GetComponentsInChildren<Button>(true).Count(value =>
                    value.name.StartsWith("Source Skill Action", StringComparison.Ordinal)) == 4,
                "source Skills rotating window exposes exactly four action controls");
            int beforeUnsupported = session.Progression.GetUnspentCharacterPoints(pc);
            FindButton(hud, "Source Skill Action 1").onClick.Invoke();
            Check(session.Progression.GetUnspentCharacterPoints(pc) == beforeUnsupported
                  && controller.Feedback.Contains("Pick Pocket"),
                "unsupported Pick Pocket targeting fails closed without character mutation");

            controller.Close();
            Check(controller.Open(GameUiScreen.Inventory), "I/Inventory management-family route opens");
            inventory.Synchronize();
            Check(inventory.IsVisible && !character.IsVisible, "Inventory owns one source management composition");
            Check(controller.Open(GameUiScreen.Character), "Inventory to Character switching succeeds");
            yield return null;
            character.Synchronize();
            inventory.Synchronize();
            Check(character.IsVisible && !inventory.IsVisible, "Character replaces Inventory without stale instances");

            foreach ((int width, int height) in new[]
                     { (800, 600), (1024, 768), (1920, 1080), (2560, 1440), (3840, 2160) })
            {
                Rect rect = RetailCharacterSheetLayout.ScreenRect(width, height);
                Check(rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= width && rect.yMax <= height,
                    $"{width}x{height} keeps the fixed active-editor composition accessible");
            }

            Check(Object.FindObjectsByType<RetailCharacterSheetView>(FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<RetailInventoryEquipmentView>(FindObjectsSortMode.None).Length == 1
                  && Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == 1,
                "one Character view, Inventory view, and production presenter own management UI");
            Debug.Log("UI-E.2 PHYSICAL VALIDATION: PASS; Character=C/HUD button>ID22 active editor; "
                      + "Skills=ID29 groups/ranks/training/transactional plus-minus; "
                      + "K/ID472>ID274 rotating window>four source actions; unsupported targeting fails closed; "
                      + "Inventory>Character switching has one owner; Original>Enhanced>Original preserves authority; "
                      + "resolutions=800x600/1024x768/1080p/1440p/4K accessible; source HUD retained. "
                      + "Play Mode remains open on Character for visual proof.");
        }

        private static Button FindButton(Component root, string name)
            => root.GetComponentsInChildren<SourceUiButton>(false).Single(value => value.name == name).Button;

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }
    }
}
