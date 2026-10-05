using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Combat
{
    /// <summary>Minimal production IMGUI projection over the transient M8I controller.</summary>
    [RequireComponent(typeof(WorldMapSessionCoordinator), typeof(WorldObjectSectorLoader))]
    public sealed class ProductionCombatPresenter : MonoBehaviour
    {
        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PlayerInputGate _inputGate;

        public CombatUiController Controller { get; private set; }

        private void Awake()
        {
            _session = GetComponent<WorldMapSessionCoordinator>();
            _loader = GetComponent<WorldObjectSectorLoader>();
            _inputGate = GetComponent<PlayerInputGate>();
            EnsureController();
        }

        private void OnDisable() => Controller?.ResetTransient();

        private void Update()
        {
            EnsureController();
            Controller.Refresh();
        }

        /// <summary>Consumes ordinary world clicks while combat is active and converts only critters to identity.</summary>
        public bool TryHandleWorldClick(Vector2 worldPoint)
        {
            EnsureController();
            if (!Controller.IsVisible || _inputGate != null && _inputGate.IsBlocked) return false;
            if (WorldObjectTargetSelector.TrySelectCombatTarget(_loader.SpriteOwners, worldPoint,
                    out ArcanumObjectId target))
                Controller.SelectTarget(target);
            else
                Controller.SelectTarget(default);
            return true;
        }

        private void OnGUI()
        {
            EnsureController();
            Controller.Refresh();
            if (!Controller.IsVisible || _inputGate != null && _inputGate.IsBlocked) return;

            GUI.depth = -80;
            float width = Mathf.Min(520f, Screen.width - 24f);
            float height = Mathf.Min(560f, Screen.height - 24f);
            var area = new Rect(12f, 12f, width, height);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"COMBAT — {Controller.Mode}");
            if (Controller.Mode == CombatMode.TurnBased)
            {
                GUILayout.Label($"Current actor: {Controller.CurrentActor}");
                GUILayout.Label($"AP: {Controller.CurrentActionPoints}/{Controller.MaximumActionPoints}");
            }
            else
            {
                string readiness = Controller.IsRealTimeBusy ? "BUSY"
                    : Controller.IsRealTimeReady ? "READY" : "RECOVERING";
                GUILayout.Label($"Player readiness: {readiness}");
            }

            GUILayout.Label(Controller.HasSelectedTarget
                ? $"Target: {Controller.SelectedTarget}" : "Target: select a hostile in the world");

            GUILayout.BeginHorizontal();
            foreach (CombatCalledLocation location in new[]
                     {
                         CombatCalledLocation.Torso, CombatCalledLocation.Head,
                         CombatCalledLocation.Arm, CombatCalledLocation.Leg,
                     })
            {
                GUI.enabled = Controller.CalledLocation != location;
                if (GUILayout.Button(location.ToString())) Controller.SetCalledLocation(location);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("MELEE ATTACK", GUILayout.Height(38f)))
            {
                Controller.SetAttackMode(CombatAttackMode.BasicMelee);
                Controller.SubmitAttack();
            }
            if (GUILayout.Button("BOW ATTACK", GUILayout.Height(38f)))
            {
                Controller.SetAttackMode(CombatAttackMode.BasicRanged);
                Controller.SubmitAttack();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Arrows: {Controller.AmmoQuantity}");

            CombatAttackPreview preview = Controller.Preview;
            if (Controller.HasSelectedTarget)
            {
                if (preview.Succeeded)
                {
                    GUILayout.Label($"Effectiveness: {preview.FinalEffectiveAttackValue}%  "
                                    + $"Final hit chance: {preview.Chance.FinalChance}%");
                    foreach (CombatAttackModifier modifier in preview.ModifierLedger.Entries
                                 .Where(value => value.Applied))
                    {
                        string suppressed = modifier.Suppressed ? " (suppressed)" : string.Empty;
                        GUILayout.Label($"{modifier.Reason}: {modifier.Value:+#;-#;0}{suppressed}");
                    }
                }
                else
                {
                    GUILayout.Label($"INVALID: {preview.Failure}", GUI.skin.box);
                }
            }

            if (!string.IsNullOrEmpty(Controller.Feedback))
                GUILayout.Label(Controller.Feedback, GUI.skin.box);
            if (Controller.LastResult.HasValue)
            {
                CombatAttackResult result = Controller.LastResult.Value;
                GUILayout.Label($"Damage: {result.MitigatedHitPointDamage} HP; "
                                + $"ammo {result.AmmoQuantityBefore}->{result.AmmoQuantityAfter}");
            }

            if (Controller.Mode == CombatMode.TurnBased
                && GUILayout.Button("END TURN (E)", GUILayout.Height(34f)))
                Controller.EndTurn();
            GUILayout.EndArea();
        }

        private void EnsureController()
        {
            _session ??= GetComponent<WorldMapSessionCoordinator>();
            _loader ??= GetComponent<WorldObjectSectorLoader>();
            _inputGate ??= GetComponent<PlayerInputGate>();
            if (Controller == null && _session != null) Controller = new CombatUiController(_session);
        }
    }
}
