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

        private readonly bool[] _terrainBlocked = new bool[SectorTerrain.TileCount];
        private readonly int[] _objectBlockers = new int[SectorTerrain.TileCount];
        private readonly Dictionary<Vector2Int, List<WorldObject>> _edgeObjects = new();
        private readonly Dictionary<ArcanumObjectId, Vector2Int> _ordinaryObjects = new();
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
            if (obj.Type == ObjectType.Wall || obj.Type == ObjectType.Portal)
            {
                if (!_edgeObjects.TryGetValue(obj.Tile, out List<WorldObject> objects))
                    _edgeObjects.Add(obj.Tile, objects = new List<WorldObject>());
                objects.Add(obj);
                return;
            }

            if ((sourceFlags & ObjectFlagNoBlock) != 0) return;
            if (!IsOrdinaryBlockingType(obj.Type)) return;
            _objectBlockers[Index(obj.Tile.x, obj.Tile.y)]++;
            if (obj.Identity.IsPersistent) _ordinaryObjects[obj.Identity] = obj.Tile;
        }

        public void Unregister(WorldObject obj)
        {
            if (obj == null || !Contains(obj.Tile)) return;
            if (obj.Type == ObjectType.Wall || obj.Type == ObjectType.Portal)
            {
                if (_edgeObjects.TryGetValue(obj.Tile, out List<WorldObject> objects))
                {
                    objects.Remove(obj);
                    if (objects.Count == 0) _edgeObjects.Remove(obj.Tile);
                }
                return;
            }
            if (!_ordinaryObjects.Remove(obj.Identity)) return;
            _objectBlockers[Index(obj.Tile.x, obj.Tile.y)] = Math.Max(0,
                _objectBlockers[Index(obj.Tile.x, obj.Tile.y)] - 1);
            if (_controlledIdentity == obj.Identity) _controlledIdentity = default;
        }

        /// <summary>Removes the controlled critter from static occupancy without ignoring other occupants.</summary>
        public void SetControlledObject(WorldObject obj)
        {
            if (_controlledIdentity.IsPersistent && _ordinaryObjects.TryGetValue(_controlledIdentity, out Vector2Int oldTile))
                _objectBlockers[Index(oldTile.x, oldTile.y)]++;

            _controlledIdentity = obj != null ? obj.Identity : default;
            if (obj != null && obj.Identity.IsPersistent && _ordinaryObjects.TryGetValue(obj.Identity, out Vector2Int tile))
                _objectBlockers[Index(tile.x, tile.y)] = Math.Max(0, _objectBlockers[Index(tile.x, tile.y)] - 1);
        }

        /// <summary>Moves one registered critter occupancy after an authoritative combat route is preflighted.</summary>
        internal bool MoveRegisteredObject(ArcanumObjectId identity, Vector2Int destination)
        {
            if (!identity.IsPersistent || !Contains(destination)
                || !_ordinaryObjects.TryGetValue(identity, out Vector2Int previous)) return false;
            if (previous == destination) return true;
            bool controlled = _controlledIdentity == identity;
            if (!controlled && !IsWalkable(destination)) return false;
            if (!controlled)
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
                if (pair.Key != identity && pair.Value == tile) return true;
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

        private static int Index(int x, int y) => y * Size + x;
    }
}
