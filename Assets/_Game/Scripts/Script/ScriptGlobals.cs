using System.Collections.Generic;

namespace Arcanum.Script
{
    /// <summary>
    /// Session-global script state — engine <c>script.c</c>'s <c>script_global_vars</c> /
    /// <c>script_global_flags</c> / <c>script_story_state</c>, plus quest states. Persists for the run (and is
    /// the natural unit to save/load later). Sizes match the engine: 2000 vars, 100×32 = 3200 bit-packed flags.
    /// </summary>
    public sealed class ScriptGlobals : IScriptGlobals
    {
        private const int MaxGlobalVars = 2000;
        private const int MaxGlobalFlagWords = 100; // 32 flags each

        private readonly int[] _vars = new int[MaxGlobalVars];
        private readonly uint[] _flags = new uint[MaxGlobalFlagWords];
        // Quest states by quest number. PC quest state is per-PC in the engine; single-PC here, so a flat map.
        private readonly Dictionary<int, int> _globalQuest = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _pcQuest = new Dictionary<int, int>();
        private int _storyState;

        public int GetVar(int index) =>
            (uint)index < MaxGlobalVars ? _vars[index] : 0;

        public void SetVar(int index, int value)
        {
            if ((uint)index < MaxGlobalVars) _vars[index] = value;
        }

        public int GetFlag(int index)
        {
            int word = index >> 5;
            return (uint)word < MaxGlobalFlagWords ? (int)((_flags[word] >> (index & 31)) & 1u) : 0;
        }

        public void SetFlag(int index, int value)
        {
            int word = index >> 5;
            if ((uint)word >= MaxGlobalFlagWords) return;
            uint bit = 1u << (index & 31);
            if ((value & 1) != 0) _flags[word] |= bit;
            else _flags[word] &= ~bit;
        }

        public int StoryState => _storyState;

        /// <summary>Engine <c>script_story_state_set</c>: story state only ever advances.</summary>
        public void SetStoryState(int value)
        {
            if (value > _storyState) _storyState = value;
        }

        // Quest states (engine quest_*_state_get/set). Stored as a plain value here — the engine's botch/advance
        // rules aren't modelled yet, so a script can move a quest to any state.
        public int GetGlobalQuestState(int quest) => _globalQuest.TryGetValue(quest, out int s) ? s : 0;
        public void SetGlobalQuestState(int quest, int state) => _globalQuest[quest] = state;
        public int GetPcQuestState(int quest) => _pcQuest.TryGetValue(quest, out int s) ? s : 0;
        public void SetPcQuestState(int quest, int state) => _pcQuest[quest] = state;

        // Per-PC vars/flags (engine OBJ_F_PC_GLOBAL_VARIABLES / _FLAGS). Single-PC here: sparse map + bit-packed
        // words. The operand's high 16 bits (the focus PC) are ignored — there's one PC.
        private readonly Dictionary<int, int> _pcVars = new Dictionary<int, int>();
        private readonly Dictionary<int, uint> _pcFlagWords = new Dictionary<int, uint>();

        public int GetPcVar(int index) => _pcVars.TryGetValue(index, out int v) ? v : 0;
        public void SetPcVar(int index, int value) => _pcVars[index] = value;

        public int GetPcFlag(int index)
            => _pcFlagWords.TryGetValue(index >> 5, out uint w) ? (int)((w >> (index & 31)) & 1u) : 0;

        public void SetPcFlag(int index, int value)
        {
            int word = index >> 5;
            uint bit = 1u << (index & 31);
            _pcFlagWords.TryGetValue(word, out uint w);
            if ((value & 1) != 0) w |= bit; else w &= ~bit;
            _pcFlagWords[word] = w;
        }
    }
}
