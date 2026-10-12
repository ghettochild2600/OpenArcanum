using System;
using System.Diagnostics;
using Arcanum.Runtime.Campaign;

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
        public const int InitialYear = 1885;
        public const int InitialHour = 15;
        public const long InitialMilliseconds = InitialHour * 3_600_000L;
        public const int RealTimeScale = 8;
        public const int MinimumPollMilliseconds = 5;
        public const int MaximumPollMilliseconds = 250;
        public const int TurnBasedRoundMilliseconds = 1_000;

        private long? _lastMonotonicMilliseconds;

        public long ElapsedMilliseconds { get; private set; }
        public long CampaignMilliseconds => InitialMilliseconds + ElapsedMilliseconds;
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

    public static class SourceCalendar
    {
        private const long DayMilliseconds = 86_400_000L;
        private static readonly string[] Months =
            { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        public static QuestTimestamp Timestamp(long campaignMilliseconds)
        {
            if (campaignMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(campaignMilliseconds));
            return new QuestTimestamp((uint)(campaignMilliseconds / DayMilliseconds),
                (uint)(campaignMilliseconds % DayMilliseconds));
        }

        public static string FormatLogbook(QuestTimestamp timestamp)
        {
            DateParts(timestamp, out int year, out int month, out int day, out int hour, out int minute, out _);
            int displayHour = hour % 12;
            if (displayHour == 0) displayHour = 12;
            return $"{Months[month - 1]} {day}, {year}  {displayHour}:{minute:00}{(hour < 12 ? 'a' : 'p')}m";
        }

        public static string FormatHud(long campaignMilliseconds)
        {
            QuestTimestamp timestamp = Timestamp(campaignMilliseconds);
            DateParts(timestamp, out int year, out int month, out int day, out int hour, out int minute, out int second);
            return $"Current Time: {hour:00}:{minute:00}:{second:00}   Current Date: {month}/{day}/{year}";
        }

        private static void DateParts(QuestTimestamp timestamp, out int year, out int month, out int day,
            out int hour, out int minute, out int second)
        {
            year = SourceTimeService.InitialYear + (int)timestamp.Days / 360;
            month = (int)timestamp.Days / 30 % 12 + 1;
            day = (int)timestamp.Days % 30 + 1;
            hour = (int)(timestamp.Milliseconds / 3_600_000u) % 24;
            minute = (int)(timestamp.Milliseconds / 60_000u) % 60;
            second = (int)(timestamp.Milliseconds / 1_000u) % 60;
        }
    }
}
