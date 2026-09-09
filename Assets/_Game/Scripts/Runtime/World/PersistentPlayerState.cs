using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Session-owned production-player state, independent of Unity presentation lifetime.</summary>
    public sealed class PersistentPlayerState
    {
        private string _mapPath;
        private Vector2 _mapPosition;
        public ArcanumObjectId Identity { get; }
        public Vector2 MapPosition => _mapPosition;
        public string Sector => SectorCoordinate.FromGlobal(_mapPath, _mapPosition).Path;
        public Vector2 TilePosition => SectorCoordinate.FromGlobal(_mapPath, _mapPosition).ToLocal(_mapPosition);
        public Vector2Int? Destination { get; private set; }
        public uint ArtId { get; internal set; }

        internal PersistentPlayerState(ArcanumObjectId identity, string sector, Vector2 tilePosition, uint artId)
        {
            Identity = identity;
            Relocate(sector, tilePosition, artId);
            ArtId = artId;
        }

        internal void EnterSector(string sector, Vector2 spawnTile, uint artId)
        {
            if (Sector == sector) return;
            Relocate(sector, spawnTile, artId);
        }

        internal void Relocate(string sector, Vector2 localPosition, uint artId)
        {
            if (!SectorCoordinate.TryParse(sector, out SectorCoordinate coordinate))
                throw new System.ArgumentException("Player sector path must contain a source sector identity.", nameof(sector));
            _mapPath = coordinate.MapPath;
            _mapPosition = coordinate.ToGlobal(localPosition);
            ArtId = artId;
        }

        internal void SetLocalPosition(string sector, Vector2 localPosition)
        {
            if (!SectorCoordinate.TryParse(sector, out SectorCoordinate coordinate)
                || !string.Equals(coordinate.MapPath, _mapPath, System.StringComparison.Ordinal))
                throw new System.ArgumentException("Movement sector must belong to the player's current map.", nameof(sector));
            _mapPosition = coordinate.ToGlobal(localPosition);
        }

        internal void RestoreMapPosition(string mapPath, Vector2 mapPosition, uint artId)
        {
            _mapPath = mapPath;
            _mapPosition = mapPosition;
            ArtId = artId;
        }

        internal void SetDestination(Vector2Int destination) => Destination = destination;
        internal void ClearDestination() => Destination = null;

        internal void Restore(WorldObject runtime)
        {
            runtime.Type = ObjectType.Pc;
            runtime.Identity = Identity;
            runtime.Oid = Identity.Key;
            runtime.ArtId = ArtId;
            runtime.TilePosition = TilePosition;
            runtime.ApplyMovementState(TilePosition, ArtId, false);
        }

        internal void Capture(WorldObject runtime)
        {
            ArtId = runtime.ArtId;
            if (runtime.IsMoving && IsCritterArt(ArtId))
                ArtId = CritterArtResolver.WithAnimRotation(ArtId, 0, CritterArtResolver.RotationOf(ArtId));
            SetLocalPosition(Sector, runtime.TilePosition);
        }

        private static bool IsCritterArt(uint artId)
        {
            int type = Arcanum.Formats.Art.ArtId.Type(artId);
            return type == Arcanum.Formats.Art.ArtId.TypeCritter
                   || type == Arcanum.Formats.Art.ArtId.TypeMonster
                   || type == Arcanum.Formats.Art.ArtId.TypeUniqueNpc;
        }
    }
}
