using System;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr;

public static class AdsbHelper
{
    private static readonly byte[] Preamble = [1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0];
    private const int AdsbHalfBitRate = 2_000_000;
    private const int AdsbPrefixPostfixCount = 10;
    private const int MaxAdsbPulseCount = 112;
    private const int MinAdsbPulseCount = 56;
    private const double AdsbCorrelationThreshold = 0.0;
    
    public static IReaderIqSubject<double> AdsbPulseDetector<T>(this IReaderIqSubject<double> src, int sampleRate)
    {
        return src.PulseDetector(sampleRate/AdsbHalfBitRate,Preamble,AdsbCorrelationThreshold,MaxAdsbPulseCount,AdsbPrefixPostfixCount);
    }
    
    public static IReaderIqSubject<double> AdsbNormalize(this IReaderIqSubject<double> src, int sampleRate)
    {
        return src.PulseAvgNormalize(0,MinAdsbPulseCount*sampleRate/AdsbHalfBitRate,0,1);
    }
    
    public static IObservable<byte> AdsbPulseTruncate(this IReaderIqSubject<double> src, int sampleRate, bool useArrayPool = false)
    {
        return new ReaderIqAdsbPulseTruncate(src, 0.5,sampleRate/AdsbHalfBitRate);
    }

}

public class ReaderIqAdsbPulseTruncate : DisposableOnceWithCancel, IObservable<byte>
{
    private readonly double _avgThreshold;
    private readonly int _pulseSize;
    private readonly int _halfPulseSize;
    private readonly Subject<byte> _output;
    public ReaderIqAdsbPulseTruncate(IReaderIqSubject<double> src, double avgThreshold, int pulseSize)
    {
        _avgThreshold = avgThreshold;
        _pulseSize = pulseSize;
        _halfPulseSize = pulseSize / 2;
        _output = new Subject<byte>().DisposeItWith(Disposable);
        src.Subscribe(OnNext).DisposeItWith(Disposable);
    }

    private void OnNext(Memory<double> input)
    {
        var span = input.Span;
        var firstIndex = 0;
        for (var i = 0; i < input.Length; i++)
        {
            if (!(span[i] > _avgThreshold)) continue;
            firstIndex = i;
            break;
        }
        for (var i = firstIndex; i < input.Length; i+=_pulseSize)
        {
            var cnt = 0;
            if (i + _pulseSize > input.Length) break;
            for (int j = 0; j < _pulseSize; j++)
            {
                if (!(span[i + j] >= _avgThreshold)) continue;
                cnt++;
                if (cnt >= _halfPulseSize)
                {
                    break;
                }
            }

            _output.OnNext(cnt >= _halfPulseSize ? (byte)1 : (byte)0);
        }
    }

    public IDisposable Subscribe(IObserver<byte> observer)
    {
        return _output.Subscribe(observer);
    }
}