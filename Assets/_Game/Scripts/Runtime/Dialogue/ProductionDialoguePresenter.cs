using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Dialogue
{
    /// <summary>Minimal M5A IMGUI projection over the authoritative dialogue session.</summary>
    [RequireComponent(typeof(WorldMapSessionCoordinator))]
    public sealed class ProductionDialoguePresenter : MonoBehaviour
    {
        private WorldMapSessionCoordinator _session;

        private void Awake() => _session = GetComponent<WorldMapSessionCoordinator>();

        private void Update()
        {
            ProductionDialogueSession dialogue = _session?.Dialogue;
            if (dialogue == null || dialogue.Phase != DialogueSessionPhase.AwaitingPlayerChoice) return;
            if (Input.GetKeyDown(KeyCode.Escape)) dialogue.Cancel("Cancelled by player.");
            for (int index = 0; index < dialogue.AvailableResponses.Count && index < 5; index++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + index))) dialogue.SelectResponse(index);
        }

        private void OnGUI()
        {
            ProductionDialogueSession dialogue = _session?.Dialogue;
            if (dialogue == null || dialogue.Phase != DialogueSessionPhase.AwaitingPlayerChoice) return;
            float width = Mathf.Min(760f, Screen.width - 40f);
            var area = new Rect((Screen.width - width) * .5f, Mathf.Max(20f, Screen.height - 330f), width, 310f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(dialogue.NpcText ?? string.Empty, GUI.skin.textArea);
            GUILayout.Space(8f);
            for (int index = 0; index < dialogue.AvailableResponses.Count; index++)
            {
                int choice = index;
                string text = dialogue.AvailableResponses[index].Text;
                if (GUILayout.Button($"{index + 1}. {text}", GUILayout.MinHeight(30f)))
                    dialogue.SelectResponse(choice);
            }
            if (!string.IsNullOrEmpty(dialogue.LastFailure))
                GUILayout.Label(dialogue.LastFailure);
            if (GUILayout.Button("Cancel (Esc)")) dialogue.Cancel("Cancelled by player.");
            GUILayout.EndArea();
        }
    }
}
