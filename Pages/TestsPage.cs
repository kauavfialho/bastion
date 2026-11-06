using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class TestsPage : PageBase
    {
        string test = "CPU + RAM";
        int minutes = 5;
        string drive = "";
        readonly CheckBox gpuCut = Ui.Switch("Parar se a placa de vídeo passar de 85 °C", "Proteção automática: o teste para sozinho se esquentar demais.", true);
        readonly StackPanel driveRow = new() { Orientation = Orientation.Horizontal };
        readonly StackPanel durationRow = new() { Orientation = Orientation.Horizontal };
        readonly StackPanel live = new();
        readonly StackPanel results = new();
        readonly Button start, stop;

        CancellationTokenSource? cts;
        StressRun? run;
        DispatcherTimer? timer;
        DateTime started;
        double maxGpuTemp, maxCpu;
        string? stopReason;

        public TestsPage() : base("Testes", "Testes de estresse e de velocidade para achar instabilidade, superaquecimento e gargalos.")
        {
            var tests = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (name, hint) in new[]
            {
                ("CPU", "Todos os núcleos no máximo"),
                ("RAM", "Grava e confere a memória: acha pente com defeito"),
                ("CPU + RAM", "O mais completo para estabilidade (XMP, overclock)"),
                ("Disco", "Velocidade real de leitura e gravação"),
            })
            {
                var rb = Tab(name, "tests", name == test, () => { test = name; UpdateOptions(); });
                rb.ToolTip = I18n.Tr(hint);
                tests.Children.Add(rb);
            }
            foreach (var m in new[] { 1, 5, 15, 30 })
                durationRow.Children.Add(Tab($"{m} min", "dur", m == minutes, () => minutes = m));
            var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).ToList();
            drive = drives.FirstOrDefault()?.Name ?? "C:\\";
            foreach (var d in drives)
                driveRow.Children.Add(Tab($"{d.Name.TrimEnd('\\')} {(string.IsNullOrEmpty(d.VolumeLabel) ? "" : d.VolumeLabel)}".Trim(), "drive", d.Name == drive, () => drive = d.Name));

            start = Ui.BtnAsync("Iniciar teste", Start, "primary", "\uE768");
            stop = Ui.Btn("Parar", () => { stopReason = "Parado por você"; cts?.Cancel(); }, "danger", "\uE71A");
            stop.IsEnabled = false;

            Body.Children.Add(Ui.Card("Escolha o teste", tests,
                Ui.T("Duração", 13, Ui.Sub, margin: new Thickness(0, 14, 0, 6)), durationRow,
                Ui.T("Disco", 13, Ui.Sub, margin: new Thickness(0, 14, 0, 6)), driveRow,
                Ui.Separator(), gpuCut,
                Ui.Hint("Temperatura da CPU: precisa de um driver de sensores que ainda não vem com o Bastion. Acompanhe pelo app da placa-mãe ou do cooler durante o teste.", new Thickness(0, 0, 0, 8)),
                Ui.Buttons(start, stop)));
            Body.Children.Add(Ui.Card("Ao vivo", live));
            Body.Children.Add(Ui.Card("Resultados", results));
            live.Children.Add(Ui.Hint("Nenhum teste rodando."));
            results.Children.Add(Ui.Hint("Os resultados aparecem aqui."));
            UpdateOptions();
            HwMonitor.Shared.Start();
        }

        static RadioButton Tab(string text, string group, bool on, Action pick)
        {
            var rb = new RadioButton { Style = (Style)Application.Current.FindResource("Tab"), GroupName = group, Content = I18n.Tr(text), IsChecked = on };
            rb.Checked += (_, _) => pick();
            return rb;
        }

        void UpdateOptions()
        {
            durationRow.IsEnabled = test != "Disco";
            durationRow.Opacity = durationRow.IsEnabled ? 1 : 0.4;
            driveRow.IsEnabled = test == "Disco";
            driveRow.Opacity = driveRow.IsEnabled ? 1 : 0.4;
        }

        async Task Start()
        {
            if (cts != null) return;
            if (test != "Disco" && !Ui.Confirm($"O teste de {test} vai deixar o PC lento por {minutes} min. Feche jogos e programas pesados antes.\n\nComeçar agora?")) return;
            if (test == "Disco")
            {
                var free = new DriveInfo(drive).AvailableFreeSpace;
                if (free < 3L << 30) { Ui.Info("Precisa de pelo menos 3 GB livres nesse disco para o teste."); return; }
            }

            run = new StressRun();
            cts = new CancellationTokenSource();
            stopReason = null;
            maxGpuTemp = 0; maxCpu = 0;
            started = DateTime.Now;
            start.IsEnabled = false;
            stop.IsEnabled = true;
            if (test != "Disco") cts.CancelAfter(TimeSpan.FromMinutes(minutes));

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => Tick();
            timer.Start();
            Tick();

            var jobs = new List<Task>();
            try
            {
                if (test is "CPU" or "CPU + RAM") jobs.Add(Stress.Cpu(run, cts.Token));
                if (test is "RAM" or "CPU + RAM") jobs.Add(Stress.Ram(run, cts.Token));
                if (test == "Disco")
                {
                    var folder = Path.GetPathRoot(Path.GetTempPath())!.Equals(drive, StringComparison.OrdinalIgnoreCase) ? Path.GetTempPath() : drive;
                    long size = Math.Min(2L << 30, new DriveInfo(drive).AvailableFreeSpace / 4) / (4 << 20) * (4 << 20);
                    jobs.Add(Stress.Disk(run, folder, size, cts.Token));
                }
                await Task.WhenAll(jobs);
            }
            catch (Exception ex) { stopReason = "Erro: " + ex.Message; }
            finally
            {
                timer.Stop();
                Tick();
                ShowResult();
                cts.Dispose();
                cts = null;
                start.IsEnabled = true;
                stop.IsEnabled = false;
            }
        }

        void Tick()
        {
            if (run == null) return;
            var m = HwMonitor.Shared;
            var elapsed = DateTime.Now - started;
            maxCpu = Math.Max(maxCpu, m.Cpu.Last);
            if (m.GpuTemp is double gt) maxGpuTemp = Math.Max(maxGpuTemp, gt);

            // Corte automático por temperatura
            if (cts != null && gpuCut.IsChecked == true && m.GpuTemp > 85)
            {
                stopReason = $"Parado por segurança: a placa de vídeo chegou a {m.GpuTemp:0} °C";
                cts.Cancel();
            }

            live.Children.Clear();
            if (test != "Disco")
            {
                var total = TimeSpan.FromMinutes(minutes);
                live.Children.Add(Ui.KV("Tempo", $"{elapsed:mm\\:ss} / {total:mm\\:ss}"));
                live.Children.Add(Ui.Bar(100 * elapsed.TotalSeconds / total.TotalSeconds));
            }
            else live.Children.Add(Ui.KV("Etapa", run.Phase.Length > 0 ? run.Phase : "Preparando"));
            live.Children.Add(Ui.KV("Uso da CPU", $"{m.Cpu.Last:0}%"));
            if (m.HasGpu) live.Children.Add(Ui.KV("Temperatura da GPU", $"{m.GpuTemp:0} °C  (máx {maxGpuTemp:0} °C)", m.GpuTemp > 80 ? Ui.Warn : null));
            if (test is "CPU" or "CPU + RAM")
                live.Children.Add(Ui.KV("Velocidade de cálculo", $"{run.CpuOps / Math.Max(1, elapsed.TotalSeconds) / 1e6:0} milhões de operações/s"));
            if (test is "RAM" or "CPU + RAM")
            {
                live.Children.Add(Ui.KV("Memória em teste", $"{Ui.Size(run.RamAllocated)}  ·  {Ui.Size(run.RamBytesChecked)} conferidos  ·  {run.RamPasses} passada(s)"));
                live.Children.Add(Ui.KV("Erros de memória", run.RamErrors == 0 ? "Nenhum" : run.RamErrors.ToString("N0"), run.RamErrors == 0 ? Ui.Good : Ui.Bad));
            }
        }

        void ShowResult()
        {
            if (run == null) return;
            if (results.Children.Count == 1 && results.Children[0] is TextBlock) results.Children.Clear();
            var secs = Math.Max(1, (DateTime.Now - started).TotalSeconds);
            var lines = new List<UIElement>
            {
                Ui.T($"{test}  ·  {DateTime.Now:HH:mm}  ·  {TimeSpan.FromSeconds(secs):mm\\:ss}", 14.5, weight: FontWeights.SemiBold, margin: new Thickness(0, 8, 0, 4)),
            };
            if (stopReason != null) lines.Add(Ui.KV("Status", stopReason, stopReason.StartsWith("Parado por você") ? Ui.Sub : Ui.Warn));

            if (test is "CPU" or "CPU + RAM")
                lines.Add(Ui.KV("Pontuação da CPU", $"{run.CpuOps / secs / 1e6:0} milhões de operações/s  ·  uso máx {maxCpu:0}%"));
            if (test is "RAM" or "CPU + RAM")
            {
                bool bad = run.RamErrors > 0;
                lines.Add(Ui.KV("Memória", $"{Ui.Size(run.RamBytesChecked)} conferidos em {run.RamPasses} passada(s)"));
                lines.Add(Ui.T(bad
                    ? $"{run.RamErrors:N0} erro(s) de memória: a RAM está com defeito ou instável. Se o XMP estiver ligado, desligue e teste de novo; se continuar, teste um pente por vez."
                    : "Nenhum erro de memória. Para ter certeza absoluta, rode 30 min ou o MemTest86.", 14, bad ? Ui.Bad : Ui.Good, FontWeights.SemiBold, new Thickness(0, 4, 0, 0)));
            }
            if (test == "Disco" && run.DiskWriteMBs > 0)
            {
                lines.Add(Ui.KV("Gravação", $"{run.DiskWriteMBs:0} MB/s"));
                lines.Add(Ui.KV("Leitura", $"{run.DiskReadMBs:0} MB/s"));
                lines.Add(Ui.KV("Leitura aleatória 4K", $"{run.DiskIops:N0} operações/s"));
                var kind = run.DiskReadMBs > 1500 ? "velocidade de SSD NVMe. Ótimo para o Windows e para jogos."
                    : run.DiskReadMBs > 350 ? "velocidade de SSD SATA. Bom para jogos."
                    : run.DiskIops > 3000 ? "SSD mais lento que o normal. Veja se está quase cheio."
                    : "velocidade de HD. Bom para guardar arquivos; jogos e o Windows ficam bem mais rápidos num SSD.";
                lines.Add(Ui.Hint("Esse disco tem " + kind, new Thickness(0, 4, 0, 0)));
            }
            if (maxGpuTemp > 0) lines.Add(Ui.KV("Temperatura máx. da GPU", $"{maxGpuTemp:0} °C", maxGpuTemp > 83 ? Ui.Warn : null));
            lines.Add(Ui.Separator());
            for (int i = lines.Count - 1; i >= 0; i--) results.Children.Insert(0, lines[i]);
            Ui.Info(test == "Disco" ? "Teste de disco concluído." : stopReason ?? "Teste concluído.");
        }
    }
}
