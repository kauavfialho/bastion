using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bastion.Core
{
    /// <summary>
    /// Mouse, teclado, fone, microfone, controle e webcam ligados no PC, com o app oficial de cada marca.
    /// Só marcas conhecidas recebem indicação de app; marca genérica ou desconhecida recebe
    /// "procure no site do fabricante" (muitos apps de marcas genéricas vêm de sites duvidosos).
    /// </summary>
    public static class Peripherals
    {
        /// <summary>App oficial de uma marca. Detect: nome do programa instalado (Painel de Controle ou Microsoft Store).</summary>
        public sealed record Maker(string Brand, string App, string Url, string Detect, string Note);

        static readonly Maker GHub = new("Logitech G", "G HUB", "https://www.logitechg.com/innovation/g-hub.html", @"Logitech G HUB", "DPI, botões e luzes. As configurações podem ficar salvas no próprio aparelho.");
        static readonly Maker LogiOptions = new("Logitech", "Logi Options+", "https://www.logitech.com/software/logi-options-plus.html", @"Logi Options", "Botões, rolagem e atalhos de mouses e teclados Logitech (linha MX e comuns).");
        static readonly Maker AstroCc = new("Astro", "ASTRO Command Center", "ms-windows-store://search/?query=ASTRO%20Command%20Center", @"ASTRO Command Center|AstroCommandCenter", "Equalizador e volume do MixAmp/A40/A50. Baixe pela Microsoft Store.");
        static readonly Maker Icue = new("Corsair", "iCUE", "https://www.corsair.com/us/en/s/icue", @"Corsair iCUE", "Luzes, DPI e macros da Corsair.");
        static readonly Maker Synapse = new("Razer", "Razer Synapse", "https://www.razer.com/synapse-4", @"Razer Synapse|RazerAppEngine", "DPI, macros e luzes da Razer.");
        static readonly Maker SteelGg = new("SteelSeries", "SteelSeries GG", "https://steelseries.com/gg", @"SteelSeries GG|SteelSeries Engine", "Ajustes e equalizador da SteelSeries.");
        static readonly Maker Ngenuity = new("HyperX", "HyperX NGENUITY", "https://hyperx.com/pages/ngenuity", @"NGENUITY", "Luzes, DPI e áudio da HyperX.");
        static readonly Maker Swarm = new("Turtle Beach / ROCCAT", "Turtle Beach Swarm II", "https://www.turtlebeach.com/swarm", @"Swarm", "Ajustes dos aparelhos Turtle Beach e ROCCAT.");
        static readonly Maker Armoury = new("ASUS ROG", "Armoury Crate", "https://rog.asus.com/armoury-crate/", @"Armoury Crate", "Periféricos e placas ASUS ROG.");
        static readonly Maker MsiCenter = new("MSI", "MSI Center", "https://www.msi.com/Landing/MSI-Center", @"MSI Center", "Periféricos e placas MSI.");
        static readonly Maker Steam = new("Valve", "Steam", "https://store.steampowered.com/about/", @"^Steam$", "O controle é configurado dentro da Steam.");
        static readonly Maker XboxAcc = new("Microsoft (Xbox)", "Acessórios Xbox", "ms-windows-store://search/?query=Xbox%20Accessories", @"XboxAccessories|Acessórios Xbox|Xbox Accessories", "Botões e atualização do controle. Baixe pela Microsoft Store.");
        static readonly Maker PsAcc = new("Sony (PlayStation)", "Atualizador do controle", "https://controller.dl.playstation.net/controller/lang/en/fwupdater.html", @"PlayStation.*(Accessories|Firmware)|Wireless Controller", "Não precisa de app para jogar. Use só para atualizar o controle.");
        static readonly Maker Elgato = new("Elgato", "Camera Hub / Wave Link", "https://www.elgato.com/downloads", @"Elgato", "Ajustes de câmera e microfone Elgato.");
        static readonly Maker Jabra = new("Jabra", "Jabra Direct", "https://www.jabra.com/software-and-services/jabra-direct", @"Jabra Direct", "Atualizações e ajustes dos fones Jabra.");
        static readonly Maker Wootility = new("Wooting", "Wootility", "https://wootility.io", @"Wootility", "Teclas analógicas e luzes do Wooting.");
        static readonly Maker Keychron = new("Keychron", "Keychron Launcher (no navegador)", "https://launcher.keychron.com", @"^$", "Configura o teclado direto no navegador, sem instalar nada.");
        static readonly Maker Focusrite = new("Focusrite", "Focusrite Control", "https://focusrite.com/downloads", @"Focusrite Control", "Ganho e roteamento da interface de áudio.");
        static readonly Maker Rode = new("RØDE", "RØDE Central", "https://rode.com/en/apps/rode-central", @"RODE Central|RØDE Central", "Atualizações e ajustes dos microfones RØDE.");
        static readonly Maker Nzxt = new("NZXT", "NZXT CAM", "https://nzxt.com/software/cam", @"NZXT CAM", "Periféricos, fans e luzes NZXT.");

        // Marca pelo código USB do fabricante (VID). Chips genéricos ficam de fora de propósito.
        static readonly Dictionary<string, Func<string, Maker?>> ByVid = new(StringComparer.OrdinalIgnoreCase)
        {
            ["046D"] = n => Regex.IsMatch(n, @"(?i)\bG\s?\d{3}|\bPRO\b|LIGHTSPEED|Astro|Yeti|\bG HUB") ? GHub : LogiOptions,
            ["B58E"] = _ => GHub,                                   // Blue (Yeti), hoje da Logitech
            ["9886"] = n => Regex.IsMatch(n, @"(?i)A50 X|A30") ? GHub : AstroCc,
            ["1B1C"] = _ => Icue,
            ["1532"] = _ => Synapse,
            ["1038"] = _ => SteelGg,
            ["0951"] = _ => Ngenuity,                               // HyperX (era da Kingston)
            ["03F0"] = n => Regex.IsMatch(n, "(?i)hyperx") ? Ngenuity : null,   // HP: só a linha HyperX
            ["1E7D"] = _ => Swarm,                                  // ROCCAT
            ["10F5"] = _ => Swarm,                                  // Turtle Beach
            ["0B05"] = _ => Armoury,
            ["1462"] = _ => MsiCenter,
            ["0DB0"] = _ => MsiCenter,
            ["28DE"] = _ => Steam,
            ["045E"] = n => Regex.IsMatch(n, "(?i)xbox|controller|controle|gamepad") ? XboxAcc : null,
            ["054C"] = _ => PsAcc,
            ["0FD9"] = _ => Elgato,
            ["0B0E"] = _ => Jabra,
            ["31E3"] = _ => Wootility,
            ["3434"] = _ => Keychron,
            ["1235"] = _ => Focusrite,
            ["19F7"] = _ => Rode,
            ["1E71"] = _ => Nzxt,
        };

        // Bluetooth não tem VID confiável: a marca vem do nome do aparelho
        static readonly (string Rx, Maker App)[] ByName =
        {
            (@"(?i)\bMX\b|Logitech|\bM\d{3}\b|\bK\d{3}\b|POP (Mouse|Keys)", LogiOptions),
            (@"(?i)\bG\d{3}\b|\bPRO X\b|Logitech G", GHub),
            (@"(?i)Razer", Synapse), (@"(?i)Corsair", Icue), (@"(?i)SteelSeries|Arctis|Aerox", SteelGg),
            (@"(?i)HyperX", Ngenuity), (@"(?i)Xbox", XboxAcc), (@"(?i)DualSense|DUALSHOCK|Wireless Controller", PsAcc),
            (@"(?i)Jabra", Jabra), (@"(?i)Keychron", Keychron), (@"(?i)Astro A\d", AstroCc),
        };

        public sealed record Device(string Kind, string Name, Maker? App, bool Installed, bool Wireless);

        static readonly HashSet<string> Virtual = new(StringComparer.OrdinalIgnoreCase) { "046D:C231", "046D:C232" }; // teclado/mouse virtual do G HUB
        static readonly Regex Generic = new(@"(?i)^(USB Composite Device|Dispositivo|HID|Mouse compatível|Teclado|Receptor USB|USB Receiver|USB Input Device|USB Audio Device|Dispositivo de áudio USB)|compatível com HID|compliant|LIGHTSPEED Receiver|Unifying Receiver");

        public static async Task<List<Device>> DetectAsync()
        {
            var r = await Ps.ObjAsync(@"
$d = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'OK' -and $_.InstanceId -match '^(USB|HID)\\VID_|^BTH' })
$usb = @($d | Where-Object { $_.InstanceId -match '^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\' })
$bus = @{}
if ($usb.Count) { Get-PnpDeviceProperty -InstanceId $usb.InstanceId -KeyName DEVPKEY_Device_BusReportedDeviceDesc -ErrorAction SilentlyContinue | ForEach-Object { $bus[$_.InstanceId] = ""$($_.Data)"" } }
$names = @()
foreach ($root in 'HKCU:','HKLM:') { foreach ($p in 'Software\Microsoft\Windows\CurrentVersion\Uninstall','Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall') {
  Get-ItemProperty ""$root\$p\*"" -ErrorAction SilentlyContinue | ForEach-Object { if ($_.DisplayName) { $names += ""$($_.DisplayName)"" } } } }
$names += @(Get-AppxPackage -ErrorAction SilentlyContinue | ForEach-Object Name)
[pscustomobject]@{
  Devices = @($d | ForEach-Object {
    $v = [regex]::Match($_.InstanceId, 'VID_([0-9A-F]{4})&PID_([0-9A-F]{4})')
    [pscustomobject]@{ Id = ""$($_.InstanceId)""; Vid = $v.Groups[1].Value; Pid = $v.Groups[2].Value; Class = ""$($_.Class)""; Name = ""$($_.FriendlyName)""; Bus = ""$($bus[$_.InstanceId])"" } })
  Installed = $names
}", 60000);

            var installed = r.A("Installed").Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "").ToArray();
            bool IsInstalled(Maker m) => m.Detect != "^$" && installed.Any(n => Regex.IsMatch(n, m.Detect, RegexOptions.IgnoreCase));

            var devs = r.A("Devices").ToList();
            var result = new List<Device>();

            // USB: um item por aparelho (VID+PID)
            foreach (var g in devs.Where(x => x.S("Vid").Length == 4).GroupBy(x => x.S("Vid") + ":" + x.S("Pid")))
            {
                if (Virtual.Contains(g.Key)) continue;
                var classes = g.Select(x => x.S("Class")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var names = g.SelectMany(x => new[] { x.S("Name"), x.S("Bus") }).Where(n => n.Length > 0).ToList();
                var all = string.Join(" | ", names);
                string Pick(params string[] prefer) =>
                    prefer.SelectMany(c => g.Where(x => x.S("Class").Equals(c, StringComparison.OrdinalIgnoreCase)).Select(x => x.S("Name")))
                          .Concat(names).FirstOrDefault(n => n.Length > 0 && !Generic.IsMatch(n)) ?? "";
                bool receiver = Regex.IsMatch(all, "(?i)receiver|receptor|lightspeed|unifying|dongle|wireless");

                string? kind = null; string name = "";
                if (classes.Contains("Camera") || classes.Contains("Image")) { kind = "Webcam"; name = Pick("Camera", "Image"); }
                else if (classes.Contains("MEDIA"))
                {
                    name = Pick("MEDIA");
                    bool headset = Regex.IsMatch(all, @"(?i)headset|fone|mixamp|arctis|cloud|\bA\d0\b|kraken|blackshark|virtuoso|\bHS\d|game\b|chat\b");
                    bool mic = Regex.IsMatch(all, @"(?i)mic|yeti|wave|podcast|quadcast|seiren|\bNT-USB|\bSM7");
                    kind = mic && !headset ? "Microfone" : "Fone / headset";
                    name = Regex.Replace(name, @"\s+(Voice|Game|Chat)$", "", RegexOptions.IgnoreCase);
                }
                else if (classes.Contains("XnaComposite") || classes.Contains("XboxComposite") || Regex.IsMatch(all, @"(?i)controller|controle|gamepad|joystick|DualSense") || g.Key.StartsWith("054C", StringComparison.OrdinalIgnoreCase))
                { kind = "Controle"; name = Pick("XnaComposite", "HIDClass"); }
                else if (classes.Contains("Keyboard") || classes.Contains("Mouse"))
                {
                    name = Pick("USB");
                    bool kb = Regex.IsMatch(name, @"(?i)keyboard|teclado|\bTKL\b|\bK\d{2,3}\b|keys|\b(60|65|75)%");
                    bool ms = Regex.IsMatch(name, @"(?i)mouse|superlight|deathadder|viper|basilisk|\bG\d{3}\b|\bM\d{3}\b|\bMX (Master|Anywhere)");
                    kind = kb ? "Teclado" : ms ? "Mouse" : receiver ? "Receptor sem fio" : classes.Contains("Mouse") && !classes.Contains("Keyboard") ? "Mouse" : "Teclado";
                }
                if (kind == null) continue;   // controladoras de luz, chips, hubs: não são periféricos de mesa

                var vid = g.Key[..4];
                var app = ByVid.TryGetValue(vid, out var f) ? f(all) : null;
                if (name.Length == 0) name = kind == "Receptor sem fio" ? "Receptor USB" : I18n.Tr("Modelo não informado");
                result.Add(new Device(kind, name, app, app != null && IsInstalled(app), receiver));
            }

            // Bluetooth: só o que tem cara de periférico
            foreach (var x in devs.Where(x => x.S("Id").StartsWith("BTH", StringComparison.OrdinalIgnoreCase)))
            {
                var name = x.S("Name");
                if (name.Length == 0 || Generic.IsMatch(name) || Regex.IsMatch(name, @"(?i)enumerator|serviço|service|protocol|transport|avrcp|gatt|generic|rfcomm|^Bluetooth|Hands-Free|Mãos livres|Audio/Video Remote")) continue;
                string? kind = x.S("Class") switch
                {
                    "Mouse" => "Mouse",
                    "Keyboard" => "Teclado",
                    "AudioEndpoint" or "MEDIA" => "Fone / headset",
                    _ => Regex.IsMatch(name, "(?i)mouse|\\bMX (Master|Anywhere)") ? "Mouse"
                       : Regex.IsMatch(name, "(?i)keyboard|keys|teclado") ? "Teclado"
                       : Regex.IsMatch(name, "(?i)buds|headphone|headset|fone|airpods|\\bWH-|\\bWF-|arctis") ? "Fone / headset"
                       : Regex.IsMatch(name, "(?i)controller|controle|DualSense|Xbox") ? "Controle" : null,
                };
                if (kind == null || result.Any(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                var app = ByName.FirstOrDefault(b => Regex.IsMatch(name, b.Rx)).App;
                result.Add(new Device(kind, name, app, app != null && IsInstalled(app), true));
            }

            var order = new[] { "Mouse", "Teclado", "Receptor sem fio", "Fone / headset", "Microfone", "Controle", "Webcam" };
            return result.OrderBy(d => Array.IndexOf(order, d.Kind)).ThenBy(d => d.Name).ToList();
        }
    }
}
