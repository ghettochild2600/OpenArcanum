using System;
using System.Collections.Generic;
using Arcanum.Formats.Dialog;
using Arcanum.Formats.Objects;
using Arcanum.Formats.Quest;
using Arcanum.Formats.Script;
using Arcanum.Runtime.Campaign;
using Arcanum.Runtime.Character;
using Arcanum.Runtime.World;
using Arcanum.Script;
using UnityEngine;

namespace Arcanum.Runtime.Dialogue
{
    public enum DialogueSessionPhase
    {
        Idle,
        Starting,
        Active,
        AwaitingPlayerChoice,
        ExecutingResponse,
        Completed,
        Cancelled,
    }

    public enum DialogueStartStatus
    {
        Started,
        Busy,
        InvalidPc,
        InvalidNpc,
        TargetUnavailable,
        MissingScript,
        MissingDialogue,
        UnsupportedScript,
        ExecutionFailed,
    }

    public enum DialogueChoiceStatus
    {
        Advanced,
        Completed,
        InvalidPhase,
        InvalidChoice,
        UnsupportedEffect,
        ExecutionFailed,
    }

    public enum DialogueDiagnosticKind
    {
        UnsupportedScriptOpcode,
        UnsupportedCondition,
        UnsupportedEffect,
        ExecutionFailure,
    }

    public enum TrainingDialogueView
    {
        None,
        SkillSelection,
        Payment,
        Result,
    }

    public readonly struct DialogueDiagnostic
    {
        public DialogueDiagnosticKind Kind { get; }
        public int DialogueNumber { get; }
        public int Line { get; }
        public ArcanumObjectId Npc { get; }
        public string Detail { get; }

        public DialogueDiagnostic(DialogueDiagnosticKind kind, int dialogueNumber, int line,
            ArcanumObjectId npc, string detail)
        {
            Kind = kind;
            DialogueNumber = dialogueNumber;
            Line = line;
            Npc = npc;
            Detail = detail;
        }

        public override string ToString()
            => $"kind={Kind}; dialogue={DialogueNumber}; line={Line}; npc={Npc}; detail={Detail}";
    }

    /// <summary>
    /// Authoritative transient conversation state. It holds stable IDs and source records only; the Unity
    /// presenter observes this service and never owns dialogue/campaign decisions.
    /// </summary>
    public sealed class ProductionDialogueSession
    {
        private static readonly HashSet<string> M5AAdmittedTests = new(StringComparer.OrdinalIgnoreCase)
            { "gf", "qu", "ra" };
        private static readonly HashSet<string> M5AAdmittedEffects = new(StringComparer.OrdinalIgnoreCase)
            { "lf", "qu", "fl" };
        private static readonly HashSet<string> M5BAdmittedTests = new(StringComparer.OrdinalIgnoreCase)
            { "gf", "gv", "lf", "qu", "qb", "ra", "in", "ni", "re", "ch", "ha" };
        private static readonly HashSet<string> M5BAdmittedEffects = new(StringComparer.OrdinalIgnoreCase)
            { "lf", "qu", "fl", "in", "re", "$$" };

        private readonly WorldMapSessionCoordinator _world;
        private readonly CampaignStateService _campaign;
        private readonly DialogueTrainingService _training;
        private readonly List<DialogLine> _responses = new();
        private readonly List<CharacterSkill> _offeredTrainingSkills = new();
        private readonly HashSet<string> _reportedDiagnostics = new();
        private Func<int, ScriptFile> _resolveScript;
        private Func<int, DialogScript> _resolveDialogue;
        private Func<ArcanumObjectId, char, string> _resolveGeneratedText;
        private ITrainingDialogueTextSource _trainingText;
        private DialogScript _dialogue;
        private ProductionDialogueContext _context;
        private bool _finalSay;
        private string _startFailure;
        private int _trainingReturnLine;
        private int _trainingCost;
        private DialogueTrainingRequest _pendingTrainingRequest;

        public DialogueSessionPhase Phase { get; private set; }
        public ArcanumObjectId NpcIdentity { get; private set; }
        public ArcanumObjectId PcIdentity { get; private set; }
        public int DialogueNumber { get; private set; }
        public int CurrentLine { get; private set; }
        public string NpcText { get; private set; }
        public IReadOnlyList<DialogLine> AvailableResponses => _responses;
        public IReadOnlyList<CharacterSkill> OfferedTrainingSkills => _offeredTrainingSkills;
        public string LastFailure { get; private set; }
        public TrainingDialogueView TrainingView { get; private set; }
        public DialogueTrainingResult LastTrainingResult { get; private set; }
        public bool IsBusy => Phase is DialogueSessionPhase.Starting or DialogueSessionPhase.Active
            or DialogueSessionPhase.AwaitingPlayerChoice or DialogueSessionPhase.ExecutingResponse;

        private ISet<string> AdmittedTests => DialogueNumber == 1009
            ? M5BAdmittedTests
            : M5AAdmittedTests;
        private ISet<string> AdmittedEffects => DialogueNumber == 1009
            ? M5BAdmittedEffects
            : M5AAdmittedEffects;

