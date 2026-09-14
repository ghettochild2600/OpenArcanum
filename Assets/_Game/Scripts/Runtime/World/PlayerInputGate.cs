using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Transient presentation gate for player-originated world input; it owns no gameplay state.</summary>
    public sealed class PlayerInputGate : MonoBehaviour
    {
        public bool IsBlocked { get; private set; }

        public void SetBlocked(bool blocked) => IsBlocked = blocked;

        private void OnDisable() => IsBlocked = false;
    }
}
