using System;
using System.Reactive.Subjects;
using Asv.Common;
using Asv.Sdr.DebugPlot;

namespace Asv.Sdr;

public static class AdsbHelper
{
    private static readonly byte[] Preamble = [ 1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0];
    private const int AdsbHalfBitRate = 2_000_000;
    private const int AdsbPrefixPostfixCount = 10;
    private const int MaxAdsbPulseCount = 112 * 2 /*cause 1 bit is two pulse*/;
    private const int MinAdsbPulseCount = 56 * 2 /*cause 1 bit is two pulse*/;
    private const double AdsbCorrelationThreshold = 0;
    
    public static IReaderIqSubject<double> AdsbPulseDetector(this IReaderIqSubject<double> src, int sampleRate, IDebugPlot? plot = null)
    {
        return src.PulseDetector(sampleRate/AdsbHalfBitRate,Preamble,AdsbCorrelationThreshold,MaxAdsbPulseCount,AdsbPrefixPostfixCount,plot);
    }
    
    public static IReaderIqSubject<double> AdsbNormalize(this IReaderIqSubject<double> src, int sampleRate, IDebugPlot? plot = null)
    {
        var pulseSize = sampleRate / AdsbHalfBitRate;
        return src.PulseAvgNormalize(AdsbPrefixPostfixCount*pulseSize,(MinAdsbPulseCount + Preamble.Length)*pulseSize,0,1,plot);
    }
    
    public static IReaderIqSubject<byte> AdsbPulseTruncate(this IReaderIqSubject<double> src, int sampleRate, IDebugPlot? plot = null)
    {
        return new ReaderIqAdsbPulseTruncate(src, 0.5,sampleRate/AdsbHalfBitRate,plot);
    }

}

public class ReaderIqAdsbPulseTruncate : ReaderIqSubject<double,byte>
{
    private readonly double _avgThreshold;
    private readonly int _pulseSize;
    private readonly IDebugPlot _plot;
    private readonly int _halfPulseSize;
    public ReaderIqAdsbPulseTruncate(IReaderIqSubject<double> src, double avgThreshold, int pulseSize, IDebugPlot? plot,
        bool useArrayPool = true)
        :base(src,src.OutputBufferSize / pulseSize,useArrayPool)
    {
        _avgThreshold = avgThreshold;
        _pulseSize = pulseSize;
        _plot = plot ?? NullDebugPlot.Instance;
        _halfPulseSize = pulseSize / 2;
        
    }

    protected override void Process(ReadOnlySpan<double> input, Span<byte> output)
    {
        var firstIndex = 0;
        for (var i = 0; i < input.Length; i++)
        {
            if (!(input[i] > _avgThreshold)) continue;
            firstIndex = i;
            break;
        }
        var index = 0;
        for (var i = firstIndex; i < input.Length; i+=_pulseSize)
        {
            var cnt = 0;
            if (i + _pulseSize > input.Length) break;
            for (int j = 0; j < _pulseSize; j++)
            {
                if (!(input[i + j] >= _avgThreshold)) continue;
                cnt++;
                if (cnt >= _halfPulseSize)
                {
                    break;
                }
            }
            output[index++] = (byte)(cnt >= _halfPulseSize ? 1 : 0);
        }
        for (var i = index; i < output.Length; i++)
        {
            output[i] = 0;
        }

        if (_plot.IsEnabled)
        {
            var outputForPlot = new double[input.Length];
            for (int i = 0; i < output.Length; i++)
            {
                for (int j = 0; j < _pulseSize; j++)
                {
                    if (firstIndex + i * _pulseSize + j >= outputForPlot.Length) break;
                    outputForPlot[firstIndex + i * _pulseSize + j] = output[i];
                }
            }
            _plot.Begin();
            _plot.AddVerticalLine("Start", firstIndex);
            _plot.AddSignal("Input", input.ToArray());
            _plot.AddSignal("Output", outputForPlot);
            _plot.End();
        }
    }
    
}