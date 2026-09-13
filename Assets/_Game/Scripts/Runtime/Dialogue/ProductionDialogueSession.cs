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
        private static readonly HashSet<string> AdmittedTests = new(StringComparer.OrdinalIgnoreCase)
            { "gf", "qu", "ra" };
        private static readonly HashSet<string> AdmittedEffects = new(StringComparer.OrdinalIgnoreCase)
            { "lf", "qu", "fl" };

        private readonly WorldMapSessionCoordinator _world;
        private readonly CampaignStateService _campaign;
        private readonly List<DialogLine> _responses = new();
        private readonly HashSet<string> _reportedDiagnostics = new();
        private Func<int, ScriptFile> _resolveScript;
        private Func<int, DialogScript> _resolveDialogue;
        private DialogScript _dialogue;
        private ProductionDialogueContext _context;
        private bool _finalSay;
        private string _startFailure;

        public DialogueSessionPhase Phase { get; private set; }
        public ArcanumObjectId NpcIdentity { get; private set; }
        public ArcanumObjectId PcIdentity { get; private set; }
        public int DialogueNumber { get; private set; }
        public int CurrentLine { get; private set; }
        public string NpcText { get; private set; }
        public IReadOnlyList<DialogLine> AvailableResponses => _responses;
        public string LastFailure { get; private set; }
        public bool IsBusy => Phase is DialogueSessionPhase.Starting or DialogueSessionPhase.Active
            or DialogueSessionPhase.AwaitingPlayerChoice or DialogueSessionPhase.ExecutingResponse;

        public event Action Changed;
        public event Action<DialogueDiagnostic> Diagnostic;

        public ProductionDialogueSession(WorldMapSessionCoordinator world, CampaignStateService campaign)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        }

        public void BindSources(Func<int, ScriptFile> resolveScript, Func<int, DialogScript> resolveDialogue)
        {
            _resolveScript = resolveScript ?? throw new ArgumentNullException(nameof(resolveScript));
            _resolveDialogue = resolveDialogue ?? throw new ArgumentNullException(nameof(resolveDialogue));
        }

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

            _context = new ProductionDialogueContext(_world, _campaign, pc, npc);
            CampaignStateService.Snapshot snapshot = _campaign.CaptureSnapshot();
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
                _campaign.RestoreSnapshot(snapshot);
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
            if (_finalSay)
            {
                Complete();
                return DialogueChoiceStatus.Completed;
            }

            DialogLine response = _responses[index];
            CampaignStateService.Snapshot snapshot = _campaign.CaptureSnapshot();
            Phase = DialogueSessionPhase.ExecutingResponse;
            if (!DialogScriptEvaluator.TryRunEffectStrict(response.Effect, _context, AdmittedEffects,
                    out int gotoOverride, out string failure))
            {
                _campaign.RestoreSnapshot(snapshot);
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

        public bool Cancel(string reason)
        {
            if (!IsBusy) return false;
            LastFailure = reason;
            Phase = DialogueSessionPhase.Cancelled;
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

        private DialogueChoiceStatus RollBackChoice(CampaignStateService.Snapshot snapshot, int line, string failure)
        {
            _campaign.RestoreSnapshot(snapshot);
            Phase = DialogueSessionPhase.AwaitingPlayerChoice;
            LastFailure = failure;
            Report(DialogueDiagnosticKind.ExecutionFailure, line, failure);
            Changed?.Invoke();
            return DialogueChoiceStatus.ExecutionFailed;
        }

        private void Complete()
        {
            Phase = DialogueSessionPhase.Completed;
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
            _responses.Clear();
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

    internal sealed class ProductionDialogueContext : IDialogContext
    {
        private readonly WorldMapSessionCoordinator _world;
        private readonly CampaignStateService _campaign;
        private readonly ArcanumObjectId _pc;
        private readonly ArcanumObjectId _npc;

        public ProductionDialogueContext(WorldMapSessionCoordinator world, CampaignStateService campaign,
            ArcanumObjectId pc, ArcanumObjectId npc)
        {
            _world = world;
            _campaign = campaign;
            _pc = pc;
            _npc = npc;
        }

        public int Intelligence => Attribute(CharacterAttribute.Intelligence);
        public int Charisma => Attribute(CharacterAttribute.Charisma);
        public int Perception => Attribute(CharacterAttribute.Perception);
        public int Level => _world.Progression.GetLevel(_pc);
        public int Gold { get => throw Outside(nameof(Gold)); set => throw Outside(nameof(Gold)); }
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
        public void SetQuest(int num, int state) => _campaign.SetPcQuestState(num, state);
        public int GlobalFlag(int index) => _campaign.GetFlag(index);
        public void SetGlobalFlag(int index, int value) => _campaign.SetFlag(index, value);
        public int GlobalVar(int index) => _campaign.GetVar(index);
        public void SetGlobalVar(int index, int value) => _campaign.SetVar(index, value);
        public int Alignment => _world.DerivedStats.GetAlignment(_pc);
        public void AdjustAlignment(int delta) => throw Outside(nameof(AdjustAlignment));
        public void SetAlignment(int value) => throw Outside(nameof(SetAlignment));
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
        public int NpcReaction => _world.DerivedStats.GetReactionInputs(_npc, _pc).Subtotal;
        public void AdjustReaction(int delta) => throw Outside(nameof(AdjustReaction));
        public void SetReaction(int value) => throw Outside(nameof(SetReaction));
        public int LocalFlag(int index) => _campaign.GetLocalFlag(_npc, (int)Sap.Dialog, index);
        public void SetLocalFlag(int index, int value) => _campaign.SetLocalFlag(_npc, (int)Sap.Dialog, index, value);
        public int LocalCounter(int index) => _campaign.GetLocalCounter(_npc, (int)Sap.Dialog, index);
        public void SetLocalCounter(int index, int value) => _campaign.SetLocalCounter(_npc, (int)Sap.Dialog, index, value);
        public bool HasItem(int protoNumber, bool pcSide) => throw Outside(nameof(HasItem));
        public void TransferItem(int protoNumber, bool pcToNpc) => throw Outside(nameof(TransferItem));
        public void GiveXp(int questId) => throw Outside(nameof(GiveXp));
        public void GiveFatePoint() => throw Outside(nameof(GiveFatePoint));
        public void StartCombat() => throw Outside(nameof(StartCombat));
        public void RecruitNpc() => throw Outside(nameof(RecruitNpc));
        public bool IsNpcFollowingPc => throw Outside(nameof(IsNpcFollowingPc));
        public bool AreaKnown(int id) => throw Outside(nameof(AreaKnown));
        public void DisbandNpc() => throw Outside(nameof(DisbandNpc));
        public string GeneratedText(char token) => token == 'e' ? "Goodbye." : null;

        private int Attribute(CharacterAttribute attribute)
            => _world.Characters.GetEffectiveAttribute(_pc, attribute);
        private int Skill(CharacterSkill skill) => _world.Progression.GetEffectiveSkillRank(_pc, skill);
        private static NotSupportedException Outside(string operation)
            => new($"Dialogue operation '{operation}' is outside the M5A production vocabulary.");
    }
}
