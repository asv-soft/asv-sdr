using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Asv.Sdr.Msi
{
    public static class LibHelper
    {
        public enum OperatingSystem
        {
            Undefined,
            Windows,
            Linux,
            MacOsX,
        }

        public static void CheckLibraryFiles()
        {
            var os = DetectPlatform();
            switch (os)
            {
                case OperatingSystem.Undefined:
                    break;
                case OperatingSystem.Windows:
                    var dllDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib");
                    Console.WriteLine($"Dll directory: {dllDir}");
                    if (!SetDllDirectory(dllDir))
                    {
                        throw new Win32Exception(
                            $"Error to execute kernel32.dll:SetDllDirectory({dllDir})"
                        );
                    }

                    if (!Directory.Exists(dllDir))
                    {
                        Directory.CreateDirectory(dllDir);
                    }

                    if (Environment.Is64BitOperatingSystem)
                    {
                        CheckFile(Path.Combine(dllDir, "mirsdrapi-rsp.dll"), Libs.x64_mir_sdr_api);
                        return;
                    }

                    CheckFile(Path.Combine(dllDir, "mirsdrapi-rsp.dll"), Libs.x86_mir_sdr_api);
                    break;
                case OperatingSystem.Linux:
                    break;
                case OperatingSystem.MacOsX:
                    break;
                default:
                    throw new Exception("Support only x64 windows platform");
            }
        }

        private static void CheckFile(string path, byte[] data)
        {
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, data);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        private static OperatingSystem DetectPlatform()
        {
            var windir = Environment.GetEnvironmentVariable("windir");
            if (!string.IsNullOrEmpty(windir) && windir.Contains(@"\") && Directory.Exists(windir))
            {
                return OperatingSystem.Windows;
            }

            if (File.Exists(@"/proc/sys/kernel/ostype"))
            {
                var osType = File.ReadAllText(@"/proc/sys/kernel/ostype");
                return osType.StartsWith("Linux", StringComparison.OrdinalIgnoreCase)
                    ? OperatingSystem.Linux
                    : OperatingSystem.Undefined;
            }

            return File.Exists(@"/System/Library/CoreServices/SystemVersion.plist")
                ? OperatingSystem.MacOsX
                : OperatingSystem.Undefined;
        }
    }
}
