using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Minimal M7D player-facing list. It reads projection state and emits an intent; it cannot travel.</summary>
    [RequireComponent(typeof(WorldMapSessionCoordinator), typeof(PlayerInputGate))]
    public sealed class ProductionWorldMapDestinationPresenter : MonoBehaviour
    {
        public const KeyCode ToggleKey = KeyCode.F7;

        private WorldMapSessionCoordinator _session;
        private PlayerInputGate _inputGate;
        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private Vector2 _scroll;

        public bool IsOpen { get; private set; }
        public bool HasSelection { get; private set; }
        public WorldMapSelectionResult LastSelection { get; private set; }
        public event Action<WorldMapTravelRequest> TravelRequested;

        private void Awake()
        {
            _session = GetComponent<WorldMapSessionCoordinator>();
            _inputGate = GetComponent<PlayerInputGate>();
            _navigation = GetComponent<PlayerNavigationController>();
            _interaction = GetComponent<PlayerInteractionController>();
        }

        private void OnDisable() => Close();

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                if (IsOpen) Close();
                else Open();
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public void Open()
        {
            _navigation ??= GetComponent<PlayerNavigationController>();
            _interaction ??= GetComponent<PlayerInteractionController>();
            _navigation?.CancelRoute();
            _interaction?.CancelPending();
            IsOpen = true;
            HasSelection = false;
            LastSelection = default;
            _scroll = Vector2.zero;
            _inputGate?.SetBlocked(true);
        }

        public void Close()
        {
            IsOpen = false;
            _inputGate?.SetBlocked(false);
        }

        public bool TryGetVisibleDestinations(out IReadOnlyList<WorldMapDestination> destinations,
            out WorldMapDestinationFailure failure)
        {
            EnsureSession();
            return _session.WorldMapDestinations.TryProjectVisible(out destinations, out failure);
        }

        public WorldMapSelectionResult Select(Arcanum.Formats.World.AreaId id)
        {
            EnsureSession();
            LastSelection = _session.WorldMapDestinations.TrySelectWorldArea(id);
            HasSelection = true;
            if (LastSelection.Succeeded) TravelRequested?.Invoke(LastSelection.Request);
            return LastSelection;
        }

        private void EnsureSession()
            => _session ??= GetComponent<WorldMapSessionCoordinator>()
                ?? throw new InvalidOperationException("A world-map destination presenter requires session authority.");

        private void OnGUI()
        {
            if (!IsOpen) return;
            float width = Mathf.Min(540f, Screen.width - 40f);
            float height = Mathf.Min(560f, Screen.height - 40f);
            var area = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            GUI.depth = -90;
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("World-map destinations (M7D selection proof)");
            GUILayout.Label("Only source-known locations are shown. Selection emits an intent; travel is not implemented.");

            if (!TryGetVisibleDestinations(out IReadOnlyList<WorldMapDestination> destinations,
                    out WorldMapDestinationFailure failure))
            {
                GUILayout.Label("ERROR: " + failure, GUI.skin.textArea);
            }
            else
            {
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                if (destinations.Count == 0)
                    GUILayout.Label("No known destinations. Unknown locations are hidden by source semantics.", GUI.skin.box);
                foreach (WorldMapDestination destination in destinations)
                {
                    if (GUILayout.Button($"{destination.DisplayName}  [area {destination.AreaId.Value}]",
                            GUILayout.Height(34f)))
                        Select(destination.AreaId);
                    GUILayout.Label($"  Tile {destination.WorldTile} — {destination.Description}");
                }
                GUILayout.EndScrollView();
            }

            if (HasSelection && LastSelection.Succeeded)
                GUILayout.Label($"Selected {LastSelection.Destination.DisplayName}: intent "
                              + $"area {LastSelection.Request.AreaId.Value} at {LastSelection.Request.Destination}. "
                              + "No travel executed.", GUI.skin.box);
            else if (HasSelection)
                GUILayout.Label("Unavailable: " + LastSelection.Detail, GUI.skin.box);
            if (GUILayout.Button("CLOSE", GUILayout.Height(36f))) Close();
            GUILayout.EndArea();
        }
    }
}
