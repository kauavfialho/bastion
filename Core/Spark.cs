using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Bastion.Core
{
    /// <summary>Mini-gráfico de linha com área preenchida.</summary>
    public sealed class Spark : FrameworkElement
    {
        readonly Series series;
        readonly double? fixedMax;
        readonly Color color;

        public Spark(Series series, Color color, double? max = 100)
        {
            this.series = series;
            this.color = color;
            fixedMax = max;
            Height = 48;
            Margin = new Thickness(0, 10, 0, 0);
            SnapsToDevicePixels = true;
        }

        public void Refresh() => InvalidateVisual();

        protected override void OnRender(DrawingContext dc)
        {
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            var values = series.Values.ToArray();
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
            if (values.Length < 2) return;

            var max = fixedMax ?? Math.Max(1, values.Max() * 1.2);
            // Enquanto ainda não tem 60 leituras, estica o que tem para ocupar a largura toda
            var step = w / (Math.Min(values.Length, Series.Size) - 1);
            var x0 = 0.0;
            Point P(int i) => new(x0 + i * step, h - 2 - Math.Clamp(values[i] / max, 0, 1) * (h - 4));

            var line = new StreamGeometry();
            var area = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(P(0), false, false);
                for (int i = 1; i < values.Length; i++) g.LineTo(P(i), true, true);
            }
            using (var g = area.Open())
            {
                g.BeginFigure(new Point(x0, h), true, true);
                for (int i = 0; i < values.Length; i++) g.LineTo(P(i), true, true);
                g.LineTo(new Point(w, h), true, true);
            }
            line.Freeze();
            area.Freeze();

            var fill = new LinearGradientBrush(Color.FromArgb(0x55, color.R, color.G, color.B), Color.FromArgb(0x00, color.R, color.G, color.B), 90);
            fill.Freeze();
            dc.DrawGeometry(fill, null, area);
            var pen = new Pen(new SolidColorBrush(color), 1.8) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            dc.DrawGeometry(null, pen, line);
        }
    }
}
