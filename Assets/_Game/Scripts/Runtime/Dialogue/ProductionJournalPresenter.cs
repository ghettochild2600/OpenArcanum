using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Dialogue
{
    /// <summary>Minimal read-only M5B projection; final logbook UI remains outside this slice.</summary>
    [RequireComponent(typeof(WorldMapSessionCoordinator))]
    public sealed class ProductionJournalPresenter : MonoBehaviour
    {
        private WorldMapSessionCoordinator _session;

        private void Awake() => _session = GetComponent<WorldMapSessionCoordinator>();

        private void OnGUI()
        {
            if (_session?.PlayerState == null || !_session.Journal.HasSource) return;
            bool lowIntelligence = _session.Characters.GetEffectiveAttribute(
                _session.PlayerState.Identity, CharacterAttribute.Intelligence) <= 4;
            var entries = _session.Journal.ProjectAll(lowIntelligence);
            if (entries.Count == 0) return;

            GUILayout.BeginArea(new Rect(12f, 12f, Mathf.Min(420f, Screen.width - 24f),
                Mathf.Min(220f, Screen.height - 24f)), GUI.skin.box);
            GUILayout.Label("Quest journal (M5B projection)");
            foreach (var entry in entries)
            {
                GUILayout.Label($"Day {entry.Timestamp.Days + 1} — {entry.StateLabel}");
                GUILayout.Label(entry.Description, GUI.skin.textArea);
            }
            GUILayout.EndArea();
        }
    }
}
