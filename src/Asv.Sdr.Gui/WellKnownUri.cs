using System;

namespace Asv.Sdr.Gui;

public static class WellKnownUri
{
    /// <summary>
    /// This is Scheme for URI in this application
    /// </summary>
    public const string UriScheme = "asv";

    /// <summary>
    /// This simple non empty URI
    /// </summary>
    public const string Undefined = $"{UriScheme}:null";

    public static readonly Uri UndefinedUri = new(Undefined);

    /// <summary>
    /// This is base URI for all shell controls
    /// </summary>
    public const string Shell = $"{UriScheme}:shell";
    public static Uri ShellUri => new(Shell);
}
