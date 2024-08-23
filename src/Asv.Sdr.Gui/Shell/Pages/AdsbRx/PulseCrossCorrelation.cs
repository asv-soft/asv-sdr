using System;
using System.Collections.Immutable;
using System.Linq;
using Asv.Common;

namespace Asv.Sdr.Gui;

public class AdsbSignalProcessor
{
    private readonly Action<double[]> _onMessage;
    private static readonly byte[] Preamble = [1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0];
    private const double Threshold = 0.0;
    private const int MaxAdsbMessageSize = 112;
    private const int AdsbHalfBitRate = 2_000_000;
    
    private readonly PulseCrossCorrelation _correlation;
    private readonly CircularBuffer2<double> _rawBuffer;
    private readonly CircularBuffer2<double> _correlationBuffer;
    private State _state;
    

    public AdsbSignalProcessor(int sampleRate, Action<double[]> onMessage)
    {
        _onMessage = onMessage;
        _correlation = new PulseCrossCorrelation(sampleRate, AdsbHalfBitRate, Preamble);
        _correlationBuffer = new CircularBuffer2<double>(Preamble.Length * _correlation.PulseLength);
        _rawBuffer = new CircularBuffer2<double>((Preamble.Length * 2/**/ + MaxAdsbMessageSize) * _correlation.PulseLength*2);
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
                if (riseCorr > Threshold)
                {
                    _state = State.Fall;
                    _correlationBuffer.Clear();
                    _correlationBuffer.PushBack(riseCorr);
                    _rawBuffer.Clear();
                    _rawBuffer.PushBack(input);
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
        var buff = _rawBuffer.ToArray();
        var avg = buff.Average();
        var plot = new double[2 * buff.Length];
        for (int i = 0; i < buff.Length; i++)
        {
            var val = buff[i] > avg ? 1 : 0;
            plot[i * 2] = val;
            plot[i * 2 + 1] = val;
        }
        _onMessage.Invoke(plot);
    }
}

public class PulseCrossCorrelation:IDspFilter
{
    private readonly int _pulseLength;
    private readonly ImmutableArray<double> _template;
    private readonly CircularBuffer2<double> _buffer;

    public PulseCrossCorrelation(int sampleRate, int bitRate,byte[] puleTemplate)
    {
        if (_pulseLength % bitRate != 0) throw new Exception("Invalid bit rate. Must be a multiple of sample rate");
        _pulseLength = sampleRate / bitRate;
        _buffer = new CircularBuffer2<double>(puleTemplate.Length * _pulseLength);
        var builder = ImmutableArray.CreateBuilder<double>(puleTemplate.Length * _pulseLength);
        for (var i = 0; i < puleTemplate.Length; i++)
        {
            var val = puleTemplate[i];
            if (val is not (0 or 1))
                throw new Exception("Invalid template value: must be 0 or 1");
            if (val == 0)
            {
                for (int j = 0; j < _pulseLength; j++)
                {
                    builder.Add(-1);
                }
            }
            else
            {
                for (int j = 0; j < _pulseLength; j++)
                {
                    builder.Add(1);
                }
            }
        }
        _template = builder.ToImmutable();
    }

    public int PulseLength => _pulseLength;
    public double Process(double input)
    {
        _buffer.PushFront(input);
        if (_buffer.IsFull == false) return 0;
        var summ = 0.0;
        for (var i = 0; i < _template.Length; i++)
        {
            summ+=_template[i] * _buffer[i];
        }
        return summ;
    }
    
    public void Reset()
    {
        _buffer.Clear();
    }
}