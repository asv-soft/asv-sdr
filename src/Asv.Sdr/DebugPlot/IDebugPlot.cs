using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace Asv.Sdr.DebugPlot
{
    public enum AnnotationPosition
    {
        UpperLeft,
        UpperCenter,
        UpperRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        LowerLeft,
        LowerCenter,
        LowerRight,
    }
    public interface IDebugPlot:IDisposable
    {
        bool IsEnabled { get; }
        bool IsPlotEnabled { get; set; }
        void Begin();
        void AddSignal(string name, double[] values);
        void AddHorizontalLine(string name, double value);
        void AddVerticalLine(string name, int xValue);
        void AddMarker(string name, string text, int xValue, double yValue);
        void AddAnnotation(string name, string text,AnnotationPosition position = AnnotationPosition.UpperLeft);
        void End();
        IObservable<Unit> OnTrigger { get; }
        
    }

    

    public class NullDebugPlot : IDebugPlot
    {
        public static IDebugPlot Instance { get; } = new NullDebugPlot();

        
        private readonly Subject<Unit> _onTrigger = new Subject<Unit>();
        public bool IsEnabled { get; } = false;
        public bool IsPlotEnabled { get; set; }

        public void Begin()
        {
            
        }

        public void AddSignal(string name, double[] values)
        {
        }

        public void AddHorizontalLine(string name, double value)
        {
        }

        public void AddVerticalLine(string name, int xValue)
        {
        }

        public void AddMarker(string name, string text, int xValue, double yValue)
        {
        }

        public void AddAnnotation(string name, string text, AnnotationPosition position = AnnotationPosition.UpperLeft)
        {
            
        }

        public void End()
        {
        }

        public IObservable<Unit> OnTrigger => _onTrigger;

        public void Dispose()
        {
            _onTrigger.Dispose();
        }
    }
}