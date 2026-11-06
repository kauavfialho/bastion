using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bastion.Core
{
    /// <summary>Ajustes recomendados da BIOS, onde ficam em cada marca de placa-mãe e o que já está certo neste PC.</summary>
    public static class BiosGuide
    {
        public sealed record Menus(string Brand, string EnterKey, string BootKey, string Flash, string Xmp, string Rebar,
                                   string SecureBoot, string Tpm, string Csm, string Fans, string Pbo, string Save);

        /// <summary>Status: 0 = certo, 1 = vale ajustar, 2 = confira você (não dá para detectar).</summary>
        public sealed record Item(string Title, string Why, string Where, int Status, string StatusText);

        public sealed record Report(string Board, string BiosVersion, DateTime BiosDate, Menus Menus, List<Item> Items);

        public static Menus MenusFor(string maker, bool amd)
        {
            var m = maker.ToLowerInvariant();
            if (m.Contains("micro-star") || m.Contains("msi"))
                return new("MSI", "Del", "F11", "M-Flash (botão no menu inicial)",
                    "Botão A-XMP no topo da tela inicial (modo EZ)",
                    "F7 (modo avançado) → Settings → Advanced → PCI Subsystem Settings → Above 4G memory: Enabled e Re-Size BAR Support: Enabled",
                    "Settings → Security → Secure Boot → Secure Boot: Enabled",
                    amd ? "Settings → Security → Trusted Computing → AMD fTPM switch: AMD CPU fTPM" : "Settings → Security → Trusted Computing → Security Device Support: Enabled",
                    "Settings → Advanced → Windows OS Configuration → BIOS UEFI/CSM Mode: UEFI",
                    "Hardware Monitor (botão na parte de baixo) → curva de cada CPU_FAN / SYS_FAN",
                    "OC → Advanced CPU Configuration → AMD Overclocking → Precision Boost Overdrive: Enabled",
                    "F10 → Yes");
            if (m.Contains("asus"))
                return new("ASUS", "Del ou F2", "F8", "EZ Flash (menu Tool)",
                    "Ai Tweaker → Ai Overclock Tuner: XMP I (ou EXPO)",
                    "Advanced → PCI Subsystem Settings → Above 4G Decoding: Enabled e Re-Size BAR Support: Auto",
                    "Boot → Secure Boot → OS Type: Windows UEFI mode",
                    amd ? "Advanced → AMD fTPM configuration → Firmware TPM: Enabled" : "Advanced → PCH-FW Configuration → PTT: Enable",
                    "Boot → CSM → Launch CSM: Disabled",
                    "Monitor → Q-Fan Configuration (ou F6)",
                    "Ai Tweaker → Precision Boost Overdrive",
                    "F10 → OK");
            if (m.Contains("gigabyte"))
                return new("Gigabyte", "Del", "F12", "Q-Flash (atalho no topo)",
                    "Tweaker → Extreme Memory Profile (X.M.P.): Profile1",
                    "Settings → IO Ports → Above 4G Decoding: Enabled e Re-Size BAR Support: Auto",
                    "Boot → Secure Boot → Secure Boot: Enabled",
                    amd ? "Settings → Miscellaneous → AMD CPU fTPM: Enabled" : "Settings → Miscellaneous → Intel Platform Trust Technology (PTT): Enabled",
                    "Boot → CSM Support: Disabled",
                    "Smart Fan 6 (tecla F6)",
                    "Tweaker → Advanced CPU Settings → Precision Boost Overdrive",
                    "F10 → Yes");
            if (m.Contains("asrock"))
                return new("ASRock", "F2 ou Del", "F11", "Instant Flash (menu Tool)",
                    "OC Tweaker → DRAM Configuration → DRAM Profile / Load XMP Setting",
                    "Advanced → Chipset Configuration → Above 4G Decoding: Enabled e Re-Size BAR Support: Enabled",
                    "Security → Secure Boot → Secure Boot: Enabled",
                    amd ? "Advanced → CPU Configuration → AMD fTPM switch: AMD CPU fTPM" : "Security → Intel Platform Trust Technology: Enabled",
                    "Boot → CSM: Disabled",
                    "H/W Monitor → FAN-Tastic Tuning",
                    "OC Tweaker → Advanced → AMD Overclocking → Precision Boost Overdrive",
                    "F10 → Yes");
            return new(maker.Length > 0 ? maker : "sua placa", "Del, F2 ou F10 (aparece na tela de abertura)", "F11, F12 ou F8", "a ferramenta de atualização da BIOS",
                "procure \"XMP\", \"EXPO\" ou \"DOCP\" no menu de memória",
                "procure \"Above 4G Decoding\" e \"Re-Size BAR\" (menu PCI ou Advanced)",
                "procure \"Secure Boot\" (menu Boot ou Security)",
                amd ? "procure \"fTPM\" (menu Security ou Advanced)" : "procure \"PTT\" (menu Security ou Advanced)",
                "procure \"CSM\" e deixe desligado (UEFI)",
                "procure \"Fan\" ou \"Hardware Monitor\"",
                "procure \"Precision Boost Overdrive\"",
                "F10 para salvar e sair");
        }

        public static async Task<Report> BuildAsync()
        {
            var r = await Ps.ObjAsync(@"
$b = Get-CimInstance Win32_BIOS; $bb = Get-CimInstance Win32_BaseBoard
$sb = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\SecureBoot\State' -ErrorAction SilentlyContinue).UEFISecureBootEnabled
[pscustomobject]@{
  Maker = ""$($bb.Manufacturer)""; Board = ""$($bb.Product)""; Version = ""$($b.SMBIOSBIOSVersion)""
  Date = $(if ($b.ReleaseDate) { $b.ReleaseDate.ToString('yyyy-MM-dd') } else { '' })
  Cpu = ""$((Get-CimInstance Win32_Processor | Select-Object -First 1).Name)""
  SecureBoot = $(if ($null -eq $sb) { -1 } else { [int]$sb })
  Tpm2 = [bool](Get-PnpDevice -Class SecurityDevices -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -match 'Trusted Platform Module 2' -and $_.Status -eq 'OK' })
  Ram = @(Get-CimInstance Win32_PhysicalMemory | ForEach-Object { [pscustomobject]@{ Part = ""$($_.PartNumber)"".Trim(); Speed = $_.ConfiguredClockSpeed } })
}");
            bool amd = r.S("Cpu").Contains("AMD", StringComparison.OrdinalIgnoreCase);
            var menus = MenusFor(r.S("Maker"), amd);
            DateTime.TryParse(r.S("Date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
            int ageMonths = date == default ? 0 : (int)((DateTime.Today - date).TotalDays / 30.4);

            var ram = r.A("Ram").Select(x => (Rated: Hardware.RatedSpeed(x.S("Part")), Now: (int)(x.D("Speed") ?? 0))).ToList();
            int rated = ram.Select(x => x.Rated).DefaultIfEmpty(0).Max(), now = ram.Select(x => x.Now).DefaultIfEmpty(0).Max();
            var rebar = await ResizableBarAsync();
            int sb = (int)(r.D("SecureBoot") ?? -1);
            bool tpm = r.B("Tpm2") == true;
            bool uefi = Hardware.IsUefi;

            var items = new List<Item>
            {
                new("BIOS atualizada", "Corrige bugs, melhora a estabilidade da RAM e o suporte a processadores novos.",
                    $"Baixe no site da {menus.Brand} e use {menus.Flash}. Faça isso ANTES de formatar.",
                    ageMonths > 12 ? 1 : 0, date == default ? r.S("Version") : $"Versão {r.S("Version")}, de {date:dd/MM/yyyy}" + (ageMonths > 12 ? " (mais de 1 ano)" : "")),
                new("XMP / EXPO (velocidade da RAM)", "O maior ganho de FPS de graça: a RAM sai do modo lento e vai para a velocidade da etiqueta. Só ligue com o PC estável (sem telas azuis) e rode o teste de RAM depois.",
                    menus.Xmp,
                    rated > now + 200 && now > 0 ? 1 : rated > 0 ? 0 : 2,
                    rated > now + 200 && now > 0 ? $"Desligado: RAM a {now} MHz, mas ela é de {rated} MHz" : now > 0 ? $"RAM a {now} MHz" + (rated > 0 ? " (já na velocidade de fábrica)" : "") : "Não deu para ler"),
                new("Resizable BAR", "Deixa o processador acessar toda a memória da placa de vídeo de uma vez. Ganho de FPS em vários jogos atuais.",
                    menus.Rebar,
                    rebar == true ? 0 : rebar == false ? 1 : 2,
                    rebar == true ? "Ligado" : rebar == false ? "Desligado" : "Confira na BIOS (só dá para detectar em placas NVIDIA)"),
                new("Modo UEFI (CSM desligado)", "Necessário para o Secure Boot e para o Windows 11. Inicialização mais rápida.",
                    menus.Csm, uefi ? 0 : 1, uefi ? "UEFI" : "Modo legado (CSM ligado)"),
                new("Secure Boot", "Exigido pelo Windows 11 e por anti-cheats como o Vanguard (Valorant) e o FACEIT.",
                    menus.SecureBoot, sb == 1 ? 0 : sb == 0 ? 1 : 2, sb == 1 ? "Ligado" : sb == 0 ? "Desligado" : "Não deu para ler"),
                new(amd ? "TPM 2.0 (fTPM)" : "TPM 2.0 (PTT)", "Exigido pelo Windows 11 e por alguns anti-cheats.",
                    menus.Tpm, tpm ? 0 : 1, tpm ? "Ligado" : "Não encontrado"),
                new("Curva dos fans", "Fans mais rápidos só quando esquenta: PC silencioso no dia a dia e frio nos jogos.",
                    menus.Fans, 2, "Confira na BIOS"),
            };
            if (amd)
                items.Add(new("Precision Boost Overdrive (opcional)", "Deixa o Ryzen turbinar mais tempo. Uns % a mais em tarefas pesadas, mas esquenta mais. Só com bom cooler.",
                    menus.Pbo, 2, "Opcional"));
            items.Add(new("Para instalar o Windows pelo pendrive", "Escolhe de onde o PC liga sem mudar nada na BIOS.",
                $"Ligue o PC e aperte {menus.BootKey} várias vezes; escolha o pendrive (\"UEFI: …\").", 2, "Na hora de formatar"));

            // "Micro-Star International Co., Ltd. MPG B550 GAMING PLUS (MS-7C56)" → "MSI MPG B550 GAMING PLUS"
            var model = Regex.Replace(r.S("Board"), @"\s*\(MS-\w+\)", "").Trim();
            var board = menus.Brand is "MSI" or "ASUS" or "Gigabyte" or "ASRock" ? $"{menus.Brand} {model}" : $"{r.S("Maker")} {model}".Trim();
            return new(board, r.S("Version"), date, menus, items);
        }

        /// <summary>Resizable BAR ligado = janela BAR1 do tamanho da memória de vídeo (só placas NVIDIA).</summary>
        static async Task<bool?> ResizableBarAsync()
        {
            var smi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
            if (!File.Exists(smi)) return null;
            try
            {
                var txt = await Ps.RunAsync($"& {Ps.Q(smi)} -q -d MEMORY");
                var fb = Regex.Match(txt, @"FB Memory Usage\s+Total\s*:\s*(\d+)");
                var bar = Regex.Match(txt, @"BAR1 Memory Usage\s+Total\s*:\s*(\d+)");
                if (!fb.Success || !bar.Success) return null;
                return double.Parse(bar.Groups[1].Value) >= double.Parse(fb.Groups[1].Value) * 0.9;
            }
            catch { return null; }
        }

        // ---------- Guia para levar no celular ----------

        static Brush B(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        /// <summary>Gera uma imagem vertical, no tamanho de tela de celular, com o passo a passo da BIOS.</summary>
        public static string SaveImage(Report rep, string folder)
        {
            const double W = 1080;
            var accent = Theme.C(Theme.CurrentAccent.From);
            var root = new StackPanel { Width = W, Background = B("#101216") };
            var head = new Border { Background = Theme.Gradient(Theme.CurrentAccent), Padding = new Thickness(56, 56, 56, 44) };
            var hs = new StackPanel();
            hs.Children.Add(new TextBlock { Text = "Guia da BIOS", FontSize = 64, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            hs.Children.Add(new TextBlock { Text = rep.Board, FontSize = 34, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            hs.Children.Add(new TextBlock { Text = $"Entrar na BIOS: {rep.Menus.EnterKey} ao ligar  ·  Salvar: {rep.Menus.Save}", FontSize = 28, Foreground = Brushes.White, Opacity = 0.9, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 0) });
            head.Child = hs;
            root.Children.Add(head);

            int n = 1;
            foreach (var it in rep.Items)
            {
                var (label, color) = it.Status switch { 0 => ("JÁ ESTÁ OK", "#3DDC97"), 1 => ("AJUSTAR", "#FFB547"), _ => ("CONFERIR", "#9AA3B2") };
                var card = new Border { Background = B("#1A1D23"), CornerRadius = new CornerRadius(24), Margin = new Thickness(40, 28, 40, 0), Padding = new Thickness(40, 34, 40, 36) };
                var cs = new StackPanel();
                var top = new DockPanel();
                var pill = new Border { Background = B(color), CornerRadius = new CornerRadius(16), Padding = new Thickness(18, 6, 18, 8), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = label, FontSize = 24, FontWeight = FontWeights.Bold, Foreground = B("#101216") } };
                DockPanel.SetDock(pill, Dock.Right);
                top.Children.Add(pill);
                top.Children.Add(new TextBlock { Text = $"{n++}. {it.Title}", FontSize = 38, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 16, 0) });
                cs.Children.Add(top);
                cs.Children.Add(new TextBlock { Text = it.StatusText, FontSize = 28, Foreground = B(color), Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
                cs.Children.Add(new TextBlock { Text = it.Why, FontSize = 28, Foreground = B("#AEB6C4"), Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap });
                var where = new Border { Background = B("#0F1114"), BorderBrush = new SolidColorBrush(accent), BorderThickness = new Thickness(4, 0, 0, 0), Padding = new Thickness(24, 18, 24, 20), Margin = new Thickness(0, 20, 0, 0) };
                where.Child = new TextBlock { Text = it.Where, FontSize = 30, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Segoe UI Semibold") };
                cs.Children.Add(where);
                card.Child = cs;
                root.Children.Add(card);
            }
            root.Children.Add(new TextBlock
            {
                Text = "Se o PC não ligar depois de uma mudança, desligue, espere 10 s e ligue de novo: ele volta sozinho. Se não voltar, desligue o XMP/PBO.\nGerado pelo Bastion · " + DateTime.Now.ToString("dd/MM/yyyy"),
                FontSize = 24, Foreground = B("#7D8594"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(56, 36, 56, 56),
            });

            root.Measure(new Size(W, double.PositiveInfinity));
            root.Arrange(new Rect(root.DesiredSize));
            root.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)W, (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(root);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"Guia da BIOS - {Safe(rep.Board)}.png");
            using (var fs = File.Create(file))
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                enc.Save(fs);
            }
            return file;
        }

        /// <summary>Mesmo guia em texto simples, para abrir em qualquer lugar.</summary>
        public static string SaveText(Report rep, string folder)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"GUIA DA BIOS - {rep.Board}");
            sb.AppendLine($"Entrar na BIOS: {rep.Menus.EnterKey} ao ligar | Salvar e sair: {rep.Menus.Save}");
            sb.AppendLine(new string('=', 60));
            int n = 1;
            foreach (var it in rep.Items)
            {
                sb.AppendLine();
                sb.AppendLine($"{n++}. {it.Title}  [{(it.Status == 0 ? "JÁ ESTÁ OK" : it.Status == 1 ? "AJUSTAR" : "CONFERIR")}] {it.StatusText}");
                sb.AppendLine($"   Por quê: {it.Why}");
                sb.AppendLine($"   Onde: {it.Where}");
            }
            var file = Path.Combine(folder, $"Guia da BIOS - {Safe(rep.Board)}.txt");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            return file;
        }

        static string Safe(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
    }
}
