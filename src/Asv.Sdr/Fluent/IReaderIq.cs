using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Sdr.V2;

namespace Asv.Sdr
{
    /// <summary>
    /// Base class of IQ source device
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReaderIq<T>
    {
        Task<int> Read(Memory<T> iqBuffer, CancellationToken cancel = default);
    }

    public interface IReaderIqSubject<TOut> : IObservable<Memory<TOut>>, IDisposable
    {
        int OutputBufferSize { get; }
    }

    public static class FluentDspHelper
    {
        public static IReaderIqSubject<T> Sample<T>(this IReaderIq<T> src, int readSize,out Action start, bool useArrayPool = false,
            ThreadPriority priority = ThreadPriority.Highest)
        {
            return new ReaderIqSampler<T>(src, readSize,out start, useArrayPool, priority);
        }

        public static IReaderIqSubject<T> Parallel<T>(this IReaderIqSubject<T> src)
        {
            return new ReaderIqParallelSubject<T>(src);
        }

        public static IReaderIqSubject<TOut> IqZip<TIn1, TIn2, TOut>(this IReaderIqSubject<TIn1> src, IReaderIqSubject<TIn2> second, ProcessDelegate<TIn1, TIn2, TOut> processCallback, int outputSize, bool useArrayPool = false)
        {
            return new ReaderIqZipSubjectCallback<TIn1, TIn2, TOut>(src, second, outputSize, processCallback,
                useArrayPool);
        }

        #region Delta phase

        public static IReaderIqSubject<double> DiffPhase(this IReaderIqSubject<float> src, bool useArrayPool = true)
        {
            return new ReaderIqDiffPhaseFloat(src, useArrayPool);
        }

        public static IReaderIqSubject<double> DiffPhase(this IReaderIqSubject<double> src, bool useArrayPool = true)
        {
            return new ReaderIqDiffPhaseDouble(src, useArrayPool);
        }

        #endregion

        #region I and Q manipulation

        

        public static IReaderIqSubject<double> CopyIToQ(this IReaderIqSubject<double> src, bool useArrayPool = true)
        {
            return new ReaderIqCopyIToQDouble(src, useArrayPool);
        }

        public static IReaderIqSubject<double> CopyIToQ(this IReaderIqSubject<float> src, bool useArrayPool = true)
        {
            return new ReaderIqCopyIToQFloat(src, useArrayPool);
        }

        public static IReaderIqSubject<double> CopyQToI(this IReaderIqSubject<double> src, bool useArrayPool = true)
        {
            return new ReaderIqCopyQToIDouble(src, useArrayPool);
        }

        public static IReaderIqSubject<double> CopyQToI(this IReaderIqSubject<float> src, bool useArrayPool = true)
        {
            return new ReaderIqCopyQToIFloat(src, useArrayPool);
        }

        public static IReaderIqSubject<double> MoveIToQ(this IReaderIqSubject<double> src, bool useArrayPool = true)
        {
            return new ReaderIqMoveIToQDouble(src, useArrayPool);
        }

        public static IReaderIqSubject<double> MoveIToQ(this IReaderIqSubject<float> src, bool useArrayPool = true)
        {
            return new ReaderIqMoveIToQFloat(src, useArrayPool);
        }

        public static IReaderIqSubject<double> MoveQToI(this IReaderIqSubject<double> src, bool useArrayPool = true)
        {
            return new ReaderIqMoveQToIDouble(src, useArrayPool);
        }

        public static IReaderIqSubject<double> MoveQToI(this IReaderIqSubject<float> src, bool useArrayPool = true)
        {
            return new ReaderIqMoveQToIFloat(src, useArrayPool);
        }

        public static IReaderIqSubject<double> Select(this IReaderIqSubject<float> src,
            ProcessValueDelegate<float, double> iCallback, ProcessValueDelegate<float, double> qCallback,
            bool useArrayPool = true)
        {
            return new ReaderIqSelectIAndQSubject<float, double>(src, iCallback, qCallback, useArrayPool);
        }

        public static IReaderIqSubject<double> Select(this IReaderIqSubject<double> src,
            ProcessValueDelegate<double, double> iCallback, ProcessValueDelegate<double, double> qCallback,
            bool useArrayPool = true)
        {
            return new ReaderIqSelectIAndQSubject<double, double>(src, iCallback, qCallback, useArrayPool);
        }

        public static IReaderIqSubject<double> Select(this IReaderIqSubject<float> src,
            ProcessIWithQValueDelegate<float, double> callback, bool useArrayPool = true)
        {
            return new ReaderIqSelectIWithQSubject<float, double>(src, callback, useArrayPool);
        }

        public static IReaderIqSubject<double> Select(this IReaderIqSubject<double> src,
            ProcessIWithQValueDelegate<double, double> callback, bool useArrayPool = true)
        {
            return new ReaderIqSelectIWithQSubject<double, double>(src, callback, useArrayPool);
        }

