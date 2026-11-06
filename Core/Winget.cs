using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bastion.Core
{
    /// <summary>Instalação silenciosa de apps pelo winget (vem com o Windows 10/11).</summary>
    public static class Winget
    {
        static async Task<(int Code, string Output)> Run(string args, int timeoutMs)
        {
            var psi = new ProcessStartInfo("winget", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync();
            if (!await Task.Run(() => p.WaitForExit(timeoutMs)))
            {
                try { p.Kill(true); } catch { }
                return (-1, "Tempo esgotado");
            }
            return (p.ExitCode, await output);
        }

        public static async Task<bool> IsAvailableAsync()
        {
            try { return (await Run("--version", 20000)).Code == 0; }
            catch { return false; }
        }

        /// <summary>IDs dos pacotes já instalados (winget e Microsoft Store).</summary>
        public static async Task<HashSet<string>> InstalledAsync()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var file = Path.Combine(Path.GetTempPath(), "pcmanager-winget-export.json");
            try
            {
                await Run($"export -o \"{file}\" --accept-source-agreements --disable-interactivity", 120000);
                if (!File.Exists(file)) return set;
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
                foreach (var src in doc.RootElement.GetProperty("Sources").EnumerateArray())
                    foreach (var pkg in src.GetProperty("Packages").EnumerateArray())
                        set.Add(pkg.GetProperty("PackageIdentifier").GetString() ?? "");
            }
            catch { }
            finally { try { File.Delete(file); } catch { } }
            return set;
        }

        public static async Task<(bool Ok, string Message)> InstallAsync(string id, string source)
        {
            var (code, output) = await Run(
                $"install --id {id} -e --source {source} --silent --accept-package-agreements --accept-source-agreements --disable-interactivity",
                1800000);
            if (code == 0) return (true, "Instalado");
            if (output.Contains("already installed", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("já está instalado", StringComparison.OrdinalIgnoreCase) ||
                code == unchecked((int)0x8A15002B))
                return (true, "Já estava instalado");
            if (code == unchecked((int)0x8A150011) || output.Contains("cancel", StringComparison.OrdinalIgnoreCase))
                return (false, "Cancelado");
            return (false, $"Falhou (código 0x{code:X8})");
        }
    }

    /// <summary>ArtCraft Launcher, baixado do GitHub oficial (storytold/craft-launcher).</summary>
    public static class ArtCraft
    {
        const string Api = "https://api.github.com/repos/storytold/craft-launcher/releases/latest";

        public static async Task<(bool Ok, string Message)> InstallLauncherAsync()
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Bastion/1.1");

            using var rel = JsonDocument.Parse(await http.GetStringAsync(Api));
            string? msiUrl = null, msiName = null, sumsUrl = null;
            foreach (var a in rel.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                var url = a.GetProperty("browser_download_url").GetString();
                if (name.EndsWith("-windows-x64.msi", StringComparison.OrdinalIgnoreCase)) { msiUrl = url; msiName = name; }
                if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) sumsUrl = url;
            }
            if (msiUrl == null || msiName == null) return (false, "Instalador para Windows não encontrado na versão mais recente.");

            var bytes = await http.GetByteArrayAsync(msiUrl);
            // Confere o arquivo com a lista oficial de SHA256 da mesma versão
            if (sumsUrl != null)
            {
                var sums = await http.GetStringAsync(sumsUrl);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!sums.Contains(hash, StringComparison.OrdinalIgnoreCase))
                    return (false, "O arquivo baixado não bate com o SHA256 oficial. Instalação cancelada por segurança.");
            }

            var dir = Path.Combine(Path.GetTempPath(), "Bastion");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, msiName);
            await File.WriteAllBytesAsync(file, bytes);

            using var p = Process.Start(new ProcessStartInfo("msiexec.exe", $"/i \"{file}\" /passive") { UseShellExecute = true })!;
            await Task.Run(p.WaitForExit);
            return p.ExitCode switch
            {
                0 or 3010 => (true, "Instalado"),
                1602 => (false, "Cancelado"),
                _ => (false, $"Falhou (código {p.ExitCode})"),
            };
        }

        public static bool LauncherInstalled()
        {
            foreach (var root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
                foreach (var path in new[] { @"Software\Microsoft\Windows\CurrentVersion\Uninstall", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                {
                    using var k = root.OpenSubKey(path);
                    if (k == null) continue;
                    foreach (var n in k.GetSubKeyNames())
                    {
                        using var s = k.OpenSubKey(n);
                        if ((s?.GetValue("DisplayName") as string)?.Contains("ArtCraft Launcher", StringComparison.OrdinalIgnoreCase) == true) return true;
                    }
                }
            return false;
        }
    }
}
