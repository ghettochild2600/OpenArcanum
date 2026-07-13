using Arcanum.Formats.Text;

namespace Arcanum.Formats.Sound
{
    /// <summary>Positional size classes (TIG <c>TigSoundPositionalSize</c>) — scenery picks its class via
    /// the <c>OSCF_SOUND_*</c> flags; critters/items default to Large (gsound.c <c>gsound_size</c>).</summary>
    public enum SoundSize
    {
        Small = 0,
        Medium = 1,
        Large = 2,
        ExtraLarge = 3
    }

    /// <summary>
    /// Positional-audio tuning from <c>sound/soundparams.mes</c> (gsound.c:452-561): per size class a
    /// {min radius, max radius, max volume} triple — full volume inside min, silent beyond max, linear
    /// between — plus the stereo-pan distances. Distances are in engine screen pixels (40 px = 1 tile,
    /// gsound.c <c>gsound_range</c>). Missing keys keep the engine defaults.
    /// </summary>
    public sealed class SoundParams
    {
        // Engine defaults (gsound.c:444 gsound_set_defaults + the per-key defaults at :482-551).
        private readonly int[] _minRadius = { 50, 50, 150, 50 };
        private readonly int[] _maxRadius = { 150, 400, 800, 1500 };
        private readonly int[] _maxVolume = { 40, 70, 127, 100 };
        public int MinPanDistance { get; private set; } = 150;
        public int MaxPanDistance { get; private set; } = 400;

        public const int VolumeMax = 127;    // GSOUND_VOLUME_MAX (gsound.c:33)
        public const int BalanceCenter = 64; // GSOUND_BALANCE_CENTER (gsound.c:38)
        public const int PixelsPerTile = 40; // gsound_range: radius / 40 = tiles (gsound.c:1148)

        public int MinRadius(SoundSize s) => _minRadius[(int)s];
        public int MaxRadius(SoundSize s) => _maxRadius[(int)s];
        public int MaxVolume(SoundSize s) => _maxVolume[(int)s];

        public static SoundParams Read(MesFile mes)
        {
            var p = new SoundParams();
            if (mes == null) return p;
            // Key layout (gsound.c:459-551): 1/2 = LARGE min/max radius; 3/4 = pan min/max;
            // 10..12 / 20..22 / 30..32 = SMALL / MEDIUM / EXTRA_LARGE {min radius, max radius, max volume}.
            Set(mes, 1, v => p._minRadius[(int)SoundSize.Large] = v);
            Set(mes, 2, v => p._maxRadius[(int)SoundSize.Large] = v);
            Set(mes, 3, v => p.MinPanDistance = v);
            Set(mes, 4, v => p.MaxPanDistance = v);
            int[] bases = { 10, 20, 30 };
            SoundSize[] sizes = { SoundSize.Small, SoundSize.Medium, SoundSize.ExtraLarge };
            for (int i = 0; i < bases.Length; i++)
            {
                SoundSize s = sizes[i];
                Set(mes, bases[i], v => p._minRadius[(int)s] = v);
                Set(mes, bases[i] + 1, v => p._maxRadius[(int)s] = v);
                Set(mes, bases[i] + 2, v => p._maxVolume[(int)s] = v);
            }

            return p;
        }

        /// <summary>The engine attenuation (gsound.c:883-898): 0–127 from the isometric distance
        /// (<paramref name="distancePx"/> must already carry the doubled-Y).</summary>
        public int Volume(SoundSize size, float distancePx)
        {
            int min = MinRadius(size), max = MaxRadius(size), top = MaxVolume(size);

            if (distancePx <= min) return top;
            if (distancePx >= max) return 0;

            return (int)(top * (max - distancePx) / (max - min));
        }

        /// <summary>The stereo pan (gsound.c:902-936): 0 (hard left) … 64 … 127 (hard right) from the
        /// HORIZONTAL offset only.</summary>
        public int Balance(float dxPx)
        {
            float abs = dxPx < 0 ? -dxPx : dxPx;

            if (abs <= MinPanDistance) return BalanceCenter;
            if (abs >= MaxPanDistance) return dxPx < 0 ? 0 : 127;

            float t = (abs - MinPanDistance) / (MaxPanDistance - MinPanDistance);
            return BalanceCenter + (int)(t * 63f) * (dxPx < 0 ? -1 : 1);
        }

        private static void Set(MesFile mes, int key, System.Action<int> apply)
        {
            string v = mes.Get(key);
            if (v != null && int.TryParse(v.Trim(), out int n)) apply(n);
        }
    }
}
