using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace Asv.Sdr
{
    public enum ReaderIqFftImplementation
    {
        Alglib,
        Managed,
        ManagedArmOptimized
    }

    public static class ReaderIqFftSettings
    {
        public static ReaderIqFftImplementation Implementation { get; set; } = ReaderIqFftImplementation.Alglib;

        internal static IReaderIqFftPlan CreatePlan(int complexSampleCount)
        {
            return Implementation switch
            {
                ReaderIqFftImplementation.Alglib => new AlglibReaderIqFftPlan(complexSampleCount),
                ReaderIqFftImplementation.Managed => new ManagedReaderIqFftPlan(complexSampleCount),
                ReaderIqFftImplementation.ManagedArmOptimized => new ArmOptimizedReaderIqFftPlan(complexSampleCount),
                _ => throw new ArgumentOutOfRangeException(nameof(Implementation), Implementation, null)
            };
        }
    }

    internal interface IReaderIqFftPlan
    {
        void Transform(double[] buffer);
    }

    public sealed class AlglibReaderIqFftPlan : IReaderIqFftPlan
    {
        private readonly alglib.xparams _params;
        private readonly alglib.ftbase.fasttransformplan _plan;

        public AlglibReaderIqFftPlan(int complexSampleCount)
        {
            if (complexSampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(complexSampleCount));
            _params = alglib.parallel;
            _plan = new alglib.ftbase.fasttransformplan();
            alglib.ftbase.ftcomplexfftplan(complexSampleCount, 1, _plan, _params);
        }

        public void Transform(double[] buffer)
        {
            alglib.ftbase.ftapplyplan(_plan, buffer, 0, 1, _params);
        }
    }

    public class ManagedReaderIqFftPlan : IReaderIqFftPlan
    {
        protected readonly int ComplexSampleCount;
        protected readonly bool IsPowerOfTwo;
        protected readonly int[]? BitReversal;
        protected readonly double[] Cos;
        protected readonly double[] Sin;

        public ManagedReaderIqFftPlan(int complexSampleCount)
        {
            if (complexSampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(complexSampleCount));
            ComplexSampleCount = complexSampleCount;
            IsPowerOfTwo = (complexSampleCount & (complexSampleCount - 1)) == 0;
            Cos = new double[complexSampleCount / 2];
            Sin = new double[complexSampleCount / 2];

            for (var i = 0; i < Cos.Length; i++)
            {
                var angle = -2.0 * Math.PI * i / complexSampleCount;
                Cos[i] = Math.Cos(angle);
                Sin[i] = Math.Sin(angle);
            }

            if (IsPowerOfTwo)
            {
                BitReversal = CreateBitReversalTable(complexSampleCount);
            }
        }

        public void Transform(double[] buffer)
        {
            if (buffer.Length < ComplexSampleCount * 2)
            {
                throw new ArgumentException("FFT buffer is shorter than the configured transform size.", nameof(buffer));
            }

            if (IsPowerOfTwo)
            {
                TransformRadix2(buffer);
            }
            else
            {
                TransformDft(buffer);
            }
        }

        protected virtual void TransformRadix2(double[] buffer)
        {
            var bitReversal = BitReversal!;
            for (var i = 0; i < ComplexSampleCount; i++)
            {
                var j = bitReversal[i];
                if (j <= i) continue;
                SwapComplex(buffer, i, j);
            }

            for (var size = 2; size <= ComplexSampleCount; size <<= 1)
            {
                var halfSize = size >> 1;
                var tableStep = ComplexSampleCount / size;

                for (var start = 0; start < ComplexSampleCount; start += size)
                {
                    for (var j = 0; j < halfSize; j++)
                    {
                        ApplyButterfly(buffer, start, j, halfSize, tableStep, Cos, Sin);
                    }
                }
            }
        }

        protected void TransformDft(double[] buffer)
        {
            var result = new double[ComplexSampleCount * 2];
            for (var k = 0; k < ComplexSampleCount; k++)
            {
                var sumReal = 0.0;
                var sumImag = 0.0;
                for (var n = 0; n < ComplexSampleCount; n++)
                {
                    var angle = -2.0 * Math.PI * k * n / ComplexSampleCount;
                    var cos = Math.Cos(angle);
                    var sin = Math.Sin(angle);
                    var sourceIndex = n * 2;
                    var real = buffer[sourceIndex];
                    var imag = buffer[sourceIndex + 1];

                    sumReal += real * cos - imag * sin;
                    sumImag += real * sin + imag * cos;
                }

                var resultIndex = k * 2;
                result[resultIndex] = sumReal;
                result[resultIndex + 1] = sumImag;
            }

            Array.Copy(result, buffer, result.Length);
        }

        protected static void ApplyButterfly(
            double[] buffer,
            int start,
            int j,
            int halfSize,
            int tableStep,
            double[] cos,
            double[] sin)
        {
            var twiddleIndex = j * tableStep;
            var wr = cos[twiddleIndex];
            var wi = sin[twiddleIndex];
            var evenIndex = (start + j) * 2;
            var oddIndex = evenIndex + halfSize * 2;

            var oddReal = buffer[oddIndex];
            var oddImag = buffer[oddIndex + 1];
            var tr = wr * oddReal - wi * oddImag;
            var ti = wr * oddImag + wi * oddReal;
            var evenReal = buffer[evenIndex];
            var evenImag = buffer[evenIndex + 1];

            buffer[oddIndex] = evenReal - tr;
            buffer[oddIndex + 1] = evenImag - ti;
            buffer[evenIndex] = evenReal + tr;
            buffer[evenIndex + 1] = evenImag + ti;
        }

        private static int[] CreateBitReversalTable(int count)
        {
            var result = new int[count];
            var bits = 0;
            for (var value = count; value > 1; value >>= 1)
            {
                bits++;
            }

            for (var i = 0; i < count; i++)
            {
                var reversed = 0;
                var value = i;
                for (var bit = 0; bit < bits; bit++)
                {
                    reversed = (reversed << 1) | (value & 1);
                    value >>= 1;
                }

                result[i] = reversed;
            }

            return result;
        }

        protected static void SwapComplex(double[] buffer, int first, int second)
        {
            var firstIndex = first * 2;
            var secondIndex = second * 2;
            (buffer[firstIndex], buffer[secondIndex]) = (buffer[secondIndex], buffer[firstIndex]);
            (buffer[firstIndex + 1], buffer[secondIndex + 1]) = (buffer[secondIndex + 1], buffer[firstIndex + 1]);
        }
    }

    public sealed class ArmOptimizedReaderIqFftPlan : ManagedReaderIqFftPlan
    {
        public ArmOptimizedReaderIqFftPlan(int complexSampleCount)
            : base(complexSampleCount)
        {
        }

        protected override void TransformRadix2(double[] buffer)
        {
            if (AdvSimd.Arm64.IsSupported == false)
            {
                base.TransformRadix2(buffer);
                return;
            }

            var bitReversal = BitReversal!;
            for (var i = 0; i < ComplexSampleCount; i++)
            {
                var j = bitReversal[i];
                if (j <= i) continue;
                SwapComplex(buffer, i, j);
            }

            for (var size = 2; size <= ComplexSampleCount; size <<= 1)
            {
                var halfSize = size >> 1;
                var tableStep = ComplexSampleCount / size;

                for (var start = 0; start < ComplexSampleCount; start += size)
                {
                    var j = 0;
                    for (; j + 1 < halfSize; j += 2)
                    {
                        ApplyArm64ButterflyPair(buffer, start, j, halfSize, tableStep);
                    }

                    if (j < halfSize)
                    {
                        ApplyButterfly(buffer, start, j, halfSize, tableStep, Cos, Sin);
                    }
                }
            }
        }

        private void ApplyArm64ButterflyPair(double[] buffer, int start, int j, int halfSize, int tableStep)
        {
            var evenIndex = (start + j) * 2;
            var oddIndex = evenIndex + halfSize * 2;
            var twiddleIndex = j * tableStep;
            var nextTwiddleIndex = twiddleIndex + tableStep;

            var wr = Vector128.Create(Cos[twiddleIndex], Cos[nextTwiddleIndex]);
            var wi = Vector128.Create(Sin[twiddleIndex], Sin[nextTwiddleIndex]);
            var evenReal = Vector128.Create(buffer[evenIndex], buffer[evenIndex + 2]);
            var evenImag = Vector128.Create(buffer[evenIndex + 1], buffer[evenIndex + 3]);
            var oddReal = Vector128.Create(buffer[oddIndex], buffer[oddIndex + 2]);
            var oddImag = Vector128.Create(buffer[oddIndex + 1], buffer[oddIndex + 3]);

            var tr = AdvSimd.Arm64.Subtract(
                AdvSimd.Arm64.Multiply(wr, oddReal),
                AdvSimd.Arm64.Multiply(wi, oddImag));
            var ti = AdvSimd.Arm64.Add(
                AdvSimd.Arm64.Multiply(wr, oddImag),
                AdvSimd.Arm64.Multiply(wi, oddReal));

            var outEvenReal = AdvSimd.Arm64.Add(evenReal, tr);
            var outEvenImag = AdvSimd.Arm64.Add(evenImag, ti);
            var outOddReal = AdvSimd.Arm64.Subtract(evenReal, tr);
            var outOddImag = AdvSimd.Arm64.Subtract(evenImag, ti);

            buffer[evenIndex] = outEvenReal.GetElement(0);
            buffer[evenIndex + 1] = outEvenImag.GetElement(0);
            buffer[evenIndex + 2] = outEvenReal.GetElement(1);
            buffer[evenIndex + 3] = outEvenImag.GetElement(1);
            buffer[oddIndex] = outOddReal.GetElement(0);
            buffer[oddIndex + 1] = outOddImag.GetElement(0);
            buffer[oddIndex + 2] = outOddReal.GetElement(1);
            buffer[oddIndex + 3] = outOddImag.GetElement(1);
        }
    }
}
