using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Converts an undragged left click into a sector-local Arcanum tile destination.</summary>
    [RequireComponent(typeof(PlayerNavigationController))]
    public sealed class PlayerClickMoveInput : MonoBehaviour
    {
        [SerializeField] private Camera gameCamera;
        [SerializeField, Min(0f)] private float dragThresholdPixels = 6f;

        private PlayerNavigationController _navigation;
        private PlayerInteractionController _interaction;
        private WorldObjectSectorLoader _loader;
        private Vector3 _pressPosition;

        public Vector2Int? LastClickedTile { get; private set; }
        public ArcanumObjectId? LastClickedObject { get; private set; }
        public WorldInteractionResult? LastInteractionResult { get; private set; }
        public bool LastClickAccepted { get; private set; }

        private void Awake()
        {
            _navigation = GetComponent<PlayerNavigationController>();
            _loader = GetComponent<WorldObjectSectorLoader>();
            _interaction = GetComponent<PlayerInteractionController>()
                ?? gameObject.AddComponent<PlayerInteractionController>();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0)) _pressPosition = Input.mousePosition;
            if (!Input.GetMouseButtonUp(0)) return;
            if ((Input.mousePosition - _pressPosition).sqrMagnitude > dragThresholdPixels * dragThresholdPixels) return;

            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null || _loader == null || _loader.NavigationMap == null) return;
            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(cam.transform.position.z - _loader.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(screen);
            if (WorldObjectTargetSelector.TrySelectInteractionTarget(_loader.SpriteOwners, world,
                    out ArcanumObjectId target, out ObjectType targetType))
            {
                LastClickedTile = null;
                LastClickedObject = target;
                WorldInteractionResult result = targetType switch
                {
                    ObjectType.Portal => _interaction.TryUse(target),
                    ObjectType.Npc => _interaction.TryTalk(target),
                    _ => _interaction.TryPickUp(target),
                };
                LastInteractionResult = result;
                LastClickAccepted = result.IsAccepted;
                return;
            }
            Vector3 local = _loader.transform.InverseTransformPoint(world);
            Vector2Int tile = IsoProjection.WorldToTile(local, _loader.PixelsPerUnit);
            LastClickedTile = tile;
            LastClickedObject = null;
            LastInteractionResult = null;
            LastClickAccepted = _loader.NavigationMap.IsWalkable(tile) && _navigation.TrySetDestination(tile);
        }
    }
}
