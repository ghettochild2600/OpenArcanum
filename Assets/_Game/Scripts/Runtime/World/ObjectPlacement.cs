using System;
using Arcanum.Formats.Objects;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    public enum ObjectPlacementKind
    {
        World,
        Contained,
    }

    /// <summary>Authoritative session placement. Containment is never inferred from a Unity hierarchy.</summary>
    public readonly struct ObjectPlacement : IEquatable<ObjectPlacement>
    {
        public ObjectPlacementKind Kind { get; }
        public string Sector { get; }
        public Vector2 TilePosition { get; }
        public ArcanumObjectId ParentIdentity { get; }

        private ObjectPlacement(ObjectPlacementKind kind, string sector, Vector2 tilePosition,
            ArcanumObjectId parentIdentity)
        {
            Kind = kind;
            Sector = sector;
            TilePosition = tilePosition;
            ParentIdentity = parentIdentity;
        }

        public static ObjectPlacement InWorld(string sector, Vector2 tilePosition)
            => new(ObjectPlacementKind.World, WorldMapSessionCoordinator.NormalizeSector(sector), tilePosition, default);

        public static ObjectPlacement ContainedBy(ArcanumObjectId parentIdentity)
            => new(ObjectPlacementKind.Contained, null, default, parentIdentity);

        public bool Equals(ObjectPlacement other)
            => Kind == other.Kind
               && (Kind == ObjectPlacementKind.World
                   ? string.Equals(Sector, other.Sector, StringComparison.Ordinal)
                     && TilePosition == other.TilePosition
                   : ParentIdentity == other.ParentIdentity);

        public override bool Equals(object obj) => obj is ObjectPlacement other && Equals(other);
        public override int GetHashCode()
            => Kind == ObjectPlacementKind.World
                ? HashCode.Combine((int)Kind, Sector, TilePosition)
                : HashCode.Combine((int)Kind, ParentIdentity);
        public override string ToString()
            => Kind == ObjectPlacementKind.World
                ? $"World({Sector}@{TilePosition.x},{TilePosition.y})"
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
}
