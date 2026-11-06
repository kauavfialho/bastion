using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class AppsPage : PageBase
    {
        /// <summary>Source: "winget", "msstore", "artcraft" (GitHub) ou "ninite".</summary>
        sealed class Item
        {
            public string Id = "", Name = "", Desc = "", Category = "", Source = "winget", Tab = "";
            public bool Installed;
            public CheckBox Box = null!;
            public Border Tile = null!;
            public ContentControl Badge = null!;
        }

        static Item W(string tab, string cat, string id, string name, string desc, string source = "winget") =>
            new() { Tab = tab, Category = cat, Id = id, Name = name, Desc = desc, Source = source };

        const string Games = "Launchers de jogos", Art = "ArtCraft", Programs = "Programas";

        static List<Item> Catalog()
        {
            var list = new List<Item>
            {
                // ---- Launchers de jogos (IDs conferidos no winget) ----
                W(Games, "Lojas", "Valve.Steam", "Steam", "A maior loja de jogos de PC"),
                W(Games, "Lojas", "EpicGames.EpicGamesLauncher", "Epic Games", "Fortnite e jogos grátis toda semana"),
                W(Games, "Lojas", "9MV0B5HZVK9Z", "Xbox", "Game Pass para PC", "msstore"),
                W(Games, "Lojas", "GOG.Galaxy", "GOG Galaxy", "Jogos sem DRM e biblioteca unificada"),
                W(Games, "Lojas", "Blizzard.BattleNet", "Battle.net", "Call of Duty, Overwatch, Diablo, WoW"),
                W(Games, "Lojas", "ElectronicArts.EADesktop", "EA app", "EA FC, Battlefield, The Sims"),
                W(Games, "Lojas", "Ubisoft.Connect", "Ubisoft Connect", "Assassin's Creed, Rainbow Six"),
                W(Games, "Lojas", "RockstarGames.Launcher", "Rockstar Games", "GTA V, Red Dead Redemption 2"),
                W(Games, "Lojas", "Amazon.Games", "Amazon Games", "Jogos do Prime Gaming"),
                W(Games, "Lojas", "ItchIo.Itch", "itch.io", "Jogos independentes"),
                W(Games, "Lojas", "Wargaming.GameCenter", "Wargaming", "World of Tanks, World of Warships"),
                W(Games, "Lojas", "Garena.Garena", "Garena", "Free Fire e jogos da Garena"),
                W(Games, "Riot Games", "RiotGames.Valorant.BR", "VALORANT", "Instala o Riot Client + Vanguard (servidor BR)"),
                W(Games, "Riot Games", "RiotGames.LeagueOfLegends.BR", "League of Legends", "Instala o Riot Client (servidor BR)"),
                W(Games, "Minecraft", "Mojang.MinecraftLauncher", "Minecraft Launcher", "Launcher oficial da Mojang"),
                W(Games, "Minecraft", "Moonsworth.LunarClient", "Lunar Client", "PvP, mais FPS e mods prontos"),
                W(Games, "Minecraft", "PrismLauncher.PrismLauncher", "Prism Launcher", "Várias instâncias e modpacks"),
                W(Games, "Minecraft", "Modrinth.ModrinthApp", "Modrinth App", "Mods e modpacks do Modrinth"),
                W(Games, "Minecraft", "Overwolf.CurseForge", "CurseForge", "Mods para Minecraft e outros jogos"),
                W(Games, "Outros jogos", "Roblox.Roblox", "Roblox", "Roblox Player"),
                W(Games, "Competitivo", "FACEITLTD.FACEITClient", "FACEIT", "Partidas ranqueadas de CS2 e outros"),
                W(Games, "Organizadores", "Playnite.Playnite", "Playnite", "Todos os jogos de todas as lojas num lugar"),
                W(Games, "Organizadores", "HeroicGamesLauncher.HeroicGamesLauncher", "Heroic", "Epic, GOG e Amazon num launcher leve"),
                W(Games, "Nuvem e streaming", "Nvidia.GeForceNow", "GeForce NOW", "Jogar na nuvem da NVIDIA"),
                W(Games, "Nuvem e streaming", "Parsec.Parsec", "Parsec", "Jogar no PC à distância ou com amigos"),
                W(Games, "Emulação", "Libretro.RetroArch", "RetroArch", "Emulador de consoles clássicos"),

                // ---- ArtCraft (Learning Machines) ----
                W(Art, "Launcher", "artcraft-launcher", "ArtCraft Launcher", "Instala e atualiza todos os apps ArtCraft. Baixado do GitHub oficial e conferido por SHA256.", "artcraft"),
                W(Art, "Apps", "ArtCraft.EffectCraft", "EffectCraft", "Efeitos e edição de vídeo"),
                W(Art, "Apps", "ArtCraft.FilmCraft", "FilmCraft", "Criação de vídeos"),
                W(Art, "Apps", "ArtCraft.VectorCraft", "VectorCraft", "Ilustração vetorial"),
                W(Art, "Apps", "ArtCraft.CadCraft", "CADCraft", "Desenho técnico e CAD"),
                W(Art, "Apps", "ArtCraft.GridCraft", "GridCraft", "Planilhas"),
                W(Art, "Apps", "ArtCraft.PdfCraft", "PdfCraft", "Editar e organizar PDFs"),
                W(Art, "Apps", "ArtCraft.SoundCraft", "SoundCraft", "Edição de áudio"),
            };

            // ---- Programas pelo Ninite (identificadores oficiais do ninite.com) ----
            (string Cat, (string Id, string Name)[] Apps)[] ninite =
            {
                ("Navegadores", new[] { ("chrome", "Chrome"), ("firefox", "Firefox"), ("brave", "Brave"), ("operaChromium", "Opera"), ("vivaldi", "Vivaldi") }),
                ("Mensagens", new[] { ("discord", "Discord"), ("whatsapp", "WhatsApp"), ("telegram", "Telegram"), ("zoom", "Zoom"), ("teams", "Teams"), ("slack", "Slack") }),
                ("Mídia", new[] { ("vlc", "VLC"), ("spotify", "Spotify"), ("obsstudio", "OBS Studio"), ("klitecodecs", "K-Lite Codecs"), ("audacity", "Audacity"), ("handbrake", "HandBrake") }),
                ("Imagem", new[] { ("sharex", "ShareX"), ("greenshot", "Greenshot"), ("paint.net", "Paint.NET"), ("gimp", "GIMP"), ("irfanview", "IrfanView"), ("krita", "Krita"), ("blender", "Blender") }),
                ("Documentos", new[] { ("libreoffice", "LibreOffice"), ("sumatrapdf", "SumatraPDF"), ("foxit", "Foxit Reader") }),
                ("Utilitários", new[] { ("7zip", "7-Zip"), ("winrar", "WinRAR"), ("everything", "Everything"), ("powertoys", "PowerToys"), ("wiztree", "WizTree"), ("teracopy", "TeraCopy"), ("revo", "Revo Uninstaller") }),
                ("Acesso remoto", new[] { ("anydesk", "AnyDesk"), ("teamviewer15", "TeamViewer"), ("rustdesk", "RustDesk") }),
                ("Armazenamento", new[] { ("googledrivefordesktop", "Google Drive"), ("dropbox", "Dropbox"), ("onedrive", "OneDrive") }),
                ("Segurança e senhas", new[] { ("malwarebytes", "Malwarebytes"), ("bitwarden", "Bitwarden"), ("keepassxc", "KeePassXC") }),
                ("Bibliotecas que jogos precisam", new[] { ("vcredistx15", "Visual C++ x64"), ("vcredist15", "Visual C++ x86"), (".netx8", ".NET Desktop 8"), ("adoptjavax21", "Java 21") }),
                ("Programação", new[] { ("vscode", "VS Code"), ("notepadplusplus", "Notepad++"), ("git", "Git"), ("pythonx3", "Python 3"), ("node.js24", "Node.js 24") }),
                ("IA", new[] { ("claude", "Claude"), ("chatgpt", "ChatGPT") }),
            };
            foreach (var (cat, apps) in ninite)
                foreach (var (id, name) in apps)
                    list.Add(W(Programs, cat, id, name, "", "ninite"));
            return list;
        }

        // Kit básico para PC recém-formatado
        static readonly string[] Essentials = { "Valve.Steam", "discord", "chrome", "vlc", "7zip", "vcredistx15", "vcredist15", ".netx8" };

        readonly List<Item> items = Catalog();
        readonly Dictionary<string, StackPanel> tabPanels = new();
        readonly TextBox search = new() { Tag = I18n.Tr("Buscar app…"), Width = 260 };
        readonly TextBlock count = Ui.T("", 14.5, weight: FontWeights.SemiBold);
        readonly StackPanel progress = new();
        string currentTab = Games;
        bool busy;

        public AppsPage() : base("Instalar apps", "Launchers de jogos, apps ArtCraft e programas do dia a dia. Marque o que quiser e instale tudo de uma vez, sem propaganda.")
        {
            search.Style = (Style)Application.Current.FindResource("Search");
            search.TextChanged += (_, _) => Filter();

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var t in new[] { Games, Art, Programs })
            {
                var rb = new RadioButton { Style = (Style)Application.Current.FindResource("Tab"), GroupName = "apps-tab", Content = I18n.Tr(t), IsChecked = t == Games };
                rb.Checked += (_, _) => { currentTab = t; Filter(); };
                tabs.Children.Add(rb);
            }
            var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            DockPanel.SetDock(search, Dock.Right);
            bar.Children.Add(search);
            bar.Children.Add(tabs);
            Body.Children.Add(bar);

            Body.Children.Add(Ui.Card(null,
                count,
                Ui.Buttons(
                    Ui.BtnAsync("Instalar selecionados", Install, "primary", ""),
                    Ui.Btn("Kit básico pós-formatação", () => { foreach (var i in items) i.Box.IsChecked = !i.Installed && Essentials.Contains(i.Id); }, icon: ""),
                    Ui.Btn("Limpar seleção", () => { foreach (var i in items) i.Box.IsChecked = false; })),
                progress));

            foreach (var t in new[] { Games, Art, Programs })
            {
                var panel = new StackPanel();
                foreach (var cat in items.Where(i => i.Tab == t).GroupBy(i => i.Category))
                {
                    panel.Children.Add(Ui.T(cat.Key, 13, Ui.Sub, FontWeights.SemiBold, new Thickness(2, 8, 0, 8)));
                    var grid = new UniformGrid { Columns = t == Programs ? 4 : 3, Margin = new Thickness(0, 0, -10, 6) };
                    foreach (var i in cat) grid.Children.Add(Tile(i));
                    panel.Children.Add(grid);
                }
                tabPanels[t] = panel;
                Body.Children.Add(panel);
            }
            Body.Children.Add(Ui.Hint("Launchers e ArtCraft instalam pelo winget, o instalador oficial do Windows, de forma silenciosa. Programas usam o Ninite. " +
                                      "Alguns instaladores pedem permissão de administrador.", new Thickness(2, 6, 0, 0)));
            Filter();
            Count();
        }

        Border Tile(Item i)
        {
            var name = Ui.T(i.Name, 14, weight: FontWeights.SemiBold);
            var content = new StackPanel();
            content.Children.Add(name);
            if (i.Desc.Length > 0) content.Children.Add(Ui.Hint(i.Desc));

            i.Box = new CheckBox { Style = (Style)Application.Current.FindResource("Check"), Content = content };
            i.Box.Checked += (_, _) => { Highlight(i); Count(); };
            i.Box.Unchecked += (_, _) => { Highlight(i); Count(); };
            i.Badge = new ContentControl { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 0, 0, 0) };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(i.Box);
            Grid.SetColumn(i.Badge, 1);
            g.Children.Add(i.Badge);

            i.Tile = new Border
            {
                Style = (Style)Application.Current.FindResource("Card"),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 10, 10),
                Cursor = Cursors.Hand,
                Child = g,
            };
            // Clicar em qualquer parte do cartão marca/desmarca (o clique na própria caixa já vem tratado)
            i.Tile.MouseLeftButtonUp += (_, e) =>
            {
                if (!e.Handled && i.Box.IsEnabled) i.Box.IsChecked = i.Box.IsChecked != true;
            };
            return i.Tile;
        }

        void Highlight(Item i) =>
            i.Tile.BorderBrush = i.Box.IsChecked == true ? Ui.Accent : (Brush)Application.Current.FindResource("CardBorder");

        void SetBadge(Item i, string text, Brush color) => i.Badge.Content = Ui.Pill(text, color);

        void Filter()
        {
            var q = search.Text.Trim();
            foreach (var (tab, panel) in tabPanels)
                panel.Visibility = tab == currentTab ? Visibility.Visible : Visibility.Collapsed;
            foreach (var i in items)
                i.Tile.Visibility = q.Length == 0 || i.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || i.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
            // Se a busca não achar nada na aba atual, mas achar em outra, mostra a outra
            if (q.Length > 0 && !items.Any(i => i.Tab == currentTab && i.Tile.Visibility == Visibility.Visible))
            {
                var other = items.FirstOrDefault(i => i.Tile.Visibility == Visibility.Visible);
                if (other != null) foreach (var (tab, panel) in tabPanels) panel.Visibility = tab == other.Tab ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void Count()
        {
            var sel = items.Where(i => i.Box.IsChecked == true).ToList();
            count.Text = sel.Count == 0 ? I18n.Tr("Nenhum app selecionado") : I18n.Tr("{0} selecionado(s): {1}", sel.Count, string.Join(", ", sel.Select(i => I18n.Tr(i.Name)).Take(8)) + (sel.Count > 8 ? "…" : ""));
        }

        public override Task LoadAsync() => Busy("Verificando o que já está instalado…", async () =>
        {
            if (!await Winget.IsAvailableAsync())
            {
                progress.Children.Clear();
                progress.Children.Add(Ui.Hint("O winget não foi encontrado. Em PC recém-formatado ele aparece depois que a Microsoft Store atualiza o \"Instalador de Aplicativo\".", new Thickness(0, 8, 0, 4)));
                progress.Children.Add(Ui.Buttons(Ui.Btn("Abrir Instalador de Aplicativo na Store", () => Admin.Open("ms-windows-store://pdp/?productid=9NBLGGH4NNS1"), icon: "")));
                return;
            }
            var installed = await Winget.InstalledAsync();
            foreach (var i in items)
            {
                i.Installed = i.Source switch
                {
                    "artcraft" => ArtCraft.LauncherInstalled(),
                    "ninite" => false,
                    _ => installed.Contains(i.Id),
                };
                if (i.Installed)
                {
                    SetBadge(i, "Instalado", Ui.Good);
                    i.Box.IsChecked = false;
                    i.Tile.Opacity = 0.75;
                }
            }
        });

        async Task Install()
        {
            if (busy) return;
            var sel = items.Where(i => i.Box.IsChecked == true).ToList();
            if (sel.Count == 0) { Ui.Info("Selecione pelo menos um app."); return; }
            busy = true;
            progress.Children.Clear();
            int ok = 0, fail = 0;
            try
            {
                // Um por vez pelo winget/GitHub, com status em cada cartão
                foreach (var i in sel.Where(i => i.Source != "ninite"))
                {
                    SetBadge(i, "Instalando…", Ui.Accent);
                    Status($"Instalando {i.Name}… ({ok + fail + 1} de {sel.Count(x => x.Source != "ninite")})");
                    (bool Ok, string Message) r;
                    try { r = i.Source == "artcraft" ? await ArtCraft.InstallLauncherAsync() : await Winget.InstallAsync(i.Id, i.Source); }
                    catch (Exception ex) { r = (false, ex.Message); }
                    if (r.Ok) { ok++; i.Installed = true; i.Box.IsChecked = false; SetBadge(i, "Instalado", Ui.Good); }
                    else { fail++; SetBadge(i, r.Message, Ui.Bad); }
                    progress.Children.Add(Ui.KV(i.Name, r.Message, r.Ok ? Ui.Good : Ui.Bad));
                }

                var nin = sel.Where(i => i.Source == "ninite").ToList();
                if (nin.Count > 0)
                {
                    Status("Baixando o instalador do Ninite…");
                    await RunNinite(nin);
                    foreach (var i in nin) { i.Box.IsChecked = false; SetBadge(i, "Pelo Ninite", Ui.Accent); }
                    progress.Children.Add(Ui.KV($"{nin.Count} programa(s)", "Instalador do Ninite aberto, acompanhe a janela dele", Ui.Accent));
                }
                Status("");
                Ui.Info(fail == 0 ? "Instalação concluída!" : $"{ok} instalado(s), {fail} com problema. Veja a lista.");
            }
            finally { busy = false; }
        }

        async Task RunNinite(List<Item> apps)
        {
            var url = $"https://ninite.com/{string.Join("-", apps.Select(a => Uri.EscapeDataString(a.Id)))}/ninite.exe";
            var dir = Path.Combine(Path.GetTempPath(), "Bastion");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "Ninite Instalador.exe");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Bastion/1.1");
            var bytes = await http.GetByteArrayAsync(url);
            // Um .exe válido começa com "MZ"; se vier uma página de erro, avisa
            if (bytes.Length < 2 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
                throw new Exception("O Ninite não devolveu um instalador. Algum dos apps pode ter saído do catálogo.");
            await File.WriteAllBytesAsync(file, bytes);
            try { Process.Start(new ProcessStartInfo(file) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
