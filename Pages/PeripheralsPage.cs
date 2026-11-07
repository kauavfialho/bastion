using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Bastion.Core;

namespace Bastion.Pages
{
    /// <summary>Mouse, teclado, fone e cia. ligados no PC, com o app oficial de cada um, e os apps de fabricante abertos.</summary>
    public sealed class PeripheralsPage : PageBase
    {
        readonly MainWindow main;
        readonly StackPanel found = new();
        readonly StackPanel vendors = new();

        public PeripheralsPage(MainWindow main) : base("Periféricos", "O que está ligado no seu PC e o app oficial certo para cada aparelho.")
        {
            this.main = main;
            Body.Children.Add(Ui.Card("Seus periféricos", found,
                Ui.Buttons(Ui.BtnAsync("Procurar de novo", LoadAsync, icon: ""))));
            Body.Children.Add(Ui.Card("Apps de fabricante", vendors));
        }

        static string Glyph(string kind) => kind switch
        {
            "Mouse" => "",
            "Teclado" => "",
            "Fone / headset" => "",
            "Microfone" => "",
            "Controle" => "",
            "Webcam" => "",
            _ => "",
        };

        public override Task LoadAsync() => Busy("Procurando mouse, teclado, fone e outros aparelhos…", async () =>
        {
            var list = await Peripherals.DetectAsync();
            found.Children.Clear();
            if (list.Count == 0)
                found.Children.Add(Ui.Hint("Nenhum periférico USB ou Bluetooth identificado."));

            foreach (var d in list)
            {
                var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = Ui.Icon(Glyph(d.Kind), 20, Ui.Accent);
                icon.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(icon);

                var text = new StackPanel();
                var head = new StackPanel { Orientation = Orientation.Horizontal };
                head.Children.Add(Ui.T(d.Name, 14.5, weight: FontWeights.SemiBold));
                head.Children.Add(new Border { Width = 10 });
                head.Children.Add(Ui.Pill(I18n.Tr(d.Kind) + (d.Wireless ? " · " + I18n.Tr("sem fio") : ""), Ui.Sub));
                text.Children.Add(head);

                Button? action = null;
                if (d.App is { } app)
                {
                    text.Children.Add(Ui.T($"{app.Brand} · {I18n.Tr("app oficial")}: {I18n.Tr(app.App)}", 13, Ui.Sub, margin: new Thickness(0, 2, 0, 0)));
                    text.Children.Add(Ui.Hint(app.Note));
                    action = d.Installed
                        ? Ui.Btn("Já instalado", () => main.Navigate("Inicialização"), icon: "")
                        : Ui.Btn("Baixar no site oficial", () => Admin.Open(app.Url), "primary", "");
                    if (d.Installed) { action.IsEnabled = false; action.ToolTip = I18n.Tr("O app oficial já está no seu PC."); }
                }
                else
                {
                    text.Children.Add(Ui.T("Fabricante não identificado.", 13, Ui.Warn, margin: new Thickness(0, 2, 0, 0)));
                    text.Children.Add(Ui.Hint("Procure o app no site oficial da marca (o nome está na caixa ou embaixo do aparelho). Evite sites de download de terceiros: muitos trazem vírus. A maioria funciona bem sem app nenhum."));
                }
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                if (action != null)
                {
                    action.VerticalAlignment = VerticalAlignment.Center;
                    action.Margin = new Thickness(12, 0, 0, 0);
                    Grid.SetColumn(action, 2);
                    row.Children.Add(action);
                }
                found.Children.Add(row);
            }
            if (list.Any(d => d.Wireless && d.Kind is "Mouse" or "Teclado" or "Receptor sem fio"))
                found.Children.Add(Ui.Hint("Aparelhos sem fio aparecem pelo receptor. Se você usa mouse e teclado no mesmo receptor, o app da marca mostra os dois.", new Thickness(0, 6, 0, 0)));
            found.Children.Add(Ui.Hint("Fone ligado no conector P2 da placa-mãe não aparece aqui: ele não tem como dizer ao Windows qual é.", new Thickness(0, 4, 0, 0)));

            BuildVendors();
        });

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
                if (s.Running)
                {
                    var st = s;
                    var close = Ui.Btn("Fechar", () =>
                    {
                        int n = VendorApps.Close(st);
                        Ui.Info(n > 0 ? $"{st.App.Name} fechado." : $"Não foi possível fechar o {st.App.Name}. Tente como administrador.");
                        BuildVendors();
                    }, icon: "");
                    close.VerticalAlignment = VerticalAlignment.Center;
                    close.Margin = new Thickness(8, 0, 0, 0);
                    Grid.SetColumn(close, 1);
                    row.Children.Add(close);
                }
                vendors.Children.Add(row);
            }
            vendors.Children.Add(Ui.Buttons(
                Ui.Btn("Atualizar", BuildVendors, icon: ""),
                Ui.Btn("Tirar da inicialização", () => main.Navigate("Inicialização"), icon: "")));
        }
    }
}
