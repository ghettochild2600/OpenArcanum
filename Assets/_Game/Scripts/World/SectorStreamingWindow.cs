using System.Collections.Generic;
using System.IO;

namespace Arcanum.World
{
    /// <summary>The source map precache window: selected sector plus its eight immediate neighbors.</summary>
    public static class SectorStreamingWindow
    {
        private const long CoordinateMask = 0x3FFFFFF;

        public readonly struct Entry
        {
            public string Path { get; }
            public int DeltaX { get; }
            public int DeltaY { get; }

            public Entry(string path, int deltaX, int deltaY)
            {
                Path = path;
                DeltaX = deltaX;
                DeltaY = deltaY;
            }
        }

        public static IEnumerable<Entry> Around(string centerPath)
        {
            string normalized = centerPath?.Replace('\\', '/').Trim().ToLowerInvariant();
            int slash = normalized?.LastIndexOf('/') ?? -1;
            if (slash <= 0 || !long.TryParse(Path.GetFileNameWithoutExtension(normalized), out long id) || id < 0)
                yield break;
            long x = id & CoordinateMask;
            long y = (id >> 26) & CoordinateMask;
            string mapPath = normalized.Substring(0, slash);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                long nx = x + dx;
                long ny = y + dy;
                if (nx < 0 || ny < 0 || nx > CoordinateMask || ny > CoordinateMask) continue;
                long neighborId = nx | (ny << 26);
                yield return new Entry($"{mapPath}/{neighborId}.sec", dx, dy);
            }
        }
    }
}
