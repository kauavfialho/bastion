using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Bastion.Core
{
    /// <summary>Cores do app: destaque (degradê) + tom de fundo. Muda na hora e fica salvo.</summary>
    public static class Theme
    {
        public sealed record Accent(string Name, string From, string To);
        public sealed record Background(string Name, string Bg, string Sidebar, string Card, string Border, string Hover);

        public static readonly Accent[] Accents =
        {
            new("Roxo neon", "#7C5CFF", "#00C2FF"),
            new("Azul elétrico", "#2F6BFF", "#00E5FF"),
            new("Verde matrix", "#00C853", "#64FFDA"),
            new("Vermelho gamer", "#FF1F4B", "#FF8A00"),
            new("Laranja solar", "#FF6A00", "#FFD000"),
            new("Rosa synthwave", "#FF3CAC", "#784BA0"),
        };

        public static readonly Background[] Backgrounds =
        {
            new("Grafite", "#0F1115", "#14171D", "#1A1E26", "#272C37", "#222733"),
            new("Preto total", "#000000", "#08090B", "#101216", "#1E2128", "#1A1D24"),
            new("Azul-noite", "#0A0F1E", "#0E1428", "#141B33", "#243052", "#1C2541"),
            new("Carbono", "#121212", "#171717", "#1F1F1F", "#2E2E2E", "#2A2A2A"),
        };

        sealed class Saved { public string Accent { get; set; } = ""; public string Background { get; set; } = ""; public bool Rain { get; set; } }

        /// <summary>Chuva binária na barra lateral.</summary>
        public static bool Rain { get; private set; }

        static string FilePath => Path.Combine(Folders.AppData, "tema.json");

        public static Accent CurrentAccent { get; private set; } = Accents[0];
        public static Background CurrentBackground { get; private set; } = Backgrounds[0];

        public static event Action? Changed;

        public static void LoadSaved()
        {
            try
            {
                var s = JsonSerializer.Deserialize<Saved>(File.ReadAllText(FilePath));
                CurrentAccent = Accents.FirstOrDefault(a => a.Name == s?.Accent) ?? Accents[0];
                CurrentBackground = Backgrounds.FirstOrDefault(b => b.Name == s?.Background) ?? Backgrounds[0];
                Rain = s?.Rain ?? false;
            }
            catch { }
            Apply();
        }

        public static void Set(Accent? accent = null, Background? background = null, bool? rain = null)
        {
            CurrentAccent = accent ?? CurrentAccent;
            CurrentBackground = background ?? CurrentBackground;
            Rain = rain ?? Rain;
            Apply();
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(new Saved { Accent = CurrentAccent.Name, Background = CurrentBackground.Name, Rain = Rain })); } catch { }
            Changed?.Invoke();
        }

        public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        public static LinearGradientBrush Gradient(Accent a) =>
            new(new GradientStopCollection { new(C(a.From), 0), new(C(a.To), 1) }, new Point(0, 0), new Point(1, 1));

        /// <summary>
        /// Troca os pincéis do tema. O App.xaml e a janela usam DynamicResource para essas chaves,
        /// então botões, abas e cartões mudam na hora; textos coloridos criados em código atualizam ao reabrir a tela.
        /// </summary>
        static void Apply()
        {
            var r = Application.Current.Resources;
            void Solid(string key, string hex) => r[key] = new SolidColorBrush(C(hex));
            Solid("Accent", CurrentAccent.From);
            r["AccentGradient"] = Gradient(CurrentAccent);

            var bg = CurrentBackground;
            Solid("Bg", bg.Bg);
            Solid("Sidebar", bg.Sidebar);
            Solid("CardBg", bg.Card);
            Solid("CardBorder", bg.Border);
            Solid("Hover", bg.Hover);
            Solid("Track", bg.Border);
        }
    }
}
