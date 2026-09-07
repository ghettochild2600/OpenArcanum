using System;
using System.Collections.Generic;
using Arcanum.Formats;
using Arcanum.Formats.Art;
using Arcanum.Formats.Database;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// Production owner for the ordinary SpriteRenderer object layer of one loaded sector.
    /// It consumes real sector instances, resolves inherited prototype ART ids, and creates
    /// runtime <see cref="WorldObject"/> presentations. Terrain remains a separate owner.
    /// </summary>
    public sealed class WorldObjectSectorLoader : MonoBehaviour
    {
        private const string ObjectRootName = "WorldObjects";
        private const uint PortalWallIdentityMask = (0x1FFu << 19) | (7u << 11);

        public enum RenderIssueCategory
        {
            MissingPrototype,
            ZeroArtIdentity,
            UnsupportedArtType,
            ResolverMiss,
            MissingArtFile,
            SpriteBuildFailure,
        }

        public sealed class RenderIssue
        {
            public RenderIssueCategory Category { get; }
            public ObjectType ObjectType { get; }
            public int PrototypeNumber { get; }
            public uint ArtId { get; }
            public Vector2Int Tile { get; }
            public string ResolvedPath { get; }
            public string Detail { get; }

            internal RenderIssue(
                RenderIssueCategory category,
                ObjectInstance instance,
                uint artId,
                string resolvedPath,
                string detail)
            {
                Category = category;
                ObjectType = instance.Type;
                PrototypeNumber = instance.PrototypeNumber;
                ArtId = artId;
                Tile = new Vector2Int(instance.TileX, instance.TileY);
                ResolvedPath = resolvedPath;
                Detail = detail;
            }

            public override string ToString()
                => $"category={Category}, objectType={ObjectType}, proto={PrototypeNumber}, " +
                   $"artId=0x{ArtId:X8}, artType={ArtId >> 28}, tile={Tile}, " +
                   $"path='{ResolvedPath ?? "<none>"}', detail='{Detail}'.";
        }

        private const int ObjectFlagDestroyed = 0x00000001;
        private const int ObjectFlagOff = 0x00000002;
        private const int ObjectFlagDontDraw = 0x00000100;
        private const int ObjectFlagInventory = 0x00001000;

        [Header("Source archives")]
        [SerializeField]
        private string moduleArchive = "modules/Arcanum.dat";

        [SerializeField]
        private string[] artArchives = { "arcanum1.dat", "arcanum2.dat", "arcanum3.dat", "arcanum4.dat" };

        [SerializeField]
        private string sectorPath = "maps/arcanum1-024-fixed/101602821844.sec";

        [Header("Presentation")]
        [SerializeField]
        private float pixelsPerUnit = 100f;

        [SerializeField]
        private bool loadOnStart = true;

        private readonly List<WorldObjectSpriteOwner> _spriteOwners = new List<WorldObjectSpriteOwner>();
        private readonly List<RenderIssue> _renderIssues = new List<RenderIssue>();
        private DatVirtualFileSystem _vfs;
        private ProtoLibrary _prototypes;
        private ObjectArtResolvers _art;
        private Transform _objectRoot;
        [SerializeField] private WorldMapSessionCoordinator session;
        private string _registeredSector;
        public WorldMapSessionCoordinator Session => session != null ? session
            : session = GetComponent<WorldMapSessionCoordinator>() ?? gameObject.AddComponent<WorldMapSessionCoordinator>();
        public int LastNonPersistentIdentityCount { get; private set; }

        public string CurrentSector => sectorPath;
        public IReadOnlyList<WorldObjectSpriteOwner> SpriteOwners => _spriteOwners;
        public IReadOnlyList<RenderIssue> RenderIssues => _renderIssues;
        public int RenderedObjectCount => _spriteOwners.Count;
        public int LastRecordCount { get; private set; }
        public int LastInventoryCount { get; private set; }
        public int LastSuppressedCount { get; private set; }
        public int LastDerivedPortalCount { get; private set; }
        public bool IsLoaded => _objectRoot != null && _objectRoot.gameObject.activeSelf;

        private void Start()
        {
            if (loadOnStart) LoadSector(sectorPath);
        }

        public bool LoadSector(string path)
        {
            if (!string.IsNullOrWhiteSpace(path)) sectorPath = path;
            if (!EnsureData()) return false;
            if (!_vfs.Exists(sectorPath))
            {
                Debug.LogError($"WorldObjectSectorLoader: sector '{sectorPath}' was not found.", this);
                return false;
            }

            List<ObjectInstance> instances;
            try
            {
                instances = SectorReader.ReadObjects(_vfs.ReadAllBytes(sectorPath));
                instances.AddRange(ReadSectorMobiles(sectorPath));
            }
            catch (Exception ex)
            {
                Debug.LogError($"WorldObjectSectorLoader: object read failed for '{sectorPath}': {ex.Message}", this);
                return false;
            }

            sectorPath = sectorPath.Replace('\\', '/').ToLowerInvariant();
            if (!Session.ValidateSector(sectorPath, instances, out string identityError))
            {
                Debug.LogError($"WorldObjectSectorLoader: {identityError}", this);
                return false;
            }
            ClearObjects();
            Session.BeginSector(sectorPath);
            _registeredSector = sectorPath;
            LastNonPersistentIdentityCount = 0;
            _renderIssues.Clear();
            LastRecordCount = instances.Count;
            var root = new GameObject(ObjectRootName);
            root.transform.SetParent(transform, false);
            _objectRoot = root.transform;
            Dictionary<Vector2Int, List<PortalWallCandidate>> portalWalls = BuildPortalWallContext(instances);

            int inherited = 0;
            int suppressed = 0;
            int inventory = 0;
            int unresolvedProto = 0;
            int unresolvedArt = 0;
            int derivedPortals = 0;
            var byType = new Dictionary<ObjectType, int>();

            foreach (ObjectInstance instance in instances)
            {
                if (!instance.Identity.IsPersistent) LastNonPersistentIdentityCount++;
                if (!instance.Location.HasValue) continue;

                ObjectProtoInfo proto = _prototypes?.Get(instance.PrototypeNumber);
                uint artId;
                if (instance.CurrentArtId.HasValue) artId = instance.CurrentArtId.Value;
                else if (proto != null)
                {
                    artId = proto.CurrentArtId;
                    inherited++;
                }
                else
                {
                    unresolvedProto++;
                    AddIssue(
                        RenderIssueCategory.MissingPrototype,
                        instance,
                        0,
                        null,
                        "The instance has no ART override and its prototype could not be loaded.");
                    continue;
                }

                int flags = instance.Flags ?? proto?.Flags ?? 0;
                string artPath = _art.Resolve(artId);
                if (string.IsNullOrEmpty(artPath)
                    && instance.Type == ObjectType.Portal
                    && TryResolvePortalFromWall(instance, artId, portalWalls, out uint derivedArtId, out string derivedPath))
                {
                    artId = derivedArtId;
                    artPath = derivedPath;
                    derivedPortals++;
                }
                int stateFlags = instance.Type == ObjectType.Portal
                    ? instance.PortalFlags ?? proto?.PortalFlags ?? 0
                    : instance.Type == ObjectType.Container ? instance.ContainerFlags ?? proto?.ContainerFlags ?? 0 : 0;
                PersistentObjectState state = Session.GetOrCreate(instance, sectorPath, artId,
                    (flags & ObjectFlagOff) != 0, (stateFlags & 1) != 0);
                if (state != null)
                {
                    artId = state.ArtId;
                    artPath = _art.Resolve(artId);
                    flags = state.Off ? flags | ObjectFlagOff : flags & ~ObjectFlagOff;
                }
                if (instance.IsInInventory || (flags & ObjectFlagInventory) != 0)
                {
                    inventory++;
                    continue;
                }

                if ((flags & (ObjectFlagDestroyed | ObjectFlagOff | ObjectFlagDontDraw)) != 0)
                {
                    suppressed++;
                    continue;
                }

                if (artId == 0)
                {
                    unresolvedArt++;
                    AddIssue(
                        RenderIssueCategory.ZeroArtIdentity,
                        instance,
                        artId,
                        null,
                        "The effective current ART id is zero.");
                    continue;
                }

                if (!_art.Supports(ArtId.Type(artId)))
                {
                    unresolvedArt++;
                    AddIssue(
                        RenderIssueCategory.UnsupportedArtType,
                        instance,
                        artId,
                        null,
                        "No ordinary world-object ART resolver owns this ART type.");
                    continue;
                }

                if (string.IsNullOrEmpty(artPath))
                {
                    unresolvedArt++;
                    AddIssue(
                        RenderIssueCategory.ResolverMiss,
                        instance,
                        artId,
                        null,
                        "The type-specific resolver found no source path for this ART identity.");
                    continue;
                }

                if (!_vfs.Exists(artPath))
                {
                    unresolvedArt++;
                    AddIssue(
                        RenderIssueCategory.MissingArtFile,
                        instance,
                        artId,
                        artPath,
                        "The resolver produced a path that is absent from the mounted data.");
                    continue;
                }

                WorldObject worldObject = CreateWorldObject(instance, artId, artPath, proto, state);
                if (worldObject == null)
                {
                    unresolvedArt++;
                    AddIssue(
                        RenderIssueCategory.SpriteBuildFailure,
                        instance,
                        artId,
                        artPath,
                        "The ART source resolved and existed, but no SpriteRenderer presentation was built.");
                    continue;
                }

                if (!byType.ContainsKey(instance.Type)) byType[instance.Type] = 0;
                byType[instance.Type]++;
            }

            LastInventoryCount = inventory;
            LastSuppressedCount = suppressed;
            LastDerivedPortalCount = derivedPortals;

            var typeSummary = new List<string>();
            foreach (KeyValuePair<ObjectType, int> pair in byType)
                typeSummary.Add($"{pair.Key}={pair.Value}");
            typeSummary.Sort(StringComparer.Ordinal);

            var issueSummary = new List<string>();
            var issuesByCategory = new Dictionary<RenderIssueCategory, int>();
            foreach (RenderIssue issue in _renderIssues)
            {
                if (!issuesByCategory.ContainsKey(issue.Category)) issuesByCategory[issue.Category] = 0;
                issuesByCategory[issue.Category]++;
            }
            foreach (KeyValuePair<RenderIssueCategory, int> pair in issuesByCategory)
                issueSummary.Add($"{pair.Key}={pair.Value}");
            issueSummary.Sort(StringComparer.Ordinal);

            Debug.Log(
                $"WorldObjectSectorLoader: rendered {_spriteOwners.Count}/{instances.Count} placed object(s) from " +
                 $"'{sectorPath}' (inheritedArt={inherited}, suppressed={suppressed}, inventory={inventory}, " +
                 $"derivedPortals={derivedPortals}, unresolvedProto={unresolvedProto}, " +
                 $"unresolvedArt={unresolvedArt}, issues=[{string.Join(", ", issueSummary)}]; " +
                $"{string.Join(", ", typeSummary)}).",
                this);
            return true;
        }

        private Dictionary<Vector2Int, List<PortalWallCandidate>> BuildPortalWallContext(
            IReadOnlyList<ObjectInstance> instances)
        {
            var byTile = new Dictionary<Vector2Int, List<PortalWallCandidate>>();
            foreach (ObjectInstance instance in instances)
            {
                if (instance.Type != ObjectType.Wall || !instance.Location.HasValue) continue;

                ObjectProtoInfo proto = _prototypes?.Get(instance.PrototypeNumber);
                uint wallArtId = instance.CurrentArtId ?? proto?.CurrentArtId ?? 0;
                if (wallArtId == 0 || ArtId.Type(wallArtId) != ArtId.TypeWall) continue;

                string wallPath = _art.Resolve(wallArtId);
                uint? derivedArtId = _art.DerivePortalFromWall(wallArtId, wallPath);
                if (!derivedArtId.HasValue) continue;

                string portalPath = _art.Resolve(derivedArtId.Value);
                if (string.IsNullOrEmpty(portalPath)) continue;

                var tile = new Vector2Int(instance.TileX, instance.TileY);
                if (!byTile.TryGetValue(tile, out List<PortalWallCandidate> candidates))
                {
                    candidates = new List<PortalWallCandidate>();
                    byTile.Add(tile, candidates);
                }
                candidates.Add(new PortalWallCandidate(derivedArtId.Value, portalPath));
            }
            return byTile;
        }

        private static bool TryResolvePortalFromWall(
            ObjectInstance portal,
            uint storedArtId,
            IReadOnlyDictionary<Vector2Int, List<PortalWallCandidate>> byTile,
            out uint derivedArtId,
            out string derivedPath)
        {
            derivedArtId = 0;
            derivedPath = null;
            var tile = new Vector2Int(portal.TileX, portal.TileY);
            if (!byTile.TryGetValue(tile, out List<PortalWallCandidate> candidates)) return false;

            bool found = false;
            foreach (PortalWallCandidate candidate in candidates)
            {
                // The shipped generic portal identity still carries the correct portal number and
                // rotation. Requiring both prevents a same-tile wall from being chosen by proximity.
                if ((candidate.ArtId & PortalWallIdentityMask) != (storedArtId & PortalWallIdentityMask))
                    continue;

                if (found && (candidate.ArtId != derivedArtId
                              || !string.Equals(candidate.Path, derivedPath, StringComparison.OrdinalIgnoreCase)))
                    return false;

                derivedArtId = candidate.ArtId;
                derivedPath = candidate.Path;
                found = true;
            }
            return found;
        }

        private readonly struct PortalWallCandidate
        {
            public uint ArtId { get; }
            public string Path { get; }

            public PortalWallCandidate(uint artId, string path)
            {
                ArtId = artId;
                Path = path;
            }
        }

        private void AddIssue(
            RenderIssueCategory category,
            ObjectInstance instance,
            uint artId,
            string resolvedPath,
            string detail)
            => _renderIssues.Add(new RenderIssue(category, instance, artId, resolvedPath, detail));

        /// <summary>Recreates every owned presentation from retained source identity.</summary>
        public void RebuildVisuals()
        {
            int rebuilt = 0;
            foreach (WorldObjectSpriteOwner owner in _spriteOwners)
                if (owner != null && owner.Rebuild()) rebuilt++;
            Debug.Log($"WorldObjectSectorLoader: rebuilt {rebuilt}/{_spriteOwners.Count} visual owner(s).", this);
        }

        private WorldObject CreateWorldObject(
            ObjectInstance instance,
            uint artId,
            string artPath,
            ObjectProtoInfo proto,
            PersistentObjectState state)
        {
            var root = new GameObject($"{instance.Type}_{instance.PrototypeNumber}_{instance.TileX}_{instance.TileY}");
            root.transform.SetParent(_objectRoot, false);
            root.transform.localPosition = IsoProjection.TileToWorld(instance.TileX, instance.TileY, pixelsPerUnit);

            var worldObject = root.AddComponent<WorldObject>();
            worldObject.Type = instance.Type;
            worldObject.Tile = new Vector2Int(instance.TileX, instance.TileY);
            worldObject.ArtId = artId;
            worldObject.PrototypeNumber = instance.PrototypeNumber;
            int stateFlags = instance.Type == ObjectType.Portal
                ? instance.PortalFlags ?? proto?.PortalFlags ?? 0
                : instance.Type == ObjectType.Container
                    ? instance.ContainerFlags ?? proto?.ContainerFlags ?? 0
                    : 0;
            worldObject.Locked = (stateFlags & 0x1) != 0;
            worldObject.IsOpen = instance.Type == ObjectType.Portal && ((artId >> 14) & 0x1F) != 0;
            worldObject.Identity = instance.Identity;
            worldObject.ParentIdentity = instance.ParentIdentity;
            Session.Bind(_registeredSector, state, worldObject);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = (instance.TileX + instance.TileY) * 2 + 1;

            bool animate = instance.Type == ObjectType.Pc
                || instance.Type == ObjectType.Npc
                || (instance.Type == ObjectType.Scenery
                    && ((instance.SceneryFlags ?? proto?.SceneryFlags ?? 0) & 0x1) == 0); // !OSCF_NO_AUTO_ANIMATE

            var owner = visual.AddComponent<WorldObjectSpriteOwner>();
            owner.Initialize(
                _vfs,
                _art.Resolve,
                worldObject,
                instance.OffsetX,
                instance.OffsetY,
                pixelsPerUnit,
                animate);

            if (owner.CurrentSprite == null)
            {
                root.SetActive(false);
                if (Application.isPlaying) Destroy(root);
                else DestroyImmediate(root);
                return null;
            }

            if (instance.Type == ObjectType.Portal)
            {
                worldObject.PortalOpenable = owner.FrameCount > 1;
                Session.BindPortal(state, worldObject, owner.FrameCount, owner.FramesPerSecond);
            }

            _spriteOwners.Add(owner);
            return worldObject;
        }

        // Retail maps consolidate their authored .mob files into one GUID-prefixed stream
        // named reverse(ROT13(map-folder)). Only records in the selected sector are owned here.
        private List<ObjectInstance> ReadSectorMobiles(string selectedSectorPath)
        {
            var mobiles = new List<ObjectInstance>();
            string[] parts = selectedSectorPath.Replace('\\', '/').Split('/');
            if (parts.Length < 3) return mobiles;

            string mapName = parts[parts.Length - 2];
            string sectorName = System.IO.Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
            if (!long.TryParse(sectorName, out long selectedSectorId)) return mobiles;

            // Module archives retain the authored solitary .mob files. This is the source path
            // used by the editor and is also the least lossy input for our read-only runtime.
            int solitaryCount = 0;
            foreach (string path in _vfs.EnumerateFiles($"maps/{mapName}/"))
            {
                if (!path.EndsWith(".mob", StringComparison.OrdinalIgnoreCase)) continue;
                solitaryCount++;
                byte[] mobileBytes = _vfs.ReadAllBytes(path);
                int mobileOffset = 0;
                ObjectInstance mobile = ObjectInstanceReader.Read(mobileBytes, ref mobileOffset);
                if (BelongsToSector(mobile, selectedSectorId)) mobiles.Add(mobile);
            }
            if (solitaryCount > 0) return mobiles;

            // Finished/preprocessed modules may instead expose one GUID-prefixed stream.
            string mobilePath = "maps/" + ObfuscateMapName(mapName);
            if (!_vfs.Exists(mobilePath)) return mobiles;

            byte[] bytes = _vfs.ReadAllBytes(mobilePath);
            int offset = 16; // module GUID
            while (offset < bytes.Length)
            {
                int before = offset;
                ObjectInstance instance = ObjectInstanceReader.Read(bytes, ref offset);
                if (offset <= before)
                    throw new DatFormatException($"Mobile object reader made no progress at byte {before}.");

                if (BelongsToSector(instance, selectedSectorId)) mobiles.Add(instance);
            }

            return mobiles;
        }

        private static bool BelongsToSector(ObjectInstance instance, long selectedSectorId)
        {
            if (!instance.Location.HasValue) return false;
            long sectorX = (uint)instance.MapX >> 6;
            long sectorY = (uint)instance.MapY >> 6;
            return (sectorX | (sectorY << 26)) == selectedSectorId;
        }

        private static string ObfuscateMapName(string value)
        {
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= 'A' && c <= 'Z') chars[i] = (char)('A' + (c - 'A' + 13) % 26);
                else if (c >= 'a' && c <= 'z') chars[i] = (char)('a' + (c - 'a' + 13) % 26);
            }
            System.Array.Reverse(chars);
            return new string(chars);
        }

        private bool EnsureData()
        {
            if (_vfs != null) return true;

            var vfs = new DatVirtualFileSystem();
            int mounted = 0;
            string modulePath = GameDataLocator.Find(moduleArchive);
            if (!string.IsNullOrEmpty(modulePath))
            {
                vfs.MountFile(modulePath);
                mounted++;
            }

            foreach (string archive in artArchives)
            {
                string path = GameDataLocator.Find(archive);
                if (string.IsNullOrEmpty(path)) continue;
                vfs.MountFile(path);
                mounted++;
            }

            if (mounted == 0)
            {
                Debug.LogError("WorldObjectSectorLoader: no Arcanum archives could be located.", this);
                vfs.Dispose();
                return false;
            }

            string protoDirectory = GameDataLocator.FindDirectory("data/proto");
            if (string.IsNullOrEmpty(protoDirectory))
                Debug.LogWarning("WorldObjectSectorLoader: loose data/proto directory was not found; inherited ART ids cannot resolve.", this);

            _vfs = vfs;
            _prototypes = new ProtoLibrary(protoDirectory ?? string.Empty);
            _art = new ObjectArtResolvers(_vfs);
            return true;
        }

        public bool ReloadSector() => LoadSector(sectorPath);

        /// <summary>Releases the currently owned sector presentation without disposing mounted source data.</summary>
        public int UnloadSector()
        {
            int removed = ClearObjects();
            _renderIssues.Clear();
            LastRecordCount = 0;
            LastInventoryCount = 0;
            LastSuppressedCount = 0;
            LastDerivedPortalCount = 0;
            return removed;
        }

        private int ClearObjects()
        {
            if (session != null) session.UnloadSector(_registeredSector);
            _registeredSector = null;
            int removed = _spriteOwners.Count;
            _spriteOwners.Clear();
            _objectRoot = null;

            // Find owned roots as well as using the cached reference. This makes the owner recover
            // cleanly after an Editor domain reload, where non-serialized fields are reset but the
            // play-mode hierarchy survives.
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (child.name != ObjectRootName) continue;
                removed = Math.Max(removed, child.GetComponentsInChildren<WorldObjectSpriteOwner>(true).Length);
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            return removed;
        }

        private void OnDestroy()
        {
            ClearObjects();
            _vfs?.Dispose();
            _vfs = null;
        }

        private sealed class ObjectArtResolvers
        {
            private readonly WallArtResolver _walls;
            private readonly PortalArtResolver _portals;
            private readonly SceneryArtResolver _scenery;
            private readonly ItemArtResolver _items;
            private readonly ContainerArtResolver _containers;
            private readonly RoofArtResolver _roofs;
            private readonly FacadeArtResolver _facades;
            private readonly CritterArtResolver _critters;
            private readonly MonsterArtResolver _monsters;
            private readonly UniqueNpcArtResolver _uniqueNpcs;
            private readonly LightArtResolver _lights;
            private readonly EyeCandyArtResolver _eyeCandy;

            public ObjectArtResolvers(DatVirtualFileSystem vfs)
            {
                MesFile Mes(string path) => vfs.Exists(path) ? MesReader.Read(vfs.ReadAllBytes(path)) : null;

                MesFile wallName = Mes("art/wall/wallname.mes");
                MesFile structures = Mes("art/wall/structure.mes");
                if (wallName != null && structures != null) _walls = WallArtResolver.FromMes(wallName, structures);

                MesFile portal = Mes("art/portal/portal.mes");
                if (portal != null) _portals = PortalArtResolver.FromMes(portal);

                MesFile scenery = Mes("art/scenery/scenery.mes");
                if (scenery != null) _scenery = SceneryArtResolver.FromMes(scenery);

                MesFile itemGround = Mes("art/item/item_ground.mes");
                MesFile itemInven = Mes("art/item/item_inven.mes");
                MesFile itemPaper = Mes("art/item/item_paper.mes");
                MesFile itemSchematic = Mes("art/item/item_schematic.mes");
                if (itemGround != null) _items = new ItemArtResolver(itemGround, itemInven, itemPaper, itemSchematic);

                MesFile containers = Mes("art/container/container.mes");
                if (containers != null) _containers = ContainerArtResolver.FromMes(containers);

                MesFile roofs = Mes("art/roof/roofname.mes");
                if (roofs != null) _roofs = RoofArtResolver.FromMes(roofs);

                MesFile facades = Mes("art/facade/facadename.mes");
                if (facades != null) _facades = FacadeArtResolver.FromMes(facades);

                _critters = CritterArtResolver.Create();

                MesFile monsters = Mes("art/monster/monster.mes");
                if (monsters != null) _monsters = MonsterArtResolver.FromMes(monsters);

                MesFile unique = Mes("art/unique_npc/unique_npc.mes");
                if (unique != null) _uniqueNpcs = UniqueNpcArtResolver.FromMes(unique);

                MesFile lights = Mes("art/light/light.mes");
                if (lights != null) _lights = LightArtResolver.FromMes(lights);

                MesFile eyeCandy = Mes("art/eye_candy/eye_candy.mes");
                if (eyeCandy != null) _eyeCandy = EyeCandyArtResolver.FromMes(eyeCandy);
            }

            public string Resolve(uint artId)
            {
                switch (ArtId.Type(artId))
                {
                    case ArtId.TypeWall: return _walls?.Resolve(artId);
                    case ArtId.TypeCritter: return _critters.Resolve(artId, forceStand: false);
                    case ArtId.TypePortal: return _portals?.Resolve(artId);
                    case ArtId.TypeScenery: return _scenery?.Resolve(artId);
                    case ArtId.TypeItem: return _items?.Resolve(artId);
                    case ArtId.TypeContainer: return _containers?.Resolve(artId);
                    case ArtId.TypeRoof: return _roofs?.Resolve(artId);
                    case ArtId.TypeFacade: return _facades?.Resolve(artId);
                    case ArtId.TypeMonster: return _monsters?.Resolve(artId, forceStand: false);
                    case ArtId.TypeUniqueNpc: return _uniqueNpcs?.Resolve(artId, forceStand: false);
                    case ArtId.TypeLight: return _lights?.Resolve(artId);
                    case ArtId.TypeEyeCandy: return _eyeCandy?.Resolve(artId);
                    default: return null;
                }
            }

            public uint? DerivePortalFromWall(uint wallArtId, string wallPath)
                => _portals?.DeriveFromWall(wallArtId, wallPath);

            public bool Supports(int artType)
            {
                switch (artType)
                {
                    case ArtId.TypeWall:
                    case ArtId.TypeCritter:
                    case ArtId.TypePortal:
                    case ArtId.TypeScenery:
                    case ArtId.TypeItem:
                    case ArtId.TypeContainer:
                    case ArtId.TypeRoof:
                    case ArtId.TypeFacade:
                    case ArtId.TypeMonster:
                    case ArtId.TypeUniqueNpc:
                    case ArtId.TypeLight:
                    case ArtId.TypeEyeCandy:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}
