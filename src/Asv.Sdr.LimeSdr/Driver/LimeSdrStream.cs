using System;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using NLog;
using static Asv.Sdr.LimeSdr.NativeMethods;

namespace Asv.Sdr.LimeSdr
{
    public class LmsStream : DisposableOnceWithCancel, ILmsStream
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly TaskFactory _deviceFactory;

        private bool _started;
        private lms_stream_meta_t _meta;
        private readonly TaskFactory _factory;
        private readonly IntPtr _stream;
        private readonly string _name;


        public LmsStream(TaskFactory deviceFactory, LmsChannel type, uint channel, IntPtr device, string id,
            uint bufferLength, float throughputVsLatency, lms_stream_meta_t meta, bool isThreadSafe,
            Func<ILmsStream,Task> destroyStream)
        {
            _deviceFactory = deviceFactory;
            Type = type;
            Channel = channel;
            _name = $"LMS stream {type:G}{channel}_{id}";
            _factory = isThreadSafe ? new TaskFactory(new SingleThreadTaskScheduler(_name, priority: ThreadPriority.Highest).DisposeItWith(Disposable))
                    : Task.Factory;
            LimeSdrDevice.Check(LMS_SetupStream(device, type == LmsChannel.Tx, DataFormat.LMS_FMT_F32, ref _stream, channel, bufferLength, throughputVsLatency),nameof(LMS_SetupStream));
            _meta = meta;
            Disposable.AddAction(() =>
            {
                if (_started) LimeSdrDevice.Check(LMS_StopStream(_stream), nameof(LMS_StopStream));
                LimeSdrDevice.Check(LMS_DestroyStream(device, _stream),nameof(LMS_DestroyStream));
                destroyStream(this).Wait();
            });
        }

        public LmsChannel Type { get; }
        public ulong Channel { get; }
        public bool IsStarted => _started;

        public Task Start(CancellationToken cancel)
        {
            return _deviceFactory.StartNew(() =>
            {
                _logger.Info($"Start {_name}");
                LimeSdrDevice.Check(LMS_StartStream(_stream),nameof(LMS_StartStream));
                _started = true;
            }, cancel);
        }

        public Task Stop(CancellationToken cancel)
        {
            return _deviceFactory.StartNew(() =>
            {
                if (!_started) return;
                _logger.Info($"Stop {_name}");
                LimeSdrDevice.Check(LMS_StopStream(_stream),nameof(LMS_StopStream));
                _started = false;
            }, cancel);
        }

        public Task<LmsStreamStatus> GetStatus(CancellationToken cancel)
        {
            return _factory.StartNew(() =>
            {
                var status = new LmsStreamStatus();
                LimeSdrDevice.Check(LMS_GetStreamStatus(_stream, ref status),nameof(LMS_GetStreamStatus));
                return status;
            }, cancel);
        }

        public unsafe Task<int> Read(Memory<float> iqBuffer, uint timeoutMs = 1000, CancellationToken cancel = default)
        {
            return _factory.StartNew(() =>
            {
                using var pin = iqBuffer.Pin();
                return LMS_RecvStream(_stream, pin.Pointer, (uint)(iqBuffer.Length / 2), ref _meta, timeoutMs);
            }, cancel);
        }
      
        public unsafe Task<int> Write(ReadOnlyMemory<float> iqBuffer, uint timeoutMs = 1000, CancellationToken cancel = default)
        {
            return _factory.StartNew(() =>
            {
                using var pin = iqBuffer.Pin();
                return LMS_SendStream(_stream, pin.Pointer, (uint)(iqBuffer.Length / 2), ref _meta, timeoutMs);
            }, cancel);
        }

        
    }
}