        public event Action Changed;
        public event Action<DialogueDiagnostic> Diagnostic;

        public ProductionDialogueSession(WorldMapSessionCoordinator world, CampaignStateService campaign)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _training = new DialogueTrainingService(world);
        }

        public void BindSources(Func<int, ScriptFile> resolveScript, Func<int, DialogScript> resolveDialogue)
        {
            _resolveScript = resolveScript ?? throw new ArgumentNullException(nameof(resolveScript));
            _resolveDialogue = resolveDialogue ?? throw new ArgumentNullException(nameof(resolveDialogue));
        }

        public void BindGeneratedText(Func<ArcanumObjectId, char, string> resolveGeneratedText)
            => _resolveGeneratedText = resolveGeneratedText ?? throw new ArgumentNullException(nameof(resolveGeneratedText));

        public void BindTrainingDialogueText(ITrainingDialogueTextSource source)
            => _trainingText = source ?? throw new ArgumentNullException(nameof(source));

        public DialogueStartStatus Start(ArcanumObjectId pc, ArcanumObjectId npc)
        {
            if (IsBusy) return DialogueStartStatus.Busy;
            ResetTransient(DialogueSessionPhase.Starting);
            PcIdentity = pc;
            NpcIdentity = npc;

            if (_world.PlayerState == null || _world.PlayerState.Identity != pc
                || !_world.Characters.TryGet(pc, out PersistentCharacterState pcState)
                || pcState.ObjectType != ObjectType.Pc)
                return FailStart(DialogueStartStatus.InvalidPc, "The listener is not the production PC.");
            if (!_world.TryGetObjectState(npc, out PersistentObjectState npcState)
                || npcState.Type != ObjectType.Npc)
                return FailStart(DialogueStartStatus.InvalidNpc, "The speaker is not an authoritative NPC.");
            if (npcState.Off || !_world.TryGetLoadedObject(npc, out _))
                return FailStart(DialogueStartStatus.TargetUnavailable, "The speaker is not loaded and active.");
            if (_resolveScript == null || _resolveDialogue == null)
                return FailStart(DialogueStartStatus.MissingScript, "Dialogue sources have not been bound.");

            DialogueNumber = npcState.DialogNum;
            ScriptFile script = _resolveScript(DialogueNumber);
            if (script == null)
                return FailStart(DialogueStartStatus.MissingScript, $"SAP_DIALOG script {DialogueNumber} is missing.");
            if (!ProductionDialogueScriptPolicy.Supports(script, out string unsupported))
            {
                Report(DialogueDiagnosticKind.UnsupportedScriptOpcode, 0, unsupported);
                return FailStart(DialogueStartStatus.UnsupportedScript, unsupported);
            }
            _dialogue = _resolveDialogue(DialogueNumber);
            if (_dialogue == null)
                return FailStart(DialogueStartStatus.MissingDialogue, $"Dialogue {DialogueNumber} is missing.");

            _context = new ProductionDialogueContext(_world, _campaign, pc, npc, _resolveGeneratedText);
            DialogueTransactionSnapshot snapshot = new(_world, _campaign);
            ScriptAttachmentState attachment = _campaign.GetScriptAttachment(npc, (int)Sap.Dialog);
            var scriptContext = new ScriptContext
            {
                Triggerer = new WorldScriptObjectReference(pc),
                Attachee = new WorldScriptObjectReference(npc),
                AttachmentPoint = (int)Sap.Dialog,
                ScriptNum = DialogueNumber,
                LocalFlags = attachment.Flags,
                LocalCounters = attachment.Counters,
            };
            _startFailure = null;
            ScriptExecutionResult result = new ScriptVm(new DialogueScriptHost(this, _world), _campaign)
                .ExecuteStrict(script, scriptContext);
            if (!result.Succeeded || _startFailure != null || Phase != DialogueSessionPhase.AwaitingPlayerChoice)
            {
                snapshot.Restore(_world, _campaign);
                string detail = _startFailure ?? result.Detail ?? $"SAP_DIALOG ended with {result.Status}.";
                Report(DialogueDiagnosticKind.ExecutionFailure, CurrentLine, detail);
                return FailStart(DialogueStartStatus.ExecutionFailed, detail);
            }
            _campaign.SetScriptAttachment(npc, (int)Sap.Dialog, scriptContext.LocalFlags,
                scriptContext.LocalCounters);
            LastFailure = null;
            Changed?.Invoke();
            return DialogueStartStatus.Started;
        }

