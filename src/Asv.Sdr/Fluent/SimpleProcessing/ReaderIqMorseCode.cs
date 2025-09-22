using System;
using System.Collections.Generic;
using System.Linq;
using Asv.Common;

namespace Asv.Sdr;

public readonly struct MorseData(
    IReadOnlyList<char> symbols,
    double dotTime,
    double dashTimeMs,
    double gapTimeMs,
    double amMin,
    double amMax,
    double amAvg)
{
    public IReadOnlyList<char> Symbols => symbols;
    public double DashTimeMs => dashTimeMs;
    public double GapTimeMs => gapTimeMs;
    public double DotTimeMs => dotTime;
    public double AmMin => amMin;
    public double AmAvg => amAvg;
    public double AmMax => amMax;
}

public class ReaderIqMorseSubject : DisposableOnce
{
    private readonly double _amMin;
    private readonly double _amMax;
    private readonly int _dotTimeMs;
    private readonly CircularBuffer2<double> _circularBuffer;
    private readonly double _oneSampleTimeMs;

    public ReaderIqMorseSubject(
        double amMin,
        double amMax,
        int dotTimeMs,
        int fftBufferSize,
        int sampleRate, int bufferSizeMs = 1000)
    {
        _amMin = amMin;
        _amMax = amMax;
        _dotTimeMs = dotTimeMs;
        _oneSampleTimeMs = (double) fftBufferSize * 1000.0 / (double) sampleRate;
        _circularBuffer = new CircularBuffer2<double>(bufferSizeMs / (int)_oneSampleTimeMs);
    }
  
    public MorseData Process(double am)
    {
        _circularBuffer.PushFront(am);
        return InternalProcess(_circularBuffer);
    }

    private MorseData InternalProcess(CircularBuffer2<double> buffer)
    {
        var signalUp = false;
        var signalTime = 0.0;
        var noSignalTime = 0.0;
        var symbols = new List<char>();
        var statDotTimeMs = 0.0;
        var statDotCount = 0;
        var statDashTimeMs = 0.0;
        var statDashCount = 0;
        var statGapTimeMs = 0.0;
        var statGapCount = 0;
        foreach (var am in buffer)
        {
            if (signalUp) // signal up
            {
                if (am > _amMin && am < _amMax) // still up => no changes
                {
                    signalTime += _oneSampleTimeMs;
                    continue;
                }
                // changed: up => down
                signalUp = false;
                noSignalTime = _oneSampleTimeMs;
                var delay = (int)Math.Round(signalTime / _dotTimeMs, 0);
                if (delay <= 1)
                {
                    symbols.Add('.');
                    statDotTimeMs +=(int)signalTime;
                    ++statDotCount;
                }
                else
                {
                    symbols.Add('-');
                    statDashTimeMs += (int)signalTime;
                    ++statDashCount;
                }
            }
            else // signal down
            {
                if (!(am > _amMin) || !(am < _amMax)) // still down => no changed
                {
                    noSignalTime += _oneSampleTimeMs;
                    continue;
                }
                // changed: down => up
                signalTime = _oneSampleTimeMs;
                signalUp = true;
                statGapTimeMs += noSignalTime;
                ++statGapCount;
            }
        }

        
        return new MorseData(
            symbols,
            dotTime: statDotTimeMs / statDotCount,
            dashTimeMs: statDashTimeMs / statDashCount,
            gapTimeMs: statGapTimeMs / statGapCount,
            amMin: buffer.Min(),
            amMax: buffer.Max(),
            amAvg: buffer.Average());

    }

    protected override void InternalDisposeOnce()
    {
        
    }
}