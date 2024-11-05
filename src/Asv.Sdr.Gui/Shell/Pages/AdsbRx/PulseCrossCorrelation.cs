using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reactive.Linq;
using Asv.Common;
using Asv.IO;

namespace Asv.Sdr.Gui;

public delegate void DebugPlotDelegate(string name, double[] data, DataType type, PlotNumber plotNumber);

public enum DataType
{
    Clear,
    Bar,
    Signal,
    HorizontalLine,
    VerticalLine
}

public enum PlotNumber
{
    Plot1,
    Plot2,
}


public class AdsbSignalProcessor
{
    private readonly DebugPlotDelegate? _debugCallback;
    private static readonly byte[] Preamble = [1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0];
    private const double Threshold = 0.0;
    private const int MaxAdsbMessageSize = 112;
    private const int PrefixPostfixSize = 10;
    private const int AdsbHalfBitRate = 2_000_000;
    private const int MinAdsbMessageSize = 56;
    
    private readonly PulseCrossCorrelation _correlation;
    private readonly CircularBuffer2<double> _rawBuffer;
    private readonly CircularBuffer2<double> _correlationBuffer;
    private State _state;
    private readonly int _prefixPulseSize;
    private readonly int _avgSize;
    private readonly AdsbMessageParser _parser;
    private readonly AdsbBitDecoder _parser2;
    private readonly object _sync = new();
    private readonly int _bitPulseMinimumLevel;
    private readonly int _pulseLength;

    public AdsbSignalProcessor(int sampleRate, DebugPlotDelegate? debugCallback)
    {
        _debugCallback = debugCallback;
        _pulseLength = sampleRate / AdsbHalfBitRate;
        if (sampleRate % AdsbHalfBitRate != 0)
        {
            throw new ArgumentException($"Sample rate must be multiple of {AdsbHalfBitRate}");
        }
        _correlation = new PulseCrossCorrelation(_pulseLength, Preamble);
        
        _correlationBuffer = new CircularBuffer2<double>(Preamble.Length * _pulseLength);
        _rawBuffer = new CircularBuffer2<double>( (Preamble.Length + MaxAdsbMessageSize * 2  /*cause 1 bit is 2 pulse*/ + PrefixPostfixSize*2) * _pulseLength);
        _prefixPulseSize = PrefixPostfixSize * _pulseLength;
        _avgSize = (Preamble.Length + MinAdsbMessageSize * 2  /*cause 1 bit is 2 pulse*/) * _pulseLength;
        _bitPulseMinimumLevel = _pulseLength / 2;
        
        
        _parser = new AdsbMessageParser();
        //_parser.Register(()=>new AdsbAirbornePosition());
        _parser.Register(()=>new AdsbAircraftIdentification());
        _parser2 = new AdsbBitDecoder();
        _parser.OnMessageRecev.Subscribe(x =>
        {
            Console.WriteLine($"{DateTime.Now:O} {x}");
        });
        var timeBuffer = new double[1000];
        Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
            .Subscribe(x =>
            {
                lock (_sync)
                {
                    _debugCallback.Invoke("Time", [], DataType.Clear, PlotNumber.Plot2);
                    _debugCallback.Invoke("Time", timeBuffer, DataType.Bar, PlotNumber.Plot2);
                }
                
            });
        _parser2.FrameReceived+= (frame, actualLength) =>
        {
            lock (_sync)
            {
                timeBuffer[DateTime.Now.Millisecond] += 1;
                _parser2.Reset();
            }
            Console.WriteLine($"{DateTime.Now:O} {AdsbBitDecoder.GetICAOAddress(frame):X}");
        };

    }

    enum State
    {
        Rise,
        Fall,
        Data,
    }

    
    public void Process(double input)
    {
        switch (_state)
        {
            case State.Rise:
                var riseCorr = _correlation.Process(input);
                _rawBuffer.PushBack(input);
                if (riseCorr > Threshold)
                {
                    _state = State.Fall;
                    _correlationBuffer.Clear();
                    _correlationBuffer.PushBack(riseCorr);
                    while(_rawBuffer.Size > _prefixPulseSize)
                    {
                        _rawBuffer.PopFront();
                    }
                }
                break;
            case State.Fall:
                var fallCorr = _correlation.Process(input);
                _correlationBuffer.PushBack(fallCorr);
                _rawBuffer.PushBack(input);
                // found new rise correlation
                if (_correlationBuffer.IsFull)
                {
                    var maxIndex = 0;
                    var maxValue = double.MinValue;
                    for (int i = 0; i < _correlationBuffer.Size; i++)
                    {
                        if (_correlationBuffer[i] > maxValue)
                        {
                            maxValue = _correlationBuffer[i];
                            maxIndex = i;
                        }
                    }
                    for (int i = 0; i < maxIndex; i++)
                    {
                        _correlationBuffer.PopFront();
                    }
                    _state = State.Data;
                    _correlation.Reset();
                }
                break;
            case State.Data:
                _rawBuffer.PushBack(input);
                if (_rawBuffer.IsFull)
                {
                    _state = State.Rise;
                    TryFindMessage();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    private void TryFindMessage()
    {
        // clear all plots
        _debugCallback?.Invoke(string.Empty, [], DataType.Clear, PlotNumber.Plot1);
        
        // calculate average for minimum size of message (skip prefix)
        var avg = 0.0;
        for (int i = 0; i < _avgSize; i++)
        {
            avg+=_rawBuffer[_prefixPulseSize + i];
        }
        _debugCallback?.Invoke("Raw signal", _rawBuffer.ToArray(), DataType.Signal, PlotNumber.Plot1);
        avg /= _avgSize;
        _debugCallback?.Invoke("Average", [avg], DataType.HorizontalLine, PlotNumber.Plot1);
        
        if (_debugCallback != null)
        {
            var normilizedBuffer = new double[_rawBuffer.Size];
            for (var i = 0; i < _rawBuffer.Size; i ++)
            {
                normilizedBuffer[i] = _rawBuffer[i] > avg ? avg * 2.0 : 0.0;
            }
            _debugCallback.Invoke("Normalized", normilizedBuffer, DataType.Bar, PlotNumber.Plot1);
        }
        
        // clear buffer before first rise
        while (_rawBuffer.IsEmpty == false && _rawBuffer[0] < avg)
        {
            _rawBuffer.PopFront(); 
        }
        

        while (_rawBuffer.IsEmpty == false && _rawBuffer.Size > _pulseLength)
        {
            var value = 0;
            var cnt = 0;
            for (var j = 0; j < _pulseLength; j++)
            {
                if (_rawBuffer[j] > avg)
                {
                    cnt++;
                }
            }
            if (cnt > _bitPulseMinimumLevel)
            {
                value = 1;
            }
            _parser2.ProcessSample(value);
            if (_parser.ProcessSample((byte)value) == true)
            {
                
            }
            for (var i = 0; i < _pulseLength; i++)
            {
                _rawBuffer.PopFront();
            }
        }
        
       
    }

    public IObservable<AdsbDfMessageBase> OnMessage => _parser.OnMessage;
    public IObservable<string> OnMessageText => _parser.OnMessageRecev;
}
