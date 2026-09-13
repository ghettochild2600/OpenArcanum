using System;
using System.Collections.Generic;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Dialog
{
    /// <summary>
    /// The generated-dialog text tables (<c>mes/gd_*.mes</c>) — canned PC lines used by dialog response
    /// tokens (dialog.c <c>sub_416C10</c> / <c>dialog_copy_pc_generic_msg</c> 0x4182D0). Each token letter
    /// owns a key range in the file; the engine picks a random entry from the range. The PC-facing files are
    /// <c>gd_pc2m.mes</c>/<c>gd_pc2f.mes</c> (by the NPC's gender), with <c>gd_dumb_pc2m/f.mes</c> variants
    /// for dumb PCs — the caller picks which file(s) to load and wraps one of these per conversation.
    /// </summary>
    public sealed class GeneratedDialogText
    {
        // Token letter → its display-text range in the gd tables (dialog.c sub_416C10). Some entries,
        // such as t:, still require a separate UI handler when selected; resolving their source label does
        // not imply that the handler is implemented.
        private static readonly Dictionary<char, (int lo, int hi)> Ranges = new Dictionary<char, (int, int)>
        {
            { 'y', (1, 99) },      // "yes" flavour
            { 'n', (100, 199) },   // "no" flavour
            { 's', (200, 299) },   // "sounds good"
            { 'e', (400, 499) },   // goodbye / back out
            { 't', (500, 599) },   // training label; selection requires the training UI
            { 'f', (800, 899) },   // generic
            { 'k', (1500, 1599) }, // generic
            { 'w', (1800, 1899) }, // generic
        };

        private readonly MesFile _mes;
        private readonly Random _rng;

        public GeneratedDialogText(MesFile mes, Random rng = null)
        {
            _mes = mes;
            _rng = rng ?? new Random();
        }

        /// <summary>Whether selecting a token needs no special UI handler.</summary>
        public static bool IsGenericToken(char token) => token is 'y' or 'n' or 's' or 'e' or 'f' or 'k' or 'w';

        /// <summary>A random line from the token's range, or null (unknown token / no entries loaded).</summary>
        public string For(char token)
        {
            if (_mes == null || !Ranges.TryGetValue(token, out (int lo, int hi) r)) return null;

            // Collect the keys that actually exist in the range (the files are sparse), then pick one.
            var present = new List<int>();
            for (int k = r.lo; k <= r.hi; k++)
                if (!string.IsNullOrEmpty(_mes.Get(k)))
                    present.Add(k);
            if (present.Count == 0) return null;
            return _mes.Get(present[_rng.Next(present.Count)]).Trim();
        }
    }
}
