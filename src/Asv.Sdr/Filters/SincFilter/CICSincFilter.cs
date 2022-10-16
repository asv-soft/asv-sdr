namespace Asv.Sdr
{
    /// <summary>
    /// FIR filter designed with
    ///  http://t-filter.appspot.com
    /// 
    /// sampling frequency: 48000 Hz
    /// 
    /// * 0 Hz - 5000 Hz
    ///   gain = 0
    ///   desired attenuation = -40 dB
    ///   actual attenuation = -40.089989131512205 dB
    /// 
    /// * 9000 Hz - 11000 Hz
    ///   gain = 1
    ///   desired ripple = 5 dB
    ///   actual ripple = 2.3089711052878354 dB
    /// 
    /// * 15000 Hz - 24000 Hz
    ///   gain = 0
    ///   desired attenuation = -40 dB
    ///   actual attenuation = -40.089989131512205 dB
    /// 
    /// </summary>
    public class Vor9960SincFilter : CicSincFilter
    {
        public Vor9960SincFilter() : base(new[]
        {
            -0.008477192482657929,
            -0.031303288439231886,
            0.004128078504796253,
            0.07982415720817815,
            0.052088192050652925,
            -0.10105255314509162,
            -0.13976173688287996,
            0.04758321886488338,
            0.18404531762017742,
            0.04758321886488338,
            -0.13976173688287996,
            -0.10105255314509162,
            0.052088192050652925,
            0.07982415720817815,
            0.004128078504796253,
            -0.031303288439231886,
            -0.008477192482657929
        })
        {

        }
    }

    /// <summary>
    /// FIR filter designed with
    ///  http://t-filter.appspot.com
    /// 
    /// sampling frequency: 48000 Hz
    /// 
    /// * 0 Hz - 100 Hz
    ///   gain = 1
    ///   desired ripple = 5 dB
    ///   actual ripple = 27.790982847628403 dB
    /// 
    /// * 300 Hz - 24000 Hz
    ///   gain = 0
    ///   desired attenuation = -40 dB
    ///   actual attenuation = -27.81478569669814 dB
    /// </summary>
    public class Vor100SincFilter : CicSincFilter
    {
        public Vor100SincFilter() : base(new[]
        {
            0.020357476196127,
            0.00003138975273936362,
            0.000016566914336187123,
            0.00003138975273936362,
            0.020357476196127
        })
        {

        }
    }

    public class CicSincFilter:IDspFilter
    {
        private readonly double[] _filterTaps;
        private readonly double[] history;
        private readonly int _sampleFilterTapNum;
        private int _lastIndex = 0;

        public CicSincFilter(double[] filterTaps)
        {
            _filterTaps = filterTaps;
            _sampleFilterTapNum = filterTaps.Length;
            history = new double[_sampleFilterTapNum];
        }

        public double Process(double input)
        {
            history[_lastIndex] = input;
            if (_lastIndex == _sampleFilterTapNum)
                _lastIndex = 0;
            double acc = 0;
            int index = _lastIndex, i;
            for (i = 0; i < _sampleFilterTapNum; ++i)
            {
                index = index != 0 ? index - 1 : _sampleFilterTapNum - 1;
                acc += history[index] * _filterTaps[i];
            };
            return acc;
        }
    }


}