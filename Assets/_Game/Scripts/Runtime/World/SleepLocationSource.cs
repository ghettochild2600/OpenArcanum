using System;
using System.Collections.Generic;
using Arcanum.Formats.Text;
using Arcanum.Formats.World;

namespace Arcanum.Runtime.World
{
    public enum SleepLocationMode
    {
        Unavailable,
        WildernessSleep,
        TownWait,
        BedSleep,
    }

    public interface ISleepLocationSource
    {
        SleepLocationMode Resolve(string selectedSector);
    }

    /// <summary>
    /// Source <c>sleep_ui_toggle</c> location authority: sector town-map zero permits wilderness sleep;
    /// otherwise the exact <c>[w:1]</c> flag from <c>rules/townmap.mes</c> permits waiting only.
    /// </summary>
    public sealed class SleepLocationSource : ISleepLocationSource
    {
        private readonly Func<string, bool> _exists;
        private readonly Func<string, byte[]> _read;
        private readonly HashSet<int> _waitableTownMaps = new();

        public SleepLocationSource(MesFile townMaps, Func<string, bool> exists, Func<string, byte[]> read)
        {
            if (townMaps == null) throw new ArgumentNullException(nameof(townMaps));
            _exists = exists ?? throw new ArgumentNullException(nameof(exists));
            _read = read ?? throw new ArgumentNullException(nameof(read));
            foreach (KeyValuePair<int, string> entry in townMaps.Entries)
                if (HasWaitableFlag(entry.Value)) _waitableTownMaps.Add(entry.Key);
        }

        public SleepLocationMode Resolve(string selectedSector)
        {
            if (string.IsNullOrWhiteSpace(selectedSector) || !_exists(selectedSector))
                return SleepLocationMode.Unavailable;
            try
            {
                return ResolveTownMap(SectorReader.ReadSections(_read(selectedSector)).TownMapInfo);
            }
            catch
            {
                return SleepLocationMode.Unavailable;
            }
        }

        public SleepLocationMode ResolveTownMap(int townMap)
            => townMap == 0 ? SleepLocationMode.WildernessSleep
                : _waitableTownMaps.Contains(townMap) ? SleepLocationMode.TownWait
                : SleepLocationMode.Unavailable;

        internal static bool HasWaitableFlag(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            int marker = value.IndexOf("[w:", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return false;
            int index = marker + 3;
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
            return index < value.Length && value[index] == '1';
        }
    }
}
