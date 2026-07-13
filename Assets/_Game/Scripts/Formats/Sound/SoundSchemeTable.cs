using System;
using System.Collections.Generic;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Sound
{
    /// <summary>One line of a scheme (gsound.c <c>gsound_scheme_parse_entry</c>, :1428-1554): a music
    /// track (<c>/loop</c>, <c>/anchor</c>, <c>/over</c>) or an ambient one-shot (<c>/freq</c>), with an
    /// optional game-hour window and randomized volume/pan ranges.</summary>
    public sealed class SchemeEntry
    {
        public string File;        // relative path under sound/ (e.g. "music\\Tarant.mp3"), or null when
        public int SoundId = -1;   // the line used "#<id>" (resolved through SoundTable instead)
        public bool IsLoop;        // looping song, plays while the hour is inside the window
        public bool IsAnchor;      // non-looping song
        public bool IsTransition;  // "/over": one-shot that restores the previous scheme when done
        public int Frequency;      // ambients: chance per 250ms tick, d1000 < Frequency (gsound.c:1497)
        public int HourStart = -1; // "/time:a-b" window in game hours; -1 = always
        public int HourEnd = -1;
        public int VolMin = 100, VolMax = 100; // percent (converted to raw 0-127 at play time)
        public int BalMin = -1, BalMax = -1;   // random pan range; -1 = centered
        public int Scatter;                    // "/scatter:N": derives random vol/pan spreads (positional feel)

        public bool IsMusic => IsLoop || IsAnchor || IsTransition;

        /// <summary>Whether the game hour falls inside the window (wrapping windows like 19-0 supported).</summary>
        public bool InHourWindow(int hour)
        {
            if (HourStart < 0) return true;
            return HourStart <= HourEnd
                ? hour >= HourStart && hour <= HourEnd
                : hour >= HourStart || hour <= HourEnd;
        }
    }

    /// <summary>A named scheme — up to 100 consecutive schemelist lines (gsound.c:1397-1399).</summary>
    public sealed class SoundScheme
    {
        public int Index;
        public string Name;
        public readonly List<SchemeEntry> Entries = new List<SchemeEntry>();
    }

    /// <summary>
    /// The scheme registry: <c>sound/schemeindex.mes</c> maps scheme index → "Name #base" where base is
    /// the starting key into <c>sound/schemelist.mes</c>; the scheme's entries are the consecutive keys
    /// base...base+99 (gsound.c <c>gsound_scheme_load</c>). Slot conventions: scheme slot 0 = music,
    /// slot 1 = ambient.
    /// </summary>
    public sealed class SoundSchemeTable
    {
        private readonly Dictionary<int, SoundScheme> _schemes = new Dictionary<int, SoundScheme>();

        public static SoundSchemeTable Read(MesFile schemeIndex, MesFile schemeList)
        {
            var table = new SoundSchemeTable();
            if (schemeIndex == null || schemeList == null) return table;

            foreach (KeyValuePair<int, string> e in schemeIndex.Entries)
            {
                string s = e.Value;
                int hash = s.LastIndexOf('#');
                if (hash < 0) continue; // "<<NONE>>" etc.

                if (!int.TryParse(s.Substring(hash + 1).Trim(), out int baseKey)) continue;

                var scheme = new SoundScheme { Index = e.Key, Name = s.Substring(0, hash).Trim() };
                for (int k = baseKey; k < baseKey + 100; k++)
                {
                    string line = schemeList.Get(k);
                    if (line == null) continue;
                    SchemeEntry entry = ParseEntry(line);
                    if (entry != null) scheme.Entries.Add(entry);
                }

                table._schemes[e.Key] = scheme;
            }

            return table;
        }

        public SoundScheme Get(int index) => _schemes.GetValueOrDefault(index);

        public IReadOnlyCollection<SoundScheme> All => _schemes.Values;

        // "file [/key:v1-v2 ...]" — the engine splits on spaces; values use '-' or ',' pair separators.
        private static SchemeEntry ParseEntry(string line)
        {
            string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var e = new SchemeEntry();
            string file = parts[0];
            if (file.StartsWith("#") && int.TryParse(file.Substring(1), out int id)) e.SoundId = id;
            else e.File = file.Replace('\\', '/');

            for (int i = 1; i < parts.Length; i++)
            {
                string opt = parts[i];
                if (!opt.StartsWith("/")) continue;
                if (opt.Equals("/loop", StringComparison.OrdinalIgnoreCase))
                {
                    e.IsLoop = true;
                    continue;
                }

                if (opt.Equals("/anchor", StringComparison.OrdinalIgnoreCase))
                {
                    e.IsAnchor = true;
                    continue;
                }

                if (opt.Equals("/over", StringComparison.OrdinalIgnoreCase))
                {
                    e.IsTransition = true;
                    continue;
                }

                int colon = opt.IndexOf(':');
                if (colon < 0) continue;
                string key = opt.Substring(1, colon - 1).ToLowerInvariant();

                if (!ParsePair(opt.Substring(colon + 1), out int a, out int b)) continue;

                switch (key)
                {
                    case "vol":
                        e.VolMin = a;
                        e.VolMax = b;
                        break;
                    case "time":
                        e.HourStart = a;
                        e.HourEnd = b;
                        break;
                    case "freq": e.Frequency = a; break;
                    case "bal":
                        e.BalMin = a;
                        e.BalMax = b;
                        break;
                    case "scatter": e.Scatter = a; break;
                }
            }

            return e;
        }

        private static bool ParsePair(string v, out int a, out int b)
        {
            a = b = 0;
            int sep = v.IndexOfAny(new[] { '-', ',' });
            if (sep <= 0) return int.TryParse(v, out a) && (b = a) == a;
            bool okA = int.TryParse(v.Substring(0, sep), out a);
            bool okB = int.TryParse(v.Substring(sep + 1), out b);
            if (okA && !okB) b = a;
            return okA;
        }
    }
}