        public DialogueChoiceStatus SelectResponse(int index)
        {
            if (Phase != DialogueSessionPhase.AwaitingPlayerChoice)
                return DialogueChoiceStatus.InvalidPhase;
            if ((uint)index >= (uint)_responses.Count) return DialogueChoiceStatus.InvalidChoice;
            if (TrainingView != TrainingDialogueView.None) return SelectTrainingResponse(index);
            if (_finalSay)
            {
                Complete();
                return DialogueChoiceStatus.Completed;
            }

            DialogLine response = _responses[index];
            if (response.TokenCode != '\0' && !GeneratedDialogText.IsGenericToken(response.TokenCode))
            {
                if (response.TokenCode == 't' && DialogueNumber == 1009 && _trainingText != null)
                    return BeginTraining(response);
                string tokenFailure = $"dialog token '{response.TokenCode}' requires an unsupported UI handler";
                LastFailure = tokenFailure;
                Report(DialogueDiagnosticKind.UnsupportedEffect, response.Num, tokenFailure);
                Changed?.Invoke();
                return DialogueChoiceStatus.UnsupportedEffect;
            }
            DialogueTransactionSnapshot snapshot = new(_world, _campaign);
            Phase = DialogueSessionPhase.ExecutingResponse;
            if (!DialogScriptEvaluator.TryRunEffectStrict(response.Effect, _context, AdmittedEffects,
                    out int gotoOverride, out string failure))
            {
                snapshot.Restore(_world, _campaign);
                Phase = DialogueSessionPhase.AwaitingPlayerChoice;
                LastFailure = failure;
                Report(DialogueDiagnosticKind.UnsupportedEffect, response.Num, failure);
                Changed?.Invoke();
                return DialogueChoiceStatus.UnsupportedEffect;
            }

            if (gotoOverride >= 0)
            {
                if (!EnterNode(gotoOverride, true, out failure))
                    return RollBackChoice(snapshot, response.Num, failure);
                Changed?.Invoke();
                return DialogueChoiceStatus.Advanced;
            }
            if (response.Target <= 0)
            {
                Complete();
                return DialogueChoiceStatus.Completed;
            }
            if (!EnterNode(response.Target, false, out failure))
                return RollBackChoice(snapshot, response.Num, failure);
            Changed?.Invoke();
            return DialogueChoiceStatus.Advanced;
        }

        private DialogueChoiceStatus BeginTraining(DialogLine response)
        {
            DialogueTransactionSnapshot snapshot = new(_world, _campaign);
            if (!TryParseTrainingPayload(response.TokenPayload, out List<CharacterSkill> skills, out string failure))
            {
                string detail = $"malformed t: payload on dialogue {DialogueNumber} line {response.Num}: {failure}";
                LastFailure = detail;
                Report(DialogueDiagnosticKind.UnsupportedEffect, response.Num, detail);
                Changed?.Invoke();
                return DialogueChoiceStatus.UnsupportedEffect;
            }

            Phase = DialogueSessionPhase.ExecutingResponse;
            if (!DialogScriptEvaluator.TryRunEffectStrict(response.Effect, _context, AdmittedEffects,
                    out int gotoOverride, out failure))
            {
                snapshot.Restore(_world, _campaign);
                Phase = DialogueSessionPhase.AwaitingPlayerChoice;
                LastFailure = failure;
                Report(DialogueDiagnosticKind.UnsupportedEffect, response.Num, failure);
                Changed?.Invoke();
                return DialogueChoiceStatus.UnsupportedEffect;
            }
            if (gotoOverride >= 0)
            {
                if (!EnterNode(gotoOverride, true, out failure))
                    return RollBackChoice(snapshot, response.Num, failure);
                Changed?.Invoke();
                return DialogueChoiceStatus.Advanced;
            }

            _trainingReturnLine = response.Target;
            _offeredTrainingSkills.Clear();
            _offeredTrainingSkills.AddRange(skills);
            if (!ShowTrainingSkillSelection(out failure))
                return RollBackChoice(snapshot, response.Num, failure);
            Changed?.Invoke();
            return DialogueChoiceStatus.Advanced;
        }

        private DialogueChoiceStatus SelectTrainingResponse(int index)
        {
            switch (TrainingView)
            {
                case TrainingDialogueView.SkillSelection:
                    if (index == _offeredTrainingSkills.Count) return ReturnFromTraining();
                    CharacterSkill skill = _offeredTrainingSkills[index];
                    _pendingTrainingRequest = new DialogueTrainingRequest(PcIdentity, NpcIdentity, skill,
                        SkillTrainingLevel.Apprentice);
                    DialogueTrainingResult evaluation = _training.Evaluate(_pendingTrainingRequest,
                        _offeredTrainingSkills);
                    LastTrainingResult = evaluation;
                    if (evaluation.Failure == DialogueTrainingFailure.AlreadyTrained)
                        return ShowTrainingResult(4000, false);
                    if (evaluation.Failure == DialogueTrainingFailure.InsufficientSkillRank)
                        return ShowTrainingResult(5000, false);
                    if (!evaluation.Succeeded)
                        return FailTrainingSelection(evaluation.Failure,
                            $"Training eligibility failed: {evaluation.Failure}.");
                    _trainingCost = evaluation.Cost;
                    return ShowTrainingPayment();

                case TrainingDialogueView.Payment:
                    if (index == 1) return ReturnFromTraining();
                    DialogueTrainingResult result = _training.Train(_pendingTrainingRequest,
                        _offeredTrainingSkills);
                    LastTrainingResult = result;
                    if (result.Failure == DialogueTrainingFailure.InsufficientGold)
                        return ShowTrainingResult(2000, false);
                    if (!result.Succeeded)
                        return FailTrainingSelection(result.Failure,
                            $"Training transaction failed: {result.Failure}.");
                    return ShowTrainingResult(6000, true);

                case TrainingDialogueView.Result:
                    return ReturnFromTraining();

                default:
                    return DialogueChoiceStatus.InvalidPhase;
            }
        }

