using System;
using System.Collections.Generic;
using Arcanum.Formats.Objects;

namespace Arcanum.Runtime.World
{
    public enum PortalPhase { Closed, Opening, Open, Closing }

    /// <summary>Gameplay-owned frame progression from portal.c and anim.c. One clock for all active portals.
    /// Stable state contains no presentation references; binding callbacks exist only while a sector is loaded.</summary>
    public sealed class PortalTransitionScheduler
    {
        private sealed class Entry
        {
            public PersistentObjectState State;
            public Action<uint, bool> Present;
            public int FrameCount;
            public int IntervalMs;
            public uint CurrentArtId;
            public bool Active;
            public bool Opening;
            public double ElapsedMs;
        }

        private readonly Dictionary<ArcanumObjectId, Entry> _entries = new();
        public int ActiveCount { get; private set; }
        public int BoundCount => _entries.Count;

        public void Bind(PersistentObjectState state, int frameCount, int fps, Action<uint, bool> present)
        {
            if (state.Type != ObjectType.Portal) throw new ArgumentException("Not a portal.");
            _entries.Add(state.Identity, new Entry
            {
                State = state, Present = present, FrameCount = frameCount,
                IntervalMs = fps > 0 && fps <= 1000 ? 1000 / fps : 0,
                CurrentArtId = state.ArtId
            });
        }

        public PortalPhase Phase(ArcanumObjectId id)
        {
            Entry e = _entries[id];
            return e.Active ? (e.Opening ? PortalPhase.Opening : PortalPhase.Closing)
                : e.State.PortalOpen ? PortalPhase.Open : PortalPhase.Closed;
        }

        public bool TryGetPhase(ArcanumObjectId id, out PortalPhase phase)
        {
            if (!_entries.TryGetValue(id, out Entry e))
            {
                phase = default;
                return false;
            }
            phase = e.Active ? (e.Opening ? PortalPhase.Opening : PortalPhase.Closing)
                : e.State.PortalOpen ? PortalPhase.Open : PortalPhase.Closed;
            return true;
        }

        public static int Frame(uint artId) => (int)((artId >> 14) & 31);
        public static bool IsWindow(uint artId) => (artId & (1u << 10)) != 0;
        public static int OpenFrame(uint artId)
        {
            if (IsWindow(artId)) return 1;
            int rotation = (int)((artId >> 11) & 7) & ~1;
            return rotation == 0 || rotation == 6 ? 6 : 3;
        }

        public bool Request(ArcanumObjectId id, bool open)
        {
            if (!_entries.TryGetValue(id, out Entry e) || e.Active || e.FrameCount <= 1
                || OpenFrame(e.CurrentArtId) >= e.FrameCount || e.IntervalMs == 0) return false;
            if (open && Frame(e.CurrentArtId) == OpenFrame(e.CurrentArtId)
                || !open && Frame(e.CurrentArtId) == 0) return false;
            e.Opening = open;
            e.Active = true;
            e.ElapsedMs = 0;
            ActiveCount++;
            Advance(e); // anim.c begins with portal_open/close immediately, then waits 1000 / ART FPS ms.
            return true;
        }

        public void Tick(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            foreach (Entry e in _entries.Values)
            {
                if (!e.Active) continue;
                e.ElapsedMs += seconds * 1000;
                while (e.Active && e.ElapsedMs + 1e-7 >= e.IntervalMs)
                {
                    e.ElapsedMs -= e.IntervalMs;
                    Advance(e);
                }
            }
        }

        private void Advance(Entry e)
        {
            int current = Frame(e.CurrentArtId);
            int last = OpenFrame(e.CurrentArtId);
            int first = last - 2;
            int next;
            if (IsWindow(e.CurrentArtId)) next = e.Opening ? 1 : 0;
            else if (e.Opening) next = current >= first && current < last ? current + 1 : first;
            else next = current >= first && current <= last ? (current == first ? 0 : current - 1) : last;
            e.CurrentArtId = (e.CurrentArtId & ~(31u << 14)) | ((uint)next << 14);
            if (next == (e.Opening ? last : 0))
            {
                e.State.ArtId = e.CurrentArtId;
                e.State.PortalOpen = e.Opening;
                e.Active = false;
                ActiveCount--;
            }
            e.Present?.Invoke(e.CurrentArtId, next != 0);
        }

        public bool Cancel(ArcanumObjectId id)
        {
            if (!_entries.TryGetValue(id, out Entry e) || !e.Active) return false;
            e.Active = false;
            e.ElapsedMs = 0;
            ActiveCount--;
            e.CurrentArtId = e.State.ArtId;
            e.Present?.Invoke(e.State.ArtId, e.State.PortalOpen);
            return true;
        }

        public void Unbind(ArcanumObjectId id)
        {
            Cancel(id); // Approved unload policy: roll back to last stable state before capture/destruction.
            _entries.Remove(id);
        }
    }
}
