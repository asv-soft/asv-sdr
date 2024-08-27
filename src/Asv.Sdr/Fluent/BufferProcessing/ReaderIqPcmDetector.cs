using System;
using System.Buffers;
using System.Reactive.Subjects;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqPcmDetector: DisposableOnceWithCancel, IReaderIqSubject<double>
    {
        private readonly PulseCrossCorrelation _correlation;
        private readonly CircularBuffer2<double> _correlationBuffer;
        private readonly CircularBuffer2<double> _rawBuffer;
        private readonly int _prefixPulseSize;
        private State _state;
        private readonly Subject<Memory<double>> _output;
        private readonly double[] _outputBuffer;
        private readonly Memory<double> _outputMemory;
        private readonly double _correlationThreshold;

        public ReaderIqPcmDetector(IReaderIqSubject<double> input, int pulseSize,
            byte[] template, double correlationThreshold, int maxPulseCount, int prefixPulseCount, bool useArrayPool)
        {
            _correlation = new PulseCrossCorrelation(pulseSize, template);
            _correlationThreshold = correlationThreshold;
            var correlationBufferLength = template.Length * pulseSize;
            var rawBufferLength = (template.Length + maxPulseCount + prefixPulseCount * 2) * pulseSize;
            
            if (useArrayPool)
            {
                var buff = ArrayPool<double>.Shared.Rent(correlationBufferLength);
                _correlationBuffer = new CircularBuffer2<double>(buff, correlationBufferLength);
                var rawBuff = ArrayPool<double>.Shared.Rent(rawBufferLength);
                _rawBuffer = new CircularBuffer2<double>(rawBuff, rawBufferLength);
                _outputBuffer = ArrayPool<double>.Shared.Rent(_correlationBuffer.Capacity);
                Disposable.AddAction(()=>
                {
                    ArrayPool<double>.Shared.Return(buff);
                    ArrayPool<double>.Shared.Return(rawBuff);
                    ArrayPool<double>.Shared.Return(_outputBuffer);
                });
            }
            else
            {
                _correlationBuffer = new CircularBuffer2<double>(correlationBufferLength);
                _rawBuffer = new CircularBuffer2<double>(rawBufferLength);
                _outputBuffer = new double[_correlationBuffer.Capacity];
            }
            _outputMemory = new Memory<double>(_outputBuffer, 0, _outputBuffer.Length);
            OutputBufferSize = _rawBuffer.Capacity;
            _prefixPulseSize = prefixPulseCount * pulseSize;
            input.Subscribe(OnNext).DisposeItWith(Disposable);
            _output = new Subject<Memory<double>>().DisposeItWith(Disposable);
        }

        private void OnNext(Memory<double> input)
        {
            var span = input.Span;
            for (var i = 0; i < input.Length/2; i++)
            {
                OnNext(span[i*2]);
            }
        }

        private void OnNext(double input)
        {
            switch (_state)
                    {
                        case State.Rise:
                            var riseCorr = _correlation.Process(input);
                            _rawBuffer.PushBack(input);
                            if (riseCorr > _correlationThreshold)
                            {
                                _state = State.Fall;
                                _correlationBuffer.Clear();
                                _correlationBuffer.PushBack(riseCorr);
                                while(_rawBuffer.Size > _prefixPulseSize)
                                {
                                    _rawBuffer.PopFront();
                                }
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
                                for (var i = 0; i < _correlationBuffer.Size; i++)
                                {
                                    if (_correlationBuffer[i] > maxValue)
                                    {
                                        maxValue = _correlationBuffer[i];
                                        maxIndex = i;
                                    }
                                }
                                for (var i = 0; i < maxIndex; i++)
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
                                _rawBuffer.CopyTo(new Span<double>(_outputBuffer,0,_correlationBuffer.Capacity));
                                _output.OnNext(_outputMemory);
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
        }


        private enum State
        {
            Rise,
            Fall,
            Data,
        }

        public IDisposable Subscribe(IObserver<Memory<double>> observer) => _output.Subscribe(observer);

        public int OutputBufferSize { get; }
    }
}