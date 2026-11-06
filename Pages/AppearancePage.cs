using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class AppearancePage : PageBase
    {
        readonly UniformGrid accents = new() { Columns = 3, Margin = new Thickness(0, 0, -12, 0) };
        readonly UniformGrid backgrounds = new() { Columns = 4, Margin = new Thickness(0, 0, -12, 0) };

        public AppearancePage() : base("Aparência", "Escolha as cores do Bastion. Muda na hora e fica salvo.")
        {
            Body.Children.Add(Ui.Card("Cor de destaque", Ui.Hint("Usada nos botões, na barra lateral, nas abas e nos gráficos.", new Thickness(0, 0, 0, 12)), accents));
            Body.Children.Add(Ui.Card("Fundo", Ui.Hint("O tom das telas e dos cartões.", new Thickness(0, 0, 0, 12)), backgrounds));
            var rain = Ui.Switch("Chuva binária na barra lateral", "0 e 1 caindo na cor de destaque, bem suave, atrás dos botões do menu. Fica ótimo com o Verde matrix.", Theme.Rain);
            rain.Checked += (_, _) => Theme.Set(rain: true);
            rain.Unchecked += (_, _) => Theme.Set(rain: false);
            Body.Children.Add(Ui.Card("Efeitos", rain));
            Body.Children.Add(Ui.Card("Prévia",
                Ui.Buttons(Ui.Btn("Botão principal", () => Ui.Info("Assim aparecem os avisos do Bastion."), "primary", ""), Ui.Btn("Botão comum", () => Ui.Info("Selecione pelo menos um item. (Exemplo de aviso de atenção.)"))),
                Ui.Switch("Interruptor", "Assim ficam as opções liga/desliga.", true),
                Ui.Bar(62)));
            Build();
            Theme.Changed += Build;
        }

        void Build()
        {
            accents.Children.Clear();
            foreach (var a in Theme.Accents)
            {
                var swatch = new Border { Height = 46, CornerRadius = new CornerRadius(10), Background = Theme.Gradient(a) };
                accents.Children.Add(Choice(swatch, a.Name, a == Theme.CurrentAccent, () => Theme.Set(accent: a)));
            }
            backgrounds.Children.Clear();
            foreach (var b in Theme.Backgrounds)
            {
                // Miniatura: fundo, barra lateral e um cartão
                var mini = new Grid { Height = 46 };
                mini.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                mini.ColumnDefinitions.Add(new ColumnDefinition());
                mini.Children.Add(new Border { Background = new SolidColorBrush(Theme.C(b.Sidebar)), CornerRadius = new CornerRadius(8, 0, 0, 8) });
                var main = new Border { Background = new SolidColorBrush(Theme.C(b.Bg)), CornerRadius = new CornerRadius(0, 8, 8, 0), Padding = new Thickness(8) };
                main.Child = new Border { Background = new SolidColorBrush(Theme.C(b.Card)), BorderBrush = new SolidColorBrush(Theme.C(b.Border)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
                Grid.SetColumn(main, 1);
                mini.Children.Add(main);
                backgrounds.Children.Add(Choice(mini, b.Name, b == Theme.CurrentBackground, () => Theme.Set(background: b)));
            }
        }

        Border Choice(UIElement preview, string name, bool selected, System.Action pick)
        {
            var sp = new StackPanel();
            sp.Children.Add(preview);
            var label = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            if (selected) label.Children.Add(Ui.Icon("", 13, Ui.Good));
            label.Children.Add(Ui.T(name, 13.5, weight: selected ? FontWeights.SemiBold : FontWeights.Normal, margin: new Thickness(selected ? 6 : 0, 0, 0, 0)));
            sp.Children.Add(label);
            var card = new Border
            {
                Style = (Style)FindResource("Card"),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 12, 12),
                Cursor = Cursors.Hand,
                Child = sp,
            };
            if (selected) card.BorderBrush = Ui.Accent;
            card.MouseLeftButtonUp += (_, _) => pick();
            return card;
        }

        public override Task LoadAsync() => Task.CompletedTask;
    }
}
