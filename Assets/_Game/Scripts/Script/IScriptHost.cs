namespace Arcanum.Script
{
    /// <summary>
    /// The side-effecting operations a running script can perform on the game world. The VM
    /// (<see cref="ScriptVm"/>) owns pure dispatch/flow + the value/flag/var opcodes; anything that touches the
    /// world (teleport, focus-object resolution, portal/lock state, …) goes through this interface, implemented
    /// by the world host (<c>ArcanumSectorDemo</c>). See Docs/Scripting.md.
    /// </summary>
    public interface IScriptHost
    {
        /// <summary>Teleport the triggering object to a map + tile (engine <c>SAT_TELEPORT</c>). <paramref
        /// name="mapId"/> is the 1-based MapList id (already converted from the script's 5000+ key).</summary>
        void Teleport(int mapId, int x, int y);

        /// <summary>Resolves an object-typed operand (an <c>Sfo</c> focus type + index) to the game object(s) it
        /// refers to — triggerer/attachee/extra/player and the vicinity sets. Empty array if none.</summary>
        object[] ResolveFocus(int sfoType, int sfoValue, ScriptContext ctx);

        /// <summary>Open↔close a portal/door (engine <c>SAT_TOGGLE_OPEN_CLOSED</c>); no-op for non-portals.</summary>
        void TogglePortal(object obj);

        /// <summary>Set a portal's locked state (engine <c>SAT_SET_LOCK_STATE</c>).</summary>
        void SetLocked(object obj, bool locked);

        /// <summary>Whether a portal/container is currently open (engine <c>SCT_OBJ_IS_OPEN</c>).</summary>
        bool IsOpen(object obj);

        /// <summary>Whether a critter object is dead (engine <c>SCT_OBJ_IS_DEAD</c>).</summary>
        bool IsDead(object obj);

        /// <summary>Float a line of the script's <c>.dlg</c> over an object (engine <c>SAT_FLOAT_LINE</c>):
        /// load dialog <paramref name="scriptNum"/>, line <paramref name="lineNum"/>, show it over <paramref name="obj"/>.</summary>
        void FloatLine(object obj, int scriptNum, int lineNum);

        /// <summary>Show a line of the script's <c>.dlg</c> as a message (engine <c>SAT_PRINT_LINE</c>): load dialog
        /// <paramref name="scriptNum"/>, line <paramref name="lineNum"/>, attributed to <paramref name="obj"/> (the attachee).</summary>
        void PrintLine(object obj, int scriptNum, int lineNum);

        /// <summary>Toggle an object's <c>OF_OFF</c> state (engine <c>SAT_TOGGLE_STATE</c>) — off objects are
        /// inactive and not drawn.</summary>
        void ToggleOff(object obj);

        /// <summary>Kill a critter (engine <c>SAT_KILL</c> → <c>critter_kill</c>).</summary>
        void Kill(object obj);

        /// <summary>A critter's gold (engine <c>SCT_HAS_GOLD</c> → <c>item_gold_get</c>).</summary>
        int GetGold(object obj);

        /// <summary>Object is switched off — the <c>OF_OFF</c> flag (engine <c>SCT_OBJ_IS_SWITCHED_OFF</c>).</summary>
        bool IsSwitchedOff(object obj);

        /// <summary>Critter is a follower of the PC (engine <c>SCT_OBJ_FOLLOWING_PC</c> → <c>critter_leader_get</c>).</summary>
        bool IsFollowingPc(object obj);

        /// <summary>Critter is engaged in combat (engine <c>SCT_OBJ_IS_IN_COMBAT</c>).</summary>
        bool IsInCombat(object obj);

        /// <summary>Object is at the global tile <paramref name="x"/>,<paramref name="y"/> (engine
        /// <c>SCT_OBJ_IS_AT_LOCATION</c> — <c>location_make(x,y) == obj.location</c>).</summary>
        bool IsAtTile(object obj, int x, int y);

        /// <summary>Object is within <paramref name="range"/> tiles of the global tile <paramref name="x"/>,
        /// <paramref name="y"/> (engine <c>SCT_OBJ_WITHIN_RANGE</c> — <c>location_dist ≤ range</c>).</summary>
        bool IsWithinRange(object obj, int x, int y, int range);

        /// <summary>Object's name index equals <paramref name="name"/> (engine <c>SCT_OBJ_IS_NAMED</c> —
        /// <c>OBJ_F_NAME</c>).</summary>
        bool IsNamed(object obj, int name);

        /// <summary>The NPC has spoken with the PC before (<c>SCT_OBJ_MET_PC_BEFORE</c> — reaction_met_before).</summary>
        bool HasMetPc(object obj);

        /// <summary>The object is currently in a conversation (<c>SCT_OBJ_IS_IN_DIALOG</c>).</summary>
        bool IsInDialog(object obj);

        /// <summary>The game clock reads day (<c>SCT_DAYTIME</c> — engine <c>game_time_is_day</c>, 06:00–18:00).</summary>
        bool IsDaytime();

        /// <summary>The object's inventory holds an item with this <c>OBJ_F_NAME</c> index
        /// (<c>SCT_OBJ_HAS_ITEM_NAMED</c> — key-gated doors, quest fetch checks).</summary>
        bool HasItemNamed(object obj, int nameId);

        /// <summary>The critter is a monster of the given species (<c>SCT_OBJ_IS_MONSTER_OF_TYPE</c> — the
        /// art-id's monster species, tig <c>tig_art_monster_id_specie_get</c>).</summary>
        bool IsMonsterOfType(object obj, int specie);

        /// <summary>The critter has an item with this <c>OBJ_F_NAME</c> index in a WORN slot
        /// (<c>SCT_OBJ_IS_WIELDING_ITEM</c> — wield locs 1000–1008).</summary>
        bool IsWieldingItemNamed(object obj, int nameId);

        /// <summary>The PC knows this rumor (<c>SCT_RUMOR_KNOWN</c> — engine <c>rumor_known_get</c>).</summary>
        bool RumorKnown(object obj, int rumorId);

        /// <summary>The actor can open the portal (<c>SCT_CAN_OPEN_PORTAL</c> — engine
        /// <c>ai_attempt_open_portal</c>; we approximate with the portal's lock state).</summary>
        bool CanOpenPortal(object actor, object portal, int direction);

        /// <summary>The actor can open the container (<c>SCT_OBJ_CAN_OPEN_CONTAINER</c> — engine
        /// <c>ai_attempt_open_container</c>; approximated with the container's lock state).</summary>
        bool CanOpenContainer(object actor, object container);

        /// <summary>The observer can see the target (<c>SCT_OBJ_CAN_SEE_OBJ</c> — engine <c>ai_can_see</c>;
        /// approximated with a sight-range check).</summary>
        bool CanSeeObj(object observer, object target);

        /// <summary>The listener can hear the source (<c>SCT_OBJ_CAN_HEAR_OBJ</c> — engine <c>ai_can_hear</c>
        /// at normal loudness; approximated with a hearing-range check).</summary>
        bool CanHearObj(object listener, object source);

        /// <summary>Critter is an animal (engine <c>SCT_OBJ_IS_ANIMAL</c> — <c>OCF_ANIMAL</c>).</summary>
        bool IsAnimal(object obj);

        /// <summary>Critter is undead (engine <c>SCT_OBJ_IS_UNDEAD</c> — <c>OCF_UNDEAD</c>).</summary>
        bool IsUndead(object obj);

        /// <summary>Add (or, if negative, remove) gold to a critter (engine <c>SAT_ADJUST_GOLD</c>).</summary>
        void AdjustGold(object obj, int amount);

        /// <summary>Make a critter join the PC's party (engine <c>SAT_CRITTER_FOLLOWS</c>).</summary>
        void CritterFollow(object obj);

        /// <summary>Remove a critter from the party (engine <c>SAT_CRITTER_DISBANDS</c>).</summary>
        void CritterDisband(object obj);

        /// <summary>A critter's stat level (engine <c>SAT_GET_STAT</c> → <c>stat_level_get</c>); <paramref
        /// name="stat"/> is the engine <c>STAT_*</c> index.</summary>
        int GetStat(object obj, int stat);

        /// <summary>A critter's skill level (engine <c>SAT_GET_SKILL</c>); <paramref name="skill"/> is the engine
        /// skill index (basic, then tech).</summary>
        int GetSkill(object obj, int skill);

        /// <summary>Critter knows the spell with id <paramref name="spell"/> (engine <c>SCT_KNOWS_SPELL</c> →
        /// <c>spell_is_known</c>).</summary>
        bool KnowsSpell(object obj, int spell);

        /// <summary>Open <paramref name="obj"/>'s dialog with the PC (engine <c>SAT_DIALOG</c>) at
        /// <paramref name="dialogLine"/>. <paramref name="scriptNum"/>/<paramref name="scriptLine"/> identify
        /// the calling script entry, so the ended conversation can resume the SAP_DIALOG script (response 0 →
        /// <c>scriptLine + 1</c>, a negative response → that explicit line — dialog_ui_execute_script).</summary>
        void StartDialog(object obj, int dialogLine, int scriptNum, int scriptLine);

        /// <summary>Make <paramref name="attacker"/> attack <paramref name="target"/> (engine <c>SAT_ATTACK</c>).</summary>
        void Attack(object attacker, object target);

        /// <summary>Deal <paramref name="amount"/> damage of engine type <paramref name="type"/> to a critter
        /// (engine <c>SAT_DAMAGE</c>).</summary>
        void Damage(object obj, int amount, int type);

        /// <summary>Heal hit points (engine <c>SAT_HEAL_HP</c>).</summary>
        void HealHp(object obj, int amount);

        /// <summary>Heal fatigue (engine <c>SAT_HEAL_FATIGUE</c>).</summary>
        void HealFatigue(object obj, int amount);

        /// <summary>Have <paramref name="source"/> cast spell id <paramref name="spell"/> at <paramref
        /// name="target"/> (engine <c>SAT_CAST_SPELL</c>).</summary>
        void CastSpell(object source, int spell, object target);

        /// <summary>Reveal a world-map area to the PC (<c>SAT_MARK_MAP_LOCATION</c> — <c>area_set_known</c>).</summary>
        void MarkMapLocation(object pc, int area);

        /// <summary>The PC learns a rumor (<c>SAT_SET_RUMOR</c> — <c>rumor_known_set</c>).</summary>
        void SetRumor(object pc, int rumor);

        /// <summary>Reveal a town map (<c>SAT_UNFOG_TOWNMAP</c> — <c>townmap_set_known</c>). Recorded state; no
        /// town-map UI displays it yet.</summary>
        void UnfogTownmap(int map);

        /// <summary>Mark a rumor's quest quelled (<c>SAT_QUELL_RUMOR</c> — global <c>rumor_qstate_set</c>,
        /// only when the object is the local PC).</summary>
        void QuellRumor(object pc, int rumor);

        /// <summary>Run another object's attachment-point script (<c>SAT_CALL_SCRIPT_EX</c> — engine
        /// <c>object_script_execute(triggerer, attachee, SAP, line)</c>): e.g. open the attachee's dialog at a
        /// line. No-op if the attachee has no script at that attachment point.</summary>
        void RunObjectScript(object triggerer, object attachee, int sap, int line);

        /// <summary>Loads a parsed script by number for <c>SAT_CALL_SCRIPT</c> (the VM runs it); null if absent.</summary>
        Arcanum.Formats.Script.ScriptFile GetScript(int num);
    }
}
