using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using Asv.Common;

namespace Asv.Sdr
{

    public readonly struct CodeId(
        string value, 
        double dashTimeMs, 
        double dotTimeMs, 
        double symbolPauseMs, 
        double charPauseMs,
        double minAm,
        double maxAm)
    {
        public string Value => value;
        public double DashTimeMs => dashTimeMs;
        public double DotTimeMs => dotTimeMs;
        public double SymbolPauseMs => symbolPauseMs;
        public double CharPauseMs => charPauseMs;
        public double MinAm => minAm;
        public double MaxAm => maxAm;
    }
    public class ReaderIqCodeIdSubject:DisposableOnce, IObservable<CodeId>
    {

        private static ReadOnlyDictionary<string, char> AlphabetData { get; } = new(new Dictionary<string, char>()
        {
            {
                ".-",
                'A'
            },
            {
                "-...",
                'B'
            },
            {
                "-.-.",
                'C'
            },
            {
                "-..",
                'D'
            },
            {
                ".",
                'E'
            },
            {
                "..-.",
                'F'
            },
            {
                "--.",
                'G'
            },
            {
                "....",
                'H'
            },
            {
                "..",
                'I'
            },
            {
                ".---",
                'J'
            },
            {
                "-.-",
                'K'
            },
            {
                ".-..",
                'L'
            },
            {
                "--",
                'M'
            },
            {
                "-.",
                'N'
            },
            {
                "---",
                'O'
            },
            {
                ".--.",
                'P'
            },
            {
                "--.-",
                'Q'
            },
            {
                ".-.",
                'R'
            },
            {
                "...",
                'S'
            },
            {
                "-",
                'T'
            },
            {
                "..-",
                'U'
            },
            {
                "...-",
                'V'
            },
            {
                ".--",
                'W'
            },
            {
                "-..-",
                'X'
            },
            {
                "-.--",
                'Y'
            },
            {
                "--..",
                'Z'
            },
            {
                ".----",
                '1'
            },
            {
                "..---",
                '2'
            },
            {
                "...--",
                '3'
            },
            {
                "....-",
                '4'
            },
            {
                ".....",
                '5'
            },
            {
                "-....",
                '6'
            },
            {
                "--...",
                '7'
            },
            {
                "---..",
                '8'
            },
            {
                "----.",
                '9'
            },
            {
                "-----",
                '0'
            },
            {
                "--..--",
                ','
            },
            {
                "..--..",
                '?'
            },
            {
                "---...",
                ':'
            },
            {
                "-....-",
                '-'
            },
            {
                ".-..-.",
                '"'
            },
            {
                "-.--.",
                '('
            },
            {
                "-...-",
                '='
            },
            {
                ".-.-.-",
                '.'
            },
            {
                "-.-.-.",
                ';'
            },
            {
                "-..-.",
                '/'
            },
            {
                ".----.",
                '\''
            },
            {
                "_.__._",
                ')'
            },
            {
                ".-.-.",
                '+'
            },
            {
                ".__._.",
                '@'
            },
            {
                " ",
                ' '
            }
        });

        private const double MinSignalDotUnits = 0.3;

        // Schmitt-trigger hysteresis + debounce for the raw per-window AM gate.
        // The rising edge uses _amMin; the falling edge uses a lower threshold so a tone
        // whose AM ripples near _amMin does not chatter on/off. A state change is only
        // committed after SignalDebounceWindows consecutive windows agree, so a single
        // noisy/edge window cannot split an element or inject a phantom gap. Because each
        // edge absorbs the same number of windows, element/pause durations are preserved.
        private const double SignalOffThresholdFactor = 0.7;
        private const int SignalDebounceWindows = 2;

        private const double DotDashDecisionDotUnits = 1.5;
        private const double SymbolPauseMaxDotUnits = 1.5;
        private const double CharacterPauseMaxDotUnits = 4.0;
        private const int MinRepeatedCodeChars = 2;
        private const int MaxRepeatedCodeChars = 8;
        private const double MinAutoDotTimeMs = 50.0;
        private const double MaxAutoDotTimeMs = 300.0;
        private const double MaxAutoSignalDotUnits = 4.5;
        private const double AutoMinUnitMinDotUnits = 0.75;
        private const double AutoMinUnitMaxDotUnits = 1.25;
        private const double AutoWordPauseMinDotUnits = 6.0;
        private const double MinLongSignalDotUnits = 1.6;
        private const double MaxAutoScore = 0.20;
        private const int MaxAutoEvents = 256;

