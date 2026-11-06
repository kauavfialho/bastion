using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class FormatPage : PageBase
    {
        readonly MainWindow main;
        readonly TextBlock activation = Ui.Hint("Verificando ativação…");
        readonly StackPanel bios = new() { Children = { Ui.T("Lendo a placa-mãe e a BIOS…", 14, Ui.Sub) } };
        readonly Border biosCard;
        BiosGuide.Report? report;

        public FormatPage(MainWindow main) : base("Formatar PC", "Plano completo: o que fazer antes, como formatar, o que fazer depois e os ajustes da BIOS para o PC render o máximo.")
        {
            this.main = main;

            // ---- Plano pré-formatação ----
            Body.Children.Add(Plan("1. Plano pré-formatação", "pre", new (string, string, string, Button?)[]
            {
                ("backup", "Backup dos seus arquivos", "Área de Trabalho, Documentos, Imagens, Vídeos e Downloads copiados para outro disco, pendrive ou nuvem.", null),
                ("browser", "Navegador sincronizado", "Entre na sua conta no navegador para não perder favoritos e senhas.", null),
                ("saves", "Saves de jogos", "Jogos sem nuvem guardam em Documentos\\My Games, Saved Games e AppData. Copie essas pastas.", Ui.Btn("Abrir Saved Games", () => Admin.Open(Path.Combine(Folders.UserProfile, "Saved Games")))),
                ("keys", "Chaves e logins anotados", "Programas pagos, Wi-Fi e contas que você não lembra a senha.", null),
                ("programs", "Lista de programas instalados", "Salva na Área de Trabalho para você saber o que reinstalar.", Ui.BtnAsync("Salvar lista", SaveProgramList)),
                ("drivers", "Backup dos drivers", "Garante a internet depois de formatar, mesmo se o driver de rede não vier no Windows.", AdminButton(Ui.BtnAsync("Fazer backup", BackupDrivers), "fazer backup dos drivers")),
                ("activation", "Windows ativado", "Com licença digital, o Windows ativa sozinho depois de formatar, se usar a mesma edição (Home/Pro).", null),
                ("bios", "BIOS atualizada", "Atualize ANTES de formatar: é mais seguro e o Windows novo já instala em cima da BIOS certa.", Ui.Btn("Ver ajustes da BIOS", () => biosCard!.BringIntoView())),
                ("usb", "Pendrive do Windows 11", "Pendrive de 8 GB ou mais gravado com a ferramenta oficial da Microsoft (apaga tudo do pendrive).", Ui.Btn("Baixar ferramenta", () => Admin.Open("https://www.microsoft.com/software-download/windows11"))),
                ("guide", "Guia da BIOS no celular", "Imagem com os ajustes da SUA placa para seguir enquanto mexe na BIOS (a BIOS não abre arquivos).", Ui.BtnAsync("Gerar guia", GenerateGuide)),
            }, activation));

            // ---- Formatar ----
            Body.Children.Add(Ui.Card("2. Formatar",
                Ui.T("Opção A: Restaurar o PC (mais fácil, sem pendrive)", 14.5, weight: FontWeights.SemiBold),
                Ui.Hint("Configurações → Sistema → Recuperação → Restaurar o PC → \"Remover tudo\" → \"Download na nuvem\". Reinstala um Windows limpo direto da Microsoft."),
                Ui.Buttons(Ui.Btn("Abrir Recuperação", () => Admin.Open("ms-settings:recovery"), icon: "")),
                Ui.T("Opção B: Instalação limpa com pendrive (mais completa)", 14.5, weight: FontWeights.SemiBold, margin: new Thickness(0, 10, 0, 0)),
                Ui.Hint("Com o pendrive conectado, ligue o PC apertando a tecla do menu de boot (veja nos ajustes da BIOS abaixo) e escolha \"UEFI: pendrive\". " +
                        "Na instalação, apague só as partições do disco do Windows e instale no espaço livre. O Windows ativa sozinho quando conectar à internet."),
                Ui.Buttons(Ui.Btn("Baixar Windows 11 (site da Microsoft)", () => Admin.Open("https://www.microsoft.com/software-download/windows11"), icon: ""))));

            // ---- Plano pós-formatação ----
            Body.Children.Add(Plan("3. Plano pós-formatação", "pos", new (string, string, string, Button?)[]
            {
                ("update", "Windows Update até o fim", "Rode até não ter mais nada, incluindo as atualizações opcionais de drivers.", Ui.Btn("Windows Update", () => Admin.Open("ms-settings:windowsupdate"))),
                ("chipset", "Driver de chipset", "AMD: pacote \"AMD Chipset Software\". Intel: Driver & Support Assistant. Melhora energia e desempenho.", Ui.Btn("Ver em Drivers", () => main.Navigate("Drivers"))),
                ("gpu", "Driver de vídeo", "NVIDIA App ou AMD Adrenalin, a versão mais nova.", Ui.Btn("Ver em Drivers", () => main.Navigate("Drivers"))),
                ("bios2", "Ajustes da BIOS", "XMP/EXPO, Resizable BAR e curva dos fans (a atualização de BIOS zera tudo isso).", Ui.Btn("Ver ajustes", () => biosCard!.BringIntoView())),
                ("debloat", "Remover bloatware", "Apps de fábrica, anúncios e rastreamento.", Ui.Btn("Ir para Debloat", () => main.Navigate("Debloat"))),
                ("apps", "Instalar seus programas", "Launchers de jogos, navegador e o resto de uma vez.", Ui.Btn("Ir para Instalar apps", () => main.Navigate("Instalar apps"))),
                ("games", "Otimizar para jogos", "Plano de energia, Modo de Jogo e Game Bar.", Ui.Btn("Ir para Jogos", () => main.Navigate("Jogos"))),
                ("startup", "Limpar a inicialização", "Deixe abrir com o Windows só o necessário.", Ui.Btn("Ir para Inicialização", () => main.Navigate("Inicialização"))),
                ("organizer", "Ligar o organizador", "Imagens e vídeos vão para as pastas certas sozinhos.", Ui.Btn("Ir para Organizador", () => main.Navigate("Organizador"))),
                ("test", "Teste de estabilidade", "Depois do XMP, rode CPU + RAM por 15 min. Zero erros = tudo pronto.", Ui.Btn("Ir para Testes", () => main.Navigate("Testes"))),
            }, null));

            // ---- BIOS ----
            biosCard = Ui.Card("4. Ajustes da BIOS", bios);
            Body.Children.Add(biosCard);
        }

        static Button AdminButton(Button b, string what) { Ui.NeedsAdmin(b, what); return b; }

        /// <summary>Lista de tarefas com caixas que ficam salvas e barra de progresso.</summary>
        Border Plan(string title, string key, (string Id, string Title, string Hint, Button? Action)[] items, UIElement? extra)
        {
            var progressText = Ui.T("", 13.5, Ui.Sub);
            var bar = Ui.Bar(0);
            var list = new StackPanel();
            var boxes = new List<CheckBox>();
            void Update()
            {
                int done = boxes.Count(b => b.IsChecked == true);
                progressText.Text = done == boxes.Count ? I18n.Tr("Tudo pronto! {0} de {1}", done, boxes.Count) : I18n.Tr("{0} de {1} feitos", done, boxes.Count);
                progressText.Foreground = done == boxes.Count ? Ui.Good : Ui.Sub;
                bar.Value = 100.0 * done / boxes.Count;
            }
            foreach (var it in items)
            {
                var id = key + "." + it.Id;
                var content = new StackPanel();
                content.Children.Add(Ui.T(it.Title, 14, weight: FontWeights.SemiBold));
                content.Children.Add(Ui.Hint(it.Hint));
                if (it.Id == "activation" && extra != null) content.Children.Add(extra);
                var cb = new CheckBox { Style = (Style)Application.Current.FindResource("Check"), Content = content, IsChecked = PlanStore.Get(id) };
                cb.Checked += (_, _) => { PlanStore.Set(id, true); Update(); };
                cb.Unchecked += (_, _) => { PlanStore.Set(id, false); Update(); };
                boxes.Add(cb);
                var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(cb);
                if (it.Action != null)
                {
                    it.Action.Margin = new Thickness(12, 0, 0, 0);
                    it.Action.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(it.Action, 1);
                    row.Children.Add(it.Action);
                }
                list.Children.Add(row);
            }
            Update();
            var reset = Ui.Btn("Desmarcar tudo", () => { foreach (var b in boxes) b.IsChecked = false; });
            return Ui.Card(title, progressText, bar, Ui.Separator(), list, Ui.Buttons(reset));
        }

        public override async Task LoadAsync()
        {
            try
            {
                var r = await Ps.ObjAsync(@"
$p = Get-CimInstance SoftwareLicensingProduct -Filter ""ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL"" -ErrorAction SilentlyContinue | Select-Object -First 1
[pscustomobject]@{ Status = [int]$p.LicenseStatus; Channel = ""$($p.ProductKeyChannel)"" }");
                var ok = r.D("Status") == 1;
                activation.Text = ok ? I18n.Tr("✓ Seu Windows está ativado ({0}).", r.S("Channel")) : I18n.Tr("✗ Seu Windows NÃO está ativado: tenha uma chave em mãos antes de formatar.");
                activation.Foreground = ok ? Ui.Good : Ui.Warn;
            }
            catch { activation.Text = I18n.Tr("Não foi possível verificar a ativação."); }

            try
            {
                report = await BiosGuide.BuildAsync();
                BuildBios(report);
            }
            catch (Exception ex)
            {
                bios.Children.Clear();
                bios.Children.Add(Ui.T("Não foi possível ler a BIOS: " + ex.Message, 14, Ui.Warn));
            }
        }

        void BuildBios(BiosGuide.Report rep)
        {
            bios.Children.Clear();
            int todo = rep.Items.Count(i => i.Status == 1);
            bios.Children.Add(Ui.KV("Placa-mãe", rep.Board));
            bios.Children.Add(Ui.KV("Entrar na BIOS", $"Aperte {rep.Menus.EnterKey} várias vezes ao ligar  ·  Menu de boot: {rep.Menus.BootKey}"));
            bios.Children.Add(Ui.T(todo == 0 ? "Tudo o que dá para detectar já está certo." : $"{todo} ajuste(s) recomendados para o seu PC render mais:", 14.5,
                todo == 0 ? Ui.Good : Ui.Warn, FontWeights.SemiBold, new Thickness(0, 10, 0, 4)));

            foreach (var it in rep.Items)
            {
                var (label, color) = it.Status switch { 0 => ("OK", Ui.Good), 1 => ("Ajustar", Ui.Warn), _ => ("Conferir", Ui.Sub) };
                var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var pill = Ui.Pill(label, color);
                pill.VerticalAlignment = VerticalAlignment.Top;
                pill.Margin = new Thickness(0, 2, 0, 0);
                g.Children.Add(pill);
                var text = new StackPanel();
                text.Children.Add(Ui.T($"{it.Title}  ·  {it.StatusText}", 14, weight: FontWeights.SemiBold));
                text.Children.Add(Ui.Hint(it.Why));
                text.Children.Add(Ui.T("→ " + it.Where, 13, Ui.Accent, margin: new Thickness(0, 3, 0, 0)));
                Grid.SetColumn(text, 1);
                g.Children.Add(text);
                bios.Children.Add(g);
            }

            bios.Children.Add(Ui.Separator());
            bios.Children.Add(Ui.T("Como atualizar a BIOS", 14.5, weight: FontWeights.SemiBold, margin: new Thickness(0, 0, 0, 4)));
            int n = 1;
            foreach (var s in new[]
            {
                $"Na página da {rep.Menus.Brand}, compare a versão mais nova com a sua ({rep.BiosVersion}). Leia as notas: algumas pedem uma versão intermediária antes.",
                "Extraia o arquivo num pendrive em FAT32.",
                $"Reinicie na BIOS e abra {rep.Menus.Flash}. Escolha o arquivo.",
                "NÃO desligue nem tire o pendrive até terminar. O PC pode reiniciar algumas vezes.",
                "A atualização volta tudo ao padrão: refaça os ajustes acima (principalmente XMP e Resizable BAR).",
            })
                bios.Children.Add(Ui.T($"{n++}.  {I18n.Tr(s)}", 13.5, margin: new Thickness(0, 3, 0, 3)));

            var (_, _, _, url) = MakerInfo(rep.Board, rep.Board, "", "");
            bios.Children.Add(Ui.Buttons(
                Ui.BtnAsync("Gerar guia para o celular", GenerateGuide, "primary", ""),
                Ui.Btn($"Página de BIOS da {rep.Menus.Brand}", () => Admin.Open(url), icon: ""),
                Ui.Btn("Reiniciar direto na BIOS", RebootToBios, "danger", "")));
            bios.Children.Add(Ui.Hint("A BIOS não consegue abrir arquivos de texto ou imagem. Por isso o guia vira uma imagem para você mandar para o celular (pelo WhatsApp, por exemplo) e seguir enquanto mexe.", new Thickness(0, 6, 0, 0)));
        }

        async Task GenerateGuide()
        {
            report ??= await BiosGuide.BuildAsync();
            var png = BiosGuide.SaveImage(report, Folders.Pictures);
            var txt = BiosGuide.SaveText(report, Folders.Pictures);
            // Se tiver pendrive conectado, deixa uma cópia nele também
            var usb = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToList();
            foreach (var d in usb)
            {
                try
                {
                    File.Copy(png, Path.Combine(d.RootDirectory.FullName, Path.GetFileName(png)), true);
                    File.Copy(txt, Path.Combine(d.RootDirectory.FullName, Path.GetFileName(txt)), true);
                }
                catch { }
            }
            Ui.Info("Guia salvo em Imagens" + (usb.Count > 0 ? " e no pendrive" : "") + ". Mande a imagem para o seu celular e siga enquanto mexe na BIOS.");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{png}\"") { UseShellExecute = true });
        }

        /// <summary>Nome da ferramenta de atualização, onde fica o XMP e a página de suporte de cada fabricante.</summary>
        internal static (string Brand, string Flash, string Xmp, string Url) MakerInfo(string maker, string board, string sysMaker, string sysModel)
        {
            static string Search(string what, string site) =>
                "https://www.google.com/search?q=" + Uri.EscapeDataString($"{what} BIOS download" + (site.Length > 0 ? $" site:{site}" : ""));
            var model = System.Text.RegularExpressions.Regex.Replace(board, @"^(Micro-Star International Co\., Ltd\.|ASUSTeK COMPUTER INC\.|Gigabyte Technology Co\., Ltd\.|ASRock|MSI|ASUS|Gigabyte)\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            model = System.Text.RegularExpressions.Regex.Replace(model, @"\s*\(MS-\w+\)", "").Trim();
            var m = (maker + " " + board).ToLowerInvariant();
            if (m.Contains("micro-star") || m.Contains("msi"))
                return ("MSI", "M-Flash", "A-XMP", $"https://www.msi.com/Motherboard/{model.Replace(' ', '-')}/support#bios");
            if (m.Contains("asus")) return ("ASUS", "EZ Flash", "XMP I", Search(model, "asus.com"));
            if (m.Contains("gigabyte")) return ("Gigabyte", "Q-Flash", "X.M.P.", Search(model, "gigabyte.com"));
            if (m.Contains("asrock")) return ("ASRock", "Instant Flash", "XMP", Search(model, "asrock.com"));
            if (m.Contains("biostar")) return ("Biostar", "BIO-Flasher", "XMP", Search(model, "biostar.com.tw"));
            return (sysMaker, "app de suporte do fabricante", "", Search(sysModel.Length > 0 ? sysModel : model, ""));
        }

        void RebootToBios()
        {
            if (!Hardware.IsUefi) { Ui.Info("Seu PC usa inicialização antiga: para entrar na BIOS, reinicie e aperte Del ou F2 várias vezes na tela de abertura."); return; }
            if (!Admin.IsAdmin)
            {
                if (Ui.Confirm("Para reiniciar direto na BIOS, o Bastion precisa estar aberto como administrador.\n\nReabrir como administrador agora?")) Admin.Relaunch();
                return;
            }
            if (!Ui.Confirm("O PC vai reiniciar em 5 segundos e abrir a BIOS.\n\nSalve tudo o que estiver aberto antes. Continuar?")) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/r /fw /t 5") { UseShellExecute = false, CreateNoWindow = true }); }
            catch (Exception ex) { Ui.Info("Não foi possível reiniciar na BIOS: " + ex.Message); }
        }

        async Task SaveProgramList()
        {
            var file = Path.Combine(Folders.Desktop, $"Programas instalados - {Environment.MachineName}.txt");
            await Busy("Listando programas…", () => Task.Run(() =>
            {
                var names = new[]
                {
                    (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                }
                .SelectMany(s =>
                {
                    using var k = s.Item1.OpenSubKey(s.Item2);
                    if (k == null) return Enumerable.Empty<string>();
                    return k.GetSubKeyNames().Select(n =>
                    {
                        using var sub = k.OpenSubKey(n);
                        if (sub?.GetValue("DisplayName") is not string name || sub.GetValue("SystemComponent") is int sc && sc == 1) return null;
                        var ver = sub.GetValue("DisplayVersion") as string;
                        return string.IsNullOrEmpty(ver) ? name : $"{name}  ({ver})";
                    }).Where(x => x != null).Select(x => x!).ToList();
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
                var sb = new StringBuilder();
                sb.AppendLine($"Programas instalados em {Environment.MachineName} — {DateTime.Now:dd/MM/yyyy HH:mm}");
                sb.AppendLine(new string('-', 60));
                foreach (var n in names) sb.AppendLine(n);
                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            }));
            if (File.Exists(file)) { Ui.Info("Lista salva na Área de Trabalho."); Admin.Open(file); }
        }

        async Task BackupDrivers()
        {
            if (!Admin.IsAdmin) { Ui.Info("Abra o Bastion como administrador para fazer backup dos drivers."); return; }
            using var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Escolha onde salvar os drivers (de preferência fora do disco do Windows)", UseDescriptionForTitle = true };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var dest = Path.Combine(dlg.SelectedPath, $"Drivers {Environment.MachineName} {DateTime.Now:yyyy-MM-dd}");
            Directory.CreateDirectory(dest);
            await Busy("Exportando drivers… pode levar alguns minutos.", async () =>
            {
                await Ps.RunAsync($"pnputil /export-driver * {Ps.Q(dest)} | Out-Null", 1800000);
            });
            Ui.Info("Drivers salvos. Para reinstalar depois: Gerenciador de Dispositivos → Atualizar driver → Procurar no computador → escolha essa pasta.");
            Admin.Open(dest);
        }
    }
}
