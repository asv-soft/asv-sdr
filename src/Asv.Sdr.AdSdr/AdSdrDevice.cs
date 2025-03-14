using iio;

namespace Asv.Sdr.AdSdr;

public class AdSdrDevice
{
    #region Static

    static AdSdrDevice()
    {
        if (OperatingSystem.IsWindows())
        {
            CheckFile("libiio.dll", Libs.Libiio_x64);
        }
    }
    
    private static void CheckFile(string path, byte[] data)
    {
        if (!File.Exists(path)) File.WriteAllBytes(path, data);
    }

    public static IEnumerable<KeyValuePair<string,string>> GetAllDevices()
    {
        var ctx = new ScanContext();
        foreach (var context in ctx.get_dns_sd_backend_contexts())
        {
            yield return context;
        }
       
        foreach (var context in ctx.get_usb_backend_contexts())
        {
            yield return context;
        }
    }
    
    #endregion
    
    private readonly Context _context;

    public AdSdrDevice(string uri)
    {
        _context = new Context(uri);
        
        
        
    }
    
}