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


    public GspnModeInfo[] modes { get; } = {
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

    public string[] set_mode(string serialPort, int mode)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));

        if (modes.FirstOrDefault(_ => _.Code == mode) == null)
        {
            throw new Exception($"Режим работы с номером {0} не существует. Возможные варианты: \n" +
                                $"{string.Join("\n", modes.Select(_ => _.ToString()))}");
        }
        return InternalExecuteAndCheckAnswer(serialPort, 0, mode);
    }


    public string[] set_output_power(string serialPort, double power)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 1, Math.Round(power, 3));
    }


    public string[] set_freq(string serialPort, int freq)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 2, freq);
    }


    public string[] set_dac_voltage(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 3, value);
    }


    public string[] set_sn(string serialPort, int sn)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 6, sn);
    }


    public string[] set_calib_date(string serialPort, int date)
    {
        if (!DateTime.TryParseExact($"{date / 1000000:00}.{date / 10000 % 100:00}.{date % 10000:0000}", "dd'.'MM'.'yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new ArgumentException($"Не верный формат времени: '{date}'");

        var dateStr = parsed.ToString("dd'.'MM'.'yyyy", CultureInfo.InvariantCulture);
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 7, dateStr);
    }


    public string[] set_calib_date(string serialPort, string date)
    {
        if (!DateTime.TryParseExact(date, "dd'.'MM'.'yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            throw new ArgumentException($"Не верный формат времени: '{date}'");

        var dateStr = parsed.ToString("dd'.'MM'.'yyyy", CultureInfo.InvariantCulture);
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 7, dateStr);
    }


    public string[] reset_to_default(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 8, 0);
    }


    public string[] write_to_fram(string serialPort, float data)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 4, data);
    }
        

    public double[] read_from_fram(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        string[] resString = InternalExecuteAndCheckAnswer(serialPort, 5, 0);
        string[] tmpString = resString[1].Split("   ");
        double[] result = new double[tmpString.Length-1];
        double outRes;
        for(int i=1; i<tmpString.Length; i++)
        {
            double.TryParse(tmpString[i], out outRes);
            result.SetValue(outRes, i - 1);
        }            
        return result;
    }


    public string[] display_turn_on(string serialPort, int val)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 17, val);
    }


    public string[] reset_device(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 18, 0);
    }


    public string[] write_start_voltage(string serialPort, int val)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 19, val);
    }


    public string[] write_step_voltage(string serialPort, int val)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 20, val);
    }


    public double read_start_voltage(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        var s = InternalExecuteAndCheckAnswer(serialPort, 26, 0);
        if (s == null) return 0.0;
        double result;
        double.TryParse(s[1], out result);
        return result;
    }


    public double read_step_voltage(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        var s = InternalExecuteAndCheckAnswer(serialPort, 27, 0);
        if (s == null) return 0.0;
        double result;
        double.TryParse(s[1], out result);
        return result;
    }


    public string read_serial_number(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        string[] res = InternalExecuteAndCheckAnswer(serialPort, 28, 0);
        return res[1];
    }


    public string read_calibration_date(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));            
        string[] res = InternalExecuteAndCheckAnswer(serialPort, 29, 0);
        return res[1];
    }


    public string[] rf_on(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 9, 1);
    }


    public string[] rf_off(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 9, 0);
    }


    public string[] set_cam_90(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 10, value);
    }


    public string[] set_cam_150(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 11, value);
    }


    public string[] set_cam_id(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 30, value);
    }


    public string[] enable_id(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 16, 1);
    }


    public string[] disable_id(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 16, 0);
    }


    public string[] set_cam_30(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 12, value);
    }


    public string[] set_cam_fm(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 13, value);
    }


    public string[] set_cam_3000(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 14, value);
    }


    public string[] set_phase(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 15, value);
    }


    public string[] call_on(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 16, 1);
    }


    public string[] call_off(string serialPort)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 16, 0);
    }


    public string[] set_deviation(string serialPort, double value)
    {
        if (serialPort == null) throw new ArgumentNullException(nameof(serialPort));
        return InternalExecuteAndCheckAnswer(serialPort, 21, value);
    }

    private string[] InternalExecuteAndCheckAnswer(string port, int cmd, double data,
        Func<string, bool> validateCmdResult = null, Func<string, bool> validateDataResult = null)
    {
        return InternalExecuteAndCheckAnswer(port, cmd, data.ToString(NumberFormatInfo.InvariantInfo), validateCmdResult, validateDataResult);
    }

    private string[] InternalExecuteAndCheckAnswer(string port, int cmd, string data, Func<string, bool> validateCmdResult = null, Func<string, bool> validateDataResult = null)
    {
        string dataRes;
        string cmdRes;

        try
        {
            InternalSendCommand(port, cmd, data, out cmdRes,out dataRes);
        }
        catch (TimeoutException ex)
        {
            throw new Exception("Устройство не ответило в нужное время:", ex);
        }
            
        if (validateCmdResult != null && !validateCmdResult(cmdRes))
            throw new Exception($"Получен неожиданный ответ от устрйоства при передаче команды: '{cmdRes}'.");

        if (validateDataResult != null && !validateDataResult(dataRes))
            throw new Exception($"Получен неожиданный ответ от устрйоства при передаче аргументов: '{dataRes}'.");
        return new[] {cmdRes, dataRes};
    }

    private void InternalSendCommand(string serialPort, int cmd, string data, out string cmdRes,out string dataRes)
    {
        using (var serial = InternalCreateAndStart(serialPort))
        {
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
                    if (attempt>= 5) throw;
                }
                Thread.Sleep(500);
            }
                
                
            serial.Write("{calib_on}\n");
            try
            {
                Thread.Sleep(100);
                // читаем -error-, здесь это нормально
                serial.ReadLine();
            }
            catch (TimeoutException)
            {
                    
            }
                
            serial.Write("{cmd:"+ cmd + "}\n");
            while (true)
            {
                try
                {
                    Thread.Sleep(100);
                    cmdRes = serial.ReadLine();
                    break;
                }
                catch (Exception)
                {
                    // ignored
                }
            }
                
            serial.Write("{data:" + data + "}\n");
            bool l_continue = true;
            dataRes = "";
            while (l_continue)
            {
                try
                {
                    Thread.Sleep(100);                       
                    dataRes = serial.ReadLine();
                    var tmpRes = "";
                    while(tmpRes != "-end_message-")
                    {
                        try
                        {
                            tmpRes = serial.ReadLine();
                            if(tmpRes != "-end_message-")
                                dataRes += tmpRes;                                
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
                    //throw;
                    l_continue = false;
                }
            }                
        }
    }

    private SerialPort InternalCreateAndStart(string serialPort)
    {
        return new SerialPort(serialPort,9600,Parity.None,8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
            }
            ;
    }
}