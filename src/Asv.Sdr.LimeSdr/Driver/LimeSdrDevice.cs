using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using Microsoft.Extensions.Logging;
using ZLogger;

using static Asv.Sdr.LimeSdr.NativeMethods;

namespace Asv.Sdr.LimeSdr
{
    public class LimeSdrDevice:DisposableOnceWithCancel, ILimeSdrDevice
    {
        #region Static

        private static readonly LogCallBack _callback;
        private static readonly AsyncLocal<NativeOperationScope?> CurrentNativeOperation = new();
        static LimeSdrDevice()
        {
            _callback = OnLmsLog;
            GC.KeepAlive(_callback);
            LMS_RegisterLogHandler(_callback);
        }

        private static readonly object Sync = new();

        public static unsafe IReadOnlyList<string> GetAvailableDevices()
        {
            lock (Sync)
            {
                var buff = new byte[256 * 8];
                var result = new List<string>();
                fixed (byte* inBuf = buff)
                {
                    var count = LMS_GetDeviceList(inBuf);
                    for (var i = 0; i < count; i++)
                    {
                        var sb = new StringBuilder();
                        for (int j = 0; j < 256; j++)
                        {
                            var t = buff[i * 256 + j];
                            if (t == '\0') break;
                            sb.Append((char)t);
                        }
                        result.Add(sb.ToString());
                    }
                }
                return result;
            }
        }
        
        public static unsafe string GetApiVersion()
        {
            var str = LMS_GetLibraryVersion();
            return str == null ? string.Empty : Marshal.PtrToStringAnsi((IntPtr)str) ?? string.Empty;
        }
        

        #endregion
        
        private readonly IntPtr _device;
        private readonly TaskFactory _taskFactory;
        private readonly List<ILmsStream> _streamList = new(4);
        private readonly bool _isThreadSafe;
        private readonly HashSet<string> _ignoreLogLmsParams;
        private readonly ILogger _logger;
        private readonly ILmsRegisterEditor _registerEditor;


        public LimeSdrDevice(string deviceId, bool isThreadSave = true, ILoggerFactory? logFactory = null)
            :this(deviceId,isThreadSave,logFactory?.CreateLogger<LimeSdrDevice>() ?? LmsLogManager.GetLogger(nameof(LimeSdrDevice)),LimeSdrParams.LMS7_CAPSEL,LimeSdrParams.LMS7_CAPTURE)
        {
            
        }
        public LimeSdrDevice(string deviceId, bool isThreadSave,ILogger logger, params LMS7Parameter[] ignoreLogLmsParams)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _registerEditor = new LmsRegisterEditor(this);
            _logger = logger;
            DeviceId = deviceId;
            _device = IntPtr.Zero;
            _isThreadSafe = isThreadSave;
            _ignoreLogLmsParams = new HashSet<string>(ignoreLogLmsParams.Select(x=>x.name),StringComparer.InvariantCultureIgnoreCase);
            
            _logger.ZLogDebug($"Try to open LimeSDR device {deviceId}");

            Disposable.AddAction(() =>
            {
                foreach (var stream in _streamList.ToArray())
                {
                    stream.Dispose();
                }
                if (_device != IntPtr.Zero)
                {
                    Check(LMS_Close(_device), nameof(LMS_Close));
                }
            });

            _taskFactory = isThreadSave
                ? new TaskFactory(
                    new SingleThreadTaskScheduler($"LMS device stream {deviceId}").DisposeItWith(Disposable))
                : Task.Factory;
            for (var attempt = 1; ; attempt++)
            {
                var initialization = TryInitializeDevice(attempt);
                if (initialization.Failure is null)
                {
                    _device = initialization.Device;
                    break;
                }

                var failure = initialization.Failure.Value;
                var retryDelay = LimeSdrNativeOperationPolicy.GetRetryDelay(
                    failure.ResultCode,
                    attempt
                );
                if (retryDelay is null)
                {
                    throw CreateNativeOperationException(failure);
                }

                LogRetry(failure, retryDelay.Value);
                Thread.Sleep(retryDelay.Value);
            }
        }

