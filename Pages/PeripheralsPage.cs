using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Bastion.Core;

namespace Bastion.Pages
{
    /// <summary>Luzes de todos os aparelhos num lugar só (via OpenRGB) e controle dos apps de fabricante.</summary>
    public sealed class PeripheralsPage : PageBase
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "Bastion Luzes";

        readonly MainWindow main;
        readonly StackPanel rgbStatus = new();
        readonly StackPanel rgbControls = new() { Visibility = Visibility.Collapsed };
        readonly StackPanel deviceList = new();
        readonly StackPanel vendors = new();
        readonly TextBox hex = new() { Width = 110, Text = "#00C853", Padding = new Thickness(8, 6, 8, 6), FontFamily = new FontFamily("Cascadia Mono, Consolas") };
        readonly Border preview = new() { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Margin = new Thickness(10, 0, 0, 0) };
        readonly List<(CheckBox Box, OpenRgb.Device Dev)> devices = new();
        Color color = Theme.C(Theme.CurrentAccent.From);
        OpenRgb? client;

        public PeripheralsPage(MainWindow main) : base("Periféricos", "As luzes de todos os seus aparelhos num lugar só, e os apps de fabricante sob controle.")
        {
            this.main = main;
            hex.Background = new SolidColorBrush(Color.FromRgb(0x12, 0x15, 0x1B));
            hex.Foreground = Ui.Fg;
            hex.BorderBrush = (Brush)Application.Current.FindResource("CardBorder");
            hex.TextChanged += (_, _) => { if (TryParse(hex.Text, out var c)) SetColor(c, fromText: true); };
            SetColor(color);

            Body.Children.Add(Ui.Card("Luzes (RGB)", rgbStatus, rgbControls));
            Body.Children.Add(Ui.Card("Apps de fabricante", vendors));
            Body.Children.Add(Ui.Card("Mouse, teclado e fone",
                Ui.T("O que fica salvo no próprio aparelho", 14, weight: FontWeights.SemiBold),
                Ui.Hint("Logitech: DPI, botões e macros ficam na memória do mouse/teclado (\"memória integrada\" no G HUB). Configure uma vez e desligue o G HUB da inicialização: continua tudo funcionando."),
                Ui.Hint("Astro (MixAmp/A40/A50): a equalização fica salva no aparelho. Use o Command Center só quando quiser mudar.", new Thickness(0, 6, 0, 0)),
                Ui.Buttons(
                    Ui.Btn("Configurações de som", () => Admin.Open("ms-settings:sound"), icon: ""),
                    Ui.Btn("Testar microfone", () => Admin.Open("ms-settings:sound-devices"), icon: ""),
                    Ui.Btn("Controlar a inicialização", () => main.Navigate("Inicialização"), icon: ""))));
            BuildControls();
        }

        // ---------- Cores ----------

        static readonly (string Name, string Hex)[] Swatches =
        {
            ("Branco", "#FFFFFF"), ("Vermelho", "#FF1A1A"), ("Laranja", "#FF7A00"), ("Amarelo", "#FFD000"), ("Verde", "#00E050"),
            ("Ciano", "#00E5FF"), ("Azul", "#1E5BFF"), ("Roxo", "#8A2BFF"), ("Rosa", "#FF2EA6"),
        };

        static bool TryParse(string s, out Color c)
        {
            c = default;
            if (!Regex.IsMatch(s.Trim(), "^#?[0-9a-fA-F]{6}$")) return false;
            c = (Color)ColorConverter.ConvertFromString("#" + s.Trim().TrimStart('#'));
            return true;
        }

        void SetColor(Color c, bool fromText = false)
        {
            color = c;
            preview.Background = new SolidColorBrush(c);
            if (!fromText) hex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        void BuildControls()
        {
            var sw = new WrapPanel();
            void AddSwatch(string name, Brush b, Color c)
            {
                var s = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8), Background = b, Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand, ToolTip = I18n.Tr(name),
                    BorderBrush = (Brush)Application.Current.FindResource("CardBorder"), BorderThickness = new Thickness(1) };
                s.MouseLeftButtonUp += (_, _) => SetColor(c);
                sw.Children.Add(s);
            }
            AddSwatch("Cor do Bastion", Theme.Gradient(Theme.CurrentAccent), Theme.C(Theme.CurrentAccent.From));
            foreach (var (name, h) in Swatches) AddSwatch(name, new SolidColorBrush(Theme.C(h)), Theme.C(h));

