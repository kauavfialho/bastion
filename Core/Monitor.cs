using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Bastion.Core
{
    /// <summary>Últimos 60 valores de um sensor, para os mini-gráficos.</summary>
    public sealed class Series
    {
        public const int Size = 60;
        readonly Queue<double> values = new();
        public IReadOnlyCollection<double> Values => values;
        public double Last { get; private set; }

        public void Add(double v)
        {
            Last = v;
            values.Enqueue(v);
            while (values.Count > Size) values.Dequeue();
        }
    }

    /// <summary>Leituras ao vivo de CPU, RAM, GPU (NVIDIA), ping e rede, a cada segundo.</summary>
    public sealed class HwMonitor : IDisposable
    {
        public static HwMonitor Shared { get; } = new();

        public event Action? Updated;

        public readonly Series Cpu = new(), Ram = new(), Gpu = new(), Ping = new(), Down = new();
        public double RamUsedGB, RamTotalGB, NetUpKBs;
        public double? GpuTemp, GpuPower, GpuMemUsed, GpuMemTotal;
        public string GpuName = "";
        public bool HasGpu => gpuSeen;
        public bool PingFailed;

        DispatcherTimer? timer;
        Process? smi;
        volatile bool gpuSeen;
        double gpuUtil;
        long lastIdle, lastKernel, lastUser, lastRx, lastTx;
        DateTime lastNet;
        bool pinging;
        int tick;

        public void Start()
        {
            if (timer != null) return;
            ReadCpu(out lastIdle, out lastKernel, out lastUser);
            (lastRx, lastTx) = NetBytes();
            lastNet = DateTime.UtcNow;
            StartNvidia();
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => Sample();
            timer.Start();
            Sample();
        }

        void Sample()
        {
            // CPU: fração do tempo que não foi ocioso desde a última leitura
            ReadCpu(out var idle, out var kernel, out var user);
            long total = (kernel - lastKernel) + (user - lastUser);
            double cpu = total > 0 ? 100.0 * (1 - (double)(idle - lastIdle) / total) : 0;
            (lastIdle, lastKernel, lastUser) = (idle, kernel, user);
            Cpu.Add(Math.Clamp(cpu, 0, 100));

            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem))
            {
                RamTotalGB = mem.ullTotalPhys / 1073741824.0;
                RamUsedGB = (mem.ullTotalPhys - mem.ullAvailPhys) / 1073741824.0;
                Ram.Add(mem.dwMemoryLoad);
            }

            if (gpuSeen) Gpu.Add(gpuUtil);

            var (rx, tx) = NetBytes();
            var secs = Math.Max(0.2, (DateTime.UtcNow - lastNet).TotalSeconds);
            Down.Add(Math.Max(0, (rx - lastRx) / 1024.0 / secs));
            NetUpKBs = Math.Max(0, (tx - lastTx) / 1024.0 / secs);
            (lastRx, lastTx, lastNet) = (rx, tx, DateTime.UtcNow);

            if (tick++ % 2 == 0) PingOnce();
            Updated?.Invoke();
        }

        async void PingOnce()
        {
            if (pinging) return;
            pinging = true;
            try
            {
                using var p = new System.Net.NetworkInformation.Ping();
                var r = await p.SendPingAsync("1.1.1.1", 1500);
                PingFailed = r.Status != IPStatus.Success;
                if (!PingFailed) Ping.Add(r.RoundtripTime);
            }
            catch { PingFailed = true; }
            finally { pinging = false; }
        }

        static (long Rx, long Tx) NetBytes()
        {
            long rx = 0, tx = 0;
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                    var s = n.GetIPStatistics();
                    rx += s.BytesReceived;
                    tx += s.BytesSent;
                }
            }
            catch { }
            return (rx, tx);
        }

        /// <summary>O nvidia-smi vem com o driver da NVIDIA e manda uma linha por segundo.</summary>
        void StartNvidia()
        {
            var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
            if (!File.Exists(exe)) return;
            try
            {
                smi = Process.Start(new ProcessStartInfo(exe,
                    "--query-gpu=name,utilization.gpu,temperature.gpu,memory.used,memory.total,power.draw --format=csv,noheader,nounits -lms 1000")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                });
                if (smi == null) return;
                smi.OutputDataReceived += (_, e) =>
                {
                    var f = e.Data?.Split(',').Select(x => x.Trim()).ToArray();
                    if (f == null || f.Length < 6) return;
                    static double? N(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
                    GpuName = f[0];
                    gpuUtil = N(f[1]) ?? 0;
                    GpuTemp = N(f[2]);
                    GpuMemUsed = N(f[3]);
                    GpuMemTotal = N(f[4]);
                    GpuPower = N(f[5]);
                    gpuSeen = true;
                };
                smi.BeginOutputReadLine();
            }
            catch { smi = null; }
        }

        public void Dispose()
        {
            timer?.Stop();
            try { if (smi is { HasExited: false }) smi.Kill(); } catch { }
            smi?.Dispose();
        }

        // ---------- Windows ----------

        [DllImport("kernel32.dll")]
        static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        static void ReadCpu(out long idle, out long kernel, out long user)
        {
            if (!GetSystemTimes(out idle, out kernel, out user)) idle = kernel = user = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        public static ulong AvailablePhys()
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref m) ? m.ullAvailPhys : 0;
        }
    }
}
