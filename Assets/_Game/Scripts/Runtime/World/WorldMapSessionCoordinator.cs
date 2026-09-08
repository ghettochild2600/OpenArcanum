using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>One in-memory session. No HD loading, terrain ownership, disk saves or interaction rules.</summary>
    public sealed class WorldMapSessionCoordinator : MonoBehaviour
    {
        private readonly Dictionary<ArcanumObjectId, PersistentObjectState> _states = new();
        private readonly Dictionary<string, Dictionary<ArcanumObjectId, WorldObject>> _loaded = new();
        public IReadOnlyDictionary<ArcanumObjectId, PersistentObjectState> States => _states;
        public int LoadedSectorCount => _loaded.Count;
        public string CurrentMap { get; private set; }
        public PortalTransitionScheduler Portals { get; } = new();

        private void Update() => Portals.Tick(Time.deltaTime);

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
            _loaded.Add(sector, new Dictionary<ArcanumObjectId, WorldObject>());
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
            _loaded[sector].Add(state.Identity, runtime);
            state.Restore(runtime);
        }

        /// <summary>Applies a critter movement sample through session-owned state, then updates its runtime view.</summary>
        public bool SetMovementState(ArcanumObjectId identity, Vector2 tilePosition, uint artId, bool moving)
        {
            if (!_states.TryGetValue(identity, out PersistentObjectState state)) return false;
            state.TilePosition = tilePosition;
            state.ArtId = artId;
            foreach (Dictionary<ArcanumObjectId, WorldObject> sector in _loaded.Values)
                if (sector.TryGetValue(identity, out WorldObject runtime) && runtime != null)
                {
                    runtime.ApplyMovementState(tilePosition, artId, moving);
                    return true;
                }
            return false;
        }

        public void UnloadSector(string sector)
        {
            if (sector == null || !_loaded.TryGetValue(sector, out var bindings)) return;
            foreach (var pair in bindings)
            {
                Portals.Unbind(pair.Key);
                if (pair.Value != null)
                {
                    _states[pair.Key].Capture(pair.Value);
                    pair.Value.Session = null;
                }
            }
            _loaded.Remove(sector);
        }

        private void OnDestroy()
        {
            foreach (string sector in new List<string>(_loaded.Keys)) UnloadSector(sector);
        }
    }
}
