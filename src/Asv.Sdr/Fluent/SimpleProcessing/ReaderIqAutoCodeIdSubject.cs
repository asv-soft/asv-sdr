using System;
using System.Collections.Generic;
using System.Text;
using Asv.Common;

namespace Asv.Sdr
{
    /// <summary>
    /// Parameter-free identifier (Morse) decoder for the per-window AM stream.
    /// <para>
    /// Unlike <see cref="ReaderIqCodeIdSubject"/> it takes no amplitude window
    /// (amMin/amMax) and no dot length: it self-calibrates everything from the signal.
    /// </para>
    /// <list type="number">
    /// <item>An adaptive slicer (online two-means level tracker + hysteresis + debounce)
    /// converts the raw AM into a clean keyed on/off stream without fixed thresholds.</item>
    /// <item>Run lengths between transitions become timed events.</item>
    /// <item>The dot unit is estimated by snapping every event to the Morse 1:3:7 ratio
    /// and taking the median implied unit; dot/dash/symbol-pause/char-pause widths follow.</item>
    /// <item>The repeated identifier group is recovered by majority vote across repeats,
    /// so a single corrupted element does not change the reported code.</item>
    /// </list>
    /// One window lasts <c>fftBufferSize / sampleRate</c> seconds.
    /// </summary>
    public sealed class ReaderIqAutoCodeIdSubject : DisposableOnce, IObservable<CodeId>
    {
        // --- adaptive amplitude slicer ---
        private const double LevelTrackTimeMs = 600.0; // two-means level adaptation time constant
        private const double LevelLeakTimeMs = 8000.0; // held ON level relaxes toward OFF in silence
        private const double OnThresholdFraction = 0.60; // OFF -> ON crossing inside [off..on]
        private const double OffThresholdFraction = 0.40; // ON -> OFF crossing (hysteresis)
        private const double MinContrast = 0.03; // minimum on-off separation to accept a keyed signal
        private const int DebounceWindows = 2; // consecutive windows required to confirm a transition

        // --- morse timing model ---
        private const int MaxEvents = 96; // bounded history (~ several identifier repeats)
        private const int MinEventsForDecode = 5;
        private const int CalibrationIterations = 5;
        private const double DotDashSplitUnits = 2.0; // signal < 2u = dot, otherwise dash
        private const double SymbolGapMaxUnits = 2.0; // gap < 2u = intra-character
        private const double CharGapMaxUnits = 5.0; // gap < 5u = inter-character, otherwise word gap
        private const double MinUnitMs = 20.0;
        private const double MaxUnitMs = 1000.0;
        private const double MinDashDotRatio = 2.2; // sanity gate: reject non-standard ~2:1 keying
        private const double MaxDashDotRatio = 6.0;

        // --- repeated-code extraction ---
        private const int MaxCodeChars = 16;
        private const int MaxRecentGroups = 8; // identifier groups kept for the majority vote
        private const int MinRepeats = 2; // need >= 2 matching groups to publish a code

        private static readonly int[] SignalLevels = { 1, 3 };
        private static readonly int[] GapLevels = { 1, 3, 7 };

        private readonly System.Reactive.Subjects.Subject<CodeId> _subject = new();
        private readonly IDisposable _subscribe;
        private readonly double _windowMs;
        private readonly double _levelAlpha;
        private readonly double _levelLeak;

        private bool _levelsInitialized;
        private double _onLevel;
        private double _offLevel;
        private bool _hysteresisOn;
        private bool _gateState;
        private int _gatePending;

        private bool _hasRun;
        private bool _runIsSignal;
        private double _runMs;
        private readonly List<MorseEvent> _events = new();
        private string _lastPublished = string.Empty;

        private readonly struct MorseEvent(bool isSignal, double durationMs)
        {
            public bool IsSignal => isSignal;
            public double DurationMs => durationMs;
        }

        private readonly struct MorseModel(
            double dotMs,
            double dashMs,
            double symbolPauseMs,
            double charPauseMs
        )
        {
            public double DotMs => dotMs;
            public double DashMs => dashMs;
            public double SymbolPauseMs => symbolPauseMs;
            public double CharPauseMs => charPauseMs;
        }

