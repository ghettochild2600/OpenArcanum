using System.Collections.Generic;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// A parsed Arcanum dialog (one <c>.dlg</c>): all lines keyed by number, with the logic to gather the
    /// player options that follow an NPC speech line. The conversation walks it: show an NPC line's text,
    /// list <see cref="OptionsFor"/>, and on a pick jump to that option's <see cref="DialogLine.Target"/>.
    /// </summary>
    public sealed class DialogScript
    {
        private readonly SortedDictionary<int, DialogLine> _lines;

        public DialogScript(SortedDictionary<int, DialogLine> lines) => _lines = lines;

        public bool TryGet(int num, out DialogLine line) => _lines.TryGetValue(num, out line);

        public IReadOnlyCollection<int> LineNumbers => _lines.Keys;

        /// <summary>The engine shows at most 5 responses per NPC line (dialog.c <c>DialogState.options[5]</c>).</summary>
        public const int MaxOptions = 5;

        /// <summary>
        /// The player options that follow an NPC speech line: the consecutive option lines after
        /// <paramref name="npcLine"/> up to the next NPC speech line (engine <c>sub_414F50</c>), filtered by
        /// the IQ gate (positive = min INT, negative = max INT — the "dumb" branches), the gender gate, and
        /// the line's script test; capped at <see cref="MaxOptions"/>. Token options (<c>e:</c> goodbye,
        /// <c>y:/n:/s:/f:/k:/w:</c> generic lines) resolve their display text from the generated-dialog
        /// tables via <see cref="IDialogContext.GeneratedText"/>; tokens we can't render (b: barter,
        /// t: train, …) are skipped.
        /// </summary>
        public List<DialogLine> OptionsFor(int npcLine, int playerIq = 20, IDialogContext ctx = null)
        {
            var options = new List<DialogLine>();
            bool past = false;
            foreach (KeyValuePair<int, DialogLine> kv in _lines)
            {
                if (!past)
                {
                    if (kv.Key == npcLine) past = true;
                    continue; // skip everything up to and including the NPC line itself
                }

                DialogLine l = kv.Value;
                if (l.IsNpcSpeech) break;       // reached the next NPC line → this block of options ends
                if (!IqAllows(l.Iq, playerIq)) continue;
                // Gender gate: the option's gender field is STAT_GENDER — GENDER_FEMALE = 0, GENDER_MALE = 1
                // (stat.h; dialog.c sub_414F50 shows the option only when it matches the PC's gender).
                if (ctx != null && l.OptionGender != -1 && l.OptionGender != (ctx.PcIsMale ? 1 : 0)) continue;
                if (!DialogScriptEvaluator.TestPasses(l.Test, ctx)) continue; // script condition gate

                if (l.IsToken)
                {
                    // Engine response tokens (sub_416C10) — resolve the generic ones to generated text
                    // (gd_*.mes); anything the context can't render is dropped, like before.
                    string text = ctx?.GeneratedText(l.TokenCode);
                    if (text == null) continue;
                    l = l.WithText(text);
                }

                options.Add(l);
                if (options.Count >= MaxOptions) break; // engine cap: 5 responses per node
            }

            return options;
        }

        private static bool IqAllows(int iq, int playerIq)
            => iq == 0 || (iq > 0 ? playerIq >= iq : playerIq <= -iq);
    }
}
