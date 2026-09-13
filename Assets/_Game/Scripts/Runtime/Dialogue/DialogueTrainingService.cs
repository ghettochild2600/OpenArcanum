using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;

namespace Arcanum.Runtime.Dialogue
{
    public enum DialogueTrainingFailure
    {
        None,
        InvalidTrainee,
        InvalidTrainer,
        InvalidSkill,
        InvalidTier,
        UnsupportedTier,
        SkillNotOffered,
        AlreadyTrained,
        InsufficientSkillRank,
        NonSequentialTraining,
        DerivedTraining,
        InsufficientGold,
        PaymentFailed,
        AssignmentFailed,
    }

    /// <summary>Typed request at the boundary between a source t: response and character progression.</summary>
    public readonly struct DialogueTrainingRequest
    {
        public ArcanumObjectId Trainee { get; }
        public ArcanumObjectId Trainer { get; }
        public CharacterSkill Skill { get; }
        public SkillTrainingLevel Tier { get; }

        public DialogueTrainingRequest(ArcanumObjectId trainee, ArcanumObjectId trainer,
            CharacterSkill skill, SkillTrainingLevel tier)
        {
            Trainee = trainee;
            Trainer = trainer;
            Skill = skill;
            Tier = tier;
        }
    }

    public readonly struct DialogueTrainingResult
    {
        public DialogueTrainingFailure Failure { get; }
        public DialogueTrainingRequest Request { get; }
        public int Cost { get; }
        public int GoldBefore { get; }
        public int GoldAfter { get; }
        public bool Succeeded => Failure == DialogueTrainingFailure.None;

        internal DialogueTrainingResult(DialogueTrainingFailure failure, DialogueTrainingRequest request,
            int cost = 0, int goldBefore = 0, int goldAfter = 0)
        {
            Failure = failure;
            Request = request;
            Cost = cost;
            GoldBefore = goldBefore;
            GoldAfter = goldAfter;
        }
    }

    /// <summary>Source text projection used by the transient training subviews.</summary>
    public interface ITrainingDialogueTextSource
    {
        string NpcClassMessage(ArcanumObjectId npc, ArcanumObjectId pc, int sourceKey);
        string PcClassMessage(ArcanumObjectId npc, ArcanumObjectId pc, int sourceKey);
        string PcGenericMessage(ArcanumObjectId npc, ArcanumObjectId pc, int firstKey, int lastKey);
        string SkillName(CharacterSkill skill);
    }

    /// <summary>
    /// Narrow t: transaction orchestrator. Skill state stays in CharacterProgressionService and Gold stays in the
    /// world inventory authority; this service owns neither domain.
    /// </summary>
    public sealed class DialogueTrainingService
    {
        public const int SourceBaseCost = 100;

        private readonly WorldMapSessionCoordinator _world;

        public DialogueTrainingService(WorldMapSessionCoordinator world)
            => _world = world ?? throw new ArgumentNullException(nameof(world));

