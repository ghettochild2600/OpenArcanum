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
        public ArcanumObjectId ParentIdentity => Placement.Kind is ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped
            ? Placement.ParentIdentity : default;
        public string SourceSector { get; }
        public ObjectType Type { get; }
        public int PrototypeNumber { get; }
        /// <summary>Effective source OBJ_F_NAME index used by dialogue/script inventory lookup.</summary>
        public int NameIndex { get; }
        /// <summary>Effective source NPC social class used by generated class-specific dialogue.</summary>
        public int SocialClass { get; }
        public long? AuthoredLocation { get; }
        public uint ArtId { get; internal set; }
        public bool Off { get; internal set; }
        public bool Locked { get; internal set; }
        public int UseScriptNum { get; internal set; }
        public int DialogNum { get; internal set; }
        public int ItemFlags { get; }
        public uint? InventoryArtId { get; }
        public int WeaponFlags { get; }
        public int GenericFlags { get; }
        /// <summary>Effective stored OBJ_F_ITEM_WEIGHT (instance override, otherwise prototype).</summary>
        public int UnitWeight { get; }
        /// <summary>First inventory ART frame rounded up to source 32-pixel grid cells.</summary>
        public InventoryFootprint InventoryFootprint { get; }
        /// <summary>Source ordinary inventory grid origin, or a worn-location value while equipped.</summary>
        public int InventoryLocation { get; internal set; }
        /// <summary>Source Ammo/Gold quantity. Null means this object type is singular and cannot stack.</summary>
        public int? StackQuantity { get; internal set; }
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
            int? socialClass = null)
        {
            Identity = identity;
            AuthoredParentIdentity = source.ParentIdentity;
            SourceSector = sector;
            Type = source.Type;
            PrototypeNumber = source.PrototypeNumber;
            NameIndex = nameIndex ?? source.NameIndex ?? 0;
            SocialClass = socialClass ?? source.SocialClass ?? 0;
            AuthoredLocation = source.Location;
            ArtId = artId;
            Off = off;
            Locked = locked;
            UseScriptNum = source.UseScriptNum;
            DialogNum = source.DialogNum;
            ItemFlags = itemFlags;
            InventoryArtId = inventoryArtId;
            WeaponFlags = weaponFlags;
            GenericFlags = genericFlags;
            UnitWeight = unitWeight ?? source.Weight ?? 0;
            InventoryFootprint = inventoryFootprint ?? InventoryFootprint.OneCell;
            StackQuantity = ValidateStackQuantity(Type, stackQuantity);
            TilePosition = source.Location.HasValue
                ? new Vector2(source.TileX, source.TileY)
                : Vector2.zero;
            Placement = source.ParentIdentity.IsPersistent
                ? WornLocations.TryFromSource(source.InvLocation, out WornLocation wornLocation)
                    ? ObjectPlacement.EquippedBy(source.ParentIdentity, wornLocation)
                    : ObjectPlacement.ContainedBy(source.ParentIdentity)
                : ObjectPlacement.InWorld(sector, TilePosition);
            InventoryLocation = Placement.Kind == ObjectPlacementKind.World
                ? -1
                : Placement.Kind == ObjectPlacementKind.Equipped
                    ? (int)Placement.WornLocation
                    : inventoryLocation ?? source.InvLocation;
            // portal.c portal_is_open: CURRENT_AID frame != 0, including prototype fallback.
            PortalOpen = Type == ObjectType.Portal && ((artId >> 14) & 31) != 0;
        }

        internal PersistentObjectState(ObjectProtoInfo prototype, ArcanumObjectId identity,
            string creationSector, ObjectPlacement placement, InventoryFootprint inventoryFootprint,
            int inventoryLocation = -1)
        {
            Identity = identity;
            SourceSector = creationSector;
            Type = prototype.Type;
            PrototypeNumber = prototype.ProtoNumber;
            NameIndex = prototype.NameIndex ?? 0;
            SocialClass = prototype.SocialClass ?? 0;
            ArtId = prototype.CurrentArtId;
            ItemFlags = prototype.ItemFlags ?? 0;
            InventoryArtId = prototype.InvAid;
            WeaponFlags = prototype.Weapon?.Flags ?? 0;
            GenericFlags = prototype.GenericFlags ?? 0;
            UnitWeight = prototype.Weight;
            InventoryFootprint = inventoryFootprint;
            StackQuantity = ValidateStackQuantity(Type, Type switch
            {
                ObjectType.Ammo => prototype.AmmoQuantity,
                ObjectType.Gold => prototype.GoldQuantity,
                _ => null,
            });
            Placement = placement;
            InventoryLocation = placement.Kind == ObjectPlacementKind.Equipped
                ? (int)placement.WornLocation : inventoryLocation;
            TilePosition = placement.Kind == ObjectPlacementKind.World ? placement.TilePosition : Vector2.zero;
            IsRuntimeCreated = true;
        }

        internal PersistentObjectState(PersistentObjectState source, ArcanumObjectId identity,
            ObjectPlacement placement, int stackQuantity, int inventoryLocation)
        {
            Identity = identity;
            SourceSector = source.SourceSector;
            Type = source.Type;
            PrototypeNumber = source.PrototypeNumber;
            NameIndex = source.NameIndex;
            SocialClass = source.SocialClass;
            ArtId = source.ArtId;
            Off = source.Off;
            Locked = source.Locked;
            UseScriptNum = source.UseScriptNum;
            DialogNum = source.DialogNum;
            ItemFlags = source.ItemFlags;
            InventoryArtId = source.InventoryArtId;
            WeaponFlags = source.WeaponFlags;
            GenericFlags = source.GenericFlags;
            UnitWeight = source.UnitWeight;
            InventoryFootprint = source.InventoryFootprint;
            StackQuantity = ValidateStackQuantity(Type, stackQuantity);
            Placement = placement;
            InventoryLocation = placement.Kind == ObjectPlacementKind.Equipped
                ? (int)placement.WornLocation : inventoryLocation;
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
            runtime.DialogNum = DialogNum;
            runtime.ItemFlags = ItemFlags;
            runtime.AmmoQuantity = Type == ObjectType.Ammo ? StackQuantity.GetValueOrDefault() : 0;
            runtime.GoldQuantity = Type == ObjectType.Gold ? StackQuantity.GetValueOrDefault() : 0;
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

        private static int? ValidateStackQuantity(ObjectType type, int? quantity)
        {
            bool stackable = type is ObjectType.Ammo or ObjectType.Gold;
            if (!stackable)
            {
                if (quantity.HasValue)
                    throw new System.ArgumentException($"{type} does not have a source quantity field.",
                        nameof(quantity));
                return null;
            }
            if (!quantity.HasValue || quantity.Value < 1)
                throw new System.ArgumentOutOfRangeException(nameof(quantity), quantity,
                    $"{type} quantity must be in the positive Int32 source range.");
            return quantity.Value;
        }
    }
}
