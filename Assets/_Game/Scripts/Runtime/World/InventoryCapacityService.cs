using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;

namespace Arcanum.Runtime.World
{
    /// <summary>Inventory ART footprint measured in the source engine's 32-pixel grid cells.</summary>
    public readonly struct InventoryFootprint : IEquatable<InventoryFootprint>
    {
        public static InventoryFootprint OneCell { get; } = new(1, 1);

        public int Width { get; }
        public int Height { get; }
        public int Cells => Width * Height;

        public InventoryFootprint(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        public bool Equals(InventoryFootprint other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is InventoryFootprint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Width, Height);
        public override string ToString() => $"{Width}x{Height}";
        public static bool operator ==(InventoryFootprint left, InventoryFootprint right) => left.Equals(right);
        public static bool operator !=(InventoryFootprint left, InventoryFootprint right) => !left.Equals(right);
    }

    /// <summary>
    /// Source-faithful item load and inventory-grid rules over session-owned state. Unity hierarchy and sprites are
    /// deliberately absent from this service.
    /// </summary>
    public sealed class InventoryCapacityService
    {
        public const int GridColumns = 10;
        public const int CritterGridRows = 12;
        public const int ContainerGridRows = 96;
        public const int MinimumCarryCapacity = 300;
        public const int MaximumCarryCapacity = 10000;

        private readonly WorldMapSessionCoordinator _session;

        internal InventoryCapacityService(WorldMapSessionCoordinator session)
            => _session = session ?? throw new ArgumentNullException(nameof(session));

        /// <summary>Raw effective OBJ_F_ITEM_WEIGHT after instance-over-prototype resolution.</summary>
        public int GetUnitWeight(ArcanumObjectId itemIdentity) => Item(itemIdentity).UnitWeight;

        /// <summary>Source item_weight result before deferred magic/technology weight adjustment.</summary>
        public long GetTotalWeight(ArcanumObjectId itemIdentity)
        {
            PersistentObjectState item = Item(itemIdentity);
            return TotalWeight(item.Type, item.UnitWeight, item.StackQuantity);
        }

        public long GetInventoryLoad(ArcanumObjectId ownerIdentity)
        {
            RequireOwner(ownerIdentity, out _);
            long total = 0;
            foreach (PersistentObjectState item in _session.States.Values)
                if (item.ParentIdentity == ownerIdentity
                    && item.Placement.Kind is ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped)
                    total = checked(total + TotalWeight(item.Type, item.UnitWeight, item.StackQuantity));
            return total;
        }

        public int GetCarryCapacity(ArcanumObjectId actorIdentity)
        {
            RequireOwner(actorIdentity, out ObjectType type);
            if (type is not ObjectType.Pc and not ObjectType.Npc)
                throw new ArgumentException($"{type} does not have a carry-weight stat.", nameof(actorIdentity));
            int strength = _session.Characters.GetEffectiveAttribute(actorIdentity, CharacterAttribute.Strength);
            return Math.Max(MinimumCarryCapacity, Math.Min(MaximumCarryCapacity, 500 * strength));
        }

        /// <summary>The source engine has no per-container field: every container owns a 10x96 grid.</summary>
        public int GetContainerCapacity(ArcanumObjectId containerIdentity)
        {
            RequireOwner(containerIdentity, out ObjectType type);
            if (type != ObjectType.Container)
                throw new ArgumentException($"{type} is not a container.", nameof(containerIdentity));
            return GridColumns * ContainerGridRows;
        }

        public int GetContainerLoad(ArcanumObjectId containerIdentity)
        {
            RequireOwner(containerIdentity, out ObjectType type);
            if (type != ObjectType.Container)
                throw new ArgumentException($"{type} is not a container.", nameof(containerIdentity));
            return CountOccupiedCells(BuildGrid(containerIdentity, ContainerGridRows, default));
        }

        internal InventoryAcceptance Evaluate(PersistentObjectState item, ArcanumObjectId destinationOwner,
            PersistentObjectState compatibleStack = null)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            RequireOwner(destinationOwner, out ObjectType ownerType);
            if (ownerType is ObjectType.Pc or ObjectType.Npc)
            {
                long current = GetInventoryLoad(destinationOwner);
                long incoming = TotalWeight(item.Type, item.UnitWeight, item.StackQuantity);
                if (compatibleStack != null)
                {
                    incoming = checked(TotalWeight(compatibleStack.Type, compatibleStack.UnitWeight,
                        checked(compatibleStack.StackQuantity.Value + item.StackQuantity.Value))
                        - TotalWeight(compatibleStack.Type, compatibleStack.UnitWeight,
                            compatibleStack.StackQuantity));
                }
                if (checked(current + incoming) > GetCarryCapacity(destinationOwner))
                    return InventoryAcceptance.TooHeavy;
            }

            if (compatibleStack != null) return InventoryAcceptance.StackMerge;
            int rows = ownerType == ObjectType.Container ? ContainerGridRows : CritterGridRows;
            int location = FindFirstFit(BuildGrid(destinationOwner, rows, default), rows, item.InventoryFootprint);
            return location >= 0 ? InventoryAcceptance.Accept(location) : InventoryAcceptance.NoRoom;
        }

        internal InventoryAcceptance EvaluatePrototype(ObjectProtoInfo prototype, InventoryFootprint footprint,
            ArcanumObjectId destinationOwner, PersistentObjectState compatibleStack = null)
        {
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));
            RequireOwner(destinationOwner, out ObjectType ownerType);
            int? quantity = prototype.Type switch
            {
                ObjectType.Ammo => prototype.AmmoQuantity,
                ObjectType.Gold => prototype.GoldQuantity,
                _ => null,
            };
            if (ownerType is ObjectType.Pc or ObjectType.Npc)
            {
                long incoming = TotalWeight(prototype.Type, prototype.Weight, quantity);
                if (compatibleStack != null)
                {
                    incoming = checked(TotalWeight(compatibleStack.Type, compatibleStack.UnitWeight,
                        checked(compatibleStack.StackQuantity.Value + quantity.GetValueOrDefault()))
                        - TotalWeight(compatibleStack.Type, compatibleStack.UnitWeight,
                            compatibleStack.StackQuantity));
                }
                if (checked(GetInventoryLoad(destinationOwner) + incoming) > GetCarryCapacity(destinationOwner))
                    return InventoryAcceptance.TooHeavy;
            }
            if (compatibleStack != null) return InventoryAcceptance.StackMerge;
            int rows = ownerType == ObjectType.Container ? ContainerGridRows : CritterGridRows;
            int location = FindFirstFit(BuildGrid(destinationOwner, rows, default), rows, footprint);
            return location >= 0 ? InventoryAcceptance.Accept(location) : InventoryAcceptance.NoRoom;
        }

        internal InventoryAcceptance EvaluateSplit(PersistentObjectState source, int splitQuantity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ArcanumObjectId owner = source.ParentIdentity;
            RequireOwner(owner, out ObjectType ownerType);
            if (ownerType is ObjectType.Pc or ObjectType.Npc)
            {
                long current = GetInventoryLoad(owner);
                long before = TotalWeight(source.Type, source.UnitWeight, source.StackQuantity);
                long after = checked(TotalWeight(source.Type, source.UnitWeight,
                                         source.StackQuantity.Value - splitQuantity)
                                     + TotalWeight(source.Type, source.UnitWeight, splitQuantity));
                if (checked(current - before + after) > GetCarryCapacity(owner))
                    return InventoryAcceptance.TooHeavy;
            }
            int rows = ownerType == ObjectType.Container ? ContainerGridRows : CritterGridRows;
            int location = FindFirstFit(BuildGrid(owner, rows, default), rows, source.InventoryFootprint);
            return location >= 0 ? InventoryAcceptance.Accept(location) : InventoryAcceptance.NoRoom;
        }

        internal bool CanMergeWithinOwner(PersistentObjectState source, PersistentObjectState destination,
            int quantity)
        {
            ArcanumObjectId owner = source.ParentIdentity;
            RequireOwner(owner, out ObjectType ownerType);
            if (ownerType is not ObjectType.Pc and not ObjectType.Npc) return true;
            long current = GetInventoryLoad(owner);
            long before = checked(TotalWeight(source.Type, source.UnitWeight, source.StackQuantity)
                                  + TotalWeight(destination.Type, destination.UnitWeight,
                                      destination.StackQuantity));
            int sourceRemaining = source.StackQuantity.Value - quantity;
            int destinationQuantity = checked(destination.StackQuantity.Value + quantity);
            long after = TotalWeight(destination.Type, destination.UnitWeight, destinationQuantity);
            if (sourceRemaining > 0)
                after = checked(after + TotalWeight(source.Type, source.UnitWeight, sourceRemaining));
            return checked(current - before + after) <= GetCarryCapacity(owner);
        }

        internal int FindInventoryLocation(ArcanumObjectId owner, InventoryFootprint footprint,
            ArcanumObjectId excludedIdentity = default)
        {
            RequireOwner(owner, out ObjectType ownerType);
            int rows = ownerType == ObjectType.Container ? ContainerGridRows : CritterGridRows;
            return FindFirstFit(BuildGrid(owner, rows, excludedIdentity), rows, footprint);
        }

        internal static long TotalWeight(ObjectType type, int unitWeight, int? quantity)
        {
            if (type == ObjectType.Gold) return 0;
            if (type == ObjectType.Ammo)
                return checked((long)unitWeight * (quantity.GetValueOrDefault() / 4));
            return unitWeight;
        }

        private PersistentObjectState Item(ArcanumObjectId identity)
        {
            if (!_session.TryGetObjectState(identity, out PersistentObjectState item))
                throw new KeyNotFoundException($"No authoritative item state exists for {identity}.");
            if (!WorldMapSessionCoordinator.IsItemType(item.Type))
                throw new ArgumentException($"{item.Type} is not an item.", nameof(identity));
            return item;
        }

        private void RequireOwner(ArcanumObjectId identity, out ObjectType type)
        {
            if (_session.PlayerState != null && _session.PlayerState.Identity == identity)
            {
                type = ObjectType.Pc;
                return;
            }
            if (_session.TryGetObjectState(identity, out PersistentObjectState owner)
                && WorldMapSessionCoordinator.IsInventoryOwnerType(owner.Type))
            {
                type = owner.Type;
                return;
            }
            throw new KeyNotFoundException($"No authoritative inventory owner exists for {identity}.");
        }

        private bool[] BuildGrid(ArcanumObjectId ownerIdentity, int rows, ArcanumObjectId excludedIdentity)
        {
            var grid = new bool[GridColumns * rows];
            var contents = new List<PersistentObjectState>();
            foreach (PersistentObjectState item in _session.States.Values)
                if (item.Identity != excludedIdentity
                    && item.Placement.Kind == ObjectPlacementKind.Contained
                    && item.ParentIdentity == ownerIdentity)
                    contents.Add(item);
            contents.Sort((left, right) => string.CompareOrdinal(left.Identity.Key, right.Identity.Key));

            foreach (PersistentObjectState item in contents)
            {
                int location = Fits(grid, rows, item.InventoryLocation, item.InventoryFootprint)
                    ? item.InventoryLocation
                    : FindFirstFit(grid, rows, item.InventoryFootprint);
                if (location >= 0) Mark(grid, location, item.InventoryFootprint);
            }
            return grid;
        }

        private static int CountOccupiedCells(bool[] grid)
        {
            int count = 0;
            foreach (bool occupied in grid) if (occupied) count++;
            return count;
        }

        private static int FindFirstFit(bool[] grid, int rows, InventoryFootprint footprint)
        {
            for (int y = 0; y <= rows - footprint.Height; y++)
                for (int x = 0; x <= GridColumns - footprint.Width; x++)
                {
                    int location = y * GridColumns + x;
                    if (Fits(grid, rows, location, footprint)) return location;
                }
            return -1;
        }

        private static bool Fits(bool[] grid, int rows, int location, InventoryFootprint footprint)
        {
            if (location < 0 || location >= grid.Length) return false;
            int x = location % GridColumns;
            int y = location / GridColumns;
            if (x + footprint.Width > GridColumns || y + footprint.Height > rows) return false;
            for (int dy = 0; dy < footprint.Height; dy++)
                for (int dx = 0; dx < footprint.Width; dx++)
                    if (grid[(y + dy) * GridColumns + x + dx]) return false;
            return true;
        }

        private static void Mark(bool[] grid, int location, InventoryFootprint footprint)
        {
            int x = location % GridColumns;
            int y = location / GridColumns;
            for (int dy = 0; dy < footprint.Height; dy++)
                for (int dx = 0; dx < footprint.Width; dx++)
                    grid[(y + dy) * GridColumns + x + dx] = true;
        }
    }

    internal readonly struct InventoryAcceptance
    {
        public InventoryResultCode Code { get; }
        public int InventoryLocation { get; }
        public bool Succeeded => Code == InventoryResultCode.Success;

        private InventoryAcceptance(InventoryResultCode code, int inventoryLocation = -1)
        {
            Code = code;
            InventoryLocation = inventoryLocation;
        }

        public static InventoryAcceptance Accept(int location) => new(InventoryResultCode.Success, location);
        public static InventoryAcceptance StackMerge => new(InventoryResultCode.Success);
        public static InventoryAcceptance TooHeavy => new(InventoryResultCode.TooHeavy);
        public static InventoryAcceptance NoRoom => new(InventoryResultCode.NoRoom);
    }
}
