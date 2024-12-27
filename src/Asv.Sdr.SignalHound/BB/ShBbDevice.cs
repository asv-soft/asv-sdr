using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace Asv.Sdr.SignalHound
{
    public enum BBMode : uint
    {
        Sweeping = BbApi.BB_SWEEPING,
        RealTime = BbApi.BB_REAL_TIME,
        Streaming = BbApi.BB_STREAMING,
        AudioDemod = BbApi.BB_AUDIO_DEMOD,
        TgSweeping = BbApi.BB_TG_SWEEPING,
    }

    public enum BBSubMode : uint
    {
        StreamIq = BbApi.BB_STREAM_IQ,
        StreamIf = BbApi.BB_STREAM_IF,
        DirectRf = BbApi.BB_DIRECT_RF,
        TimeStamp = BbApi.BB_TIME_STAMP,
    }

    public enum BBRbwShape : uint
    {
        Nuttall = BbApi.BB_RBW_SHAPE_NUTTALL,
        Flattop = BbApi.BB_RBW_SHAPE_FLATTOP,
        Cispr = BbApi.BB_RBW_SHAPE_CISPR,
    }

    public enum BbRejection : uint
    {
        NoSpurReject = BbApi.BB_NO_SPUR_REJECT,
        SpurReject = BbApi.BB_SPUR_REJECT,
    }

    public enum BbDetector : uint
    {
        MinAndMax = BbApi.BB_MIN_AND_MAX,
        Average = BbApi.BB_AVERAGE,
    }

    public enum BbScale : uint
    {
        LogScale = BbApi.BB_LOG_SCALE,
        LinScale = BbApi.BB_LIN_SCALE,
        LogFullScale = BbApi.BB_LOG_FULL_SCALE,
        LinFullScale = BbApi.BB_LIN_FULL_SCALE,
    }

    public struct BbTraceInfo
    {
        public uint TraceLen { get; }
        public double BinSize { get; }
        public double Start { get; }

        public BbTraceInfo(uint traceLen, double binSize, double start)
        {
            TraceLen = traceLen;
            BinSize = binSize;
            Start = start;
        }
    }

    public struct BbRealTimeInfo
    {
        public int FrameWidth { get; }
        public int FrameHeight { get; }

        public BbRealTimeInfo(int frameWidth, int frameHeight)
        {
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
        }
    }

    public interface IShBbDevice
    {
        string FirmwareVersion { get; }
        string Name { get; }
        string SerialNumber { get; }
        Task Init(
            BBMode mode = BBMode.Streaming,
            BBSubMode subMode = BBSubMode.StreamIq,
            CancellationToken cancel = default
        );

        /// <summary>
        /// See bbConfigureIQCenter for configuring I/Q streaming center frequency.
        ///
        /// This function configures the sweep frequency range. Start and stop frequencies can be determined from the center and span.
        ///
        /// start = center – (span / 2)
        /// stop = center + (span / 2)
        /// During initialization a more precise start frequency and span is determined and returned in the bbQueryTraceInfo function.
        ///
        /// The start/stop frequencies cannot exceed [9kHz, 6.4GHz].
        ///
        /// There is an absolute minimum operating span of 20 Hz, but 200kHz is a suggested minimum.
        ///
        /// Certain modes of operation have specific frequency range limits. Those mode dependent limits are tested against during bbInitiate.
        /// </summary>
        /// <param name="center">Center frequency in Hz.</param>
        /// <param name="span">Span in Hz.</param>
        /// <param name="cancel">cancel.</param>
        /// <returns>.</returns>
        Task ConfigureCenterSpan(double center, double span, CancellationToken cancel = default);
        Task ConfigureGain(int? gain = null, CancellationToken cancel = default);
        Task ConfigureLevel(
            double refLevel,
            double? attenuation = null,
            CancellationToken cancel = default
        );
        Task ConfigureIQ(
            int downsampleFactor,
            double bandwidth,
            CancellationToken cancel = default
        );
        Task<StreamInfo> Read(
            float[] iqBuffer,
            int[] triggers,
            bool skipOldData = true,
            CancellationToken cancel = default
        );

        /// <summary>
        /// For standard bandwidths, the API uses the 3 dB points to define the RBW. For the CISPR RBW shape, 6dB bandwidths are used.
        ///
        /// The video bandwidth is implemented as an IIR filter applied bin-by-bin to a sequence of overlapping FFTs. Larger RBW/VBW ratios require more FFTs.
        /// The API uses the Stanford flattop window when FLATTOP is selected. The Nuttall window shape trades increased measurement speed for reduces measurement accuracy by adding up to 0.8dB scalloping losses. The CISPR window uses a Gaussian window with 6dB /// cutoff.
        ///
        /// All windows use zero-padding to achieve arbitrary RBWs. Only powers of 2 FFT sizes are used in the API.
        /// sweepTime applies to standard swept analysis and is ignored for other operating modes. If in sweep mode, sweepTime is the amount of time the device will spend collecting data before processing. Increasing this value is useful for capturing signals of /// interest or viewing a more consistent view of the spectrum. Increasing sweepTime can have a large impact on the resources used by the API due to the increase of data needing to be stored and the amount of signal processing performed.
        /// Rejection can be used to optimize certain aspects of the signal. The default is BB_NO_SPUR_REJECT and should be used for most measurements. If you have a steady CW or slowly changing signal and need to minimize image and spurious responses from the /// device, use BB_SPUR_REJECT. Rejection is ignored outside of standard swept analysis.
        /// </summary>
        /// <param name="rbw">Resolution bandwidth in Hz. RBWs can be set to arbitrary values but may be limited by mode of operation and span.</param>
        /// <param name="vbw">Video bandwidth in Hz. VBW must be less than or equal to RBW. VBW can be arbitrary. For best performance use RBW as the VBW. When VBW is set equal to RBW, no VBW filtering is performed.</param>
        /// <param name="sweepTime">Suggest a sweep time in seconds. In sweep mode, this value specifies how long the BB60 should sample spectrum for the configured sweep. Larger sweep times may increase the odds of capturing spectral events at the cost of slower sweep rates. The range of possible sweepTime values run from 1ms -> 100ms or [0.001 – 0.1].</param>
        /// <param name="rbwShape">The possible values for rbwShape are BB_RBW_SHAPE_NUTTALL, BB_RBW_SHAPE_FLATTOP, and BB_RBW_SHAPE_CISPR. This choice determines the window function used and the bandwidth cutoff of the RBW filter. BB_RBW_SHAPE_NUTTALL is default and unchangeable for real-time operation.</param>
        /// <param name="rejection">The possible values for rejection are BB_NO_SPUR_REJECT and BB_SPUR_REJECT.</param>
        /// <param name="cancel">cancel.</param>
        /// <returns>.</returns>
        Task ConfigureSweepCoupling(
            double rbw,
            double vbw,
            double sweepTime,
            BBRbwShape rbwShape,
            BbRejection rejection,
            CancellationToken cancel = default
        );

        /// <summary>
        /// The detector parameter specifies how to produce the results of the signal processing for the final sweep. Depending on settings, potentially many overlapping FFTs will be performed on the input time domain data to retrieve a more consistent and accurate result. When the results overlap detector chooses whether to average the results together or maintain the minimum and maximum values. If averaging is chosen, the min and max trace arrays returned from bbFetchTrace will contain the same averaged data.
        /// The scale parameter will change the units of returned sweeps. If BB_LOG_SCALE is provided, sweeps will be returned as dBm values, If BB_LIN_SCALE is return, the returned units will be in milli-volts. If the full-scale units are specified, no corrections are applied to the data and amplitudes are taken directly from the full scale input.
        /// </summary>
        /// <param name="detector">Specifies the video detector. The two possible values for detector type are BB_AVERAGE and BB_MIN_AND_MAX.</param>
        /// <param name="scale">Specifies the scale in which sweep results are returned int. The four possible values for scale are BB_LOG_SCALE, BB_LIN_SCALE, BB_LOG_FULL_SCALE, and BB_LIN_FULL_SCALE. Returns</param>
        /// <param name="cancel">cancel.</param>
        /// <returns>.</returns>
        Task ConfigureAcquisition(
            BbDetector detector,
            BbScale scale,
            CancellationToken cancel = default
        );

        /// <summary>
        /// The function allows you to configure additional parameters of the real-time frames returned from the API. If this function is not called a scale of 100dB is used and a frame rate of 30fps is used. For more information regarding real-time mode see Real-Time Spectrum Analysis.
        /// </summary>
        /// <param name="frameScale">Specifies the height in dB of the real-time frame. The value is ignored if the scale is linear. Possible values range from [10 – 200].</param>
        /// <param name="frameRate">Specifies the rate at which frames are generated in real-time mode, in frames per second. Possible values range from [4 – 30], where four means a frame is generated every 250ms and 30 means a frame is generated every ~33 ms.</param>
        /// <param name="cancel">cancel.</param>
        /// <returns>.</returns>
        Task ConfigureRealTime(
            double frameScale,
            int frameRate,
            CancellationToken cancel = default
        );

        /// <summary>
        /// This function should be called to determine sweep characteristics after a device has been configured and initiated for sweep mode.
        /// </summary>
        /// <param name="cancel">cancel.</param>
        /// <returns>.</returns>
        Task<BbTraceInfo> QueryTraceInfo(CancellationToken cancel = default);

        /// <summary>
        /// This function should be called after initializing the device for real-time mode.
        /// </summary>
        /// <param name="cancel">cancel.</param>
        /// <returns>.</returns>
        Task<BbRealTimeInfo> QueryRealTimeInfo(CancellationToken cancel = default);
    }

    public class StreamInfo
    {
        public StreamInfo(int dataRemaining, int sampleLoss, int sec, int nano)
        {
            DataRemaining = dataRemaining;
            SampleLoss = sampleLoss;
            Sec = sec;
            Nano = nano;
        }

        public int Nano { get; init; }

        public int Sec { get; init; }

        public int SampleLoss { get; init; }

        public int DataRemaining { get; init; }
    }

    public class ShBbDevice : DisposableOnceWithCancel, IShBbDevice
    {
        #region Static
        public static string ApiVersion => BbApi.bbGetAPIString();

        public static IEnumerable<int> GetAvailableDevices()
        {
            var devices = new int[BbApi.BB_MAX_DEVICES];
            var deviceCount = 0;
            BbApi.bbGetSerialNumberList(devices, ref deviceCount);
            for (var i = 0; i < deviceCount; i++)
            {
                yield return devices[i];
            }
        }

        private void InternalCheckStatus(bbStatus status, bool throwIfWarning = false)
        {
            if (status == bbStatus.bbNoError)
            {
                return;
            }

            if (status > 0 && throwIfWarning == false)
            {
                return;
            }

            var err = BbApi.bbGetStatusString(status);
            _logger.ZLogError($"SignalHound device error: {err}");
            throw new SignalHoundException(err, status);
        }

        #endregion

        private readonly int _deviceHandle;
        private readonly TaskFactory _taskFactory;
        private readonly ILogger<ShBbDevice> _logger;

        public ShBbDevice(
            int serial,
            TaskFactory? taskFactory = null,
            ILogger<ShBbDevice>? logger = null
        )
        {
            _logger = logger ?? NullLogger<ShBbDevice>.Instance;
            InternalCheckStatus(BbApi.bbOpenDeviceBySerialNumber(ref _deviceHandle, serial));
            Name = BbApi.bbGetDeviceName(_deviceHandle);
            SerialNumber = BbApi.bbGetSerialString(_deviceHandle);
            FirmwareVersion = BbApi.bbGetFirmwareString(_deviceHandle);
            _taskFactory =
                taskFactory
                ?? new TaskFactory(
                    new SingleThreadTaskScheduler($"BB device {serial}").DisposeItWith(Disposable)
                );
        }

        public string FirmwareVersion { get; }
        public string Name { get; }
        public string SerialNumber { get; }

        public Task ConfigureLevel(
            double refLevel,
            double? attenuation = null,
            CancellationToken cancel = default
        )
        {
            if (attenuation.HasValue && attenuation > BbApi.BB_MAX_ATTENUATION)
            {
                throw new ArgumentOutOfRangeException(nameof(attenuation));
            }

            return _taskFactory.StartNew(
                () =>
                    InternalCheckStatus(
                        BbApi.bbConfigureLevel(
                            _deviceHandle,
                            refLevel,
                            attenuation ?? BbApi.BB_AUTO_GAIN
                        )
                    ),
                cancel
            );
        }

        public Task ConfigureCenterSpan(
            double center,
            double span,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () => InternalCheckStatus(BbApi.bbConfigureCenterSpan(_deviceHandle, center, span)),
                cancel
            );
        }

        public Task ConfigureSweepCoupling(
            double rbw,
            double vbw,
            double sweepTime,
            BBRbwShape rbwShape,
            BbRejection rejection,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () =>
                    InternalCheckStatus(
                        BbApi.bbConfigureSweepCoupling(
                            _deviceHandle,
                            rbw,
                            vbw,
                            sweepTime,
                            (uint)rbwShape,
                            (uint)rejection
                        )
                    ),
                cancel
            );
        }

        public Task ConfigureAcquisition(
            BbDetector detector,
            BbScale scale,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () =>
                    InternalCheckStatus(
                        BbApi.bbConfigureAcquisition(_deviceHandle, (uint)detector, (uint)scale)
                    ),
                cancel
            );
        }

        public Task ConfigureRealTime(
            double frameScale,
            int frameRate,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () =>
                    InternalCheckStatus(
                        BbApi.bbConfigureRealTime(_deviceHandle, frameScale, frameRate)
                    ),
                cancel
            );
        }

        public Task Init(
            BBMode mode = BBMode.Streaming,
            BBSubMode subMode = BBSubMode.StreamIq,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () =>
                    InternalCheckStatus(BbApi.bbInitiate(_deviceHandle, (uint)mode, (uint)subMode)),
                cancel
            );
        }

        public Task<BbTraceInfo> QueryTraceInfo(CancellationToken cancel = default)
        {
            return _taskFactory.StartNew(
                () =>
                {
                    uint traceLen = 0;
                    double binSize = 0;
                    double start = 0;
                    InternalCheckStatus(
                        BbApi.bbQueryTraceInfo(_deviceHandle, ref traceLen, ref binSize, ref start)
                    );
                    return new BbTraceInfo(traceLen, binSize, start);
                },
                cancel
            );
        }

        public Task<BbRealTimeInfo> QueryRealTimeInfo(CancellationToken cancel = default)
        {
            return _taskFactory.StartNew(
                () =>
                {
                    int frameWidth = 0;
                    int frameHeight = 0;
                    InternalCheckStatus(
                        BbApi.bbQueryRealTimeInfo(_deviceHandle, ref frameWidth, ref frameHeight)
                    );
                    return new BbRealTimeInfo(frameWidth, frameHeight);
                },
                cancel
            );
        }

        public Task ConfigureIQ(
            int downsampleFactor,
            double bandwidth,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () =>
                    InternalCheckStatus(
                        BbApi.bbConfigureIQ(_deviceHandle, downsampleFactor, bandwidth)
                    ),
                cancel
            );
        }

        public Task ConfigureGain(int? gain = null, CancellationToken cancel = default)
        {
            if (gain.HasValue && gain > BbApi.BB60C_MAX_GAIN)
            {
                throw new ArgumentOutOfRangeException(nameof(gain));
            }

            return _taskFactory.StartNew(
                () =>
                    InternalCheckStatus(
                        BbApi.bbConfigureGain(_deviceHandle, gain ?? BbApi.BB_AUTO_GAIN)
                    ),
                cancel
            );
        }

        public Task<StreamInfo> Read(
            float[] iqBuffer,
            int[] triggers,
            bool skipOldData = true,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () =>
                {
                    unsafe
                    {
                        var bufferLen = 0;
                        double bandwidth = 0;
                        int samplesPerSec = 0;

                        BbApi.bbQueryStreamInfo(0, ref bufferLen, ref bandwidth, ref samplesPerSec);
                        var f1 = new Double[1024];
                        var f2 = new Double[1024];
                        var f3 = new float[1024];
                        var f4 = new float[1024];

                        // using var pin = iqBuffer.Pin();
                        var dataRemaining = 0;
                        var sampleLoss = 0;
                        var sec = 0;
                        var nano = 0;
                        BbApi.bbFetchTrace(_deviceHandle, 1024, f1, f2);

                        // InternalCheckStatus(BbApi.bbGetIQUnpacked(_deviceHandle, iqBuffer, iqBuffer.Length/2, new int[16], 16,skipOldData ? 1:0, ref dataRemaining, ref sampleLoss, ref sec, ref nano ));
                        return new StreamInfo(dataRemaining, sampleLoss, sec, nano);
                    }
                },
                cancel
            );
        }

        protected override void InternalDisposeOnce()
        {
            base.InternalDisposeOnce();
            try
            {
                InternalCheckStatus(BbApi.bbAbort(_deviceHandle));
            }
            catch (Exception e)
            {
                _logger.ZLogError(e, $"Error to bbAbort device:{e.Message}");
            }

            try
            {
                InternalCheckStatus(BbApi.bbCloseDevice(_deviceHandle));
            }
            catch (Exception e)
            {
                _logger.ZLogError(e, $"Error to close device:{e.Message}");
            }
        }

        public Task FetchRealTimeFrame(
            Memory<float> sweepMin,
            Memory<float> sweepMax,
            Memory<float> frame,
            Memory<float> alphaFrame,
            CancellationToken cancel = default
        )
        {
            return _taskFactory.StartNew(
                () =>
                {
                    unsafe
                    {
                        using var sweepMinPin = sweepMin.Pin();
                        using var sweepMaxPin = sweepMax.Pin();
                        using var framePin = frame.Pin();
                        using var alphaFramePin = alphaFrame.Pin();
                        InternalCheckStatus(
                            BbApi.bbFetchRealTimeFrame(
                                _deviceHandle,
                                sweepMinPin.Pointer,
                                sweepMaxPin.Pointer,
                                framePin.Pointer,
                                alphaFramePin.Pointer
                            )
                        );
                    }
                },
                cancel
            );
        }
    }
}
