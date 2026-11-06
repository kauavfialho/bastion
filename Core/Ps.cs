using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bastion.Core
{
    /// <summary>Roda scripts do Windows PowerShell e devolve texto ou JSON.</summary>
    public static class Ps
    {
        public static async Task<string> RunAsync(string script, int timeoutMs = 180000)
        {
            var full = "$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Continue'; " +
                       "[Console]::OutputEncoding=[Text.Encoding]::UTF8\n" + script;
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            var exited = await Task.Run(() => p.WaitForExit(timeoutMs));
            if (!exited)
            {
                try { p.Kill(true); } catch { }
                throw new TimeoutException("O comando demorou demais e foi cancelado.");
            }
            var output = (await stdout).Trim();
            var err = (await stderr).Trim();
            if (output.Length == 0 && p.ExitCode != 0 && err.Length > 0)
                throw new Exception(FirstLine(err));
            return output;
        }

        /// <summary>Resultado do script como um objeto JSON.</summary>
        public static async Task<JsonElement> ObjAsync(string script, int timeoutMs = 180000)
        {
            var txt = await RunAsync($"ConvertTo-Json -Depth 6 -Compress -InputObject (& {{\n{script}\n}})", timeoutMs);
            return Parse(txt);
        }

        /// <summary>Resultado do script sempre como lista JSON.</summary>
        public static async Task<List<JsonElement>> ListAsync(string script, int timeoutMs = 180000)
        {
            var txt = await RunAsync($"ConvertTo-Json -Depth 6 -Compress -InputObject @(& {{\n{script}\n}})", timeoutMs);
            var list = new List<JsonElement>();
            var root = Parse(txt);
            if (root.ValueKind == JsonValueKind.Array) foreach (var e in root.EnumerateArray()) list.Add(e);
            else if (root.ValueKind == JsonValueKind.Object) list.Add(root);
            return list;
        }

        static JsonElement Parse(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return default;
            // Ignora qualquer aviso impresso antes do JSON
            int i = txt.IndexOfAny(new[] { '{', '[' });
            if (i > 0) txt = txt.Substring(i);
            using var doc = JsonDocument.Parse(txt);
            return doc.RootElement.Clone();
        }

        static string FirstLine(string s)
        {
            var i = s.IndexOf('\n');
            return (i < 0 ? s : s.Substring(0, i)).Trim();
        }

        /// <summary>Escapa texto para usar entre aspas simples no PowerShell.</summary>
        public static string Q(string s) => "'" + s.Replace("'", "''") + "'";
    }

    public static class J
    {
        public static string S(this JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return "";
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                _ => v.ToString(),
            };
        }

        public static double? D(this JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            return null;
        }

        public static bool? B(this JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
        }

        public static IEnumerable<JsonElement> A(this JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) yield break;
            if (v.ValueKind == JsonValueKind.Array) { foreach (var x in v.EnumerateArray()) yield return x; }
            else if (v.ValueKind == JsonValueKind.Object) yield return v;
        }
    }
}
