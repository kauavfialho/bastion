using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class HealthPage : PageBase
    {
        public HealthPage() : base("Saúde do PC", "Antivírus, discos, memória e o histórico de travamentos, com a causa provável.") { }

        public override Task LoadAsync() => Busy("Analisando o computador… (pode levar alguns segundos)", async () =>
        {
            var defT = Ps.ObjAsync(@"
$s = $null; try { $s = Get-MpComputerStatus -ErrorAction Stop } catch {}
$av = @(Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct -ErrorAction SilentlyContinue | ForEach-Object displayName)
[pscustomobject]@{
  Has = [bool]$s
  Enabled = $s.AntivirusEnabled; RealTime = $s.RealTimeProtectionEnabled; Tamper = $s.IsTamperProtected
  SigAge = $s.AntivirusSignatureAge
  LastQuick = $(if ($s -and $s.QuickScanEndTime) { $s.QuickScanEndTime.ToString('dd/MM/yyyy HH:mm') } else { '' })
  Threats = @(Get-MpThreatDetection -ErrorAction SilentlyContinue).Count
  Products = ($av -join ', ')
}");
            var diskT = Ps.ListAsync(@"
Get-PhysicalDisk | ForEach-Object {
  $r = $null; try { $r = $_ | Get-StorageReliabilityCounter -ErrorAction Stop } catch {}
  [pscustomobject]@{ Name=$_.FriendlyName; Media=""$($_.MediaType)""; Bus=""$($_.BusType)""; SizeGB=[math]::Round($_.Size/1GB)
    Health=""$($_.HealthStatus)""; Temp=$r.Temperature; Wear=$r.Wear; ReadErr=$r.ReadErrorsUncorrected; WriteErr=$r.WriteErrorsUncorrected; Hours=$r.PowerOnHours }
}");
            var memT = Ps.ObjAsync(@"
$diag = Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Microsoft-Windows-MemoryDiagnostics-Results','Microsoft-Windows-Memory-Diagnostic-Task-Handler'} -MaxEvents 5 -ErrorAction SilentlyContinue
[pscustomobject]@{
  Modules = @(Get-CimInstance Win32_PhysicalMemory | ForEach-Object { [pscustomobject]@{ Slot=$_.DeviceLocator; Part=""$($_.PartNumber)"".Trim(); Maker=""$($_.Manufacturer)"".Trim(); GB=[math]::Round($_.Capacity/1GB); Speed=$_.Speed; Configured=$_.ConfiguredClockSpeed } })
  Diag = @($diag | ForEach-Object { [pscustomobject]@{ When=$_.TimeCreated.ToString('dd/MM/yyyy HH:mm'); Msg=($_.Message -split ""`n"")[0].Trim() } })
}");
            var crashT = Ps.ObjAsync(@"
$since = (Get-Date).AddDays(-90)
function Count($f) { @(Get-WinEvent -FilterHashtable $f -ErrorAction SilentlyContinue).Count }
$bsod = Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Microsoft-Windows-WER-SystemErrorReporting';Id=1001;StartTime=$since} -ErrorAction SilentlyContinue | ForEach-Object {
  [pscustomobject]@{ When=$_.TimeCreated.ToString('dd/MM/yyyy HH:mm'); Text=(""$($_.Properties | ForEach-Object Value) $($_.Message)"") }
}
$apps = Get-WinEvent -FilterHashtable @{LogName='Application';ProviderName='Application Error';Id=1000;StartTime=$since} -ErrorAction SilentlyContinue |
  ForEach-Object { ""$($_.Properties[0].Value)"" } | Group-Object | Sort-Object Count -Descending | Select-Object -First 8 |
  ForEach-Object { [pscustomobject]@{ App=$_.Name; Count=$_.Count } }
[pscustomobject]@{
  Bsod = @($bsod)
  Unexpected = Count @{LogName='System';ProviderName='Microsoft-Windows-Kernel-Power';Id=41;StartTime=$since}
  Whea = Count @{LogName='System';ProviderName='Microsoft-Windows-WHEA-Logger';StartTime=$since}
  Tdr = Count @{LogName='System';ProviderName='Display';Id=4101;StartTime=$since}
  DiskErr = Count @{LogName='System';ProviderName='disk','Ntfs','stornvme','storahci';Level=1,2;StartTime=$since}
  Apps = @($apps)
}");
            await Task.WhenAll(defT, diskT, memT, crashT);
            Body.Children.Clear();

            var crash = crashT.Result;
            var mem = memT.Result;
            Body.Children.Add(Verdict(crash, mem, diskT.Result));
            Body.Children.Add(DefenderCard(defT.Result));
            Body.Children.Add(DisksCard(diskT.Result));
            Body.Children.Add(MemoryCard(mem));
            Body.Children.Add(CrashCard(crash));
        });

        // ---------- Diagnóstico geral ----------

        static readonly Dictionary<int, (string Name, string Cause, bool Ram)> BugChecks = new()
        {
            [0x0A] = ("IRQL_NOT_LESS_OR_EQUAL", "Driver com defeito ou RAM instável", true),
            [0x19] = ("BAD_POOL_HEADER", "Memória corrompida: RAM ou driver", true),
            [0x1A] = ("MEMORY_MANAGEMENT", "Gerenciamento de memória: quase sempre RAM com defeito ou instável", true),
            [0x1E] = ("KMODE_EXCEPTION_NOT_HANDLED", "Driver ou RAM", true),
            [0x3B] = ("SYSTEM_SERVICE_EXCEPTION", "Driver (vídeo, antivírus, anti-cheat) ou RAM", true),
            [0x50] = ("PAGE_FAULT_IN_NONPAGED_AREA", "RAM, driver ou disco", true),
            [0x7A] = ("KERNEL_DATA_INPAGE_ERROR", "Erro lendo o disco: SSD/HD, cabo ou controladora", false),
            [0x7E] = ("SYSTEM_THREAD_EXCEPTION_NOT_HANDLED", "Driver ou RAM", true),
            [0x9F] = ("DRIVER_POWER_STATE_FAILURE", "Driver com problema ao suspender/hibernar", false),
            [0xC2] = ("BAD_POOL_CALLER", "Driver com defeito", false),
            [0xD1] = ("DRIVER_IRQL_NOT_LESS_OR_EQUAL", "Driver (rede, áudio, vídeo)", false),
            [0xEF] = ("CRITICAL_PROCESS_DIED", "Arquivos do Windows ou disco com problema", false),
            [0x101] = ("CLOCK_WATCHDOG_TIMEOUT", "Processador: overclock, BIOS ou energia", false),
            [0x109] = ("CRITICAL_STRUCTURE_CORRUPTION", "RAM ou driver corrompendo o sistema", true),
            [0x116] = ("VIDEO_TDR_FAILURE", "Placa de vídeo ou driver de vídeo", false),
            [0x117] = ("VIDEO_TDR_TIMEOUT_DETECTED", "Placa de vídeo ou driver de vídeo", false),
            [0x124] = ("WHEA_UNCORRECTABLE_ERROR", "Erro de hardware: CPU, overclock, fonte ou temperatura", false),
            [0x133] = ("DPC_WATCHDOG_VIOLATION", "Driver ou firmware do SSD", false),
            [0x139] = ("KERNEL_SECURITY_CHECK_FAILURE", "Driver ou RAM", true),
            [0x154] = ("UNEXPECTED_STORE_EXCEPTION", "SSD/HD com problema", false),
        };

        static int? Code(string text)
        {
            var m = Regex.Match(text, @"0x0*([0-9a-fA-F]{1,8})\b");
            return m.Success ? Convert.ToInt32(m.Groups[1].Value, 16) : null;
        }

        Border Verdict(JsonElement crash, JsonElement mem, List<JsonElement> disks)
        {
            var bsods = crash.A("Bsod").ToList();
            int ramish = bsods.Count(b => Code(b.S("Text")) is int c && BugChecks.TryGetValue(c, out var i) && i.Ram);
            bool memDiag = mem.A("Diag").Any(d => Regex.IsMatch(d.S("Msg"), "defeituos|defect|hardware|erro", RegexOptions.IgnoreCase));
            bool diskBad = disks.Any(d => d.S("Health") is "Warning" or "Unhealthy");
            int whea = (int)(crash.D("Whea") ?? 0);

            string title, text; int level;
            if (memDiag || ramish >= 2)
            {
                (level, title) = (2, "Provável problema na memória RAM");
                text = (memDiag ? "O próprio Windows detectou memória com defeito. " : "") +
                       $"{ramish} tela(s) azul(is) recentes têm códigos típicos de RAM. Teste cada pente separado com o MemTest86 e troque o que falhar.";
            }
            else if (diskBad)
                (level, title, text) = (2, "Um disco está com problema", "O Windows marcou um disco como não saudável. Faça backup dos arquivos importantes o quanto antes.");
            else if (whea > 0)
                (level, title, text) = (1, "Erros de hardware registrados", $"{whea} erro(s) WHEA nos últimos 90 dias. Costuma ser overclock, temperatura alta ou fonte. Desfaça overclocks e confira as temperaturas.");
            else if (bsods.Count > 0)
                (level, title, text) = (1, $"{bsods.Count} tela(s) azul(is) nos últimos 90 dias", "Veja abaixo a causa provável de cada uma.");
            else
                (level, title, text) = (0, "Tudo certo", "Nenhum sinal de problema de hardware ou de estabilidade.");

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Ui.Icon(level == 0 ? "" : "", 28, Ui.Level(level)));
            var txt = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
            txt.Children.Add(Ui.T(title, 18, Ui.Level(level), FontWeights.SemiBold));
            txt.Children.Add(Ui.T(text, 14, Ui.Sub, margin: new Thickness(0, 4, 0, 0)));
            Grid.SetColumn(txt, 1);
            g.Children.Add(txt);
            return Ui.Card(null, g);
        }

        // ---------- Antivírus ----------

        Border DefenderCard(JsonElement d)
        {
            var items = new List<UIElement>();
            if (d.B("Has") == true)
            {
                bool rt = d.B("RealTime") == true;
                items.Add(Ui.KV("Proteção em tempo real", rt ? "Ativa" : "DESLIGADA", rt ? Ui.Good : Ui.Bad));
                items.Add(Ui.KV("Proteção contra adulteração", d.B("Tamper") == true ? "Ativa" : "Desligada", d.B("Tamper") == true ? Ui.Good : Ui.Warn));
                var age = (int)(d.D("SigAge") ?? 0);
                items.Add(Ui.KV("Definições de vírus", age <= 1 ? "Atualizadas" : $"{age} dias atrás", age <= 3 ? Ui.Good : Ui.Warn));
                items.Add(Ui.KV("Última verificação rápida", d.S("LastQuick") is { Length: > 0 } q ? q : "Nunca"));
                var threats = (int)(d.D("Threats") ?? 0);
                items.Add(Ui.KV("Ameaças detectadas", threats == 0 ? "Nenhuma" : threats.ToString(), threats == 0 ? Ui.Good : Ui.Bad));
            }
            else items.Add(Ui.KV("Antivírus", d.S("Products") is { Length: > 0 } p ? p : "Não identificado"));

            items.Add(Ui.Buttons(
                Ui.BtnAsync("Verificação rápida agora", async () =>
                {
                    Status("Verificando com o Windows Defender… isso leva alguns minutos.");
                    await Ps.RunAsync("Start-MpScan -ScanType QuickScan", 1800000);
                    Status("");
                    await LoadAsync();
                }, "primary", ""),
                Ui.Btn("Abrir Segurança do Windows", () => Admin.Open("windowsdefender:"), icon: "")));
            return Ui.Card("Antivírus", items.ToArray());
        }

        // ---------- Discos ----------

        Border DisksCard(List<JsonElement> disks)
        {
            var items = new List<UIElement> { AdminNote("ver temperatura, desgaste e erros de leitura dos discos") };
            foreach (var d in disks)
            {
                var health = d.S("Health");
                var (txt, lvl) = health switch { "Healthy" => ("Saudável", 0), "Warning" => ("Atenção", 1), "Unhealthy" => ("Com defeito", 2), _ => (health, 1) };
                var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                head.Children.Add(Ui.T($"{d.S("Name")}  ·  {d.S("Media")} {d.S("Bus")}  ·  {d.D("SizeGB") ?? 0:0} GB", 14.5, weight: FontWeights.SemiBold));
                head.Children.Add(new Border { Width = 12 });
                head.Children.Add(Ui.Pill(txt, Ui.Level(lvl)));
                items.Add(head);
                if (d.D("Temp") is double t && t > 0) items.Add(Ui.KV("Temperatura", $"{t:0} °C", t > 65 ? Ui.Warn : null));
                if (d.D("Wear") is double w) items.Add(Ui.KV("Desgaste (SSD)", $"{w:0}%", w > 80 ? Ui.Bad : w > 50 ? Ui.Warn : null));
                if (d.D("Hours") is double h) items.Add(Ui.KV("Tempo ligado", $"{h:0} h"));
                var errs = (d.D("ReadErr") ?? 0) + (d.D("WriteErr") ?? 0);
                if (d.D("ReadErr") != null) items.Add(Ui.KV("Erros de leitura/gravação", errs == 0 ? "Nenhum" : errs.ToString("0"), errs == 0 ? Ui.Good : Ui.Bad));
                items.Add(Ui.Separator());
            }
            if (items.Count > 1) items.RemoveAt(items.Count - 1);
            return Ui.Card("Discos (SSD e HD)", items.ToArray());
        }

        // ---------- Memória ----------

        Border MemoryCard(JsonElement mem)
        {
            var items = new List<UIElement>();
            foreach (var m in mem.A("Modules"))
            {
                var part = m.S("Part");
                var configured = (int)(m.D("Configured") ?? m.D("Speed") ?? 0);
                items.Add(Ui.KV($"{m.S("Slot")}", $"{m.D("GB") ?? 0:0} GB  ·  {part}  ·  rodando a {configured} MHz"));
                var rated = Hardware.RatedSpeed(part);
                if (rated > configured + 200 && configured > 0)
                    items.Add(Ui.Hint($"Esse pente é de {rated} MHz mas está a {configured} MHz: o XMP/EXPO está desligado na BIOS. Ligar dá mais desempenho, mas só faça isso se a RAM estiver estável.", new Thickness(0, 0, 0, 8)));
            }
            var diag = mem.A("Diag").ToList();
            items.Add(Ui.KV("Diagnóstico de memória", diag.Count == 0 ? "Nunca executado" : $"{diag[0].S("When")}: {diag[0].S("Msg")}",
                diag.Any(d => Regex.IsMatch(d.S("Msg"), "defeituos|defect|erro", RegexOptions.IgnoreCase)) ? Ui.Bad : null));
            items.Add(Ui.Buttons(
                Ui.Btn("Testar memória (reinicia o PC)", () =>
                {
                    if (Ui.Confirm("O Windows vai agendar o teste de memória. Ele roda ao reiniciar e leva uns 15 minutos.\n\nAbrir agora?"))
                        Admin.Open("mdsched.exe");
                }, icon: ""),
                Ui.Btn("Baixar MemTest86 (mais completo)", () => Admin.Open("https://www.memtest86.com/download.htm"), icon: "")));
            return Ui.Card("Memória RAM", items.ToArray());
        }

        // ---------- Travamentos ----------

        Border CrashCard(JsonElement crash)
        {
            var items = new List<UIElement>
            {
                Ui.KV("Desligamentos inesperados", $"{crash.D("Unexpected") ?? 0:0}", (crash.D("Unexpected") ?? 0) > 3 ? Ui.Warn : null),
                Ui.KV("Erros de hardware (WHEA)", $"{crash.D("Whea") ?? 0:0}", (crash.D("Whea") ?? 0) > 0 ? Ui.Bad : null),
                Ui.KV("Driver de vídeo reiniciado", $"{crash.D("Tdr") ?? 0:0}", (crash.D("Tdr") ?? 0) > 2 ? Ui.Warn : null),
                Ui.KV("Erros de disco", $"{crash.D("DiskErr") ?? 0:0}", (crash.D("DiskErr") ?? 0) > 0 ? Ui.Bad : null),
            };

            var bsods = crash.A("Bsod").ToList();
            items.Add(Ui.T(bsods.Count == 0 ? "Nenhuma tela azul nos últimos 90 dias." : "Telas azuis", 14.5, bsods.Count == 0 ? Ui.Good : Ui.Fg,
                FontWeights.SemiBold, new Thickness(0, 14, 0, 4)));
            foreach (var b in bsods)
            {
                var code = Code(b.S("Text"));
                var info = code is int c && BugChecks.TryGetValue(c, out var i) ? i : ("Código " + (code?.ToString("X") ?? "?"), "Causa não catalogada", false);
                items.Add(Ui.KV(b.S("When"), $"{info.Item1}: {info.Item2}", Ui.Warn));
            }

            var apps = crash.A("Apps").ToList();
            if (apps.Count > 0)
            {
                items.Add(Ui.T("Programas que mais travaram", 14.5, weight: FontWeights.SemiBold, margin: new Thickness(0, 14, 0, 4)));
                foreach (var a in apps) items.Add(Ui.KV(a.S("App"), $"{a.D("Count") ?? 0:0} vez(es)"));
            }
            items.Add(Ui.Hint("Período: últimos 90 dias.", new Thickness(0, 10, 0, 0)));
            return Ui.Card("Travamentos e telas azuis", items.ToArray());
        }
    }
}
