using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>Session-owned instance values, independent of Unity object/presentation lifetime.
    /// Source identity/location are retained for collision checks, not a new movement system.</summary>
    public sealed class PersistentObjectState
    {
        public ArcanumObjectId Identity { get; }
        public ArcanumObjectId AuthoredParentIdentity { get; }
        public ArcanumObjectId ParentIdentity => Placement.Kind == ObjectPlacementKind.Contained
            ? Placement.ParentIdentity : default;
        public string SourceSector { get; }
        public ObjectType Type { get; }
        public int PrototypeNumber { get; }
        public long? AuthoredLocation { get; }
        public uint ArtId { get; internal set; }
        public bool Off { get; internal set; }
        public bool Locked { get; internal set; }
        public int UseScriptNum { get; internal set; }
        public bool PortalOpen { get; internal set; }
        public Vector2 TilePosition { get; internal set; }
        public ObjectPlacement Placement { get; internal set; }
        public bool IsRuntimeCreated { get; }

        public PersistentObjectState(
            ObjectInstance source,
            ArcanumObjectId identity,
            string sector,
            uint artId,
            bool off,
            bool locked)
        {
            Identity = identity;
            AuthoredParentIdentity = source.ParentIdentity;
            SourceSector = sector;
            Type = source.Type;
            PrototypeNumber = source.PrototypeNumber;
            AuthoredLocation = source.Location;
            ArtId = artId;
            Off = off;
            Locked = locked;
            TilePosition = source.Location.HasValue
                ? new Vector2(source.TileX, source.TileY)
                : Vector2.zero;
            Placement = source.ParentIdentity.IsPersistent
                ? ObjectPlacement.ContainedBy(source.ParentIdentity)
                : ObjectPlacement.InWorld(sector, TilePosition);
            // portal.c portal_is_open: CURRENT_AID frame != 0, including prototype fallback.
            PortalOpen = Type == ObjectType.Portal && ((artId >> 14) & 31) != 0;
        }

        internal PersistentObjectState(ObjectProtoInfo prototype, ArcanumObjectId identity,
            string creationSector, ObjectPlacement placement)
        {
            Identity = identity;
            SourceSector = creationSector;
            Type = prototype.Type;
            PrototypeNumber = prototype.ProtoNumber;
            ArtId = prototype.CurrentArtId;
            Placement = placement;
            TilePosition = placement.Kind == ObjectPlacementKind.World ? placement.TilePosition : Vector2.zero;
            IsRuntimeCreated = true;
        }

        internal bool Matches(ObjectInstance source, string sector)
            => SourceSector == sector && Type == source.Type && PrototypeNumber == source.PrototypeNumber
               && AuthoredLocation == source.Location && AuthoredParentIdentity == source.ParentIdentity;

        internal void Restore(WorldObject runtime)
        {
            runtime.Identity = Identity;
            runtime.ParentIdentity = ParentIdentity;
            runtime.Oid = Identity.IsPersistent ? Identity.Key : null;
            runtime.ParentOid = ParentIdentity.IsPersistent ? ParentIdentity.Key : null;
            runtime.ArtId = ArtId;
            runtime.Off = Off;
            runtime.Locked = Locked;
            runtime.UseScriptNum = UseScriptNum;
            runtime.IsOpen = PortalOpen;
            runtime.ApplyMovementState(TilePosition, ArtId, false);
        }

        internal void Capture(WorldObject runtime)
        {
            // Portal ArtId and logical state are owned by PortalTransitionScheduler, never by a sprite.
            if (Type != ObjectType.Portal)
            {
                ArtId = runtime.ArtId;
                if (runtime.IsMoving && IsCritterArt(ArtId))
                    ArtId = CritterArtResolver.WithAnimRotation(ArtId, 0, CritterArtResolver.RotationOf(ArtId));
                TilePosition = runtime.TilePosition;
                if (Placement.Kind == ObjectPlacementKind.World)
                    Placement = ObjectPlacement.InWorld(Placement.Sector, TilePosition);
            }
            Off = runtime.Off;
            Locked = runtime.Locked;
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
