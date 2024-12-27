using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace Asv.Sdr.Gui;

public static class AdsbParserFactory
{
    /// <summary>
    /// Represents a collection of default Nmea0183 message types.
    /// </summary>
    public static IEnumerable<Func<AdsbDfMessageBase>> DefaultMessages
    {
        get
        {
            yield return () => new AdsbAircraftIdentification();
            yield return () => new AdsbAirbornePositionWithBaroAlt();
            yield return () => new AdsbAirbornePositionWithGnssAlt();
            yield return () => new AdsbSurfacePosition();
            yield return () => new AdsbGroundSpeed();
            yield return () => new AdsbAirspeed();
            yield return () => new AdsbAircraftOperationStatusV0();
            yield return () => new AdsbAircraftOperationStatusV1();
            yield return () => new AdsbAircraftOperationStatusV2();
        }
    }

    /// <summary>
    /// Registers the default messages to the Nmea0183Parser instance.
    /// </summary>
    /// <param name="src">The Nmea0183Parser instance.</param>
    /// <returns>The Nmea0183Parser instance with the default messages registered.</returns>
    public static AdsbMessageParser RegisterDefaultMessages(this AdsbMessageParser src)
    {
        foreach (var func in DefaultMessages)
        {
            src.Register(func);
        }

        return src;
    }

    /// <summary>
    /// Filters the messages of the given type from the GNSS connection messages.
    /// </summary>
    /// <typeparam name="TMsg">The type of message to filter.</typeparam>
    /// <param name="src">The GNSS connection to filter the messages from.</param>
    /// <returns>An Observable that contains only the messages of type TMsg.</returns>
    public static IObservable<TMsg> Filter<TMsg>(this AdsbMessageParser src)
    {
        return src
            .OnMessage. /*ObserveOn(Scheduler.Default).*/
            Where(_ => _ is TMsg)
            .Cast<TMsg>();
    }
}
