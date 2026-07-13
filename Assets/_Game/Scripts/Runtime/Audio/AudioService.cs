using System.Collections.Generic;
using Arcanum.Formats.Sound;
using UnityEngine;

namespace Arcanum.Runtime.Audio
{
    /// <summary>
    /// The only Unity-audio owner for SFX: a pool of 2D <see cref="AudioSource"/>s fed by the preloaded
    /// <see cref="SoundBank"/>. Positional playback uses the engine model (gsound.c:862-945): isometric
    /// distance with DOUBLED screen-y, linear volume falloff between the per-size radii from
    /// <c>soundparams.mes</c>, stereo pan from the horizontal offset only. The listener is the player;
    /// distances are engine screen pixels (40 px = one tile).
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const int PoolSize = 16;

        private SoundBank _bank;
        private float _pixelsPerUnit = 1f;
        private Vector3 _listener;
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly List<float> _poolBaseVolume = new List<float>(); // engine volume 0..1 per source
        private float _effectsVolume = 0.8f;

        /// <summary>Effects channel volume 0..1 (the engine's 0–10 user scale × 0.8 headroom). Setting it
        /// re-scales sounds that are ALREADY playing, so the options slider is heard immediately.</summary>
        public float EffectsVolume
        {
            get => _effectsVolume;
            set
            {
                _effectsVolume = value;
                for (int i = 0; i < _pool.Count; i++)
                    if (_pool[i].isPlaying)
                        _pool[i].volume = _poolBaseVolume[i] * value;
            }
        }

        public SoundBank Bank => _bank;

        public void Init(SoundBank bank, float pixelsPerUnit)
        {
            _bank = bank;
            _pixelsPerUnit = pixelsPerUnit;
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // engine audio is 2D with manual volume/pan
                _pool.Add(src);
                _poolBaseVolume.Add(0f);
            }
        }

        /// <summary>The player's world position — positional volume/pan are computed against it.</summary>
        public void SetListener(Vector3 worldPos) => _listener = worldPos;

        /// <summary>A UI/global sound: full volume, centered.</summary>
        public void PlayUi(int soundId) => Play(_bank?.Clip(soundId), SoundParams.VolumeMax, SoundParams.BalanceCenter, 1f);

        /// <summary>A positional sound at a world position (gsound_play_sfx_at_loc).</summary>
        public void PlayAt(int soundId, Vector3 worldPos, SoundSize size = SoundSize.Large)
        {
            AudioClip clip = _bank?.Clip(soundId);
            if (clip == null) return;
            (int vol, int bal) = Positional(worldPos, size);
            Play(clip, vol, bal, 1f);
        }

        /// <summary>A positional clip by file (scheme ambients), with explicit engine volume/balance.</summary>
        public void PlayClip(AudioClip clip, int volume, int balance) => Play(clip, volume, balance, 1f);

        /// <summary>Engine volume (0–127) + balance (0–127) for a source at a world position.</summary>
        public (int volume, int balance) Positional(Vector3 worldPos, SoundSize size)
        {
            if (_bank == null) return (SoundParams.VolumeMax, SoundParams.BalanceCenter);
            float dxPx = (worldPos.x - _listener.x) * _pixelsPerUnit;
            float dyPx = (worldPos.y - _listener.y) * _pixelsPerUnit * 2f; // GSOUND_ISOMETRIC_Y_SCALE
            float dist = Mathf.Sqrt(dxPx * dxPx + dyPx * dyPx);
            return (_bank.Params.Volume(size, dist), _bank.Params.Balance(dxPx));
        }

        private void Play(AudioClip clip, int volume, int balance, float pitch)
        {
            if (clip == null || volume <= 0) return;
            int i = FreeSource();
            if (i < 0) return;
            AudioSource src = _pool[i];
            _poolBaseVolume[i] = volume / 127f;
            src.clip = clip;
            src.volume = _poolBaseVolume[i] * _effectsVolume;
            src.panStereo = (balance - SoundParams.BalanceCenter) / 63f;
            src.pitch = pitch;
            src.loop = false;
            src.Play();
        }

        private int FreeSource()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].isPlaying)
                    return i;
            return -1; // all 16 busy — drop the sound (engine channel limit behaves the same)
        }
    }
}
