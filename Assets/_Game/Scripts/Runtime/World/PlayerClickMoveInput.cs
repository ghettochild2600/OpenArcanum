using Arcanum.Formats.Objects;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.UI;
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
        private PlayerInputGate _inputGate;
        private ProductionCombatPresenter _combatPresenter;
        private ProductionGameUiPresenter _ui;
        private Vector3 _pressPosition;
        private ArcanumObjectId _dragCorpse;

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
            _inputGate = GetComponent<PlayerInputGate>();
            _combatPresenter = GetComponent<ProductionCombatPresenter>();
            _ui = GetComponent<ProductionGameUiPresenter>();
        }

        private void Update()
        {
            _inputGate ??= GetComponent<PlayerInputGate>();
            if (_inputGate != null && _inputGate.IsBlocked) return;
            if (Input.GetMouseButtonDown(0))
            {
                _pressPosition = Input.mousePosition;
                _dragCorpse = default;
                if (AltHeld && TryScreenWorld(_pressPosition, out Vector3 pressedWorld)
                    && WorldObjectTargetSelector.TrySelectInteractionTarget(_loader.SpriteOwners, pressedWorld,
                        out ArcanumObjectId pressed, out ObjectType pressedType)
                    && pressedType == ObjectType.Npc && _loader.Session.Vitality.TryGet(pressed, out _)
                    && _loader.Session.Vitality.IsDead(pressed))
                    _dragCorpse = pressed;
            }
            if (!Input.GetMouseButtonUp(0)) return;
            bool dragged = (Input.mousePosition - _pressPosition).sqrMagnitude
                           > dragThresholdPixels * dragThresholdPixels;
            if (dragged)
            {
                if (!_dragCorpse.IsNull && TryScreenWorld(Input.mousePosition, out Vector3 dropWorld))
                {
                    Vector3 localDrop = _loader.transform.InverseTransformPoint(dropWorld);
                    Vector2Int destination = IsoProjection.WorldToTile(localDrop, _loader.PixelsPerUnit);
                    LastClickedObject = _dragCorpse;
                    LastClickedTile = destination;
                    LastClickAccepted = _loader.NavigationMap.Contains(destination)
                                        && _loader.Session.TryDragCorpse(_loader.Session.PlayerState.Identity,
                                            _dragCorpse, destination);
                }
                _dragCorpse = default;
                return;
            }

            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null || _loader == null || _loader.NavigationMap == null) return;
            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(cam.transform.position.z - _loader.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(screen);
            _combatPresenter ??= GetComponent<ProductionCombatPresenter>();
            if (_combatPresenter != null && _combatPresenter.TryHandleWorldClick(world))
            {
                LastClickedTile = null;
                LastClickedObject = _combatPresenter.Controller.SelectedTarget;
                LastInteractionResult = null;
                LastClickAccepted = _combatPresenter.Controller.LastFailure == CombatFailure.None;
                if (LastClickAccepted && ShiftHeld) _combatPresenter.Controller.SubmitAttack();
                return;
            }
            if (WorldObjectTargetSelector.TrySelectInteractionTarget(_loader.SpriteOwners, world,
                    out ArcanumObjectId target, out ObjectType targetType))
            {
                LastClickedTile = null;
                LastClickedObject = target;
                _ui ??= GetComponent<ProductionGameUiPresenter>();
                if (targetType == ObjectType.Npc && _loader.Session.Vitality.TryGet(target, out _)
                    && !_loader.Session.Vitality.IsDead(target)
                    && (_ui?.Controller.CursorMode == GameUiCursorMode.Attack || AltHeld))
                {
                    LastInteractionResult = null;
                    LastClickAccepted = _ui.Controller.StartAttack(target, AltHeld);
                    return;
                }
                WorldInteractionResult result = targetType switch
                {
                    ObjectType.Portal => _interaction.TryUse(target),
                    ObjectType.Scenery => _interaction.TryUse(target),
                    ObjectType.Npc when _loader.Session.Vitality.TryGet(target, out _)
                                             && _loader.Session.Vitality.IsDead(target)
                        => _interaction.TryLoot(target),
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
            if (ShiftHeld) { LastClickAccepted = false; return; }
            bool insideSelectedSector = _loader.NavigationMap.Contains(tile);
            _ui ??= GetComponent<ProductionGameUiPresenter>();
            _navigation.SetRunIntent(_ui != null ? _ui.Keyboard.EffectiveRun : ControlHeld);
            LastClickAccepted = (!insideSelectedSector || _loader.NavigationMap.IsWalkable(tile))
                                && _navigation.TrySetDestination(tile);
        }

        private bool AltHeld => _ui != null ? _ui.Keyboard.AltHeld
            : Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        private bool ShiftHeld => _ui != null ? _ui.Keyboard.ShiftHeld
            : Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        private bool ControlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private bool TryScreenWorld(Vector3 position, out Vector3 world)
        {
            world = default;
            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null || _loader == null) return false;
            position.z = Mathf.Abs(cam.transform.position.z - _loader.transform.position.z);
            world = cam.ScreenToWorldPoint(position);
            return true;
        }
    }
}
