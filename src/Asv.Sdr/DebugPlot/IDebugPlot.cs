namespace Asv.Sdr.DebugPlot
{
    public interface IDebugPlot
    {
        
    }

    public class NullDebugPlot : IDebugPlot
    {
        public static IDebugPlot Instance { get; } = new NullDebugPlot();
        
    }
}