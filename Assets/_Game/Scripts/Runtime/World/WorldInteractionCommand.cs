using Arcanum.Formats.Objects;
using Arcanum.Script;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    public enum WorldInteractionCommandType { Use, PickUp, Drop, Transfer, Talk, Loot }

    public enum WorldInteractionResultCode
    {
        Success,
        Approaching,
        ActorNotFound,
        TargetNotFound,
        OutOfRange,
        NoReachableInteractionPosition,
        InvalidTarget,
        Blocked,
        Unsupported,
        Cancelled,
        ScriptUnavailable,
        ScriptMissing,
        ScriptUnsupported,
        ScriptFailed,
        ItemNotFound,
        InvalidItem,
        AlreadyContained,
        ItemNotInWorld,
        SourceOwnerMismatch,
        InvalidDestination,
        NotDroppable,
        TransferFailed,
        TooHeavy,
        NoRoom,
        DialogueBusy,
        DialogueMissing,
        DialogueUnsupported,
        DialogueFailed,
        TravelFailed,
    }

    /// <summary>One immutable gameplay command identified only by persistent domain identities.</summary>
    public readonly struct WorldInteractionCommand
    {
        public ArcanumObjectId Actor { get; }
        public ArcanumObjectId Target { get; }
        public WorldInteractionCommandType Type { get; }
        public Vector2Int? InteractionPosition { get; }
        public ObjectPlacement? InventoryDestination { get; }

        public WorldInteractionCommand(ArcanumObjectId actor, ArcanumObjectId target,
            WorldInteractionCommandType type, Vector2Int? interactionPosition = null,
            ObjectPlacement? inventoryDestination = null)
        {
            Actor = actor;
            Target = target;
            Type = type;
            InteractionPosition = interactionPosition;
            InventoryDestination = inventoryDestination;
        }

        public WorldInteractionCommand At(Vector2Int interactionPosition)
            => new(Actor, Target, Type, interactionPosition, InventoryDestination);

        public static WorldInteractionCommand PickUp(ArcanumObjectId actor, ArcanumObjectId item)
            => new(actor, item, WorldInteractionCommandType.PickUp);

        public static WorldInteractionCommand Drop(ArcanumObjectId actor, ArcanumObjectId item,
            string sector, Vector2 tilePosition)
            => new(actor, item, WorldInteractionCommandType.Drop,
                inventoryDestination: ObjectPlacement.InWorld(sector, tilePosition));

        public static WorldInteractionCommand Transfer(ArcanumObjectId actor, ArcanumObjectId item,
            ArcanumObjectId destinationOwner)
            => new(actor, item, WorldInteractionCommandType.Transfer,
                inventoryDestination: ObjectPlacement.ContainedBy(destinationOwner));
    }

    /// <summary>Deterministic terminal or accepted result for a world interaction command.</summary>
    public readonly struct WorldInteractionResult
    {
        public WorldInteractionCommand Command { get; }
        public WorldInteractionResultCode Code { get; }
        public bool? RequestedPortalOpen { get; }
        public int ScriptNum { get; }
        public ScriptExecutionStatus? ScriptStatus { get; }
        public bool? ScriptRunDefault { get; }
        public InventoryResultCode? InventoryStatus { get; }
        public AreaEntranceResult? AreaEntrance { get; }
        public bool IsSuccess => Code == WorldInteractionResultCode.Success;
        public bool IsAccepted => IsSuccess || Code == WorldInteractionResultCode.Approaching;

        public WorldInteractionResult(WorldInteractionCommand command, WorldInteractionResultCode code,
            bool? requestedPortalOpen = null, int scriptNum = 0,
            ScriptExecutionStatus? scriptStatus = null, bool? scriptRunDefault = null,
            InventoryResultCode? inventoryStatus = null, AreaEntranceResult? areaEntrance = null)
        {
            Command = command;
            Code = code;
            RequestedPortalOpen = requestedPortalOpen;
            ScriptNum = scriptNum;
            ScriptStatus = scriptStatus;
            ScriptRunDefault = scriptRunDefault;
            InventoryStatus = inventoryStatus;
            AreaEntrance = areaEntrance;
        }
    }

    public static class InteractionRangeRules
    {
        // anim.c sub_428930: ordinary portal/scenery AG_USE_OBJECT range.
        public const int PortalUseRange = 2;

        // anim.c AG_PICKUP_ITEM sets AGDATA_RANGE_DATA to 0 before AG_MOVE_NEAR_OBJ.
        public const int ItemPickupRange = 0;

        // anim.c AG_TALK tests distance < ai_max_dialog_distance; ai.c returns 5 for a PC.
        public const int TalkStartRange = 4;

        // Out-of-range AG_TALK schedules AG_MOVE_NEAR_OBJ with range 1.
        public const int TalkApproachRange = 1;

        // Corpse inventory is opened from an adjacent interaction position.
        public const int CorpseLootRange = 1;

        public static int For(WorldInteractionCommandType type)
            => type switch
            {
                WorldInteractionCommandType.PickUp => ItemPickupRange,
                WorldInteractionCommandType.Talk => TalkStartRange,
                WorldInteractionCommandType.Loot => CorpseLootRange,
                _ => PortalUseRange,
            };

        public static int Distance(Vector2 a, Vector2 b)
            => Mathf.CeilToInt(Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)) - 0.00001f);

        public static bool IsWithin(Vector2 actor, Vector2 target, int range)
            => Distance(actor, target) <= range;
    }
}
