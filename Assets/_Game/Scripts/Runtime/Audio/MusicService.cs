using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using Arcanum.Formats.Database;
using Arcanum.Formats.Sound;
using UnityEngine;
using UnityEngine.Networking;

namespace Arcanum.Runtime.Audio
{
    /// <summary>
    /// The scheme player (gsound.c:1179-1254 <c>gsound_update</c>): two concurrent slots — 0 music,
    /// 1 ambient — each holding one scheme from <c>schemelist.mes</c>. Every 250 ms tick: looping entries
    /// start/stop as the game hour crosses their <c>/time</c> window; ambient one-shots fire on
    /// d1000 &lt; <c>/freq</c> with randomized volume/pan. Combat music (gsound.c:1718-1786) saves the
    /// schemes, plays a random <c>music/combat N.mp3</c> stinger then loops <c>combatmusic.mp3</c>, and
    /// restores on combat end. MP3s stream from the module's loose <c>sound/</c> folder; WAV ambients come
    /// from the preloaded bank.
    /// </summary>
    public sealed class MusicService : MonoBehaviour
    {
        private const float TickInterval = 0.25f; // GSOUND_PING_INTERVAL_MS

        private SoundBank _bank;
        private DatVirtualFileSystem _vfs;
        private System.Func<float> _hour; // game hour 0..24 (WorldSimulation clock)
        private AudioService _sfx;        // ambient one-shots route through the SFX pool

        private float _musicVolume = 0.8f;