        private readonly double _amMin;
        private readonly double _amMax;
        private readonly int _dotTimeMs;
        private readonly bool _autoDotTime;
        private readonly Subject<CodeId> _subject = new();
        private readonly IDisposable _subscribe;
        private State _state = State.SignalDown;
        private readonly double _oneSampleTimeMs;
        private double _signalTime;
        private double _noSignalTime;
        private bool _hasCodeActivity;
        private bool _gateSignalState;
        private int _gatePendingCount;
        private readonly List<CodeEvent> _events = new();

        private readonly List<char> _symbols = new();
        private readonly List<char> _word = new();
        
        private double _statDotTimeMs;
        private int _statDotCount;
        
        private double _statDashTimeMs;
        private int _statDashCount;
        
        private double _statCharPauseMs;
        private int _statCharPauseCount;
        
        private double _statSymbolPauseMs;
        private double _statSymbolCount;
        
        private double _amMaxValue;
        private double _amMinValue = double.MaxValue;
        
        private readonly struct CodeEvent(bool isSignal, double durationMs)
        {
            public bool IsSignal => isSignal;
            public double DurationMs => durationMs;
        }

        private readonly struct AutoDecodeResult(
            string value,
            double score,
            double dashTimeMs,
            double dotTimeMs,
            double symbolPauseMs,
            double charPauseMs)
        {
            public string Value => value;
            public double Score => score;
            public double DashTimeMs => dashTimeMs;
            public double DotTimeMs => dotTimeMs;
            public double SymbolPauseMs => symbolPauseMs;
            public double CharPauseMs => charPauseMs;
        }


        public ReaderIqCodeIdSubject(
            IObservable<double> amSubject,
            double amMin,
            double amMax,
            int fftBufferSize,
            int sampleRate)
            : this(amSubject, amMin, amMax, null, fftBufferSize, sampleRate)
        {
        }

        public ReaderIqCodeIdSubject(
            IObservable<double> amSubject,
            double amMin,
            double amMax,
            int dotTimeMs,
            int fftBufferSize,
            int sampleRate)
            : this(amSubject, amMin, amMax, (int?)dotTimeMs, fftBufferSize, sampleRate)
        {
        }

        private ReaderIqCodeIdSubject(
            IObservable<double> amSubject,
            double amMin,
            double amMax,
            int? dotTimeMs,
            int fftBufferSize,
            int sampleRate)
        {
            if (dotTimeMs.HasValue && dotTimeMs.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dotTimeMs));
            }

