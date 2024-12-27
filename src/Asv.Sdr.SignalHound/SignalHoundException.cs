using System;
using Asv.Sdr.SignalHound;

public class SignalHoundException : Exception
{
    public bbStatus Status { get; }

    public SignalHoundException(bbStatus status)
    {
        Status = status;
    }

    public SignalHoundException(string message, bbStatus status)
        : base(message)
    {
        Status = status;
    }

    public SignalHoundException(string message, Exception inner, bbStatus status)
        : base(message, inner)
    {
        Status = status;
    }
}
