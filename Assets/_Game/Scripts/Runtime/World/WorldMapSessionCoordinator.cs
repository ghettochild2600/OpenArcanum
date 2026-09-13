using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Runtime.Character;
using Arcanum.Script;
using Arcanum.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Authoritative in-memory map/sector selection and persistent world/player state.</summary>
    public sealed class WorldMapSessionCoordinator : MonoBehaviour, ISectorSelectionAuthority
    {
        private readonly Dictionary<ArcanumObjectId, PersistentObjectState> _states = new();
        private readonly HashSet<ArcanumObjectId> _removedObjectIdentities = new();
        private readonly Dictionary<string, Dictionary<ArcanumObjectId, LoadedBinding>> _loaded = new();
        [SerializeField] private MonoBehaviour terrainSectorOwner;
        [SerializeField] private MonoBehaviour objectSectorOwner;
        [SerializeField] private string initialSector;
        [SerializeField] private bool selectOnStart = true;
        private ISectorPresentationOwner _terrainOwner;
        private ISectorPresentationOwner _objectOwner;
        private Func<int, ObjectProtoInfo> _resolvePrototype;
        private Func<uint?, InventoryFootprint> _resolveInventoryFootprint;
        private InventoryCapacityService _inventoryCapacity;
        private CharacterVitalityService _vitality;
        private CharacterProgressionService _progression;
        private CharacterDerivedStatService _derivedStats;
        private ulong _nextDynamicIdentity = 1;

        public IReadOnlyDictionary<ArcanumObjectId, PersistentObjectState> States => _states;
        public int LoadedSectorCount => _loaded.Count;
        public string CurrentMap { get; private set; }
        public string SelectedSector { get; private set; }
        public bool HasSelectedSector => !string.IsNullOrEmpty(SelectedSector);
        public PersistentPlayerState PlayerState { get; private set; }
        public CharacterStatService Characters { get; } = new();
        public CharacterProgressionService Progression
            => _progression ??= new CharacterProgressionService(Characters);
        public CharacterVitalityService Vitality
            => _vitality ??= new CharacterVitalityService(Characters, Progression);
        public CharacterDerivedStatService DerivedStats
            => _derivedStats ??= new CharacterDerivedStatService(Characters, Progression, InventoryCapacity);
        public InventoryCapacityService InventoryCapacity
            => _inventoryCapacity ??= new InventoryCapacityService(this);
        public PortalTransitionScheduler Portals { get; } = new();
        public ScriptGlobals ScriptGlobals { get; } = new();
        public WorldUseScriptDispatcher UseScripts { get; private set; }
        public event Action<string> SectorUnloading;
        public event Action<string> SectorSelected;
        public event Action<PersistentObjectState, ObjectPlacement, ObjectPlacement> ObjectPlacementChanged;
        public event Action<PersistentObjectState, int, int> ObjectQuantityChanged;
        public event Action<PersistentObjectState, ObjectPlacement> ObjectStateRemoved;

        public const int MaxStackQuantity = int.MaxValue;

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

        public void BindInventoryFootprintSource(Func<uint?, InventoryFootprint> resolveInventoryFootprint)
            => _resolveInventoryFootprint = resolveInventoryFootprint
                ?? throw new ArgumentNullException(nameof(resolveInventoryFootprint));

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
                if (_removedObjectIdentities.Contains(identity)) continue;
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

            var occupiedWornLocations = new Dictionary<(ArcanumObjectId, WornLocation), ArcanumObjectId>();
            foreach (PersistentObjectState state in _states.Values)
                if (state.Placement.Kind == ObjectPlacementKind.Equipped)
                    occupiedWornLocations[(state.ParentIdentity, state.Placement.WornLocation)] = state.Identity;
            foreach (ObjectInstance source in sources)
            {
                ArcanumObjectId identity = identities[source];
                if (!identity.IsPersistent) continue;
                ObjectPlacement placement = _states.TryGetValue(identity, out PersistentObjectState retained)
                    ? retained.Placement
                    : source.ParentIdentity.IsPersistent
                      && WornLocations.TryFromSource(source.InvLocation, out WornLocation location)
                        ? ObjectPlacement.EquippedBy(source.ParentIdentity, location)
                        : default;
                if (placement.Kind != ObjectPlacementKind.Equipped) continue;
                var key = (placement.ParentIdentity, placement.WornLocation);
                if (occupiedWornLocations.TryGetValue(key, out ArcanumObjectId occupant) && occupant != identity)
                {
                    error = $"Equipment location {(int)placement.WornLocation} for {placement.ParentIdentity} "
                            + $"is occupied by both {occupant} and {identity}.";
                    return false;
                }
                occupiedWornLocations[key] = identity;
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
            bool locked,
            int itemFlags = 0,
            uint? inventoryArtId = null,
            int weaponFlags = 0,
            int genericFlags = 0,
            int? stackQuantity = null,
            int? unitWeight = null,
            InventoryFootprint? inventoryFootprint = null,
            int? inventoryLocation = null)
        {
            if (!identity.IsPersistent) return null;
            if (_removedObjectIdentities.Contains(identity)) return null;
            if (_states.TryGetValue(identity, out var existing))
            {
                if (!existing.Matches(source, sector)) throw new InvalidOperationException($"ObjectID collision: {identity}");
                return existing;
            }
            var state = new PersistentObjectState(source, identity, sector, artId, off, locked, itemFlags,
                inventoryArtId, weaponFlags, genericFlags, stackQuantity, unitWeight, inventoryFootprint,
                inventoryLocation);
            _states.Add(identity, state);
            return state;
        }

        public PersistentObjectState GetOrCreate(ObjectInstance source, string sector, uint artId, bool off, bool locked,
            int itemFlags = 0, uint? inventoryArtId = null, int weaponFlags = 0, int genericFlags = 0,
            int? stackQuantity = null, int? unitWeight = null, InventoryFootprint? inventoryFootprint = null,
            int? inventoryLocation = null)
            => GetOrCreate(source, source.Identity, sector, artId, off, locked, itemFlags, inventoryArtId,
                weaponFlags, genericFlags, stackQuantity, unitWeight, inventoryFootprint, inventoryLocation);

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
            if (PlayerState != null && PlayerState.Identity != identity)
                throw new InvalidOperationException("A different production player is already registered.");
            Characters.GetOrCreateDevelopmentPlayer(identity);
            Progression.GetOrCreateDevelopmentPlayer(identity);
            DerivedStats.GetOrCreateDevelopmentPlayer(identity);
            Vitality.GetOrCreateDevelopmentPlayer(identity);
            if (PlayerState == null)
                PlayerState = new PersistentPlayerState(identity, normalized, spawnTile, artId);
            else
            {
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

        public bool IsObjectRemoved(ArcanumObjectId identity) => _removedObjectIdentities.Contains(identity);

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

        /// <summary>Returns the exact item occupying one source worn location, independent of presentation.</summary>
        public bool TryGetEquippedItem(ArcanumObjectId owner, WornLocation location,
            out PersistentObjectState item)
        {
            item = null;
            if (!WornLocations.IsValid(location)) return false;
            foreach (PersistentObjectState candidate in _states.Values)
                if (candidate.Placement.Kind == ObjectPlacementKind.Equipped
                    && candidate.Placement.ParentIdentity == owner
                    && candidate.Placement.WornLocation == location)
                {
                    if (item == null || string.CompareOrdinal(candidate.Identity.Key, item.Identity.Key) < 0)
                        item = candidate;
                }
            return item != null;
        }

        /// <summary>Returns the source worn location currently carried by an item.</summary>
        public bool TryGetWornLocation(ArcanumObjectId itemIdentity, out WornLocation location)
        {
            if (_states.TryGetValue(itemIdentity, out PersistentObjectState item)
                && item.Placement.Kind == ObjectPlacementKind.Equipped)
            {
                location = item.Placement.WornLocation;
                return true;
            }
            location = default;
            return false;
        }

        /// <summary>Enumerates equipment deterministically by source location, then stable ObjectID.</summary>
        public IReadOnlyList<PersistentObjectState> EquippedItems(ArcanumObjectId owner)
        {
            var equipped = new List<PersistentObjectState>();
            foreach (PersistentObjectState state in _states.Values)
                if (state.Placement.Kind == ObjectPlacementKind.Equipped
                    && state.Placement.ParentIdentity == owner)
                    equipped.Add(state);
            equipped.Sort((left, right) =>
            {
                int byLocation = left.Placement.WornLocation.CompareTo(right.Placement.WornLocation);
                return byLocation != 0 ? byLocation : string.CompareOrdinal(left.Identity.Key, right.Identity.Key);
            });
            return equipped;
        }

        /// <summary>
        /// Atomically equips an already-owned item. On replacement both placements are committed before observers run.
        /// </summary>
        public EquipmentTransactionResult EquipItem(ArcanumObjectId actorIdentity,
            ArcanumObjectId itemIdentity, WornLocation location)
        {
            EquipmentResultCode ownerValidation = ValidateEquipmentOwner(actorIdentity);
            if (ownerValidation != EquipmentResultCode.Success)
                return EquipmentFailure(ownerValidation, actorIdentity, itemIdentity, location);
            if (!_states.TryGetValue(itemIdentity, out PersistentObjectState item))
                return EquipmentFailure(EquipmentResultCode.ItemNotFound, actorIdentity, itemIdentity, location);
            if (!IsItemType(item.Type))
                return EquipmentFailure(EquipmentResultCode.InvalidItemType, actorIdentity, itemIdentity, location);
            if (!WornLocations.IsValid(location))
                return EquipmentFailure(EquipmentResultCode.InvalidWornLocation, actorIdentity, itemIdentity, location);
            if (item.Placement.Kind == ObjectPlacementKind.Equipped
                && item.Placement.ParentIdentity == actorIdentity
                && item.Placement.WornLocation == location)
                return EquipmentFailure(EquipmentResultCode.AlreadyEquipped, actorIdentity, itemIdentity, location);
            if (item.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped)
                || item.Placement.ParentIdentity != actorIdentity)
                return EquipmentFailure(EquipmentResultCode.ItemNotOwned, actorIdentity, itemIdentity, location);
            if (!IsCompatibleWith(item, location))
                return EquipmentFailure(EquipmentResultCode.IncompatibleWornLocation, actorIdentity, itemIdentity,
                    location);
            if ((item.ItemFlags & 0x20) != 0)
                return EquipmentFailure(EquipmentResultCode.NotRemovable, actorIdentity, itemIdentity, location);
            if (ConflictsWithHands(actorIdentity, item, location))
                return EquipmentFailure(EquipmentResultCode.NoFreeHand, actorIdentity, itemIdentity, location);

            TryGetEquippedItem(actorIdentity, location, out PersistentObjectState displaced);
            if (displaced != null && displaced.Identity != itemIdentity && (displaced.ItemFlags & 0x20) != 0)
                return EquipmentFailure(EquipmentResultCode.NotRemovable, actorIdentity, itemIdentity, location,
                    displaced.Identity);

            ObjectPlacement itemPrevious = item.Placement;
            ObjectPlacement itemDestination = ObjectPlacement.EquippedBy(actorIdentity, location);
            ObjectPlacement displacedPrevious = default;
            ObjectPlacement displacedDestination = default;
            int displacedInventoryLocation = -1;
            if (displaced != null && displaced.Identity != itemIdentity)
            {
                displacedPrevious = displaced.Placement;
                displacedDestination = ObjectPlacement.ContainedBy(actorIdentity);
                ArcanumObjectId excluded = itemPrevious.Kind == ObjectPlacementKind.Contained
                    ? item.Identity : default;
                displacedInventoryLocation = InventoryCapacity.FindInventoryLocation(actorIdentity,
                    displaced.InventoryFootprint, excluded);
                if (displacedInventoryLocation < 0)
                    return EquipmentFailure(EquipmentResultCode.NoRoom, actorIdentity, itemIdentity, location,
                        displaced.Identity);
            }

            // Commit the complete transaction before callbacks. An observer of either event sees the final pair.
            item.Placement = itemDestination;
            item.InventoryLocation = (int)location;
            if (displaced != null && displaced.Identity != itemIdentity)
            {
                displaced.Placement = displacedDestination;
                displaced.InventoryLocation = displacedInventoryLocation;
            }
            if (displaced != null && displaced.Identity != itemIdentity)
                ObjectPlacementChanged?.Invoke(displaced, displacedPrevious, displacedDestination);
            ObjectPlacementChanged?.Invoke(item, itemPrevious, itemDestination);
            return new EquipmentTransactionResult(EquipmentResultCode.Success, actorIdentity, itemIdentity, location,
                displaced?.Identity ?? default);
        }

        /// <summary>Atomically returns the item in a source worn location to ordinary containment.</summary>
        public EquipmentTransactionResult UnequipItem(ArcanumObjectId actorIdentity, WornLocation location)
        {
            EquipmentResultCode ownerValidation = ValidateEquipmentOwner(actorIdentity);
            if (ownerValidation != EquipmentResultCode.Success)
                return EquipmentFailure(ownerValidation, actorIdentity, default, location);
            if (!WornLocations.IsValid(location))
                return EquipmentFailure(EquipmentResultCode.InvalidWornLocation, actorIdentity, default, location);
            if (!TryGetEquippedItem(actorIdentity, location, out PersistentObjectState item))
                return EquipmentFailure(EquipmentResultCode.SlotEmpty, actorIdentity, default, location);
            if ((item.ItemFlags & 0x20) != 0)
                return EquipmentFailure(EquipmentResultCode.NotRemovable, actorIdentity, item.Identity, location);

            ObjectPlacement previous = item.Placement;
            ObjectPlacement destination = ObjectPlacement.ContainedBy(actorIdentity);
            int inventoryLocation = InventoryCapacity.FindInventoryLocation(actorIdentity, item.InventoryFootprint);
            if (inventoryLocation < 0)
                return EquipmentFailure(EquipmentResultCode.NoRoom, actorIdentity, item.Identity, location);
            item.Placement = destination;
            item.InventoryLocation = inventoryLocation;
            ObjectPlacementChanged?.Invoke(item, previous, destination);
            return new EquipmentTransactionResult(EquipmentResultCode.Success, actorIdentity, item.Identity, location);
        }

        private EquipmentResultCode ValidateEquipmentOwner(ArcanumObjectId actorIdentity)
        {
            if (!TryResolveKnownType(actorIdentity, new Dictionary<ArcanumObjectId, ObjectType>(),
                    out ObjectType actorType))
                return EquipmentResultCode.ActorNotFound;
            return actorType is ObjectType.Pc or ObjectType.Npc
                ? EquipmentResultCode.Success : EquipmentResultCode.InvalidActorType;
        }

        private static EquipmentTransactionResult EquipmentFailure(EquipmentResultCode code,
            ArcanumObjectId actorIdentity, ArcanumObjectId itemIdentity, WornLocation location,
            ArcanumObjectId displacedIdentity = default)
            => new(code, actorIdentity, itemIdentity, location, displacedIdentity);

        private bool ConflictsWithHands(ArcanumObjectId actorIdentity, PersistentObjectState item,
            WornLocation location)
        {
            const int fixedTwoHanded = 0x0004 | 0x0008;
            if (location == WornLocation.Weapon && (item.WeaponFlags & fixedTwoHanded) == fixedTwoHanded)
                return TryGetEquippedItem(actorIdentity, WornLocation.Shield, out _);
            if (location == WornLocation.Shield
                && TryGetEquippedItem(actorIdentity, WornLocation.Weapon, out PersistentObjectState weapon))
                return (weapon.WeaponFlags & fixedTwoHanded) == fixedTwoHanded;
            return false;
        }

        private static bool IsCompatibleWith(PersistentObjectState item, WornLocation location)
        {
            if (!TryGetNaturalWornLocation(item, out WornLocation natural)) return false;
            return natural == WornLocation.Ring1
                ? location is WornLocation.Ring1 or WornLocation.Ring2
                : natural == location;
        }

        /// <summary>Decodes the source slot family from item type, inventory ART coverage, and generic flags.</summary>
        public static bool TryGetNaturalWornLocation(PersistentObjectState item, out WornLocation location)
        {
            if (item != null && item.Type == ObjectType.Weapon)
            {
                location = WornLocation.Weapon;
                return true;
            }
            if (item != null && item.Type == ObjectType.Generic && (item.GenericFlags & 0x0001) != 0)
            {
                location = WornLocation.Shield;
                return true;
            }
            if (item == null || item.Type != ObjectType.Armor || !item.InventoryArtId.HasValue)
            {
                location = default;
                return false;
            }

            location = ((item.InventoryArtId.Value >> 14) & 0x7) switch
            {
                0 => WornLocation.Armor,
                1 => WornLocation.Shield,
                2 => WornLocation.Helmet,
                3 => WornLocation.Gauntlet,
                4 => WornLocation.Boots,
                5 => WornLocation.Ring1,
                6 => WornLocation.Medallion,
                _ => default,
            };
            return WornLocations.IsValid(location);
        }

        /// <summary>Source stack compatibility: positive Gold/Ammo quantities and the exact same prototype.</summary>
        public bool CanStack(ArcanumObjectId leftIdentity, ArcanumObjectId rightIdentity)
            => leftIdentity != rightIdentity
               && _states.TryGetValue(leftIdentity, out PersistentObjectState left)
               && _states.TryGetValue(rightIdentity, out PersistentObjectState right)
               && CanStack(left, right);

        public static bool CanStack(PersistentObjectState left, PersistentObjectState right)
            => left != null && right != null && left.Identity != right.Identity
               && left.StackQuantity is > 0 && right.StackQuantity is > 0
               && left.Type is ObjectType.Ammo or ObjectType.Gold
               && right.Type == left.Type
               && right.PrototypeNumber == left.PrototypeNumber;

        /// <summary>
        /// Moves a requested quantity into a same-owner ordinary inventory stack. The destination identity survives.
        /// </summary>
        public StackMergeResult MergeStacks(ArcanumObjectId sourceIdentity,
            ArcanumObjectId destinationIdentity, int? requestedQuantity = null)
        {
            if (!_states.TryGetValue(sourceIdentity, out PersistentObjectState source))
                return StackMergeFailure(StackResultCode.SourceNotFound, sourceIdentity, destinationIdentity);
            if (!_states.TryGetValue(destinationIdentity, out PersistentObjectState destination))
                return StackMergeFailure(StackResultCode.DestinationNotFound, sourceIdentity, destinationIdentity);
            if (sourceIdentity == destinationIdentity)
                return StackMergeFailure(StackResultCode.SameObject, sourceIdentity, destinationIdentity);
            if (!source.StackQuantity.HasValue || !destination.StackQuantity.HasValue)
                return StackMergeFailure(StackResultCode.NotStackable, sourceIdentity, destinationIdentity);
            if (!CanStack(source, destination))
                return StackMergeFailure(StackResultCode.Incompatible, sourceIdentity, destinationIdentity);
            if (source.Placement.Kind != ObjectPlacementKind.Contained
                || destination.Placement.Kind != ObjectPlacementKind.Contained
                || source.Placement.ParentIdentity != destination.Placement.ParentIdentity)
                return StackMergeFailure(StackResultCode.InvalidPlacement, sourceIdentity, destinationIdentity);

            int quantity = requestedQuantity ?? source.StackQuantity.Value;
            if (quantity < 1 || quantity > source.StackQuantity.Value)
                return StackMergeFailure(StackResultCode.InvalidQuantity, sourceIdentity, destinationIdentity);
            if ((long)destination.StackQuantity.Value + quantity > MaxStackQuantity)
                return StackMergeFailure(StackResultCode.QuantityOverflow, sourceIdentity, destinationIdentity);
            if (!InventoryCapacity.CanMergeWithinOwner(source, destination, quantity))
                return StackMergeFailure(StackResultCode.TooHeavy, sourceIdentity, destinationIdentity);
            return CommitStackMerge(source, destination, quantity);
        }

        /// <summary>Splits an ordinary contained Gold/Ammo stack while preserving its identity.</summary>
        public StackSplitResult SplitStack(ArcanumObjectId sourceIdentity, int quantity)
        {
            if (!_states.TryGetValue(sourceIdentity, out PersistentObjectState source))
                return new StackSplitResult(StackResultCode.SourceNotFound, sourceIdentity);
            if (!source.StackQuantity.HasValue)
                return new StackSplitResult(StackResultCode.NotStackable, sourceIdentity);
            if (source.Placement.Kind != ObjectPlacementKind.Contained)
                return new StackSplitResult(StackResultCode.InvalidPlacement, sourceIdentity,
                    source.StackQuantity.Value);
            if (quantity < 1 || quantity >= source.StackQuantity.Value)
                return new StackSplitResult(StackResultCode.InvalidQuantity, sourceIdentity,
                    source.StackQuantity.Value);
            InventoryAcceptance acceptance = InventoryCapacity.EvaluateSplit(source, quantity);
            if (!acceptance.Succeeded)
                return new StackSplitResult(acceptance.Code == InventoryResultCode.TooHeavy
                        ? StackResultCode.TooHeavy : StackResultCode.NoRoom,
                    sourceIdentity, source.StackQuantity.Value);
            if (!TryAllocateDynamicIdentity(out ArcanumObjectId identity))
                return new StackSplitResult(StackResultCode.IdentityExhausted, sourceIdentity,
                    source.StackQuantity.Value);

            int previousQuantity = source.StackQuantity.Value;
            int remainingQuantity = previousQuantity - quantity;
            var created = new PersistentObjectState(source, identity, source.Placement, quantity,
                acceptance.InventoryLocation);

            // Commit both authoritative states before observers can inspect the transaction.
            source.StackQuantity = remainingQuantity;
            _states.Add(identity, created);
            ObjectQuantityChanged?.Invoke(source, previousQuantity, remainingQuantity);
            ObjectPlacementChanged?.Invoke(created, default, created.Placement);
            return new StackSplitResult(StackResultCode.Success, sourceIdentity, remainingQuantity, created);
        }

        private StackMergeResult CommitStackMerge(PersistentObjectState source,
            PersistentObjectState destination, int quantity)
        {
            int sourcePrevious = source.StackQuantity.Value;
            int destinationPrevious = destination.StackQuantity.Value;
            int sourceRemaining = sourcePrevious - quantity;
            int destinationQuantity = destinationPrevious + quantity;
            ObjectPlacement sourcePlacement = source.Placement;

            destination.StackQuantity = destinationQuantity;
            if (sourceRemaining == 0)
            {
                _states.Remove(source.Identity);
                _removedObjectIdentities.Add(source.Identity);
            }
            else source.StackQuantity = sourceRemaining;

            // All mutations precede callbacks: observers see the final quantity table and tombstone.
            ObjectQuantityChanged?.Invoke(destination, destinationPrevious, destinationQuantity);
            if (sourceRemaining == 0) ObjectStateRemoved?.Invoke(source, sourcePlacement);
            else ObjectQuantityChanged?.Invoke(source, sourcePrevious, sourceRemaining);
            return new StackMergeResult(StackResultCode.Success, source.Identity, destination.Identity, quantity,
                sourceRemaining, destinationQuantity, sourceRemaining == 0);
        }

        private static StackMergeResult StackMergeFailure(StackResultCode code,
            ArcanumObjectId sourceIdentity, ArcanumObjectId destinationIdentity)
            => new(code, sourceIdentity, destinationIdentity);

        private PersistentObjectState FindCompatibleContainedStack(PersistentObjectState source,
            ArcanumObjectId parentIdentity)
        {
            PersistentObjectState match = null;
            foreach (PersistentObjectState candidate in _states.Values)
                if (candidate.Identity != source.Identity
                    && candidate.Placement.Kind == ObjectPlacementKind.Contained
                    && candidate.Placement.ParentIdentity == parentIdentity
                    && CanStack(source, candidate)
                    && (match == null || string.CompareOrdinal(candidate.Identity.Key, match.Identity.Key) < 0))
                    match = candidate;
            return match;
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
            if (previous.Kind == ObjectPlacementKind.Equipped)
                return new InventoryTransferResult(InventoryResultCode.EquipmentCommandRequired, itemIdentity, previous,
                    destination);
            InventoryResultCode validation = ValidateDestination(itemIdentity, destination);
            if (validation != InventoryResultCode.Success)
                return new InventoryTransferResult(validation, itemIdentity, previous, destination);
            if (previous == destination)
                return new InventoryTransferResult(InventoryResultCode.AlreadyAtDestination, itemIdentity, previous, destination);

            PersistentObjectState existing = null;
            if (destination.Kind == ObjectPlacementKind.Contained && item.StackQuantity.HasValue)
            {
                existing = FindCompatibleContainedStack(item, destination.ParentIdentity);
                if (existing != null)
                {
                    if ((long)existing.StackQuantity.Value + item.StackQuantity.Value > MaxStackQuantity)
                        return new InventoryTransferResult(InventoryResultCode.QuantityOverflow, itemIdentity, previous,
                            destination, existing.Identity);
                }
            }

            InventoryAcceptance acceptance = destination.Kind == ObjectPlacementKind.Contained
                ? InventoryCapacity.Evaluate(item, destination.ParentIdentity, existing)
                : InventoryAcceptance.Accept(-1);
            if (!acceptance.Succeeded)
                return new InventoryTransferResult(acceptance.Code, itemIdentity, previous, destination,
                    existing?.Identity ?? default);
            if (existing != null)
            {
                CommitStackMerge(item, existing, item.StackQuantity.Value);
                return new InventoryTransferResult(InventoryResultCode.Success, itemIdentity, previous,
                    destination, existing.Identity, true);
            }

            item.Placement = destination;
            item.InventoryLocation = acceptance.InventoryLocation;
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

            InventoryFootprint footprint = _resolveInventoryFootprint?.Invoke(prototype.InvAid)
                                           ?? InventoryFootprint.OneCell;
            InventoryAcceptance acceptance = destination.Kind == ObjectPlacementKind.Contained
                ? InventoryCapacity.EvaluatePrototype(prototype, footprint, destination.ParentIdentity)
                : InventoryAcceptance.Accept(-1);
            if (!acceptance.Succeeded) return new ItemCreationResult(acceptance.Code);

            if (!TryAllocateDynamicIdentity(out ArcanumObjectId identity))
                return new ItemCreationResult(InventoryResultCode.IdentityExhausted);

            string creationSector = destination.Kind == ObjectPlacementKind.World
                ? destination.Sector : SelectedSector ?? PlayerState?.Sector;
            var state = new PersistentObjectState(prototype, identity, creationSector, destination, footprint,
                acceptance.InventoryLocation);
            _states.Add(identity, state);
            ObjectPlacementChanged?.Invoke(state, default, destination);
            return new ItemCreationResult(InventoryResultCode.Success, state);
        }

        private bool TryAllocateDynamicIdentity(out ArcanumObjectId identity)
        {
            do
            {
                if (_nextDynamicIdentity == 0)
                {
                    identity = default;
                    return false;
                }
                identity = ArcanumObjectId.CreateSessionDynamic(_nextDynamicIdentity++);
            } while (_states.ContainsKey(identity) || _removedObjectIdentities.Contains(identity)
                     || PlayerState != null && PlayerState.Identity == identity);
            return true;
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
                    || state.Placement.Kind == ObjectPlacementKind.World)
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

        public static bool IsItemType(ObjectType type)
            => type >= ObjectType.Weapon && type <= ObjectType.Generic;

        public static bool IsInventoryOwnerType(ObjectType type)
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
            return command.Type switch
            {
                WorldInteractionCommandType.Use => ExecuteUse(command),
                WorldInteractionCommandType.PickUp => ExecutePickUp(command),
                WorldInteractionCommandType.Drop => ExecuteDrop(command),
                WorldInteractionCommandType.Transfer => ExecuteOwnerTransfer(command),
                _ => new WorldInteractionResult(command, WorldInteractionResultCode.Unsupported),
            };
        }

        private WorldInteractionResult ExecuteUse(WorldInteractionCommand command)
        {
            if (!_states.TryGetValue(command.Target, out PersistentObjectState targetState) || targetState.Off
                || !TryGetLoadedObject(command.Target, out WorldObject target))
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
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

        private WorldInteractionResult ExecutePickUp(WorldInteractionCommand command)
        {
            if (!_states.TryGetValue(command.Target, out PersistentObjectState item))
                return new WorldInteractionResult(command, WorldInteractionResultCode.ItemNotFound);
            if (!IsItemType(item.Type))
                return new WorldInteractionResult(command, WorldInteractionResultCode.InvalidItem);
            if (item.Placement.Kind != ObjectPlacementKind.World)
                return new WorldInteractionResult(command, WorldInteractionResultCode.AlreadyContained);
            if (item.Off || item.Placement.Kind != ObjectPlacementKind.World
                || item.Placement.Sector != SelectedSector || PlayerState.Sector != SelectedSector
                || !TryGetLoadedObject(command.Target, out WorldObject runtime)
                || runtime.Type != item.Type)
                return new WorldInteractionResult(command, WorldInteractionResultCode.ItemNotInWorld);
            if (!SectorCoordinate.TryParse(item.Placement.Sector, out SectorCoordinate sector))
                return new WorldInteractionResult(command, WorldInteractionResultCode.ItemNotInWorld);
            Vector2 targetPosition = sector.ToGlobal(item.Placement.TilePosition);
            if (!InteractionRangeRules.IsWithin(PlayerState.MapPosition, targetPosition,
                    InteractionRangeRules.ItemPickupRange))
                return new WorldInteractionResult(command, WorldInteractionResultCode.OutOfRange);

            return FromInventoryTransfer(command, TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(command.Actor)));
        }

        private WorldInteractionResult ExecuteDrop(WorldInteractionCommand command)
        {
            if (!_states.TryGetValue(command.Target, out PersistentObjectState item))
                return new WorldInteractionResult(command, WorldInteractionResultCode.ItemNotFound);
            if (!IsItemType(item.Type))
                return new WorldInteractionResult(command, WorldInteractionResultCode.InvalidItem);
            if (item.Placement.Kind != ObjectPlacementKind.Contained
                || item.Placement.ParentIdentity != command.Actor)
                return new WorldInteractionResult(command, WorldInteractionResultCode.SourceOwnerMismatch);
            if ((item.ItemFlags & 0x00000020) != 0)
                return new WorldInteractionResult(command, WorldInteractionResultCode.NotDroppable);
            if (!command.InventoryDestination.HasValue
                || !IsValidActiveWorldDestination(command.InventoryDestination.Value))
                return new WorldInteractionResult(command, WorldInteractionResultCode.InvalidDestination);
            return FromInventoryTransfer(command, TransferItem(item.Identity, item.Placement,
                command.InventoryDestination.Value));
        }

        private WorldInteractionResult ExecuteOwnerTransfer(WorldInteractionCommand command)
        {
            if (!_states.TryGetValue(command.Target, out PersistentObjectState item))
                return new WorldInteractionResult(command, WorldInteractionResultCode.ItemNotFound);
            if (!IsItemType(item.Type))
                return new WorldInteractionResult(command, WorldInteractionResultCode.InvalidItem);
            if (item.Placement.Kind != ObjectPlacementKind.Contained
                || !command.InventoryDestination.HasValue
                || command.InventoryDestination.Value.Kind != ObjectPlacementKind.Contained)
                return new WorldInteractionResult(command, WorldInteractionResultCode.SourceOwnerMismatch);
            ObjectPlacement destination = command.InventoryDestination.Value;
            if (item.Placement.ParentIdentity != command.Actor
                && destination.ParentIdentity != command.Actor)
                return new WorldInteractionResult(command, WorldInteractionResultCode.SourceOwnerMismatch);
            return FromInventoryTransfer(command, TransferItem(item.Identity, item.Placement, destination));
        }

        private bool IsValidActiveWorldDestination(ObjectPlacement destination)
            => destination.Kind == ObjectPlacementKind.World
               && destination.Sector == SelectedSector
               && PlayerState != null && PlayerState.Sector == SelectedSector
               && destination.TilePosition.x >= 0 && destination.TilePosition.x < SectorCoordinate.Size
               && destination.TilePosition.y >= 0 && destination.TilePosition.y < SectorCoordinate.Size
               && Mathf.Approximately(destination.TilePosition.x, Mathf.Round(destination.TilePosition.x))
               && Mathf.Approximately(destination.TilePosition.y, Mathf.Round(destination.TilePosition.y));

        private static WorldInteractionResult FromInventoryTransfer(WorldInteractionCommand command,
            InventoryTransferResult transfer)
        {
            if (transfer.Succeeded)
                return new WorldInteractionResult(command, WorldInteractionResultCode.Success,
                    inventoryStatus: transfer.Code);
            WorldInteractionResultCode code = transfer.Code switch
            {
                InventoryResultCode.ItemNotFound => WorldInteractionResultCode.ItemNotFound,
                InventoryResultCode.InvalidItemType => WorldInteractionResultCode.InvalidItem,
                InventoryResultCode.InvalidDestination or InventoryResultCode.ParentNotFound
                    or InventoryResultCode.InvalidParentType or InventoryResultCode.SelfParent
                    or InventoryResultCode.CycleDetected => WorldInteractionResultCode.InvalidDestination,
                InventoryResultCode.AlreadyAtDestination => WorldInteractionResultCode.AlreadyContained,
                InventoryResultCode.SourceMismatch => WorldInteractionResultCode.SourceOwnerMismatch,
                InventoryResultCode.TooHeavy => WorldInteractionResultCode.TooHeavy,
                InventoryResultCode.NoRoom => WorldInteractionResultCode.NoRoom,
                _ => WorldInteractionResultCode.TransferFailed,
            };
            return new WorldInteractionResult(command, code, inventoryStatus: transfer.Code);
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
