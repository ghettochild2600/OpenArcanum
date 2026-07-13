using System;
using System.Collections.Generic;
using Arcanum.Formats.Text;

namespace Arcanum.Formats.Sound
{
    /// <summary>
    /// The sound-id → file map (ports gsound.c <c>gsound_init</c>/<c>gsound_resolve_path</c>):
    /// <c>sound/snd_00index.mes</c> is a manifest whose VALUES name the actual SFX tables
    /// (snd_item.mes, snd_interface.mes, …) — the table list itself is data. Tables are searched in
    /// manifest order, the per-module <c>snd_user.mes</c> last; the first table containing the id wins
    /// (gsound.c:361-388). Resolved value = <c>sound/&lt;file&gt;</c>.
    /// </summary>
    public sealed class SoundTable
    {
        public const string BasePath = "sound/"; // gsound_base_sound_path (gsound.c:136)

        private readonly List<MesFile> _tables = new List<MesFile>();

        /// <summary>Loads the manifest and every table it names. <paramref name="loadMes"/> resolves a
        /// VFS path ("sound/snd_item.mes") to a parsed mes, or null when absent (entries just skip).</summary>
        public static SoundTable Load(Func<string, MesFile> loadMes)
        {
            var table = new SoundTable();
            if (loadMes == null) return table;

            MesFile index = loadMes(BasePath + "snd_00index.mes");
            if (index != null)
                foreach (KeyValuePair<int, string> entry in index.Entries)
                {
                    MesFile t = loadMes(BasePath + entry.Value.Trim());
                    if (t != null) table._tables.Add(t);
                }

            MesFile user = loadMes(BasePath + "snd_user.mes"); // module overrides, searched LAST
            if (user != null) table._tables.Add(user);
            return table;
        }

        /// <summary>The sound file for an id (e.g. 3005 → "sound/level_up.wav"), or null when unmapped.</summary>
        public string Resolve(int soundId)
        {
            for (int i = 0; i < _tables.Count; i++)
            {
                string file = _tables[i].Get(soundId);
                if (!string.IsNullOrEmpty(file)) return BasePath + file.Trim();
            }

            return null;
        }

        /// <summary>Every mapped id across all tables (first-hit-wins de-duplication) — the boot preload
        /// walks this to bake the whole bank.</summary>
        public IEnumerable<KeyValuePair<int, string>> All()
        {
            var seen = new HashSet<int>();
            foreach (MesFile t in _tables)
                foreach (KeyValuePair<int, string> e in t.Entries)
                    if (seen.Add(e.Key) && !string.IsNullOrEmpty(e.Value))
                        yield return new KeyValuePair<int, string>(e.Key, BasePath + e.Value.Trim());
        }
    }
}
