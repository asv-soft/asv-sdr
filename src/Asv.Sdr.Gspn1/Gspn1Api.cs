using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace Asv.Sdr.Gspn1;

public class Gspn1Api
{
    public const string ApiName = "gspn1";
    private readonly ILogger<Gspn1Api> _logger;

    public GspnModeInfo[] Modes { get; } =
        {
            new GspnModeInfo(1, "Loc", "Курсовой радиомаяк"),
            new GspnModeInfo(2, "GP", "Глиссадный радиомаяк"),
            new GspnModeInfo(3, "VOR", "Всенаправленный азимутальный радиомаяк"),
            new GspnModeInfo(4, "Marker", "Маркерный радиомаяк"),
            new GspnModeInfo(5, "Loc-SP50", "Курсовой радиомаяк СП-50"),
            new GspnModeInfo(6, "Glide-SP50", "Глиссадный радиомаяк СП-50"),
        };

    public Gspn1Api(ILogger<Gspn1Api>? logger)
    {
        _logger = logger ?? NullLogger<Gspn1Api>.Instance;
    }

    public string[] SetMode(string serialPort, int mode)
    {
        ArgumentNullException.ThrowIfNull(serialPort);

        if (Modes.FirstOrDefault(_ => _.Code == mode) == null)
        {
            throw new Exception(
                $"Режим работы с номером {0} не существует. Возможные варианты: \n"
                    + $"{string.Join("\n", Modes.Select(_ => _.ToString()))}"
            );
        }

        return InternalExecuteAndCheckAnswer(serialPort, 0, mode);
    }

