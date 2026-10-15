using System;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.Social;
using Arcanum.Script;

namespace Arcanum.Runtime.World
{
    /// <summary>Shared source-authoritative backend for production world and dialogue script hosts.</summary>
    internal class WorldScriptCampaignHost : ScriptHostAdapter, IScriptCampaignHost
    {
        private const long DayMilliseconds = 86_400_000L;
        private const long HourMilliseconds = 3_600_000L;
        private const long MinuteMilliseconds = 60_000L;

        protected readonly WorldMapSessionCoordinator Session;
        private readonly Func<int, ScriptFile> _resolveScript;

        protected WorldScriptCampaignHost(WorldMapSessionCoordinator session,
            Func<int, ScriptFile> resolveScript = null)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _resolveScript = resolveScript;
        }

        public override object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext context)
        {
            return (Sfo)sfoType switch
            {
                Sfo.Triggerer => One(context.Triggerer),
                Sfo.Attachee => One(context.Attachee),
                Sfo.ExtraObject => One(context.Extra),
                Sfo.Player => Session.PlayerState == null ? Array.Empty<object>()
                    : One(new WorldScriptObjectReference(Session.PlayerState.Identity)),
                _ => throw Unsupported($"ResolveFocus({(Sfo)sfoType})"),
            };
        }

        public override ScriptFile GetScript(int num)
            => _resolveScript?.Invoke(num) ?? throw Unsupported(nameof(GetScript));

        public int GetObjectType(object obj)
        {
            ArcanumObjectId identity = Identity(obj);
            if (Session.PlayerState?.Identity == identity) return (int)ObjectType.Pc;
            return (int)State(identity).Type;
        }

        public int GetMagicTechAdjustmentEx(int value, object item, object source)
        {
            PersistentObjectState itemState = State(Identity(item));
            int complexity = Session.ResolvePrototype(itemState.PrototypeNumber)?.ItemComplexity ?? 0;
            int effectiveness;
            if (complexity == 0 || !Session.Characters.TryGet(Identity(source), out _))
                effectiveness = 100;
            else
                effectiveness = 100 * Session.Technology.GetItemEffectivePower(itemState, Identity(source)) / complexity;
            return checked((value * effectiveness + 50) / 100);
        }

        public int GetMagicTechAdjustment(int value, object item, object source, object target)
        {
            PersistentObjectState itemState = State(Identity(item));
            int complexity = Session.ResolvePrototype(itemState.PrototypeNumber)?.ItemComplexity ?? 0;
            if (!Session.Characters.TryGet(Identity(source), out _)
                || !Session.Characters.TryGet(Identity(target), out _)) return value;
            int sourceAptitude = Session.DerivedStats.GetDerivedStat(Identity(source),
                CharacterDerivedStat.MagickTechAptitude);
            int targetAptitude = Session.DerivedStats.GetDerivedStat(Identity(target),
                CharacterDerivedStat.MagickTechAptitude);
            int ratio = 0;
            if (complexity > 0 && targetAptitude < 0)
                ratio = sourceAptitude < 0 ? -targetAptitude : -targetAptitude * (100 - sourceAptitude) / 100;
            else if (complexity < 0 && targetAptitude > 0)
                ratio = sourceAptitude > 0 ? targetAptitude : targetAptitude * (100 + sourceAptitude) / 100;
            return checked(value * (100 - ratio) / 100);
        }

        public int GetReaction(object npc, object pc)
            => Session.Social.GetReaction(Identity(npc), Identity(pc));

        public void SetReaction(object npc, object pc, int value)
            => Session.Social.SetReaction(Identity(npc), Identity(pc), value);

        public void AdjustReaction(object npc, object pc, int delta)
            => Session.Social.AdjustReaction(Identity(npc), Identity(pc), delta);

        public bool HasReputation(object obj, int reputation)
            => Session.Social.HasReputation(Identity(obj), new ReputationId(reputation));

        public bool IsIdentified(object obj)
        {
            PersistentObjectState state = State(Identity(obj));
            if (!WorldMapSessionCoordinator.IsItemType(state.Type)) return false;
            int complexity = Session.ResolvePrototype(state.PrototypeNumber)?.ItemComplexity ?? 0;
            return complexity <= 0
                || (state.ItemFlags & Arcanum.Runtime.Economy.EconomyStateService.ItemFlagIdentified) != 0;
        }

        public void AddReputation(object obj, int reputation)
            => Session.Social.AddReputation(Identity(obj), new ReputationId(reputation));

        public void RemoveReputation(object obj, int reputation)
            => Session.Social.RemoveReputation(Identity(obj), new ReputationId(reputation));

        public int GetFaction(object obj) => Session.Social.GetFaction(Identity(obj));

        public void GetLocation(object obj, out int x, out int y)
        {
            UnityEngine.Vector2 tile = Position(Identity(obj));
            x = UnityEngine.Mathf.RoundToInt(tile.x);
            y = UnityEngine.Mathf.RoundToInt(tile.y);
        }

        public int DistanceBetween(object left, object right)
        {
            UnityEngine.Vector2 a = Position(Identity(left));
            UnityEngine.Vector2 b = Position(Identity(right));
            return UnityEngine.Mathf.RoundToInt(Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y)));
        }

        public int GetArea(object obj) => Session.ResolveScriptArea(Identity(obj));

        public void GetFatiguePoints(object obj, out int current, out int maximum)
        {
            ArcanumObjectId identity = Identity(obj);
            current = Session.Vitality.GetCurrentFatigue(identity);
            maximum = Session.Vitality.GetMaximumFatigue(identity);
        }

        public void TransferItem(int name, object from, object to)
        {
            ArcanumObjectId source = Identity(from);
            ArcanumObjectId destination = Identity(to);
            if (!Session.TryFindContainedItemByName(source, name, out PersistentObjectState item)) return;
            InventoryTransferResult result = Session.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(destination));
            if (!result.Succeeded) throw new InvalidOperationException($"Script item transfer failed: {result.Code}.");
        }

        public void CreateItem(int prototype, object parent)
        {
            ItemCreationResult result = Session.CreateItem(prototype, ObjectPlacement.ContainedBy(Identity(parent)));
            if (!result.Succeeded) throw new InvalidOperationException($"Script item creation failed: {result.Code}.");
        }

        public void Destroy(object obj)
        {
            if (!Session.DestroyScriptObject(Identity(obj)))
                throw new InvalidOperationException("Script destroy target is not a persistent world object.");
        }

        public void DestroyItemNamed(int name, object owner)
        {
            if (Session.TryFindContainedItemByName(Identity(owner), name, out PersistentObjectState item))
                Destroy(new WorldScriptObjectReference(item.Identity));
        }

        public int GetDaySinceStartup() => checked((int)(CampaignMilliseconds / DayMilliseconds));
        public int GetCurrentHour() => (int)(CampaignMilliseconds / HourMilliseconds % 24);
        public int GetCurrentMinute() => (int)(CampaignMilliseconds / MinuteMilliseconds % 60);
        public int GetHoursSinceStartup() => checked((int)(CampaignMilliseconds / HourMilliseconds));
        public void AdvanceTimeSeconds(int seconds) => Session.SourceTime.Advance(checked(seconds * 1_000));

        public void ScheduleScriptCall(int scriptNumber, int startLine, object triggerer, object attachee,
            int seconds, bool atTimeOfDay)
            => Session.ScriptTimers.Schedule(scriptNumber, startLine, Identity(triggerer), Identity(attachee),
                seconds, atTimeOfDay);

        public void RemoveScriptCall(int scriptNumber, object attachee)
            => Session.ScriptTimers.RemoveFirst(scriptNumber, Identity(attachee));

        public void GrantOneFatePoint(object obj)
        {
            if (Session.PlayerState?.Identity == Identity(obj)) Session.GameplayHud.GrantFatePoint();
        }

        protected static object[] One(object value) => value == null ? Array.Empty<object>() : new[] { value };

        private long CampaignMilliseconds => Session.SourceTime.CampaignMilliseconds;

        private static ArcanumObjectId Identity(object value)
            => value is WorldScriptObjectReference reference
                ? reference.Identity
                : throw new InvalidOperationException($"Script object '{value ?? "null"}' has no persistent identity.");

        private PersistentObjectState State(ArcanumObjectId identity)
            => Session.TryGetObjectState(identity, out PersistentObjectState state)
                ? state
                : throw new InvalidOperationException($"Script object '{identity}' is not a persistent world object.");

        private UnityEngine.Vector2 Position(ArcanumObjectId identity)
        {
            if (Session.PlayerState?.Identity == identity) return Session.PlayerState.MapPosition;
            return State(identity).TilePosition;
        }
    }
}
