using System.Collections.Generic;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// Runtime state of one conversation: the current NPC line plus the player options reachable from it.
    /// The UI shows <see cref="NpcText"/> and <see cref="Options"/>, then calls <see cref="Pick"/> with the
    /// chosen option; the conversation ends when an option's target says so. On end,
    /// <see cref="EndScriptLine"/> tells the caller whether to hand control back to the NPC's
    /// <c>SAP_DIALOG</c> script (the engine's response-number sign convention, dialog.c <c>sub_417590</c>):
    /// target 0 ⇒ continue the script after the <c>SAT_DIALOG</c> that opened us; a negative target ⇒ jump
    /// the script to that line; −1 ⇒ nothing to run (an <c>fl</c> final say).
    /// </summary>
    public sealed class DialogConversation
    {
        private readonly DialogScript _script;
        private readonly IDialogContext _ctx;

        public int CurrentLine { get; private set; }
        public int PlayerIq { get; }

        /// <summary>Set by an <c>fl</c> effect (engine end-reason 4): the NPC delivers the current line as a
        /// closing remark — no real options remain, only the generated goodbye.</summary>
        public bool FinalSay { get; private set; }

        /// <summary>Valid after <see cref="Pick"/> returns false: the <c>SAP_DIALOG</c> script line to resume
        /// at (0 = right after the opening <c>SAT_DIALOG</c>), or −1 when no script should run.</summary>
        public int EndScriptLine { get; private set; } = -1;

        public DialogConversation(DialogScript script, int startLine = 1, int playerIq = 20, IDialogContext ctx = null)
        {
            _script = script;
            CurrentLine = startLine;
            PlayerIq = playerIq;
            _ctx = ctx;
        }

        /// <summary>The current NPC line's spoken text (gender variant + <c>@name@</c> codes expanded), or null.</summary>
        public string NpcText => _script != null && _script.TryGet(CurrentLine, out DialogLine l)
            ? DialogText.Expand(l.NpcSpeech(_ctx?.PcIsMale ?? true), _ctx) : null;

        /// <summary>A player option's display text with <c>@name@</c> codes expanded.</summary>
        public string OptionText(DialogLine option) => DialogText.Expand(option.Text, _ctx);

        /// <summary>The player options available at the current NPC line (script tests applied). Never empty:
        /// when every authored option is filtered out — or an <c>fl</c> final say is pending — a generated
        /// goodbye is injected, as the engine does (dialog.c <c>sub_414E60</c>).</summary>
        public List<DialogLine> Options()
        {
            List<DialogLine> options = _script != null && !FinalSay
                ? _script.OptionsFor(CurrentLine, PlayerIq, _ctx)
                : new List<DialogLine>();
            if (options.Count == 0)
                options.Add(new DialogLine(-1, _ctx?.GeneratedText('e') ?? "Goodbye.", "", 1, "", 0, ""));
            return options;
        }

        /// <summary>
        /// Run a chosen option's effect, then advance. An <c>fl</c> effect jumps to its line as a FINAL say
        /// (the conversation ends after it). Returns false when the conversation ends — check
        /// <see cref="EndScriptLine"/> for the script hand-off.
        /// </summary>
        public bool Pick(DialogLine option)
        {
            if (_script == null) return false;

            DialogScriptEvaluator.RunEffect(option.Effect, _ctx, out int gotoOverride);

            // `fl` (engine end-reason 4): the NPC says that line, then the dialog ends — no script hand-off.
            if (gotoOverride >= 0)
            {
                if (_script.TryGet(gotoOverride, out _))
                {
                    CurrentLine = gotoOverride;
                    FinalSay = true;
                    return true;
                }
                EndScriptLine = -1;
                return false;
            }

            int next = option.Target;
            if (next < 0)
            {
                EndScriptLine = -next; // end + jump the SAP_DIALOG script to that line (engine type 2)
                return false;
            }
            if (next == 0 || !_script.TryGet(next, out _))
            {
                // Normal end (engine type 1): the SAP_DIALOG script resumes after its SAT_DIALOG — unless
                // this was an fl final say, which ends outright.
                EndScriptLine = FinalSay ? -1 : 0;
                return false;
            }
            CurrentLine = next;
            return true;
        }
    }
}
