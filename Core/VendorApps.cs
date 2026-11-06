using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Bastion.Core
{
    /// <summary>Apps de periféricos (Corsair, Logitech, NZXT, Astro…): o que está instalado, rodando e quanta RAM gasta.</summary>
    public static class VendorApps
    {
        public sealed record App(string Name, string Brand, string InstallRegex, string ProcessRegex, bool ControlsRgb, string Note);

        public static readonly App[] Catalog =
        {
            new("iCUE", "Corsair", @"Corsair iCUE", @"^iCUE$|^iCUE Launcher$", true, "Luzes e fans da Corsair (RAM, coolers, periféricos)."),
            new("G HUB", "Logitech", @"Logitech G HUB", @"^lghub", true, "Mouse e teclado Logitech. DPI e macros ficam salvos no próprio aparelho."),
            new("NZXT CAM", "NZXT", @"NZXT CAM", @"^NZXT CAM$|^cam_helper$", true, "Fans e luzes da NZXT."),
            new("ASTRO Command Center", "Astro", @"ASTRO Command Center", @"(?i)astro.*command|^ACC$", false, "Equalizador do MixAmp/A40/A50. As predefinições ficam salvas no aparelho."),
            new("Razer Synapse", "Razer", @"Razer Synapse", @"^Razer|^RazerAppEngine", true, "Periféricos Razer."),
            new("SteelSeries GG", "SteelSeries", @"SteelSeries GG|SteelSeries Engine", @"^SteelSeries", true, "Periféricos SteelSeries."),
            new("MSI Center / Mystic Light", "MSI", @"MSI Center|Mystic Light", @"^MSI\.CentralServer|^MSI Center|^MysticLight", true, "Luz da placa-mãe MSI."),
            new("Armoury Crate", "ASUS", @"Armoury Crate", @"^ArmouryCrate|^ArmourySocketServer", true, "Luz e ajustes das placas ASUS."),
            new("RGB Fusion / GCC", "Gigabyte", @"RGB Fusion|GIGABYTE Control Center", @"^RGBFusion|^GCC", true, "Luz das placas Gigabyte."),
            new("SignalRGB", "SignalRGB", @"SignalRGB", @"^SignalRgb", true, "Controle de RGB de vários fabricantes."),
            new("Wallpaper Engine", "Wallpaper Engine", @"^$", @"^wallpaper(32|64)$", false, "Papel de parede animado: gasta GPU mesmo em jogo, se não pausar."),
        };

        public sealed record Status(App App, bool Installed, List<Process> Processes, long RamBytes)
        {
            public bool Running => Processes.Count > 0;
        }

        static readonly Lazy<string[]> installedNames = new(() =>
        {
            var names = new List<string>();
            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
                foreach (var path in new[] { @"Software\Microsoft\Windows\CurrentVersion\Uninstall", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                {
                    using var k = root.OpenSubKey(path);
                    if (k == null) continue;
                    foreach (var n in k.GetSubKeyNames())
                    {
                        using var s = k.OpenSubKey(n);
                        if (s?.GetValue("DisplayName") is string dn) names.Add(dn);
                    }
                }
            return names.ToArray();
        });

        public static List<Status> Scan()
        {
            var procs = Process.GetProcesses();
            var list = new List<Status>();
            foreach (var a in Catalog)
            {
                var running = procs.Where(p => Regex.IsMatch(p.ProcessName, a.ProcessRegex, RegexOptions.IgnoreCase)).ToList();
                bool installed = running.Count > 0 || (a.InstallRegex != "^$" && installedNames.Value.Any(n => Regex.IsMatch(n, a.InstallRegex, RegexOptions.IgnoreCase)));
                long ram = 0;
                foreach (var p in running) { try { ram += p.WorkingSet64; } catch { } }
                if (installed) list.Add(new Status(a, installed, running, ram));
            }
            return list;
        }

        /// <summary>Fecha os processos do app (pede para fechar, e força se não fechar). Serviços do sistema não são tocados.</summary>
        public static int Close(Status s)
        {
            int closed = 0;
            foreach (var p in s.Processes)
            {
                try
                {
                    if (p.HasExited) continue;
                    if (!p.CloseMainWindow() || !p.WaitForExit(1500)) p.Kill(true);
                    closed++;
                }
                catch { }
            }
            return closed;
        }

        public static string? LaunchPath(Status s) => s.Processes.Select(p => { try { return p.MainModule?.FileName; } catch { return null; } }).FirstOrDefault(f => f != null);
    }
}
