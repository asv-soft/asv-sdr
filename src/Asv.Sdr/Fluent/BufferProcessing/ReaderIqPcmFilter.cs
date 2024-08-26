using System;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr
{
    /*public class ReaderIqPcmFilter: DisposableOnceWithCancel, IReaderIqSubject<double>
    {
        private readonly PulseCrossCorrelation _correlation;
        private readonly CircularBuffer2<double> _correlationBuffer;
        private readonly CircularBuffer2<double> _rawBuffer;
        private readonly int _prefixPulseSize;
        private readonly int _pulseSize;
        private readonly int _avgSize;
        private State _state;
        private readonly Subject<Memory<double>> _output;

        public ReaderIqPcmFilter(IReaderIqSubject<double> input, int pulseSize,
            byte[] template, int minMessageSize, int maxMessageSize,  int prefixSize, bool useArrayPool)
        {
            _correlation = new PulseCrossCorrelation(pulseSize, template);
            _pulseSize = pulseSize;
            _correlationBuffer = new CircularBuffer2<double>(template.Length * _pulseSize);
            _rawBuffer = new CircularBuffer2<double>( (template.Length + maxMessageSize + prefixSize*2) * _pulseSize);
            OutputBufferSize = _rawBuffer.Capacity;
            _avgSize = (template.Length + minMessageSize) * _pulseSize;
            _prefixPulseSize = prefixSize * _pulseSize;
            input.Subscribe(OnNext).DisposeItWith(Disposable);
            _output = new Subject<Memory<double>>().DisposeItWith(Disposable);
        }

        private void OnNext(Memory<double> input)
        {
            var span = input.Span;
            for (int i = 0; i < input.Length/2; i++)
            {
                OnNext(span[i*2]);
            }
        }

        private void OnNext(double input)
        {
            switch (_state)
            {
                
            }
        }


        enum State
        {
            Rise,
            Fall,
            Data,
        }

        public IDisposable Subscribe(IObserver<Memory<double>> observer)
        {
            throw new NotImplementedException();
        }

        public int OutputBufferSize { get; }
    }*/
}