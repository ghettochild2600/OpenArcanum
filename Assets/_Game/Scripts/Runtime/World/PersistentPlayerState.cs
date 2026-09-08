using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Session-owned production-player state, independent of Unity presentation lifetime.</summary>
    public sealed class PersistentPlayerState
    {
        public ArcanumObjectId Identity { get; }
        public string Sector { get; private set; }
        public Vector2 TilePosition { get; internal set; }
        public uint ArtId { get; internal set; }

        internal PersistentPlayerState(ArcanumObjectId identity, string sector, Vector2 tilePosition, uint artId)
        {
            Identity = identity;
            Sector = sector;
            TilePosition = tilePosition;
            ArtId = artId;
        }

        internal void EnterSector(string sector, Vector2 spawnTile, uint artId)
        {
            if (Sector == sector) return;
            Sector = sector;
            TilePosition = spawnTile;
            ArtId = artId;
        }

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
            TilePosition = runtime.TilePosition;
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