        public ReaderIqAutoCodeIdSubject(
            IObservable<double> amSubject,
            int fftBufferSize,
            int sampleRate
        )
        {
            if (fftBufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fftBufferSize));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            _windowMs = fftBufferSize * 1000.0 / sampleRate;
            _levelAlpha = Math.Clamp(_windowMs / LevelTrackTimeMs, 0.0, 1.0);
            _levelLeak = Math.Clamp(_windowMs / LevelLeakTimeMs, 0.0, 1.0);
            _subscribe = amSubject.Subscribe(OnNext);
        }

        public IDisposable Subscribe(IObserver<CodeId> observer)
        {
            return _subject.Subscribe(observer);
        }

        protected override void InternalDisposeOnce()
        {
            _subscribe.Dispose();
            _subject.OnCompleted();
            _subject.Dispose();
        }

        private void OnNext(double am)
        {
            var isSignal = Slice(am);
            AccumulateRun(isSignal);
        }

        // --- stage 1: adaptive slicing -------------------------------------------------

        private bool Slice(double am)
        {
            UpdateLevels(am);

            var contrast = _onLevel - _offLevel;
            bool raw;
            if (contrast < MinContrast)
            {
                // No keyed carrier present (silence/noise): force OFF.
                raw = false;
            }
            else
            {
                var onThreshold = _offLevel + (contrast * OnThresholdFraction);
                var offThreshold = _offLevel + (contrast * OffThresholdFraction);
                // Schmitt trigger: different thresholds for rising and falling edges.
                raw = _hysteresisOn ? am >= offThreshold : am >= onThreshold;
            }

            _hysteresisOn = raw;

            // Debounce: only commit after DebounceWindows consecutive agreeing windows,
            // so a lone noisy window cannot split an element or inject a phantom gap.
            if (raw == _gateState)
            {
                _gatePending = 0;
                return _gateState;
            }

            if (++_gatePending >= DebounceWindows)
            {
                _gateState = raw;
                _gatePending = 0;
            }

            return _gateState;
        }

        private void UpdateLevels(double am)
        {
            if (_levelsInitialized == false)
            {
                _onLevel = am;
                _offLevel = am;
                _levelsInitialized = true;
                return;
            }

            // Online two-means classifier: each sample nudges the nearer of the two level
            // estimates. During silence only the OFF level tracks, so the ON (tone) level
            // is held across the gaps between identifier groups instead of decaying away.
            var mid = (_onLevel + _offLevel) * 0.5;
            if (am >= mid)
            {
                _onLevel += (am - _onLevel) * _levelAlpha;
            }
            else
            {
                _offLevel += (am - _offLevel) * _levelAlpha;

                // Relax the held ON level toward OFF very slowly, so it stays warm across
                // the gaps between identifier repeats but a weaker signal returning after
                // a long silence can still pull it back up and be detected.
                _onLevel += (_offLevel - _onLevel) * _levelLeak;
            }
        }

        // --- stage 2: run-length events ------------------------------------------------

        private void AccumulateRun(bool isSignal)
        {
            if (_hasRun == false)
            {
                _hasRun = true;
                _runIsSignal = isSignal;
                _runMs = _windowMs;
                return;
            }

            if (isSignal == _runIsSignal)
            {
                _runMs += _windowMs;
                return;
            }

            PushEvent(_runIsSignal, _runMs);
            _runIsSignal = isSignal;
            _runMs = _windowMs;
        }

        private void PushEvent(bool isSignal, double durationMs)
        {
            _events.Add(new MorseEvent(isSignal, durationMs));
            if (_events.Count > MaxEvents)
            {
                _events.RemoveRange(0, _events.Count - MaxEvents);
            }

            TryPublish();
        }

        // --- stage 3 + 4: calibrate, decode, publish -----------------------------------

        private void TryPublish()
        {
            if (TryCalibrate(out var unit, out var model) == false)
            {
                return;
            }

            if (TryDecode(unit, out var code, out var wordBoundary) == false)
            {
                return;
            }

            // Publish a newly recognised code immediately; refresh an unchanged code only
            // after a word gap so statistics/age stay current without spamming.
            if (code == _lastPublished && wordBoundary == false)
            {
                return;
            }

            _lastPublished = code;
            _subject.OnNext(
                new CodeId(
                    code,
                    model.DashMs,
                    model.DotMs,
                    model.SymbolPauseMs,
                    model.CharPauseMs,
                    _offLevel,
                    _onLevel
                )
            );
        }

