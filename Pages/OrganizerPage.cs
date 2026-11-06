using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class OrganizerPage : PageBase
    {
        readonly TextBlock state = Ui.T("", 15, weight: FontWeights.SemiBold);
        readonly CheckBox autoStart;
        readonly TextBox log = new()
        {
            IsReadOnly = true,
            Height = 220,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x15, 0x1B)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xC0, 0xCC)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        readonly StackPanel oldTask = new();

        public OrganizerPage() : base("Organizador de arquivos",
            "Imagens vão para Imagens, vídeos vão para Vídeos, e todo o resto fica em Downloads. Funciona sozinho em segundo plano.")
        {
            autoStart = Ui.Switch("Iniciar com o Windows", "O organizador abre escondido perto do relógio toda vez que você liga o PC.", TrayHost.AutoStart);
            autoStart.Checked += (_, _) => TrayHost.AutoStart = true;
            autoStart.Unchecked += (_, _) => TrayHost.AutoStart = false;

            var rules = new StackPanel();
            rules.Children.Add(Ui.KV("Imagens", "→ " + Folders.Pictures, Ui.Good));
            rules.Children.Add(Ui.KV("Vídeos", "→ " + Folders.Videos, Ui.Good));
            rules.Children.Add(Ui.KV("Qualquer outro arquivo", "fica onde está"));
            rules.Children.Add(Ui.Hint("Imagens: " + string.Join(" ", Organizer.ImageExt) + "\nVídeos: " + string.Join(" ", Organizer.VideoExt), new Thickness(0, 8, 0, 0)));
            rules.Children.Add(Ui.Hint("Pastas observadas: " + string.Join(",  ", Organizer.WatchedFolders()), new Thickness(0, 8, 0, 0)));

            Body.Children.Add(Ui.Card("Status", state,
                Ui.Buttons(
                    Ui.Btn("Ligar agora", () => { TrayHost.LaunchOrganizer(); Refresh(delay: true); }, "primary", ""),
                    Ui.Btn("Parar", () => { TrayHost.RequestStop(); Refresh(delay: true); }, icon: ""),
                    Ui.Btn("Organizar o que já está lá", () =>
                    {
                        int n = Organizer.OrganizeNow();
                        Ui.Info(n == 0 ? "Nada para organizar." : $"{n} arquivo(s) organizado(s).");
                        Refresh();
                    }, icon: "")),
                autoStart, oldTask));
            Body.Children.Add(Ui.Card("Regras", rules));
            Body.Children.Add(Ui.Card("Últimas movimentações", log,
                Ui.Buttons(Ui.Btn("Atualizar", () => Refresh(), icon: ""), Ui.Btn("Abrir arquivo de registro", () => Admin.Open(Organizer.LogPath), icon: ""))));
        }

        public override async Task LoadAsync()
        {
            Refresh();
            // Tarefa agendada antiga, criada antes deste app existir
            var exists = await Ps.RunAsync("if (Get-ScheduledTask -TaskName 'Organizador de Imagens' -ErrorAction SilentlyContinue) { 'sim' }");
            oldTask.Children.Clear();
            if (exists == "sim")
            {
                oldTask.Children.Add(Ui.Hint("Existe uma tarefa antiga \"Organizador de Imagens\" no Agendador de Tarefas. Este app já faz o mesmo trabalho.", new Thickness(0, 6, 0, 4)));
                oldTask.Children.Add(Ui.Buttons(Ui.BtnAsync("Remover tarefa antiga", async () =>
                {
                    await Ps.RunAsync("Unregister-ScheduledTask -TaskName 'Organizador de Imagens' -Confirm:$false");
                    oldTask.Children.Clear();
                }, "danger")));
            }
        }

        async void Refresh(bool delay = false)
        {
            if (delay) await Task.Delay(1200);
            bool running = TrayHost.IsRunning;
            state.Text = I18n.Tr(running ? "● Ativo, organizando em segundo plano" : "○ Parado");
            state.Foreground = running ? Ui.Good : Ui.Sub;
            try
            {
                log.Text = File.Exists(Organizer.LogPath)
                    ? string.Join(Environment.NewLine, File.ReadLines(Organizer.LogPath).Reverse().Take(60))
                    : I18n.Tr("Nenhuma movimentação ainda.");
            }
            catch { }
        }
    }
}
