using System;

namespace Asv.Sdr.Gui;

public static class ModeSGenerator
{
    private static bool[] GetDataBits(ReadOnlySpan<byte> data)
    {
        var lenght = data.Length <= 7 ? 7 : 14;
        var result = new bool[lenght * 8];
        for (var i = 0; i < Math.Min(lenght, data.Length); i++)
        {
            byte shift = 0x80;
            for (var j = 0; j < 8; j++)
            {
                result[i * 8 + j] = (data[i] & shift) != 0;
                shift >>= 1;
            }
        }

        return result;
    }
    
    // Генерация запроса Mode S в базовой полосе
    public static float[] GenerateModeSQuery(ReadOnlySpan<byte> data, double sampleRate, /* double freqOffset,*/ double amplitude = 1.0)
    {
        // Длительности в секундах
        const float p1Duration = 0.8e-6f;
        const float p2Duration = 0.8e-6f;
        const float p1ToP2Gap = 2e-6f - p1Duration; // 1.2 мкс
        const float p2ToP6Gap = 1.5e-6f - p2Duration; // 0.7 мкс
        const float p6SyncDuration = 1.25e-6f;
        const float syncToDataGap = 0.5e-6f;
        const float bitDuration = 0.25e-6f; // 4 Мбит/с
        const float guardInterval = 0.5e-6f;

        var dataBits = GetDataBits(data);
        
        // Количество бит в P6
        var bitCount = dataBits.Length;
        var dataDuration = bitCount * bitDuration;
        
        // Общее количество отсчетов
        var p1Samples = (int)Math.Round(sampleRate * p1Duration);
        var p1ToP2Samples = (int)Math.Round(sampleRate * p1ToP2Gap);
        var p2Samples = (int)Math.Round(sampleRate * p2Duration);
        var p2ToP6Samples = (int)Math.Round(sampleRate * p2ToP6Gap);
        var p6SyncSamples = (int)Math.Round(sampleRate * p6SyncDuration);
        var syncToDataSamples = (int)Math.Round(sampleRate * syncToDataGap);
        var dataSamples = (int)Math.Round(sampleRate * dataDuration);
        var guardSamples = (int)Math.Round(sampleRate * guardInterval);

        var totalSamples = p1Samples + p1ToP2Samples + p2Samples + p2ToP6Samples +
                           p6SyncSamples + syncToDataSamples + dataSamples + guardSamples;

        var iqBuffer = new float[totalSamples * 2];
        var sampleIdx = 0;
        var phases = new[] { 0.0, Math.PI }; // Дифференциальные фазы DBPSK
        var phaseIdx = 0;
        // double t = 0; // Время
        // var dtInc = 2 * Math.PI * freqOffset / sampleRate;

        // P1 (немодулированный импульс: фаза 0°)
        for (var i = 0; i < p1Samples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // var iBase = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            // var qBase = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Cos(t) - qBase * (float)Math.Sin(t);
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Sin(t) + qBase * (float)Math.Cos(t);
            // t += dtInc;
        }

        // Тишина между P1 и P2
        sampleIdx += p1ToP2Samples * 2;
        // t += p1ToP2Samples * dtInc;

        // P2 (немодулированный импульс: фаза 0°)
        for (var i = 0; i < p2Samples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // var iBase = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            // var qBase = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Cos(t) - qBase * (float)Math.Sin(t);
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Sin(t) + qBase * (float)Math.Cos(t);
            // t += dtInc;
        }

        // Тишина между P2 и P6
        sampleIdx += p2ToP6Samples * 2;
        // t += p2ToP6Samples * dtInc;

        // P6: Начало (до синхронного опрокидывания, фаза 0°)
        for (var i = 0; i < p6SyncSamples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // var iBase = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            // var qBase = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Cos(t) - qBase * (float)Math.Sin(t);
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Sin(t) + qBase * (float)Math.Cos(t);
            // t += dtInc;
        }

        // Синхронное опрокидывание фазы
        phaseIdx = ++phaseIdx % 2;
        
        // P6: Продолжение (после синхронного опрокидывания, фаза 180°)
        for (var i = 0; i < syncToDataSamples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // var iBase = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            // var qBase = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Cos(t) - qBase * (float)Math.Sin(t);
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Sin(t) + qBase * (float)Math.Cos(t);
            // t += dtInc;
        }

        // Данные (DBPSK)
        var samplesPerBit = (int)Math.Round(sampleRate * bitDuration);
        for (var bit = 0; bit < bitCount; bit++)
        {
            if (dataBits[bit]) phaseIdx = ++phaseIdx % 2;
            for (var i = 0; i < samplesPerBit; i++)
            {
                iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phases[phaseIdx]));
                iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phases[phaseIdx]));
                // var iBase = (float)(amplitude * Math.Cos(phases[phaseIdx]));
                // var qBase = (float)(amplitude * Math.Sin(phases[phaseIdx]));
                // iqBuffer[sampleIdx++] = iBase * (float)Math.Cos(t) - qBase * (float)Math.Sin(t);
                // iqBuffer[sampleIdx++] = iBase * (float)Math.Sin(t) + qBase * (float)Math.Cos(t);
                // t += dtInc;
            }
        }

        // Защитный интервал
        for (var i = 0; i < guardSamples; i++)
        {
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            iqBuffer[sampleIdx++] = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // var iBase = (float)(amplitude * Math.Cos(phases[phaseIdx]));
            // var qBase = (float)(amplitude * Math.Sin(phases[phaseIdx]));
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Cos(t) - qBase * (float)Math.Sin(t);
            // iqBuffer[sampleIdx++] = iBase * (float)Math.Sin(t) + qBase * (float)Math.Cos(t);
            // t += dtInc;
        }

        return iqBuffer;
    }
}