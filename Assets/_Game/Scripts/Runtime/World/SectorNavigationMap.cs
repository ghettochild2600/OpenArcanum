using System;
using System.Collections.Generic;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Tiles;
using Arcanum.Formats.World;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// The navigable surface of one 64x64 Arcanum sector. Tile masks and terrain flags
    /// block destinations; walls and portals block the directional edge crossed by a step.
    /// This deliberately models source data rather than Unity colliders or a NavMesh.
    /// </summary>
    public sealed class SectorNavigationMap
    {
        private const int Size = SectorTerrain.Size;
        private const int ObjectFlagNoBlock = 0x00000400;
        private const int ObjectFlagShootThrough = 0x00000020;

        private readonly bool[] _terrainBlocked = new bool[SectorTerrain.TileCount];
        private readonly int[] _objectBlockers = new int[SectorTerrain.TileCount];
        private readonly int[] _projectileBlockers = new int[SectorTerrain.TileCount];
        private readonly Dictionary<Vector2Int, List<WorldObject>> _edgeObjects = new();
        private readonly Dictionary<WorldObject, int> _sourceFlags = new();
        private readonly Dictionary<WorldObject, Vector2Int> _projectileObjects = new();
        private readonly Dictionary<ArcanumObjectId, Vector2Int> _ordinaryObjects = new();
        private readonly HashSet<ArcanumObjectId> _ordinaryBlockers = new();
        private ArcanumObjectId _controlledIdentity;

        public SectorNavigationMap(SectorTerrain terrain, bool[] placedBlocks, TileNameTable tileNames)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int index = Index(x, y);
                uint artId = terrain.At(x, y);
                bool generated = ArtId.Type(artId) == ArtId.TypeFacade
                    ? !FacadeArtResolver.Walkable(artId)
                    : TileArtId.IsTile(artId)
                      && (tileNames.FlagsOf(TileArtId.Num1(artId), TileArtId.TileType(artId), TileArtId.Flippable1(artId))
                          & TileFlags.Block) != 0;
                _terrainBlocked[index] = generated || (placedBlocks != null && index < placedBlocks.Length && placedBlocks[index]);
            }
        }

        public bool Contains(Vector2Int tile)
            => tile.x >= 0 && tile.y >= 0 && tile.x < Size && tile.y < Size;

        public bool IsWalkable(Vector2Int tile)
            => Contains(tile) && !_terrainBlocked[Index(tile.x, tile.y)] && _objectBlockers[Index(tile.x, tile.y)] == 0;

        public void Register(WorldObject obj, int sourceFlags)
        {
            if (obj == null || !Contains(obj.Tile)) return;
            _sourceFlags[obj] = sourceFlags;
            if (obj.Type == ObjectType.Wall || obj.Type == ObjectType.Portal)
            {
                if (!_edgeObjects.TryGetValue(obj.Tile, out List<WorldObject> objects))
                    _edgeObjects.Add(obj.Tile, objects = new List<WorldObject>());
                objects.Add(obj);
                return;
            }

            if (!IsOrdinaryBlockingType(obj.Type)) return;
            if (obj.Identity.IsPersistent) _ordinaryObjects[obj.Identity] = obj.Tile;
            if ((sourceFlags & ObjectFlagNoBlock) != 0) return;
            _objectBlockers[Index(obj.Tile.x, obj.Tile.y)]++;
            if (obj.Identity.IsPersistent) _ordinaryBlockers.Add(obj.Identity);
            if (IsProjectileBlockingType(obj.Type) && (sourceFlags & ObjectFlagShootThrough) == 0)
            {
                _projectileBlockers[Index(obj.Tile.x, obj.Tile.y)]++;
                _projectileObjects[obj] = obj.Tile;
            }
        }

        public void Unregister(WorldObject obj)
        {
            if (obj == null || !Contains(obj.Tile)) return;
            _sourceFlags.Remove(obj);
            if (obj.Type == ObjectType.Wall || obj.Type == ObjectType.Portal)
            {
                if (_edgeObjects.TryGetValue(obj.Tile, out List<WorldObject> objects))
                {
                    objects.Remove(obj);
                    if (objects.Count == 0) _edgeObjects.Remove(obj.Tile);
                }
                return;
            }
            if (_projectileObjects.Remove(obj, out Vector2Int projectileTile))
                _projectileBlockers[Index(projectileTile.x, projectileTile.y)] = Math.Max(0,
                    _projectileBlockers[Index(projectileTile.x, projectileTile.y)] - 1);
            if (!_ordinaryObjects.Remove(obj.Identity)) return;
            if (_ordinaryBlockers.Remove(obj.Identity))
                _objectBlockers[Index(obj.Tile.x, obj.Tile.y)] = Math.Max(0,
                    _objectBlockers[Index(obj.Tile.x, obj.Tile.y)] - 1);
            if (_controlledIdentity == obj.Identity) _controlledIdentity = default;
        }

        /// <summary>Removes the controlled critter from static occupancy without ignoring other occupants.</summary>
        public void SetControlledObject(WorldObject obj)
        {
            if (_controlledIdentity.IsPersistent && _ordinaryBlockers.Contains(_controlledIdentity)
                && _ordinaryObjects.TryGetValue(_controlledIdentity, out Vector2Int oldTile))
                _objectBlockers[Index(oldTile.x, oldTile.y)]++;

            _controlledIdentity = obj != null ? obj.Identity : default;
            if (obj != null && obj.Identity.IsPersistent && _ordinaryBlockers.Contains(obj.Identity)
                && _ordinaryObjects.TryGetValue(obj.Identity, out Vector2Int tile))
                _objectBlockers[Index(tile.x, tile.y)] = Math.Max(0, _objectBlockers[Index(tile.x, tile.y)] - 1);
        }

        /// <summary>Projects source OF_NO_BLOCK without losing the registered stable identity.</summary>
        internal bool SetRegisteredObjectBlocking(ArcanumObjectId identity, bool blocks)
        {
            if (!identity.IsPersistent || !_ordinaryObjects.TryGetValue(identity, out Vector2Int tile)) return false;
            bool wasBlocking = _ordinaryBlockers.Contains(identity);
            if (wasBlocking == blocks) return true;
            if (blocks)
            {
                _ordinaryBlockers.Add(identity);
                if (_controlledIdentity != identity) _objectBlockers[Index(tile.x, tile.y)]++;
            }
            else
            {
                _ordinaryBlockers.Remove(identity);
                if (_controlledIdentity != identity)
                    _objectBlockers[Index(tile.x, tile.y)] = Math.Max(0,
                        _objectBlockers[Index(tile.x, tile.y)] - 1);
            }
            return true;
        }

        /// <summary>Moves one registered critter occupancy after an authoritative combat route is preflighted.</summary>
        internal bool MoveRegisteredObject(ArcanumObjectId identity, Vector2Int destination)
        {
            if (!identity.IsPersistent || !Contains(destination)
                || !_ordinaryObjects.TryGetValue(identity, out Vector2Int previous)) return false;
            if (previous == destination) return true;
            bool contributes = _ordinaryBlockers.Contains(identity) && _controlledIdentity != identity;
            if (contributes && !IsWalkable(destination)) return false;
            if (contributes)
            {
                _objectBlockers[Index(previous.x, previous.y)] = Math.Max(0,
                    _objectBlockers[Index(previous.x, previous.y)] - 1);
                _objectBlockers[Index(destination.x, destination.y)]++;
            }
            _ordinaryObjects[identity] = destination;
            return true;
        }

        /// <summary>Combat actors ignore their own registered tile but never another actor's tile.</summary>
        internal bool IsOccupiedByOther(ArcanumObjectId identity, Vector2Int tile)
        {
            foreach (KeyValuePair<ArcanumObjectId, Vector2Int> pair in _ordinaryObjects)
                if (pair.Key != identity && _ordinaryBlockers.Contains(pair.Key) && pair.Value == tile) return true;
            return false;
        }

        /// <summary>Source projectile traversal: hard blockers count, intervening critters do not.</summary>
        public bool HasProjectileLineOfFire(Vector2Int source, Vector2Int target)
        {
            if (!Contains(source) || !Contains(target)) return false;
            int x = source.x;
            int y = source.y;
            int dx = Math.Abs(target.x - source.x);
            int dy = Math.Abs(target.y - source.y);
            int sx = source.x < target.x ? 1 : -1;
            int sy = source.y < target.y ? 1 : -1;
            int error = dx - dy;
            while (x != target.x || y != target.y)
            {
                int twice = 2 * error;
                int nextX = x;
                int nextY = y;
                if (twice > -dy) { error -= dy; nextX += sx; }
                if (twice < dx) { error += dx; nextY += sy; }
                var from = new Vector2Int(x, y);
                var to = new Vector2Int(nextX, nextY);
                if (ProjectileEdgeBlocked(from, to) || _terrainBlocked[Index(to.x, to.y)]) return false;
                if (to != target && _projectileBlockers[Index(to.x, to.y)] > 0) return false;
                x = nextX;
                y = nextY;
            }
            return true;
        }

        private bool ProjectileEdgeBlocked(Vector2Int from, Vector2Int to)
        {
            int dx = Math.Sign(to.x - from.x);
            int dy = Math.Sign(to.y - from.y);
            int rotation = IsoProjection.DirFromDelta(dx, dy);
            if ((rotation & 1) != 0) return ProjectileBlocksAt(from, rotation)
                                             || ProjectileBlocksAt(to, (rotation + 4) & 7);
            int ccw = (rotation + 7) & 7;
            int cw = (rotation + 1) & 7;
            return ProjectileBlocksAt(from, ccw)
                   || ProjectileBlocksAt(from + IsoProjection.DirDelta[ccw], cw)
                   || ProjectileBlocksAt(from, cw)
                   || ProjectileBlocksAt(from + IsoProjection.DirDelta[cw], ccw);
        }

        private bool ProjectileBlocksAt(Vector2Int tile, int crossingRotation)
        {
            if (!_edgeObjects.TryGetValue(tile, out List<WorldObject> objects)) return false;
            foreach (WorldObject obj in objects)
            {
                if (obj == null || obj.Off
                    || _sourceFlags.TryGetValue(obj, out int flags) && (flags & ObjectFlagShootThrough) != 0)
                    continue;
                int artRotation = CritterArtResolver.RotationOf(obj.ArtId);
                if ((artRotation & 1) == 0) artRotation++;
                if (artRotation != crossingRotation) continue;
                if (obj.Type == ObjectType.Portal) return !obj.IsOpen;
                int piece = (int)((obj.ArtId >> 14) & 0x3F);
                if (!IsWallPassagePiece(piece)) return true;
            }
            return false;
        }

        public bool CanTraverse(Vector2Int from, int rotation)
        {
            if (rotation < 0 || rotation >= IsoProjection.DirDelta.Length || !Contains(from)) return false;
            Vector2Int to = from + IsoProjection.DirDelta[rotation];
            if (!IsWalkable(to)) return false;

            // object_calc_traversal_cost decomposes even rotations into both adjacent
            // odd-edge routes, preventing diagonal corner cutting through walls.
            if ((rotation & 1) == 0)
            {
                int ccw = (rotation + 7) & 7;
                int cw = (rotation + 1) & 7;
                return !OddEdgeBlocked(from, ccw)
                       && !OddEdgeBlocked(from + IsoProjection.DirDelta[ccw], cw)
                       && !OddEdgeBlocked(from, cw)
                       && !OddEdgeBlocked(from + IsoProjection.DirDelta[cw], ccw);
            }

            return !OddEdgeBlocked(from, rotation);
        }

        /// <summary>Checks the current sector's side of one cardinal boundary crossing.</summary>
        public bool CanExit(Vector2Int from, int rotation)
        {
            if ((rotation & 1) == 0 || rotation < 0 || rotation >= IsoProjection.DirDelta.Length
                || !IsWalkable(from)) return false;
            Vector2Int outside = from + IsoProjection.DirDelta[rotation];
            if (Contains(outside)) return false;
            return !OddEdgeBlocked(from, rotation);
        }

        public bool CanCrossBoundaryTo(SectorNavigationMap adjacent, Vector2Int exitTile, int rotation)
        {
            if (adjacent == null || !CanExit(exitTile, rotation)) return false;
            Vector2Int entry = EntryTile(exitTile, rotation);
            return adjacent.CanExit(entry, (rotation + 4) & 7);
        }

        public static Vector2Int EntryTile(Vector2Int exitTile, int rotation)
        {
            Vector2Int outside = exitTile + IsoProjection.DirDelta[rotation];
            return new Vector2Int((outside.x + Size) % Size, (outside.y + Size) % Size);
        }

        private bool OddEdgeBlocked(Vector2Int from, int rotation)
        {
            Vector2Int to = from + IsoProjection.DirDelta[rotation];
            return BlocksAt(from, rotation) || BlocksAt(to, (rotation + 4) & 7);
        }

        private bool BlocksAt(Vector2Int tile, int crossingRotation)
        {
            if (!_edgeObjects.TryGetValue(tile, out List<WorldObject> objects)) return false;
            foreach (WorldObject obj in objects)
            {
                if (obj == null || obj.Off) continue;
                int artRotation = CritterArtResolver.RotationOf(obj.ArtId);
                if ((artRotation & 1) == 0) artRotation++;
                if (artRotation != crossingRotation) continue;

                if (obj.Type == ObjectType.Portal)
                {
                    if (!obj.IsOpen) return true;
                    continue;
                }

                int piece = (int)((obj.ArtId >> 14) & 0x3F);
                if (!IsWallPassagePiece(piece)) return true;
            }
            return false;
        }

        private static bool IsWallPassagePiece(int piece)
        {
            switch (piece)
            {
                case 10: case 13: case 14: case 17: case 18: case 19:
                case 22: case 25: case 26: case 29: case 30: case 31: case 32:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsOrdinaryBlockingType(ObjectType type)
            => type == ObjectType.Container || type == ObjectType.Scenery || type == ObjectType.Projectile
               || type == ObjectType.Pc || type == ObjectType.Npc || type == ObjectType.Trap;

        private static bool IsProjectileBlockingType(ObjectType type)
            => type == ObjectType.Container || type == ObjectType.Scenery
               || type == ObjectType.Projectile || type == ObjectType.Trap;

        private static int Index(int x, int y) => y * Size + x;
    }
}
