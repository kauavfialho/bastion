using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class CleanupPage : PageBase
    {
        sealed record Target(string Name, string Hint, bool NeedsAdmin, bool OnByDefault, Func<IEnumerable<(string Path, string Pattern)>> Paths)
        {
            public long Size;
            public CheckBox? Box;
        }

        static string Win => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        static string Local => Folders.LocalAppData;

        static IEnumerable<(string, string)> BrowserCaches()
        {
            var bases = new[]
            {
                Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data"),
                Path.Combine(Local, @"Google\Chrome\User Data"),
                Path.Combine(Local, @"Microsoft\Edge\User Data"),
                Path.Combine(Local, @"Opera Software"),
            };
            foreach (var b in bases.Where(Directory.Exists))
                foreach (var profile in Directory.EnumerateDirectories(b))
                    foreach (var sub in new[] { @"Cache\Cache_Data", "Code Cache", "GPUCache", "Service Worker\\CacheStorage" })
                    {
                        var p = Path.Combine(profile, sub);
                        if (Directory.Exists(p)) yield return (p, "*");
                    }
        }

        readonly List<Target> targets = new()
        {
            new("Arquivos temporários", "Sobras de instalações e programas. Seguro apagar.", false, true,
                () => new[] { (Path.GetTempPath(), "*") }),
            new("Temporários do Windows", "Pasta Temp do sistema.", true, true,
                () => new[] { (Path.Combine(Win, "Temp"), "*") }),
            new("Cache dos navegadores", "Brave, Chrome, Edge e Opera. Feche os navegadores antes. Logins e senhas não são apagados.", false, false,
                BrowserCaches),
            new("Cache de shaders (jogos)", "NVIDIA, AMD e DirectX. É recriado sozinho; os jogos podem engasgar um pouco na primeira vez.", false, false,
                () => new[] { (Path.Combine(Local, @"NVIDIA\DXCache"), "*"), (Path.Combine(Local, @"NVIDIA\GLCache"), "*"),
                              (Path.Combine(Local, @"AMD\DxCache"), "*"), (Path.Combine(Local, @"AMD\DxcCache"), "*"), (Path.Combine(Local, "D3DSCache"), "*") }),
            new("Miniaturas de imagens", "Cache das prévias do Explorador de Arquivos. É recriado sozinho.", false, true,
                () => new[] { (Path.Combine(Local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"), (Path.Combine(Local, @"Microsoft\Windows\Explorer"), "iconcache_*.db") }),
            new("Atualizações do Windows já instaladas", "Arquivos baixados pelo Windows Update.", true, true,
                () => new[] { (Path.Combine(Win, @"SoftwareDistribution\Download"), "*") }),
            new("Relatórios de erro e despejos de memória", "Arquivos de travamentos antigos. Apague só depois de investigar telas azuis.", true, false,
                () => new[] { (Path.Combine(Win, "Minidump"), "*"), (Path.Combine(Win, "MEMORY.DMP"), "*"), (Path.Combine(Local, "CrashDumps"), "*"),
                              (@"C:\ProgramData\Microsoft\Windows\WER\ReportArchive", "*"), (@"C:\ProgramData\Microsoft\Windows\WER\ReportQueue", "*") }),
        };

        readonly StackPanel list = new();
        readonly TextBlock total = Ui.T("", 15, weight: FontWeights.SemiBold);
        readonly StackPanel bigFolders = new();
        readonly TextBlock recycleText = Ui.T("", 14);

        public CleanupPage() : base("Limpeza", "Libere espaço apagando caches e temporários, e descubra o que está ocupando o SSD.")
        {
            Body.Children.Add(Ui.Card("Arquivos desnecessários",
                AdminNote("limpar também os itens do sistema"),
                list,
                Ui.Separator(),
                total,
                Ui.Buttons(
                    Ui.BtnAsync("Limpar selecionados", Clean, "primary", "\uE74D"),
                    Ui.BtnAsync("Analisar de novo", LoadAsync, icon: "\uE72C"),
                    Ui.Btn("Limpeza de Disco do Windows", () => Admin.Open("cleanmgr.exe"), icon: "\uEDA2"))));

            Body.Children.Add(Ui.Card("Lixeira", recycleText,
                Ui.Buttons(Ui.BtnAsync("Esvaziar Lixeira", EmptyRecycleBin, "danger", "\uE74D"))));

            Body.Children.Add(Ui.Card("O que mais ocupa o disco do sistema",
                Ui.Hint("Analisa as maiores pastas do disco C:. Pode levar um minuto."),
                bigFolders,
                Ui.Buttons(Ui.BtnAsync("Analisar pastas grandes", AnalyzeBig, icon: "\uE721"))));
        }

        public override Task LoadAsync() => Busy("Calculando o espaço que pode ser liberado…", async () =>
        {
            await Task.Run(() =>
            {
                foreach (var t in targets) t.Size = t.Paths().Sum(p => Folders.SizeOf(p.Path, p.Pattern));
            });
            list.Children.Clear();
            foreach (var t in targets)
            {
                bool allowed = !t.NeedsAdmin || Admin.IsAdmin;
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                t.Box = Ui.Check(t.Name + (allowed ? "" : "  (precisa de admin)"), t.Hint, allowed && t.OnByDefault && t.Size > 0);
                t.Box.IsEnabled = t.Size > 0;
                if (!allowed && t.Size > 0) Ui.NeedsAdmin(t.Box, "limpar os arquivos do sistema");
                t.Box.Checked += (_, _) => UpdateTotal();
                t.Box.Unchecked += (_, _) => UpdateTotal();
                row.Children.Add(t.Box);
                var size = Ui.T(Ui.Size(t.Size), 14, t.Size > 1L << 30 ? Ui.Warn : Ui.Sub, FontWeights.SemiBold);
                Grid.SetColumn(size, 1);
                row.Children.Add(size);
                list.Children.Add(row);
            }
            UpdateTotal();
            UpdateRecycle();
        });

        void UpdateTotal()
        {
            var sel = targets.Where(t => t.Box?.IsChecked == true).Sum(t => t.Size);
            total.Text = $"Selecionado: {Ui.Size(sel)}";
        }

        async Task Clean()
        {
            var sel = targets.Where(t => t.Box?.IsChecked == true).ToList();
            if (sel.Count == 0) { Ui.Info("Selecione pelo menos um item."); return; }
            if (!Ui.Confirm($"Apagar {Ui.Size(sel.Sum(t => t.Size))} de arquivos temporários e caches?\n\nEsses arquivos não vão para a Lixeira. Arquivos em uso são pulados.")) return;
            long freed = 0;
            await Busy("Limpando…", async () =>
            {
                freed = await Task.Run(() => sel.Sum(t => t.Paths().Sum(p => Folders.DeleteContents(p.Path, p.Pattern))));
            });
            Ui.Info($"Pronto! {Ui.Size(freed)} liberados.");
            await LoadAsync();
        }

        // ---------- Lixeira ----------

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHQueryRecycleBin(string? root, ref SHQUERYRBINFO info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);

        void UpdateRecycle()
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            SHQueryRecycleBin(null, ref info);
            recycleText.Text = info.i64NumItems == 0 ? "A Lixeira está vazia." : $"{info.i64NumItems} item(ns) ocupando {Ui.Size(info.i64Size)}.";
        }

        async Task EmptyRecycleBin()
        {
            if (!Ui.Confirm("Esvaziar a Lixeira? Os arquivos serão apagados de vez.")) return;
            await Task.Run(() => SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4)); // sem confirmação, sem barra, sem som
            UpdateRecycle();
        }

        // ---------- Pastas grandes ----------

        async Task AnalyzeBig()
        {
            List<(string Path, long Size)> result = new();
            await Busy("Medindo pastas… isso pode levar um minuto.", async () =>
            {
                result = await Task.Run(() =>
                {
                    var sys = Path.GetPathRoot(Win)!;
                    var skip = new[] { "Users", "Windows", "$Recycle.Bin", "System Volume Information", "Recovery" };
                    var dirs = new List<string>();
                    void AddChildren(string p, params string[] except)
                    {
                        try
                        {
                            dirs.AddRange(Directory.EnumerateDirectories(p).Where(d => !except.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)));
                        }
                        catch { }
                    }
                    AddChildren(sys, skip.Concat(new[] { "Program Files", "Program Files (x86)", "ProgramData" }).ToArray());
                    AddChildren(Path.Combine(sys, "Program Files"));
                    AddChildren(Path.Combine(sys, "Program Files (x86)"));
                    AddChildren(Path.Combine(sys, "ProgramData"));
                    AddChildren(Folders.UserProfile, "AppData");
                    AddChildren(Folders.LocalAppData);
                    AddChildren(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                    dirs.Add(Win);
                    return dirs.AsParallel().WithDegreeOfParallelism(4)
                        .Select(d => (d, Folders.SizeOf(d)))
                        .Where(x => x.Item2 > 300L << 20)
                        .OrderByDescending(x => x.Item2).Take(15).ToList();
                });
            });

            bigFolders.Children.Clear();
            if (result.Count == 0) { bigFolders.Children.Add(Ui.Hint("Nenhuma pasta grande encontrada.")); return; }
            var max = result[0].Size;
            foreach (var (path, size) in result)
            {
                var row = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel();
                left.Children.Add(Ui.T(path, 13.5));
                left.Children.Add(Ui.Bar(100.0 * size / max, size > 20L << 30 ? Ui.Warn : null));
                row.Children.Add(left);
                var s = Ui.T(Ui.Size(size), 13.5, Ui.Sub, FontWeights.SemiBold);
                s.HorizontalAlignment = HorizontalAlignment.Right;
                s.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(s, 1);
                row.Children.Add(s);
                var open = Ui.Btn("Abrir", () => Admin.Open(path));
                open.Margin = new Thickness(12, 0, 0, 0);
                Grid.SetColumn(open, 2);
                row.Children.Add(open);
                bigFolders.Children.Add(row);
            }
            bigFolders.Children.Add(Ui.Hint("Jogos: mova pelo próprio launcher (Steam, Epic, Riot). Copiar a pasta na mão costuma quebrar o jogo.", new Thickness(0, 12, 0, 0)));
        }
    }
}
