using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bastion.Core
{
    public static class Hardware
    {
        /// <summary>Velocidade de fábrica que costuma vir no código do pente (ex.: CMH32GX4M2D3600C18 → 3600).</summary>
        public static int RatedSpeed(string partNumber) =>
            Regex.Matches(partNumber, @"(?<!\d)(2[4-9]\d\d|[3-8]\d{3})(?!\d)").Select(m => int.Parse(m.Value)).DefaultIfEmpty(0).Max();

        [DllImport("kernel32.dll")]
        static extern bool GetFirmwareType(out int type);

        public static bool IsUefi => GetFirmwareType(out var t) && t == 2;

        /// <summary>
        /// Estado real do agendamento de GPU acelerado por hardware, lido do relatório do DirectX.
        /// O registro nem sempre tem a chave: no Windows 11 ele vem ligado de fábrica em placas compatíveis.
        /// </summary>
        public static async Task<(bool Supported, bool Enabled)?> GpuSchedulingAsync()
        {
            var file = Path.Combine(Path.GetTempPath(), "bastion-dxdiag.txt");
            try
            {
                using var p = Process.Start(new ProcessStartInfo("dxdiag.exe", $"/whql:off /t \"{file}\"") { UseShellExecute = false, CreateNoWindow = true })!;
                await Task.Run(() => p.WaitForExit(60000));
                if (!File.Exists(file)) return null;
                var m = Regex.Match(await File.ReadAllTextAsync(file), @"Hardware Scheduling:\s*DriverSupportState:(\w+)\s+Enabled:(\w+)");
                if (!m.Success) return (false, false);
                bool supported = !m.Groups[1].Value.Equals("Unsupported", StringComparison.OrdinalIgnoreCase);
                return (supported, m.Groups[2].Value.Equals("True", StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
            finally { try { File.Delete(file); } catch { } }
        }
    }
}
