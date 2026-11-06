using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class DebloatPage : PageBase
    {
        // (pacote, nome amigável, marcado por padrão)
        static readonly (string Id, string Name, bool Default)[] Bloat =
        {
            ("Microsoft.BingNews", "Notícias", true),
            ("Microsoft.BingWeather", "Clima", true),
            ("Microsoft.BingSearch", "Bing Search", true),
            ("Microsoft.GetHelp", "Obter Ajuda", true),
            ("Microsoft.Getstarted", "Dicas", true),
            ("Microsoft.MicrosoftSolitaireCollection", "Coleção Paciência", true),
            ("Microsoft.People", "Pessoas", true),
            ("Microsoft.WindowsFeedbackHub", "Hub de Comentários", true),
            ("Microsoft.ZuneVideo", "Filmes e TV", true),
            ("Microsoft.MicrosoftOfficeHub", "Microsoft 365 (atalho do Office)", true),
            ("Microsoft.Todos", "Microsoft To Do", true),
            ("Clipchamp.Clipchamp", "Clipchamp", true),
            ("Microsoft.PowerAutomateDesktop", "Power Automate", true),
            ("Microsoft.549981C3F5F10", "Cortana", true),
            ("Microsoft.WindowsMaps", "Mapas", true),
            ("Microsoft.Windows.DevHome", "Dev Home", true),
            ("MicrosoftTeams", "Teams (versão antiga)", true),
            ("Microsoft.MixedReality.Portal", "Portal de Realidade Mista", true),
            ("Microsoft.SkypeApp", "Skype", true),
            ("king.com.CandyCrushSaga", "Candy Crush", true),
            ("king.com.CandyCrushSodaSaga", "Candy Crush Soda", true),
            ("MSTeams", "Teams (pessoal)", false),
            ("Microsoft.OutlookForWindows", "Outlook (novo)", false),
            ("Microsoft.Copilot", "Copilot", false),
            ("Microsoft.YourPhone", "Vincular ao Celular", false),
            ("Microsoft.WindowsAlarms", "Alarmes e Relógio", false),
            ("Microsoft.WindowsSoundRecorder", "Gravador de Som", false),
            ("Microsoft.ZuneMusic", "Media Player", false),
            ("SpotifyAB.SpotifyMusic", "Spotify (da Microsoft Store)", false),
        };

        sealed record Tweak(string Name, string Hint, bool Machine, Func<bool> IsOn, Action<bool> Apply);

        readonly StackPanel apps = new();
        readonly List<(CheckBox Box, string Id)> appBoxes = new();
        readonly CheckBox provisioned;
        readonly StackPanel tweaks = new();

        public DebloatPage() : base("Debloat", "Remove apps que vêm de fábrica e desliga anúncios e rastreamento do Windows. Tudo aqui pode ser desfeito.")
        {
            provisioned = Ui.Check("Também impedir que voltem para novos usuários", "Remove o instalador desses apps do Windows (precisa de admin).", Admin.IsAdmin);
            Ui.NeedsAdmin(provisioned, "remover os apps também para novos usuários");

            Body.Children.Add(Ui.Card("1. Ponto de restauração",
                Ui.T("Antes de mexer, crie um ponto de restauração. Se algo der errado, o Windows volta para este momento.", 14, Ui.Sub),
                AdminNote("criar o ponto de restauração"),
                Ui.Buttons(
                    Ui.BtnAsync("Criar ponto de restauração", CreateRestorePoint, "primary", ""),
                    Ui.Btn("Abrir Restauração do Sistema", () => Admin.Open("rstrui.exe"), icon: ""))));

            Body.Children.Add(Ui.Card("2. Apps pré-instalados", Ui.Hint("Só aparecem os que estão instalados neste PC. Dá para reinstalar qualquer um pela Microsoft Store.", new Thickness(0, 0, 0, 10)),
                apps, provisioned,
                Ui.Buttons(
                    Ui.BtnAsync("Remover selecionados", RemoveApps, "danger", ""),
                    Ui.Btn("Marcar recomendados", () => { foreach (var (b, id) in appBoxes) b.IsChecked = Bloat.First(x => x.Id == id).Default; }),
                    Ui.Btn("Desmarcar todos", () => { foreach (var (b, _) in appBoxes) b.IsChecked = false; }))));

            Body.Children.Add(Ui.Card("3. Anúncios, sugestões e privacidade", tweaks));
        }

        public override Task LoadAsync() => Busy("Procurando apps instalados…", async () =>
        {
            var installed = (await Ps.ListAsync("Get-AppxPackage | ForEach-Object { $_.Name }"))
                .Select(e => e.GetString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            apps.Children.Clear();
            appBoxes.Clear();
            var present = Bloat.Where(b => installed.Contains(b.Id)).ToList();
            if (present.Count == 0) apps.Children.Add(Ui.T("Nenhum app desnecessário encontrado. 🎉", 14, Ui.Good));
            var grid = Ui.Columns(2);
            foreach (var b in present)
            {
                var cb = Ui.Check(b.Name, b.Default ? null : "Opcional", b.Default);
                cb.Margin = new Thickness(0, 4, 14, 8);
                appBoxes.Add((cb, b.Id));
                grid.Children.Add(cb);
            }
            apps.Children.Add(grid);
            LoadTweaks();
        });

        async Task CreateRestorePoint()
        {
            if (!Admin.IsAdmin) { Ui.Info("Abra o Bastion como administrador para criar o ponto de restauração."); return; }
            await Busy("Criando ponto de restauração…", async () =>
            {
                var r = await Ps.RunAsync(@"
try { Enable-ComputerRestore -Drive ""$env:SystemDrive\"" -ErrorAction SilentlyContinue } catch {}
try { Checkpoint-Computer -Description 'Bastion' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; 'ok' } catch { $_.Exception.Message }", 600000);
                Ui.Info(r == "ok" ? "Ponto de restauração criado." :
                    "O Windows não criou o ponto agora: " + r + "\n\n(O Windows só permite um ponto a cada 24 horas.)");
            });
        }

        async Task RemoveApps()
        {
            var sel = appBoxes.Where(a => a.Box.IsChecked == true).Select(a => a.Id).ToList();
            if (sel.Count == 0) { Ui.Info("Selecione pelo menos um app."); return; }
            if (!Ui.Confirm($"Remover {sel.Count} app(s)? Dá para reinstalar depois pela Microsoft Store.")) return;
            var ids = string.Join(",", sel.Select(Ps.Q));
            var prov = provisioned.IsChecked == true && Admin.IsAdmin;
            await Busy("Removendo apps…", async () =>
            {
                var res = await Ps.RunAsync($@"
$ids = @({ids}); $ok = 0; $fail = @()
foreach ($id in $ids) {{
  try {{ Get-AppxPackage -Name $id | Remove-AppxPackage -ErrorAction Stop; $ok++ }} catch {{ $fail += $id }}
  {(prov ? "Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq $id | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null" : "")}
}}
""$ok|$($fail -join ', ')""", 900000);
                var parts = res.Split('|');
                Ui.Info($"{parts[0]} app(s) removido(s)." + (parts.Length > 1 && parts[1].Length > 0 ? $"\nNão foi possível remover: {parts[1]}" : ""));
            });
            await LoadAsync();
        }

        // ---------- Ajustes ----------

        static object? Get(RegistryKey hive, string key, string name)
        {
            using var k = hive.OpenSubKey(key);
            return k?.GetValue(name);
        }

        static void Set(RegistryKey hive, string key, string name, int value)
        {
            using var k = hive.CreateSubKey(key);
            k.SetValue(name, value, RegistryValueKind.DWord);
        }

        static void Del(RegistryKey hive, string key, string name)
        {
            using var k = hive.OpenSubKey(key, true);
            k?.DeleteValue(name, false);
        }

        const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        static readonly string[] CdmValues =
        {
            "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled", "SubscribedContent-353694Enabled",
            "SubscribedContent-353696Enabled", "SubscribedContent-310093Enabled", "SystemPaneSuggestionsEnabled",
            "SilentInstalledAppsEnabled", "SoftLandingEnabled", "PreInstalledAppsEnabled", "OemPreInstalledAppsEnabled",
        };
        const string ClassicMenu = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        static List<Tweak> AllTweaks() => new()
        {
            new("Desligar anúncios e sugestões", "Tira propagandas do Iniciar, da tela de bloqueio e das Configurações, e impede apps instalados sem pedir.", false,
                () => Get(Registry.CurrentUser, Cdm, "SystemPaneSuggestionsEnabled") is int v && v == 0,
                on => { foreach (var n in CdmValues) Set(Registry.CurrentUser, Cdm, n, on ? 0 : 1); }),
            new("Desligar ID de anúncios", "Apps deixam de usar um identificador seu para mostrar anúncios personalizados.", false,
                () => Get(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") is int v && v == 0,
                on => Set(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", on ? 0 : 1)),
            new("Desligar experiências personalizadas", "A Microsoft deixa de usar seus dados de diagnóstico para dicas e ofertas.", false,
                () => Get(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled") is int v && v == 0,
                on => Set(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", on ? 0 : 1)),
            new("Tirar resultados da web (Bing) do Iniciar", "A pesquisa do Iniciar mostra só o que está no PC. Vale depois de reiniciar.", false,
                () => Get(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") is int v && v == 1,
                on => { if (on) Set(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1); else Del(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"); }),
            new("Mostrar extensões dos arquivos", "Mostra .exe, .jpg, .pdf… Ajuda a não cair em arquivo falso.", false,
                () => Get(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt") is int v && v == 0,
                on => Set(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", on ? 0 : 1)),
            new("Menu de botão direito clássico (Windows 11)", "Volta o menu completo, sem precisar clicar em \"Mostrar mais opções\". Vale depois de reiniciar.", false,
                () => Registry.CurrentUser.OpenSubKey(ClassicMenu + @"\InprocServer32") != null,
                on =>
                {
                    if (on) { using var k = Registry.CurrentUser.CreateSubKey(ClassicMenu + @"\InprocServer32"); k.SetValue("", ""); }
                    else Registry.CurrentUser.DeleteSubKeyTree(ClassicMenu, false);
                }),
            new("Telemetria no mínimo", "Envia à Microsoft só o necessário para o Windows funcionar.", true,
                () => Get(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry") is int v && v <= 1,
                on => { if (on) Set(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1); else Del(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry"); }),
        };

        void LoadTweaks()
        {
            tweaks.Children.Clear();
            tweaks.Children.Add(AdminNote("aplicar os ajustes do sistema"));
            var all = AllTweaks();
            foreach (var t in all)
            {
                var sw = Ui.Switch(t.Name, t.Hint, SafeIsOn(t));
                if (t.Machine) Ui.NeedsAdmin(sw, "mudar esse ajuste do sistema");
                sw.Checked += (_, _) => Apply(t, true);
                sw.Unchecked += (_, _) => Apply(t, false);
                tweaks.Children.Add(sw);
            }
            tweaks.Children.Add(Ui.Buttons(Ui.Btn("Aplicar todos", () =>
            {
                foreach (var t in all.Where(t => !t.Machine || Admin.IsAdmin)) Apply(t, true, quiet: true);
                LoadTweaks();
            }, "primary", "")));
        }

        static bool SafeIsOn(Tweak t) { try { return t.IsOn(); } catch { return false; } }

        static void Apply(Tweak t, bool on, bool quiet = false)
        {
            try { t.Apply(on); }
            catch (Exception ex) { if (!quiet) Ui.Info("Não foi possível aplicar: " + ex.Message); }
        }
    }
}