        private bool ShowTrainingSkillSelection(out string failure)
        {
            failure = null;
            string prompt = _trainingText.NpcClassMessage(NpcIdentity, PcIdentity, 3000);
            string cancel = _trainingText.PcGenericMessage(NpcIdentity, PcIdentity, 800, 899);
            if (string.IsNullOrEmpty(prompt) || string.IsNullOrEmpty(cancel))
            {
                failure = "Source training selection text is unavailable.";
                return false;
            }
            _responses.Clear();
            foreach (CharacterSkill skill in _offeredTrainingSkills)
            {
                string name = _trainingText.SkillName(skill);
                if (string.IsNullOrEmpty(name))
                {
                    failure = $"Source skill name {(int)skill} is unavailable.";
                    return false;
                }
                _responses.Add(new DialogLine(-100 - (int)skill, name, string.Empty, 1, string.Empty, 0,
                    string.Empty));
            }
            _responses.Add(new DialogLine(-199, cancel, string.Empty, 1, string.Empty, 0, string.Empty));
            NpcText = ExpandTrainingText(prompt);
            TrainingView = TrainingDialogueView.SkillSelection;
            Phase = DialogueSessionPhase.AwaitingPlayerChoice;
            LastFailure = null;
            LastTrainingResult = default;
            return true;
        }

        private DialogueChoiceStatus ShowTrainingPayment()
        {
            string prompt = _trainingText.NpcClassMessage(NpcIdentity, PcIdentity, 1000);
            string yes = _trainingText.PcGenericMessage(NpcIdentity, PcIdentity, 1, 99);
            string no = _trainingText.PcGenericMessage(NpcIdentity, PcIdentity, 100, 199);
            if (string.IsNullOrEmpty(prompt) || string.IsNullOrEmpty(yes) || string.IsNullOrEmpty(no))
                return FailTrainingSelection(DialogueTrainingFailure.AssignmentFailed,
                    "Source training payment text is unavailable.");
            _responses.Clear();
            _responses.Add(new DialogLine(-201, yes, string.Empty, 1, string.Empty, 0, string.Empty));
            _responses.Add(new DialogLine(-202, no, string.Empty, 1, string.Empty, 0, string.Empty));
            NpcText = ExpandTrainingText(prompt.Replace("%d", _trainingCost.ToString()));
            TrainingView = TrainingDialogueView.Payment;
            Phase = DialogueSessionPhase.AwaitingPlayerChoice;
            LastFailure = null;
            Changed?.Invoke();
            return DialogueChoiceStatus.Advanced;
        }

        private DialogueChoiceStatus ShowTrainingResult(int npcSourceKey, bool success)
        {
            string prompt = _trainingText.NpcClassMessage(NpcIdentity, PcIdentity, npcSourceKey);
            string response = success
                ? _trainingText.PcClassMessage(NpcIdentity, PcIdentity, 1000)
                : _trainingText.PcGenericMessage(NpcIdentity, PcIdentity, 600, 699);
            if (string.IsNullOrEmpty(prompt) || string.IsNullOrEmpty(response))
                return FailTrainingSelection(DialogueTrainingFailure.AssignmentFailed,
                    "Source training result text is unavailable.");
            _responses.Clear();
            _responses.Add(new DialogLine(-203, response, string.Empty, 1, string.Empty, 0, string.Empty));
            NpcText = ExpandTrainingText(prompt);
            TrainingView = TrainingDialogueView.Result;
            Phase = DialogueSessionPhase.AwaitingPlayerChoice;
            LastFailure = null;
            Changed?.Invoke();
            return DialogueChoiceStatus.Advanced;
        }

        private DialogueChoiceStatus ReturnFromTraining()
        {
            int target = _trainingReturnLine;
            ClearTrainingFlow();
            if (target <= 0)
            {
                Complete();
                return DialogueChoiceStatus.Completed;
            }
            if (!EnterNode(target, false, out string failure))
            {
                Phase = DialogueSessionPhase.Cancelled;
                LastFailure = failure;
                Report(DialogueDiagnosticKind.ExecutionFailure, target, failure);
                Changed?.Invoke();
                return DialogueChoiceStatus.ExecutionFailed;
            }
            Changed?.Invoke();
            return DialogueChoiceStatus.Advanced;
        }

        private DialogueChoiceStatus FailTrainingSelection(DialogueTrainingFailure failure, string detail)
        {
            LastFailure = detail;
            Report(DialogueDiagnosticKind.ExecutionFailure, CurrentLine, detail);
            Phase = DialogueSessionPhase.AwaitingPlayerChoice;
            Changed?.Invoke();
            return DialogueChoiceStatus.ExecutionFailed;
        }

