using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Bastion.Core;
using Bastion.Pages;

namespace Bastion
{
    public partial class MainWindow : Window
    {
        readonly Dictionary<string, PageBase> cache = new();
        readonly Dictionary<string, RadioButton> buttons = new();

        public string CurrentPage { get; private set; } = "Início";

        public MainWindow()
        {
            InitializeComponent();
            // Tamanho fixo (só minimizar e fechar); em telas pequenas, encolhe para caber
            var area = SystemParameters.WorkArea;
            Width = Math.Min(Width, area.Width - 24);
            Height = Math.Min(Height, area.Height - 24);
            AdminText.Text = I18n.Tr(Admin.IsAdmin ? "Administrador" : "Modo limitado");
            AdminButtonText.Text = I18n.Tr("Modo administrador");
            AdminButton.Visibility = Admin.IsAdmin ? Visibility.Collapsed : Visibility.Visible;

            // Seletor de idioma, sempre à vista no topo
            foreach (var lang in I18n.Languages)
            {
                var rb = new RadioButton
                {
                    Style = (Style)FindResource("Tab"), GroupName = "lang", Content = lang.ToUpperInvariant(),
                    IsChecked = lang == I18n.Lang, FontSize = 12.5, Margin = new Thickness(2, 0, 0, 0),
                    ToolTip = lang switch { "pt" => "Português", "en" => "English", _ => "Español" },
                };
                rb.Checked += (_, _) => I18n.Set(lang);
                LangBar.Children.Add(rb);
            }

            Add("", "Início", () => new HomePage(this));
            Add("", "Saúde do PC", () => new HealthPage());
            Add("", "Limpeza", () => new CleanupPage());
            Add("", "Organizador", () => new OrganizerPage());
            Add("", "Inicialização", () => new StartupPage());
            Add("", "Jogos", () => new GamingPage());
            Add("", "Debloat", () => new DebloatPage());
            Add("", "Instalar apps", () => new AppsPage());
            Add("", "Formatar PC", () => new FormatPage(this));

            Add("", "Meu PC", () => new MyPcPage());
            Add("", "Periféricos", () => new PeripheralsPage(this));
            Add("", "Testes", () => new TestsPage());
            Add("", "Drivers", () => new DriversPage(this));
            Add("", "Aparência", () => new AppearancePage());

            Navigate("Início");
            SourceInitialized += (_, _) => DarkTitleBar();
            Theme.Changed += DarkTitleBar;
            Theme.Changed += ApplyRain;
            ApplyRain();
            Closed += (_, _) => { Theme.Changed -= DarkTitleBar; Theme.Changed -= ApplyRain; };
        }

        void ApplyRain()
        {
            Rain.SetColor(Theme.C(Theme.CurrentAccent.From));
            Rain.Visibility = Theme.Rain ? Visibility.Visible : Visibility.Collapsed;
        }

        void Add(string icon, string title, Func<PageBase> make)
        {
            var rb = new RadioButton { Style = (Style)FindResource("Nav"), GroupName = "nav" };
            rb.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = icon, FontFamily = (FontFamily)FindResource("Icons"), FontSize = 16, Width = 30, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = I18n.Tr(title), VerticalAlignment = VerticalAlignment.Center }
                }
            };
            rb.Checked += (_, _) =>
            {
                if (!cache.TryGetValue(title, out var page))
                {
                    page = make();
                    cache[title] = page;
                    _ = page.LoadAsync();
                }
                Host.Content = page;
                CurrentPage = title;
                rb.BringIntoView();   // rola o menu até a aba escolhida
            };
            buttons[title] = rb;
            Nav.Children.Add(rb);
        }

        public void Navigate(string title)
        {
            if (buttons.TryGetValue(title, out var rb)) rb.IsChecked = true;
        }

        void AdminButton_Click(object sender, RoutedEventArgs e) => Admin.Relaunch();

        /// <summary>Notificação que aparece no canto e some sozinha.</summary>
        public void ShowToast(string message, Brush color)
        {
            var text = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
            text.Children.Add(Ui.T(message, 13.5));
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = color });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var toast = new Border
            {
                Background = (Brush)FindResource("CardBg"),
                BorderBrush = (Brush)FindResource("CardBorder"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 12, 16, 12),
                Margin = new Thickness(0, 10, 0, 0),
                Child = row,
                Opacity = 0,
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = new TranslateTransform(0, 12),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Opacity = 0.45, Color = Colors.Black },
            };
            Toasts.Children.Add(toast);
            var dur = TimeSpan.FromMilliseconds(220);
            toast.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, dur));
            ((TranslateTransform)toast.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, dur));

            void Close()
            {
                var fade = new System.Windows.Media.Animation.DoubleAnimation(0, dur);
                fade.Completed += (_, _) => Toasts.Children.Remove(toast);
                toast.BeginAnimation(OpacityProperty, fade);
            }
            toast.MouseLeftButtonUp += (_, _) => Close();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Clamp(message.Length / 18.0, 4, 9)) };
            timer.Tick += (_, _) => { timer.Stop(); Close(); };
            timer.Start();
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        void DarkTitleBar()
        {
            var h = new WindowInteropHelper(this).Handle;
            int on = 1;
            DwmSetWindowAttribute(h, 20, ref on, 4);          // modo escuro
            var c = Theme.C(Theme.CurrentBackground.Sidebar);   // barra de título com a cor da barra lateral
            int caption = c.R | (c.G << 8) | (c.B << 16);      // COLORREF é BGR
            DwmSetWindowAttribute(h, 35, ref caption, 4);       // cor da barra (Windows 11)
        }
    }
}