    public string[] SetOutputPower(string serialPort, double power)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 1, Math.Round(power, 3));
    }

    public string[] SetFreq(string serialPort, int freq)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 2, freq);
    }

    public string[] SetDacVoltage(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 3, value);
    }

    public string[] SetSn(string serialPort, int sn)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 6, sn);
    }

    public string[] SetCalibDate(string serialPort, int date)
    {
        if (
            !DateTime.TryParseExact(
                $"{date / 1000000:00}.{(date / 10000) % 100:00}.{date % 10000:0000}",
                "dd'.'MM'.'yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed
            )
        )
        {
            throw new ArgumentException($"Не верный формат времени: '{date}'");
        }

        var dateStr = parsed.ToString("dd'.'MM'.'yyyy", CultureInfo.InvariantCulture);

        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 7, dateStr);
    }

    public string[] SetCalibDate(string serialPort, string date)
    {
        if (
            !DateTime.TryParseExact(
                date,
                "dd'.'MM'.'yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed
            )
        )
        {
            throw new ArgumentException($"Не верный формат времени: '{date}'");
        }

        var dateStr = parsed.ToString("dd'.'MM'.'yyyy", CultureInfo.InvariantCulture);
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 7, dateStr);
    }

    public string[] ResetToDefault(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 8, 0);
    }

    public string[] WriteToFram(string serialPort, float data)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 4, data);
    }

    public double[] ReadFromFram(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);

        string[] resString = InternalExecuteAndCheckAnswer(serialPort, 5, 0);
        string[] tmpString = resString[1].Split("   ");
        double[] result = new double[tmpString.Length - 1];
        for (int i = 1; i < tmpString.Length; i++)
        {
            double.TryParse(tmpString[i], out var outRes);
            result.SetValue(outRes, i - 1);
        }

        return result;
    }

    public string[] DisplayTurnOn(string serialPort, int val)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 17, val);
    }

    public string[] ResetDevice(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 18, 0);
    }

    public string[] WriteStartVoltage(string serialPort, int val)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 19, val);
    }

    public string[] WriteStepVoltage(string serialPort, int val)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 20, val);
    }

    public double ReadStartVoltage(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        var s = InternalExecuteAndCheckAnswer(serialPort, 26, 0);
        if (s is null)
        {
            return 0.0;
        }

        double.TryParse(s[1], out var result);
        return result;
    }

    public double ReadStepVoltage(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        var s = InternalExecuteAndCheckAnswer(serialPort, 27, 0);
        if (s == null)
        {
            return 0.0;
        }

        double.TryParse(s[1], out var result);
        return result;
    }

    public string ReadSerialNumber(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        string[] res = InternalExecuteAndCheckAnswer(serialPort, 28, 0);
        return res[1];
    }

    public string ReadCalibrationDate(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        string[] res = InternalExecuteAndCheckAnswer(serialPort, 29, 0);
        return res[1];
    }

    public string[] RfOn(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 9, 1);
    }

    public string[] RfOff(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 9, 0);
    }

    public string[] SetCam90(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 10, value);
    }

    public string[] SetCam150(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 11, value);
    }

    public string[] SetCamId(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 30, value);
    }

    public string[] EnableId(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 16, 1);
    }

    public string[] DisableId(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 16, 0);
    }

    public string[] SetCam30(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 12, value);
    }

    public string[] SetCamFm(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 13, value);
    }

    public string[] SetCam3000(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 14, value);
    }

    public string[] SetPhase(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 15, value);
    }

    public string[] CallOn(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 16, 1);
    }

    public string[] CallOff(string serialPort)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 16, 0);
    }

    public string[] SetDeviation(string serialPort, double value)
    {
        ArgumentNullException.ThrowIfNull(serialPort);
        return InternalExecuteAndCheckAnswer(serialPort, 21, value);
    }

    private string[] InternalExecuteAndCheckAnswer(
        string port,
        int cmd,
        double data,
        Func<string, bool>? validateCmdResult = null,
        Func<string, bool>? validateDataResult = null
    )
    {
        return InternalExecuteAndCheckAnswer(
            port,
            cmd,
            data.ToString(NumberFormatInfo.InvariantInfo),
            validateCmdResult,
            validateDataResult
        );
    }

    private string[] InternalExecuteAndCheckAnswer(
        string port,
        int cmd,
        string data,
        Func<string, bool>? validateCmdResult = null,
        Func<string, bool>? validateDataResult = null
    )
    {
        string dataRes;
        string cmdRes;

        try
        {
            InternalSendCommand(port, cmd, data, out cmdRes, out dataRes);
        }
        catch (TimeoutException ex)
        {
            throw new Exception("Устройство не ответило в нужное время:", ex);
        }

        if (validateCmdResult != null && !validateCmdResult(cmdRes))
        {
            throw new Exception(
                $"Получен неожиданный ответ от устрйоства при передаче команды: '{cmdRes}'."
            );
        }

        if (validateDataResult != null && !validateDataResult(dataRes))
        {
            throw new Exception(
                $"Получен неожиданный ответ от устрйоства при передаче аргументов: '{dataRes}'."
            );
        }

        return new[] { cmdRes, dataRes };
    }

    private void InternalSendCommand(
        string serialPort,
        int cmd,
        string data,
        out string cmdRes,
        out string dataRes
    )
    {
        using var serial = InternalCreateAndStart(serialPort);
        var attempt = 0;
        while (true)
        {
            try
            {
                serial.Open();
                break;
            }
            catch (Exception e)
            {
                attempt++;
                _logger.ZLogWarning(e, $"Couldn't open serial port. Attempt {attempt}");
                if (attempt >= 5)
                {
                    throw;
                }
            }

            Thread.Sleep(500);
        }

        serial.Write("{calib_on}\n");
        try
        {
            Thread.Sleep(100);
            serial.ReadLine(); // читаем -error-, здесь это нормально
        }
        catch (TimeoutException) { }

        serial.Write("{cmd:" + cmd + "}\n");
        while (true)
        {
            try
            {
                Thread.Sleep(100);
                cmdRes = serial.ReadLine();
                break;
            }
            catch
            {
                // ignored
            }
        }

        serial.Write("{data:" + data + "}\n");
        bool lContinue = true;
        dataRes = string.Empty;
        while (lContinue)
        {
            try
            {
                Thread.Sleep(100);
                dataRes = serial.ReadLine();
                var tmpRes = string.Empty;
                while (tmpRes != "-end_message-")
                {
                    try
                    {
                        tmpRes = serial.ReadLine();
                        if (tmpRes != "-end_message-")
                        {
                            dataRes += tmpRes;
                        }
                    }
                    catch (TimeoutException)
                    {
                        tmpRes = "-end_message-";
                    }
                }

                break;
            }
            catch (TimeoutException)
            {
                lContinue = false;
            }
        }
    }

    private SerialPort InternalCreateAndStart(string serialPort)
    {
        return new SerialPort(serialPort, 9600, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000,
        };
    }
}
