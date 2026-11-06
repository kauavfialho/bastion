using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class HomePage : PageBase
    {
        readonly MainWindow main;
        readonly StackPanel details = new();

        // Painel ao vivo
        sealed class Live
        {
            public TextBlock Value = null!, Detail = null!;
            public Spark Spark = null!;
        }
        readonly Live cpu = new(), ram = new(), gpu = new(), net = new();

        public HomePage(MainWindow main) : base("Olá! 👋", "Seu computador agora, ao vivo, e atalhos para as tarefas mais comuns.")
        {
            this.main = main;
            var m = HwMonitor.Shared;

            Body.Children.Add(new UniformGrid
            {
                Columns = 4,
                Margin = new Thickness(0, 0, -14, 0),
                Children =
                {
                    LiveTile(cpu, "", "Processador", m.Cpu, Color.FromRgb(0x7C, 0x5C, 0xFF)),
                    LiveTile(ram, "", "Memória RAM", m.Ram, Color.FromRgb(0x00, 0xC2, 0xFF)),
                    LiveTile(gpu, "", "Placa de vídeo", m.Gpu, Color.FromRgb(0x3D, 0xDC, 0x97)),
                    LiveTile(net, "", "Internet (ping)", m.Ping, Color.FromRgb(0xFF, 0xB5, 0x47), max: null),
                }
            });

            Body.Children.Add(Ui.Card("Ações rápidas",
                Ui.Buttons(
                    Ui.BtnAsync("Liberar RAM", FreeRam, "primary", ""),
                    Ui.Btn("Verificar saúde", () => main.Navigate("Saúde do PC"), icon: ""),
                    Ui.Btn("Liberar espaço", () => main.Navigate("Limpeza"), icon: ""),
                    Ui.Btn("Otimizar para jogos", () => main.Navigate("Jogos"), icon: ""),
                    Ui.Btn("Instalar apps", () => main.Navigate("Instalar apps"), icon: ""),
                    Ui.Btn("Gerenciador de Tarefas", () => Admin.Open("taskmgr.exe"), icon: ""))));

            Body.Children.Add(details);
            m.Updated += UpdateLive;
            m.Start();
        }

        Border LiveTile(Live l, string glyph, string label, Series series, Color color, double? max = 100)
        {
            var sp = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Ui.Icon(glyph, 15, new SolidColorBrush(color)));
            head.Children.Add(Ui.T(label, 13, Ui.Sub, margin: new Thickness(8, 0, 0, 0)));
            sp.Children.Add(head);
            l.Value = Ui.T("—", 26, weight: FontWeights.SemiBold, margin: new Thickness(0, 8, 0, 0));
            l.Detail = Ui.Hint("Lendo…");
            l.Spark = new Spark(series, color, max);
            sp.Children.Add(l.Value);
            sp.Children.Add(l.Detail);
            sp.Children.Add(l.Spark);
            return new Border { Style = (Style)FindResource("Card"), Child = sp, Margin = new Thickness(0, 0, 14, 14) };
        }

        void UpdateLive()
        {
            if (!IsVisible) return;
            var m = HwMonitor.Shared;

            cpu.Value.Text = $"{m.Cpu.Last:0}%";
            cpu.Value.Foreground = Level(m.Cpu.Last, 85, 95);
            cpu.Detail.Text = I18n.Tr("{0} threads", Environment.ProcessorCount);

            ram.Value.Text = $"{m.Ram.Last:0}%";
            ram.Value.Foreground = Level(m.Ram.Last, 80, 90);
            ram.Detail.Text = I18n.Tr("{0} de {1} GB em uso", m.RamUsedGB.ToString("0.0"), m.RamTotalGB.ToString("0"));

            if (m.HasGpu)
            {
                gpu.Value.Text = $"{m.Gpu.Last:0}%";
                var parts = new System.Collections.Generic.List<string>();
                if (m.GpuTemp is double t) parts.Add($"{t:0} °C");
                if (m.GpuPower is double p) parts.Add($"{p:0} W");
                if (m.GpuMemUsed is double u && m.GpuMemTotal is double tot) parts.Add($"VRAM {u / 1024:0.0}/{tot / 1024:0} GB");
                gpu.Detail.Text = string.Join("  ·  ", parts);
                gpu.Value.Foreground = m.GpuTemp > 83 ? Ui.Bad : m.GpuTemp > 76 ? Ui.Warn : Ui.Fg;
            }
            else
            {
                gpu.Value.Text = "—";
                gpu.Detail.Text = I18n.Tr("Leitura ao vivo disponível em placas NVIDIA");
            }

            if (m.PingFailed && m.Ping.Values.Count == 0) { net.Value.Text = I18n.Tr("Sem rede"); net.Value.Foreground = Ui.Bad; }
            else if (m.Ping.Values.Count > 0)
            {
                net.Value.Text = $"{m.Ping.Last:0} ms";
                net.Value.Foreground = Level(m.Ping.Last, 60, 120);
            }
            net.Detail.Text = $"↓ {Speed(m.Down.Last)}   ↑ {Speed(m.NetUpKBs)}";

            cpu.Spark.Refresh(); ram.Spark.Refresh(); gpu.Spark.Refresh(); net.Spark.Refresh();
        }

        static Brush Level(double v, double warn, double bad) => v >= bad ? Ui.Bad : v >= warn ? Ui.Warn : Ui.Fg;

        static string Speed(double kbs) => kbs >= 1024 ? $"{kbs / 1024:0.0} MB/s" : $"{kbs:0} KB/s";

        async Task FreeRam()
        {
            var freed = await Task.Run(MemoryCleaner.Clean);
            Ui.Info(freed > 50L << 20
                ? $"Pronto! {Ui.Size(freed)} de RAM liberados."
                : "A RAM já estava enxuta. Nada relevante para liberar.");
        }

        public override Task LoadAsync() => Busy("Lendo informações do sistema…", async () =>
        {
            var info = await Ps.ObjAsync(@"
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$bb = Get-CimInstance Win32_BaseBoard
$def = $null; try { $def = (Get-MpComputerStatus -ErrorAction Stop).RealTimeProtectionEnabled } catch {}
[pscustomobject]@{
  Os = ""$($os.Caption)"".Replace('Microsoft ','')
  Build = ""$($os.BuildNumber)""
  Pc = $cs.Name
  Cpu = ""$((Get-CimInstance Win32_Processor | Select-Object -First 1).Name)"".Trim()
  Gpu = (@(Get-CimInstance Win32_VideoController | ForEach-Object Name) -join ', ')
  RamGB = [math]::Round($cs.TotalPhysicalMemory/1GB)
  Board = ""$($bb.Manufacturer) $($bb.Product)"".Trim()
  UptimeH = [math]::Round(((Get-Date) - $os.LastBootUpTime).TotalHours, 1)
  Bsod30 = @(Get-WinEvent -FilterHashtable @{LogName='System';ProviderName='Microsoft-Windows-WER-SystemErrorReporting';Id=1001;StartTime=(Get-Date).AddDays(-30)} -ErrorAction SilentlyContinue).Count
  Defender = $def
}");
            details.Children.Clear();

            var bsod = (int)(info.D("Bsod30") ?? 0);
            var def = info.B("Defender");
            details.Children.Add(Ui.Columns(3,
                Ui.Stat("", "Proteção", def == true ? "Ativa" : def == false ? "Desligada" : "Outro antivírus",
                    "Proteção em tempo real", def == false ? Ui.Bad : Ui.Good),
                Ui.Stat("", "Telas azuis (30 dias)", bsod.ToString(),
                    bsod == 0 ? "Nenhuma, tudo estável" : "Veja a causa em Saúde do PC", bsod == 0 ? Ui.Good : Ui.Bad),
                Ui.Stat("", "Ligado há", $"{info.D("UptimeH") ?? 0:0.#} h",
                    "Reiniciar de vez em quando ajuda", (info.D("UptimeH") ?? 0) > 168 ? Ui.Warn : Ui.Accent)));

            details.Children.Add(Ui.Card("Sistema",
                Ui.KV("Windows", $"{info.S("Os")} (build {info.S("Build")})"),
                Ui.KV("Nome do PC", info.S("Pc")),
                Ui.KV("Processador", info.S("Cpu")),
                Ui.KV("Placa de vídeo", info.S("Gpu")),
                Ui.KV("Memória RAM", $"{info.D("RamGB") ?? 0:0} GB"),
                Ui.KV("Placa-mãe", info.S("Board"))));

            var disks = new StackPanel();
            foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            {
                double usedPct = 100.0 * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize;
                var color = usedPct > 90 ? Ui.Bad : usedPct > 75 ? Ui.Warn : null;
                var label = string.IsNullOrEmpty(d.VolumeLabel) ? "Disco local" : d.VolumeLabel;
                disks.Children.Add(Ui.T($"{d.Name.TrimEnd('\\')}  {I18n.Tr(label)}", 14, margin: new Thickness(0, 6, 0, 0)));
                disks.Children.Add(Ui.Bar(usedPct, color));
                disks.Children.Add(Ui.Hint($"{Ui.Size(d.TotalFreeSpace)} livres de {Ui.Size(d.TotalSize)}"));
            }
            details.Children.Add(Ui.Card("Armazenamento", disks));
        });
    }
}