        private bool TryCalibrate(out double unit, out MorseModel model)
        {
            unit = 0;
            model = default;
            if (_events.Count < MinEventsForDecode)
            {
                return false;
            }

            // Seed the dot unit from a robust low duration, then refine by snapping every
            // event to the nearest Morse multiple and taking the median implied unit.
            var seed = LowPercentileDuration(0.2);
            if (seed < MinUnitMs)
            {
                seed = MinUnitMs;
            }

            unit = seed;
            var implied = new List<double>(_events.Count);
            for (var iter = 0; iter < CalibrationIterations; iter++)
            {
                implied.Clear();
                foreach (var item in _events)
                {
                    var levels = item.IsSignal ? SignalLevels : GapLevels;
                    var multiple = NearestLevel(item.DurationMs, unit, levels);
                    if (multiple > 0)
                    {
                        implied.Add(item.DurationMs / multiple);
                    }
                }

                if (implied.Count == 0)
                {
                    return false;
                }

                implied.Sort();
                unit = Median(implied);
            }

            // Guard against locking onto a multiple of the true dot. When the signal has
            // no 1-unit elements (e.g. an all-dash identifier such as "TT") the median
            // settles on 3x or 7x the real dot; the sub-multiple that explains the events
            // at least as well is the true unit.
            unit = RefineUnit(unit);

            if (unit < MinUnitMs || unit > MaxUnitMs)
            {
                return false;
            }

            return BuildModel(unit, out model);
        }

        private bool BuildModel(double unit, out MorseModel model)
        {
            model = default;
            double dotSum = 0;
            double dashSum = 0;
            double symbolSum = 0;
            double charSum = 0;
            var dotCount = 0;
            var dashCount = 0;
            var symbolCount = 0;
            var charCount = 0;

            foreach (var item in _events)
            {
                var units = item.DurationMs / unit;
                if (item.IsSignal)
                {
                    if (units < DotDashSplitUnits)
                    {
                        dotSum += item.DurationMs;
                        ++dotCount;
                    }
                    else
                    {
                        dashSum += item.DurationMs;
                        ++dashCount;
                    }
                }
                else if (units < SymbolGapMaxUnits)
                {
                    symbolSum += item.DurationMs;
                    ++symbolCount;
                }
                else if (units < CharGapMaxUnits)
                {
                    charSum += item.DurationMs;
                    ++charCount;
                }
            }

            // Require a plausible dash/dot ratio when both are present, otherwise the
            // calibration has locked onto noise rather than a real identifier.
            if (dotCount > 0 && dashCount > 0)
            {
                var ratio = (dashSum / dashCount) / (dotSum / dotCount);
                if (ratio < MinDashDotRatio || ratio > MaxDashDotRatio)
                {
                    return false;
                }
            }

            model = new MorseModel(
                dotCount > 0 ? dotSum / dotCount : unit,
                dashCount > 0 ? dashSum / dashCount : unit * 3.0,
                symbolCount > 0 ? symbolSum / symbolCount : unit,
                charCount > 0 ? charSum / charCount : unit * 3.0
            );
            return true;
        }

        private bool TryDecode(double unit, out string code, out bool wordBoundary)
        {
            code = string.Empty;

            // Split the event stream into identifier groups. Gaps come in three sizes:
            // intra-character (~1u), inter-character (~3u) and the word gap (>=5u) that
            // separates one transmitted identifier from the next repeat. Anchoring on the
            // word gap fixes the phase, so the reported code is never a rotation.
            var groups = new List<string>();
            var current = new List<char>();
            var symbol = new StringBuilder();
            foreach (var item in _events)
            {
                if (item.IsSignal)
                {
                    symbol.Append(item.DurationMs / unit < DotDashSplitUnits ? '.' : '-');
                    continue;
                }

                var units = item.DurationMs / unit;
                if (units < SymbolGapMaxUnits)
                {
                    // intra-character gap: the current symbol keeps growing
                    continue;
                }

                FlushSymbol(symbol, current); // close the character

                if (units >= CharGapMaxUnits && current.Count > 0)
                {
                    // word gap: a transmitted identifier group has ended
                    groups.Add(new string(current.ToArray()));
                    current.Clear();
                }
            }

            // The trailing open group has no closing word gap yet, so it is ignored.
            var last = _events[^1];
            wordBoundary = last.IsSignal == false && last.DurationMs / unit >= CharGapMaxUnits;

            return TryMajorityGroup(groups, out code);
        }

