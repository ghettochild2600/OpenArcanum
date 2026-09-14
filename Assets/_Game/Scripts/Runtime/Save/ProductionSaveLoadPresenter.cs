using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Save
{
    /// <summary>Minimal player-facing IMGUI projection over the authoritative M6B slot service.</summary>
    [RequireComponent(typeof(WorldMapSessionCoordinator), typeof(PlayerInputGate))]
    public sealed class ProductionSaveLoadPresenter : MonoBehaviour
    {
        public const KeyCode ToggleKey = KeyCode.F6;

        private WorldMapSessionCoordinator _session;
        private PlayerInputGate _inputGate;
        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private Vector2 _scroll;

        public SaveLoadPanelController Controller { get; private set; }
        public bool IsOpen => Controller?.IsOpen == true;

        private void Awake()
        {
            _session = GetComponent<WorldMapSessionCoordinator>();
            _inputGate = GetComponent<PlayerInputGate>();
            _navigation = GetComponent<PlayerNavigationController>();
            _interaction = GetComponent<PlayerInteractionController>();
            EnsureController();
        }

        private void OnDisable()
        {
            Controller?.Close();
            _inputGate?.SetBlocked(false);
        }

        private void Update()
        {
            EnsureController();
            if (_inputGate != null && _inputGate.IsBlocked != IsOpen) SyncInputGate();
            if (Input.GetKeyDown(ToggleKey))
            {
                if (IsOpen) Close();
                else Open(SaveLoadPanelMode.Save);
                return;
            }
            if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (Controller.Confirmation != SaveLoadConfirmation.None) Controller.CancelConfirmation();
            else Close();
        }

        public void Open(SaveLoadPanelMode mode)
        {
            EnsureController();
            _navigation ??= GetComponent<PlayerNavigationController>();
            _interaction ??= GetComponent<PlayerInteractionController>();
            _navigation?.CancelRoute();
            _interaction?.CancelPending();
            Controller.Open(mode);
            _scroll = Vector2.zero;
            SyncInputGate();
        }

        public void Close()
        {
            Controller?.Close();
            SyncInputGate();
        }

        /// <summary>Rebinds presentation to a slot catalog supplied by a production host or validation fixture.</summary>
        public void BindSlotOperations(ISessionSaveSlotOperations operations)
        {
            Controller?.Close();
            Controller = new SaveLoadPanelController(operations);
            SyncInputGate();
        }

        internal void LoadSelectedForTests()
        {
            Controller.RequestLoad();
            SyncInputGate();
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            SyncInputGate();
            float width = Mathf.Min(900f, Screen.width - 40f);
            float height = Mathf.Min(640f, Screen.height - 40f);
            var area = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            GUI.depth = -100;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("OpenArcanum — Manual Save / Load");
            GUILayout.Label($"F6 opens or closes this panel. Escape cancels confirmation or closes it.");

            GUILayout.BeginHorizontal();
            GUI.enabled = Controller.Mode != SaveLoadPanelMode.Save;
            if (GUILayout.Button("SAVE", GUILayout.Height(34f))) Controller.SetMode(SaveLoadPanelMode.Save);
            GUI.enabled = Controller.Mode != SaveLoadPanelMode.Load;
            if (GUILayout.Button("LOAD", GUILayout.Height(34f))) Controller.SetMode(SaveLoadPanelMode.Load);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (Controller.Slots.Count == 0)
            {
                GUILayout.Label("No manual save slots exist yet.", GUI.skin.box);
            }
            foreach (SaveLoadSlotView slot in Controller.Slots)
            {
                bool selected = Controller.SelectedSlotId == slot.SlotId;
                string marker = selected ? "▶ " : string.Empty;
                string validity = slot.IsValid ? string.Empty : "  [CORRUPT / INCOMPATIBLE]";
                if (GUILayout.Button($"{marker}{slot.SlotId}{validity}", GUILayout.Height(30f)))
                    Controller.SelectSlot(slot.SlotId);
                GUILayout.Label($"  {slot.Timestamp}  |  Level {slot.Level}  |  {slot.Version}");
                GUILayout.Label($"  {slot.Location}");
                if (!slot.IsValid) GUILayout.Label($"  {slot.Error}", GUI.skin.textArea);
                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();

            if (Controller.Mode == SaveLoadPanelMode.Save && string.IsNullOrEmpty(Controller.SelectedSlotId))
                GUILayout.Label($"New save: {Controller.SuggestedSlotId}");
            if (!string.IsNullOrEmpty(Controller.StatusMessage))
                GUILayout.Label(Controller.StatusMessage, GUI.skin.box);
            if (!string.IsNullOrEmpty(Controller.ErrorMessage))
                GUILayout.Label("ERROR: " + Controller.ErrorMessage, GUI.skin.textArea);

            if (Controller.Confirmation != SaveLoadConfirmation.None)
            {
                GUILayout.Label(Controller.ConfirmationMessage, GUI.skin.box);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("CONFIRM", GUILayout.Height(38f)))
                {
                    Controller.Confirm();
                    SyncInputGate();
                }
                if (GUILayout.Button("CANCEL", GUILayout.Height(38f))) Controller.CancelConfirmation();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (Controller.Mode == SaveLoadPanelMode.Save)
                {
                    if (GUILayout.Button(string.IsNullOrEmpty(Controller.SelectedSlotId)
                            ? "CREATE SAVE" : "SAVE / OVERWRITE", GUILayout.Height(40f)))
                        Controller.RequestSave();
                    if (!string.IsNullOrEmpty(Controller.SelectedSlotId)
                        && GUILayout.Button("NEW SLOT", GUILayout.Height(40f)))
                        Controller.SelectNewSlot();
                }
                else if (GUILayout.Button("LOAD SELECTED", GUILayout.Height(40f)))
                {
                    LoadSelectedForTests();
                }
                GUI.enabled = !string.IsNullOrEmpty(Controller.SelectedSlotId);
                if (GUILayout.Button("DELETE", GUILayout.Height(40f))) Controller.RequestDelete();
                GUI.enabled = true;
                if (GUILayout.Button("CLOSE", GUILayout.Height(40f))) Close();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void EnsureController()
        {
            _session ??= GetComponent<WorldMapSessionCoordinator>();
            if (Controller == null && _session != null)
                Controller = new SaveLoadPanelController(_session.SaveSlots);
        }

        private void SyncInputGate()
        {
            _inputGate ??= GetComponent<PlayerInputGate>();
            _inputGate?.SetBlocked(IsOpen);
        }
    }
}
