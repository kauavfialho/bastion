using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Bastion.Core
{
    /// <summary>
    /// "Chuva" de 0 e 1 caindo, estilo Matrix, desenhada atrás da barra lateral.
    /// Leve: ~20 quadros por segundo e só enquanto a janela está visível.
    /// </summary>
    public sealed class BinaryRain : FrameworkElement
    {
        const double Cell = 15;       // tamanho de cada caractere
        const int Trail = 16;         // comprimento do rastro

        sealed class Drop { public double Y, Speed; public char[] Chars = Array.Empty<char>(); }

        readonly Random rnd = new();
        readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        Drop[] drops = Array.Empty<Drop>();
        FormattedText? zero, one;
        Color color = Colors.LimeGreen;
        DateTime last = DateTime.UtcNow;

        public BinaryRain()
        {
            IsHitTestVisible = false;
            ClipToBounds = true;
            timer.Tick += (_, _) => Step();
            IsVisibleChanged += (_, _) => { if (IsVisible) { last = DateTime.UtcNow; timer.Start(); } else timer.Stop(); };
            SizeChanged += (_, _) => Reset();
        }

        public void SetColor(Color c)
        {
            color = c;
            zero = one = null;   // refaz os caracteres na cor nova
            InvalidateVisual();
        }

        void Reset()
        {
            int cols = Math.Max(1, (int)(ActualWidth / Cell));
            drops = new Drop[cols];
            for (int i = 0; i < cols; i++) drops[i] = NewDrop(randomStart: true);
        }

        Drop NewDrop(bool randomStart)
        {
            var d = new Drop
            {
                Speed = 35 + rnd.NextDouble() * 85,
                Y = randomStart ? rnd.NextDouble() * ActualHeight : -rnd.NextDouble() * 200,
                Chars = new char[Trail],
            };
            for (int i = 0; i < Trail; i++) d.Chars[i] = rnd.Next(2) == 0 ? '0' : '1';
            return d;
        }

        void Step()
        {
            var now = DateTime.UtcNow;
            var dt = Math.Min(0.2, (now - last).TotalSeconds);
            last = now;
            for (int i = 0; i < drops.Length; i++)
            {
                var d = drops[i];
                d.Y += d.Speed * dt;
                if (rnd.Next(12) == 0) d.Chars[rnd.Next(Trail)] = rnd.Next(2) == 0 ? '0' : '1';   // dígitos trocando no caminho
                if (d.Y - Trail * Cell > ActualHeight) drops[i] = NewDrop(randomStart: false);
            }
            InvalidateVisual();
        }

        FormattedText Glyph(char c) => new(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            13, new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);

        protected override void OnRender(DrawingContext dc)
        {
            if (drops.Length == 0) Reset();
            zero ??= Glyph('0');
            one ??= Glyph('1');
            for (int i = 0; i < drops.Length; i++)
            {
                var d = drops[i];
                double x = i * Cell + 2;
                for (int k = 0; k < Trail; k++)
                {
                    double y = d.Y - k * Cell;
                    if (y < -Cell || y > ActualHeight) continue;
                    // A "cabeça" é mais forte e o rastro vai sumindo
                    dc.PushOpacity(k == 0 ? 1.0 : 0.75 * (1 - (double)k / Trail));
                    dc.DrawText(d.Chars[k] == '0' ? zero : one, new Point(x, y));
                    dc.Pop();
                }
            }
        }
    }
}