        public static IReaderIqSubject<TOut> Select<TIn, TOut>(this IReaderIqSubject<TIn> src,
            ProcessDelegate<TIn, TOut> callback, int outputSize, bool useArrayPool = false)
        {
            return new ReaderIqCallbackSubject<TIn, TOut>(src, outputSize, callback, useArrayPool);
        }

        public static IReaderIqSubject<TIn> Select<TIn>(this IReaderIqSubject<TIn> src,
            ProcessDelegate<TIn, TIn> callback, bool useArrayPool = false)
        {
            return new ReaderIqCallbackSubject<TIn, TIn>(src, src.OutputBufferSize, callback, useArrayPool);
        }

        #endregion

        #region Fft

        public static IReaderIqSubject<double> Fft1d(this IReaderIqSubject<float> src)
        {
            return new ReaderIqFft1dFloat(src);
        }

        public static IReaderIqSubject<double> Fft1d(this IReaderIqSubject<double> src)
        {
            return new ReaderIqFft1dDouble(src);
        }

        #endregion

        #region Overlap

        public static IReaderIqSubject<double> HalfOverlap(this IReaderIqSubject<double> src, bool useArrayPool = false)
        {
            return new ReaderIqHalfOverlapDouble(src, useArrayPool);
        }

        public static IReaderIqSubject<double> HalfOverlap(this IReaderIqSubject<float> src, bool useArrayPool = false)
        {
            return new ReaderIqHalfOverlapFloat(src, useArrayPool);
        }

        #endregion

        #region Magnitude

        public static IReaderIqSubject<double> Magnitude(this IReaderIqSubject<float> src, bool useArrayPool = false)
        {
            return new ReaderIqMagnitudeFloat(src, useArrayPool);
        }

        public static IReaderIqSubject<double> Magnitude(this IReaderIqSubject<double> src, bool useArrayPool = false)
        {
            return new ReaderIqMagnitudeDouble(src, useArrayPool);
        }

        #endregion

        #region Magnitude and phase

        public static IReaderIqSubject<double> MagnitudeAndPhase(this IReaderIqSubject<double> src, bool useArrayPool = false)
        {
            return new ReaderIqMagnitudeAndPhaseDouble(src, useArrayPool);
        }

        public static IReaderIqSubject<double> MagnitudeAndPhase(this IReaderIqSubject<float> src, bool useArrayPool = false)
        {
            return new ReaderIqMagnitudeAndPhaseFloat(src, useArrayPool);
        }

        #endregion

        #region I and Q filter

        public static IReaderIqSubject<double> AddIqFilter(this IReaderIqSubject<double> src, IDspFilter iFilter, IDspFilter qFilter, bool useArrayPool = true)
        {
            return new ReaderIqFilterIqDouble(src, iFilter, qFilter, useArrayPool);
        }
        public static IReaderIqSubject<double> AddIqFilter(this IReaderIqSubject<float> src, IDspFilter iFilter, IDspFilter qFilter, bool useArrayPool = true)
        {
            return new ReaderIqFilterIqFloat(src, iFilter, qFilter, useArrayPool);
        }

        public static IReaderIqSubject<double> AddIFilter(this IReaderIqSubject<double> src, IDspFilter iFilter, bool useArrayPool = true)
        {
            return new ReaderIqFilterIDouble(src, iFilter, useArrayPool);
        }

        public static IReaderIqSubject<double> AddIFilter(this IReaderIqSubject<float> src, IDspFilter iFilter, bool useArrayPool = true)
        {
            return new ReaderIqFilterIFloat(src, iFilter, useArrayPool);
        }

        #endregion

        #region FM modulation

        public static IObservable<double> GetPhase(this IReaderIqSubject<double> src, int sampleRate, int freqHz)
        {
            return new ReaderIqPhaseDoubleSubject(src, sampleRate, freqHz);
        }

        public static IObservable<double> GetPhase(this IReaderIqSubject<float> src, int sampleRate, int freqHz)
        {
            return new ReaderIqPhaseFloatSubject(src, sampleRate, freqHz);
        }

        #endregion

        #region AM modulation

        public static IObservable<double> GetAm(this IReaderIqSubject<double> src, int sampleRate, int amFreq)
        {
            return new ReaderIqAmSubject(src,sampleRate,amFreq);
        }

        public static IObservable<(double,double)> GetAm(this IReaderIqSubject<double> src, int sampleRate, int am1Freq, int am2Freq)
        {
            return new ReaderIqAm2Subject(src, sampleRate, am1Freq,am2Freq);
        }

