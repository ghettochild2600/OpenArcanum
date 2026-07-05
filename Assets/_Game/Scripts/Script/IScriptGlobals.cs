namespace Arcanum.Script
{
    /// <summary>
    /// The session-global + per-PC state the script VM reads/writes: global vars/flags, the story state, quest
    /// states, and PC vars/flags. The default in-memory implementation is <see cref="ScriptGlobals"/>; the
    /// runtime supplies an adapter backed by the SAME <c>GameState</c>/<c>CharacterStory</c> the dialog system
    /// uses, so a quest/flag/story change made by a script and one made by dialog land in one store (and reach
    /// the journal). Per-object local flags are NOT here — they live on the <c>ScriptContext</c>.
    /// </summary>
    public interface IScriptGlobals
    {
        int GetVar(int index);
        void SetVar(int index, int value);
        int GetFlag(int index);
        void SetFlag(int index, int value);

        int StoryState { get; }
        void SetStoryState(int value);

        int GetGlobalQuestState(int quest);
        void SetGlobalQuestState(int quest, int state);
        int GetPcQuestState(int quest);
        void SetPcQuestState(int quest, int state);

        int GetPcVar(int index);
        void SetPcVar(int index, int value);
        int GetPcFlag(int index);
        void SetPcFlag(int index, int value);
    }
}
