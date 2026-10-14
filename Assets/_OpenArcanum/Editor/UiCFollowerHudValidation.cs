using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenArcanum.Editor
{
    /// <summary>Focused native 800x600 proof for the source follower-status HUD.</summary>
    [InitializeOnLoad]
    public static class UiCFollowerHudValidation
    {
        private const string MenuPath = "OpenArcanum/UI-C/Run Follower HUD Physical Validation";
        private const string PendingKey = "OpenArcanum.UIC.FollowerHudPhysicalValidationPending";
        private const string VirgilSector = "maps/arcanum1-024-fixed/86570436012.sec";
        private static readonly ArcanumObjectId Virgil = Parse("G_A09DCD63_7A15_D411_8F1D_00E02920220C");

        static UiCFollowerHudValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("UI-C FOLLOWER HUD PHYSICAL VALIDATION: stop Play Mode before starting a new run.");
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
            WorldMapSessionCoordinator session = loader.Session;
            GameUiController controller = presenter.Controller;
            session.ResetAuthoritativeSession();
            yield return null;
            Check(session.SelectSector(VirgilSector), "authentic Virgil sector loads");
            yield return null;

            loader = Require(Object.FindFirstObjectByType<WorldObjectSectorLoader>(), "reloaded sector loader");
            var lifecycle = Require(Object.FindFirstObjectByType<ProductionPlayerLifecycle>(),
                "production player lifecycle");
            if (lifecycle.Presentation == null) Check(lifecycle.SpawnAndBind(), "production PC binds");
            Check(session.TryGetObjectState(Virgil, out PersistentObjectState virgilState)
                  && virgilState.Type == ObjectType.Npc && virgilState.PrototypeNumber == 17102
                  && virgilState.DialogNum == 1324, "exact retail Virgil fixture resolves");
            Check(session.Party.Join(Virgil).Succeeded, "Virgil joins through authoritative party transaction");

            presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                "production UI presenter after sector load");
            controller = presenter.Controller;
            controller.Close();
            controller.Refresh();
            RetailGameplayHudView view = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(),
                "retail gameplay HUD");
            view.Synchronize(true);

            Check(Screen.width == 800 && Screen.height == 600, "native 800x600 Game view");
            GameUiPartyMemberView member = controller.ProjectParty().Single();
            Check(member.Identity == Virgil && member.PortraitId > 0,
                "authentic Virgil identity and portrait project");
            RectTransform slot = (RectTransform)Descendant(view, "Follower Slot 1");
            Check(slot.gameObject.activeSelf && slot.anchoredPosition == new Vector2(11f, -50f)
                  && slot.sizeDelta == new Vector2(40f, 49f), "source first-slot rectangle projects");
            Check(Descendant(view, "Follower Slot 6").gameObject.activeSelf == false,
                "unused follower slots remain hidden");
            Check(Descendant(slot, "Portrait").GetComponent<Image>().sprite != null,
                "retail portrait/gameport source supplies the live Virgil portrait");

            int maximumFatigue = session.Vitality.GetMaximumFatigue(Virgil);
            session.Vitality.ApplyHitPointDamage(Virgil,
                Mathf.Max(1, session.Vitality.GetCurrentHitPoints(Virgil) - 1));
            session.Vitality.ApplyFatigueDamage(Virgil, Mathf.Max(1, maximumFatigue / 2));
            session.Poison.Apply(Virgil, 1000);
            view.Synchronize(true);

            member = controller.ProjectParty().Single();
            SourceUiImage health = Descendant(slot, "Health").GetComponent<SourceUiImage>();
            SourceUiImage fatigue = Descendant(slot, "Fatigue").GetComponent<SourceUiImage>();
            Check(member.HealthPercent < RetailGameplayHudLayout.FollowerLowHealthThreshold
                  && Descendant(slot, "Low Health Tint").gameObject.activeSelf,
                "sub-20-percent health activates the source red portrait tint");
            Check(member.PoisonLevel > RetailGameplayHudLayout.FollowerPoisonThreshold
                  && health.Key.SourceId == RetailGameplayHudLayout.FollowerPoisonSourceId,
                "poison above 20 replaces source health ART 505 with ART 616");
            Check(Mathf.Approximately(health.Image.fillAmount,
                      RetailGameplayHudLayout.FollowerVisiblePixels(member.HealthPercent) / 34f)
                  && Mathf.Approximately(fatigue.Image.fillAmount,
                      RetailGameplayHudLayout.FollowerVisiblePixels(member.FatiguePercent) / 34f),
                "health and fatigue use source 34-pixel integer crops");

            string save = session.SaveGames.SerializeCurrentSession();
            Check(session.SaveGames.LoadJson(save).Succeeded, "authoritative session reload succeeds");
            yield return null;
            presenter = Require(Object.FindFirstObjectByType<ProductionGameUiPresenter>(),
                "production UI presenter after reload");
            controller = presenter.Controller;
            controller.Close();
            controller.Refresh();
            view = Require(Object.FindFirstObjectByType<RetailGameplayHudView>(), "retail gameplay HUD after reload");
            view.Synchronize(true);
            member = controller.ProjectParty().Single();
            Check(member.Identity == Virgil && member.PoisonLevel > RetailGameplayHudLayout.FollowerPoisonThreshold
                  && member.HealthPercent < RetailGameplayHudLayout.FollowerLowHealthThreshold,
                "save/load reconstructs follower membership, poison, and vitality projection");

            Debug.Log("UI-C FOLLOWER HUD PHYSICAL VALIDATION: PASS; resolution=800x600; "
                      + "follower=authentic Virgil 17102; slots=6; first=(11,50,40,49); "
                      + $"portrait={member.PortraitId}; health={member.HealthPercent}%; "
                      + $"fatigue={member.FatiguePercent}%; poison={member.PoisonLevel}; "
                      + "poison-art=616; low-health-tint=active; save-load=reconstructed. "
                      + "Play Mode remains open for native inspection.");
        }

        private static Transform Descendant(Component root, string name)
            => root.GetComponentsInChildren<Transform>(true).First(value => value.name == name);

        private static T Require<T>(T value, string description) where T : class
            => value ?? throw new InvalidOperationException("Missing " + description + ".");

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + description + ".");
        }

        private static ArcanumObjectId Parse(string text)
        {
            if (!ArcanumObjectId.TryParsePersistent(text, out ArcanumObjectId identity))
                throw new InvalidOperationException("Invalid Arcanum identity " + text + ".");
            return identity;
        }
    }
}
