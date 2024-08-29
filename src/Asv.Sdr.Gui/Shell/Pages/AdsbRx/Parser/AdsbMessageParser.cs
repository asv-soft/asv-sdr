using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using Asv.Common;
using Asv.IO;

namespace Asv.Sdr.Gui;

public class AdsbMessageParser : DisposableOnce
{
    private readonly Subject<string> _onMessageRecev = new();
    public IObservable<string> OnMessageRecev => _onMessageRecev;
    private readonly Subject<AdsbParserException> _onErrorSubject = new();
    private readonly Subject<AdsbDfMessageBase> _onMessageSubject = new();

    private readonly Dictionary<ushort, Func<AdsbDfMessageBase>> _factory = new();
    private int _readBytes;
    
    /// <summary>
    /// Number of bytes in the message buffer.
    /// </summary>
    private int _readedBytes;          /* number of bytes in message buffer */

    /// <summary>
    /// The number of bits in the word buffer.
    /// </summary>
    private int _readedBits;           /* number of bits in word buffer     */

    private byte _syncByte;
    private byte _currentByte;
    private byte _stateByte;

    private State _state = State.Preamb1;
    
    private enum State
    {
        /// <summary>
        /// Represents the state where the first preamble is being processed.
        /// </summary>
        Preamb1,

        /// <summary>
        /// Represents the second preamble state of a communication protocol.
        /// </summary>
        Preamb2,

        DFAndAC,
        
        /// <summary>
        /// Represents the possible states of a transmission.
        /// </summary>
        Payload,

        /// <summary>
        /// Represents the Crc1 state of a system.
        /// </summary>
        Crc1,
        
        /// <summary>
        /// Represents the Crc2 state of a system.
        /// </summary>
        Crc2,

        /// <summary>
        /// Represents the Crc3 state of a system.
        /// </summary>
        Crc3
    }
    
    private readonly byte[] _frame = new byte[AdsbHelper.LongFrameLengthBytes];
    private int _msgLen;
    private bool _first;
    private int _firstValue;

    /// <summary>
    /// Registers a factory.
    /// </summary>
    /// <param name="factory">The factory to register.</param>
    public void Register(Func<AdsbDfMessageBase> factory)
    {
        var pkt = factory();
        _factory.Add(pkt.Id, factory);
    }

    /// <summary>
    /// Gets the statistic input bytes.
    /// </summary>
    public int StatisticInputBytes => _readBytes;
    
    /// <summary>
    /// Notifies when a message is received.
    /// </summary>
    /// <param name="message">The received message.</param>
    private void InternalOnMessage(AdsbDfMessageBase message)
    {
        _onMessageSubject.OnNext(message);
    }
    
    /// <summary>
    /// Parses a packet.
    /// </summary>
    /// <param name="id">The ID of the packet.</param>
    /// <param name="data">The data of the packet.</param>
    /// <param name="ignoreReadNotAllData">Optional boolean that defaults to false. If true, does not check if all data was read.</param>
    private void ParsePacket(ushort id, ref ReadOnlySpan<byte> data, bool ignoreReadNotAllData = false)
    {
        if (!_factory.TryGetValue(id, out var factory))
        {
            InternalOnError(new AdsbUnknownMessageException(id.ToString()));
            return;
        }
            
        var message = factory();
        try
        {
            var count = data.Length;
            message.Deserialize(ref data);
            Interlocked.Add(ref _readBytes, count - data.Length);
        }
        catch (Exception e)
        {
            InternalOnError(new AdsbDeserializeMessageException(id.ToString(), e));
            return;
        }
            
        try
        {
            InternalOnMessage(message);
        }
        catch (Exception e)
        {
            InternalOnError(new AdsbPublishMessageException(id.ToString(), e));
        }

        if (!ignoreReadNotAllData && !data.IsEmpty)
        {
            PublishWhenReadNotAllDataWhenDeserializePacket(message.DownlinkFormat.ToString());
        }
    }
    
