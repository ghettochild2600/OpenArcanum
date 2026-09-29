using System.Collections.Generic;
using Arcanum.Formats.Database;
using Arcanum.Formats.Sound;
using Arcanum.Formats.Text;
using UnityEngine;

namespace Arcanum.Runtime.Audio
{
    /// <summary>
    /// Source-backed sound bank. The original <c>snd_*.mes</c> tables remain the authority for id lookup;
    /// PCM clips are decoded lazily and cached on first use so production startup does not eagerly expand
    /// the entire retail sound library into Unity memory. Music and voice MP3s are streamed by the
    /// presentation service. Also owns the parsed positional parameters and scheme table.
    /// </summary>
    public sealed class SoundBank
    {
        private readonly Dictionary<int, AudioClip> _clips = new Dictionary<int, AudioClip>();
        private readonly Dictionary<string, AudioClip> _byPath = new Dictionary<string, AudioClip>();
        private DatVirtualFileSystem _vfs;

        public SoundTable Table { get; private set; }
        public SoundParams Params { get; private set; }
        public SoundSchemeTable Schemes { get; private set; }
        public DatVirtualFileSystem VirtualFileSystem => _vfs;

        public static SoundBank Load(DatVirtualFileSystem vfs)
        {
            var bank = new SoundBank();
            float started = Time.realtimeSinceStartup;
            bank._vfs = vfs;

            MesFile LoadMes(string path) => vfs != null && vfs.Exists(path) ? MesReader.Read(vfs.ReadAllBytes(path)) : null;

            bank.Table = SoundTable.Load(LoadMes);
            bank.Params = SoundParams.Read(LoadMes("sound/soundparams.mes"));
            bank.Schemes = SoundSchemeTable.Read(LoadMes("sound/schemeindex.mes"), LoadMes("sound/schemelist.mes"));

            int mapped = 0;
            foreach (KeyValuePair<int, string> _ in bank.Table.All()) mapped++;
            Debug.Log($"[Audio] sound bank: {mapped} source mappings indexed lazily in " +
                      $"{Time.realtimeSinceStartup - started:0.00}s; {bank.Schemes.All.Count} schemes.");
            return bank;
        }

        /// <summary>The lazily decoded clip for a sound id, or null when the id is unmapped/missing.</summary>
        public AudioClip Clip(int soundId)
        {
            if (_clips.TryGetValue(soundId, out AudioClip cached)) return cached;
            string path = ResolvePath(soundId);
            AudioClip clip = LoadClip(_vfs, path);
            _clips[soundId] = clip; // cache nulls too
            return clip;
        }

        public string ResolvePath(int soundId) => Table?.Resolve(soundId);

        public bool Exists(string virtualPath)
            => !string.IsNullOrWhiteSpace(virtualPath) && _vfs != null && _vfs.Exists(virtualPath);

        /// <summary>A clip by VFS path (scheme ambient entries reference files directly); cached.</summary>
        public AudioClip ClipByPath(DatVirtualFileSystem vfs, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_byPath.TryGetValue(path, out AudioClip cached)) return cached;
            AudioClip clip = LoadClip(vfs, path);
            _byPath[path] = clip; // cache nulls too — don't retry a missing file every tick
            return clip;
        }

        private AudioClip LoadClip(DatVirtualFileSystem vfs, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (_byPath.TryGetValue(path, out AudioClip cached)) return cached;
            if (vfs == null || !vfs.Exists(path)) return null;
            PcmData pcm;
            try { pcm = WavPcm.Decode(vfs.ReadAllBytes(path)); }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Audio] '{path}' failed to decode: {ex.Message}");
                pcm = null;
            }

            if (pcm == null || pcm.FrameCount == 0) return null;
            var clip = AudioClip.Create(path, pcm.FrameCount, pcm.Channels, pcm.SampleRate, false);
            clip.SetData(pcm.Samples, 0);
            _byPath[path] = clip;
            return clip;
        }
    }
}
