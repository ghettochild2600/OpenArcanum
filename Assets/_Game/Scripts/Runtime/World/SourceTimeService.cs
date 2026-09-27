using System;
using System.Diagnostics;

namespace Arcanum.Runtime.World
{
    public readonly struct SourceTimeAdvance
    {
        public int ElapsedMilliseconds { get; }
        public long TotalMilliseconds { get; }

        internal SourceTimeAdvance(int elapsedMilliseconds, long totalMilliseconds)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            TotalMilliseconds = totalMilliseconds;
        }
    }

    /// <summary>
    /// Authoritative source game-time clock. The retail scheduler advances game time at eight times
    /// monotonic real time outside turn-based combat and by exactly 1,000 ms per completed turn-based round.
    /// </summary>
    public sealed class SourceTimeService
    {
        public const int RealTimeScale = 8;
        public const int MinimumPollMilliseconds = 5;
        public const int MaximumPollMilliseconds = 250;
        public const int TurnBasedRoundMilliseconds = 1_000;

        private long? _lastMonotonicMilliseconds;

        public long ElapsedMilliseconds { get; private set; }
        public bool IsPaused { get; private set; }
        public event Action<SourceTimeAdvance> Advanced;

        public void SetPaused(bool paused) => IsPaused = paused;

        public void PollNonCombat(bool combatActive)
            => PollNonCombat(ToMonotonicMilliseconds(Stopwatch.GetTimestamp()), combatActive);

        public void PollNonCombat(long monotonicMilliseconds, bool combatActive)
        {
            if (monotonicMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(monotonicMilliseconds));
            if (!_lastMonotonicMilliseconds.HasValue)
            {
                _lastMonotonicMilliseconds = monotonicMilliseconds;
                return;
            }

            long delta = monotonicMilliseconds - _lastMonotonicMilliseconds.Value;
            if (delta < MinimumPollMilliseconds) return;
            _lastMonotonicMilliseconds = monotonicMilliseconds;
            if (IsPaused || combatActive) return;
            int bounded = (int)Math.Min(delta, MaximumPollMilliseconds);
            Advance(checked(bounded * RealTimeScale));
        }

        public void AdvanceRealTime(int elapsedRealMilliseconds)
        {
            if (elapsedRealMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedRealMilliseconds));
            Advance(checked(elapsedRealMilliseconds * RealTimeScale));
        }

        public void AdvanceTurnBasedRound() => Advance(TurnBasedRoundMilliseconds);

        public void Advance(int elapsedSourceMilliseconds)
        {
            if (elapsedSourceMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSourceMilliseconds));
            if (elapsedSourceMilliseconds == 0) return;
            ElapsedMilliseconds = checked(ElapsedMilliseconds + elapsedSourceMilliseconds);
            Advanced?.Invoke(new SourceTimeAdvance(elapsedSourceMilliseconds, ElapsedMilliseconds));
        }

        internal void Restore(long elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            ElapsedMilliseconds = elapsedMilliseconds;
            _lastMonotonicMilliseconds = null;
        }

        private static long ToMonotonicMilliseconds(long timestamp)
            => (long)(timestamp * (1000d / Stopwatch.Frequency));
    }
}
