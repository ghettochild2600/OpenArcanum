using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Combat
{
    [DisallowMultipleComponent]
    public sealed class ProductionCombatAiDriver : MonoBehaviour
    {
        private WorldMapSessionCoordinator _session;
        private CombatAiController _controller;

        public CombatAiController Controller
        {
            get
            {
                if (_controller == null)
                    Bind(GetComponent<WorldObjectSectorLoader>()?.Session);
                return _controller;
            }
        }
        public int TickCount { get; private set; }

        private void Awake()
        {
            Bind(GetComponent<WorldObjectSectorLoader>()?.Session);
        }

        private void OnEnable()
        {
            if (_session == null)
                Bind(GetComponent<WorldObjectSectorLoader>()?.Session);
        }

        private void OnDisable()
        {
            _controller?.ResetTransient();
        }

        private void Update()
        {
            Tick();
        }

        public void Bind(WorldMapSessionCoordinator session)
        {
            _session = session;
            _controller = session == null ? null : new CombatAiController(session);
        }

        public void Tick()
        {
            CombatAiController controller = Controller;
            if (controller == null || _session == null)
                return;

            TickCount++;
            if (!_session.Combat.IsActive)
            {
                controller.ResetTransient();
                return;
            }

            if (_session.Combat.Mode == CombatMode.TurnBased)
            {
                var current = _session.Combat.CurrentParticipant;
                if (!current.IsNull && _session.Combat.IsAutonomousCombatNpc(current))
                    controller.RunCurrentTurn();
                return;
            }

            controller.TickReadyRealTimeActors();
        }
    }
}
