using Arcanum.Formats.Objects;
using Arcanum.Script;
using UnityEngine;

namespace Arcanum.Runtime.World
{
    public enum WorldInteractionCommandType { Use }

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
    }

    /// <summary>One immutable gameplay command identified only by persistent domain identities.</summary>
    public readonly struct WorldInteractionCommand
    {
        public ArcanumObjectId Actor { get; }
        public ArcanumObjectId Target { get; }
        public WorldInteractionCommandType Type { get; }
        public Vector2Int? InteractionPosition { get; }

        public WorldInteractionCommand(ArcanumObjectId actor, ArcanumObjectId target,
            WorldInteractionCommandType type, Vector2Int? interactionPosition = null)
        {
            Actor = actor;
            Target = target;
            Type = type;
            InteractionPosition = interactionPosition;
        }

        public WorldInteractionCommand At(Vector2Int interactionPosition)
            => new(Actor, Target, Type, interactionPosition);
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
        public bool IsSuccess => Code == WorldInteractionResultCode.Success;
        public bool IsAccepted => IsSuccess || Code == WorldInteractionResultCode.Approaching;

        public WorldInteractionResult(WorldInteractionCommand command, WorldInteractionResultCode code,
            bool? requestedPortalOpen = null, int scriptNum = 0,
            ScriptExecutionStatus? scriptStatus = null, bool? scriptRunDefault = null)
        {
            Command = command;
            Code = code;
            RequestedPortalOpen = requestedPortalOpen;
            ScriptNum = scriptNum;
            ScriptStatus = scriptStatus;
            ScriptRunDefault = scriptRunDefault;
        }
    }

    public static class InteractionRangeRules
    {
        // anim.c sub_428930: ordinary portal/scenery AG_USE_OBJECT range.
        public const int PortalUseRange = 2;

        public static int Distance(Vector2 a, Vector2 b)
            => Mathf.CeilToInt(Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)) - 0.00001f);

        public static bool IsWithin(Vector2 actor, Vector2 target, int range)
            => Distance(actor, target) <= range;
    }
}
