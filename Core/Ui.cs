using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Bastion.Core
{
    /// <summary>Peças de interface reutilizadas pelas páginas.</summary>
    public static class Ui
    {
        static T R<T>(string key) => (T)Application.Current.FindResource(key);

        public static Brush Fg => R<Brush>("Fg");
        public static Brush Sub => R<Brush>("Sub");
        public static Brush Good => R<Brush>("Good");
        public static Brush Warn => R<Brush>("Warn");
        public static Brush Bad => R<Brush>("Bad");
        public static Brush Accent => R<Brush>("Accent");
        public static FontFamily Icons => R<FontFamily>("Icons");

        public static TextBlock T(string text, double size = 14, Brush? color = null, FontWeight? weight = null, Thickness? margin = null) => new()
        {
            Text = text,
            FontSize = size,
            Foreground = color ?? Fg,
            FontWeight = weight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0),
        };

        public static TextBlock Hint(string text, Thickness? margin = null) => T(text, 12.5, Sub, margin: margin ?? new Thickness(0, 2, 0, 0));

        public static TextBlock Icon(string glyph, double size = 16, Brush? color = null) => new()
        {
            Text = glyph,
            FontFamily = Icons,
            FontSize = size,
            Foreground = color ?? Fg,
            VerticalAlignment = VerticalAlignment.Center,
        };

        public static Border Card(string? title, params UIElement[] children)
        {
            var sp = new StackPanel();
            if (title != null) sp.Children.Add(T(title, 16.5, weight: FontWeights.SemiBold, margin: new Thickness(0, 0, 0, 12)));
            foreach (var c in children) sp.Children.Add(c);
            return new Border { Style = R<Style>("Card"), Child = sp };
        }

        public static Button Btn(string text, Action onClick, string kind = "", string? icon = null)
        {
            var b = new Button { Style = R<Style>(kind switch { "primary" => "BtnPrimary", "danger" => "BtnDanger", _ => "Btn" }) };
            b.Content = icon == null ? text : new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { new TextBlock { Text = icon, FontFamily = Icons, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = text } }
            };
            b.Click += (_, _) => onClick();
            return b;
        }

        /// <summary>Botão que fica desativado enquanto a tarefa roda.</summary>
        public static Button BtnAsync(string text, Func<Task> work, string kind = "", string? icon = null)
        {
            Button? b = null;
            b = Btn(text, async () =>
            {
                b!.IsEnabled = false;
                try { await work(); }
                catch (Exception ex) { Info("Erro: " + ex.Message); }
                finally { b.IsEnabled = true; }
            }, kind, icon);
            return b;
        }

        public static WrapPanel Buttons(params UIElement[] items)
        {
            var w = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var i in items) w.Children.Add(i);
            return w;
        }

        /// <summary>Linha "rótulo .... valor".</summary>
        public static Grid KV(string key, string value, Brush? valueColor = null)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(T(key, 13.5, Sub));
            var v = T(value, 13.5, valueColor ?? Fg);
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            return g;
        }

        public static Border Pill(string text, Brush color) => new()
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 2, 9, 3),
            Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = T(text, 12, color, FontWeights.SemiBold),
        };

        public static Brush Level(int level) => level switch { 0 => Good, 1 => Warn, _ => Bad };

        /// <summary>Cartão pequeno com um número/valor em destaque.</summary>
        public static Border Stat(string glyph, string label, string value, string? detail = null, Brush? color = null)
        {
            var sp = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            head.Children.Add(Icon(glyph, 15, color ?? Accent));
            head.Children.Add(T(label, 13, Sub, margin: new Thickness(8, 0, 0, 0)));
            sp.Children.Add(head);
            sp.Children.Add(T(value, 20, color ?? Fg, FontWeights.SemiBold));
            if (detail != null) sp.Children.Add(Hint(detail, new Thickness(0, 4, 0, 0)));
            return new Border { Style = R<Style>("Card"), Child = sp, Margin = new Thickness(0, 0, 14, 14) };
        }

        public static UniformGrid Columns(int columns, params UIElement[] items)
        {
            var g = new UniformGrid { Columns = columns, Margin = new Thickness(0, 0, -14, 0) };
            foreach (var i in items) g.Children.Add(i);
            return g;
        }

        public static CheckBox Switch(string label, string? hint, bool on)
        {
            var sp = new StackPanel();
            sp.Children.Add(T(label, 14));
            if (hint != null) sp.Children.Add(Hint(hint));
            return new CheckBox { Style = R<Style>("Switch"), Content = sp, IsChecked = on, Margin = new Thickness(0, 6, 0, 10) };
        }

        public static CheckBox Check(string label, string? hint, bool on)
        {
            var sp = new StackPanel();
            sp.Children.Add(T(label, 14));
            if (hint != null) sp.Children.Add(Hint(hint));
            return new CheckBox { Style = R<Style>("Check"), Content = sp, IsChecked = on, Margin = new Thickness(0, 4, 0, 8) };
        }

        public static ProgressBar Bar(double percent, Brush? color = null)
        {
            var p = new ProgressBar { Minimum = 0, Maximum = 100, Value = Math.Clamp(percent, 0, 100), Margin = new Thickness(0, 6, 0, 2) };
            if (color != null) p.Foreground = color;
            return p;
        }

        public static Border Separator() => new() { Height = 1, Background = R<Brush>("CardBorder"), Margin = new Thickness(0, 10, 0, 10) };

        /// <summary>
        /// Controle que só funciona como administrador: em vez de ficar cinza e sem explicação,
        /// ao clicar ele oferece reabrir o Bastion como admin na mesma tela.
        /// </summary>
        public static void NeedsAdmin(Control c, string what)
        {
            if (Admin.IsAdmin) return;
            c.IsEnabled = true;
            c.Opacity = 0.7;
            c.ToolTip = $"Precisa de administrador para {what}. Clique para reabrir como administrador.";
            void Ask()
            {
                if (Confirm($"Para {what}, o Bastion precisa estar aberto como administrador.\n\nReabrir como administrador agora? Você volta para esta mesma tela."))
                    Admin.Relaunch();
            }
            c.PreviewMouseLeftButtonDown += (_, e) => { e.Handled = true; Ask(); };
            c.PreviewKeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter) { e.Handled = true; Ask(); } };
        }

        public static bool Confirm(string message) =>
            MessageBox.Show(message, "Bastion", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        /// <summary>Avisa o usuário com uma notificação no canto (ou caixa de mensagem fora da janela principal).</summary>
        public static void Info(string message)
        {
            if (Application.Current?.MainWindow is MainWindow mw && mw.IsVisible)
            {
                bool bad = message.StartsWith("Erro", StringComparison.OrdinalIgnoreCase) ||
                           message.Contains("Não foi possível", StringComparison.OrdinalIgnoreCase) ||
                           message.Contains("falh", StringComparison.OrdinalIgnoreCase) ||
                           message.Contains("problema", StringComparison.OrdinalIgnoreCase);
                bool hint = message.StartsWith("Selecione", StringComparison.OrdinalIgnoreCase) || message.StartsWith("Abra", StringComparison.OrdinalIgnoreCase);
                mw.ShowToast(message, bad ? Bad : hint ? Warn : Good);
                return;
            }
            MessageBox.Show(message, "Bastion", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static string Size(long bytes)
        {
            if (bytes >= 1L << 30) return $"{bytes / (double)(1L << 30):0.0} GB";
            if (bytes >= 1L << 20) return $"{bytes / (double)(1L << 20):0} MB";
            if (bytes >= 1L << 10) return $"{bytes / 1024.0:0} KB";
            return $"{bytes} B";
        }
    }
}
