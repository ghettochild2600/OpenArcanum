using System;
using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Combat;
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
        public int SourceWorth { get; }
        public int MaximumHitPoints { get; }
        public int HitPointDamage { get; }
        public int RetailPriceMultiplier { get; }
        public int InventorySourceId { get; }
        public ArcanumObjectId SubstituteInventoryIdentity { get; }
        public int NpcFlags { get; }
        public int BuyObjectScriptNum { get; }
        public int ContainerFlags { get; }
        public long? AuthoredLocation { get; }
        public uint ArtId { get; internal set; }
        public bool Off { get; internal set; }
        public bool Locked { get; internal set; }
        public int UseScriptNum { get; internal set; }
        public int DialogNum { get; internal set; }
        public int ItemFlags { get; }
        public uint? InventoryArtId { get; }
        public int WeaponFlags { get; }
        /// <summary>Effective prototype combat facts retained independently of Unity presentation.</summary>
        public Weapon WeaponData { get; }
        /// <summary>Effective source OBJ_F_AMMO_TYPE for an ammo stack.</summary>
        public int? AmmoItemType { get; }
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
        /// <summary>Persistent exact-once guard for the supported default death consequence transaction.</summary>
        public bool DeathConsequencesProcessed { get; internal set; }

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
            int containerFlags = 0)
        {
            Identity = identity;
            AuthoredParentIdentity = source.ParentIdentity;
            SourceSector = sector;
            Type = source.Type;
            PrototypeNumber = source.PrototypeNumber;
            NameIndex = nameIndex ?? source.NameIndex ?? 0;
            SocialClass = socialClass ?? source.SocialClass ?? 0;
            SourceWorth = sourceWorth ?? source.Worth ?? 0;
            MaximumHitPoints = Math.Max(0, maximumHitPoints ?? source.HpPoints.GetValueOrDefault());
            HitPointDamage = Math.Max(0, hitPointDamage ?? source.HpDamage.GetValueOrDefault());
            RetailPriceMultiplier = retailPriceMultiplier ?? source.RetailPriceMultiplier ?? 0;
            InventorySourceId = Math.Max(0, inventorySourceId ?? source.InventorySource.GetValueOrDefault());
            SubstituteInventoryIdentity = substituteInventoryIdentity;
            NpcFlags = npcFlags;
            BuyObjectScriptNum = buyObjectScriptNum != 0 ? buyObjectScriptNum : source.BuyObjectScriptNum;
            ContainerFlags = containerFlags;
            AuthoredLocation = source.Location;
            ArtId = artId;
            Off = off;
            Locked = locked;
            UseScriptNum = source.UseScriptNum;
            DialogNum = source.DialogNum;
            ItemFlags = itemFlags;
            InventoryArtId = inventoryArtId;
            WeaponFlags = weaponFlags;
            WeaponData = weaponData?.Clone();
            AmmoItemType = ammoItemType;
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
            SourceWorth = prototype.Worth;
            MaximumHitPoints = Math.Max(0, prototype.HpPoints.GetValueOrDefault()
                                             + prototype.HpAdjustment.GetValueOrDefault());
            HitPointDamage = Math.Max(0, prototype.HpDamage.GetValueOrDefault());
            RetailPriceMultiplier = prototype.RetailPriceMultiplier ?? 0;
            InventorySourceId = Math.Max(0, prototype.InventorySource.GetValueOrDefault());
            NpcFlags = prototype.NpcFlags ?? 0;
            BuyObjectScriptNum = prototype.BuyObjectScriptNum;
            ContainerFlags = prototype.ContainerFlags ?? 0;
            ArtId = prototype.CurrentArtId;
            ItemFlags = prototype.ItemFlags ?? 0;
            InventoryArtId = prototype.InvAid;
            WeaponFlags = prototype.Weapon?.Flags ?? 0;
            WeaponData = prototype.Weapon != null ? Weapon.FromFields(prototype.Weapon) : null;
            AmmoItemType = prototype.AmmoItemType;
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
            SourceWorth = source.SourceWorth;
            MaximumHitPoints = source.MaximumHitPoints;
            HitPointDamage = source.HitPointDamage;
            RetailPriceMultiplier = source.RetailPriceMultiplier;
            InventorySourceId = source.InventorySourceId;
            SubstituteInventoryIdentity = source.SubstituteInventoryIdentity;
            NpcFlags = source.NpcFlags;
            BuyObjectScriptNum = source.BuyObjectScriptNum;
            ContainerFlags = source.ContainerFlags;
            ArtId = source.ArtId;
            Off = source.Off;
            Locked = source.Locked;
            UseScriptNum = source.UseScriptNum;
            DialogNum = source.DialogNum;
            ItemFlags = source.ItemFlags;
            InventoryArtId = source.InventoryArtId;
            WeaponFlags = source.WeaponFlags;
            WeaponData = source.WeaponData?.Clone();
            AmmoItemType = source.AmmoItemType;
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

        internal PersistentObjectState(ArcanumObjectId identity, ArcanumObjectId authoredParentIdentity,
            string sourceSector, ObjectType type, int prototypeNumber, int nameIndex, int socialClass,
            int sourceWorth, int maximumHitPoints, int hitPointDamage, int retailPriceMultiplier,
            int inventorySourceId, ArcanumObjectId substituteInventoryIdentity, int npcFlags,
            int buyObjectScriptNum, int containerFlags,
            long? authoredLocation, uint artId, bool off, bool locked, int useScriptNum, int dialogNum,
            int itemFlags, uint? inventoryArtId, int weaponFlags, int genericFlags, int unitWeight,
            InventoryFootprint inventoryFootprint, int inventoryLocation, int? stackQuantity, bool portalOpen,
            Vector2 tilePosition, ObjectPlacement placement, bool isRuntimeCreated,
            Weapon weaponData = null, int? ammoItemType = null, bool deathConsequencesProcessed = false)
        {
            Identity = identity;
            AuthoredParentIdentity = authoredParentIdentity;
            SourceSector = sourceSector;
            Type = type;
            PrototypeNumber = prototypeNumber;
            NameIndex = nameIndex;
            SocialClass = socialClass;
            SourceWorth = sourceWorth;
            MaximumHitPoints = maximumHitPoints;
            HitPointDamage = hitPointDamage;
            RetailPriceMultiplier = retailPriceMultiplier;
            InventorySourceId = inventorySourceId;
            SubstituteInventoryIdentity = substituteInventoryIdentity;
            NpcFlags = npcFlags;
            BuyObjectScriptNum = buyObjectScriptNum;
            ContainerFlags = containerFlags;
            AuthoredLocation = authoredLocation;
            ArtId = artId;
            Off = off;
            Locked = locked;
            UseScriptNum = useScriptNum;
            DialogNum = dialogNum;
            ItemFlags = itemFlags;
            InventoryArtId = inventoryArtId;
            WeaponFlags = weaponFlags;
            WeaponData = weaponData?.Clone();
            AmmoItemType = ammoItemType;
            GenericFlags = genericFlags;
            UnitWeight = unitWeight;
            InventoryFootprint = inventoryFootprint;
            InventoryLocation = inventoryLocation;
            StackQuantity = ValidateStackQuantity(type, stackQuantity);
            PortalOpen = portalOpen;
            TilePosition = tilePosition;
            Placement = placement;
            IsRuntimeCreated = isRuntimeCreated;
            DeathConsequencesProcessed = deathConsequencesProcessed;
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
