using Arcanum.Formats.Art;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Script;
using Arcanum.Script;
using Arcanum.World;

namespace Arcanum.Runtime.World
{
    public enum BedUseFailure
    {
        None,
        NoPlayer,
        Dead,
        Unconscious,
        CombatActive,
        TargetUnavailable,
        NotBed,
        PermissionDenied,
        Occupied,
        ScriptFailure,
    }

    public readonly struct BedUseResult
    {
        public bool Succeeded => Failure == BedUseFailure.None;
        public BedUseFailure Failure { get; }
        public int ScriptNum { get; }
        public ScriptExecutionStatus? ScriptStatus { get; }
        public bool? ScriptRunDefault { get; }

        internal BedUseResult(BedUseFailure failure, int scriptNum = 0,
            ScriptExecutionStatus? scriptStatus = null, bool? scriptRunDefault = null)
        {
            Failure = failure;
            ScriptNum = scriptNum;
            ScriptStatus = scriptStatus;
            ScriptRunDefault = scriptRunDefault;
        }
    }

    /// <summary>
    /// Session-owned implementation of the source bed boundary in <c>anim.c</c>,
    /// <c>sleep_ui_toggle</c>, and <c>critter_enter_bed</c>. A bed is scenery ART subtype 22;
    /// its SAP_USE script must suppress default behavior, then the occupancy handle is claimed.
    /// </summary>
    public sealed class BedSleepAuthority
    {
        public const int SourceBedScenerySubtype = 22;
        private readonly WorldMapSessionCoordinator _world;

        public ArcanumObjectId ActiveBed { get; private set; }

        internal BedSleepAuthority(WorldMapSessionCoordinator world) => _world = world;

        public static bool IsBed(PersistentObjectState state)
            => state != null && state.Type == ObjectType.Scenery
               && ArtId.Type(state.ArtId) == ArtId.TypeScenery
               && SceneryArtResolver.SubType(state.ArtId) == SourceBedScenerySubtype;

        public bool IsActiveFor(ArcanumObjectId actor)
            => !ActiveBed.IsNull
               && _world.TryGetObjectState(ActiveBed, out PersistentObjectState bed)
               && bed.SceneryOccupantIdentity == actor;

        public BedUseResult Begin(ArcanumObjectId actor, ArcanumObjectId bedIdentity)
        {
            if (_world.PlayerState == null || _world.PlayerState.Identity != actor)
                return new BedUseResult(BedUseFailure.NoPlayer);
            if (!_world.Vitality.TryGet(actor, out var vitality) || vitality.CurrentHitPoints <= 0)
                return new BedUseResult(BedUseFailure.Dead);
            if (vitality.CurrentFatigue <= 0)
                return new BedUseResult(BedUseFailure.Unconscious);
            if (_world.Combat.IsActive)
                return new BedUseResult(BedUseFailure.CombatActive);
            if (!_world.TryGetObjectState(bedIdentity, out PersistentObjectState bed)
                || bed.Off || !_world.TryGetLoadedObject(bedIdentity, out _))
                return new BedUseResult(BedUseFailure.TargetUnavailable);
            if (!IsBed(bed)) return new BedUseResult(BedUseFailure.NotBed);

            int scriptNum = _world.Campaign.GetScriptNumber(bedIdentity, (int)Sap.Use, bed.UseScriptNum);
            if (scriptNum == 0 || _world.UseScripts == null)
                return new BedUseResult(BedUseFailure.PermissionDenied, scriptNum);
            ScriptExecutionResult script = _world.UseScripts.DispatchBedUse(actor, bedIdentity, scriptNum);
            if (!script.Succeeded)
                return new BedUseResult(BedUseFailure.ScriptFailure, scriptNum, script.Status, false);
            if (script.RunDefault)
                return new BedUseResult(BedUseFailure.PermissionDenied, scriptNum, script.Status, true);

            // sleep_ui checks SAP_USE before critter_enter_bed, so an occupied rented bed preserves any
            // script-local mutations but cannot be claimed.
            if (!bed.SceneryOccupantIdentity.IsNull && bed.SceneryOccupantIdentity != actor)
                return new BedUseResult(BedUseFailure.Occupied, scriptNum, script.Status, false);
            if (!ActiveBed.IsNull && ActiveBed != bedIdentity) Leave(actor);
            bed.SceneryOccupantIdentity = actor;
            ActiveBed = bedIdentity;
            SetOccupiedFrame(bed, true);
            return new BedUseResult(BedUseFailure.None, scriptNum, script.Status, false);
        }

        public void Leave(ArcanumObjectId actor)
        {
            if (ActiveBed.IsNull) return;
            if (_world.TryGetObjectState(ActiveBed, out PersistentObjectState bed)
                && bed.SceneryOccupantIdentity == actor)
            {
                bed.SceneryOccupantIdentity = default;
                SetOccupiedFrame(bed, false);
            }
            ActiveBed = default;
        }

        internal void ResetTransient() => ActiveBed = default;

        internal void RestoreTransient(ArcanumObjectId actor)
        {
            ActiveBed = default;
            foreach (PersistentObjectState state in _world.States.Values)
            {
                if (state.SceneryOccupantIdentity != actor || !IsBed(state)) continue;
                ActiveBed = state.Identity;
                return;
            }
        }

        private void SetOccupiedFrame(PersistentObjectState bed, bool occupied)
        {
            int frame = (int)((bed.ArtId >> 14) & 31u);
            int next = occupied ? frame + 1 : frame - 1;
            if (next < 0 || next > 31) return;
            bed.ArtId = (bed.ArtId & ~(31u << 14)) | ((uint)next << 14);
            if (_world.TryGetLoadedObject(bed.Identity, out WorldObject runtime))
                runtime.ApplyMovementState(bed.TilePosition, bed.ArtId, false);
        }
    }
}
