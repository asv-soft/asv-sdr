using System;

namespace Asv.Sdr;

public class ReaderIqAmplitudeI(IReaderIqSubject<double> input) : ReaderIqSimpleSubject<double, double>(input)
{
    private readonly int _size = input.OutputBufferSize / 2;
    protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
    {
        selfPublish = false;
        double sum = 0;
        for (var i = 0; i < input.Length; i += 2)
        {
            sum += input[i];
        }
        return sum / _size;
    }
}