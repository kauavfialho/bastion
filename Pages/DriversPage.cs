using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class DriversPage : PageBase
    {
        readonly MainWindow main;
        readonly StackPanel problems = new();
        readonly StackPanel essentials = new();
        readonly StackPanel installed = new();

        // Códigos do Gerenciador de Dispositivos em português simples
        static readonly Dictionary<int, string> Codes = new()
        {
            [1] = "não está configurado direito",
            [3] = "o driver está corrompido",
            [10] = "não consegue iniciar",
            [12] = "não achou recursos livres no sistema",
            [14] = "precisa reiniciar o PC",
            [18] = "precisa reinstalar o driver",
            [19] = "configuração do registro corrompida",
            [21] = "o Windows está removendo o dispositivo",
            [22] = "está desativado",
            [24] = "não está presente ou não funciona",
            [28] = "está sem driver instalado",
            [31] = "o driver não carregou",
            [32] = "o serviço do driver está desativado",
            [37] = "o driver falhou ao iniciar",
            [39] = "o driver está corrompido ou faltando",
            [43] = "o Windows parou o dispositivo porque ele relatou problema",
            [45] = "está desconectado",
            [52] = "o driver não tem assinatura digital válida",
        };

        public DriversPage(MainWindow main) : base("Drivers", "Dispositivos com problema, os drivers principais do seu PC e onde atualizar cada um.")
        {
            this.main = main;
            Body.Children.Add(Ui.Card("Dispositivos com problema", problems));
            Body.Children.Add(Ui.Card("Drivers principais", essentials));
            Body.Children.Add(Ui.Card("Drivers de fabricantes instalados", installed));
        }

        public override Task LoadAsync() => Busy("Lendo os dispositivos e drivers…", async () =>
        {
            var probT = Ps.ListAsync(@"
Get-CimInstance Win32_PnPEntity | Where-Object { $_.ConfigManagerErrorCode -ne 0 } | ForEach-Object {
  [pscustomobject]@{ Name = ""$($_.Name)""; Code = [int]$_.ConfigManagerErrorCode; Id = ""$($_.PNPDeviceID)""; Class = ""$($_.PNPClass)"" } }");
            var drvT = Ps.ListAsync(@"
Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.DriverProviderName -and $_.DriverProviderName -notmatch '^(Microsoft|Generic)' -and $_.DeviceName } | ForEach-Object {
  [pscustomobject]@{ Device = ""$($_.DeviceName)""; Provider = ""$($_.DriverProviderName)""; Version = ""$($_.DriverVersion)""; Class = ""$($_.DeviceClass)""
    Date = $(if ($_.DriverDate) { $_.DriverDate.ToString('yyyy-MM-dd') } else { '' }) } }");
            var sysT = Ps.ObjAsync(@"
$bb = Get-CimInstance Win32_BaseBoard; $cs = Get-CimInstance Win32_ComputerSystem
[pscustomobject]@{ Maker = ""$($bb.Manufacturer)""; Board = ""$($bb.Product)""; SysMaker = ""$($cs.Manufacturer)""; SysModel = ""$($cs.Model)""
  Cpu = ""$((Get-CimInstance Win32_Processor | Select-Object -First 1).Name)""
  ChipsetPkg = ""$(Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* -ErrorAction SilentlyContinue | Where-Object DisplayName -match 'Chipset (Software|Driver|Device Software)' | Select-Object -First 1 | ForEach-Object { ""$($_.DisplayName) $($_.DisplayVersion)"" })""
  Gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object { [pscustomobject]@{ Name = ""$($_.Name)""; Version = ""$($_.DriverVersion)""; Date = $(if ($_.DriverDate) { $_.DriverDate.ToString('yyyy-MM-dd') } else { '' }) } }) }");
            await Task.WhenAll(probT, drvT, sysT);
            BuildProblems(probT.Result);
            BuildEssentials(sysT.Result, drvT.Result);
            BuildInstalled(drvT.Result);
        });

        void BuildProblems(List<JsonElement> list)
        {
            problems.Children.Clear();
            if (list.Count == 0)
            {
                problems.Children.Add(Ui.T("Nenhum dispositivo com problema. Tudo funcionando.", 14, Ui.Good, FontWeights.SemiBold));
                return;
            }
            foreach (var d in list)
            {
                int code = (int)(d.D("Code") ?? 0);
                var what = Codes.TryGetValue(code, out var w) ? w : $"tem um problema (código {code})";
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var text = new StackPanel();
                text.Children.Add(Ui.T(d.S("Name") is { Length: > 0 } n ? n : "Dispositivo desconhecido", 14, weight: FontWeights.SemiBold));
                text.Children.Add(Ui.Hint($"Este dispositivo {what}. (código {code}{(d.S("Class").Length > 0 ? " · " + d.S("Class") : "")})"));
                row.Children.Add(text);
                var id = d.S("Id");
                var fix = Ui.BtnAsync("Reiniciar dispositivo", async () =>
                {
                    var r = await Ps.RunAsync($"pnputil /restart-device {Ps.Q(id)} 2>&1 | Out-String");
                    Ui.Info(r.Contains("rro") || r.Contains("ail") ? "Não foi possível reiniciar o dispositivo. Tente atualizar o driver pelo site do fabricante." : "Dispositivo reiniciado.");
                    await LoadAsync();
                });
                Ui.NeedsAdmin(fix, "reiniciar dispositivos");
                Grid.SetColumn(fix, 1);
                row.Children.Add(fix);
                problems.Children.Add(row);
            }
            problems.Children.Add(Ui.Buttons(
                Ui.Btn("Gerenciador de Dispositivos", () => Admin.Open("devmgmt.msc"), icon: ""),
                Ui.Btn("Drivers opcionais do Windows Update", () => Admin.Open("ms-settings:windowsupdate-optionalupdates"), icon: "")));
        }

        static int AgeMonths(string date) => DateTime.TryParse(date, out var d) ? (int)((DateTime.Today - d).TotalDays / 30.4) : 0;

        void BuildEssentials(JsonElement sys, List<JsonElement> drivers)
        {
            essentials.Children.Clear();

            foreach (var g in sys.A("Gpus"))
            {
                var name = g.S("Name");
                var age = AgeMonths(g.S("Date"));
                (string label, string url) link =
                    name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? ("NVIDIA App (atualiza sozinho)", "https://www.nvidia.com/pt-br/software/nvidia-app/") :
                    name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ? ("AMD Software: Adrenalin", "https://www.amd.com/pt/support/download/drivers.html") :
                    name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? ("Intel Driver & Support Assistant", "https://www.intel.com.br/content/www/br/pt/support/detect.html") :
                    ("Site do fabricante", "https://www.google.com/search?q=" + Uri.EscapeDataString(name + " driver"));
                essentials.Children.Add(Row("", $"Vídeo: {name}", $"Versão {g.S("Version")}" + (g.S("Date").Length > 0 ? $", de {DateTime.Parse(g.S("Date")):dd/MM/yyyy}" : ""),
                    age > 4 ? 1 : 0, age > 4 ? "Mais de 4 meses: vale atualizar, cada versão traz melhorias para jogos novos." : null,
                    Ui.Btn(link.label, () => Admin.Open(link.url), icon: "")));
            }

            var cpu = sys.S("Cpu");
            bool amd = cpu.Contains("AMD", StringComparison.OrdinalIgnoreCase);
            // O fabricante aparece como "Advanced Micro Devices", não "AMD"; e o pacote oficial aparece nos programas instalados
            var rx = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            var chipDrivers = drivers.Where(d => System.Text.RegularExpressions.Regex.IsMatch(d.S("Provider"), amd ? "AMD|Advanced Micro Devices" : "Intel", rx)
                && System.Text.RegularExpressions.Regex.IsMatch(d.S("Device"), amd ? "GPIO|SMBus|PSP|PCI" : "Chipset|SMBus|Management Engine|Serial IO", rx)).ToList();
            var pkg = sys.S("ChipsetPkg");
            var (chipText, chipLevel) = pkg.Length > 0 ? ($"Pacote oficial instalado: {pkg}", 0)
                : chipDrivers.Count > 0 ? ($"Só os drivers básicos do Windows Update ({string.Join(", ", chipDrivers.Select(d => d.S("Device").Replace("AMD ", "") + " " + d.S("Version")).Distinct().Take(3))})", 1)
                : ("Não instalado", 1);
            essentials.Children.Add(Row("", $"Chipset {(amd ? "AMD" : "Intel")}", chipText,
                chipLevel, amd ? "O pacote oficial da AMD melhora o gerenciamento de energia e o desempenho do Ryzen. Instale depois de formatar." : "Instale depois de formatar, pelo assistente da Intel.",
                Ui.Btn(amd ? "Drivers de chipset AMD" : "Intel Driver Assistant", () => Admin.Open(amd ? "https://www.amd.com/pt/support/download/drivers.html" : "https://www.intel.com.br/content/www/br/pt/support/detect.html"), icon: "")));

            var (brand, _, _, url) = FormatPage.MakerInfo(sys.S("Maker"), sys.S("Board"), sys.S("SysMaker"), sys.S("SysModel"));
            essentials.Children.Add(Row("", $"Placa-mãe: {sys.S("Maker")} {sys.S("Board")}", "Rede, áudio e BIOS da placa",
                0, "A página de suporte tem os drivers de rede, áudio e a BIOS mais nova.",
                Ui.Btn($"Suporte da {brand}", () => Admin.Open(url.Replace("#bios", "#driver")), icon: "")));

            essentials.Children.Add(Ui.Buttons(
                Ui.Btn("Drivers opcionais do Windows Update", () => Admin.Open("ms-settings:windowsupdate-optionalupdates"), icon: ""),
                Ui.Btn("Backup dos drivers", () => main.Navigate("Formatar PC"), icon: "")));
        }

        static Grid Row(string glyph, string title, string detail, int level, string? hint, Button action)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = Ui.Icon(glyph, 18, Ui.Accent);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 2, 0, 0);
            g.Children.Add(icon);
            var text = new StackPanel();
            text.Children.Add(Ui.T(title, 14, weight: FontWeights.SemiBold));
            text.Children.Add(Ui.T(detail, 13.5, Ui.Level(level)));
            if (hint != null) text.Children.Add(Ui.Hint(hint));
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            action.Margin = new Thickness(12, 0, 0, 0);
            action.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(action, 2);
            g.Children.Add(action);
            return g;
        }

        void BuildInstalled(List<JsonElement> drivers)
        {
            installed.Children.Clear();
            var groups = drivers.GroupBy(d => d.S("Provider")).OrderByDescending(g => g.Count()).ToList();
            installed.Children.Add(Ui.Hint($"{drivers.Count} drivers de {groups.Count} fabricantes (sem contar os da Microsoft).", new Thickness(0, 0, 0, 8)));
            foreach (var g in groups)
            {
                installed.Children.Add(Ui.T($"{g.Key}  ({g.Count()})", 13.5, Ui.Sub, FontWeights.SemiBold, new Thickness(0, 10, 0, 2)));
                foreach (var d in g.GroupBy(x => x.S("Device")).Select(x => x.First()).OrderBy(x => x.S("Device")).Take(12))
                {
                    var age = AgeMonths(d.S("Date"));
                    var when = d.S("Date").Length > 0 ? DateTime.Parse(d.S("Date")).ToString("dd/MM/yyyy") : "";
                    installed.Children.Add(Ui.KV(d.S("Device"), $"{d.S("Version")}  ·  {when}", age > 48 ? Ui.Warn : Ui.Sub));
                }
            }
        }
    }
}