            var custom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            custom.Children.Add(Ui.T("Cor personalizada:", 13.5, Ui.Sub, margin: new Thickness(0, 0, 10, 0)));
            ((TextBlock)custom.Children[0]).VerticalAlignment = VerticalAlignment.Center;
            custom.Children.Add(hex);
            custom.Children.Add(preview);

            rgbControls.Children.Add(Ui.T("Cor", 13, Ui.Sub, FontWeights.SemiBold, new Thickness(0, 6, 0, 6)));
            rgbControls.Children.Add(sw);
            rgbControls.Children.Add(custom);
            rgbControls.Children.Add(Ui.T("Efeito", 13, Ui.Sub, FontWeights.SemiBold, new Thickness(0, 16, 0, 6)));
            rgbControls.Children.Add(Ui.Buttons(
                Ui.BtnAsync("Cor fixa", () => Apply("fixa"), "primary", ""),
                Ui.BtnAsync("Respirando", () => Apply("respirando"), icon: ""),
                Ui.BtnAsync("Arco-íris", () => Apply("arco-iris"), icon: ""),
                Ui.BtnAsync("Arco-íris parado", () => Apply("arco-iris-parado"), icon: ""),
                Ui.BtnAsync("Desligar luzes", () => Apply("desligar"), "danger", "")));
            rgbControls.Children.Add(Ui.T("Aplicar em", 13, Ui.Sub, FontWeights.SemiBold, new Thickness(0, 16, 0, 6)));
            rgbControls.Children.Add(deviceList);

