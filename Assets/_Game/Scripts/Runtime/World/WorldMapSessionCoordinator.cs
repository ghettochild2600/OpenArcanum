using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Authoritative in-memory map/sector selection and persistent world/player state.</summary>
    public sealed class WorldMapSessionCoordinator : MonoBehaviour, ISectorSelectionAuthority
    {
        private readonly Dictionary<ArcanumObjectId, PersistentObjectState> _states = new();
        private readonly Dictionary<string, Dictionary<ArcanumObjectId, LoadedBinding>> _loaded = new();
        [SerializeField] private MonoBehaviour terrainSectorOwner;
        [SerializeField] private MonoBehaviour objectSectorOwner;
        [SerializeField] private string initialSector;
        [SerializeField] private bool selectOnStart = true;
        private ISectorPresentationOwner _terrainOwner;
        private ISectorPresentationOwner _objectOwner;

        public IReadOnlyDictionary<ArcanumObjectId, PersistentObjectState> States => _states;
        public int LoadedSectorCount => _loaded.Count;
        public string CurrentMap { get; private set; }
        public string SelectedSector { get; private set; }
        public bool HasSelectedSector => !string.IsNullOrEmpty(SelectedSector);
        public PersistentPlayerState PlayerState { get; private set; }
        public PortalTransitionScheduler Portals { get; } = new();
        public event Action<string> SectorUnloading;
        public event Action<string> SectorSelected;

        private sealed class LoadedBinding
        {
            public WorldObject Runtime;
            public PersistentObjectState ObjectState;
            public PersistentPlayerState PlayerState;
        }

        private void Awake()
        {
            if (terrainSectorOwner is ISectorPresentationOwner terrain) RegisterTerrainOwner(terrain);
            if (objectSectorOwner is ISectorPresentationOwner objects) RegisterObjectOwner(objects);
        }

        private void Start()
        {
            if (!selectOnStart || HasSelectedSector) return;
            string sector = !string.IsNullOrWhiteSpace(initialSector) ? initialSector
                : _objectOwner?.ConfiguredSector ?? _terrainOwner?.ConfiguredSector;
            if (!string.IsNullOrWhiteSpace(sector)) SelectSector(sector);
        }

        private void Update() => Portals.Tick(Time.deltaTime);

        public void RegisterTerrainOwner(ISectorPresentationOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (_terrainOwner != null && !ReferenceEquals(_terrainOwner, owner))
                throw new InvalidOperationException("A terrain sector owner is already registered.");
            _terrainOwner = owner;
            if (owner is ISectorSelectionClient client) client.BindSelectionAuthority(this);
        }

        public void RegisterObjectOwner(ISectorPresentationOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (_objectOwner != null && !ReferenceEquals(_objectOwner, owner))
                throw new InvalidOperationException("A world-object sector owner is already registered.");
            _objectOwner = owner;
        }

        public bool SelectSector(string sectorPath)
        {
            string sector = NormalizeSector(sectorPath);
            if (sector == null || (_terrainOwner == null && _objectOwner == null)) return false;
            if (SelectedSector == sector && OwnersPresent(sector)) return true;

            if (HasSelectedSector || _terrainOwner?.IsSectorPresented == true || _objectOwner?.IsSectorPresented == true)
                ClearSelectedSector();
            bool terrainReady = _terrainOwner == null || _terrainOwner.PresentSector(sector);
            bool objectsReady = terrainReady && (_objectOwner == null || _objectOwner.PresentSector(sector));
            if (!terrainReady || !objectsReady)
            {
                _objectOwner?.ClearPresentedSector();
                _terrainOwner?.ClearPresentedSector();
                return false;
            }

            SelectedSector = sector;
            SectorSelected?.Invoke(sector);
            return true;
        }

        public bool ReloadSelectedSector()
        {
            string sector = SelectedSector;
            if (sector == null) return false;
            ClearSelectedSector();
            return SelectSector(sector);
        }

        public void ClearSelectedSector()
        {
            string sector = SelectedSector ?? _objectOwner?.PresentedSector ?? _terrainOwner?.PresentedSector;
            if (sector != null) SectorUnloading?.Invoke(sector);
            _objectOwner?.ClearPresentedSector();
            _terrainOwner?.ClearPresentedSector();
            SelectedSector = null;
        }

        private bool OwnersPresent(string sector)
            => (_terrainOwner == null || _terrainOwner.IsSectorPresented && _terrainOwner.PresentedSector == sector)
               && (_objectOwner == null || _objectOwner.IsSectorPresented && _objectOwner.PresentedSector == sector);

        public static string NormalizeSector(string sectorPath)
            => string.IsNullOrWhiteSpace(sectorPath)
                ? null
                : sectorPath.Replace('\\', '/').Trim().ToLowerInvariant();

        public void BindPortal(PersistentObjectState state, WorldObject runtime, int frameCount, int fps)
        {
            if (state == null) return;
            Portals.Bind(state, frameCount, fps, (artId, open) =>
            {
                if (runtime != null) runtime.ApplyPortalState(artId, open);
            });
            runtime.Session = this;
        }

        public bool ValidateSector(
            string sector,
            IReadOnlyList<ObjectInstance> sources,
            IReadOnlyDictionary<ObjectInstance, ArcanumObjectId> identities,
            out string error)
        {
            var seen = new HashSet<ArcanumObjectId>();
            foreach (ObjectInstance source in sources)
            {
                ArcanumObjectId identity = identities[source];
                if (!identity.IsPersistent) continue;
                if (!seen.Add(identity))
                {
                    error = $"Duplicate persistent ObjectID {identity} in '{sector}'.";
                    return false;
                }
                if (_states.TryGetValue(identity, out var existing) && !existing.Matches(source, sector))
                {
                    error = $"ObjectID collision {identity}: '{existing.SourceSector}' and '{sector}' differ in source metadata.";
                    return false;
                }
                foreach (var loaded in _loaded)
                    if (loaded.Key != sector && loaded.Value.ContainsKey(identity))
                    {
                        error = $"ObjectID {identity} already loaded by '{loaded.Key}'.";
                        return false;
                    }
            }
            error = null;
            return true;
        }

        public bool ValidateSector(string sector, IReadOnlyList<ObjectInstance> sources, out string error)
        {
            var identities = new Dictionary<ObjectInstance, ArcanumObjectId>();
            foreach (ObjectInstance source in sources) identities.Add(source, source.Identity);
            return ValidateSector(sector, sources, identities, out error);
        }

        public void BeginSector(string sector)
        {
            if (_loaded.ContainsKey(sector)) throw new InvalidOperationException($"Sector already registered: {sector}");
            string map = sector.Substring(0, sector.LastIndexOf('/'));
            if (_loaded.Count > 0 && CurrentMap != map)
                throw new InvalidOperationException("Unload the current map before selecting another map.");
            CurrentMap = map;
            _loaded.Add(sector, new Dictionary<ArcanumObjectId, LoadedBinding>());
        }

        public PersistentObjectState GetOrCreate(
            ObjectInstance source,
            ArcanumObjectId identity,
            string sector,
            uint artId,
            bool off,
            bool locked)
        {
            if (!identity.IsPersistent) return null;
            if (_states.TryGetValue(identity, out var existing))
            {
                if (!existing.Matches(source, sector)) throw new InvalidOperationException($"ObjectID collision: {identity}");
                return existing;
            }
            var state = new PersistentObjectState(source, identity, sector, artId, off, locked);
            _states.Add(identity, state);
            return state;
        }

        public PersistentObjectState GetOrCreate(ObjectInstance source, string sector, uint artId, bool off, bool locked)
            => GetOrCreate(source, source.Identity, sector, artId, off, locked);

        public void Bind(string sector, PersistentObjectState state, WorldObject runtime)
        {
            if (state == null) return;
            _loaded[sector].Add(state.Identity, new LoadedBinding { Runtime = runtime, ObjectState = state });
            state.Restore(runtime);
        }

        public PersistentPlayerState GetOrCreatePlayer(
            ArcanumObjectId identity,
            string sector,
            Vector2 spawnTile,
            uint artId)
        {
            if (!identity.IsPersistent) throw new ArgumentException("Player identity must be persistent.", nameof(identity));
            if (_states.ContainsKey(identity)) throw new InvalidOperationException($"Player ObjectID collides with {identity}.");
            string normalized = NormalizeSector(sector) ?? throw new ArgumentException("Player sector is required.", nameof(sector));
            if (PlayerState == null)
                PlayerState = new PersistentPlayerState(identity, normalized, spawnTile, artId);
            else
            {
                if (PlayerState.Identity != identity)
                    throw new InvalidOperationException("A different production player is already registered.");
                PlayerState.EnterSector(normalized, spawnTile, artId);
            }
            return PlayerState;
        }

        public void BindPlayer(string sector, PersistentPlayerState state, WorldObject runtime)
        {
            if (state == null || runtime == null) throw new ArgumentNullException(state == null ? nameof(state) : nameof(runtime));
            if (!ReferenceEquals(state, PlayerState)) throw new InvalidOperationException("Player state is not session-owned.");
            var bindings = _loaded[sector];
            if (bindings.ContainsKey(state.Identity)) throw new InvalidOperationException("Player is already bound in this sector.");
            bindings.Add(state.Identity, new LoadedBinding { Runtime = runtime, PlayerState = state });
            state.Restore(runtime);
            runtime.Session = this;
        }

        /// <summary>Applies a critter movement sample through session-owned state, then updates its runtime view.</summary>
        public bool SetMovementState(ArcanumObjectId identity, Vector2 tilePosition, uint artId, bool moving)
        {
            if (_states.TryGetValue(identity, out PersistentObjectState state))
            {
                state.TilePosition = tilePosition;
                state.ArtId = artId;
            }
            else if (PlayerState != null && PlayerState.Identity == identity)
            {
                PlayerState.SetLocalPosition(SelectedSector ?? PlayerState.Sector, tilePosition);
                PlayerState.ArtId = artId;
            }
            else return false;

            foreach (Dictionary<ArcanumObjectId, LoadedBinding> sector in _loaded.Values)
                if (sector.TryGetValue(identity, out LoadedBinding binding) && binding.Runtime != null)
                {
                    binding.Runtime.ApplyMovementState(tilePosition, artId, moving);
                    return true;
                }
            return false;
        }

        public void SetPlayerDestination(Vector2Int destination)
        {
            if (PlayerState == null) throw new InvalidOperationException("No production player is registered.");
            PlayerState.SetDestination(destination);
        }

        public void ClearPlayerDestination() => PlayerState?.ClearDestination();

        /// <summary>Captures/unloads the old projection, relocates the same player state, then selects both new owners.</summary>
        public bool TryTransitionPlayer(string targetSector, Vector2 entryTile, uint artId)
        {
            if (PlayerState == null || !HasSelectedSector) return false;
            string target = NormalizeSector(targetSector);
            if (!SectorCoordinate.TryParse(target, out SectorCoordinate targetCoordinate)
                || !SectorCoordinate.TryParse(SelectedSector, out SectorCoordinate currentCoordinate)
                || targetCoordinate.MapPath != currentCoordinate.MapPath)
                return false;

            string previousSector = SelectedSector;
            Vector2 previousMapPosition = PlayerState.MapPosition;
            uint previousArtId = PlayerState.ArtId;
            ClearSelectedSector();
            PlayerState.Relocate(target, entryTile, artId);
            if (SelectSector(target)) return true;

            PlayerState.RestoreMapPosition(currentCoordinate.MapPath, previousMapPosition, previousArtId);
            SelectSector(previousSector);
            return false;
        }

        public void UnloadSector(string sector)
        {
            if (sector == null || !_loaded.TryGetValue(sector, out var bindings)) return;
            foreach (var pair in bindings)
            {
                Portals.Unbind(pair.Key);
                LoadedBinding binding = pair.Value;
                if (binding.Runtime != null)
                {
                    binding.ObjectState?.Capture(binding.Runtime);
                    binding.PlayerState?.Capture(binding.Runtime);
                    binding.Runtime.Session = null;
                }
            }
            _loaded.Remove(sector);
            if (_loaded.Count == 0) CurrentMap = null;
        }

        private void OnDestroy()
        {
            foreach (string sector in new List<string>(_loaded.Keys)) UnloadSector(sector);
        }
    }
}
