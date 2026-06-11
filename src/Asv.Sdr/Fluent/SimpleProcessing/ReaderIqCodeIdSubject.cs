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
        private const double DotDashDecisionDotUnits = 1.5;
        private const double SymbolPauseMaxDotUnits = 1.5;
        private const double CharacterPauseMaxDotUnits = 4.0;
        private const int MinRepeatedCodeChars = 2;
        private const int MaxRepeatedCodeChars = 8;

        private readonly double _amMin;
        private readonly double _amMax;
        private readonly int _dotTimeMs;
        private readonly Subject<CodeId> _subject = new();
        private readonly IDisposable _subscribe;
        private State _state = State.SignalDown;
        private readonly double _oneSampleTimeMs;
        private double _signalTime;
        private double _noSignalTime;
        private bool _hasCodeActivity;

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
        


        public ReaderIqCodeIdSubject(IObservable<double> amSubject, double amMin, double amMax, int dotTimeMs, int fftBufferSize, int sampleRate)
        {
            _amMin = amMin;
            _amMax = amMax;
            _dotTimeMs = dotTimeMs;
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
            var isCodeSignal = am >= _amMin && am <= _amMax;
            
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

            _word.Clear();
            _symbols.Clear();
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
