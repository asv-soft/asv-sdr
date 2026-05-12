using System;

namespace Asv.Sdr.Gui;

public static class ModeACGenerator
{
    // Длительности в секундах
    const float p1Duration = 0.8e-6f;
    const float p2Duration = 0.8e-6f;
    const float p3Duration = 0.8e-6f;
    
    const float p1ToP2Gap = 2e-6f - p1Duration; // 1.2 мкс
    
    const float p1ToP3ModeAGap = 8e-6f - p1Duration; // 7.2 мкс
    const float p1ToP3ModeCGap = 21e-6f - p1Duration; // 20.2 мкс
    
    const float p2ToP3ModeAGap = p1ToP3ModeAGap - 2e-6f; // 5.2 мкс
    const float p2ToP3ModeCGap = p1ToP3ModeCGap - 2e-6f; // 18.2 мкс
    
    // const float p3ToEndModeAGap = 1.0f/400.0f - (8e-6f + p3Duration); // Цикл 400Hz
    // const float p3ToEndModeCGap = 1.0f/400.0f - (21e-6f + p3Duration); // Цикл 400Hz

    private static float[] GenerateModeWithP2Query(double sampleRate, /* double freqOffset,*/ float p2ToP3Gap,
        float preZeroSpan, float postZeroSpan, double amplitude = 1.0)
    {
        var p1Samples = (int)Math.Round(sampleRate * p1Duration);
        var p1ToP2Samples = (int)Math.Round(sampleRate * p1ToP2Gap);
        var p2Samples = (int)Math.Round(sampleRate * p2Duration);
        var p2ToP3Samples = (int)Math.Round(sampleRate * p2ToP3Gap);
        var p3Samples = (int)Math.Round(sampleRate * p3Duration);
        var preZeroSamples = (int)Math.Round(sampleRate * preZeroSpan);
        var postZeroSamples = (int)Math.Round(sampleRate * postZeroSpan);
        var totalSamples = p1Samples + p1ToP2Samples + p2Samples + p2ToP3Samples + p3Samples + preZeroSamples + postZeroSamples;
        // var totalSamples = p1Samples + p1ToP2Samples + p2Samples + p2ToP3Samples + p3Samples;

        var iqBuffer = new float[totalSamples * 2];
        var sampleIdx = 0;
        var phase = Math.PI / (Random.Shared.NextDouble() * 3.0 + 3.0); // Рандомная фаза [30;60]
        // double t = 0; // Время
        // var dtInc = 2 * Math.PI * freqOffset / sampleRate;

        // Тишина до P1
        sampleIdx += preZeroSamples * 2;
        
        // P1
        for (var i = 0; i < p1Samples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phase));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phase));
        }

        // Тишина между P1 и P2
        sampleIdx += p1ToP2Samples * 2;
        
        // P2
        for (var i = 0; i < p2Samples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phase));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phase));
        }

        // Тишина между P2 и P3
        sampleIdx += p2ToP3Samples * 2;
        
        // P3
        for (var i = 0; i < p3Samples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phase));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phase));
        }


        return iqBuffer;
    }
    
    private static float[] GenerateModeQuery(double sampleRate, float p1ToP3Gap,
        float preZeroSpan, float postZeroSpan, double amplitude = 1.0)
    {
        var p1Samples = (int)Math.Round(sampleRate * p1Duration);
        var p1ToP3Samples = (int)Math.Round(sampleRate * p1ToP3Gap);
        var p3Samples = (int)Math.Round(sampleRate * p3Duration);
        var preZeroSamples = (int)Math.Round(sampleRate * preZeroSpan);
        var postZeroSamples = (int)Math.Round(sampleRate * postZeroSpan);
        var totalSamples = p1Samples + p1ToP3Samples + p3Samples + preZeroSamples + postZeroSamples;
        // var totalSamples = p1Samples + p1ToP3Samples + p3Samples;

        var iqBuffer = new float[totalSamples * 2];
        var sampleIdx = 0;
        var phase = Math.PI / (Random.Shared.NextDouble() * 3.0 + 3.0); // Рандомная фаза [30;60]
        
        // Тишина до P1
        sampleIdx += preZeroSamples * 2;
        
        // P1
        for (var i = 0; i < p1Samples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phase));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phase));
        }

        // Тишина между P1 и P3
        sampleIdx += p1ToP3Samples * 2;
        
        // P3
        for (var i = 0; i < p3Samples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phase));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phase));
        }

        return iqBuffer;
    }
    
    public static float[] GenerateModeAWithP2Query(double sampleRate, /* double freqOffset,*/ double amplitude = 1.0)
    {
        return GenerateModeWithP2Query(sampleRate, p2ToP3ModeAGap, 0.002f, 0.01f - 8.8e-6f - 0.002f, amplitude);
    }
    
    public static float[] GenerateModeCWithP2Query(double sampleRate, /* double freqOffset,*/ double amplitude = 1.0)
    {
        return GenerateModeWithP2Query(sampleRate, p2ToP3ModeCGap, 0.002f, 0.01f - 21.8e-6f - 0.002f, amplitude);
    }

    public static float[] GenerateModeAQuery(double sampleRate, /* double freqOffset,*/ double amplitude = 1.0)
    {
        return GenerateModeQuery(sampleRate, p1ToP3ModeAGap, 0.002f, 0.01f - 8.8e-6f - 0.002f, amplitude);
    }
    
    public static float[] GenerateModeCQuery(double sampleRate, /* double freqOffset,*/ double amplitude = 1.0)
    {
        return GenerateModeQuery(sampleRate, p1ToP3ModeCGap, 0.002f, 0.01f - 21.8e-6f - 0.002f, amplitude);
    }
    
    public static double[] GenerateModeACResponseCorrelator(double sampleRate, double amplitude = 1.0)
    {
        const double f1Duration = 0.45e-6;
        const double f2Duration = 0.45e-6;
        const double f1ToF2Gap = 20.3e-6f - f1Duration; // 19.85 мкс
        
        var f1Samples = (int)Math.Round(sampleRate * f1Duration);
        var f1ToF2Samples = (int)Math.Round(sampleRate * f1ToF2Gap);
        var f2Samples = (int)Math.Round(sampleRate * f2Duration);
        var totalSamples = f1Samples + f1ToF2Samples + f2Samples;
        

        var iqBuffer = new double[totalSamples];
        var sampleIdx = 0;
        
        // F1
        for (var i = 0; i < f1Samples; i++)
        {
            iqBuffer[sampleIdx++] = amplitude;
        }

        // Тишина между F1 и F2
        sampleIdx += f1ToF2Samples;
        
        // F2
        for (var i = 0; i < f2Samples; i++)
        {
            iqBuffer[sampleIdx++] = amplitude;
        }

        return iqBuffer;
    }
}