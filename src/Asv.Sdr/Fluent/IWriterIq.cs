using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr
{
    public interface IWriterIq<T>
    {
        public Task<int> Write(ReadOnlyMemory<T> iqBuffer, CancellationToken cancel = default);
    }
}
