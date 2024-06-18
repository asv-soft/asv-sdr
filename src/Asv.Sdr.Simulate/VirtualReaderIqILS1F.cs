using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.Simulate
{
    public enum DdmSdmType
    {
        AM90_150,
        AM150_90
    }


    public class VirtualReaderIqIls1F:IReaderIq<double>
    {
        private readonly double _kam90;
        private readonly double _kam150;
        private double _dt;
        private readonly double _dtInc;
        private double _crsMagic = 0.5;

        public VirtualReaderIqIls1F(int sampleRate, double ddm, double sdm, DdmSdmType type)
        {
            if (ddm > 1) throw new ArgumentOutOfRangeException(nameof(ddm), "Value must be -1..1");
            if (sdm > 1) throw new ArgumentOutOfRangeException(nameof(ddm), "Value must be -1..1");
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            switch (type)
            {
                case DdmSdmType.AM90_150:
                    _kam90 = (sdm - ddm) / 2.0;
                    _kam150 = (sdm + ddm) / 2.0;
                    break;
                case DdmSdmType.AM150_90:
                    _kam150 = (sdm - ddm) / 2.0;
                                                                   _kam90 = (sdm + ddm) / 2.0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            _dt = 0;
            _dtInc = 2 * Math.PI / sampleRate;
        }

        public Task<int> Read(Memory<double> iqBuffer, CancellationToken cancel = default)
        {
            return Task.Run(() =>
            {
                var span = iqBuffer.Span;
                for (var i = 0; i < iqBuffer.Length / 2; i++)
                {
                    var crsIm = ((0.5 + _crsMagic * _kam90 * Math.Cos(90.0 * _dt) + _crsMagic * _kam150 * Math.Cos(150.0 * _dt) ) / Math.Sqrt(2));
                    var crsRe = ((0.5 + _crsMagic * _kam90 * Math.Cos(90.0 * _dt) + _crsMagic * _kam150 * Math.Cos(150.0 * _dt) ) / Math.Sqrt(2));
                    _dt += _dtInc;
                    span[i * 2] = crsIm;
                    span[i * 2 + 1] = crsRe;
                }
                return iqBuffer.Length;
            }, cancel);
        }
    }
}
