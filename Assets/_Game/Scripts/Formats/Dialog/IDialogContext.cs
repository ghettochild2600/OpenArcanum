namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// The game-state surface a dialog test/effect needs — implemented by the runtime (over the local player,
    /// the NPC, and shared world flags) and consumed by <see cref="DialogScriptEvaluator"/>. Keeps the pure
    /// dialog parser/evaluator decoupled from the runtime character model. Indices/ids are the raw numbers
    /// from the <c>.dlg</c> codes. Unsupported queries should return a permissive value (see the evaluator).
    /// </summary>
    public interface IDialogContext
    {
        // Player stats / progression (engine: stat_level_get / skill).
        int Intelligence { get; }
        int Charisma { get; }
        int Perception { get; }
        int Level { get; }
        int Gold { get; set; }
        int PersuasionSkill { get; }
        int HaggleSkill { get; }

        /// <summary>Basic / tech skill level by index (engine <c>basic_skill_level</c> / <c>tech_skill_level</c>),
        /// for the <c>sk</c> gate. The <c>.dlg</c> skill value is a basic skill if &lt; BASIC_SKILL_COUNT, else a
        /// tech skill at <c>value − BASIC_SKILL_COUNT</c>.</summary>
        int BasicSkillLevel(int skill);

        int TechSkillLevel(int skill);

        /// <summary>The PC's gender — selects an NPC line's gender variant and gates gender-specific options.</summary>
        bool PcIsMale { get; }

        /// <summary>The player's / speaking NPC's display name, for <c>@pcname@</c>/<c>@npcname@</c> expansion.</summary>
        string PcName { get; }

        string NpcName { get; }

        /// <summary>The PC's race (engine <c>STAT_RACE</c>: human=0, dwarf=1, …) — gates race-specific options.</summary>
        int PcRace { get; }

        // Player story state (engine: script_pc_flag / pc_var / quest_state).
        int PcFlag(int index);
        void SetPcFlag(int index, int value);
        int PcVar(int index);
        void SetPcVar(int index, int value);
        int Quest(int num);
        void SetQuest(int num, int state);

        // Shared world state (engine: script_global_flag / global_var).
        int GlobalFlag(int index);
        void SetGlobalFlag(int index, int value);
        int GlobalVar(int index);
        void SetGlobalVar(int index, int value);

        // Narrative statuses (engine STATs / per-PC sets / global) driven by dialog + scripts.
        int Alignment { get; } // `al` STAT_ALIGNMENT
        void AdjustAlignment(int delta);
        void SetAlignment(int value);
        int StoryState { get; } // `ss` script_story_state (global)
        void SetStoryState(int value);
        bool RumorKnown(int id); // `ru` rumor_known
        void SetRumorKnown(int id);
        bool HasReputation(int id); // `rp` reputation
        void AddReputation(int id);
        void RemoveReputation(int id);
        void MarkAreaKnown(int id); // `mm` area_set_known
        bool HasMetNpc { get; }     // `me` reaction_met_before
        void KillNpc();             // `nk` critter_kill (the speaking NPC)

        // NPC-local state + reaction (engine: script_local_flag/counter, reaction_get/adj).
        int NpcReaction { get; }
        void AdjustReaction(int delta);
        void SetReaction(int value); // for `re` >/< clamp modes
        int LocalFlag(int index);
        void SetLocalFlag(int index, int value);
        int LocalCounter(int index);
        void SetLocalCounter(int index, int value);

        // Inventory (engine: item_find_by_name). pcSide=true checks the PC, false the NPC.
        bool HasItem(int protoNumber, bool pcSide);
        void TransferItem(int protoNumber, bool pcToNpc);

        // Consequences with no return.
        void GiveXp(int questId);
        void GiveFatePoint();
        void StartCombat();

        /// <summary>The speaking NPC joins the party (dialog <c>jo</c> effect).</summary>
        void RecruitNpc();

        /// <summary>True if the speaking NPC currently follows the PC (engine <c>critter_leader_get == pc</c>) —
        /// the <c>fo</c> gate.</summary>
        bool IsNpcFollowingPc { get; }

        /// <summary>Whether a world-map area is known to the PC (engine <c>area_is_known</c>) — the <c>ar</c> gate.</summary>
        bool AreaKnown(int id);

        /// <summary>The speaking NPC leaves the party (dialog <c>lv</c> / <c>critter_disband</c>).</summary>
        void DisbandNpc();

        /// <summary>Display text for a generated-response token (dialog.c <c>sub_416C10</c> — <c>e:</c>
        /// goodbye, <c>y:/n:/s:/f:/k:/w:</c> generic lines, from the <c>mes/gd_*.mes</c> tables). Null ⇒ the
        /// token can't be rendered and the option is skipped. Default: none.</summary>
        string GeneratedText(char token) => null;

        /// <summary>The PC's magick(+)/tech(−) aptitude (engine <c>STAT_MAGICK_TECH_APTITUDE</c>) — the
        /// <c>ma</c> gate reads it, <c>ta</c> reads its negation. Default: 0 (balanced).</summary>
        int MagickAptitude => 0;

        /// <summary>The PC's trained level in a magick college (engine <c>spell_college_level_get</c>, 0–5) —
        /// the <c>sc</c> gate. Default: 0.</summary>
        int SpellCollegeLevel(int college) => 0;

        /// <summary>Whether the party holds a (non-mind-controlled) follower whose prototype has this
        /// <c>OBJ_F_NAME</c> index (engine <c>object_list_followers</c> scan) — the <c>pa</c> gate.
        /// Default: false.</summary>
        bool PartyHasMemberNamed(int nameId) => false;

        /// <summary>The NPC is jilted — abandoned by the PC (engine <c>ONF_JILTED</c>) — the <c>wt</c> gate.
        /// Default: false (follower-desertion mechanics not modelled yet; see Docs/Dialog.md).</summary>
        bool IsNpcJilted => false;

        /// <summary>The NPC was told to wait here (engine <c>ONF_AI_WAIT_HERE</c>) — the <c>wa</c> gate.
        /// Default: false (wait-here orders not modelled yet).</summary>
        bool IsNpcWaiting => false;

        /// <summary>Whether a rumor's associated quest is resolved/quelled (engine global
        /// <c>rumor_qstate_get</c>) — the <c>rq</c> gate. Default: false (rumor-quell tracking not driven yet).</summary>
        bool RumorQuelled(int id) => false;
    }
}
