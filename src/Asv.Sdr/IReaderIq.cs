using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr
{
    /// <summary>
    /// Base class of IQ source device
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReaderIq<T>
    {
        public Task<int> Read(Memory<T> iqBuffer, CancellationToken cancel = default);
    }
}
