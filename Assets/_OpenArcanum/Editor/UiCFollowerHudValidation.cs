using System;
using System.Collections;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Party;
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

            RetailFollowerSlotTarget slotTarget = Require(slot.GetComponent<RetailFollowerSlotTarget>(),
                "follower portrait pointer target");
            slotTarget.OnPointerEnter(null);
            view.Synchronize(true);
            Check(view.WorldTarget?.Identity == Virgil && !string.IsNullOrWhiteSpace(view.WorldTarget.Name),
                "portrait hover projects Virgil examination into the bottom HUD");
            slotTarget.OnPointerExit(null);
            view.Synchronize(true);
            Check(view.WorldTarget == null, "portrait exit clears Virgil examination");

            slotTarget.OnPointerClick(new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left });
            yield return null;
            Check(session.Dialogue.Phase == DialogueSessionPhase.AwaitingPlayerChoice,
                "left click routes through normal Virgil interaction/dialogue");
            controller.CancelDialogue();
            yield return null;

            slotTarget.OnPointerClick(new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Right });
            RectTransform commandMenu = (RectTransform)Descendant(view, "Follower Command Menu");
            Check(commandMenu.gameObject.activeSelf && commandMenu.anchoredPosition == new Vector2(51f, -50f),
                "right click opens source command menu beside the portrait");
            SourceUiImage commandBackground = Descendant(commandMenu, "Follower Command Background")
                .GetComponent<SourceUiImage>();
            Check(commandBackground.Key.SourceId == RetailGameplayHudLayout.FollowerCommandMenuSourceId,
                "command menu uses source ART 826");
            string[] expectedCommands =
            {
                "Walk", "Attack", "Stay Close", "Spread Out", "Back Off", "Wait", "Inventory",
                "Character Sheet",
            };
            for (int index = 0; index < expectedCommands.Length; index++)
            {
                RectTransform row = (RectTransform)Descendant(commandMenu, $"Follower Command {index}");
                SourceUiBitmapText label = Descendant(commandMenu, $"Follower Command Label {index}")
                    .GetComponent<SourceUiBitmapText>();
                Check(row.anchoredPosition == new Vector2(10f, -(3f + 18f * index))
                      && row.sizeDelta == new Vector2(118f, 15f), $"source command row {index} rectangle");
                Check(label.Text == expectedCommands[index]
                      && label.FontSourceId == RetailGameplayHudLayout.FollowerCommandFontSourceId,
                    $"source command row {index} label/font");
            }

            Descendant(commandMenu, "Follower Command 5").GetComponent<RetailFollowerCommandTarget>()
                .OnPointerClick(null);
            PartyFollowerOrderState order = session.Party.GetOrderState(Virgil);
            Check(order.LastOrder == PartyOrder.Wait && !order.FollowingEnabled,
                "Wait stops only Virgil following");
            slotTarget.OnPointerClick(new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Right });
            Descendant(commandMenu, "Follower Command 2").GetComponent<RetailFollowerCommandTarget>()
                .OnPointerClick(null);
            order = session.Party.GetOrderState(Virgil);
            Check(order.LastOrder == PartyOrder.StayClose && order.FollowingEnabled
                  && order.DesiredFollowRange == PartyFollowerMovementService.SourceCloseRange,
                "Stay Close restores source formation for Virgil");
            Check(controller.CanOpenFollowerInventory(Virgil)
                  && controller.OpenFollowerInventory(Virgil)
                  && controller.OpenInventoryTarget == Virgil,
                "legal active follower inventory opens through shared inventory authority");
            controller.Close();
            Check(controller.OpenFollowerCharacter(Virgil)
                  && controller.CharacterTarget == Virgil && controller.CharacterReadOnly,
                "Character Sheet opens passive Virgil examination");
            controller.Close();

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

            slot = (RectTransform)Descendant(view, "Follower Slot 1");
            slotTarget = Require(slot.GetComponent<RetailFollowerSlotTarget>(),
                "reconstructed follower portrait pointer target");
            slotTarget.OnPointerClick(new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Right });
            commandMenu = (RectTransform)Descendant(view, "Follower Command Menu");
            Check(commandMenu.gameObject.activeSelf, "source follower command menu remains open for inspection");

            Debug.Log("UI-C FOLLOWER HUD PHYSICAL VALIDATION: PASS; resolution=800x600; "
                      + "follower=authentic Virgil 17102; slots=6; first=(11,50,40,49); "
                      + $"portrait={member.PortraitId}; health={member.HealthPercent}%; "
                      + $"fatigue={member.FatiguePercent}%; poison={member.PoisonLevel}; "
                      + "hover=examination; left-click=dialogue; right-click=ART826; commands=8; "
                      + "wait/stay-close=per-member; inventory=shared-authority; character=passive; "
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
