using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Formats.Objects;
using Arcanum.Runtime.Save;
using Arcanum.Script;

namespace Arcanum.Runtime.World
{
    /// <summary>One saveable retail <c>TIMEEVENT_TYPE_SCRIPT</c> entry.</summary>
    public readonly struct ScheduledScriptCall
    {
        public long Sequence { get; }
        public long DueCampaignMilliseconds { get; }
        public int ScriptNumber { get; }
        public int StartLine { get; }
        public ArcanumObjectId Triggerer { get; }
        public ArcanumObjectId Attachee { get; }

        internal ScheduledScriptCall(long sequence, long dueCampaignMilliseconds, int scriptNumber,
            int startLine, ArcanumObjectId triggerer, ArcanumObjectId attachee)
        {
            Sequence = sequence;
            DueCampaignMilliseconds = dueCampaignMilliseconds;
            ScriptNumber = scriptNumber;
            StartLine = startLine;
            Triggerer = triggerer;
            Attachee = attachee;
        }
    }

    /// <summary>
    /// Persistent source-game-time scheduler for script callbacks. Retail queues these in the saveable
    /// game-time list, inserts new equal-deadline entries before older ones, and defers calls created by a
    /// running callback until the next time-event pass.
    /// </summary>
    public sealed class ScriptTimerAuthority : IDisposable
    {
        public const int CallInGameTimeScale = SourceTimeService.RealTimeScale;
        private const long DayMilliseconds = 86_400_000L;
        private readonly WorldMapSessionCoordinator _session;
        private readonly SourceTimeService _time;
        private readonly List<ScheduledScriptCall> _calls = new();
        private long _nextSequence = 1;
        private bool _processing;

        internal ScriptTimerAuthority(WorldMapSessionCoordinator session, SourceTimeService time)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _time.Advanced += OnTimeAdvanced;
        }

        public IReadOnlyList<ScheduledScriptCall> Calls => _calls;
        public event Action<ScheduledScriptCall, ScriptExecutionResult> CallExecuted;

        public ScheduledScriptCall Schedule(int scriptNumber, int startLine, ArcanumObjectId triggerer,
            ArcanumObjectId attachee, int seconds, bool atTimeOfDay = false)
        {
            if (scriptNumber <= 0) throw new ArgumentOutOfRangeException(nameof(scriptNumber));
            if (startLine < 0) throw new ArgumentOutOfRangeException(nameof(startLine));
            if (!triggerer.IsPersistent) throw new ArgumentException("A persistent triggerer is required.", nameof(triggerer));
            if (!attachee.IsPersistent) throw new ArgumentException("A persistent attachee is required.", nameof(attachee));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));

            long now = _time.CampaignMilliseconds;
            long delay;
            if (atTimeOfDay)
            {
                long target = seconds % (DayMilliseconds / 1_000L) * 1_000L;
                long current = _time.ElapsedMilliseconds % DayMilliseconds;
                delay = target - current;
                if (delay < 0) delay += DayMilliseconds;
                if (delay == 0) delay = 1;
            }
            else
            {
                long sourceDelay = seconds == 0 ? 1L : checked(seconds * 1_000L);
                delay = checked(sourceDelay * CallInGameTimeScale);
            }

            var call = new ScheduledScriptCall(_nextSequence++, checked(now + delay), scriptNumber,
                startLine, triggerer, attachee);
            Insert(call);
            return call;
        }

        /// <summary>Retail <c>timeevent_clear_one_ex</c>: remove only the first queue entry matching script and attachee.</summary>
        public bool RemoveFirst(int scriptNumber, ArcanumObjectId attachee)
        {
            int index = _calls.FindIndex(value => value.ScriptNumber == scriptNumber && value.Attachee == attachee);
            if (index < 0) return false;
            _calls.RemoveAt(index);
            return true;
        }

        internal ScriptTimerSaveData ExportSaveData() => new()
        {
            NextSequence = _nextSequence,
            Calls = _calls.Select(value => new ScheduledScriptCallSaveData
            {
                Sequence = value.Sequence,
                DueCampaignMilliseconds = value.DueCampaignMilliseconds,
                ScriptNumber = value.ScriptNumber,
                StartLine = value.StartLine,
                TriggererIdentity = value.Triggerer.Key,
                AttacheeIdentity = value.Attachee.Key,
            }).ToList(),
        };

        internal void RestoreSaveData(ScriptTimerSaveData data)
        {
            _calls.Clear();
            _nextSequence = Math.Max(1, data?.NextSequence ?? 1);
            if (data?.Calls == null) return;
            foreach (ScheduledScriptCallSaveData value in data.Calls)
            {
                ArcanumObjectId.TryParsePersistent(value.TriggererIdentity, out ArcanumObjectId triggerer);
                ArcanumObjectId.TryParsePersistent(value.AttacheeIdentity, out ArcanumObjectId attachee);
                Insert(new ScheduledScriptCall(value.Sequence, value.DueCampaignMilliseconds,
                    value.ScriptNumber, value.StartLine, triggerer, attachee));
            }
        }

        public void Dispose() => _time.Advanced -= OnTimeAdvanced;

        private void OnTimeAdvanced(SourceTimeAdvance advance)
        {
            if (_processing || _calls.Count == 0) return;
            _processing = true;
            try
            {
                long passSequence = _nextSequence - 1;
                long passTime = SourceTimeService.InitialMilliseconds + advance.TotalMilliseconds;
                while (true)
                {
                    int index = _calls.FindIndex(value => value.DueCampaignMilliseconds <= passTime
                                                         && value.Sequence <= passSequence);
                    if (index < 0) break;
                    ScheduledScriptCall call = _calls[index];
                    _calls.RemoveAt(index);
                    ScriptExecutionResult result = _session.IsScriptObjectAlive(call.Triggerer)
                                                   && _session.IsScriptObjectAlive(call.Attachee)
                        ? _session.DispatchScheduledScript(call)
                        : new ScriptExecutionResult(ScriptExecutionStatus.InvalidContext);
                    CallExecuted?.Invoke(call, result);
                }
            }
            finally { _processing = false; }
        }

        private void Insert(ScheduledScriptCall call)
        {
            int index = _calls.FindIndex(value => value.DueCampaignMilliseconds > call.DueCampaignMilliseconds
                                                 || value.DueCampaignMilliseconds == call.DueCampaignMilliseconds
                                                 && value.Sequence < call.Sequence);
            if (index < 0) _calls.Add(call);
            else _calls.Insert(index, call);
        }
    }
}
