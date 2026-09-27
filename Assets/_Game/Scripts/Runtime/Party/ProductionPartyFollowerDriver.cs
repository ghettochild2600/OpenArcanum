using Arcanum.Runtime.World;
using UnityEngine;

namespace Arcanum.Runtime.Party
{
    [DisallowMultipleComponent]
    public sealed class ProductionPartyFollowerDriver : MonoBehaviour
    {
        private const float StepIntervalSeconds = 0.2f;
        private WorldObjectSectorLoader _loader;
        private PartyFollowerMovementService _movement;
        private float _elapsed;

        private void Awake() => Bind();
        private void OnEnable() => Bind();

        private void Bind()
        {
            _loader = GetComponent<WorldObjectSectorLoader>();
            _movement = _loader == null ? null : new PartyFollowerMovementService(_loader.Session);
            _elapsed = 0;
        }

        private void Update()
        {
            if (_loader == null || _movement == null || _loader.NavigationMap == null) return;
            _elapsed += Time.deltaTime;
            if (_elapsed < StepIntervalSeconds) return;
            _elapsed = 0;
            var members = _loader.Session.Party.Members;
            for (int index = 0; index < members.Count; index++)
                _movement.AdvanceOneStep(members[index].Identity, _loader.NavigationMap);
        }
    }
}
