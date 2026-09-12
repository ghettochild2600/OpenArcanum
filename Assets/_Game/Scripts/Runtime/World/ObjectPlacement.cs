using System;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    public enum ObjectPlacementKind
    {
        World,
        Contained,
        Equipped,
    }

    /// <summary>Source <c>ITEM_INV_LOC_*</c> values. Numeric values are part of the Arcanum data contract.</summary>
    public enum WornLocation
    {
        Helmet = 1000,
        Ring1 = 1001,
        Ring2 = 1002,
        Medallion = 1003,
        Weapon = 1004,
        Shield = 1005,
        Armor = 1006,
        Gauntlet = 1007,
        Boots = 1008,
    }

    public static class WornLocations
    {
        public const int First = (int)WornLocation.Helmet;
        public const int Last = (int)WornLocation.Boots;

        public static bool IsValid(WornLocation location)
            => (int)location >= First && (int)location <= Last;

        public static bool TryFromSource(int value, out WornLocation location)
        {
            location = (WornLocation)value;
            return IsValid(location);
        }
    }

    /// <summary>Authoritative session placement. Containment is never inferred from a Unity hierarchy.</summary>
    public readonly struct ObjectPlacement : IEquatable<ObjectPlacement>
    {
        public ObjectPlacementKind Kind { get; }
        public string Sector { get; }
        public Vector2 TilePosition { get; }
        public ArcanumObjectId ParentIdentity { get; }
        public WornLocation WornLocation { get; }

        private ObjectPlacement(ObjectPlacementKind kind, string sector, Vector2 tilePosition,
            ArcanumObjectId parentIdentity, WornLocation wornLocation = default)
        {
            Kind = kind;
            Sector = sector;
            TilePosition = tilePosition;
            ParentIdentity = parentIdentity;
            WornLocation = wornLocation;
        }

        public static ObjectPlacement InWorld(string sector, Vector2 tilePosition)
            => new(ObjectPlacementKind.World, WorldMapSessionCoordinator.NormalizeSector(sector), tilePosition, default);

        public static ObjectPlacement ContainedBy(ArcanumObjectId parentIdentity)
            => new(ObjectPlacementKind.Contained, null, default, parentIdentity);

        public static ObjectPlacement EquippedBy(ArcanumObjectId parentIdentity, WornLocation wornLocation)
        {
            if (!WornLocations.IsValid(wornLocation))
                throw new ArgumentOutOfRangeException(nameof(wornLocation), wornLocation,
                    "Unknown source worn location.");
            return new ObjectPlacement(ObjectPlacementKind.Equipped, null, default, parentIdentity, wornLocation);
        }

        public bool Equals(ObjectPlacement other)
            => Kind == other.Kind
               && (Kind == ObjectPlacementKind.World
                   ? string.Equals(Sector, other.Sector, StringComparison.Ordinal)
                     && TilePosition == other.TilePosition
                   : ParentIdentity == other.ParentIdentity
                     && (Kind != ObjectPlacementKind.Equipped || WornLocation == other.WornLocation));

        public override bool Equals(object obj) => obj is ObjectPlacement other && Equals(other);
        public override int GetHashCode()
            => Kind == ObjectPlacementKind.World
                ? HashCode.Combine((int)Kind, Sector, TilePosition)
                : Kind == ObjectPlacementKind.Equipped
                    ? HashCode.Combine((int)Kind, ParentIdentity, WornLocation)
                    : HashCode.Combine((int)Kind, ParentIdentity);
        public override string ToString()
            => Kind == ObjectPlacementKind.World
                ? $"World({Sector}@{TilePosition.x},{TilePosition.y})"
                : Kind == ObjectPlacementKind.Equipped
                    ? $"Equipped({ParentIdentity},{WornLocation}:{(int)WornLocation})"
                    : $"Contained({ParentIdentity})";
        public static bool operator ==(ObjectPlacement left, ObjectPlacement right) => left.Equals(right);
        public static bool operator !=(ObjectPlacement left, ObjectPlacement right) => !left.Equals(right);
    }

    public enum InventoryResultCode
    {
        Success,
        ItemNotFound,
        InvalidItemType,
        SourceMismatch,
        InvalidDestination,
        ParentNotFound,
        InvalidParentType,
        SelfParent,
        CycleDetected,
        AlreadyAtDestination,
        PrototypeSourceUnavailable,
        PrototypeNotFound,
        IdentityExhausted,
        EquipmentCommandRequired,
    }

    public readonly struct InventoryTransferResult
    {
        public InventoryResultCode Code { get; }
        public ArcanumObjectId ItemIdentity { get; }
        public ObjectPlacement Previous { get; }
        public ObjectPlacement Destination { get; }
        public bool Succeeded => Code == InventoryResultCode.Success;

        internal InventoryTransferResult(InventoryResultCode code, ArcanumObjectId itemIdentity,
            ObjectPlacement previous, ObjectPlacement destination)
        {
            Code = code;
            ItemIdentity = itemIdentity;
            Previous = previous;
            Destination = destination;
        }
    }

    public readonly struct ItemCreationResult
    {
        public InventoryResultCode Code { get; }
        public PersistentObjectState State { get; }
        public bool Succeeded => Code == InventoryResultCode.Success;

        internal ItemCreationResult(InventoryResultCode code, PersistentObjectState state = null)
        {
            Code = code;
            State = state;
        }
    }

    public enum EquipmentResultCode
    {
        Success,
        ActorNotFound,
        InvalidActorType,
        ItemNotFound,
        InvalidItemType,
        ItemNotOwned,
        InvalidWornLocation,
        IncompatibleWornLocation,
        NoFreeHand,
        NotRemovable,
        AlreadyEquipped,
        SlotEmpty,
    }

    /// <summary>Result of one all-or-nothing equipment placement transaction.</summary>
    public readonly struct EquipmentTransactionResult
    {
        public EquipmentResultCode Code { get; }
        public ArcanumObjectId ActorIdentity { get; }
        public ArcanumObjectId ItemIdentity { get; }
        public ArcanumObjectId DisplacedItemIdentity { get; }
        public WornLocation WornLocation { get; }
        public bool Succeeded => Code == EquipmentResultCode.Success;

        internal EquipmentTransactionResult(EquipmentResultCode code, ArcanumObjectId actorIdentity,
            ArcanumObjectId itemIdentity, WornLocation wornLocation, ArcanumObjectId displacedItemIdentity = default)
        {
            Code = code;
            ActorIdentity = actorIdentity;
            ItemIdentity = itemIdentity;
            WornLocation = wornLocation;
            DisplacedItemIdentity = displacedItemIdentity;
        }
    }
}