        public static IObservable<TResult> ParallelJoin<TFirst, TSecond, TResult>(this IObservable<TFirst> first, IObservable<TSecond> second, Func<TFirst,TSecond,TResult> resultSelector)
        {
            return new ReactiveParallelJoin<TFirst, TSecond, TResult>(first,second, resultSelector);
        }

        public static IObservable<double> GetDdm(this IObservable<double> am1, IObservable<double> am2)
        {
            return am1.Zip(am2).Select(_ => _.First - _.Second);
        }

        public static IObservable<(double, double)> GetDdmSdm(this IObservable<(double, double)> am)
        {
            return am.Select(_ => (_.Item1 - _.Item2, (_.Item1 + _.Item2)));
        }

        public static IObservable<double> GetSdm(this IObservable<double> am1, IObservable<double> am2)
        {
            return am1.Zip(am2).Select(_ => _.First + _.Second);
        }

        #endregion

        #region Kalman filter

        public static IObservable<double> KalmanFilter(this IObservable<double> src, double measurementNoise, double deviceNoise)
        {
            return new ReactiveDspFilter(src, new KalmanDspFilter(measurementNoise, deviceNoise));
        }
        
        public static IObservable<double> KalmanRadianFilter(this IObservable<double> src, double measurementNoise, double deviceNoise)
        {
            return new ReactiveDspFilter(src, new KalmanRadianDspFilter(measurementNoise, deviceNoise));
        }

        public static IObservable<(double, double)> KalmanFilter(this IObservable<(double, double)> src, double measurementNoise1, double deviceNoise1, double measurementNoise2, double deviceNoise2)
        {
            return new ReactiveDsp2ArgsFilter(src, new KalmanDspFilter(measurementNoise1, deviceNoise1), new KalmanDspFilter(measurementNoise2, deviceNoise2));
        }

        #endregion

        #region Median filter

        public static IObservable<double> MedianFilter(this IObservable<double> src, int windowSize)
        {
            return new ReactiveDspFilter(src, new MedianDspFilter(windowSize));
        }

        public static IObservable<(double, double)> MedianFilter(this IObservable<(double, double)> src, int windowSize1, int windowSize2)
        {
            return new ReactiveDsp2ArgsFilter(src, new MedianDspFilter(windowSize1), new MedianDspFilter(windowSize2));
        }

        #endregion

        #region Average filter

        public static IObservable<double> AverageFilter(this IObservable<double> src, int windowSize)
        {
            return new ReactiveDspFilter(src, new MovingAverageDspFilter(windowSize));
        }
        public static IObservable<(double, double)> AverageFilter(this IObservable<(double, double)> src, int windowSize1, int windowSize2)
        {
            return new ReactiveDsp2ArgsFilter(src, new MovingAverageDspFilter(windowSize1), new MovingAverageDspFilter(windowSize2));
        }

        public static IObservable<double> AverageRadianFilter(this IObservable<double> src, int windowSize)
        {
            return new ReactiveDspFilter(src, new MovingAverageRadianDspFilter(windowSize));
        }

        #endregion

        #region Window filters


        public static IReaderIqSubject<double> WindowFilter(this IReaderIqSubject<float> src, WindowFilterEnum type, bool useArrayPool = false)
        {
            return new ReaderIqWindowFilterFloat(src, type, useArrayPool);
        }

        public static IReaderIqSubject<double> WindowFilter(this IReaderIqSubject<double> src, WindowFilterEnum type, bool useArrayPool = false)
        {
            return new ReaderIqWindowFilterDouble(src, type, useArrayPool);
        }

       

        #endregion

        #region Frequncy shift

        public static IReaderIqSubject<double> FrequencyShift(this IReaderIqSubject<float> src, double sampleRate,int freqHz, bool useArrayPool = false)
        {
            return new ReaderIqFreqShiftFloat(src, sampleRate,freqHz, useArrayPool);
        }

        public static IReaderIqSubject<double> FrequencyShift(this IReaderIqSubject<double> src, double sampleRate, int freqHz, bool useArrayPool = false)
        {
            return new ReaderIqFreqShiftDouble(src, sampleRate, freqHz, useArrayPool);
        }

        #endregion

        #region Code ID

        public static IObservable<string> CodeId(this IObservable<double> src, double amMin, double amMax, int fftBufferSize, int sampleRate)
        {
            return new ReaderIqCodeIdSubject(src, amMin, amMax, fftBufferSize, sampleRate);
        }

        #endregion

        public static IReaderIqSubject<TOut> SkipEvery<TOut>(this IReaderIqSubject<TOut> src, int skipEveryTime)
        {
            return new ReaderIqSkipEverySubject<TOut>(src, skipEveryTime);
        }

        public static IObservable<(TOut[] i,TOut[] q)> CopyToArray<TOut>(this IReaderIqSubject<TOut> src)
        {
            return new ReaderIqCopyToArray<TOut>(src);
        }

    }

}