        private static void OnLmsLog(LogLevel level, string msg)
        {
            var context = CurrentNativeOperation.Value;
            context?.Capture(level, msg);
            var logMessage = context is null
                ? msg
                : $"[LMS operation={context.Operation} device={context.DeviceId} attempt={context.Attempt} details={context.Details}] {msg}";

            switch (level)
            {
                case LogLevel.LOG_LEVEL_CRITICAL:
                    LmsLogManager.Logger.ZLogCritical($"{logMessage}");
                    break;
                case LogLevel.LOG_LEVEL_ERROR:
                    LmsLogManager.Logger.ZLogError($"{logMessage}");
                    break;
                case LogLevel.LOG_LEVEL_WARNING:
                    LmsLogManager.Logger.ZLogWarning($"{logMessage}");
                    break;
                case LogLevel.LOG_LEVEL_INFO:
                    LmsLogManager.Logger.ZLogInformation($"{logMessage}");
                    break;
                case LogLevel.LOG_LEVEL_DEBUG:
                default:
                    LmsLogManager.Logger.ZLogTrace($"{logMessage}");
                    break;
            }
        }

        private DeviceInitializationAttempt TryInitializeDevice(int attempt)
        {
            var candidate = IntPtr.Zero;
            var details = $"constructorAttempt={attempt}";
            var openResult = ExecuteNativeOperation(
                nameof(LMS_Open),
                details,
                attempt,
                () => LMS_Open(out candidate, DeviceId, null)
            );
            if (openResult.ResultCode != 0)
            {
                return new DeviceInitializationAttempt(IntPtr.Zero, openResult);
            }

            var resetResult = ExecuteNativeOperation(
                nameof(LMS_Reset),
                details,
                attempt,
                () => LMS_Reset(candidate)
            );
            if (resetResult.ResultCode != 0)
            {
                CloseFailedInitialization(candidate, attempt, details);
                return new DeviceInitializationAttempt(IntPtr.Zero, resetResult);
            }

            var initResult = ExecuteNativeOperation(
                nameof(LMS_Init),
                details,
                attempt,
                () => LMS_Init(candidate)
            );
            if (initResult.ResultCode != 0)
            {
                CloseFailedInitialization(candidate, attempt, details);
                return new DeviceInitializationAttempt(IntPtr.Zero, initResult);
            }

            var readbackFailure = ReadAndLogSampleRates(
                candidate,
                nameof(LMS_Init),
                details,
                attempt,
                null,
                null
            );
            if (readbackFailure is not null)
            {
                CloseFailedInitialization(candidate, attempt, details);
                return new DeviceInitializationAttempt(IntPtr.Zero, readbackFailure);
            }

            return new DeviceInitializationAttempt(candidate, null);
        }

        private void CloseFailedInitialization(IntPtr candidate, int attempt, string details)
        {
            var closeResult = ExecuteNativeOperation(
                nameof(LMS_Close),
                $"{details} cleanup=true",
                attempt,
                () => LMS_Close(candidate)
            );
            if (closeResult.ResultCode != 0)
            {
                _logger.LogError(
                    "Failed to close LimeSDR after an initialization error: device={DeviceId} resultCode={ResultCode} nativeError={NativeError}",
                    DeviceId,
                    closeResult.ResultCode,
                    closeResult.NativeError
                );
            }
        }


        public string DeviceId { get; }
        public IntPtr DeviceHandle => _device;

