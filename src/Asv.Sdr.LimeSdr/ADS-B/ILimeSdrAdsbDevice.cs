using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;


public interface ILimeSdrAdsbDevice : ILimeSdrDevice
{
    /// <summary>
    /// Gets the ADS-B mode.
    /// </summary>
    Task<bool> AdsbIsEnabled(CancellationToken cancel = default);

    /// <summary>
    /// Sets the ADS-B mode.
    /// </summary>
    Task AdsbSetIsEnabled(bool enabled, CancellationToken cancel = default);

    Task<bool> AdsbDf11ReplyIsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf11BroadcastIsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf4IsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf5IsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf20IsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf21IsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf17IdIsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf17PositionIsEnabled(CancellationToken cancel = default);
    
    Task<bool> AdsbDf17VelocityIsEnabled(CancellationToken cancel = default);

    
    Task SetDf11ReplyIsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf11BroadcastIsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf4IsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf5IsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf20IsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf21IsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf17IdIsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf17PositionIsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf17VelocityIsEnabled(bool enabled, CancellationToken cancel = default);
    
    /// <summary>
    /// Read UF ADS-B message
    /// </summary>
    /// <returns>Last received UF ADS-B message</returns>
    Task<byte[]> ReadAnyUFMessage(CancellationToken cancel = default);

    Task<byte[]> ReadUF4Message(CancellationToken cancel = default);
    
    Task<byte[]> ReadUF5Message(CancellationToken cancel = default);

    public Task<byte[]> ReadUF20Message(CancellationToken cancel = default);

    public Task<byte[]> ReadUF21Message(CancellationToken cancel = default);

    Task<byte[]> ReadUF11Message(CancellationToken cancel = default);

    Task SetAnyUFType(ushort type, CancellationToken cancel);
    
    Task<ushort> GetAnyUFType(CancellationToken cancel);
    
    /// <summary>
    /// Write DF4 ADS-B message
    /// </summary>
    /// <param name="message">DF4 message</param>
    Task WriteDF4Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    
    /// <summary>
    /// Write DF5 ADS-B message
    /// </summary>
    /// <param name="message">DF5 message</param>
    Task WriteDF5Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);

    Task WriteDF20Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    
    Task WriteDF21Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    
    /// <summary>
    /// Write DF11 ADS-B message
    /// </summary>
    /// <param name="message">DF11 message</param>
    Task WriteDF11Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);

#if DEBUG
    Task<byte[]> ReadDF4Message(CancellationToken cancel = default);
    Task<byte[]> ReadDF5Message(CancellationToken cancel = default);
    Task<byte[]> ReadDF20Message(CancellationToken cancel = default);
    Task<byte[]> ReadDF21Message(CancellationToken cancel = default);
    Task<byte[]> ReadDF11Message(CancellationToken cancel = default);
    Task<byte[]> ReadDF17IdMessage(CancellationToken cancel = default);
    Task<(byte[] Even, byte[] Odd)> ReadDF17PositionMessage(CancellationToken cancel = default);
    Task<byte[]> ReadDF17VelocityMessage(CancellationToken cancel = default);
#endif
    

    Task WriteDF17IdMessage(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteDF17PositionMessage(ReadOnlyMemory<byte> evenMessage, ReadOnlyMemory<byte> oddMessage,
        CancellationToken cancel = default);
    Task WriteDF17VelocityMessage(ReadOnlyMemory<byte> message, CancellationToken cancel = default);

    /// <summary>
    /// Gets the PEAK_AMP value, which represents the maximum signal amplitude over the last 100 milliseconds.
    /// The gain should be adjusted to keep PEAK_AMP within the range from 0x10 PEAK_AMP to 0x80
    /// </summary>
    Task<ushort> AdsbGetPeakAmplitude(CancellationToken cancel = default);
    
    /// <summary>
    /// Sets the DF delay reply value.
    /// </summary>
    /// <param name="delayUs">Delay DM reply in micro seconds</param>
    Task SetDfDelay(double delayUs, CancellationToken cancel = default);
    
    /// <summary>
    /// Gets the DF delay reply value.
    /// </summary>
    /// <returns>Delay DM reply in micro seconds</returns>
    Task<double> AdsbGetDfDelay(CancellationToken cancel = default);
    
    /// <summary>
    /// RX UF4/UF20 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[UF4, UF20] cnt</returns>
    Task<byte[]> GetUF4UF20Stat(CancellationToken cancel = default);
    
    /// <summary>
    /// RX UF5/UF21 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[UF5, UF21] cnt</returns>
    Task<byte[]> GetUF5UF21Stat(CancellationToken cancel = default);
    
    /// <summary>
    /// RX UF11/UFxx Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[UF11, UFxx] cnt</returns>
    Task<byte[]> GetUF11AnyUFStat(CancellationToken cancel = default);
    
    /// <summary>
    /// TX DF4/DF5 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[DF4, DF5] cnt</returns>
    Task<byte[]> GetDF4DF5Stat(CancellationToken cancel = default);

    /// <summary>
    /// TX DF20/DF21 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[DF20, DF21] cnt</returns>
    Task<byte[]> GetDF20DF21Stat(CancellationToken cancel = default);
    
    /// <summary>
    /// TX DF11/Reserve Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[DF11, 0x00] cnt</returns>
    Task<byte[]> GetDF11ReserveStat(CancellationToken cancel = default);

    /// <summary>
    /// Update ICAO address to all messages
    /// </summary>
    /// <param name="df11">New df11 message</param>
    /// <param name="df20">New df20 message</param>
    /// <param name="df21">New df21 message</param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    Task UpdateIcaoAddr(ReadOnlySpan<byte> df11, ReadOnlySpan<byte> df20, ReadOnlySpan<byte> df21, CancellationToken cancel = default);

    /// <summary>
    /// Update Squawk to all messages
    /// </summary>
    /// <param name="df5">New df5 message</param>
    /// <param name="df21">New df21 message</param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    Task UpdateSquawk(ReadOnlySpan<byte> df5, ReadOnlySpan<byte> df21, CancellationToken cancel = default);

    /// <summary>
    /// Update All DF messages
    /// </summary>
    /// <param name="df11">New df11 message</param>
    /// <param name="df4">New df4 message</param>
    /// <param name="df5">New df5 message</param>
    /// <param name="df20">New df20 message</param>
    /// <param name="df21">New df21 message</param>
    /// <param name="df17Id">New df17Id message</param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    Task InitAllMessage(ReadOnlySpan<byte> df11, ReadOnlySpan<byte> df4, ReadOnlySpan<byte> df5,
        ReadOnlySpan<byte> df20, ReadOnlySpan<byte> df21, ReadOnlySpan<byte> df17Id, CancellationToken cancel);
    
    public bool IsDisposed { get; }
    
}