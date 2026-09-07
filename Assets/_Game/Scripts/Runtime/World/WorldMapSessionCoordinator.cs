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

        public bool ValidateSector(string sector, IReadOnlyList<ObjectInstance> sources, out string error)
        {
            var seen = new HashSet<ArcanumObjectId>();
            foreach (ObjectInstance source in sources)
            {
                if (!source.Identity.IsPersistent) continue; // Classified by the loader; never fabricate an ID.
                if (!seen.Add(source.Identity))
                {
                    error = $"Duplicate authored ObjectID {source.Identity} in '{sector}'.";
                    return false;
                }
                if (_states.TryGetValue(source.Identity, out var existing) && !existing.Matches(source, sector))
                {
                    error = $"ObjectID collision {source.Identity}: '{existing.SourceSector}' and '{sector}' differ in source metadata.";
                    return false;
                }
                foreach (var loaded in _loaded)
                    if (loaded.Key != sector && loaded.Value.ContainsKey(source.Identity))
                    {
                        error = $"ObjectID {source.Identity} already loaded by '{loaded.Key}'.";
                        return false;
                    }
            }
            error = null;
            return true;
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

        public PersistentObjectState GetOrCreate(ObjectInstance source, string sector, uint artId, bool off, bool locked)
        {
            if (!source.Identity.IsPersistent) return null;
            if (_states.TryGetValue(source.Identity, out var existing))
            {
                if (!existing.Matches(source, sector)) throw new InvalidOperationException($"ObjectID collision: {source.Identity}");
                return existing;
            }
            var state = new PersistentObjectState(source, sector, artId, off, locked);
            _states.Add(source.Identity, state);
            return state;
        }

        public void Bind(string sector, PersistentObjectState state, WorldObject runtime)
        {
            if (state == null) return;
            _loaded[sector].Add(state.Identity, runtime);
            state.Restore(runtime);
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
