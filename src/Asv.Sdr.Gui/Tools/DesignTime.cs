using System;
using Avalonia.Controls;

namespace Asv.Sdr.Gui;

public static class DesignTime
{
    public static void ThrowIfNotDesignMode()
    {
        if (Design.IsDesignMode == false)
            throw new InvalidOperationException("This method is for design mode only");
    }

    
}