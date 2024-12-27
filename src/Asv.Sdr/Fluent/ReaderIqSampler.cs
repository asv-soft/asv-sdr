using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqSampler<TOut> : ReaderIqSubject<TOut>
    {
        private readonly IReaderIq<TOut> _source;

        public ReaderIqSampler(
            IReaderIq<TOut> source,
            int readSize,
            out Action start,
            bool useArrayPool = true,
            ThreadPriority priority = ThreadPriority.Highest
        )
            : base(readSize, useArrayPool)
        {
            _source = source;
            var thread = new Thread(SampleTick) { Priority = priority, IsBackground = true };
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
                    var count = await _source.Read(Memory, cancel: DisposeCancel);
                    Publish();
                }
                catch (TaskCanceledException e)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.Assert(false);
                    break;
                }
            }
        }
    }
}
