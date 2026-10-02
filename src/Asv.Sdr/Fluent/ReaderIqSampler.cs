using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqSampler<TOut>: ReaderIqSubject<TOut>
    {
        private readonly IReaderIq<TOut> _source;

        public ReaderIqSampler(IReaderIq<TOut> source, int readSize,out Action start, bool useArrayPool = true,
            ThreadPriority priority = ThreadPriority.Highest):base(readSize, useArrayPool)
        {
            _source = source;
            var thread = new Thread(SampleTick)
            {
                Priority = priority,
                IsBackground = true,
            };
            Disposable.AddAction(() =>
            {
                thread.Interrupt();
            });
            start = ()=> thread.Start();
        }

        private async void SampleTick()
        {
            while (IsDisposed == false)
            {
                try
                {
                    var count = await _source.Read(Memory, cancel: DisposeCancel);
                    Publish();
                }
                catch (TaskCanceledException e)
                {
                    break;
                }
                catch (Exception ex)
                {
                    //Debug.Assert(false);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Reads single-precision samples and publishes double-precision samples.
    /// </summary>
    public sealed class ReaderIqSamplerFloatToDouble : ReaderIqSubject<double>
    {
        private readonly IReaderIq<float> _source;
        private readonly float[] _inputBuffer;
        private readonly Memory<float> _inputMemory;

        /// <summary>
        /// Initializes a sampler that converts each source sample from <see cref="float"/> to <see cref="double"/>.
        /// </summary>
        /// <param name="source">The source reader.</param>
        /// <param name="readSize">The number of source samples read per block.</param>
        /// <param name="start">Starts the sampler thread.</param>
        /// <param name="useArrayPool">True to rent the output buffer from the shared array pool.</param>
        /// <param name="priority">The sampler thread priority.</param>
        public ReaderIqSamplerFloatToDouble(
            IReaderIq<float> source,
            int readSize,
            out Action start,
            bool useArrayPool = true,
            ThreadPriority priority = ThreadPriority.Highest
        )
            : base(readSize, useArrayPool)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _inputBuffer = ArrayPool<float>.Shared.Rent(readSize);
            _inputMemory = new Memory<float>(_inputBuffer, 0, readSize);
            Disposable.AddAction(() => ArrayPool<float>.Shared.Return(_inputBuffer));

            var thread = new Thread(SampleTick)
            {
                Priority = priority,
                IsBackground = true,
            };
            Disposable.AddAction(() =>
            {
                thread.Interrupt();
            });
            start = () => thread.Start();
        }

        private async void SampleTick()
        {
            while (IsDisposed == false)
            {
                try
                {
                    await _source.Read(_inputMemory, cancel: DisposeCancel);
                    ConvertSamples();
                    Publish();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private void ConvertSamples()
        {
            var source = _inputMemory.Span;
            var destination = Memory.Span;
            var sampleCount = Math.Min(source.Length, destination.Length);

            for (var index = 0; index < sampleCount; index++)
            {
                destination[index] = source[index];
            }
        }
    }
}