            if (fftBufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fftBufferSize));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            _amMin = amMin;
            _amMax = amMax;
            _dotTimeMs = dotTimeMs.GetValueOrDefault();
            _autoDotTime = dotTimeMs.HasValue == false;
            _subscribe = amSubject.Subscribe(OnNext);
            _oneSampleTimeMs = (fftBufferSize * 1000.0 / sampleRate);
        }

        private enum State
        {
            SignalUp,
            SignalDown
        }

        private void OnNext(double am)
        {
            RecordAmplitude(am);
            var isCodeSignal = GateSignal(am);
            
            switch (_state)
            {
                case State.SignalDown:
                    if (isCodeSignal)
                    {
                        ProcessPause(_noSignalTime);
                        _signalTime = _oneSampleTimeMs;
                        _state = State.SignalUp;
                    }
                    else
                    {
                        _noSignalTime += _oneSampleTimeMs;
                    }
                    break;
                case State.SignalUp:
                    if (isCodeSignal)
                    {
                        _signalTime += _oneSampleTimeMs;
                    }
                    else
                    {
                        _noSignalTime = _oneSampleTimeMs;
                        _state = State.SignalDown;
                        ProcessSignal(_signalTime);
                    }
                    break;

            }
        }

        private bool GateSignal(double am)
        {
            // Hysteresis: once the tone is latched, hold it until the AM drops below the
            // lower off-threshold (or exceeds _amMax); when off, require the full _amMin.
            var inBand = _gateSignalState
                ? am >= _amMin * SignalOffThresholdFactor && am <= _amMax
                : am >= _amMin && am <= _amMax;

            if (inBand == _gateSignalState)
            {
                _gatePendingCount = 0;
                return _gateSignalState;
            }

            // Debounce: flip only after SignalDebounceWindows consecutive opposing windows.
            // Until confirmed, the current window is absorbed into the current state, so a
            // lone noisy window cannot split an element or inject a phantom gap.
            if (++_gatePendingCount >= SignalDebounceWindows)
            {
                _gateSignalState = inBand;
                _gatePendingCount = 0;
            }

            return _gateSignalState;
        }

        private void RecordAmplitude(double am)
        {
            if (am > _amMaxValue)
            {
                _amMaxValue = am;
            }

            if (am < _amMinValue)
            {
                _amMinValue = am;
            }
        }

        private void ProcessSignal(double signalTimeMs)
        {
            if (_autoDotTime)
            {
                ProcessSignalAuto(signalTimeMs);
                return;
            }

            var units = signalTimeMs / _dotTimeMs;
            if (units < MinSignalDotUnits)
            {
                return;
            }

            _hasCodeActivity = true;
            if (units <= DotDashDecisionDotUnits)
            {
                _symbols.Add('.');
                _statDotTimeMs += signalTimeMs;
                ++_statDotCount;
            }
            else
            {
                _symbols.Add('-');
                _statDashTimeMs += signalTimeMs;
                ++_statDashCount;
            }
        }

        private void ProcessPause(double pauseTimeMs)
        {
            if (_hasCodeActivity == false)
            {
                return;
            }

            if (_autoDotTime)
            {
                ProcessPauseAuto(pauseTimeMs);
                return;
            }

            var units = pauseTimeMs / _dotTimeMs;
            if (units <= SymbolPauseMaxDotUnits)
            {
                _statSymbolPauseMs += pauseTimeMs;
                ++_statSymbolCount;
                return;
            }

            var publishedRepeatedWord = AppendCurrentSymbol();
            if (publishedRepeatedWord)
            {
                return;
            }

            if (units <= CharacterPauseMaxDotUnits)
            {
                _statCharPauseMs += pauseTimeMs;
                ++_statCharPauseCount;
                return;
            }

            PublishWord();
        }

        private void ProcessSignalAuto(double signalTimeMs)
        {
            var minSignalTimeMs = Math.Max(_oneSampleTimeMs * 1.5, MinAutoDotTimeMs * MinSignalDotUnits);
            if (signalTimeMs < minSignalTimeMs)
            {
                return;
            }

            _hasCodeActivity = true;
            _events.Add(new CodeEvent(true, signalTimeMs));
            TrimAutoEvents();
        }

        private void ProcessPauseAuto(double pauseTimeMs)
        {
            if (pauseTimeMs <= 0)
            {
                return;
            }

            _events.Add(new CodeEvent(false, pauseTimeMs));
            TrimAutoEvents();
            if (TryDecodeAuto(out var result))
            {
                PublishAuto(result);
            }
        }

        private void TrimAutoEvents()
        {
            if (_events.Count <= MaxAutoEvents)
            {
                return;
            }

            _events.RemoveRange(0, _events.Count - MaxAutoEvents);
        }

        private bool TryDecodeAuto(out AutoDecodeResult result)
        {
            result = default;
            if (_events.Count < 3)
            {
                return false;
            }

            var minDotTime = Math.Max(MinAutoDotTimeMs, _oneSampleTimeMs * 2.0);
            var step = Math.Max(1.0, _oneSampleTimeMs / 4.0);
            var hasBest = false;
            var best = default(AutoDecodeResult);

            for (var dotTimeMs = minDotTime; dotTimeMs <= MaxAutoDotTimeMs; dotTimeMs += step)
            {
                if (TryDecodeAuto(dotTimeMs, out var candidate) == false)
                {
                    continue;
                }

                if (candidate.Score > MaxAutoScore)
                {
                    continue;
                }

                if (hasBest == false || candidate.Score < best.Score)
                {
                    best = candidate;
                    hasBest = true;
                }
            }

            if (hasBest == false)
            {
                return false;
            }

            result = best;
            return true;
        }

        private bool TryDecodeAuto(double dotTimeMs, out AutoDecodeResult result)
        {
            result = default;
            var symbols = new List<char>();
            var word = new List<char>();

            var dotTimeTotal = 0.0;
            var dotCount = 0;
            var dashTimeTotal = 0.0;
            var dashCount = 0;
            var symbolPauseTotal = 0.0;
            var symbolPauseCount = 0;
            var charPauseTotal = 0.0;
            var charPauseCount = 0;
            var score = 0.0;
            var scoreCount = 0;
            var minUnitDurationMs = GetMinAutoUnitDuration();
            if (minUnitDurationMs <= 0)
            {
                return false;
            }

            var minUnitDotUnits = minUnitDurationMs / dotTimeMs;
            if (minUnitDotUnits < AutoMinUnitMinDotUnits || minUnitDotUnits > AutoMinUnitMaxDotUnits)
            {
                return false;
            }

            var maxSignalDurationMs = GetMaxAutoSignalDuration();
            var hasLongSignal = maxSignalDurationMs >= minUnitDurationMs * MinLongSignalDotUnits;
            score += Score(minUnitDotUnits, 1.0) * 4.0;
            scoreCount += 4;

            foreach (var item in _events)
            {
                var units = item.DurationMs / dotTimeMs;
                if (item.IsSignal)
                {
                    if (units < MinSignalDotUnits || units > MaxAutoSignalDotUnits)
                    {
                        return false;
                    }

                    if (units <= DotDashDecisionDotUnits)
                    {
                        symbols.Add('.');
                        dotTimeTotal += item.DurationMs;
                        ++dotCount;
                        score += Score(units, 1.0);
                    }
                    else
                    {
                        symbols.Add('-');
                        dashTimeTotal += item.DurationMs;
                        ++dashCount;
                        score += Math.Min(Score(units, 2.0), Score(units, 3.0));
                    }

                    ++scoreCount;
                    continue;
                }

                if (symbols.Count == 0)
                {
                    continue;
                }

                if (units <= SymbolPauseMaxDotUnits)
                {
                    symbolPauseTotal += item.DurationMs;
                    ++symbolPauseCount;
                    score += Score(units, 1.0);
                    ++scoreCount;
                    continue;
                }

                if (AppendSymbol(word, symbols) == false)
                {
                    return false;
                }

                if (units <= CharacterPauseMaxDotUnits)
                {
                    charPauseTotal += item.DurationMs;
                    ++charPauseCount;
                    score += Score(units, 3.0);
                    ++scoreCount;

                    var repeatedCode = GetRepeatedSuffix(word);
                    if (repeatedCode != null)
                    {
                        if (hasLongSignal && dashCount == 0)
                        {
                            return false;
                        }

                        result = CreateAutoDecodeResult(
                            repeatedCode,
                            score,
                            scoreCount,
                            dashTimeTotal,
                            dashCount,
                            dotTimeTotal,
                            dotCount,
                            symbolPauseTotal,
                            symbolPauseCount,
                            charPauseTotal,
                            charPauseCount);
                        return true;
                    }

                    continue;
                }

                if (units <= AutoWordPauseMinDotUnits)
                {
                    return false;
                }

                if (word.Count == 0)
                {
                    return false;
                }

                if (hasLongSignal && dashCount == 0)
                {
                    return false;
                }

                result = CreateAutoDecodeResult(
                    new string(word.ToArray()),
                    score,
                    scoreCount,
                    dashTimeTotal,
                    dashCount,
                    dotTimeTotal,
                    dotCount,
                    symbolPauseTotal,
                    symbolPauseCount,
                    charPauseTotal,
                    charPauseCount);
                return true;
            }

            return false;
        }

        private double GetMinAutoUnitDuration()
        {
            var minDuration = double.MaxValue;
            foreach (var item in _events)
            {
                if (item.DurationMs <= 0 || item.DurationMs > MaxAutoDotTimeMs * CharacterPauseMaxDotUnits)
                {
                    continue;
                }

                if (item.DurationMs < minDuration)
                {
                    minDuration = item.DurationMs;
                }
            }

            return minDuration == double.MaxValue ? 0 : minDuration;
        }

        private double GetMaxAutoSignalDuration()
        {
            var maxDuration = 0.0;
            foreach (var item in _events)
            {
                if (item.IsSignal == false)
                {
                    continue;
                }

                if (item.DurationMs > maxDuration)
                {
                    maxDuration = item.DurationMs;
                }
            }

            return maxDuration;
        }

        private static double Score(double actualUnits, double expectedUnits)
        {
            var delta = actualUnits - expectedUnits;
            return delta * delta;
        }

        private static bool AppendSymbol(List<char> word, List<char> symbols)
        {
            var str = new string(symbols.ToArray());
            var symbol = AlphabetData.GetValueOrDefault(str, '\0');
            symbols.Clear();
            if (symbol == '\0')
            {
                return false;
            }

            word.Add(symbol);
            return true;
        }

        private static string? GetRepeatedSuffix(IReadOnlyList<char> word)
        {
            var maxPeriod = Math.Min(MaxRepeatedCodeChars, word.Count / 2);
            for (var period = MinRepeatedCodeChars; period <= maxPeriod; period++)
            {
                if (HasRepeatedSuffix(word, period) == false)
                {
                    continue;
                }

                var start = word.Count - period;
                var value = new char[period];
                for (var i = 0; i < period; i++)
                {
                    value[i] = word[start + i];
                }

                return new string(value);
            }

            return null;
        }

        private static bool HasRepeatedSuffix(IReadOnlyList<char> word, int period)
        {
            var firstStart = word.Count - (period * 2);
            var secondStart = word.Count - period;
            for (var i = 0; i < period; i++)
            {
                if (word[firstStart + i] != word[secondStart + i])
                {
                    return false;
                }
            }

            return true;
        }

        private static AutoDecodeResult CreateAutoDecodeResult(
            string value,
            double score,
            int scoreCount,
            double dashTimeTotal,
            int dashCount,
            double dotTimeTotal,
            int dotCount,
            double symbolPauseTotal,
            int symbolPauseCount,
            double charPauseTotal,
            int charPauseCount)
        {
            return new AutoDecodeResult(
                value,
                Average(score, scoreCount),
                Average(dashTimeTotal, dashCount),
                Average(dotTimeTotal, dotCount),
                Average(symbolPauseTotal, symbolPauseCount),
                Average(charPauseTotal, charPauseCount));
        }

        private void PublishAuto(AutoDecodeResult result)
        {
            _subject.OnNext(new CodeId(
                result.Value,
                result.DashTimeMs,
                result.DotTimeMs,
                result.SymbolPauseMs,
                result.CharPauseMs,
                _amMinValue == double.MaxValue ? 0 : _amMinValue,
                _amMaxValue
            ));

            ResetWord();
        }

        private bool AppendCurrentSymbol()
        {
            if (_symbols.Count == 0)
            {
                return false;
            }

            var str = new string(_symbols.ToArray());
            var symbol = AlphabetData.GetValueOrDefault(str, '\0');
            _symbols.Clear();
            if (symbol != '\0')
            {
                _word.Add(symbol);
                return TryPublishRepeatedWord();
            }

            return false;
        }

        private bool TryPublishRepeatedWord()
        {
            var maxPeriod = Math.Min(MaxRepeatedCodeChars, _word.Count / 2);
            for (var period = MinRepeatedCodeChars; period <= maxPeriod; period++)
            {
                if (HasRepeatedSuffix(period) == false)
                {
                    continue;
                }

                var start = _word.Count - period;
                PublishWord(new string(_word.GetRange(start, period).ToArray()));
                return true;
            }

            return false;
        }

        private bool HasRepeatedSuffix(int period)
        {
            var firstStart = _word.Count - (period * 2);
            var secondStart = _word.Count - period;
            for (var i = 0; i < period; i++)
            {
                if (_word[firstStart + i] != _word[secondStart + i])
                {
                    return false;
                }
            }

            return true;
        }

        private void PublishWord(string? code = null)
        {
            var value = code ?? new string(_word.ToArray());
            if (value.Length != 0)
            {
                _subject.OnNext(new CodeId(
                    value,
                    Average(_statDashTimeMs, _statDashCount),
                    Average(_statDotTimeMs, _statDotCount),
                    Average(_statSymbolPauseMs, _statSymbolCount),
                    Average(_statCharPauseMs, _statCharPauseCount),
                    _amMinValue == double.MaxValue ? 0 : _amMinValue,
                    _amMaxValue
                ));
            }

            ResetWord();
        }

        private void ResetWord()
        {
            _word.Clear();
            _symbols.Clear();
            _events.Clear();
            _statDashCount = 0;
            _statDashTimeMs = 0;

            _statDotCount = 0;
            _statDotTimeMs = 0;

            _statSymbolPauseMs = 0;
            _statSymbolCount = 0;

            _statCharPauseMs = 0;
            _statCharPauseCount = 0;
            _amMaxValue = 0;
            _amMinValue = double.MaxValue;
            _hasCodeActivity = false;
        }

        private static double Average(double total, double count)
        {
            return count > 0 ? total / count : 0;
        }

        protected override void InternalDisposeOnce()
        {
            _subscribe.Dispose();
            _subject.OnCompleted();
            _subject.Dispose();
        }

        public IDisposable Subscribe(IObserver<CodeId> observer)
        {
            return _subject.Subscribe(observer);
        }

        
    }
}