        public DialogueTrainingResult Evaluate(DialogueTrainingRequest request,
            IReadOnlyCollection<CharacterSkill> offeredSkills)
        {
            if (_world.PlayerState == null || request.Trainee != _world.PlayerState.Identity
                || !_world.Characters.TryGet(request.Trainee, out PersistentCharacterState trainee)
                || trainee.ObjectType != ObjectType.Pc || !_world.Progression.TryGet(request.Trainee, out _))
                return new DialogueTrainingResult(DialogueTrainingFailure.InvalidTrainee, request);
            if (!_world.TryGetObjectState(request.Trainer, out PersistentObjectState trainer)
                || trainer.Type != ObjectType.Npc || !_world.Characters.TryGet(request.Trainer, out _))
                return new DialogueTrainingResult(DialogueTrainingFailure.InvalidTrainer, request);
            if ((int)request.Skill < 0 || (int)request.Skill >= CharacterSkillRules.SkillCount)
                return new DialogueTrainingResult(DialogueTrainingFailure.InvalidSkill, request);
            if ((int)request.Tier < 0 || (int)request.Tier > (int)SkillTrainingLevel.Master)
                return new DialogueTrainingResult(DialogueTrainingFailure.InvalidTier, request);
            // dialog.c::dialog_ask_money_for_training always requests Apprentice; t: has no tier argument.
            if (request.Tier != SkillTrainingLevel.Apprentice)
                return new DialogueTrainingResult(DialogueTrainingFailure.UnsupportedTier, request);
            if (offeredSkills == null || !Contains(offeredSkills, request.Skill))
                return new DialogueTrainingResult(DialogueTrainingFailure.SkillNotOffered, request);
            if (_world.Progression.GetTrainingLevel(request.Trainee, request.Skill) != SkillTrainingLevel.None)
                return new DialogueTrainingResult(DialogueTrainingFailure.AlreadyTrained, request);

            TrainingAssignmentResult assignment = _world.Progression.PreviewTrainingLevel(
                request.Trainee, request.Skill, request.Tier);
            DialogueTrainingFailure failure = assignment switch
            {
                TrainingAssignmentResult.Success => DialogueTrainingFailure.None,
                TrainingAssignmentResult.Unchanged => DialogueTrainingFailure.AlreadyTrained,
                TrainingAssignmentResult.NonSequentialIncrease => DialogueTrainingFailure.NonSequentialTraining,
                TrainingAssignmentResult.InsufficientSkillRank => DialogueTrainingFailure.InsufficientSkillRank,
                TrainingAssignmentResult.DerivedMonstrousMelee => DialogueTrainingFailure.DerivedTraining,
                _ => DialogueTrainingFailure.AssignmentFailed,
            };
            int cost = failure == DialogueTrainingFailure.None
                ? CalculateCost(SourceBaseCost, _world.DerivedStats.GetReaction(request.Trainer, request.Trainee)) : 0;
            return new DialogueTrainingResult(failure, request, cost,
                _world.GetGold(request.Trainee), _world.GetGold(request.Trainee));
        }

        public DialogueTrainingResult Train(DialogueTrainingRequest request,
            IReadOnlyCollection<CharacterSkill> offeredSkills)
        {
            DialogueTrainingResult evaluation = Evaluate(request, offeredSkills);
            if (!evaluation.Succeeded) return evaluation;
            int goldBefore = _world.GetGold(request.Trainee);
            if (goldBefore < evaluation.Cost)
                return new DialogueTrainingResult(DialogueTrainingFailure.InsufficientGold, request,
                    evaluation.Cost, goldBefore, goldBefore);

            CharacterProgressionService.Snapshot progression = _world.Progression.CaptureSnapshot();
            WorldMapSessionCoordinator.DialogueInventorySnapshot inventory = _world.CaptureDialogueInventorySnapshot();
            if (!_world.TryTransferGold(request.Trainee, request.Trainer, evaluation.Cost, out _))
            {
                _world.RestoreDialogueInventorySnapshot(inventory);
                return new DialogueTrainingResult(DialogueTrainingFailure.PaymentFailed, request,
                    evaluation.Cost, goldBefore, goldBefore);
            }

            TrainingAssignmentResult assignment = _world.Progression.SetTrainingLevel(
                request.Trainee, request.Skill, request.Tier);
            if (assignment != TrainingAssignmentResult.Success)
            {
                _world.Progression.RestoreSnapshot(progression);
                _world.RestoreDialogueInventorySnapshot(inventory);
                return new DialogueTrainingResult(DialogueTrainingFailure.AssignmentFailed, request,
                    evaluation.Cost, goldBefore, goldBefore);
            }
            return new DialogueTrainingResult(DialogueTrainingFailure.None, request, evaluation.Cost,
                goldBefore, _world.GetGold(request.Trainee));
        }

        public static int CalculateCost(int baseCost, int reaction)
        {
            if (baseCost < 0) throw new ArgumentOutOfRangeException(nameof(baseCost));
            int percent;
            if (reaction < 0) percent = 200;
            else
            {
                int bounded = Math.Min(reaction, 100);
                percent = bounded < 50 ? 2 * (100 - bounded) : 120 - 2 * bounded / 5;
            }
            int cost = checked(baseCost * percent / 100);
            return cost == 1 ? 2 : cost;
        }

        private static bool Contains(IReadOnlyCollection<CharacterSkill> skills, CharacterSkill skill)
        {
            foreach (CharacterSkill candidate in skills)
                if (candidate == skill) return true;
            return false;
        }
    }
}