        private string ExpandTrainingText(string text) => DialogText.Expand(text, _context);

        internal static bool TryParseTrainingPayload(string payload, out List<CharacterSkill> skills,
            out string failure)
        {
            skills = new List<CharacterSkill>();
            failure = null;
            if (string.IsNullOrWhiteSpace(payload))
            {
                failure = "the skill list is empty";
                skills.Clear();
                return false;
            }
            foreach (string raw in payload.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0)
                {
                    failure = "the skill list contains an empty entry";
                    skills.Clear();
                    return false;
                }
                int dash = part.IndexOf('-');
                int first;
                int last;
                if (dash >= 0)
                {
                    if (dash == 0 || dash == part.Length - 1 || part.IndexOf('-', dash + 1) >= 0
                        || !int.TryParse(part.Substring(0, dash).Trim(), out first)
                        || !int.TryParse(part.Substring(dash + 1).Trim(), out last) || first > last)
                    {
                        failure = $"'{part}' is not an inclusive source skill range";
                        skills.Clear();
                        return false;
                    }
                }
                else if (!int.TryParse(part, out first))
                {
                    failure = $"'{part}' is not a decimal source skill ID";
                    skills.Clear();
                    return false;
                }
                else last = first;

                for (int value = first; value <= last; value++)
                {
                    if (value < 0 || value >= CharacterSkillRules.SkillCount)
                    {
                        failure = $"skill ID {value} is outside the source 0-15 range";
                        skills.Clear();
                        return false;
                    }
                    if (skills.Count >= 100)
                    {
                        failure = "the expanded skill list exceeds the source limit of 100";
                        skills.Clear();
                        return false;
                    }
                    skills.Add((CharacterSkill)value);
                }
            }
            return skills.Count > 0;
        }

        public bool Cancel(string reason)
        {
            if (!IsBusy) return false;
            LastFailure = reason;
            Phase = DialogueSessionPhase.Cancelled;
            ClearTrainingFlow();
            _responses.Clear();
            Changed?.Invoke();
            return true;
        }

        public void ValidateActiveTarget()
        {
            if (IsBusy && (!_world.TryGetObjectState(NpcIdentity, out PersistentObjectState state)
                           || state.Off || !_world.TryGetLoadedObject(NpcIdentity, out _)))
                Cancel("Dialogue target was lost.");
        }

        internal void OpenFromScript(object npc, int startLine, int scriptLine)
        {
            if (npc is not WorldScriptObjectReference reference || reference.Identity != NpcIdentity)
            {
                _startFailure = "SAP_DIALOG tried to open dialogue for a different NPC.";
                return;
            }
            Phase = DialogueSessionPhase.Active;
            if (!EnterNode(startLine, false, out string failure)) _startFailure = failure;
        }

        private bool EnterNode(int line, bool finalSay, out string failure)
        {
            failure = null;
            if (_dialogue == null || !_dialogue.TryGet(line, out DialogLine npcLine) || !npcLine.IsNpcSpeech)
            {
                failure = $"Dialogue {DialogueNumber} has no NPC line {line}.";
                return false;
            }
            if (!DialogScriptEvaluator.TryRunEffectStrict(npcLine.Effect, _context, AdmittedEffects,
                    out int npcGoto, out failure))
            {
                Report(DialogueDiagnosticKind.UnsupportedEffect, line, failure);
                return false;
            }
            if (npcGoto >= 0)
            {
                if (npcGoto == line)
                {
                    failure = $"Dialogue {DialogueNumber} line {line} loops to itself.";
                    return false;
                }
                return EnterNode(npcGoto, true, out failure);
            }

            CurrentLine = line;
            _finalSay = finalSay;
            NpcText = DialogText.Expand(npcLine.NpcSpeech(_context.PcIsMale), _context);
            _responses.Clear();
            if (!finalSay)
                _responses.AddRange(_dialogue.OptionsFor(line, _context.Intelligence, _context, TestForOption));
            if (_responses.Count == 0)
                _responses.Add(new DialogLine(-1, _context.GeneratedText('e'), "", 1, "", 0, ""));
            Phase = DialogueSessionPhase.AwaitingPlayerChoice;
            LastFailure = null;
            return true;
        }

        private bool TestForOption(string test, IDialogContext context)
        {
            if (DialogScriptEvaluator.TryTestStrict(test, context, AdmittedTests,
                    out bool passes, out string failure)) return passes;
            Report(DialogueDiagnosticKind.UnsupportedCondition, CurrentLine, failure);
            return false;
        }

        private DialogueChoiceStatus RollBackChoice(DialogueTransactionSnapshot snapshot, int line, string failure)
        {
            snapshot.Restore(_world, _campaign);
            Phase = DialogueSessionPhase.AwaitingPlayerChoice;
            LastFailure = failure;
            Report(DialogueDiagnosticKind.ExecutionFailure, line, failure);
            Changed?.Invoke();
            return DialogueChoiceStatus.ExecutionFailed;
        }

        private sealed class DialogueTransactionSnapshot
        {
            private readonly CampaignStateService.Snapshot _campaign;
            private readonly CharacterProgressionService.Snapshot _progression;
            private readonly CharacterDerivedStatService.Snapshot _derived;
            private readonly WorldMapSessionCoordinator.DialogueInventorySnapshot _inventory;

            public DialogueTransactionSnapshot(WorldMapSessionCoordinator world, CampaignStateService campaign)
            {
                _campaign = campaign.CaptureSnapshot();
                _progression = world.Progression.CaptureSnapshot();
                _derived = world.DerivedStats.CaptureSnapshot();
                _inventory = world.CaptureDialogueInventorySnapshot();
            }

            public void Restore(WorldMapSessionCoordinator world, CampaignStateService campaign)
            {
                campaign.RestoreSnapshot(_campaign);
                world.Progression.RestoreSnapshot(_progression);
                world.DerivedStats.RestoreSnapshot(_derived);
                world.RestoreDialogueInventorySnapshot(_inventory);
            }
        }

        private void Complete()
        {
            Phase = DialogueSessionPhase.Completed;
            ClearTrainingFlow();
            _responses.Clear();
            LastFailure = null;
            Changed?.Invoke();
        }

        private DialogueStartStatus FailStart(DialogueStartStatus status, string detail)
        {
            LastFailure = detail;
            Phase = DialogueSessionPhase.Cancelled;
            _responses.Clear();
            Changed?.Invoke();
            return status;
        }

        private void ResetTransient(DialogueSessionPhase phase)
        {
            Phase = phase;
            NpcIdentity = default;
            PcIdentity = default;
            DialogueNumber = 0;
            CurrentLine = 0;
            NpcText = null;
            LastFailure = null;
            _dialogue = null;
            _context = null;
            _finalSay = false;
            ClearTrainingFlow();
            LastTrainingResult = default;
            _responses.Clear();
        }

        private void ClearTrainingFlow()
        {
            TrainingView = TrainingDialogueView.None;
            _trainingReturnLine = 0;
            _trainingCost = 0;
            _pendingTrainingRequest = default;
            _offeredTrainingSkills.Clear();
        }

        private void Report(DialogueDiagnosticKind kind, int line, string detail)
        {
            string key = $"{kind}:{DialogueNumber}:{line}:{detail}";
            if (!_reportedDiagnostics.Add(key)) return;
            var diagnostic = new DialogueDiagnostic(kind, DialogueNumber, line, NpcIdentity, detail);
            Debug.LogWarning($"OpenArcanum dialogue compatibility: {diagnostic}");
            Diagnostic?.Invoke(diagnostic);
        }

        private static class ProductionDialogueScriptPolicy
        {
            public static bool Supports(ScriptFile file, out string failure)
            {
                if (file == null || file.Entries.Count == 0)
                {
                    failure = "empty SAP_DIALOG script";
                    return false;
                }
                for (int line = 0; line < file.Entries.Count; line++)
                {
                    ScriptCondition entry = file.Entries[line];
                    Sct condition = (Sct)entry.Type;
                    if (condition is not Sct.True and not Sct.LocalFlag)
                    {
                        failure = $"condition {condition} at script line {line}";
                        return false;
                    }
                    if (!Supports(entry.Action) || !Supports(entry.Els))
                    {
                        ScriptAction action = !Supports(entry.Action) ? entry.Action : entry.Els;
                        failure = $"action {(action == null ? "<null>" : ((Sat)action.Type).ToString())} at script line {line}";
                        return false;
                    }
                }
                failure = null;
                return true;
            }

            private static bool Supports(ScriptAction action)
            {
                if (action == null) return false;
                Sat type = (Sat)action.Type;
                return type is Sat.DoNothing or Sat.ReturnAndSkipDefault or Sat.ReturnAndRunDefault
                    or Sat.Goto or Sat.Dialog;
            }
        }

        private sealed class DialogueScriptHost : ScriptHostAdapter
        {
            private readonly ProductionDialogueSession _dialogue;
            private readonly WorldMapSessionCoordinator _world;

            public DialogueScriptHost(ProductionDialogueSession dialogue, WorldMapSessionCoordinator world)
            {
                _dialogue = dialogue;
                _world = world;
            }

            public override object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext context)
            {
                return (Sfo)sfoType switch
                {
                    Sfo.Triggerer => One(context.Triggerer),
                    Sfo.Attachee => One(context.Attachee),
                    Sfo.ExtraObject => One(context.Extra),
                    Sfo.Player => _world.PlayerState == null ? Array.Empty<object>()
                        : One(new WorldScriptObjectReference(_world.PlayerState.Identity)),
                    _ => throw Unsupported($"ResolveFocus({(Sfo)sfoType})"),
                };
            }

            public override void StartDialog(object obj, int dialogLine, int scriptNum, int scriptLine)
                => _dialogue.OpenFromScript(obj, dialogLine, scriptLine);

            private static object[] One(object value) => value == null ? Array.Empty<object>() : new[] { value };
        }
    }

    internal sealed class ProductionDialogueContext : IDialogContext, IDialogEffectPreflight
    {
        private readonly WorldMapSessionCoordinator _world;
        private readonly CampaignStateService _campaign;
        private readonly ArcanumObjectId _pc;
        private readonly ArcanumObjectId _npc;
        private readonly Func<ArcanumObjectId, char, string> _resolveGeneratedText;

        public ProductionDialogueContext(WorldMapSessionCoordinator world, CampaignStateService campaign,
            ArcanumObjectId pc, ArcanumObjectId npc, Func<ArcanumObjectId, char, string> resolveGeneratedText)
        {
            _world = world;
            _campaign = campaign;
            _pc = pc;
            _npc = npc;
            _resolveGeneratedText = resolveGeneratedText;
        }

        public int Intelligence => Attribute(CharacterAttribute.Intelligence);
        public int Charisma => Attribute(CharacterAttribute.Charisma);
        public int Perception => Attribute(CharacterAttribute.Perception);
        public int Level => _world.Progression.GetLevel(_pc);
        public int Gold
        {
            get => _world.GetGold(_pc);
            set
            {
                int current = _world.GetGold(_pc);
                if (value < current) throw Outside("negative gold transfer");
                if (value > current) _world.AddGold(_pc, checked(value - current));
            }
        }
        public int PersuasionSkill => Skill(CharacterSkill.Persuasion);
        public int HaggleSkill => Skill(CharacterSkill.Haggle);
        public int BasicSkillLevel(int skill) => Skill((CharacterSkill)skill);
        public int TechSkillLevel(int skill) => Skill((CharacterSkill)(skill + 12));
        public bool PcIsMale => _world.Characters.Get(_pc).Gender == CharacterGender.Male;
        public string PcName => "Player";
        public string NpcName => $"NPC {_world.States[_npc].PrototypeNumber}";
        public int PcRace => (int)_world.Characters.Get(_pc).Race;
        public int PcFlag(int index) => _campaign.GetPcFlag(index);
        public void SetPcFlag(int index, int value) => _campaign.SetPcFlag(index, value);
        public int PcVar(int index) => _campaign.GetPcVar(index);
        public void SetPcVar(int index, int value) => _campaign.SetPcVar(index, value);
        public int Quest(int num) => _campaign.GetPcQuestState(num);
        public void SetQuest(int num, int state)
        {
            QuestState old = (QuestState)_campaign.GetPcQuestState(num);
            if (!_campaign.TryPreviewPcQuestTransition(num, state, out QuestState preview,
                    out CampaignStateFailure failure, out bool changes))
                throw new CampaignStateService.CampaignStateException(failure, num);
            if (!changes) return;
            QuestLog quests = _world.QuestSource;
            if (preview == QuestState.Completed)
            {
                if (quests?.Meta(num) == null)
                    throw new InvalidOperationException($"Quest {num} has no bound source metadata.");
                // quest_state_set awards XP before quest_state_set_internal commits the terminal state.
                _world.Progression.AwardExperience(_pc, quests.QuestXp(num));
            }
            _campaign.SetPcQuestState(num, state);
            QuestState effective = (QuestState)_campaign.GetPcQuestState(num);
            if (effective == old) return;
            if (effective == QuestState.Accepted)
            {
                int reaction = _world.DerivedStats.GetReaction(_npc, _pc);
                if (reaction < 41) _world.DerivedStats.SetReaction(_npc, _pc, 41);
                return;
            }
            if (effective != QuestState.Completed) return;
            _world.DerivedStats.AdjustAlignment(_pc, quests.AlignmentAdjustment(num));
            _world.DerivedStats.AdjustReaction(_npc, _pc, 10);
        }
        public int GlobalFlag(int index) => _campaign.GetFlag(index);
        public void SetGlobalFlag(int index, int value) => _campaign.SetFlag(index, value);
        public int GlobalVar(int index) => _campaign.GetVar(index);
        public void SetGlobalVar(int index, int value) => _campaign.SetVar(index, value);
        public int Alignment => _world.DerivedStats.GetAlignment(_pc);
        public void AdjustAlignment(int delta) => _world.DerivedStats.AdjustAlignment(_pc, delta);
        public void SetAlignment(int value) => _world.DerivedStats.SetAlignment(_pc, value);
        public int StoryState => _campaign.StoryState;
        public void SetStoryState(int value) => throw Outside(nameof(SetStoryState));
        public bool RumorKnown(int id) => throw Outside(nameof(RumorKnown));
        public void SetRumorKnown(int id) => throw Outside(nameof(SetRumorKnown));
        public bool HasReputation(int id) => throw Outside(nameof(HasReputation));
        public void AddReputation(int id) => throw Outside(nameof(AddReputation));
        public void RemoveReputation(int id) => throw Outside(nameof(RemoveReputation));
        public void MarkAreaKnown(int id) => throw Outside(nameof(MarkAreaKnown));
        public bool HasMetNpc => throw Outside(nameof(HasMetNpc));
        public void KillNpc() => throw Outside(nameof(KillNpc));
        public int NpcReaction => _world.DerivedStats.GetReaction(_npc, _pc);
        public void AdjustReaction(int delta) => _world.DerivedStats.AdjustReaction(_npc, _pc, delta);
        public void SetReaction(int value) => _world.DerivedStats.SetReaction(_npc, _pc, value);
        public int LocalFlag(int index) => _campaign.GetLocalFlag(_npc, (int)Sap.Dialog, index);
        public void SetLocalFlag(int index, int value) => _campaign.SetLocalFlag(_npc, (int)Sap.Dialog, index, value);
        public int LocalCounter(int index) => _campaign.GetLocalCounter(_npc, (int)Sap.Dialog, index);
        public void SetLocalCounter(int index, int value) => _campaign.SetLocalCounter(_npc, (int)Sap.Dialog, index, value);
        public bool HasItem(int protoNumber, bool pcSide)
            => _world.TryFindContainedItemByName(pcSide ? _pc : _npc, protoNumber, out _);
        public void TransferItem(int protoNumber, bool pcToNpc)
        {
            ArcanumObjectId source = pcToNpc ? _pc : _npc;
            ArcanumObjectId destination = pcToNpc ? _npc : _pc;
            if (!_world.TryFindContainedItemByName(source, protoNumber, out PersistentObjectState item))
                throw new InvalidOperationException($"Inventory owner {source} has no OBJ_F_NAME {protoNumber}.");
            InventoryTransferResult result = _world.TransferItem(item.Identity, item.Placement,
                ObjectPlacement.ContainedBy(destination));
            if (!result.Succeeded) throw new InvalidOperationException($"Item transfer failed: {result.Code}.");
        }
        public void GiveXp(int questId) => throw Outside(nameof(GiveXp));
        public void GiveFatePoint() => throw Outside(nameof(GiveFatePoint));
        public void StartCombat() => throw Outside(nameof(StartCombat));
        public void RecruitNpc() => throw Outside(nameof(RecruitNpc));
        public bool IsNpcFollowingPc => throw Outside(nameof(IsNpcFollowingPc));
        public bool AreaKnown(int id) => throw Outside(nameof(AreaKnown));
        public void DisbandNpc() => throw Outside(nameof(DisbandNpc));
        public string GeneratedText(char token)
            => _resolveGeneratedText?.Invoke(_npc, token) ?? (token == 'e' ? "Goodbye." : null);

        public bool TryPreflightEffect(string code, int first, int second, char operation, out string failure)
        {
            failure = null;
            try
            {
                switch (code.ToLowerInvariant())
                {
                    case "lf":
                        _campaign.GetLocalFlag(_npc, (int)Sap.Dialog, first);
                        return true;
                    case "fl":
                    case "re":
                        return true;
                    case "qu":
                        if (!_campaign.TryPreviewPcQuestTransition(first, second, out QuestState effective,
                                out CampaignStateFailure questFailure, out bool changes))
                        {
                            failure = $"quest {first} rejected {questFailure}";
                            return false;
                        }
                        if (changes && effective == QuestState.Completed)
                        {
                            if (_world.QuestSource?.Meta(first) == null)
                            {
                                failure = $"quest {first} has no bound source metadata";
                                return false;
                            }
                            _world.Progression.Get(_pc);
                            _world.DerivedStats.Get(_pc);
                            _world.DerivedStats.Get(_npc);
                        }
                        return true;
                    case "in":
                    {
                        bool pcToNpc = first >= 0;
                        int nameIndex = Math.Abs(first);
                        ArcanumObjectId source = pcToNpc ? _pc : _npc;
                        ArcanumObjectId destination = pcToNpc ? _npc : _pc;
                        if (!_world.TryFindContainedItemByName(source, nameIndex, out PersistentObjectState item))
                        {
                            failure = $"inventory owner {source} has no OBJ_F_NAME {nameIndex}";
                            return false;
                        }
                        InventoryTransferResult preview = _world.PreviewTransferItem(item.Identity, item.Placement,
                            ObjectPlacement.ContainedBy(destination));
                        if (!preview.Succeeded)
                        {
                            failure = $"item transfer preflight failed: {preview.Code}";
                            return false;
                        }
                        return true;
                    }
                    case "$$":
                        return _world.CanAddGold(_pc, first, out failure);
                    default:
                        failure = $"dialog effect '{code}' has no production preflight";
                        return false;
                }
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
        }

        private int Attribute(CharacterAttribute attribute)
            => _world.Characters.GetEffectiveAttribute(_pc, attribute);
        private int Skill(CharacterSkill skill) => _world.Progression.GetEffectiveSkillRank(_pc, skill);
        private static NotSupportedException Outside(string operation)
            => new($"Dialogue operation '{operation}' is outside the M5A production vocabulary.");
    }
}