            var keep = Ui.Switch("Manter minhas cores ao ligar o PC", "O OpenRGB abre escondido no início do Windows e reaplica as últimas cores que você escolheu aqui.", AutoStart);
            keep.Checked += (_, _) => AutoStart = true;
            keep.Unchecked += (_, _) => AutoStart = false;
            rgbControls.Children.Add(Ui.Separator());
            rgbControls.Children.Add(keep);
            rgbControls.Children.Add(Ui.Buttons(Ui.BtnAsync("Procurar aparelhos de novo", RefreshDevices, icon: "")));
        }

        static bool AutoStart
        {
            get { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(RunValue) is string; }
            set
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                var exe = OpenRgb.ExePath();
                if (value && exe != null) k.SetValue(RunValue, $"\"{exe}\" --startminimized --server --server-port {OpenRgb.Port} --profile \"Bastion\"");
                else k.DeleteValue(RunValue, false);
            }
        }

        // ---------- Conexão com o OpenRGB ----------

        public override async Task LoadAsync()
        {
            BuildVendors();
            await ConnectFlow();
        }

        async Task ConnectFlow()
        {
            rgbStatus.Children.Clear();
            rgbControls.Visibility = Visibility.Collapsed;

            if (OpenRgb.ExePath() == null)
            {
                rgbStatus.Children.Add(Ui.T("Para controlar as luzes de todas as marcas daqui, o Bastion usa o OpenRGB: gratuito, de código aberto e leve.", 14));
                rgbStatus.Children.Add(Ui.Hint("Ele conversa direto com RAM Corsair, teclados e mouses Logitech, placas-mãe MSI/ASUS/Gigabyte, controladoras NZXT e centenas de outros aparelhos.", new Thickness(0, 4, 0, 8)));
                rgbStatus.Children.Add(Ui.Buttons(Ui.BtnAsync("Instalar OpenRGB", async () =>
                {
                    Status("Instalando o OpenRGB…");
                    var (ok, msg) = await Winget.InstallAsync("OpenRGB.OpenRGB", "winget");
                    Status("");
                    Ui.Info(ok ? "OpenRGB instalado!" : "Não foi possível instalar: " + msg);
                    await ConnectFlow();
                }, "primary", "")));
                return;
            }

            client?.Dispose();
            client = new OpenRgb();
            if (!await client.ConnectAsync())
            {
                if (OpenRgb.IsRunning())
                {
                    rgbStatus.Children.Add(Ui.T("O OpenRGB está aberto, mas sem o servidor ligado.", 14, Ui.Warn));
                    rgbStatus.Children.Add(Ui.Hint("Feche o OpenRGB (perto do relógio) e clique em Ligar aqui, ou ligue em OpenRGB → SDK Server → Start Server."));
                }
                else
                {
                    rgbStatus.Children.Add(Ui.T("O OpenRGB está instalado, mas desligado.", 14));
                    rgbStatus.Children.Add(Ui.Hint("Como administrador ele também enxerga a RAM e a placa-mãe. Sem admin, só os periféricos USB."));
                }
                rgbStatus.Children.Add(Ui.Buttons(
                    Ui.BtnAsync("Ligar como administrador (tudo)", () => StartOpenRgb(true), "primary", ""),
                    Ui.BtnAsync("Ligar só para periféricos USB", () => StartOpenRgb(false), icon: "")));
                return;
            }
            await RefreshDevices();
        }

        async Task StartOpenRgb(bool elevated)
        {
            try { OpenRgb.Launch(elevated); }
            catch (System.ComponentModel.Win32Exception) { return; }   // aviso do Windows cancelado
            Status("Esperando o OpenRGB encontrar os aparelhos… (até 20 segundos)");
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(1000);
                client?.Dispose();
                client = new OpenRgb();
                if (await client.ConnectAsync()) break;
            }
            await Task.Delay(3000);   // a lista de aparelhos termina de carregar logo depois do servidor
            Status("");
            await ConnectFlow();
        }

        async Task RefreshDevices()
        {
            if (client == null) return;
            List<OpenRgb.Device> list;
            try { list = await client.DevicesAsync(); }
            catch (Exception ex) { rgbStatus.Children.Clear(); rgbStatus.Children.Add(Ui.T("Erro ao falar com o OpenRGB: " + ex.Message, 14, Ui.Warn)); return; }

            rgbStatus.Children.Clear();
            rgbStatus.Children.Add(Ui.T(list.Count == 0 ? "OpenRGB ligado, mas nenhum aparelho com luz foi encontrado." : $"● Conectado ao OpenRGB · {list.Count} aparelho(s) com luz", 14,
                list.Count == 0 ? Ui.Warn : Ui.Good, FontWeights.SemiBold));
            if (list.Count == 0)
                rgbStatus.Children.Add(Ui.Hint("Se a RAM ou a placa-mãe não aparecem, ligue o OpenRGB como administrador. Feche iCUE, G HUB e NZXT CAM: eles podem \"prender\" os aparelhos."));

            var conflicts = VendorApps.Scan().Where(s => s.Running && s.App.ControlsRgb).ToList();
            if (conflicts.Count > 0)
            {
                rgbStatus.Children.Add(Ui.T($"Atenção: {string.Join(", ", conflicts.Select(c => c.App.Name))} {(conflicts.Count == 1 ? "está aberto e pode trocar" : "estão abertos e podem trocar")} as cores de volta.", 13.5, Ui.Warn, margin: new Thickness(0, 8, 0, 0)));
                rgbStatus.Children.Add(Ui.Buttons(Ui.Btn("Fechar esses apps agora", () =>
                {
                    if (!Ui.Confirm($"Fechar {string.Join(", ", conflicts.Select(c => c.App.Name))}? Eles voltam a abrir quando você abrir de novo ou reiniciar o PC.")) return;
                    int n = conflicts.Sum(VendorApps.Close);
                    Ui.Info($"{n} processo(s) fechados.");
                    BuildVendors();
                    _ = RefreshDevices();
                }, "danger", "")));
            }

            devices.Clear();
            deviceList.Children.Clear();
            var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -12, 0) };
            foreach (var d in list)
            {
                var label = string.IsNullOrWhiteSpace(d.Vendor) || d.Name.Contains(d.Vendor, StringComparison.OrdinalIgnoreCase) ? d.Name : $"{d.Vendor} {d.Name}";
                var cb = Ui.Check(label, $"{d.TypeName} · {d.LedCount} LED(s) · {d.Modes.Count} efeito(s)", true);
                cb.Margin = new Thickness(0, 4, 12, 8);
                devices.Add((cb, d));
                grid.Children.Add(cb);
            }
            deviceList.Children.Add(grid);
            rgbControls.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        static Color Hue(double h)
        {
            double x = 1 - Math.Abs(h / 60 % 2 - 1);
            var (r, g, b) = h switch { < 60 => (1.0, x, 0.0), < 120 => (x, 1.0, 0.0), < 180 => (0.0, 1.0, x), < 240 => (0.0, x, 1.0), < 300 => (x, 0.0, 1.0), _ => (1.0, 0.0, x) };
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        async Task Apply(string effect)
        {
            if (client == null) return;
            var targets = devices.Where(d => d.Box.IsChecked == true).Select(d => d.Dev).ToList();
            if (targets.Count == 0) { Ui.Info("Selecione pelo menos um aparelho."); return; }
            var c = (color.R, color.G, color.B);
            int fallback = 0;
            try
            {
                foreach (var d in targets)
                {
                    switch (effect)
                    {
                        case "desligar":
                            await client.SetColorAsync(d, new[] { ((byte)0, (byte)0, (byte)0) });
                            break;
                        case "arco-iris-parado":
                            await client.SetColorAsync(d, Enumerable.Range(0, 24).Select(i => { var h = Hue(i * 15); return (h.R, h.G, h.B); }).ToList());
                            break;
                        case "respirando":
                        case "arco-iris":
                            var rx = effect == "respirando" ? "breath|pulse|respir" : "rainbow|spectrum|cycle|wave|color shift";
                            int idx = d.Modes.FindIndex(m => Regex.IsMatch(m.Name, rx, RegexOptions.IgnoreCase));
                            if (idx >= 0) await client.SetModeAsync(d, idx, effect == "respirando" ? c : null);
                            else { await client.SetColorAsync(d, new[] { c }); fallback++; }
                            break;
                        default:
                            await client.SetColorAsync(d, new[] { c });
                            break;
                    }
                }
                await client.SaveProfileAsync("Bastion");
                Ui.Info(fallback > 0 ? $"Aplicado! {fallback} aparelho(s) não têm esse efeito e ficaram com cor fixa." : "Luzes atualizadas!");
            }
            catch (Exception ex) { Ui.Info("Erro ao mudar as luzes: " + ex.Message); }
        }

        // ---------- Apps de fabricante ----------

        void BuildVendors()
        {
            vendors.Children.Clear();
            var list = VendorApps.Scan();
            if (list.Count == 0) { vendors.Children.Add(Ui.T("Nenhum app de periférico instalado. 🎉", 14, Ui.Good)); return; }
            long total = list.Sum(s => s.RamBytes);
            vendors.Children.Add(Ui.Hint(total > 0 ? $"Os que estão abertos agora gastam {Ui.Size(total)} de RAM juntos." : "Nenhum deles está aberto agora.", new Thickness(0, 0, 0, 8)));
            foreach (var s in list)
            {
                var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new StackPanel();
                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(Ui.T($"{s.App.Brand} · {s.App.Name}", 14, weight: FontWeights.SemiBold));
                head.Children.Add(new Border { Width = 10 });
                head.Children.Add(Ui.Pill(s.Running ? $"Aberto · {Ui.Size(s.RamBytes)}" : "Fechado", s.Running ? Ui.Warn : Ui.Sub));
                text.Children.Add(head);
                text.Children.Add(Ui.Hint(s.App.Note));
                row.Children.Add(text);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (s.Running)
                {
                    var st = s;
                    var close = Ui.Btn("Fechar", () =>
                    {
                        int n = VendorApps.Close(st);
                        Ui.Info(n > 0 ? $"{st.App.Name} fechado." : $"Não foi possível fechar o {st.App.Name}. Tente como administrador.");
                        BuildVendors();
                    }, icon: "");
                    close.Margin = new Thickness(8, 0, 0, 0);
                    actions.Children.Add(close);
                }
                Grid.SetColumn(actions, 1);
                row.Children.Add(actions);
                vendors.Children.Add(row);
            }
            vendors.Children.Add(Ui.Buttons(
                Ui.Btn("Atualizar", BuildVendors, icon: ""),
                Ui.Btn("Tirar da inicialização", () => main.Navigate("Inicialização"), icon: "")));
        }
    }
}