        /// <summary>Music channel volume 0..1 — re-scales the PLAYING loops and combat music immediately,
        /// so the options slider is heard without waiting for the next track.</summary>
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = value;
                foreach (Slot slot in _slots)
                    if (slot.Source != null && slot.ActiveLoop != null)
                        slot.Source.volume = slot.ActiveLoop.VolMax / 100f * value;
                if (_combatSource != null && _combatSource.isPlaying) _combatSource.volume = value;
            }
        }

        private sealed class Slot
        {
            public SoundScheme Scheme;
            public SchemeEntry ActiveLoop;
            public AudioSource Source;
        }

        private readonly Slot[] _slots = { new Slot(), new Slot() };
        private readonly Dictionary<string, AudioClip> _streamed = new Dictionary<string, AudioClip>();
        private float _nextTick;
        private int _musicIdx, _ambientIdx; // active scheme indices (to restore after combat)
        private bool _combatMusic;
        private AudioSource _combatSource;
        private long _sequence;

        public int MusicSchemeIndex => _musicIdx;
        public int AmbientSchemeIndex => _ambientIdx;
        public bool CombatMusicActive => _combatMusic;
        public int ActiveLoopCount
        {
            get
            {
                int count = _combatSource != null && _combatSource.isPlaying ? 1 : 0;
                foreach (Slot slot in _slots) if (slot.Source != null && slot.Source.isPlaying) count++;
                return count;
            }
        }
        public AudioPlaybackRecord LastMusicPlayback { get; private set; }
        public AudioPlaybackRecord LastAmbientPlayback { get; private set; }
        public event Action<AudioPlaybackRecord> PlaybackStarted;

        public void Init(SoundBank bank, DatVirtualFileSystem vfs, AudioService sfx, System.Func<float> gameHour)
        {
            _bank = bank;
            _vfs = vfs;
            _sfx = sfx;
            _hour = gameHour;
            foreach (Slot s in _slots)
            {
                s.Source = gameObject.AddComponent<AudioSource>();
                s.Source.playOnAwake = false;
                s.Source.spatialBlend = 0f;
            }

            _combatSource = gameObject.AddComponent<AudioSource>();
            _combatSource.playOnAwake = false;
            _combatSource.spatialBlend = 0f;
        }

        /// <summary>Swap the scheme slots (map load / sector override / script). 0 = keep silence.</summary>
        public void PlayScheme(int musicIdx, int ambientIdx)
        {
            bool musicChanged = _musicIdx != musicIdx;
            bool ambientChanged = _ambientIdx != ambientIdx;
            _musicIdx = musicIdx;
            _ambientIdx = ambientIdx;
            if (_combatMusic) return; // deferred — restored when combat music ends (gsound.c:1319)
            if (musicChanged || _slots[0].Scheme == null && musicIdx != 0) Apply(0, musicIdx);
            if (ambientChanged || _slots[1].Scheme == null && ambientIdx != 0) Apply(1, ambientIdx);
        }

        /// <summary>Combat music on/off — save schemes → stinger → loop; restore on end.</summary>
        public void SetCombat(bool active)
        {
            if (active == _combatMusic) return;
            _combatMusic = active;
            if (active)
            {
                StopSlot(_slots[0]);
                StopSlot(_slots[1]);
                StartCoroutine(PlayCombat());
            }
            else
            {
                StopAllCoroutines();
                _combatSource.Stop();
                Apply(0, _musicIdx); // restore the pre-combat schemes
                Apply(1, _ambientIdx);
            }
        }

        private void Apply(int slotIdx, int schemeIdx)
        {
            Slot slot = _slots[slotIdx];
            if (slot.Scheme?.Index == schemeIdx) return;
            StopSlot(slot);
            slot.Scheme = schemeIdx == 0 ? null : _bank?.Schemes.Get(schemeIdx);
        }

        private void StopSlot(Slot slot)
        {
            slot.Source.Stop();
            slot.ActiveLoop = null;
        }

        private void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;
            if (_combatMusic || _bank == null) return;

            int hour = _hour != null ? Mathf.FloorToInt(_hour()) : 12;
            foreach (Slot slot in _slots)
            {
                if (slot.Scheme == null) continue;
                foreach (SchemeEntry e in slot.Scheme.Entries)
                {
                    bool inWindow = e.InHourWindow(hour);
                    if (e.IsLoop)
                    {
                        if (inWindow && slot.ActiveLoop != e) StartCoroutine(StartLoop(slot, e));
                        else if (!inWindow && slot.ActiveLoop == e) StopSlot(slot);
                    }
                    else if (!e.IsMusic && e.Frequency > 0 && inWindow && UnityEngine.Random.Range(0, 1000) < e.Frequency)
                    {
                        PlayAmbientOneShot(e);
                    }
                }
            }
        }

        // An ambient one-shot with the entry's randomized volume/pan (gsound.c:1493-1507): /bal picks a
        // random stereo position, /scatter approximates the engine's derived spread.
        private void PlayAmbientOneShot(SchemeEntry e)
        {
            AudioClip clip = e.SoundId >= 0
                ? _bank.Clip(e.SoundId)
                : _bank.ClipByPath(_vfs, SoundTable.BasePath + e.File);
            if (clip == null || _sfx == null) return;
            int vol = UnityEngine.Random.Range(e.VolMin, e.VolMax + 1) * 127 / 100;
            int bal = e.BalMin >= 0 ? UnityEngine.Random.Range(e.BalMin, e.BalMax + 1) * 127 / 100
                : e.Scatter > 0 ? UnityEngine.Random.Range(0, 128)
                : SoundParams.BalanceCenter;
            LastAmbientPlayback = _sfx.PlayClip(clip, vol, bal,
                e.SoundId >= 0 ? _bank.ResolvePath(e.SoundId) : SoundTable.BasePath + e.File,
                AudioCategory.Ambience, AudioPresentationKind.Ambience);
        }

        private IEnumerator StartLoop(Slot slot, SchemeEntry e)
        {
            slot.ActiveLoop = e; // claim before the async load so the tick doesn't double-start
            AudioClip clip = null;
            if (e.File != null && e.File.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase))
            {
                yield return StreamMp3(SoundTable.BasePath + e.File, c => clip = c);
            }
            else
            {
                clip = e.SoundId >= 0
                    ? _bank.Clip(e.SoundId)
                    : _bank.ClipByPath(_vfs, SoundTable.BasePath + e.File);
            }

            if (clip == null || slot.ActiveLoop != e) yield break; // scheme changed while loading
            slot.Source.clip = clip;
            slot.Source.loop = true;
            slot.Source.volume = e.VolMax / 100f * _musicVolume;
            slot.Source.Play();
            var record = new AudioPlaybackRecord
            {
                Sequence = ++_sequence, Kind = slot == _slots[0]
                    ? AudioPresentationKind.Music : AudioPresentationKind.Ambience,
                Category = slot == _slots[0] ? AudioCategory.Music : AudioCategory.Ambience,
                SoundId = e.SoundId, VirtualPath = e.SoundId >= 0
                    ? _bank.ResolvePath(e.SoundId) : SoundTable.BasePath + e.File,
                Loop = true, Clip = clip, Source = slot.Source,
            };
            if (slot == _slots[0]) LastMusicPlayback = record;
            else LastAmbientPlayback = record;
            PlaybackStarted?.Invoke(record);
        }

        private IEnumerator PlayCombat()
        {
            // A random tension stinger, then the combat loop (gsound.c:1750-1752).
            AudioClip stinger = null, loop = null;
            yield return StreamMp3($"{SoundTable.BasePath}music/combat {UnityEngine.Random.Range(1, 7)}.mp3", c => stinger = c);
            if (!_combatMusic) yield break;
            if (stinger != null)
            {
                _combatSource.clip = stinger;
                _combatSource.loop = false;
                _combatSource.volume = _musicVolume;
                _combatSource.Play();
                RecordCombat(stinger, stinger.name, false);
                yield return new WaitForSeconds(stinger.length);
            }

            if (!_combatMusic) yield break;
            yield return StreamMp3(SoundTable.BasePath + "music/combatmusic.mp3", c => loop = c);
            if (!_combatMusic || loop == null) yield break;
            _combatSource.clip = loop;
            _combatSource.loop = true;
            _combatSource.volume = _musicVolume;
            _combatSource.Play();
            RecordCombat(loop, SoundTable.BasePath + "music/combatmusic.mp3", true);
        }

        private void RecordCombat(AudioClip clip, string path, bool loop)
        {
            LastMusicPlayback = new AudioPlaybackRecord
            {
                Sequence = ++_sequence, Kind = AudioPresentationKind.Music,
                Category = AudioCategory.Music, VirtualPath = path, Loop = loop,
                Clip = clip, Source = _combatSource,
            };
            PlaybackStarted?.Invoke(LastMusicPlayback);
        }

        /// <summary>Streams a retail MP3. Loose files are used in place; DAT-only files are copied to a
        /// deterministic generated cache under Unity's temporary cache because UnityWebRequest cannot
        /// decode directly from the DAT byte stream. Original GameData is never changed.</summary>
        public IEnumerator StreamMp3(string virtualPath, System.Action<AudioClip> done)
        {
            if (_streamed.TryGetValue(virtualPath, out AudioClip cached))
            {
                done(cached);
                yield break;
            }

            string file = MaterializedPath(virtualPath);
            if (file == null)
            {
                _streamed[virtualPath] = null;
                done(null);
                yield break;
            }

            using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, AudioType.MPEG);
            yield return req.SendWebRequest();
            AudioClip clip = req.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(req) : null;
            if (clip == null) Debug.LogWarning($"[Audio] music stream failed: {virtualPath}");
            _streamed[virtualPath] = clip;
            done(clip);
        }

        public string MaterializedPath(string virtualPath)
        {
            if (_vfs == null) return null;
            foreach (string root in _vfs.LooseRoots)
            {
                string candidate = Path.Combine(root, virtualPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
            }
            if (!_vfs.Exists(virtualPath)) return null;
            string relative = virtualPath.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string cache = Path.Combine(Application.temporaryCachePath, "OpenArcanumAudio", relative);
            string directory = Path.GetDirectoryName(cache);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (!File.Exists(cache)) File.WriteAllBytes(cache, _vfs.ReadAllBytes(virtualPath));
            return cache;
        }
    }
}
