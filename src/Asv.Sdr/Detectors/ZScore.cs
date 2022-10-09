using System;
using System.Collections.Generic;
using System.Linq;

namespace Asv.Sdr
{
    public class ZScoreOutput
    {
        public List<double> input;
        public List<int> signals;
        public List<double> avgFilter;
        public List<double> filtered_stddev;
    }

    public static class ZScore
    {
        /// <summary>
        /// Robust peak detection algorithm (using z-scores)
        /// </summary>
        /// <param name="input"></param>
        /// <param name="lag">the lag parameter determines how much your data will be smoothed and how adaptive the algorithm
        /// is to changes in the long-term average of the data. The more stationary your data is, the more lags you should include
        /// (this should improve the robustness of the algorithm). If your data contains time-varying trends, you should consider
        /// how quickly you want the algorithm to adapt to these trends. I.e., if you put lag at 10, it takes 10 'periods' before
        /// the algorithm's treshold is adjusted to any systematic changes in the long-term average. So choose the lag parameter
        /// based on the trending behavior of your data and how adaptive you want the algorithm to be.
        /// </param>
        /// <param name="threshold">the threshold parameter is the number of standard deviations from the moving mean above which the
        /// algorithm will classify a new datapoint as being a signal. For example, if a new datapoint is 4.0 standard deviations above
        /// the moving mean and the threshold parameter is set as 3.5, the algorithm will identify the datapoint as a signal. This parameter
        /// should be set based on how many signals you expect. For example, if your data is normally distributed, a threshold (or: z-score)
        /// of 3.5 corresponds to a signaling probability of 0.00047 (from this table), which implies that you expect a signal once every 2128 datapoints (1/0.00047).
        /// The threshold therefore directly influences how sensitive the algorithm is and thereby also determines how often the algorithm signals.
        /// Examine your own data and choose a sensible threshold that makes the algorithm signal when you want it to (some trial-and-error might
        /// be needed here to get to a good threshold for your purpose).</param>
        /// <param name="influence">this parameter determines the influence of signals on the algorithm's detection threshold.
        /// If put at 0, signals have no influence on the threshold, such that future signals are detected based on a threshold
        /// that is calculated with a mean and standard deviation that is not influenced by past signals. If put at 0.5, signals
        /// have half the influence of normal data points. Another way to think about this is that if you put the influence at 0,
        /// you implicitly assume stationarity (i.e. no matter how many signals there are, you always expect the time series to
        /// return to the same average over the long term). If this is not the case, you should put the influence parameter somewhere
        /// between 0 and 1, depending on the extent to which signals can systematically influence the time-varying trend of the data.
        /// E.g., if signals lead to a structural break of the long-term average of the time series, the influence parameter should
        /// be put high (close to 1) so the threshold can react to structural breaks quickly.</param>
        /// <returns></returns>
        public static ZScoreOutput StartAlgo(List<double> input, int lag, double threshold, double influence)
        {
            // init variables!
            int[] signals = new int[input.Count];
            double[] filteredY = new List<double>(input).ToArray();
            double[] avgFilter = new double[input.Count];
            double[] stdFilter = new double[input.Count];

            var initialWindow = new List<double>(filteredY).Skip(0).Take(lag).ToList();

            avgFilter[lag - 1] = Mean(initialWindow);
            stdFilter[lag - 1] = StdDev(initialWindow);

            for (int i = lag; i < input.Count; i++)
            {
                if (Math.Abs(input[i] - avgFilter[i - 1]) > threshold * stdFilter[i - 1])
                {
                    signals[i] = (input[i] > avgFilter[i - 1]) ? 1 : -1;
                    filteredY[i] = influence * input[i] + (1 - influence) * filteredY[i - 1];
                }
                else
                {
                    signals[i] = 0;
                    filteredY[i] = input[i];
                }

                // Update rolling average and deviation
                var slidingWindow = new List<double>(filteredY).Skip(i - lag).Take(lag + 1).ToList();

                var tmpMean = Mean(slidingWindow);
                var tmpStdDev = StdDev(slidingWindow);

                avgFilter[i] = Mean(slidingWindow);
                stdFilter[i] = StdDev(slidingWindow);
            }

            // Copy to convenience class 
            var result = new ZScoreOutput
            {
                input = input,
                avgFilter = new List<double>(avgFilter),
                signals = new List<int>(signals),
                filtered_stddev = new List<double>(stdFilter)
            };

            return result;
        }

        private static double Mean(List<double> list)
        {
            // Simple helper function! 
            return list.Average();
        }

        private static double StdDev(List<double> values)
        {
            double ret = 0;
            if (values.Count() > 0)
            {
                double avg = values.Average();
                double sum = values.Sum(d => Math.Pow(d - avg, 2));
                ret = Math.Sqrt((sum) / (values.Count() - 1));
            }
            return ret;
        }
    }
}
