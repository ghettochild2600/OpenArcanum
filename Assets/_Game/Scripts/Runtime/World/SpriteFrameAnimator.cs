using UnityEngine;

namespace Arcanum.Runtime.World
{
    /// <summary>
    /// Loops a fixed set of sprite frames on a <see cref="SpriteRenderer"/> at a constant
    /// frame rate — used for critter idle (STAND) animations decoded from one <c>.art</c> file.
    /// Frame-rate independent (advances by <see cref="Time.deltaTime"/>), and a per-instance
    /// start frame lets a crowd of NPCs idle out of phase instead of breathing in lockstep.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps;
        private float _accum;
        private int _frame;

        // One-shot playback (attack swing, death, hit reaction): play a clip once, fire a callback at the
        // impact frame, then revert to the looping clip.
        private bool _once;
        private bool _holdLast;
        private int _impactFrame;
        private bool _impactFired;
        private System.Action _onImpact, _onComplete;
        private Sprite[] _loopFrames;
        private float _loopFps;
        private bool _manualLoop;

        /// <summary>True while a one-shot clip is playing (callers shouldn't override the clip meanwhile).</summary>
        public bool IsPlayingOnce => _once;

        public int CurrentFrame => _frame;

        public float FramesPerSecond => _fps;

        /// <summary>Prevents <see cref="Update"/> from advancing a looping clip. Source locomotion uses this so
        /// the frame change and the authored movement delta are consumed by one clock.</summary>
        public void SetManualLoop(bool manual) => _manualLoop = manual;

        /// <summary>Advances a manually-driven looping clip and reports every newly displayed frame.</summary>
        public void AdvanceManual(float deltaSeconds, float fps, System.Action<int> frameEntered)
        {
            if (_frames == null || _frames.Length < 2 || _once || deltaSeconds <= 0f) return;
            _manualLoop = true;
            _fps = fps > 0f ? fps : _fps;
            _accum += deltaSeconds * _fps;
            while (_accum >= 1f)
            {
                _accum -= 1f;
                _frame = (_frame + 1) % _frames.Length;
                if (_sr != null) _sr.sprite = _frames[_frame];
                frameEntered?.Invoke(_frame);
            }
        }

        public void Init(Sprite[] frames, float fps, int startFrame = 0)
        {
            _sr = GetComponent<SpriteRenderer>();
            SetClip(frames, fps, startFrame);
        }

        /// <summary>Replaces the looping clip (e.g. switching STAND↔WALK or changing facing).</summary>
        public void SetClip(Sprite[] frames, float fps, int startFrame = 0)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _frames = frames;
            _fps = fps > 0f ? fps : 8f; // some art reports 0 fps; fall back to a gentle idle rate
            _accum = 0f;
            _frame = frames != null && frames.Length > 0 ? ((startFrame % frames.Length) + frames.Length) % frames.Length : 0;
            if (_frames != null && _frames.Length > 0) _sr.sprite = _frames[_frame];
        }

        /// <summary>
        /// Replaces sprites rebuilt from the same ART clip without resetting animation phase.
        /// Used when a presentation owner recreates its frames after a graphics-mode change.
        /// </summary>
        public void RebuildLoop(Sprite[] frames, float fps)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            int frame = _frame;
            float accum = _accum;
            _frames = frames;
            _fps = fps > 0f ? fps : 8f;
            _frame = frames != null && frames.Length > 0
                ? Mathf.Clamp(frame, 0, frames.Length - 1)
                : 0;
            _accum = accum;
            _once = false;
            _loopFrames = frames;
            _loopFps = _fps;
            if (_frames != null && _frames.Length > 0) _sr.sprite = _frames[_frame];
        }

        /// <summary>Freeze on a single sprite with no animation — a static corpse pose (engine
        /// <c>critter_kill</c>'s "set FALL_DOWN last frame"). Clears the clip so <see cref="Update"/> doesn't loop.</summary>
        public void ShowStatic(Sprite sprite, int frameIndex = 0)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _once = false;
            _frames = null;
            _frame = Mathf.Max(0, frameIndex);
            _accum = 0f;
            if (_sr != null && sprite != null) _sr.sprite = sprite;
        }

        /// <summary>Plays <paramref name="frames"/> once, fires <paramref name="onImpact"/> at the impact
        /// frame (fraction of the clip) and <paramref name="onComplete"/> at the end, then reverts to the
        /// current looping clip. With no usable clip, both callbacks fire immediately (graceful fallback).</summary>
        public void PlayOnce(Sprite[] frames, float fps, System.Action onImpact, System.Action onComplete = null, float impactFraction = 0.5f, bool holdLast = false)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (frames == null || frames.Length < 2)
            {
                if (holdLast && frames != null && frames.Length > 0) _sr.sprite = frames[frames.Length - 1];
                onImpact?.Invoke();
                onComplete?.Invoke();
                return;
            }

            _loopFrames = _frames; _loopFps = _fps;        // remember what to revert to
            _frames = frames;
            _fps = fps > 0f ? fps : 10f;
            _frame = 0; _accum = 0f; _impactFired = false;
            _holdLast = holdLast;
            _impactFrame = Mathf.Clamp(Mathf.RoundToInt(impactFraction * (frames.Length - 1)), 0, frames.Length - 1);
            _onImpact = onImpact; _onComplete = onComplete;
            _once = true;
            _sr.sprite = frames[0];
        }

        private void Update()
        {
            if (_frames == null) return;

            if (_once) { UpdateOnce(); return; } // one-shots (attack/death) always run — they drive logic

            if (_frames.Length < 2 || _manualLoop) return;
            if (_sr != null && !_sr.isVisible) return; // skip looping anims while off-screen (perf)
            _accum += Time.deltaTime * _fps;
            while (_accum >= 1f)
            {
                _accum -= 1f;
                _frame = (_frame + 1) % _frames.Length;
                _sr.sprite = _frames[_frame];
            }
        }

        private void UpdateOnce()
        {
            _accum += Time.deltaTime * _fps;
            while (_accum >= 1f)
            {
                _accum -= 1f;
                _frame++;
                if (!_impactFired && _frame >= _impactFrame) { _impactFired = true; _onImpact?.Invoke(); }
                if (_frame >= _frames.Length)
                {
                    _once = false;
                    System.Action done = _onComplete;
                    _onImpact = _onComplete = null;
                    if (_holdLast)
                    {
                        _sr.sprite = _frames[_frames.Length - 1]; // freeze on the last frame (death/corpse)
                        _frames = null;                            // and stop: clear frames so Update doesn't loop the death clip
                    }
                    else if (_loopFrames != null) SetClip(_loopFrames, _loopFps); // back to idle/walk
                    done?.Invoke();
                    return;
                }
                _sr.sprite = _frames[_frame];
            }
        }
    }
}
