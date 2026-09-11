using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Script;
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
        private Func<int, ObjectProtoInfo> _resolvePrototype;
        private ulong _nextDynamicIdentity = 1;

        public IReadOnlyDictionary<ArcanumObjectId, PersistentObjectState> States => _states;
        public int LoadedSectorCount => _loaded.Count;
        public string CurrentMap { get; private set; }
        public string SelectedSector { get; private set; }
        public bool HasSelectedSector => !string.IsNullOrEmpty(SelectedSector);
        public PersistentPlayerState PlayerState { get; private set; }
        public PortalTransitionScheduler Portals { get; } = new();
        public ScriptGlobals ScriptGlobals { get; } = new();
        public WorldUseScriptDispatcher UseScripts { get; private set; }
        public event Action<string> SectorUnloading;
        public event Action<string> SectorSelected;
        public event Action<PersistentObjectState, ObjectPlacement, ObjectPlacement> ObjectPlacementChanged;

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

        public void BindUseScriptSource(ScriptDatabase scripts)
        {
            if (scripts == null) throw new ArgumentNullException(nameof(scripts));
            UseScripts = new WorldUseScriptDispatcher(this, scripts.Get, ScriptGlobals);
        }

        public void BindUseScriptSource(Func<int, ScriptFile> resolveScript)
            => UseScripts = new WorldUseScriptDispatcher(this, resolveScript, ScriptGlobals);

        public void BindPrototypeSource(Func<int, ObjectProtoInfo> resolvePrototype)
            => _resolvePrototype = resolvePrototype ?? throw new ArgumentNullException(nameof(resolvePrototype));

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
            var incomingTypes = new Dictionary<ArcanumObjectId, ObjectType>();
            var candidateParents = new Dictionary<ArcanumObjectId, ArcanumObjectId>();
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
                incomingTypes.Add(identity, source.Type);
                candidateParents[identity] = _states.TryGetValue(identity, out PersistentObjectState retained)
                    ? retained.ParentIdentity : source.ParentIdentity;
            }

            var validationParents = new Dictionary<ArcanumObjectId, ArcanumObjectId>();
            foreach (PersistentObjectState state in _states.Values)
                validationParents[state.Identity] = state.ParentIdentity;
            foreach (KeyValuePair<ArcanumObjectId, ArcanumObjectId> pair in candidateParents)
                validationParents[pair.Key] = pair.Value;

            foreach (KeyValuePair<ArcanumObjectId, ArcanumObjectId> pair in validationParents)
            {
                ArcanumObjectId parent = pair.Value;
                if (!parent.IsPersistent) continue;
                if (pair.Key == parent)
                {
                    error = $"ObjectID {pair.Key} cannot contain itself.";
                    return false;
                }
                if (TryResolveKnownType(parent, incomingTypes, out ObjectType parentType)
                    && !IsInventoryOwnerType(parentType))
                {
                    error = $"Inventory parent {parent} has unsupported owner type {parentType}.";
                    return false;
                }
                var visited = new HashSet<ArcanumObjectId> { pair.Key };
                ArcanumObjectId ancestor = parent;
                while (ancestor.IsPersistent)
                {
                    if (!visited.Add(ancestor))
                    {
                        error = $"Inventory relationship for {pair.Key} contains a cycle at {ancestor}.";
                        return false;
                    }
                    if (validationParents.TryGetValue(ancestor, out ArcanumObjectId incomingParent))
                        ancestor = incomingParent;
                    else break;
                }
            }
            error = null;
            return true;
        }

        private bool TryResolveKnownType(ArcanumObjectId identity,
            IReadOnlyDictionary<ArcanumObjectId, ObjectType> incoming, out ObjectType type)
        {
            if (PlayerState != null && PlayerState.Identity == identity)
            {
                type = ObjectType.Pc;
                return true;
            }
            if (_states.TryGetValue(identity, out PersistentObjectState state))
            {
                type = state.Type;
                return true;
            }
            return incoming.TryGetValue(identity, out type);
        }

        public bool ValidateSector(string sector, IReadOnlyList<ObjectInstance> sources, out string error)
        {
            var identities = new Dictionary<ObjectInstance, ArcanumObjectId>();
            foreach (ObjectInstance source in sources) identities[source] = source.Identity;
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

        public bool TryGetObjectState(ArcanumObjectId identity, out PersistentObjectState state)
            => _states.TryGetValue(identity, out state);

        public bool TryGetPlacement(ArcanumObjectId identity, out ObjectPlacement placement)
        {
            if (_states.TryGetValue(identity, out PersistentObjectState state))
            {
                placement = state.Placement;
                return true;
            }
            if (PlayerState != null && PlayerState.Identity == identity)
            {
                placement = ObjectPlacement.InWorld(PlayerState.Sector, PlayerState.TilePosition);
                return true;
            }
            placement = default;
            return false;
        }

        public IReadOnlyList<ArcanumObjectId> ChildrenOf(ArcanumObjectId parent)
        {
            var children = new List<ArcanumObjectId>();
            foreach (PersistentObjectState state in _states.Values)
                if (state.Placement.Kind == ObjectPlacementKind.Contained
                    && state.Placement.ParentIdentity == parent)
                    children.Add(state.Identity);
            children.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            return children;
        }

        public bool IsWorldPresentationEligible(PersistentObjectState state, string sector)
            => state != null && !state.Off && state.Placement.Kind == ObjectPlacementKind.World
               && string.Equals(state.Placement.Sector, NormalizeSector(sector), StringComparison.Ordinal);

        /// <summary>Validates and commits one raw containment transfer. Gameplay/equipment rules are separate.</summary>
        public InventoryTransferResult TransferItem(ArcanumObjectId itemIdentity, ObjectPlacement source,
            ObjectPlacement destination)
        {
            if (!_states.TryGetValue(itemIdentity, out PersistentObjectState item))
                return new InventoryTransferResult(InventoryResultCode.ItemNotFound, itemIdentity, default, destination);
            ObjectPlacement previous = item.Placement;
            if (!IsItemType(item.Type))
                return new InventoryTransferResult(InventoryResultCode.InvalidItemType, itemIdentity, previous, destination);
            if (previous != source)
                return new InventoryTransferResult(InventoryResultCode.SourceMismatch, itemIdentity, previous, destination);
            InventoryResultCode validation = ValidateDestination(itemIdentity, destination);
            if (validation != InventoryResultCode.Success)
                return new InventoryTransferResult(validation, itemIdentity, previous, destination);
            if (previous == destination)
                return new InventoryTransferResult(InventoryResultCode.AlreadyAtDestination, itemIdentity, previous, destination);

            item.Placement = destination;
            if (destination.Kind == ObjectPlacementKind.World) item.TilePosition = destination.TilePosition;
            ObjectPlacementChanged?.Invoke(item, previous, destination);
            return new InventoryTransferResult(InventoryResultCode.Success, itemIdentity, previous, destination);
        }

        public ItemCreationResult CreateItem(int prototypeNumber, ObjectPlacement destination)
        {
            if (_resolvePrototype == null)
                return new ItemCreationResult(InventoryResultCode.PrototypeSourceUnavailable);
            ObjectProtoInfo prototype = _resolvePrototype(prototypeNumber);
            if (prototype == null) return new ItemCreationResult(InventoryResultCode.PrototypeNotFound);
            if (!IsItemType(prototype.Type)) return new ItemCreationResult(InventoryResultCode.InvalidItemType);
            InventoryResultCode validation = ValidateDestination(default, destination);
            if (validation != InventoryResultCode.Success) return new ItemCreationResult(validation);

            ArcanumObjectId identity;
            do
            {
                if (_nextDynamicIdentity == 0) return new ItemCreationResult(InventoryResultCode.IdentityExhausted);
                identity = ArcanumObjectId.CreateSessionDynamic(_nextDynamicIdentity++);
            } while (_states.ContainsKey(identity) || PlayerState != null && PlayerState.Identity == identity);

            string creationSector = destination.Kind == ObjectPlacementKind.World
                ? destination.Sector : SelectedSector ?? PlayerState?.Sector;
            var state = new PersistentObjectState(prototype, identity, creationSector, destination);
            _states.Add(identity, state);
            ObjectPlacementChanged?.Invoke(state, default, destination);
            return new ItemCreationResult(InventoryResultCode.Success, state);
        }

        private InventoryResultCode ValidateDestination(ArcanumObjectId child, ObjectPlacement destination)
        {
            if (destination.Kind == ObjectPlacementKind.World)
                return string.IsNullOrEmpty(destination.Sector)
                    ? InventoryResultCode.InvalidDestination : InventoryResultCode.Success;
            if (destination.Kind != ObjectPlacementKind.Contained || !destination.ParentIdentity.IsPersistent)
                return InventoryResultCode.InvalidDestination;
            if (child.IsPersistent && child == destination.ParentIdentity) return InventoryResultCode.SelfParent;
            if (!TryResolveKnownType(destination.ParentIdentity,
                    new Dictionary<ArcanumObjectId, ObjectType>(), out ObjectType parentType))
                return InventoryResultCode.ParentNotFound;
            if (!IsInventoryOwnerType(parentType)) return InventoryResultCode.InvalidParentType;

            var visited = new HashSet<ArcanumObjectId>();
            if (child.IsPersistent) visited.Add(child);
            ArcanumObjectId ancestor = destination.ParentIdentity;
            while (ancestor.IsPersistent)
            {
                if (!visited.Add(ancestor)) return InventoryResultCode.CycleDetected;
                if (!_states.TryGetValue(ancestor, out PersistentObjectState state)
                    || state.Placement.Kind != ObjectPlacementKind.Contained)
                    break;
                ancestor = state.Placement.ParentIdentity;
            }
            return InventoryResultCode.Success;
        }

        public bool UnbindPresentation(string sector, ArcanumObjectId identity)
        {
            if (sector == null || !_loaded.TryGetValue(sector, out Dictionary<ArcanumObjectId, LoadedBinding> bindings)
                || !bindings.TryGetValue(identity, out LoadedBinding binding)) return false;
            Portals.Unbind(identity);
            if (binding.Runtime != null)
            {
                binding.ObjectState?.Capture(binding.Runtime);
                binding.PlayerState?.Capture(binding.Runtime);
                binding.Runtime.Session = null;
            }
            bindings.Remove(identity);
            return true;
        }

        private static bool IsItemType(ObjectType type)
            => type >= ObjectType.Weapon && type <= ObjectType.Generic;

        private static bool IsInventoryOwnerType(ObjectType type)
            => type == ObjectType.Container || type == ObjectType.Pc || type == ObjectType.Npc;

        public bool TryGetLoadedObject(ArcanumObjectId identity, out WorldObject runtime)
        {
            foreach (Dictionary<ArcanumObjectId, LoadedBinding> sector in _loaded.Values)
                if (sector.TryGetValue(identity, out LoadedBinding binding) && binding.Runtime != null)
                {
                    runtime = binding.Runtime;
                    return true;
                }
            runtime = null;
            return false;
        }

        /// <summary>Validates and commits one gameplay interaction; presentation never decides the result.</summary>
        public WorldInteractionResult ExecuteInteraction(WorldInteractionCommand command)
        {
            if (PlayerState == null || PlayerState.Identity != command.Actor)
                return new WorldInteractionResult(command, WorldInteractionResultCode.ActorNotFound);
            if (!_states.TryGetValue(command.Target, out PersistentObjectState targetState) || targetState.Off
                || !TryGetLoadedObject(command.Target, out WorldObject target))
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
            if (command.Type != WorldInteractionCommandType.Use)
                return new WorldInteractionResult(command, WorldInteractionResultCode.Unsupported);
            if (targetState.Type != ObjectType.Portal || target.Type != ObjectType.Portal)
                return new WorldInteractionResult(command, WorldInteractionResultCode.InvalidTarget);
            if (!SectorCoordinate.TryParse(targetState.SourceSector, out SectorCoordinate targetSector))
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
            Vector2 targetPosition = targetSector.ToGlobal(targetState.TilePosition);
            if (!InteractionRangeRules.IsWithin(PlayerState.MapPosition, targetPosition,
                    InteractionRangeRules.PortalUseRange))
                return new WorldInteractionResult(command, WorldInteractionResultCode.OutOfRange);
            int scriptNum = targetState.UseScriptNum;
            ScriptExecutionResult scriptResult = default;
            if (targetState.UseScriptNum != 0)
            {
                if (UseScripts == null)
                    return new WorldInteractionResult(command, WorldInteractionResultCode.ScriptUnavailable,
                        scriptNum: scriptNum);
                scriptResult = UseScripts.DispatchUse(command.Actor, command.Target, scriptNum);
                if (!scriptResult.Succeeded)
                {
                    WorldInteractionResultCode failure = scriptResult.Status switch
                    {
                        ScriptExecutionStatus.MissingScript => WorldInteractionResultCode.ScriptMissing,
                        ScriptExecutionStatus.UnsupportedOpcode or ScriptExecutionStatus.EmptyScript
                            => WorldInteractionResultCode.ScriptUnsupported,
                        _ => WorldInteractionResultCode.ScriptFailed,
                    };
                    return new WorldInteractionResult(command, failure, scriptNum: scriptNum,
                        scriptStatus: scriptResult.Status, scriptRunDefault: false);
                }
                if (!scriptResult.RunDefault)
                    return new WorldInteractionResult(command, WorldInteractionResultCode.Success,
                        scriptNum: scriptNum, scriptStatus: scriptResult.Status, scriptRunDefault: false);
            }
            // Key/lock resolution is outside M2A; conservatively preserve the closed authoritative state.
            if (targetState.Locked)
                return new WorldInteractionResult(command, WorldInteractionResultCode.Blocked,
                    scriptNum: scriptNum, scriptStatus: ScriptStatus(scriptNum, scriptResult),
                    scriptRunDefault: ScriptDefault(scriptNum, scriptResult));
            if (!Portals.TryGetPhase(command.Target, out PortalPhase phase)
                || phase == PortalPhase.Opening || phase == PortalPhase.Closing)
                return new WorldInteractionResult(command, WorldInteractionResultCode.Blocked,
                    scriptNum: scriptNum, scriptStatus: ScriptStatus(scriptNum, scriptResult),
                    scriptRunDefault: ScriptDefault(scriptNum, scriptResult));

            bool open = phase == PortalPhase.Closed;
            return Portals.Request(command.Target, open)
                ? new WorldInteractionResult(command, WorldInteractionResultCode.Success, open, scriptNum,
                    ScriptStatus(scriptNum, scriptResult), ScriptDefault(scriptNum, scriptResult))
                : new WorldInteractionResult(command, WorldInteractionResultCode.Blocked, scriptNum: scriptNum,
                    scriptStatus: ScriptStatus(scriptNum, scriptResult), scriptRunDefault: ScriptDefault(scriptNum, scriptResult));
        }

        private static ScriptExecutionStatus? ScriptStatus(int scriptNum, ScriptExecutionResult result)
            => scriptNum != 0 ? result.Status : null;

        private static bool? ScriptDefault(int scriptNum, ScriptExecutionResult result)
            => scriptNum != 0 ? result.RunDefault : null;

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
