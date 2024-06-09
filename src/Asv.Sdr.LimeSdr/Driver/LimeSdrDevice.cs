using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using NLog;
using static Asv.Sdr.LimeSdr.NativeMethods;

namespace Asv.Sdr.LimeSdr
{
    public class LimeSdrDevice:DisposableOnceWithCancel, ILimeSdrDevice
    {
        #region Static

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static readonly LogCallBack _callback;

        static LimeSdrDevice()
        {
            _callback = OnLmsLog;
            GC.KeepAlive(_callback);
            LMS_RegisterLogHandler(_callback);
        }

        private static readonly object _sync = new();

        public static unsafe IReadOnlyList<string> GetAvailableDevices()
        {
            lock (_sync)
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
        

        #endregion
        
        private readonly IntPtr _device;
        private readonly TaskFactory _taskFactory;
        private readonly List<ILmsStream> _streamList = new(4);
        private readonly bool _isThreadSafe;
        private HashSet<string> _ignoreLogLmsParams;
        
        public LimeSdrDevice(string deviceId, bool isThreadSave = false)
            :this(deviceId,isThreadSave,LimeSdrParams.LMS7_CAPSEL,LimeSdrParams.LMS7_CAPTURE)
        {
            
        }
        public LimeSdrDevice(string deviceId, bool isThreadSave, params LMS7Parameter[] ignoreLogLmsParams)
        {
            DeviceId = deviceId;
            _isThreadSafe = isThreadSave;
            _ignoreLogLmsParams = new HashSet<string>(ignoreLogLmsParams.Select(x=>x.name),StringComparer.InvariantCultureIgnoreCase);
            
            _logger.Debug("Try to open LimeSDR device {0}",deviceId);

            Disposable.AddAction(() =>
            {
                foreach (var stream in _streamList.ToArray())
                {
                    stream.Dispose();
                }
                Check(LMS_Close(_device), nameof(LMS_Close));
            });

            _taskFactory = isThreadSave
                ? new TaskFactory(
                    new SingleThreadTaskScheduler($"LMS device stream {deviceId}").DisposeItWith(Disposable))
                : Task.Factory;
            var errInitCnt = 0;
            while (true) // BUG in Lime SDR. Try open\close 3 times. If error => throw exception
            {
                if (LMS_Open(out _device, deviceId, null) != 0)
                {
                    throw new Exception("Cannot open LimeSDR device. Is the device locked somewhere?");
                }

                var err = LMS_Reset(_device);
                if (err == 0)
                {
                    err = LMS_Init(_device);
                    if (err == 0) break;
                }
                ++errInitCnt;
                LMS_Close(_device);
                if (errInitCnt > 3)
                {
                    Check(err,nameof(LMS_Close)); // error to init device
                }
            }
          
            Check(LMS_Init(_device),nameof(LMS_Init));
            //Check(LMS_Reset(_device), nameof(LMS_Reset));

            
        }

        private static void OnLmsLog(LogLevel level, string msg)
        {
            switch (level)
            {
                case LogLevel.LOG_LEVEL_CRITICAL:
                    _logger.Fatal("LMS=> {0}", msg);
                    break;
                case LogLevel.LOG_LEVEL_ERROR:
                    _logger.Error("LMS=> {0}", msg);
                    break;
                case LogLevel.LOG_LEVEL_WARNING:
                    _logger.Warn("LMS=> {0}", msg);
                    break;
                case LogLevel.LOG_LEVEL_INFO:
                    _logger.Info("LMS=> {0}", msg);
                    break;
                case LogLevel.LOG_LEVEL_DEBUG:
                    _logger.Debug("LMS=> {0}", msg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }


        public string DeviceId { get; }
        public IntPtr DeviceHandle => _device;

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
                _logger.Info("Set LMS {0:G}{1} GFIR LPF:{2} = {3}", type, channel, bandwidth, enable);
                Check(LMS_SetGFIRLPF(_device, type == LmsChannel.Tx, channel, enable, bandwidth),nameof(LMS_SetGFIRLPF));
            }, cancel);
        }

