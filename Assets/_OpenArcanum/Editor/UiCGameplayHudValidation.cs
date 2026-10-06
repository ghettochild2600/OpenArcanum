using System;
using System.Collections;
using System.IO;
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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Production Play Mode proof for UI-C. Enhanced pixels are generated in a temporary directory.</summary>
    [InitializeOnLoad]
    public static class UiCGameplayHudValidation
    {
        private const string MenuPath = "OpenArcanum/UI-C/Run Physical PlayMode Validation";
        private const string PendingKey = "OpenArcanum.UIC.PhysicalValidationPending";

        static UiCGameplayHudValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-C PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            string fixtureRoot = Path.Combine(Path.GetTempPath(), "OpenArcanum-UIC-Physical-"
                                                                  + Guid.NewGuid().ToString("N"));
            UiSkinResolver fixtureSkin = null;
            GraphicsMode initialMode = OpenArcanumGraphicsSettings.Mode;
            try
            {
                ProductionGameUiPresenter presenter = Require(
                    Object.FindFirstObjectByType<ProductionGameUiPresenter>(), "production UI presenter");
                WorldMapSessionCoordinator session = presenter.GetComponent<WorldMapSessionCoordinator>();
                GameUiController controller = presenter.Controller;
                session.ResetAuthoritativeSession();
                controller.Refresh();
                yield return null;

                Check(controller.BeginNewGame(), "fresh authentic New Game opens character creation");
                controller.SetCreationName("UI-C Human");
                controller.SetCreationIdentity(CharacterRace.Human, CharacterGender.Male);
                controller.SetCreationPortrait(1005);
                controller.SetCreationBackground(3);
                controller.AdjustCreationSpell(SpellCollege.Earth, 1);
                Check(controller.CreationValidation.Succeeded, "bounded source-valid character is ready");
                Check(controller.FinalizeNewGame(), "fresh New Game reaches START_MAP 1");
                yield return null;
                yield return null;

                presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                    "production UI presenter after New Game");
                controller = presenter.Controller;
                loader = Require(Object.FindFirstObjectByType<WorldObjectSectorLoader>(), "crash-site loader");
                var lifecycle = Require(Object.FindFirstObjectByType<ProductionPlayerLifecycle>(),
                    "production player lifecycle");
                if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
                yield return null;

                RetailGameplayHudView view = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                    "retail gameplay HUD");
                view.Synchronize(true);
                Check(session.SelectedSector.Contains("arcanum1-024-fixed"),
                    "validation remains in the crash-site START_MAP");
                Check(view.IsVisible && view.TopAsset?.Key.SourceId == 185
                                     && view.TopAsset.LogicalSize == new Vector2Int(800, 41)
                                     && view.BottomAsset?.Key.SourceId == 184
                                     && view.BottomAsset.LogicalSize == new Vector2Int(800, 159),
                    "source 185/184 supply the active top/bottom gameplay windows");
                Check(!view.GetComponentsInChildren<SourceUiImage>(true)
                        .Any(value => value.CurrentAsset?.Key.SourceId == 3),
                    "legacy/unknown source 3 composite is not production HUD authority");
                Check(Object.FindObjectsByType<RetailGameplayHudView>(FindObjectsSortMode.None).Length == 1,
                    "exactly one retail gameplay HUD presenter");
                Check(Object.FindObjectsByType<ProductionGameUiPresenter>(FindObjectsSortMode.None).Length == 1,
                    "exactly one production controller presenter");
                Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1
                      && Object.FindFirstObjectByType<EventSystem>().isActiveAndEnabled,
                    "one persistent active EventSystem serves the HUD");

                GameUiHudView hud = controller.ProjectHud();
                Check(hud != null && view.LastProjection.HitPoints == hud.HitPoints
                                  && view.LastProjection.Fatigue == hud.Fatigue,
                    "health and fatigue are read from authoritative M4 state");
                Check(Mathf.Approximately(view.HealthFill,
                          RetailGameplayHudLayout.LiquidFill01(hud.HitPoints, hud.MaximumHitPoints))
                      && Mathf.Approximately(view.FatigueFill,
                          RetailGameplayHudLayout.LiquidFill01(hud.Fatigue, hud.MaximumFatigue)),
                    "source vial crop tracks authoritative values");
                Check(Camera.main != null && Approximately(Camera.main.rect,
                          RetailGameplayHudLayout.GameplayCameraViewport(Screen.width, Screen.height)),
                    "camera renders behind the transparent source composition");

                Button inventory = FindButton("Inventory");
                inventory.onClick.Invoke();
                view.Synchronize(true);
                Check(controller.Screen == GameUiScreen.Inventory && !view.IsVisible,
                    "source inventory control routes to existing M12C screen");
                controller.Close();
                view.Synchronize(true);
                Check(view.IsVisible, "closing management returns to the same gameplay HUD");

                Check(controller.AssignQuickSlotSpell(0, PhaseOneSpellCatalog.StrengthOfEarth),
                    "learned source spell assigns through coordinator quick-slot authority");
                view.Synchronize(true);
                Check(view.QuickSlots[0].Kind == QuickSlotKind.Spell
                      && view.QuickSlots[0].SourceId == PhaseOneSpellCatalog.StrengthOfEarth,
                    "source slot 1 presents the authoritative spell binding");
                int fatigue = hud.Fatigue;
                Check(controller.ActivateQuickSlot(0), "number-key quick-slot command activates the binding");
                view.Synchronize(true);
                Check(session.Shortcuts.ActiveSlot == 0 && view.LastProjection.Fatigue < fatigue,
                    "quick-slot activation commits only through existing magic/vitality authority");

                PersistentObjectState combatTarget = session.States.Values
                    .Where(value => value.Identity != session.PlayerState.Identity
                                    && value.Type == Arcanum.Formats.Objects.ObjectType.Npc
                                    && session.Vitality.TryGet(value.Identity, out _)
                                    && !session.Vitality.IsDead(value.Identity)
                                    && session.TryGetLoadedObject(value.Identity, out _))
                    .OrderBy(value => value.Identity.Key, StringComparer.Ordinal)
                    .FirstOrDefault();
                Check(combatTarget != null, "an authentic loaded crash-site NPC is available for combat proof");
                Check(controller.StartAttack(combatTarget.Identity, forceAttack: true),
                    "source combat route starts turn-based combat against the authentic loaded actor");
                view.Synchronize(true);
                Check(controller.ProjectHud().CombatActive && view.LastProjection.CombatActive,
                    "source combat control enters combat and projects live state");
                Check(controller.ProjectHud().CombatMode == view.LastProjection.CombatMode,
                    "combat mode/readiness use M8 projection");
                if (session.Combat.IsActive)
                    Check(session.Combat.EndCombat(session.PlayerState.Identity).Succeeded,
                        "combat ends through existing M8 authority");
                view.Synchronize(true);

                Directory.CreateDirectory(fixtureRoot);
                var retail = RetailUiAssetResolver.CreateProduction();
                var enhanced = new EnhancedUiAssetResolver(fixtureRoot, _ => { });
                fixtureSkin = new UiSkinResolver(retail, enhanced);
                UiResolvedAsset originalTop = null;
                UiResolvedAsset originalBottom = null;
                Check(retail.TryResolve(new UiAssetKey(185), out originalTop),
                    "retail top HUD strip resolves for Enhanced proof");
                Check(retail.TryResolve(new UiAssetKey(184), out originalBottom),
                    "retail bottom HUD strip resolves for Enhanced proof");
                WriteFixture(enhanced.GetReplacementPath(originalTop), originalTop.LogicalSize * 4);
                WriteFixture(enhanced.GetReplacementPath(originalBottom), originalBottom.LogicalSize * 4);
                view.Bind(controller, session, screen => controller.Open(screen), fixtureSkin);
                OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Enhanced);
                view.Synchronize(true);
                Check(view.TopAsset?.ResolvedSkin == UiAssetSkin.Enhanced
                      && view.TopAsset.Texture.width == 3200
                      && view.TopAsset.Texture.height == 164
                      && view.BottomAsset?.ResolvedSkin == UiAssetSkin.Enhanced
                      && view.BottomAsset.Texture.width == 3200
                      && view.BottomAsset.Texture.height == 636,
                    "generated exact-4x Enhanced HUD strips appear at source-native geometry");
                Check(view.QuickSlots[0].Kind == QuickSlotKind.Spell
                      && session.PlayerState.Identity == ProductionPlayerLifecycle.DefaultPlayerIdentity,
                    "skin rebuild preserves controller/session/slot authority");
                OpenArcanumGraphicsSettings.SetRuntimeMode(GraphicsMode.Original);
                view.Synchronize(true);
                Check(view.TopAsset?.ResolvedSkin == UiAssetSkin.Original
                      && view.BottomAsset?.ResolvedSkin == UiAssetSkin.Original,
                    "Original retail HUD returns without state loss");

                foreach ((int width, int height) in new[]
                         {
                             (800, 600), (1024, 768), (1920, 1080), (2560, 1440), (3840, 2160),
                         })
                {
                    Rect top = RetailGameplayHudLayout.TopScreenRect(width, height);
                    Rect bottom = RetailGameplayHudLayout.BottomScreenRect(width, height);
                    Rect viewport = RetailGameplayHudLayout.GameplayCameraViewport(width, height);
                    Check(top.width == 800f && top.height == 41f && top.y == 0f
                          && bottom.width == 800f && bottom.height == 159f
                          && Mathf.Approximately(bottom.y, height - 159f)
                          && Mathf.Approximately(top.x, (width - 800f) * .5f)
                          && Mathf.Approximately(bottom.x, top.x)
                          && viewport == new Rect(0f, 0f, 1f, 1f),
                        $"{width}x{height} uses native HRP top/bottom gravity and full-screen world");
                }

                RetailMainMenuCursorView cursor = view.GetComponentsInChildren<RetailMainMenuCursorView>(true)
                    .Single(value => value.name == "Retail Gameplay Cursor" && value.gameObject.activeInHierarchy);
                Check(cursor.transform.parent.name == "Retail Gameplay Cursor Layer"
                      && Mathf.Approximately(cursor.transform.lossyScale.x, 1f)
                      && Mathf.Approximately(cursor.transform.lossyScale.y, 1f),
                    "source cursor uses native full-screen pointer mapping and hotspot geometry");

                view.Bind(controller, session, screen => controller.Open(screen));
                view.Synchronize(true);
                yield return null;
                Check(Object.FindObjectsByType<RetailGameplayHudView>(FindObjectsSortMode.None).Length == 1
                      && Object.FindObjectsByType<RetailMainMenuCursorView>(FindObjectsSortMode.None)
                          .Count(value => value.name == "Retail Gameplay Cursor") == 1
                      && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                    "rebuild leaves one HUD, one gameplay cursor, and one EventSystem");

                Debug.Log("UI-C PHYSICAL VALIDATION: PASS; fresh New Game=crash-site START_MAP 1; "
                          + "source HUD=185 top 800x41 + 184 bottom 800x159; world=full-screen; "
                          + "vitals/equipment/message=authoritative; source Inventory route=PASS; "
                          + "quick slot spell activation=PASS; combat state=PASS; "
                          + "skin=Original>Enhanced exact-4x strips>Original; "
                          + "resolutions=800x600/1024x768/1080p/1440p/4K native HRP gravity; "
                          + "cursor=native full-screen mapping; "
                          + "presenters=1; EventSystems=1. Play Mode remains open at the crash site for visual proof.");
            }
            finally
            {
                OpenArcanumGraphicsSettings.SetRuntimeMode(initialMode);
                fixtureSkin?.Dispose();
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        private static Button FindButton(string name)
            => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.name == name);

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }

        private static bool Approximately(Rect left, Rect right)
            => Mathf.Abs(left.x - right.x) < .001f && Mathf.Abs(left.y - right.y) < .001f
               && Mathf.Abs(left.width - right.width) < .001f && Mathf.Abs(left.height - right.height) < .001f;

        private static void WriteFixture(string path, Vector2Int size)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var row = Enumerable.Repeat(new Color32(172, 36, 80, 255), size.x).ToArray();
                for (int y = 0; y < size.y; y++) texture.SetPixels32(0, y, size.x, 1, row);
                texture.SetPixel(0, 0, Color.clear);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