        private static void FlushSymbol(StringBuilder symbol, List<char> chars)
        {
            if (symbol.Length == 0)
            {
                return;
            }

            // '?' keeps the character count (and therefore the period alignment) intact
            // when a corrupted element produces an invalid pattern.
            chars.Add(MorseAlphabet.CodeToChar.GetValueOrDefault(symbol.ToString(), '?'));
            symbol.Clear();
        }

        private static bool TryMajorityGroup(List<string> groups, out string code)
        {
            code = string.Empty;
            if (groups.Count < MinRepeats)
            {
                return false;
            }

            // Consider only the most recent groups (the identifier currently on air).
            var first = Math.Max(0, groups.Count - MaxRecentGroups);

            // Pick the most common group length; a corrupted repeat usually has a
            // different length and is therefore excluded from the vote.
            var bestLength = 0;
            var bestLengthCount = 0;
            for (var i = first; i < groups.Count; i++)
            {
                var length = groups[i].Length;
                if (length <= 0 || length > MaxCodeChars)
                {
                    continue;
                }

                var count = 0;
                for (var j = first; j < groups.Count; j++)
                {
                    if (groups[j].Length == length)
                    {
                        ++count;
                    }
                }

                if (count > bestLengthCount)
                {
                    bestLengthCount = count;
                    bestLength = length;
                }
            }

            if (bestLength == 0 || bestLengthCount < MinRepeats)
            {
                return false;
            }

            // Majority-vote each position across the groups of that length, so a single
            // corrupted element cannot change the published identifier.
            var voted = new char[bestLength];
            for (var position = 0; position < bestLength; position++)
            {
                voted[position] = MajorityCharAt(groups, first, bestLength, position);
            }

            var candidate = new string(voted);
            if (candidate.IndexOf('?') >= 0)
            {
                return false;
            }

            code = candidate;
            return true;
        }

        private static char MajorityCharAt(List<string> groups, int first, int length, int position)
        {
            var counts = new Dictionary<char, int>();
            var best = '?';
            var bestCount = 0;
            for (var i = first; i < groups.Count; i++)
            {
                if (groups[i].Length != length)
                {
                    continue;
                }

                var value = groups[i][position];
                var current = counts.GetValueOrDefault(value, 0) + 1;
                counts[value] = current;
                if (current > bestCount)
                {
                    bestCount = current;
                    best = value;
                }
            }

            return best;
        }

        private double RefineUnit(double unit)
        {
            var best = unit;
            var bestError = SnapError(unit);

            // A median lock happens at an integer Morse multiple of the true dot (3 or 7).
            foreach (var divisor in new[] { 3.0, 7.0 })
            {
                var candidate = unit / divisor;
                if (candidate < MinUnitMs)
                {
                    continue;
                }

                var error = SnapError(candidate);
                // Prefer the smaller (more fundamental) unit unless it is clearly worse.
                if (error <= bestError * 1.05)
                {
                    best = candidate;
                    bestError = error;
                }
            }

            return best;
        }

        private double SnapError(double unit)
        {
            if (_events.Count == 0)
            {
                return double.MaxValue;
            }

            var total = 0.0;
            foreach (var item in _events)
            {
                var levels = item.IsSignal ? SignalLevels : GapLevels;
                var multiple = NearestLevel(item.DurationMs, unit, levels);
                total += Math.Abs(item.DurationMs - (multiple * unit)) / unit;
            }

            return total / _events.Count;
        }

        private static int NearestLevel(double durationMs, double unit, int[] levels)
        {
            var best = 0;
            var bestError = double.MaxValue;
            foreach (var level in levels)
            {
                var error = Math.Abs(durationMs - (level * unit));
                if (error < bestError)
                {
                    bestError = error;
                    best = level;
                }
            }

            return best;
        }

        private double LowPercentileDuration(double percentile)
        {
            var durations = new double[_events.Count];
            for (var i = 0; i < _events.Count; i++)
            {
                durations[i] = _events[i].DurationMs;
            }

            Array.Sort(durations);
            var index = (int)Math.Clamp(Math.Round(percentile * (durations.Length - 1)), 0, durations.Length - 1);
            return durations[index];
        }

        private static double Median(List<double> sorted)
        {
            var count = sorted.Count;
            if (count == 0)
            {
                return 0;
            }

            return count % 2 == 1
                ? sorted[count / 2]
                : (sorted[(count / 2) - 1] + sorted[count / 2]) * 0.5;
        }
    }
}
