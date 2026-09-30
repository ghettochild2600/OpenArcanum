using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Formats.World;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Dialogue;
using Arcanum.Formats.Quest;
using Arcanum.Script;
using Arcanum.World;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.Combat;
using Arcanum.Runtime.Party;
using Arcanum.Runtime.Magic;
using Arcanum.Runtime.Technology;
using Arcanum.Runtime.Economy;
using Arcanum.Runtime.Social;
using Arcanum.Runtime.Crafting;
using Arcanum.Runtime.Creation;
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
        private CharacterStatService _characters;
        private InventoryCapacityService _inventoryCapacity;
        private CharacterVitalityService _vitality;
        private CharacterProgressionService _progression;
        private CharacterDerivedStatService _derivedStats;
        private ProductionDialogueSession _dialogue;
        private JournalProjectionService _journal;
        private QuestLog _questSource;
        private PortalTransitionScheduler _portals;
        private CampaignStateService _campaign;
        private SessionSaveService _saveGames;
        private SessionSaveSlotService _saveSlots;
        private Func<int, ScriptFile> _resolveUseScript;
        private Func<int, ScriptFile> _resolveDialogueScript;
        private Func<int, Arcanum.Formats.Dialog.DialogScript> _resolveDialogue;
        private Func<ArcanumObjectId, char, string> _resolveGeneratedDialogueText;
        private ITrainingDialogueTextSource _trainingDialogueText;
        private MapTransitionResolver _mapTransitions;
        private AreaEntranceResolver _areaEntrances;
        private AreaList _areaSource;
        private WorldMapDestinationProjection _worldMapDestinations;
        private WorldMapTravelSource _worldMapTravelSource;
        private WorldMapTravelService _worldMapTravel;
        private CombatStateService _combat;
        private DeathConsequenceService _deathConsequences;
        private PartyStateService _party;
        private MagicStateService _magic;
        private TechnologyStateService _technology;
        private CraftingStateService _crafting;
        private SchematicCatalog _schematicSource;
        private SourceTimeService _sourceTime;
        private EconomyStateService _economy;
        private InventorySourceCatalog _economySource;
        private SocialStateService _social;
        private ReputationCatalog _reputationSource;
        private SocialAiCatalog _socialAiSource;
        private CharacterCreationCatalog _characterCreationSource;
        private CharacterCreationStateService _characterCreation;
        private bool _mapTransitionActive;
        private ulong _nextDynamicIdentity = 1;

        public IReadOnlyDictionary<ArcanumObjectId, PersistentObjectState> States => _states;
        public int LoadedSectorCount => _loaded.Count;
        public string CurrentMap { get; private set; }
        public string SelectedSector { get; private set; }
        public bool HasSelectedSector => !string.IsNullOrEmpty(SelectedSector);
        public PersistentPlayerState PlayerState { get; private set; }
        public CharacterStatService Characters => _characters ??= new CharacterStatService();
        public CharacterProgressionService Progression
            => _progression ??= new CharacterProgressionService(Characters);
        public CharacterVitalityService Vitality
            => _vitality ??= new CharacterVitalityService(Characters, Progression);
        public CharacterDerivedStatService DerivedStats
            => _derivedStats ??= new CharacterDerivedStatService(Characters, Progression, InventoryCapacity);
        public InventoryCapacityService InventoryCapacity
            => _inventoryCapacity ??= new InventoryCapacityService(this);
        public PortalTransitionScheduler Portals => _portals ??= new PortalTransitionScheduler();
        public CampaignStateService Campaign => _campaign ??= CreateCampaign();
        /// <summary>Compatibility name for the shared script/campaign store used by M2B callers.</summary>
        public CampaignStateService ScriptGlobals => Campaign;
        public ProductionDialogueSession Dialogue => _dialogue ??= CreateDialogue();
        public QuestLog QuestSource => _questSource;
        public JournalProjectionService Journal => _journal ??= CreateJournal();
        public SessionSaveService SaveGames => _saveGames ??= new SessionSaveService(this);
        public SessionSaveSlotService SaveSlots => _saveSlots ??= new SessionSaveSlotService(this);
        public WorldMapDestinationProjection WorldMapDestinations
            => _worldMapDestinations ??= new WorldMapDestinationProjection(_areaSource, Campaign);
        public WorldMapTravelService WorldMapTravel
            => _worldMapTravel ??= new WorldMapTravelService(this, _worldMapTravelSource);
        public CombatStateService Combat => _combat ??= new CombatStateService(this);
        public DeathConsequenceService DeathConsequences
            => _deathConsequences ??= new DeathConsequenceService(this);
        public PartyStateService Party => _party ??= new PartyStateService(this);
        public SourceTimeService SourceTime => _sourceTime ??= new SourceTimeService();
        public MagicStateService Magic => _magic ??= new MagicStateService(this);
        public TechnologyStateService Technology => _technology ??= new TechnologyStateService(this);
        public CraftingStateService Crafting => _crafting ??= CreateCrafting();
        public EconomyStateService Economy => _economy ??= CreateEconomy();
        public SocialStateService Social => _social ??= CreateSocial();
        public CharacterCreationStateService CharacterCreation
            => _characterCreation ??= new CharacterCreationStateService(this, _characterCreationSource);
        internal bool HasMagicCritterFlag(ArcanumObjectId identity, int flag)
            => _magic?.HasActiveCritterFlag(identity, flag) == true;
        public bool IsMapTransitionActive => _mapTransitionActive;
        public MapTransitionResult LastMapTransitionResult { get; private set; }
        public AreaEntranceResult LastAreaEntranceResult { get; private set; }
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

        internal sealed class DialogueInventorySnapshot
        {
            private readonly Dictionary<ArcanumObjectId, ItemValues> _items = new();
            private readonly HashSet<ArcanumObjectId> _removed;
            private readonly ulong _nextDynamicIdentity;

            internal DialogueInventorySnapshot(WorldMapSessionCoordinator session)
            {
                foreach (var pair in session._states)
                    _items.Add(pair.Key, new ItemValues(pair.Value));
                _removed = new HashSet<ArcanumObjectId>(session._removedObjectIdentities);
                _nextDynamicIdentity = session._nextDynamicIdentity;
            }

            internal void Restore(WorldMapSessionCoordinator session)
            {
                var extra = new List<ArcanumObjectId>();
                foreach (ArcanumObjectId identity in session._states.Keys)
                    if (!_items.ContainsKey(identity)) extra.Add(identity);
                foreach (ArcanumObjectId identity in extra) session._states.Remove(identity);
                foreach (var pair in _items)
                {
                    session._states[pair.Key] = pair.Value.State;
                    pair.Value.Restore();
                }
                session._removedObjectIdentities.Clear();
                foreach (ArcanumObjectId identity in _removed) session._removedObjectIdentities.Add(identity);
                session._nextDynamicIdentity = _nextDynamicIdentity;
            }

            private readonly struct ItemValues
            {
                public readonly PersistentObjectState State;
                private readonly ObjectPlacement _placement;
                private readonly int _inventoryLocation;
                private readonly int? _quantity;

                public ItemValues(PersistentObjectState state)
                {
                    State = state;
                    _placement = state.Placement;
                    _inventoryLocation = state.InventoryLocation;
                    _quantity = state.StackQuantity;
                }

                public void Restore()
                {
                    State.Placement = _placement;
                    State.InventoryLocation = _inventoryLocation;
                    State.StackQuantity = _quantity;
                    if (_placement.Kind == ObjectPlacementKind.World) State.TilePosition = _placement.TilePosition;
                }
            }
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

        private void Update()
        {
            SourceTime.PollNonCombat(_combat?.IsActive == true);
            Portals.Tick(Time.deltaTime);
            _dialogue?.ValidateActiveTarget();
        }

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
            BindUseScriptSource(scripts.Get);
        }

        public void BindUseScriptSource(Func<int, ScriptFile> resolveScript)
        {
            _resolveUseScript = resolveScript ?? throw new ArgumentNullException(nameof(resolveScript));
            UseScripts = new WorldUseScriptDispatcher(this, resolveScript, ScriptGlobals);
        }

        public void BindDialogueSource(Func<int, ScriptFile> resolveScript,
            Func<int, Arcanum.Formats.Dialog.DialogScript> resolveDialogue)
        {
            _resolveDialogueScript = resolveScript ?? throw new ArgumentNullException(nameof(resolveScript));
            _resolveDialogue = resolveDialogue ?? throw new ArgumentNullException(nameof(resolveDialogue));
            Dialogue.BindSources(resolveScript, resolveDialogue);
        }

        public void BindGeneratedDialogueText(Func<ArcanumObjectId, char, string> resolveGeneratedText)
        {
            _resolveGeneratedDialogueText = resolveGeneratedText
                ?? throw new ArgumentNullException(nameof(resolveGeneratedText));
            Dialogue.BindGeneratedText(resolveGeneratedText);
        }

        public void BindTrainingDialogueText(ITrainingDialogueTextSource source)
        {
            _trainingDialogueText = source ?? throw new ArgumentNullException(nameof(source));
            Dialogue.BindTrainingDialogueText(source);
        }

        public void BindQuestSource(QuestLog source)
        {
            _questSource = source ?? throw new ArgumentNullException(nameof(source));
            Journal.BindSource(source);
        }

        private ProductionDialogueSession CreateDialogue()
        {
            var dialogue = new ProductionDialogueSession(this, Campaign);
            if (_resolveDialogueScript != null && _resolveDialogue != null)
                dialogue.BindSources(_resolveDialogueScript, _resolveDialogue);
            if (_resolveGeneratedDialogueText != null) dialogue.BindGeneratedText(_resolveGeneratedDialogueText);
            if (_trainingDialogueText != null) dialogue.BindTrainingDialogueText(_trainingDialogueText);
            return dialogue;
        }

        private JournalProjectionService CreateJournal()
        {
            var journal = new JournalProjectionService(Campaign);
            if (_questSource != null) journal.BindSource(_questSource);
            return journal;
        }

        public void BindPrototypeSource(Func<int, ObjectProtoInfo> resolvePrototype)
            => _resolvePrototype = resolvePrototype ?? throw new ArgumentNullException(nameof(resolvePrototype));

        internal ObjectProtoInfo ResolvePrototype(int prototypeNumber)
            => _resolvePrototype?.Invoke(prototypeNumber);

        internal InventoryFootprint ResolveInventoryFootprint(uint? inventoryArtId)
            => _resolveInventoryFootprint?.Invoke(inventoryArtId) ?? InventoryFootprint.OneCell;

        public void BindEconomySource(InventorySourceCatalog source)
        {
            _economySource = source ?? throw new ArgumentNullException(nameof(source));
            Economy.BindInventorySources(source);
        }

        public void BindCraftingSource(SchematicCatalog source)
        {
            _schematicSource = source ?? throw new ArgumentNullException(nameof(source));
            Crafting.BindCatalog(source);
        }

        public void BindSocialSources(ReputationCatalog reputations, SocialAiCatalog ai)
        {
            _reputationSource = reputations ?? throw new ArgumentNullException(nameof(reputations));
            _socialAiSource = ai ?? throw new ArgumentNullException(nameof(ai));
            Social.BindSources(reputations, ai);
        }

        public void BindCharacterCreationSource(CharacterCreationCatalog source)
        {
            _characterCreationSource = source ?? throw new ArgumentNullException(nameof(source));
            CharacterCreation.BindCatalog(source);
        }

        public void BindInventoryFootprintSource(Func<uint?, InventoryFootprint> resolveInventoryFootprint)
            => _resolveInventoryFootprint = resolveInventoryFootprint
                ?? throw new ArgumentNullException(nameof(resolveInventoryFootprint));

        public void BindMapTransitionSource(MapTransitionResolver resolver)
            => _mapTransitions = resolver ?? throw new ArgumentNullException(nameof(resolver));

        public void BindAreaEntranceSource(AreaEntranceResolver resolver)
            => _areaEntrances = resolver ?? throw new ArgumentNullException(nameof(resolver));

        public void BindWorldMapTravelSource(WorldMapTravelSource source)
        {
            _worldMapTravelSource = source ?? throw new ArgumentNullException(nameof(source));
            _worldMapTravel = null;
        }

        public void BindAreaSource(AreaList source)
        {
            _areaSource = source ?? throw new ArgumentNullException(nameof(source));
            Campaign.BindAreaSource(source);
            _worldMapDestinations = null;
        }

        internal AreaList AreaSource => _areaSource;

        private CampaignStateService CreateCampaign()
        {
            var campaign = new CampaignStateService();
            if (_areaSource != null) campaign.BindAreaSource(_areaSource);
            return campaign;
        }

        private EconomyStateService CreateEconomy()
        {
            var economy = new EconomyStateService(this);
            if (_economySource != null) economy.BindInventorySources(_economySource);
            return economy;
        }

        private CraftingStateService CreateCrafting()
        {
            var crafting = new CraftingStateService(this);
            if (_schematicSource != null) crafting.BindCatalog(_schematicSource);
            return crafting;
        }

        private SocialStateService CreateSocial()
        {
            var social = new SocialStateService(this);
            if (_reputationSource != null && _socialAiSource != null)
                social.BindSources(_reputationSource, _socialAiSource);
            return social;
        }

        public bool IsAreaEntranceTarget(ArcanumObjectId identity)
            => _areaEntrances != null && _states.TryGetValue(identity, out PersistentObjectState state)
               && AreaEntranceResolver.IsAdmittedTarget(state);

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
            if (PlayerState != null)
                Combat.RegisterActorSource(new CombatActorSource(PlayerState.Identity, ObjectType.Pc, null,
                    PlayerState.Sector, int.MaxValue, 0, 0, 0));
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
            _combat?.ResetForWorldChange();
            _dialogue?.Cancel("Sector unloaded during dialogue.");
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
            int? inventoryLocation = null,
            int? nameIndex = null,
            int? socialClass = null,
            Weapon weaponData = null,
            int? ammoItemType = null,
            int? sourceWorth = null,
            int? maximumHitPoints = null,
            int? hitPointDamage = null,
            int? retailPriceMultiplier = null,
            int? inventorySourceId = null,
            ArcanumObjectId substituteInventoryIdentity = default,
            int npcFlags = 0,
            int buyObjectScriptNum = 0,
            int containerFlags = 0,
            int? aiData = null,
            int? origin = null,
            int? faction = null,
            int? writtenSubtype = null,
            int? writtenStartLine = null)
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
                inventoryLocation, nameIndex, socialClass, weaponData, ammoItemType, sourceWorth,
                maximumHitPoints, hitPointDamage, retailPriceMultiplier, inventorySourceId,
                substituteInventoryIdentity, npcFlags, buyObjectScriptNum, containerFlags, aiData, origin, faction,
                writtenSubtype, writtenStartLine);
            _states.Add(identity, state);
            return state;
        }

        public PersistentObjectState GetOrCreate(ObjectInstance source, string sector, uint artId, bool off, bool locked,
            int itemFlags = 0, uint? inventoryArtId = null, int weaponFlags = 0, int genericFlags = 0,
            int? stackQuantity = null, int? unitWeight = null, InventoryFootprint? inventoryFootprint = null,
            int? inventoryLocation = null, int? nameIndex = null, int? socialClass = null,
            Weapon weaponData = null, int? ammoItemType = null, int? sourceWorth = null,
            int? maximumHitPoints = null, int? hitPointDamage = null, int? retailPriceMultiplier = null,
            int? inventorySourceId = null, ArcanumObjectId substituteInventoryIdentity = default,
            int npcFlags = 0, int buyObjectScriptNum = 0, int containerFlags = 0,
            int? aiData = null, int? origin = null, int? faction = null,
            int? writtenSubtype = null, int? writtenStartLine = null)
            => GetOrCreate(source, source.Identity, sector, artId, off, locked, itemFlags, inventoryArtId,
                weaponFlags, genericFlags, stackQuantity, unitWeight, inventoryFootprint, inventoryLocation,
                nameIndex, socialClass, weaponData, ammoItemType, sourceWorth, maximumHitPoints,
                hitPointDamage, retailPriceMultiplier, inventorySourceId, substituteInventoryIdentity,
                npcFlags, buyObjectScriptNum, containerFlags, aiData, origin, faction,
                writtenSubtype, writtenStartLine);

        public void Bind(string sector, PersistentObjectState state, WorldObject runtime)
        {
            if (state == null) return;
            _loaded[sector].Add(state.Identity, new LoadedBinding { Runtime = runtime, ObjectState = state });
            state.Restore(runtime);
            runtime.Session = this;
        }

        public PersistentPlayerState GetOrCreatePlayer(
            ArcanumObjectId identity,
            string sector,
            Vector2 spawnTile,
            uint artId)
        {
            if (!identity.IsPersistent) throw new ArgumentException("Player identity must be persistent.", nameof(identity));
            string normalized = NormalizeSector(sector) ?? throw new ArgumentException("Player sector is required.", nameof(sector));
            if (PlayerState != null && PlayerState.Identity == identity)
            {
                PlayerState.EnterSector(normalized, spawnTile, artId);
                Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Pc, null, normalized,
                    int.MaxValue, 0, 0, 0));
                return PlayerState;
            }
            if (_states.ContainsKey(identity)) throw new InvalidOperationException($"Player ObjectID collides with {identity}.");
            if (PlayerState != null && PlayerState.Identity != identity)
                throw new InvalidOperationException("A different production player is already registered.");
            Characters.GetOrCreateDevelopmentPlayer(identity);
            Progression.GetOrCreateDevelopmentPlayer(identity);
            DerivedStats.GetOrCreateDevelopmentPlayer(identity);
            Vitality.GetOrCreateDevelopmentPlayer(identity);
            Magic.RegisterSourceCharacter(identity, new int[17], null);
            Technology.RegisterSourceCharacter(identity, new int[25], null);
            Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Pc, null, normalized,
                int.MaxValue, 0, 0, 0));
            if (PlayerState == null)
                PlayerState = new PersistentPlayerState(identity, normalized, spawnTile, artId);
            else
            {
                PlayerState.EnterSector(normalized, spawnTile, artId);
            }
            return PlayerState;
        }

        internal PersistentPlayerState InitializeCreatedPlayer(ArcanumObjectId identity, string sector,
            Vector2 spawnTile, uint artId, CharacterAttributeSet attributes, CharacterRace race,
            CharacterGender gender, CharacterProgressionSource progression, CharacterDerivedSource derived,
            CharacterVitalitySource vitality, int[] spellTech)
        {
            if (PlayerState != null) throw new InvalidOperationException("A production player is already registered.");
            string normalized = NormalizeSector(sector)
                ?? throw new ArgumentException("Player sector is required.", nameof(sector));
            Characters.GetOrCreateCreatedPlayer(identity, attributes, race, gender);
            Progression.GetOrCreateCreatedPlayer(identity, progression);
            DerivedStats.GetOrCreateCreatedPlayer(identity, derived);
            Vitality.GetOrCreateCreatedPlayer(identity, vitality);
            Magic.RegisterSourceCharacter(identity, spellTech, null);
            Technology.RegisterSourceCharacter(identity, spellTech, null);
            Combat.RegisterActorSource(new CombatActorSource(identity, ObjectType.Pc, null, normalized,
                int.MaxValue, 0, 0, 0));
            PlayerState = new PersistentPlayerState(identity, normalized, spawnTile, artId);
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

        /// <summary>Finds the stable source-shaped ammo stack used by the bounded ranged transaction.</summary>
        public bool TryGetAmmo(ArcanumObjectId owner, int ammoType, int quantity,
            out PersistentObjectState ammo)
        {
            ammo = null;
            if (quantity < 1) return false;
            foreach (PersistentObjectState candidate in _states.Values)
            {
                if (candidate.Type != ObjectType.Ammo || candidate.AmmoItemType != ammoType
                    || candidate.StackQuantity.GetValueOrDefault() < quantity
                    || candidate.Placement.Kind != ObjectPlacementKind.Contained
                    || candidate.Placement.ParentIdentity != owner) continue;
                if (ammo == null || string.CompareOrdinal(candidate.Identity.Key, ammo.Identity.Key) < 0)
                    ammo = candidate;
            }
            return ammo != null;
        }

        /// <summary>Consumes a preflighted ammo stack; zero quantity becomes a persistent tombstone.</summary>
        internal bool ConsumeAmmo(ArcanumObjectId identity, int quantity, out int remaining)
        {
            remaining = 0;
            if (quantity < 1 || !_states.TryGetValue(identity, out PersistentObjectState ammo)
                || ammo.Type != ObjectType.Ammo || ammo.StackQuantity.GetValueOrDefault() < quantity)
                return false;
            int previous = ammo.StackQuantity.Value;
            remaining = previous - quantity;
            if (remaining == 0)
            {
                ObjectPlacement placement = ammo.Placement;
                _states.Remove(identity);
                _removedObjectIdentities.Add(identity);
                ObjectStateRemoved?.Invoke(ammo, placement);
            }
            else
            {
                ammo.StackQuantity = remaining;
                ObjectQuantityChanged?.Invoke(ammo, previous, remaining);
            }
            return true;
        }

        /// <summary>Consumes one preflighted singular inventory item and records its persistent tombstone.</summary>
        internal bool ConsumeSingularItem(ArcanumObjectId identity, ArcanumObjectId owner)
        {
            if (!_states.TryGetValue(identity, out PersistentObjectState item)
                || item.StackQuantity.HasValue
                || item.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped)
                || item.ParentIdentity != owner)
                return false;
            ObjectPlacement placement = item.Placement;
            _states.Remove(identity);
            _removedObjectIdentities.Add(identity);
            ObjectStateRemoved?.Invoke(item, placement);
            return true;
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
        public InventoryTransferResult PreviewTransferItem(ArcanumObjectId itemIdentity, ObjectPlacement source,
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
                return new InventoryTransferResult(InventoryResultCode.AlreadyAtDestination, itemIdentity, previous,
                    destination);

            PersistentObjectState existing = null;
            if (destination.Kind == ObjectPlacementKind.Contained && item.StackQuantity.HasValue)
            {
                existing = FindCompatibleContainedStack(item, destination.ParentIdentity);
                if (existing != null
                    && (long)existing.StackQuantity.Value + item.StackQuantity.Value > MaxStackQuantity)
                    return new InventoryTransferResult(InventoryResultCode.QuantityOverflow, itemIdentity, previous,
                        destination, existing.Identity);
            }
            InventoryAcceptance acceptance = destination.Kind == ObjectPlacementKind.Contained
                ? InventoryCapacity.Evaluate(item, destination.ParentIdentity, existing)
                : InventoryAcceptance.Accept(-1);
            return new InventoryTransferResult(acceptance.Code, itemIdentity, previous, destination,
                existing?.Identity ?? default, existing != null);
        }

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

        /// <summary>
        /// Atomic source-owner transfer used only after the death-consequence service has established a corpse.
        /// Ordinary inventory reuses <see cref="TransferItem"/>; worn items move directly to looter containment.
        /// </summary>
        internal InventoryTransferResult TransferCorpseOwnedItem(ArcanumObjectId itemIdentity,
            ArcanumObjectId corpseIdentity, ArcanumObjectId destinationOwner)
        {
            if (!_states.TryGetValue(itemIdentity, out PersistentObjectState item))
                return new InventoryTransferResult(InventoryResultCode.ItemNotFound, itemIdentity, default,
                    ObjectPlacement.ContainedBy(destinationOwner));
            ObjectPlacement previous = item.Placement;
            ObjectPlacement destination = ObjectPlacement.ContainedBy(destinationOwner);
            if (item.ParentIdentity != corpseIdentity
                || previous.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped))
                return new InventoryTransferResult(InventoryResultCode.SourceMismatch, itemIdentity, previous,
                    destination);
            if (previous.Kind == ObjectPlacementKind.Contained)
                return TransferItem(itemIdentity, previous, destination);
            if ((item.ItemFlags & 0x20) != 0)
                return new InventoryTransferResult(InventoryResultCode.EquipmentCommandRequired, itemIdentity,
                    previous, destination);

            InventoryResultCode validation = ValidateDestination(itemIdentity, destination);
            if (validation != InventoryResultCode.Success)
                return new InventoryTransferResult(validation, itemIdentity, previous, destination);
            InventoryAcceptance acceptance = InventoryCapacity.Evaluate(item, destinationOwner);
            if (!acceptance.Succeeded)
                return new InventoryTransferResult(acceptance.Code, itemIdentity, previous, destination);

            item.Placement = destination;
            item.InventoryLocation = acceptance.InventoryLocation;
            ObjectPlacementChanged?.Invoke(item, previous, destination);
            return new InventoryTransferResult(InventoryResultCode.Success, itemIdentity, previous, destination);
        }

        public bool TryFindContainedItem(ArcanumObjectId ownerIdentity, int prototypeNumber,
            out PersistentObjectState item)
        {
            item = null;
            foreach (PersistentObjectState candidate in _states.Values)
            {
                if (candidate.PrototypeNumber != prototypeNumber
                    || candidate.Placement.Kind != ObjectPlacementKind.Contained
                    || candidate.Placement.ParentIdentity != ownerIdentity) continue;
                if (item == null || string.CompareOrdinal(candidate.Identity.Key, item.Identity.Key) < 0)
                    item = candidate;
            }
            return item != null;
        }

        /// <summary>Source item_find_by_name: resolves contained items by effective OBJ_F_NAME, not prototype.</summary>
        public bool TryFindContainedItemByName(ArcanumObjectId ownerIdentity, int nameIndex,
            out PersistentObjectState item)
        {
            item = null;
            foreach (PersistentObjectState candidate in _states.Values)
            {
                if (candidate.NameIndex != nameIndex
                    || candidate.Placement.Kind != ObjectPlacementKind.Contained
                    || candidate.Placement.ParentIdentity != ownerIdentity) continue;
                if (item == null || string.CompareOrdinal(candidate.Identity.Key, item.Identity.Key) < 0)
                    item = candidate;
            }
            return item != null;
        }

        public int GetGold(ArcanumObjectId ownerIdentity)
        {
            long total = 0;
            foreach (PersistentObjectState item in _states.Values)
                if (item.Type == ObjectType.Gold && item.Placement.Kind == ObjectPlacementKind.Contained
                    && item.Placement.ParentIdentity == ownerIdentity)
                    total += item.StackQuantity.GetValueOrDefault();
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        public bool CanAddGold(ArcanumObjectId ownerIdentity, int amount, out string failure)
        {
            failure = null;
            if (amount <= 0)
            {
                failure = "M5B admits only positive source gold awards.";
                return false;
            }
            if (!TryResolveKnownType(ownerIdentity, new Dictionary<ArcanumObjectId, ObjectType>(), out ObjectType type)
                || type is not ObjectType.Pc and not ObjectType.Npc and not ObjectType.Container)
            {
                failure = "Gold owner is not a known source inventory owner.";
                return false;
            }
            foreach (PersistentObjectState item in _states.Values)
                if (item.Type == ObjectType.Gold && item.Placement.Kind == ObjectPlacementKind.Contained
                    && item.Placement.ParentIdentity == ownerIdentity)
                {
                    if ((long)item.StackQuantity.GetValueOrDefault() + amount > MaxStackQuantity)
                    {
                        failure = "Gold quantity would overflow the source Int32 field.";
                        return false;
                    }
                    return true;
                }
            ObjectProtoInfo prototype = _resolvePrototype?.Invoke(9056);
            if (prototype?.Type != ObjectType.Gold)
            {
                failure = "Source gold prototype 9056 is unavailable.";
                return false;
            }
            InventoryFootprint footprint = _resolveInventoryFootprint?.Invoke(prototype.InvAid)
                                           ?? InventoryFootprint.OneCell;
            InventoryAcceptance acceptance = InventoryCapacity.EvaluatePrototype(prototype, footprint, ownerIdentity);
            if (!acceptance.Succeeded)
            {
                failure = $"Source gold award cannot enter the PC inventory: {acceptance.Code}.";
                return false;
            }
            return true;
        }

        public void AddGold(ArcanumObjectId ownerIdentity, int amount)
        {
            if (!CanAddGold(ownerIdentity, amount, out string failure))
                throw new InvalidOperationException(failure);
            PersistentObjectState existing = null;
            foreach (PersistentObjectState item in _states.Values)
                if (item.Type == ObjectType.Gold && item.Placement.Kind == ObjectPlacementKind.Contained
                    && item.Placement.ParentIdentity == ownerIdentity
                    && (existing == null || string.CompareOrdinal(item.Identity.Key, existing.Identity.Key) < 0))
                    existing = item;
            if (existing != null)
            {
                int previous = existing.StackQuantity.Value;
                existing.StackQuantity = checked(previous + amount);
                ObjectQuantityChanged?.Invoke(existing, previous, existing.StackQuantity.Value);
                return;
            }
            ItemCreationResult created = CreateItem(9056, ObjectPlacement.ContainedBy(ownerIdentity));
            if (!created.Succeeded) throw new InvalidOperationException($"Gold creation failed: {created.Code}.");
            int initial = created.State.StackQuantity.Value;
            created.State.StackQuantity = amount;
            ObjectQuantityChanged?.Invoke(created.State, initial, amount);
        }

        public bool CanTransferGold(ArcanumObjectId sourceOwner, ArcanumObjectId destinationOwner, int amount,
            out string failure)
        {
            failure = null;
            if (amount <= 0)
            {
                failure = "Gold transfer amount must be positive.";
                return false;
            }
            if (sourceOwner == destinationOwner)
            {
                failure = "Gold source and destination must differ.";
                return false;
            }
            if (GetGold(sourceOwner) < amount)
            {
                failure = "The source inventory does not contain enough Gold.";
                return false;
            }
            return CanAddGold(destinationOwner, amount, out failure);
        }

        /// <summary>Atomic source item_gold_transfer-shaped movement through authoritative Gold stacks.</summary>
        public bool TryTransferGold(ArcanumObjectId sourceOwner, ArcanumObjectId destinationOwner, int amount,
            out string failure)
        {
            if (!CanTransferGold(sourceOwner, destinationOwner, amount, out failure)) return false;
            DialogueInventorySnapshot snapshot = CaptureDialogueInventorySnapshot();
            try
            {
                AddGold(destinationOwner, amount);
                int remaining = amount;
                var sourceStacks = _states.Values
                    .Where(item => item.Type == ObjectType.Gold
                                   && item.Placement.Kind == ObjectPlacementKind.Contained
                                   && item.Placement.ParentIdentity == sourceOwner)
                    .OrderBy(item => item.Identity.Key, StringComparer.Ordinal)
                    .ToList();
                foreach (PersistentObjectState stack in sourceStacks)
                {
                    int previous = stack.StackQuantity.Value;
                    int taken = Math.Min(previous, remaining);
                    int quantity = previous - taken;
                    remaining -= taken;
                    if (quantity == 0)
                    {
                        ObjectPlacement placement = stack.Placement;
                        _states.Remove(stack.Identity);
                        _removedObjectIdentities.Add(stack.Identity);
                        ObjectStateRemoved?.Invoke(stack, placement);
                    }
                    else
                    {
                        stack.StackQuantity = quantity;
                        ObjectQuantityChanged?.Invoke(stack, previous, quantity);
                    }
                    if (remaining == 0) break;
                }
                if (remaining != 0) throw new InvalidOperationException("Gold source changed after preflight.");
                failure = null;
                return true;
            }
            catch (Exception ex)
            {
                RestoreDialogueInventorySnapshot(snapshot);
                failure = ex.Message;
                return false;
            }
        }

        internal DialogueInventorySnapshot CaptureDialogueInventorySnapshot() => new(this);
        internal void RestoreDialogueInventorySnapshot(DialogueInventorySnapshot snapshot) => snapshot.Restore(this);

        internal bool RemoveEconomyGeneratedObject(ArcanumObjectId identity, ArcanumObjectId expectedOwner)
        {
            if (!_states.TryGetValue(identity, out PersistentObjectState state)
                || state.ParentIdentity != expectedOwner
                || state.Placement.Kind != ObjectPlacementKind.Contained) return false;
            ObjectPlacement placement = state.Placement;
            _states.Remove(identity);
            _removedObjectIdentities.Add(identity);
            ObjectStateRemoved?.Invoke(state, placement);
            return true;
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
            if (Combat.IsActive)
                return new WorldInteractionResult(command, WorldInteractionResultCode.Blocked);
            return command.Type switch
            {
                WorldInteractionCommandType.Use => ExecuteUse(command),
                WorldInteractionCommandType.PickUp => ExecutePickUp(command),
                WorldInteractionCommandType.Drop => ExecuteDrop(command),
                WorldInteractionCommandType.Transfer => ExecuteOwnerTransfer(command),
                WorldInteractionCommandType.Talk => ExecuteTalk(command),
                WorldInteractionCommandType.Loot => ExecuteLoot(command),
                _ => new WorldInteractionResult(command, WorldInteractionResultCode.Unsupported),
            };
        }

        private WorldInteractionResult ExecuteUse(WorldInteractionCommand command)
        {
            if (!_states.TryGetValue(command.Target, out PersistentObjectState targetState) || targetState.Off
                || !TryGetLoadedObject(command.Target, out WorldObject target))
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
            if (targetState.Type == ObjectType.Scenery && target.Type == ObjectType.Scenery)
            {
                AreaEntranceResult travel = RequestAreaEntrance(command.Actor, command.Target);
                return new WorldInteractionResult(command, travel.Succeeded ? WorldInteractionResultCode.Success
                        : travel.Failure == AreaEntranceFailure.OutOfRange ? WorldInteractionResultCode.OutOfRange
                        : WorldInteractionResultCode.TravelFailed,
                    scriptNum: targetState.UseScriptNum, scriptRunDefault: false, areaEntrance: travel);
            }
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

        private WorldInteractionResult ExecuteTalk(WorldInteractionCommand command)
        {
            if (!_states.TryGetValue(command.Target, out PersistentObjectState targetState) || targetState.Off
                || targetState.Type != ObjectType.Npc
                || !TryGetLoadedObject(command.Target, out WorldObject target) || target.Type != ObjectType.Npc)
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
            if (!SectorCoordinate.TryParse(targetState.Placement.Sector, out SectorCoordinate targetSector))
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
            if (!InteractionRangeRules.IsWithin(PlayerState.MapPosition,
                    targetSector.ToGlobal(targetState.Placement.TilePosition), InteractionRangeRules.TalkStartRange))
                return new WorldInteractionResult(command, WorldInteractionResultCode.OutOfRange);

            DialogueStartStatus status = Dialogue.Start(command.Actor, command.Target);
            WorldInteractionResultCode result = status switch
            {
                DialogueStartStatus.Started => WorldInteractionResultCode.Success,
                DialogueStartStatus.Busy => WorldInteractionResultCode.DialogueBusy,
                DialogueStartStatus.MissingScript or DialogueStartStatus.MissingDialogue
                    => WorldInteractionResultCode.DialogueMissing,
                DialogueStartStatus.UnsupportedScript => WorldInteractionResultCode.DialogueUnsupported,
                DialogueStartStatus.InvalidPc => WorldInteractionResultCode.ActorNotFound,
                DialogueStartStatus.InvalidNpc or DialogueStartStatus.TargetUnavailable
                    => WorldInteractionResultCode.TargetNotFound,
                _ => WorldInteractionResultCode.DialogueFailed,
            };
            return new WorldInteractionResult(command, result, scriptNum: targetState.DialogNum);
        }

        private WorldInteractionResult ExecuteLoot(WorldInteractionCommand command)
        {
            if (!_states.TryGetValue(command.Target, out PersistentObjectState corpse) || corpse.Off
                || corpse.Type != ObjectType.Npc
                || !TryGetLoadedObject(command.Target, out WorldObject runtime) || runtime.Type != ObjectType.Npc)
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
            if (!Vitality.TryGet(command.Target, out _) || !Vitality.IsDead(command.Target))
                return new WorldInteractionResult(command, WorldInteractionResultCode.InvalidTarget);
            if (!SectorCoordinate.TryParse(corpse.Placement.Sector, out SectorCoordinate sector)
                || corpse.Placement.Sector != SelectedSector || PlayerState.Sector != SelectedSector)
                return new WorldInteractionResult(command, WorldInteractionResultCode.TargetNotFound);
            if (!InteractionRangeRules.IsWithin(PlayerState.MapPosition,
                    sector.ToGlobal(corpse.Placement.TilePosition), InteractionRangeRules.CorpseLootRange))
                return new WorldInteractionResult(command, WorldInteractionResultCode.OutOfRange);
            return new WorldInteractionResult(command, WorldInteractionResultCode.Success);
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
                if (state.Placement.Kind == ObjectPlacementKind.World)
                    state.Placement = ObjectPlacement.InWorld(state.Placement.Sector, tilePosition);
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

        /// <summary>Resolves the production PC's exact current tile as one stable passive jump source.</summary>
        public MapTransitionResult RequestCurrentJumpPoint(ArcanumObjectId actor)
        {
            if (_mapTransitions == null)
                return Remember(new MapTransitionResult(MapTransitionFailure.NoSourceData,
                    detail: "Map-transition source data has not been bound."));
            if (PlayerState == null)
                return Remember(new MapTransitionResult(MapTransitionFailure.NoProductionPlayer,
                    detail: "No production PC is registered."));
            if (!SectorCoordinate.TryParse(SelectedSector, out SectorCoordinate selected)
                || !_mapTransitions.TryGetMapId(selected.MapPath, out int mapId))
                return Remember(new MapTransitionResult(MapTransitionFailure.SourceMapMissing,
                    detail: $"Selected map '{selected.MapPath}' is absent from MapList."));
            Vector2 position = PlayerState.MapPosition;
            Vector2Int tile = Vector2Int.RoundToInt(position);
            if (Vector2.SqrMagnitude(position - tile) > 0.0001f)
                return Remember(new MapTransitionResult(MapTransitionFailure.SourceTileMismatch,
                    detail: "The PC has not landed exactly on a source tile."));
            return RequestMapTransition(actor, new MapTransitionSourceId(mapId, tile));
        }

        /// <summary>
        /// Validates one source-authored map transition before teardown, then relocates and re-projects the same PC.
        /// </summary>
        public MapTransitionResult RequestMapTransition(ArcanumObjectId actor, MapTransitionSourceId source)
        {
            if (_mapTransitionActive)
                return Remember(new MapTransitionResult(MapTransitionFailure.Busy,
                    detail: "A map transition is already active."));
            if (PlayerState == null || !HasSelectedSector)
                return Remember(new MapTransitionResult(MapTransitionFailure.NoProductionPlayer,
                    detail: "No selected map has a production PC."));
            if (!actor.IsPersistent || PlayerState.Identity != actor)
                return Remember(new MapTransitionResult(MapTransitionFailure.InvalidActor,
                    detail: "Only the production PC can activate this transition."));
            if (_mapTransitions == null)
                return Remember(new MapTransitionResult(MapTransitionFailure.NoSourceData,
                    detail: "Map-transition source data has not been bound."));
            if (!SectorCoordinate.TryParse(SelectedSector, out SectorCoordinate selected)
                || !_mapTransitions.TryGetMapId(selected.MapPath, out int selectedMapId))
                return Remember(new MapTransitionResult(MapTransitionFailure.SourceMapMissing,
                    detail: $"Selected map '{selected.MapPath}' is absent from MapList."));
            if (source.MapId != selectedMapId)
                return Remember(new MapTransitionResult(MapTransitionFailure.SourceMapMismatch,
                    detail: $"{source} does not belong to selected map id {selectedMapId}."));
            if (Vector2.SqrMagnitude(PlayerState.MapPosition - source.GlobalTile) > 0.0001f)
                return Remember(new MapTransitionResult(MapTransitionFailure.SourceTileMismatch,
                    detail: $"The production PC is not standing on {source}."));

            MapTransitionResult resolved = _mapTransitions.Resolve(source);
            if (!resolved.Succeeded) return Remember(resolved);
            MapTransitionDestination destination = resolved.Destination;
            if (destination.MapId == selectedMapId)
                return Remember(new MapTransitionResult(MapTransitionFailure.UnsupportedDestinationMap, destination,
                    "M7A admits one cross-map jump; remote same-map jumps remain deferred."));

            return ApplyResolvedMapTransition(destination);
        }

        /// <summary>PC-only physical SAP_USE entrance. Discovery/UI/time/encounters are not fabricated.</summary>
        public AreaEntranceResult RequestAreaEntrance(ArcanumObjectId actor, ArcanumObjectId entrance)
        {
            AreaEntranceResult result;
            if (_mapTransitionActive)
                result = new AreaEntranceResult(AreaEntranceFailure.Busy);
            else if (PlayerState == null || PlayerState.Identity != actor || !HasSelectedSector)
                result = new AreaEntranceResult(AreaEntranceFailure.InvalidActor);
            else if (_areaEntrances == null)
                result = new AreaEntranceResult(AreaEntranceFailure.NoSourceData);
            else if (!_states.TryGetValue(entrance, out PersistentObjectState target) || target.Off
                || !TryGetLoadedObject(entrance, out WorldObject runtime) || runtime.Type != target.Type
                || target.Placement.Kind != ObjectPlacementKind.World || target.Placement.Sector != SelectedSector)
                result = new AreaEntranceResult(AreaEntranceFailure.TargetUnavailable);
            else
            {
                result = _areaEntrances.Resolve(target);
                if (result.Succeeded)
                {
                    if (!SectorCoordinate.TryParse(SelectedSector, out SectorCoordinate current)
                        || !SectorCoordinate.TryParse(PlayerState.Sector, out SectorCoordinate playerSector)
                        || playerSector.MapPath != current.MapPath
                        || !InteractionRangeRules.IsWithin(PlayerState.MapPosition, result.Source.Tile,
                            InteractionRangeRules.PortalUseRange))
                        result = new AreaEntranceResult(AreaEntranceFailure.OutOfRange, entrance);
                    else result = result.WithTransition(ApplyResolvedMapTransition(result.Transition.Destination));
                }
            }
            LastAreaEntranceResult = result;
            return result;
        }

        /// <summary>Consumes an M7D intent through the presentation-independent M7E authority.</summary>
        public WorldMapTravelResult RequestWorldMapTravel(ArcanumObjectId actor, WorldMapTravelRequest request)
            => WorldMapTravel.Execute(actor, request);

        internal MapTransitionResult ApplyResolvedWorldMapTravel(MapTransitionDestination destination)
            => ApplyResolvedMapTransition(destination);

        // One shared pipeline for M7A passive jumps and M7B admitted physical entrances.
        private MapTransitionResult ApplyResolvedMapTransition(MapTransitionDestination destination)
        {
            SectorCoordinate.TryParse(SelectedSector, out SectorCoordinate selected);
            string previousSector = SelectedSector;
            string previousMap = selected.MapPath;
            Vector2 previousPosition = PlayerState.MapPosition;
            uint previousArt = PlayerState.ArtId;
            PartyTransitionSnapshot partySnapshot = Party.CaptureTransition();
            int facing = destination.Facing ?? CritterArtResolver.RotationOf(previousArt);
            uint arrivalArt = CritterArtResolver.WithAnimRotation(previousArt, 0, facing) & ~(0x1Fu << 14);

            _mapTransitionActive = true;
            try
            {
                ClearPlayerDestination();
                ClearSelectedSector();
                PlayerState.RestoreMapPosition(destination.MapPath, destination.GlobalTile, arrivalArt);
                if (SelectSector(destination.Sector.Path))
                {
                    Party.RelocateEligibleFollowers(destination.Sector.Path, destination.LocalTile);
                    return Remember(new MapTransitionResult(MapTransitionFailure.None, destination));
                }

                PlayerState.RestoreMapPosition(previousMap, previousPosition, previousArt);
                if (SelectSector(previousSector))
                {
                    Party.RestoreTransition(partySnapshot);
                    return Remember(new MapTransitionResult(MapTransitionFailure.PresentationFailed, destination,
                        $"Destination '{destination.Sector.Path}' could not be presented; the source map was restored."));
                }
                return Remember(new MapTransitionResult(MapTransitionFailure.RollbackFailed, destination,
                    $"Destination '{destination.Sector.Path}' and source rollback '{previousSector}' both failed."));
            }
            finally
            {
                _mapTransitionActive = false;
            }
        }

        private MapTransitionResult Remember(MapTransitionResult result)
        {
            LastMapTransitionResult = result;
            return result;
        }

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
            PartyTransitionSnapshot partySnapshot = Party.CaptureTransition();
            ClearSelectedSector();
            PlayerState.Relocate(target, entryTile, artId);
            if (SelectSector(target))
            {
                Party.RelocateEligibleFollowers(target, entryTile);
                return true;
            }

            PlayerState.RestoreMapPosition(currentCoordinate.MapPath, previousMapPosition, previousArtId);
            SelectSector(previousSector);
            Party.RestoreTransition(partySnapshot);
            return false;
        }

        /// <summary>Moves one persistent world object through session placement authority.</summary>
        internal bool RelocateWorldObject(ArcanumObjectId identity, string sector, Vector2 tile)
        {
            string normalized = NormalizeSector(sector);
            if (normalized == null || !_states.TryGetValue(identity, out PersistentObjectState state)
                || state.Placement.Kind != ObjectPlacementKind.World) return false;
            ObjectPlacement previous = state.Placement;
            ObjectPlacement destination = ObjectPlacement.InWorld(normalized, tile);
            if (previous == destination) return true;
            state.Placement = destination;
            state.TilePosition = tile;
            ObjectPlacementChanged?.Invoke(state, previous, destination);
            return true;
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

        internal ulong NextDynamicIdentity => _nextDynamicIdentity;
        internal IReadOnlyCollection<ArcanumObjectId> RemovedObjectIdentities => _removedObjectIdentities;

        internal void CaptureLoadedStateForSave()
        {
            foreach (Dictionary<ArcanumObjectId, LoadedBinding> sector in _loaded.Values)
                foreach (LoadedBinding binding in sector.Values)
                    if (binding.Runtime != null)
                    {
                        binding.ObjectState?.Capture(binding.Runtime);
                        binding.PlayerState?.Capture(binding.Runtime);
                    }
        }

        /// <summary>
        /// Unloads presentation and replaces every authoritative runtime root with an empty session.
        /// Data/configuration sources and registered presentation owners remain bound so a validated save can load.
        /// </summary>
        public void ResetAuthoritativeSession()
        {
            ClearSelectedSector();
            foreach (string sector in new List<string>(_loaded.Keys)) UnloadSector(sector);

            _states.Clear();
            _removedObjectIdentities.Clear();
            _nextDynamicIdentity = 1;
            PlayerState = null;
            _characters = null;
            _progression = null;
            _vitality = null;
            _inventoryCapacity = null;
            _derivedStats = null;
            _campaign = null;
            _worldMapDestinations = null;
            _worldMapTravel = null;
            _combat = null;
            _deathConsequences = null;
            _party = null;
            _magic = null;
            _technology = null;
            _crafting = null;
            _economy = null;
            _social = null;
            _characterCreation?.ClearFinalized();
            _sourceTime = null;
            _portals = null;
            _dialogue = null;
            _journal = null;
            UseScripts = _resolveUseScript == null
                ? null
                : new WorldUseScriptDispatcher(this, _resolveUseScript, Campaign);
            CurrentMap = null;
            SelectedSector = null;
        }

        internal bool ApplyRestorePlan(SessionRestorePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            ClearSelectedSector();
            foreach (string sector in new List<string>(_loaded.Keys)) UnloadSector(sector);

            _states.Clear();
            foreach (var pair in plan.Objects) _states.Add(pair.Key, pair.Value);
            _removedObjectIdentities.Clear();
            foreach (ArcanumObjectId identity in plan.Tombstones) _removedObjectIdentities.Add(identity);
            _nextDynamicIdentity = plan.NextDynamicIdentity;
            PlayerState = plan.Player;

            _characters = plan.Characters;
            _progression = plan.Progression;
            _vitality = plan.Vitality;
            _inventoryCapacity = plan.InventoryCapacity;
            _derivedStats = plan.DerivedStats;
            _campaign = plan.Campaign;
            _party = new PartyStateService(this);
            _magic = null;
            _technology = null;
            _crafting = null;
            _economy = null;
            _social = null;
            _sourceTime = new SourceTimeService();
            _sourceTime.Restore(plan.Magic?.ElapsedMilliseconds ?? 0);
            foreach (PartyMember member in plan.PartyMembers)
                if (!_party.TryAddRestored(member, out string partyError))
                    throw new InvalidOperationException(partyError);
            if (_areaSource != null) _campaign.BindAreaSource(_areaSource);
            _worldMapDestinations = null;
            _worldMapTravel = null;
            _combat = null;
            _portals = new PortalTransitionScheduler();
            _dialogue = null;
            _journal = null;
            UseScripts = _resolveUseScript == null
                ? null
                : new WorldUseScriptDispatcher(this, _resolveUseScript, Campaign);
            CurrentMap = null;
            SelectedSector = null;
            Combat.RegisterActorSource(new CombatActorSource(PlayerState.Identity, ObjectType.Pc, null,
                PlayerState.Sector, int.MaxValue, 0, 0, 0));
            _magic = new MagicStateService(this);
            _magic.RestoreSaveData(plan.Magic);
            _technology = new TechnologyStateService(this);
            _technology.RestoreSaveData(plan.Technology);
            _crafting = CreateCrafting();
            _crafting.RestoreSaveData(plan.Crafting);
            _economy = CreateEconomy();
            _economy.RestoreSaveData(plan.Economy);
            _social = CreateSocial();
            _social.RestoreSaveData(plan.Social);
            CharacterCreation.RestoreSaveData(plan.CharacterCreation);
            return SelectSector(plan.SelectedSector);
        }

        private void OnDestroy()
        {
            foreach (string sector in new List<string>(_loaded.Keys)) UnloadSector(sector);
        }
    }
}
