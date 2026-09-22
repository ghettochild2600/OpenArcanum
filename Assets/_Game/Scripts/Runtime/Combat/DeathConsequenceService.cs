using System;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Combat
{
    public enum DeathConsequenceFailure
    {
        None,
        VictimNotFound,
        VictimNotNpc,
        VictimNotDead,
        InvalidKiller,
        AlreadyProcessed,
        SourceNotFound,
        UnsupportedDeathScript,
    }

    public readonly struct DeathConsequenceResult
    {
        public bool Succeeded => Failure == DeathConsequenceFailure.None;
        public DeathConsequenceFailure Failure { get; }
        public ArcanumObjectId Killer { get; }
        public ArcanumObjectId Victim { get; }
        public int DeathScriptNumber { get; }
        public int ExperienceWorth { get; }
        public int ExperienceAwarded { get; }

        internal DeathConsequenceResult(DeathConsequenceFailure failure, ArcanumObjectId killer,
            ArcanumObjectId victim, int deathScriptNumber = 0, int experienceWorth = 0,
            int experienceAwarded = 0)
        {
            Failure = failure;
            Killer = killer;
            Victim = victim;
            DeathScriptNumber = deathScriptNumber;
            ExperienceWorth = experienceWorth;
            ExperienceAwarded = experienceAwarded;
        }
    }

    public enum CorpseLootFailure
    {
        None,
        CorpseNotFound,
        CorpseNotDead,
        CorpseUnavailable,
        ItemNotFound,
        ItemNotOwnedByCorpse,
        TransferFailed,
    }

    public readonly struct CorpseLootResult
    {
        public bool Succeeded => Failure == CorpseLootFailure.None;
        public CorpseLootFailure Failure { get; }
        public InventoryTransferResult Transfer { get; }

        internal CorpseLootResult(CorpseLootFailure failure, InventoryTransferResult transfer = default)
        {
            Failure = failure;
            Transfer = transfer;
        }
    }

    /// <summary>
    /// Coordinator-owned exact-once default death consequences. Scripted deaths are an explicit deferred boundary.
    /// </summary>
    public sealed class DeathConsequenceService
    {
        private readonly WorldMapSessionCoordinator _world;

        public DeathConsequenceService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public DeathConsequenceResult Process(ArcanumObjectId killer, ArcanumObjectId victim)
        {
            if (!_world.TryGetObjectState(victim, out PersistentObjectState victimState))
                return Fail(DeathConsequenceFailure.VictimNotFound, killer, victim);
            if (victimState.Type != ObjectType.Npc)
                return Fail(DeathConsequenceFailure.VictimNotNpc, killer, victim);
            if (!_world.Vitality.IsDead(victim))
                return Fail(DeathConsequenceFailure.VictimNotDead, killer, victim);
            if (_world.PlayerState == null || killer != _world.PlayerState.Identity)
                return Fail(DeathConsequenceFailure.InvalidKiller, killer, victim);
            if (victimState.DeathConsequencesProcessed)
                return Fail(DeathConsequenceFailure.AlreadyProcessed, killer, victim);
            if (!_world.Combat.TryGetActorSource(victim, out CombatActorSource victimSource))
                return Fail(DeathConsequenceFailure.SourceNotFound, killer, victim);
            if (victimSource.DyingScriptNum != 0)
                return new DeathConsequenceResult(DeathConsequenceFailure.UnsupportedDeathScript, killer, victim,
                    victimSource.DyingScriptNum, victimSource.ExperienceWorth);

            int award = 0;
            CharacterProgressionService.Snapshot progressionSnapshot = _world.Progression.CaptureSnapshot();
            try
            {
                if (_world.PlayerState != null && killer == _world.PlayerState.Identity)
                {
                    award = checked((int)(20L * victimSource.ExperienceWorth / 100L));
                    if (award > 0) _world.Progression.AwardExperience(killer, award);
                }
                victimState.DeathConsequencesProcessed = true;
            }
            catch
            {
                _world.Progression.RestoreSnapshot(progressionSnapshot);
                victimState.DeathConsequencesProcessed = false;
                throw;
            }

            return new DeathConsequenceResult(DeathConsequenceFailure.None, killer, victim, 0,
                victimSource.ExperienceWorth, award);
        }

        public CorpseLootResult LootItem(ArcanumObjectId corpse, ArcanumObjectId looter,
            ArcanumObjectId itemIdentity)
        {
            if (!_world.TryGetObjectState(corpse, out _))
                return new CorpseLootResult(CorpseLootFailure.CorpseNotFound);
            if (!_world.Vitality.IsDead(corpse))
                return new CorpseLootResult(CorpseLootFailure.CorpseNotDead);
            if (!_world.TryGetLoadedObject(corpse, out _))
                return new CorpseLootResult(CorpseLootFailure.CorpseUnavailable);
            if (!_world.TryGetObjectState(itemIdentity, out PersistentObjectState item))
                return new CorpseLootResult(CorpseLootFailure.ItemNotFound);
            if (item.ParentIdentity != corpse
                || item.Placement.Kind is not (ObjectPlacementKind.Contained or ObjectPlacementKind.Equipped))
                return new CorpseLootResult(CorpseLootFailure.ItemNotOwnedByCorpse);

            InventoryTransferResult transfer = _world.TransferCorpseOwnedItem(itemIdentity, corpse, looter);
            return new CorpseLootResult(transfer.Succeeded ? CorpseLootFailure.None : CorpseLootFailure.TransferFailed,
                transfer);
        }

        private static DeathConsequenceResult Fail(DeathConsequenceFailure failure, ArcanumObjectId killer,
            ArcanumObjectId victim) => new(failure, killer, victim);
    }
}
