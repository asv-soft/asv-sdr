using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqCodeIdSubject : DisposableOnce, IObservable<string>
    {
        private static ReadOnlyDictionary<string, char> AlphabetData { get; } =
            new(
                new Dictionary<string, char>()
                {
                    { ".-", 'A' },
                    { "-...", 'B' },
                    { "-.-.", 'C' },
                    { "-..", 'D' },
                    { ".", 'E' },
                    { "..-.", 'F' },
                    { "--.", 'G' },
                    { "....", 'H' },
                    { "..", 'I' },
                    { ".---", 'J' },
                    { "-.-", 'K' },
                    { ".-..", 'L' },
                    { "--", 'M' },
                    { "-.", 'N' },
                    { "---", 'O' },
                    { ".--.", 'P' },
                    { "--.-", 'Q' },
                    { ".-.", 'R' },
                    { "...", 'S' },
                    { "-", 'T' },
                    { "..-", 'U' },
                    { "...-", 'V' },
                    { ".--", 'W' },
                    { "-..-", 'X' },
                    { "-.--", 'Y' },
                    { "--..", 'Z' },
                    { ".----", '1' },
                    { "..---", '2' },
                    { "...--", '3' },
                    { "....-", '4' },
                    { ".....", '5' },
                    { "-....", '6' },
                    { "--...", '7' },
                    { "---..", '8' },
                    { "----.", '9' },
                    { "-----", '0' },
                    { "--..--", ',' },
                    { "..--..", '?' },
                    { "---...", ':' },
                    { "-....-", '-' },
                    { ".-..-.", '"' },
                    { "-.--.", '(' },
                    { "-...-", '=' },
                    { ".-.-.-", '.' },
                    { "-.-.-.", ';' },
                    { "-..-.", '/' },
                    { ".----.", '\'' },
                    { "_.__._", ')' },
                    { ".-.-.", '+' },
                    { ".__._.", '@' },
                    { " ", ' ' },
                }
            );

        private readonly double _amMin;
        private readonly double _amMax;
        private readonly Subject<string> _subject = new();
        private readonly IDisposable _subscribe;
        private State _state = State.SignalUp;
        private readonly double _oneSampleTimeMs;
        private double _signalTime;
        private double _noSignalTime;

        private const int BetweenWordTime = 1000;
        private const double DotTimeTime = 150.0;
        private readonly List<char> _symbols = new();
        private readonly List<char> _word = new();
        private double _statDotTimeMs;
        private int _statDotCount;
        private double _statDashTimeMs;
        private int _statDashCount;
        private double _statCharPause;
        private int _statCharPauseCound;
        private double _statSymbolPause;

        public ReaderIqCodeIdSubject(
            IObservable<double> amSubject,
            double amMin,
            double amMax,
            int fftBufferSize,
            int sampleRate
        )
        {
            _amMin = amMin;
            _amMax = amMax;
            _subscribe = amSubject.Subscribe(OnNext);
            _oneSampleTimeMs = fftBufferSize * 1000.0 / sampleRate;
        }

        private enum State
        {
            SignalUp,
            SignalDown,
        }

        private void OnNext(double am)
        {
            switch (_state)
            {
                case State.SignalDown:
                    if (am > _amMin && am < _amMax)
                    {
                        _signalTime = _oneSampleTimeMs;
                        _state = State.SignalUp;
                        var delay = (int)Math.Round(_noSignalTime / DotTimeTime, 0);
                        switch (delay)
                        {
                            case <= 2:
                                _statSymbolPause += _noSignalTime;
                                ++_statCharPauseCound;
                                break;
                            case <= 4:
                            {
                                var str = new string(_symbols.ToArray());
                                _word.Add(AlphabetData.TryGetValue(str, out var ch) ? ch : '\0');
                                _symbols.Clear();
                                _statCharPause += _noSignalTime;
                                break;
                            }

                            default:
                            {
                                var str = new string(_symbols.ToArray());
                                _word.Add(AlphabetData.TryGetValue(str, out var ch) ? ch : '\0');
                                _symbols.Clear();
                                if (_word.Count != 0)
                                {
                                    var str1 = new string(_word.ToArray());
                                    _subject.OnNext(str1);
                                    _word.Clear();
                                    _symbols.Clear();
                                    _statDashCount = 0;
                                    _statDashTimeMs = 0;
                                    _statDotCount = 0;
                                    _statDotTimeMs = 0;
                                    _statSymbolPause = 0;
                                    _statCharPause = 0;
                                }

                                break;
                            }
                        }
                    }
                    else
                    {
                        _noSignalTime += _oneSampleTimeMs;
                    }

                    break;
                case State.SignalUp:
                    if (am > _amMin && am < _amMax)
                    {
                        _signalTime += _oneSampleTimeMs;
                    }
                    else
                    {
                        _noSignalTime = _oneSampleTimeMs;
                        _state = State.SignalDown;
                        var delay = (int)Math.Round(_signalTime / DotTimeTime, 0);
                        if (delay <= 2)
                        {
                            _symbols.Add('.');
                            _statDotTimeMs += (int)_signalTime;
                            ++_statDotCount;
                        }
                        else
                        {
                            _symbols.Add('-');
                            _statDashTimeMs += (int)_signalTime;
                            ++_statDashCount;
                        }
                    }

                    break;
            }
        }

        protected override void InternalDisposeOnce()
        {
            _subscribe.Dispose();
            _subject.OnCompleted();
            _subject.Dispose();
        }

        public IDisposable Subscribe(IObserver<string> observer)
        {
            return _subject.Subscribe(observer);
        }
    }
}
