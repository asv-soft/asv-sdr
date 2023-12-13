using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;
using Asv.IO;

namespace Asv.Sdr.Evsg
{
    public interface IEvsgDevice : IDisposable
    {
        IRxValue<bool> IsConnected { get; }
        Task WaitConnected(CancellationToken cancel);
        Task<string> GetHostName(CancellationToken cancel);
        Task<double> GetDdm(CancellationToken cancel);
        IObservable<EvsgIlsStreamData> StartIlsLocStream();
    }


    public class EvsgDevice:DisposableOnceWithCancel,IEvsgDevice
    {
        private readonly TelnetStream _strm;
        private const int DefaultTimeoutMs = 3_000;
        private readonly RxValue<bool> _isConnected = new();
        private readonly IPort _port;

        public EvsgDevice(string connectionString)
        {
            _port = PortFactory.Create(connectionString).DisposeItWith(Disposable);
            _port.Enable();
            _strm = new TelnetStream(_port, Encoding.ASCII).DisposeItWith(Disposable);
            _isConnected.DisposeItWith(Disposable);
            _port.State.Select(_ => _ == PortState.Connected).Subscribe(_isConnected).DisposeItWith(Disposable);
        }

        public IRxValue<bool> IsConnected => _isConnected;

        public async Task WaitConnected(CancellationToken cancel)
        {
            while (_isConnected.Value == false)
            {
                await Task.Delay(100,cancel);
            }
        }

        public Task<string> GetHostName(CancellationToken cancel)
        {
            return _strm.RequestText("GETHOSTNAME", DefaultTimeoutMs, cancel);
        }

        public async Task<double> GetDdm(CancellationToken cancel)
        {
            return double.Parse(await _strm.RequestText("DD0", DefaultTimeoutMs, cancel));
        }

        private async Task StartIlsLocStream(CancellationToken cancel)
        {
            const string successResult = "READY.";
            var result =  await _strm.RequestText("STREAM ALL,1", DefaultTimeoutMs, cancel);
            if (result.Equals(successResult) == false)
                throw new Exception($"Unknown result: want {successResult}. Got {result}");
        }

        public async Task StopStream(CancellationToken cancel)
        {
            const string successResult = "READY.";
            var result = await _strm.RequestText("STOPSTREAM", DefaultTimeoutMs, cancel);
            if (result.Equals(successResult) == false)
                throw new Exception($"Unknown result: want {successResult}. Got {result}");
        }

        public IObservable<EvsgIlsStreamData> StartIlsLocStream()
        {
            return Observable.Create<EvsgIlsStreamData>(observer =>
            {
                StopStream(CancellationToken.None).Wait(DefaultTimeoutMs);
                StartIlsLocStream(CancellationToken.None).Wait(DefaultTimeoutMs);
                return new CompositeDisposable(
                    _strm.Select(_ => new EvsgIlsStreamData(_)).Subscribe(observer),
                    System.Reactive.Disposables.Disposable.Create(() =>StopStream(CancellationToken.None).Wait(DefaultTimeoutMs)));
            });
        }
    }


    public class EvsgIlsStreamData
    {
        private readonly string[] _buffer;

        public EvsgIlsStreamData(string data)
        {
            _buffer = data.Split(",");
        }

