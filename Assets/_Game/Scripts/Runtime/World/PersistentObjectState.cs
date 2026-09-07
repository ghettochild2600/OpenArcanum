using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.World
{
    /// <summary>Session-owned instance values, independent of Unity object/presentation lifetime.
    /// Source identity/location are retained for collision checks, not a new movement system.</summary>
    public sealed class PersistentObjectState
    {
        public ArcanumObjectId Identity { get; }
        public ArcanumObjectId ParentIdentity { get; }
        public string SourceSector { get; }
        public ObjectType Type { get; }
        public int PrototypeNumber { get; }
        public long? AuthoredLocation { get; }
        public uint ArtId { get; internal set; }
        public bool Off { get; internal set; }
        public bool Locked { get; internal set; }
        public bool PortalOpen { get; internal set; }

        public PersistentObjectState(
            ObjectInstance source,
            ArcanumObjectId identity,
            string sector,
            uint artId,
            bool off,
            bool locked)
        {
            Identity = identity;
            ParentIdentity = source.ParentIdentity;
            SourceSector = sector;
            Type = source.Type;
            PrototypeNumber = source.PrototypeNumber;
            AuthoredLocation = source.Location;
            ArtId = artId;
            Off = off;
            Locked = locked;
            // portal.c portal_is_open: CURRENT_AID frame != 0, including prototype fallback.
            PortalOpen = Type == ObjectType.Portal && ((artId >> 14) & 31) != 0;
        }

        internal bool Matches(ObjectInstance source, string sector)
            => SourceSector == sector && Type == source.Type && PrototypeNumber == source.PrototypeNumber
               && AuthoredLocation == source.Location && ParentIdentity == source.ParentIdentity;

        internal void Restore(WorldObject runtime)
        {
            runtime.Identity = Identity;
            runtime.ParentIdentity = ParentIdentity;
            runtime.Oid = Identity.IsPersistent ? Identity.Key : null;
            runtime.ParentOid = ParentIdentity.IsPersistent ? ParentIdentity.Key : null;
            runtime.ArtId = ArtId;
            runtime.Off = Off;
            runtime.Locked = Locked;
            runtime.IsOpen = PortalOpen;
        }

        internal void Capture(WorldObject runtime)
        {
            // Portal ArtId and logical state are owned by PortalTransitionScheduler, never by a sprite.
            if (Type != ObjectType.Portal) ArtId = runtime.ArtId;
            Off = runtime.Off;
            Locked = runtime.Locked;
        }
    }
}