    private bool TryFormByte(byte mag, out byte result)
    {
        _readedBits++;
        _stateByte |= (byte)(mag << (_readedBits % 2));
        if (_readedBits % 2 == 0)
        {
            if ((_stateByte & 0b11) is 0b00 or 0b11)
            {
                Reset();
                result = 0;
                return false;
            }
            _currentByte <<= 1;
            _currentByte |= (byte)((_stateByte & 0b11) == 0b01 ? 0 : 1);
            _stateByte = 0;
        }

        if (_readedBits == 16)
        {
            result = _currentByte;
            _readedBits = 0;
            _currentByte = 0;
            return true;
        }

        result = 0;
        return false;
    }
    /// <summary>
    /// Reads a bit of data.
    /// </summary>
    /// <param name="mag">The magnitude to read (0 or 1).</param>
    /// <returns>A boolean indicating the success of the read operation.</returns>
    public bool ProcessSample(byte mag)
    {
        switch (_state)
        {
            case State.Preamb1:
                _syncByte <<= 1;
                _syncByte |= (byte)(mag & 0x1);
                if (_syncByte == AdsbHelper.Preamble[0])
                {
                    _frame[_readedBytes] = _syncByte;
                    _syncByte = 0;
                    _state = State.Preamb2;
                    _readedBytes++;
                }
                break;
            case State.Preamb2:
                _syncByte <<= 1;
                _syncByte |= (byte)(mag & 0x1);
                _readedBits++;
                if (_readedBits == 8)
                {
                    if (_syncByte == AdsbHelper.Preamble[1])
                    {
                        _frame[_readedBytes] = _syncByte;
                        _syncByte = 0;
                        _readedBits = 0;
                        _readedBytes++;
                        _state = State.DFAndAC;
                    }
                    else
                    {
                        Reset();
                    }
                }
                break;
            case State.DFAndAC:
                if (TryFormByte(mag, out var dfacByte))
                {
                    _frame[_readedBytes] = dfacByte;
                    _readedBytes++;
                    var df = AdsbHelper.GetDownlinkFormat(new ReadOnlySpan<byte>(_frame)[.._readedBytes]);
                    _msgLen = AdsbHelper.GetMessageLength(df);
                    _state = State.Payload;
                }
                break;
            case State.Payload:
                if (TryFormByte(mag, out var payloadByte))
                {
                    _frame[_readedBytes] = payloadByte;
                    _readedBytes++;
                    if (_readedBytes == _msgLen - 3)
                    {
                        _state = State.Crc1;
                    }
                }
                break;
            case State.Crc1:
                if (TryFormByte(mag, out var crc1Byte))
                {
                    _frame[_readedBytes] = crc1Byte;
                    _readedBytes++;
                    if (_readedBytes == _msgLen - 2)
                    {
                        _state = State.Crc2;
                    }
                }
                break;
            case State.Crc2:
                if (TryFormByte(mag, out var crc2Byte))
                {
                    _frame[_readedBytes] = crc2Byte;
                    _readedBytes++;
                    if (_readedBytes == _msgLen - 1)
                    {
                        _state = State.Crc3;
                    }
                }
                break;
            case State.Crc3:
                if (TryFormByte(mag, out var crc3Byte))
                {
                    _frame[_readedBytes] = crc3Byte;
                    _readedBytes++;
                    if (_readedBytes == _msgLen)
                    {
                        var originalCrc = AdsbHelper.CalcCrc(_frame);
                        var sourceCrc = (uint)(_frame[_msgLen - 3] << 16) | (uint)(_frame[_msgLen - 2] << 8) |
                                        _frame[_msgLen - 1];
                        if (originalCrc == sourceCrc)
                        {
                            var id = AdsbHelper.GetMessageId(_frame);
                            _onMessageRecev.OnNext($"Down link format: {(_frame[2] >> 3) & 0x1F}");
                            var span = new ReadOnlySpan<byte>(_frame, 0, _msgLen);
                            ParsePacket(id, ref span, true);
                            Reset();
                            return true;
                        }
                        PublishWhenCrcError();
                        Reset();
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return false;
    }

    /// <summary>
    /// Resets the parser.
    /// </summary>
    private void Reset()
    {
        _state = State.Preamb1;
        _readedBits = 0;
        _readedBytes = 0;
        _syncByte = 0;
        _currentByte = 0;
        _stateByte = 0;
        _msgLen = 0;
    }

    /// <summary>
    /// Notifies when a CRC error occurs.
    /// </summary>
    protected void PublishWhenCrcError()
    {
        InternalOnError(new AdsbCrcErrorException());
    }

    /// <summary>
    /// Notifies when not all data read when deserializing packet.
    /// </summary>
    /// <param name="messageIdOrName">The message ID or name.</param>
    protected void PublishWhenReadNotAllDataWhenDeserializePacket(string messageIdOrName)
    {
        InternalOnError(new AdsbReadNotAllDataWhenDeserializePacketErrorException(messageIdOrName));
    }

    /// <summary>
    /// Publishes an error to the subject.
    /// </summary>
    /// <param name="ex">The exception to publish.</param>
    protected void InternalOnError(AdsbParserException ex)
    {
        _onErrorSubject.OnNext(ex);
    }

    /// <summary>
    /// Gets an observable of the error subject.
    /// </summary>
    public IObservable<AdsbParserException> OnError => _onErrorSubject;

    /// <summary>
    /// Gets an observable of the message subject.
    /// </summary>
    public IObservable<AdsbDfMessageBase> OnMessage => _onMessageSubject;

    protected override void InternalDisposeOnce()
    {
        _onErrorSubject.OnCompleted();
        _onErrorSubject.Dispose();
            
        _onMessageSubject.OnCompleted();
        _onMessageSubject.Dispose();
        
        _factory.Clear();
    }
}