        public string RX => _buffer[0];
        public string STIOCPM => _buffer[1];
        public string Index => _buffer[2];
        public string Date => _buffer[3];
        public string Time => _buffer[4];
        public string FREQ_MHz => _buffer[5];
        public string SINGLE_kHz => _buffer[6];
        public string CRS_UF => _buffer[7];
        public string CLR_LF_kHz => _buffer[8];
        public string LEVEL_dBm => _buffer[9];
        public string AM_MOD_90Hz_persent => _buffer[10];
        public string AM_MOD_150Hz_persent => _buffer[11];
        public string FREQ_90_Hz => _buffer[12];
        public string FREQ_150_Hz => _buffer[13];
        public double Ddm90vs150Persent => double.Parse(_buffer[14], CultureInfo.InvariantCulture);
        public double SDMPercent => double.Parse(_buffer[15], CultureInfo.InvariantCulture);
        public string PHI_90_150 => _buffer[16];
        public string VOICE_MOD_persent => _buffer[17];
        public string ID_MOD_persent => _buffer[18];
        public string ID_F_Hz => _buffer[19];
        public string ID_CODE => _buffer[20];
        public string ID_Per_s => _buffer[21];
        public string Last_ID_s => _buffer[22];
        public string DotLen_ms => _buffer[23];
        public string DashLen_ms => _buffer[24];
        public string DotDashGap_ms => _buffer[25];
        public string Lettergap_ms => _buffer[26];
        public string LEV_CLR_LF_dBm => _buffer[27];
        public string LEV_CRS_UF_dBm => _buffer[28];
        public string AM_MOD_CLR_LF_90Hz_persent => _buffer[29];
        public string AM_MOD_CLR_LF_150Hz_persent => _buffer[30];
        public string FREQ_90_CLR_LF_Hz => _buffer[31];
        public string FREQ_150_CLR_LF_Hz => _buffer[32];
        public string DDM_CLR_LF_90_150__1 => _buffer[33];
        public string SDM_CLR_LF_persent => _buffer[34];
        public string PHI_90_150_CLR_LF_ => _buffer[35];
        public string AM_MOD_CRS_UF_90Hz_persent => _buffer[36];
        public string AM_MOD_CRS_UF_150Hz_persent => _buffer[37];
        public string FREQ_90_CRS_UF_Hz => _buffer[38];
        public string FREQ_150_CRS_UF_Hz => _buffer[39];
        public string DDM_CRS_UF_90_150__1 => _buffer[40];
        public string SDM_CRS_UF_persent => _buffer[41];
        public string PHI_90_150_CRS_UF_ => _buffer[42];
        public string PHI_90_90_ => _buffer[43];
        public string PHI_150_150_ => _buffer[44];
        public string ResFM90_Hz => _buffer[45];
        public string ResFM150_Hz => _buffer[46];
        public string K2_90Hz_persent => _buffer[47];
        public string K2_150Hz_persent => _buffer[48];
        public string K3_90Hz_persent => _buffer[49];
        public string K3_150Hz_persent => _buffer[50];
        public string K4_90Hz_persent => _buffer[51];
        public string K4_150Hz_persent => _buffer[52];
        public string THD_90Hz_persent => _buffer[53];
        public string THD_150Hz_persent => _buffer[54];
        public string AM240_persent => _buffer[55];
        public string GPS_lat => _buffer[56];
        public string GPS_long => _buffer[57];
        public string GPS_alt_m => _buffer[58];
        public string GPS_speed_km_h => _buffer[59];
        public string GPS_date => _buffer[60];
        public string GPS_time => _buffer[61];
        public string GPS_Sat => _buffer[62];
        public string GPS_Status => _buffer[63];
        public string GPS_Fix => _buffer[64];
        public string GPS_HDOP => _buffer[65];
        public string GPS_VDOP => _buffer[66];
        public string GPS_Und_m => _buffer[67];
        public string Temp_C => _buffer[68];
        public string MeasTime_ms => _buffer[69];
        public string MeasMode => _buffer[70];
        public string LOC_GP => _buffer[71];
        public string ATTMODE => _buffer[72];
        public string DemodOffset_1F => _buffer[73];
        public string DemodOffset_CRS => _buffer[74];
        public string DemodOffset_CLR => _buffer[75];
        public string Autotune_1F => _buffer[76];
        public string Autotune_CRS => _buffer[77];
        public string Autotune_CLR => _buffer[78];
        public string IFBW_Man_WIDE => _buffer[79];
        public string IFBW_Man_UCLC => _buffer[80];
        public string TrigCounter => _buffer[81];

    }
}