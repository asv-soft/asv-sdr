using System;

namespace Asv.Sdr.Gui;

/// <summary>
    /// Exception class that represents a generic GNSS parser exception.
    /// </summary>
    [Serializable]
    public class AdsbParserException : Exception
    {
        public AdsbParserException(string message) : base(message)
        {
        }

        public AdsbParserException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Exception class that represents a CRC error in a ADS-B message.
    /// </summary>
    [Serializable]
    public class AdsbCrcErrorException : AdsbParserException
    {
        public AdsbCrcErrorException() : base("Crc error occurred when recv message")
        {
        }
    }

    /// <summary>
    /// Exception class that represents an error where not all data was read when deserializing a ADS-B packet.
    /// </summary>
    [Serializable]
    public class AdsbReadNotAllDataWhenDeserializePacketErrorException : AdsbParserException
    {
        /// <summary>
        /// Gets the message ID associated with the exception.
        /// </summary>
        public string MessageId { get; }

        public AdsbReadNotAllDataWhenDeserializePacketErrorException(string messageId) : base($"Read not all data when deserialize '{messageId}' message")
        {
            MessageId = messageId;
        }
    }

    /// <summary>
    /// Exception class that represents an unknown ADS-B message.
    /// </summary>
    [Serializable]
    public class AdsbUnknownMessageException : AdsbParserException
    {
        /// <summary>
        /// Gets the message ID associated with the exception.
        /// </summary>
        public string MessageId { get; }

        public AdsbUnknownMessageException(string messageId) : base($"Unknown packet message number [MSG={messageId}]")
        {
            MessageId = messageId;
        }
    }

    /// <summary>
    /// Exception class that represents a deserialization error in a ADS-B message.
    /// </summary>
    [Serializable]
    public class AdsbDeserializeMessageException : AdsbParserException
    {
        /// <summary>
        /// Gets the message ID associated with the exception.
        /// </summary>
        public string MessageId { get; }

        public AdsbDeserializeMessageException(string messageId, Exception inner) : base($"Deserialization [ID={messageId}] packet error ", inner)
        {
            MessageId = messageId;
        }
    }

    /// <summary>
    /// Exception class that represents an error in publishing a ADS-B message.
    /// </summary>
    [Serializable]
    public class AdsbPublishMessageException : AdsbParserException
    {
        /// <summary>
        /// Gets the message ID associated with the exception.
        /// </summary>
        public string MessageId { get; }

        public AdsbPublishMessageException(string messageId, Exception inner) : base($"Publication [ID={messageId}] packet throw exception ", inner)
        {
            MessageId = messageId;
        }
    }