        public Task Reset( CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return -1;
                return LMS_Reset(_device);
            }, cancel);
        }
        
        public Task<int> GetChannelNumbers(LmsChannel channel, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return -1;
                return LMS_GetNumChannels(_device, channel == LmsChannel.Tx);
            }, cancel);
        }

        public Task<double> GetChipTemperature(CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                unsafe
                {
                    if (IsDisposed) return double.NaN;
                    var buffer = new double[1];
                    fixed (double* buf = &buffer[0])
                    {
                        Check(LMS_GetChipTemperature(_device, 0, buf),nameof(LMS_GetChipTemperature));
                    }
                    return buffer[0];
                }
            }, cancel);
        }

        public Task SetGFIRLPF(LmsChannel type, uint channel, bool enable, double bandwidth, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set LMS {type:G}{channel} GFIR LPF:{bandwidth} = {enable}");
                Check(LMS_SetGFIRLPF(_device, type == LmsChannel.Tx, channel, enable, bandwidth),nameof(LMS_SetGFIRLPF));
            }, cancel);
        }

        public Task Calibrate(LmsChannel type, uint channel, double bandwidth, uint flags, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Do LMS {type:G}{channel} calibration with bandwidth:{bandwidth} and flags {flags}");
                Check(LMS_Calibrate(_device, type == LmsChannel.Tx, channel, bandwidth, flags),nameof(LMS_Calibrate));
            }, cancel);
        }

        #region Save\load config        

        public Task SaveConfig(string path, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Save LMS config to file {path}");
                Check(LMS_SaveConfig(_device, path), nameof(LMS_SaveConfig));
            }, cancel);
        }

        public Task LoadConfig(string path, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Load LMS config from file {path}");
                Check(LMS_LoadConfig(_device, path),nameof(LMS_LoadConfig));
            }, cancel);
        }

        #endregion

        #region SampleRate  

        public Task SetSampleRate(double rate, uint oversample, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set sample rate {rate} with oversample {oversample}");
                SetSampleRateWithDiagnostics(null, rate, oversample, cancel);
            }, cancel);
        }

        public Task SetSampleRateDir(LmsChannel type, double rate, uint oversample, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set sample rate for {type:G} {rate} with oversample {oversample}");
                SetSampleRateWithDiagnostics(type, rate, oversample, cancel);
            }, cancel);
        }

        private void SetSampleRateWithDiagnostics(
            LmsChannel? direction,
            double rate,
            uint oversample,
            CancellationToken cancel
        )
        {
            var operation = direction is null
                ? nameof(LMS_SetSampleRate)
                : nameof(LMS_SetSampleRateDir);
            var details = direction is null
                ? $"rateHz={rate:R} oversample={oversample}"
                : $"direction={direction:G} rateHz={rate:R} oversample={oversample}";

            for (var attempt = 1; ; attempt++)
            {
                cancel.ThrowIfCancellationRequested();
                var setResult = ExecuteNativeOperation(
                    operation,
                    details,
                    attempt,
                    () => direction is null
                        ? LMS_SetSampleRate(_device, rate, oversample)
                        : LMS_SetSampleRateDir(
                            _device,
                            direction == LmsChannel.Tx,
                            rate,
                            oversample
                        )
                );

                var failure = setResult.ResultCode == 0
                    ? ReadAndLogSampleRates(
                        _device,
                        operation,
                        details,
                        attempt,
                        rate,
                        direction
                    )
                    : setResult;
                if (failure is null)
                {
                    return;
                }

                var retryDelay = LimeSdrNativeOperationPolicy.GetRetryDelay(
                    failure.Value.ResultCode,
                    attempt
                );
                if (retryDelay is null)
                {
                    throw CreateNativeOperationException(failure.Value);
                }

                LogRetry(failure.Value, retryDelay.Value);
                if (cancel.WaitHandle.WaitOne(retryDelay.Value))
                {
                    cancel.ThrowIfCancellationRequested();
                }
            }
        }

        private NativeOperationResult? ReadAndLogSampleRates(
            IntPtr device,
            string sourceOperation,
            string sourceDetails,
            int attempt,
            double? requestedRate,
            LmsChannel? requestedDirection
        )
        {
            var rxHostHz = double.NaN;
            var rxRfHz = double.NaN;
            var rxResult = ExecuteNativeOperation(
                nameof(LMS_GetSampleRate),
                $"source={sourceOperation} direction=Rx {sourceDetails}",
                attempt,
                () => LMS_GetSampleRate(device, false, 0, ref rxHostHz, ref rxRfHz)
            );
            if (rxResult.ResultCode != 0)
            {
                return rxResult;
            }

            var txHostHz = double.NaN;
            var txRfHz = double.NaN;
            var txResult = ExecuteNativeOperation(
                nameof(LMS_GetSampleRate),
                $"source={sourceOperation} direction=Tx {sourceDetails}",
                attempt,
                () => LMS_GetSampleRate(device, true, 0, ref txHostHz, ref txRfHz)
            );
            if (txResult.ResultCode != 0)
            {
                return txResult;
            }

            var valuesAreFinite = double.IsFinite(rxHostHz)
                && double.IsFinite(rxRfHz)
                && double.IsFinite(txHostHz)
                && double.IsFinite(txRfHz);
            var requestedRateMatches = requestedRate is null
                || requestedDirection switch
                {
                    LmsChannel.Rx => NearlyEqual(rxHostHz, requestedRate.Value),
                    LmsChannel.Tx => NearlyEqual(txHostHz, requestedRate.Value),
                    null => NearlyEqual(rxHostHz, requestedRate.Value)
                        && NearlyEqual(txHostHz, requestedRate.Value),
                    _ => false,
                };

            _logger.LogDebug(
                "LMS sample-rate readback: source={SourceOperation} device={DeviceId} attempt={Attempt} requestedRateHz={RequestedRateHz} requestedDirection={RequestedDirection} rxHostHz={RxHostHz} rxRfHz={RxRfHz} txHostHz={TxHostHz} txRfHz={TxRfHz} finite={Finite} requestedRateMatches={RequestedRateMatches}",
                sourceOperation,
                DeviceId,
                attempt,
                requestedRate,
                requestedDirection?.ToString() ?? "Both",
                rxHostHz,
                rxRfHz,
                txHostHz,
                txRfHz,
                valuesAreFinite,
                requestedRateMatches
            );

            if (!valuesAreFinite || !requestedRateMatches)
            {
                _logger.LogWarning(
                    "Unexpected LMS sample-rate readback after successful native call: source={SourceOperation} device={DeviceId} finite={Finite} requestedRateMatches={RequestedRateMatches} requestedRateHz={RequestedRateHz} requestedDirection={RequestedDirection} rxHostHz={RxHostHz} rxRfHz={RxRfHz} txHostHz={TxHostHz} txRfHz={TxRfHz}",
                    sourceOperation,
                    DeviceId,
                    valuesAreFinite,
                    requestedRateMatches,
                    requestedRate,
                    requestedDirection?.ToString() ?? "Both",
                    rxHostHz,
                    rxRfHz,
                    txHostHz,
                    txRfHz
                );
            }

            return null;
        }

        private static bool NearlyEqual(double actual, double expected)
        {
            if (!double.IsFinite(actual) || !double.IsFinite(expected))
            {
                return false;
            }

            return Math.Abs(actual - expected) <= Math.Max(1.0, Math.Abs(expected) * 0.001);
        }

        #endregion

        public Task SetFrequency(LmsChannel type, uint channel, double freq, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set {type:G}{channel} LO frequency {freq} ");
                Check(LMS_SetLOFrequency(_device, type == LmsChannel.Tx, channel, freq),nameof(LMS_SetLOFrequency));
            }, cancel);
        }
        
        public async Task<double> GetFrequency(LmsChannel type, uint channel, CancellationToken cancel)
        {
            double freq = 0;
            await _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set {type:G}{channel} LO frequency {freq} ");
                Check(LMS_GetLOFrequency(_device, type == LmsChannel.Tx, channel, ref freq),nameof(LMS_SetLOFrequency));
            }, cancel);
            return freq;
        }

        public Task SetAntenna(LmsChannel type, uint channel, uint index, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set {type:G}{channel} antenna {index}");
                Check(LMS_SetAntenna(_device, type == LmsChannel.Tx, channel, index),nameof(LMS_SetAntenna));
            }, cancel);
        }

        #region Gain

        public Task<double> GetNormalizedGain(LmsChannel type, uint channel, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                unsafe
                {
                    if (IsDisposed) return double.NaN;
                    var buffer = new double[1];
                    fixed (double* buf = &buffer[0])
                    {
                        Check(LMS_GetNormalizedGain(_device, type == LmsChannel.Tx, channel, buf), nameof(LMS_GetGaindB));
                    }
                    
                    return buffer[0];
                }
            }, cancel);
        }

        public Task SetNormalizedGain(LmsChannel type, uint channel, double normalizedGain, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set {type:G}{channel} normalized gain  {normalizedGain}");
                Check(LMS_SetNormalizedGain(_device, type == LmsChannel.Tx, channel, normalizedGain),nameof(LMS_SetNormalizedGain));
            }, cancel);
        }

        public Task SetNormalizedGainDbm(LmsChannel type, uint channel, uint gainDbm, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set {type:G}{channel} gain  {gainDbm} dBm");
                Check(LMS_SetGaindB(_device, type == LmsChannel.Tx, channel, gainDbm),nameof(LMS_SetGaindB));
            }, cancel);
        }

        public Task<uint> GetNormalizedGainDbm(LmsChannel type, uint channel, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return uint.MaxValue;
                uint buf = 0;
                Check(LMS_GetGaindB(_device, type == LmsChannel.Tx, channel, ref buf), nameof(LMS_GetGaindB));
                return buf;
            }, cancel);
        }

       

        #endregion

        private Task DestroyStream(ILmsStream strm)
        {
            return _taskFactory.StartNew(() =>
            {
                _streamList.Remove(strm);
            });
        }

        public Task<ILmsStream> CreateStream(LmsChannel type, uint channel, uint bufferLength, float throughputVsLatency = 1F, bool flushPartialPacket = true, bool waitForTimestamp = false, CancellationToken cancel = default)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return null;
                _logger.ZLogInformation($"Create new stream {type:G}{channel} buff:{bufferLength}, throughputVsLatency:{throughputVsLatency}");
                var strm = new LmsStream(_taskFactory,  type, channel, _device, DeviceId, bufferLength, throughputVsLatency, new lms_stream_meta_t
                {
                    flushPartialPacket = flushPartialPacket,
                    waitForTimestamp = waitForTimestamp,
                },_isThreadSafe, DestroyStream, _logger);
                _streamList.Add(strm);
                return (ILmsStream)strm;
            }, cancel)!;
        }

        public Task EnableChannel(LmsChannel type, uint channel, bool isEnable, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Enable channel {type:G}{channel} ={isEnable}");
                Check(LMS_EnableChannel(_device, type == LmsChannel.Tx, channel, isEnable),nameof(LMS_EnableChannel));
            }, cancel);
        }

        public Task SetBandWidth(LmsChannel type, uint channel, double bandwidth, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set {type:G}{channel} BW={bandwidth}");
                Check(LMS_SetLPFBW(_device, type == LmsChannel.Tx, channel, bandwidth),nameof(LMS_SetLPFBW));
            }, cancel);
        }

        #region LPF

        public Task SetLPF(LmsChannel type, uint channel, bool enable, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Enable  {type:G}{channel} LPF={enable}");
                Check(LMS_SetLPF(_device, type == LmsChannel.Tx, channel, enable),nameof(LMS_SetLPF));
            }, cancel);
        }

        public Task SetLPFBw(LmsChannel type, uint channel, double bandwidth, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogInformation($"Set  {type:G}{channel} LPF BW={bandwidth}");
                Check(LMS_SetLPFBW(_device, type == LmsChannel.Tx, channel, bandwidth),nameof(LMS_SetLPFBW));
            }, cancel);
        }

        #endregion

        #region FpgaRegister

        public Task<ushort> ReadFpgaRegister(ushort address, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                unsafe
                {
                    if (IsDisposed) return ushort.MaxValue;
                    var buffer = new ushort[1];
                    fixed (ushort* buf = &buffer[0])
                    {
                        Check(LMS_ReadFPGAReg(_device, address, buf),nameof(LMS_ReadFPGAReg));
                    }
                    return buffer[0];
                }
            }, cancel);
        }

        public Task WriteFpgaRegister(ushort address, ushort value, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogTrace($"Set FPGA register [{address:X2}]={value}");
                Check(LMS_WriteFPGAReg(_device, address, value),nameof(LMS_WriteFPGAReg));
            }, cancel);
        }

        #endregion

        #region LMSReg  

        public Task<ushort> ReadLMSReg(uint address, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                unsafe
                {
                    if (IsDisposed) return ushort.MaxValue;
                    var buffer = new ushort[1];
                    fixed (ushort* buf = &buffer[0])
                    {
                        Check(LMS_ReadLMSReg(_device, address, buf),nameof(LMS_ReadLMSReg));
                    }
                    return buffer[0];
                }
            }, cancel);
        }

        public Task WriteLMSReg(UInt32 address, ushort value, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.ZLogTrace($"Set LMS register {address:X2}={value:X2}");
                Check(LMS_WriteLMSReg(_device, address, value),nameof(LMS_WriteLMSReg));
            }, cancel);
        }

        public Task WriteLMSParam(LMS7Parameter param, ushort value, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                if (_ignoreLogLmsParams.Contains(param.name) == false)
                {
                    _logger.ZLogTrace($"Set LMS param {param.name}={value:X2}");
                }
                Check(LMS_WriteParam(_device, param, value),nameof(LMS_WriteParam));
            }, cancel);
        }

        public Task<ushort> ReadLMSParam(LMS7Parameter param, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                unsafe
                {
                    if (IsDisposed) return ushort.MaxValue;
                    var buffer = new ushort[1];
                    fixed (ushort* buf = &buffer[0])
                    {
                        Check(LMS_ReadParam(_device, param, buf), nameof(LMS_ReadParam));
                    }

                    return buffer[0];
                }
            }, cancel);
        }

        #endregion

        #region GPIO

        public unsafe Task WriteGpioDirection(ReadOnlyMemory<byte> val, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                using var handle = val.Pin();
                Check(LMS_GPIODirWrite(_device, handle.Pointer, (uint)val.Length), nameof(LMS_GPIOWrite));
            }, cancel);
        }
        
        public unsafe Task WriteGpio(ReadOnlyMemory<byte> val, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                using var handle = val.Pin();
                Check(LMS_GPIOWrite(_device, handle.Pointer, (uint)val.Length), nameof(LMS_GPIOWrite));
            }, cancel);
        }
        
        public unsafe Task ReadGpioDirection(Memory<byte> val, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                using var handle = val.Pin();
                Check(LMS_GPIODirRead(_device, handle.Pointer, (uint)val.Length), nameof(LMS_GPIOWrite));
            }, cancel);
        }
        
        public unsafe Task ReadGpio(Memory<byte> val, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                using var handle = val.Pin();
                Check(LMS_GPIORead(_device, handle.Pointer, (uint)val.Length), nameof(LMS_GPIOWrite));
            }, cancel);
        }
        
        #endregion

        #region Single task

        protected Task AtomicEditRegister(Action<ILmsRegisterEditor> edit, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                edit(_registerEditor);
            }, cancel);
        }

        #endregion
        
        #region CustomRegister

        // public Task WriteCustomRegister(ushort addr, ushort val, CancellationToken cancel)
        // {
        //     return _taskFactory.StartNew(() =>
        //     {
        //         if (IsDisposed) return;
        //         Check(LMS_WriteFPGAReg(_device, 21, addr), nameof(LMS_WriteFPGAReg));
        //         Check(LMS_WriteFPGAReg(_device, 22, val), nameof(LMS_WriteFPGAReg));
        //     }, cancel);
        // }
        //
        // public Task<ushort> ReadCustomRegister(ushort addr, CancellationToken cancel)
        // {
        //     return _taskFactory.StartNew(() =>
        //     {
        //         unsafe
        //         {
        //             if (IsDisposed) return ushort.MaxValue;
        //             Check(LMS_WriteFPGAReg(_device, 21, addr), nameof(LMS_WriteFPGAReg));
        //             var buffer = new ushort[1];
        //             fixed (ushort* buf = &buffer[0])
        //             {
        //                 Check(LMS_ReadFPGAReg(_device, 22, buf), nameof(LMS_ReadFPGAReg));
        //             }
        //             return buffer[0];
        //         }
        //     }, cancel);
        //
        // }

        #endregion



        internal static void Check(int resultCode, string methodName)
        {
            if (resultCode != 0)
            {
                throw new Exception($"Call {methodName} error: {limesdr_strerror()}");
            }
        }

        private NativeOperationResult ExecuteNativeOperation(
            string operation,
            string details,
            int attempt,
            Func<int> nativeCall
        )
        {
            using var scope = new NativeOperationScope(operation, DeviceId, details, attempt);
            var stopwatch = Stopwatch.StartNew();
            int resultCode;
            try
            {
                resultCode = nativeCall();
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                _logger.LogError(
                    e,
                    "LMS native operation threw: operation={Operation} device={DeviceId} attempt={Attempt} elapsedMs={ElapsedMilliseconds} details={Details}",
                    operation,
                    DeviceId,
                    attempt,
                    stopwatch.Elapsed.TotalMilliseconds,
                    details
                );
                throw;
            }

            stopwatch.Stop();
            var nativeError = resultCode == 0 ? string.Empty : limesdr_strerror();
            var operationResult = new NativeOperationResult(
                operation,
                details,
                attempt,
                resultCode,
                nativeError,
                stopwatch.Elapsed,
                scope.CallbackErrorCount,
                scope.CallbackWarningCount,
                scope.PllPhaseSearchTimeoutCount
            );

            if (resultCode != 0)
            {
                _logger.LogWarning(
                    "LMS native operation failed: operation={Operation} device={DeviceId} attempt={Attempt} resultCode={ResultCode} elapsedMs={ElapsedMilliseconds} callbackErrors={CallbackErrors} callbackWarnings={CallbackWarnings} pllPhaseTimeouts={PllPhaseTimeouts} nativeError={NativeError} details={Details}",
                    operation,
                    DeviceId,
                    attempt,
                    resultCode,
                    stopwatch.Elapsed.TotalMilliseconds,
                    scope.CallbackErrorCount,
                    scope.CallbackWarningCount,
                    scope.PllPhaseSearchTimeoutCount,
                    nativeError,
                    details
                );
            }
            else if (scope.CallbackErrorCount != 0 || scope.PllPhaseSearchTimeoutCount != 0)
            {
                _logger.LogWarning(
                    "LMS native operation recovered after transient native diagnostics: operation={Operation} device={DeviceId} attempt={Attempt} resultCode=0 elapsedMs={ElapsedMilliseconds} callbackErrors={CallbackErrors} callbackWarnings={CallbackWarnings} pllPhaseTimeouts={PllPhaseTimeouts} details={Details}",
                    operation,
                    DeviceId,
                    attempt,
                    stopwatch.Elapsed.TotalMilliseconds,
                    scope.CallbackErrorCount,
                    scope.CallbackWarningCount,
                    scope.PllPhaseSearchTimeoutCount,
                    details
                );
            }
            else
            {
                _logger.LogDebug(
                    "LMS native operation completed: operation={Operation} device={DeviceId} attempt={Attempt} resultCode=0 elapsedMs={ElapsedMilliseconds} callbackErrors={CallbackErrors} callbackWarnings={CallbackWarnings} pllPhaseTimeouts={PllPhaseTimeouts} details={Details}",
                    operation,
                    DeviceId,
                    attempt,
                    stopwatch.Elapsed.TotalMilliseconds,
                    scope.CallbackErrorCount,
                    scope.CallbackWarningCount,
                    scope.PllPhaseSearchTimeoutCount,
                    details
                );
            }

            return operationResult;
        }

        private void LogRetry(NativeOperationResult failure, TimeSpan retryDelay)
        {
            _logger.LogWarning(
                "Retrying LMS native operation after nonzero result: operation={Operation} device={DeviceId} completedAttempt={Attempt} resultCode={ResultCode} retryDelayMs={RetryDelayMilliseconds} nativeError={NativeError} details={Details}",
                failure.Operation,
                DeviceId,
                failure.Attempt,
                failure.ResultCode,
                retryDelay.TotalMilliseconds,
                failure.NativeError,
                failure.Details
            );
        }

        private Exception CreateNativeOperationException(NativeOperationResult failure)
        {
            return new Exception(
                $"Call {failure.Operation} error for LimeSDR '{DeviceId}' "
                    + $"after attempt {failure.Attempt} ({failure.Details}): {failure.NativeError}"
            );
        }

        private readonly record struct DeviceInitializationAttempt(
            IntPtr Device,
            NativeOperationResult? Failure
        );

        private readonly record struct NativeOperationResult(
            string Operation,
            string Details,
            int Attempt,
            int ResultCode,
            string NativeError,
            TimeSpan Elapsed,
            int CallbackErrorCount,
            int CallbackWarningCount,
            int PllPhaseSearchTimeoutCount
        );

        private sealed class NativeOperationScope : IDisposable
        {
            private readonly NativeOperationScope? _previous;
            private int _callbackErrorCount;
            private int _callbackWarningCount;
            private int _pllPhaseSearchTimeoutCount;

            public NativeOperationScope(
                string operation,
                string deviceId,
                string details,
                int attempt
            )
            {
                Operation = operation;
                DeviceId = deviceId;
                Details = details;
                Attempt = attempt;
                _previous = CurrentNativeOperation.Value;
                CurrentNativeOperation.Value = this;
            }

            public string Operation { get; }
            public string DeviceId { get; }
            public string Details { get; }
            public int Attempt { get; }
            public int CallbackErrorCount => Volatile.Read(ref _callbackErrorCount);
            public int CallbackWarningCount => Volatile.Read(ref _callbackWarningCount);
            public int PllPhaseSearchTimeoutCount => Volatile.Read(
                ref _pllPhaseSearchTimeoutCount
            );

            public void Capture(LogLevel level, string message)
            {
                if (
                    level is LogLevel.LOG_LEVEL_CRITICAL or LogLevel.LOG_LEVEL_ERROR
                )
                {
                    Interlocked.Increment(ref _callbackErrorCount);
                }
                else if (level == LogLevel.LOG_LEVEL_WARNING)
                {
                    Interlocked.Increment(ref _callbackWarningCount);
                }

                if (LimeSdrNativeOperationPolicy.IsPllPhaseSearchTimeout(message))
                {
                    Interlocked.Increment(ref _pllPhaseSearchTimeoutCount);
                }
            }

            public void Dispose()
            {
                if (ReferenceEquals(CurrentNativeOperation.Value, this))
                {
                    CurrentNativeOperation.Value = _previous;
                }
            }
        }

        internal void InternalWriteFPGAReg(ushort addr, ushort val)
        {
            _logger.ZLogTrace($"Set FPGA register [{addr:X2}]={val}");
            Check(LMS_WriteFPGAReg(_device, addr, val),nameof(LMS_WriteFPGAReg));
        }

        internal ushort InternalReadFPGAReg(ushort addr)
        {
            unsafe
            {
                if (IsDisposed) return ushort.MaxValue;
                var buffer = new ushort[1];
                fixed (ushort* buf = &buffer[0])
                {
                    Check(LMS_ReadFPGAReg(_device, addr, buf),nameof(LMS_ReadFPGAReg));
                }
                return buffer[0];
            }
        }
    }

    public interface ILmsRegisterEditor
    {
        void WriteFPGAReg(ushort addr, ushort val);
        ushort RaedFPGAReg(ushort addr);
    }

    public class LmsRegisterEditor : ILmsRegisterEditor
    {
        private readonly LimeSdrDevice _device;

        public LmsRegisterEditor(LimeSdrDevice device)
        {
            _device = device;
        }
        public void WriteFPGAReg(ushort addr, ushort val)
        {
            _device.InternalWriteFPGAReg(addr, val);
        }

        public ushort RaedFPGAReg(ushort addr)
        {
            return _device.InternalReadFPGAReg(addr);
        }
    }
}