        public Task Calibrate(LmsChannel type, uint channel, double bandwidth, uint flags, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Do LMS {0:G}{1} calibration with bandwidth:{2} and flags {3}", type,channel,bandwidth,flags);
                Check(LMS_Calibrate(_device, type == LmsChannel.Tx, channel, bandwidth, flags),nameof(LMS_Calibrate));
            }, cancel);
        }

        #region Save\load config        

        public Task SaveConfig(string path, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info($"Save LMS config to file {path}");
                Check(LMS_SaveConfig(_device, path), nameof(LMS_SaveConfig));
            }, cancel);
        }

        public Task LoadConfig(string path, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info($"Load LMS config from file {path}");
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
                _logger.Info("Set sample rate {0} with oversample {1}",rate,oversample);
                Check(LMS_SetSampleRate(_device, rate, oversample),nameof(LMS_SetSampleRate));
            }, cancel);
        }

        public Task SetSampleRateDir(LmsChannel type, double rate, uint oversample, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Set sample rate for {0:G} {1} with oversample {2}", type, rate, oversample);
                Check(LMS_SetSampleRateDir(_device, type == LmsChannel.Tx, rate, oversample), nameof(LMS_SetSampleRateDir));
            }, cancel);
        }

        #endregion

        public Task SetFrequency(LmsChannel type, uint channel, double freq, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Set {0:G}{1} LO frequency {2} ", type, channel, freq);
                Check(LMS_SetLOFrequency(_device, type == LmsChannel.Tx, channel, freq),nameof(LMS_SetLOFrequency));
            }, cancel);
        }

        public Task SetAntenna(LmsChannel type, uint channel, uint index, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Set {0:G}{1} antenna {2:G}", type, channel, type == LmsChannel.Rx ? (LmsPathRx)index : (LmsPathTx)index);
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
                _logger.Info("Set {0:G}{1} normalized gain  {2:G}", type, channel, normalizedGain);
                Check(LMS_SetNormalizedGain(_device, type == LmsChannel.Tx, channel, normalizedGain),nameof(LMS_SetNormalizedGain));
            }, cancel);
        }

        public Task SetNormalizedGainDbm(LmsChannel type, uint channel, uint gainDbm, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Set {0:G}{1} gain  {2:G} dBm", type, channel, gainDbm);
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
                _logger.Info("Create new stream {0:G}{1} buff:{2}, throughputVsLatency:{3}", type, channel, bufferLength, throughputVsLatency);
                var strm = new LmsStream(_taskFactory,  type, channel, _device, DeviceId, bufferLength, throughputVsLatency, new lms_stream_meta_t
                {
                    flushPartialPacket = flushPartialPacket,
                    waitForTimestamp = waitForTimestamp,
                },_isThreadSafe, DestroyStream);
                _streamList.Add(strm);
                return (ILmsStream)strm;
            }, cancel);
        }

        public Task EnableChannel(LmsChannel type, uint channel, bool isEnable, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Enable channel {0:G}{1} ={2}", type, channel, isEnable);
                Check(LMS_EnableChannel(_device, type == LmsChannel.Tx, channel, isEnable),nameof(LMS_EnableChannel));
            }, cancel);
        }

        public Task SetBandWidth(LmsChannel type, uint channel, double bandwidth, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Set {0:G}{1} BW={2}", type, channel, bandwidth);
                Check(LMS_SetLPFBW(_device, type == LmsChannel.Tx, channel, bandwidth),nameof(LMS_SetLPFBW));
            }, cancel);
        }

        #region LPF

        public Task SetLPF(LmsChannel type, uint channel, bool enable, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Enable  {0:G}{1} LPF={2}", type, channel, enable);
                Check(LMS_SetLPF(_device, type == LmsChannel.Tx, channel, enable),nameof(LMS_SetLPF));
            }, cancel);
        }

        public Task SetLPFBw(LmsChannel type, uint channel, double bandwidth, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                _logger.Info("Set  {0:G}{1} LPF BW={2}", type, channel, bandwidth);
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
                _logger.Info("Set FPGA register [{0:X2}]={1}", address, value);
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
                _logger.Trace("Set LMS register {0:X2}={1:X2}", address, value);
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
                    _logger.Trace("Set LMS param {0}={1:X2}", (object)param.name, (object)value);    
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

        #region CustomRegister

            public Task WriteCustomRegister(ushort addr, ushort val, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                if (IsDisposed) return;
                Check(LMS_WriteFPGAReg(_device, 21, addr), nameof(LMS_WriteFPGAReg));
                Check(LMS_WriteFPGAReg(_device, 22, val), nameof(LMS_WriteFPGAReg));
            }, cancel);
        }

        public Task<ushort> ReadCustomRegister(ushort addr, CancellationToken cancel)
        {
            return _taskFactory.StartNew(() =>
            {
                unsafe
                {
                    if (IsDisposed) return ushort.MaxValue;
                    Check(LMS_WriteFPGAReg(_device, 21, addr), nameof(LMS_WriteFPGAReg));
                    var buffer = new ushort[1];
                    fixed (ushort* buf = &buffer[0])
                    {
                        Check(LMS_ReadFPGAReg(_device, 22, buf), nameof(LMS_ReadFPGAReg));
                    }
                    return buffer[0];
                }
            }, cancel);

        }

        #endregion



        internal static void Check(int resultCode, string methodName)
        {
            if (resultCode != 0)
            {
                throw new Exception($"Call {methodName} error: {limesdr_strerror()}");
            }
        }
    }
}
