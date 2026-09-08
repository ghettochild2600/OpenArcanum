using System;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Binds one deterministic session-owned PC to the current sector presentation.</summary>
    [RequireComponent(typeof(WorldMapSessionCoordinator))]
    [RequireComponent(typeof(WorldObjectSectorLoader))]
    [RequireComponent(typeof(PlayerNavigationController))]
    public sealed class ProductionPlayerLifecycle : MonoBehaviour
    {
        private static readonly Guid PlayerGuid = new("25e7b7c9-1ae7-4af5-b1e4-0a62fa6bca01");

        [SerializeField] private Vector2Int spawnTile = new(36, 58);
        // CRITTER type (2) + human body + male + villager clothes + no shield + unarmed + STAND.
        [SerializeField] private uint playerArtId = 0x28100000u;

        private WorldMapSessionCoordinator _session;
        private WorldObjectSectorLoader _loader;
        private PlayerNavigationController _navigation;

        public static ArcanumObjectId DefaultPlayerIdentity => ArcanumObjectId.CreateGuid(PlayerGuid);
        public PersistentPlayerState State => _session?.PlayerState;
        public WorldObject Presentation { get; private set; }

        private void Awake()
        {
            _session = GetComponent<WorldMapSessionCoordinator>();
            _loader = GetComponent<WorldObjectSectorLoader>();
            _navigation = GetComponent<PlayerNavigationController>();
        }

        private void OnEnable()
        {
            if (_session == null) _session = GetComponent<WorldMapSessionCoordinator>();
            _session.SectorUnloading += OnSectorUnloading;
            _session.SectorSelected += OnSectorSelected;
        }

        private void Start()
        {
            if (_session.HasSelectedSector) SpawnAndBind();
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.SectorUnloading -= OnSectorUnloading;
                _session.SectorSelected -= OnSectorSelected;
            }
            Unbind();
        }

        public bool SpawnAndBind()
        {
            if (_session == null || _loader == null || _navigation == null
                || !_session.HasSelectedSector || !_loader.IsSectorPresented)
                return false;

            PersistentPlayerState state = _session.GetOrCreatePlayer(
                DefaultPlayerIdentity,
                _session.SelectedSector,
                spawnTile,
                playerArtId);
            WorldObject runtime = _loader.CreatePlayerPresentation(state);
            if (runtime == null || !_navigation.TryBind(runtime)) return false;
            Presentation = runtime;
            return true;
        }

        public void Unbind()
        {
            _navigation?.Unbind(Presentation);
            Presentation = null;
        }

        private void OnSectorUnloading(string _) => Unbind();
        private void OnSectorSelected(string _) => SpawnAndBind();
    }
}
