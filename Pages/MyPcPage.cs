using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Bastion.Core;

namespace Bastion.Pages
{
    /// <summary>Tudo o que o Windows consegue detectar no computador, num lugar só.</summary>
    public sealed class MyPcPage : PageBase
    {
        // Fabricantes de periféricos pelo código USB (VID)
        static readonly Dictionary<string, string> Vendors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["046D"] = "Logitech", ["1B1C"] = "Corsair", ["1E71"] = "NZXT", ["1532"] = "Razer", ["9886"] = "Astro",
            ["1038"] = "SteelSeries", ["0951"] = "HyperX", ["03F0"] = "HP/HyperX", ["3142"] = "Fifine", ["EBA4"] = "UGREEN",
            ["0B05"] = "ASUS", ["1462"] = "MSI", ["048D"] = "Placa-mãe (RGB)", ["2516"] = "Cooler Master", ["0C45"] = "Microdia",
            ["045E"] = "Microsoft", ["054C"] = "Sony (controle PlayStation)", ["057E"] = "Nintendo", ["28DE"] = "Valve", ["1915"] = "Receptor sem fio",
            ["258A"] = "Redragon/Sinowealth", ["3554"] = "Pulsar/Compx", ["373B"] = "Compx", ["0E8D"] = "MediaTek", ["8087"] = "Intel (Bluetooth)",
            ["0BDA"] = "Realtek", ["1A86"] = "Chip USB CH340", ["0403"] = "Chip USB FTDI",
        };

        // Aparelhos "de mentira" criados por programas (ex.: teclado/mouse virtual do Logitech G HUB)
        static readonly HashSet<string> VirtualDevices = new(StringComparer.OrdinalIgnoreCase) { "046D:C231", "046D:C232" };

        public MyPcPage() : base("Meu PC", "Tudo o que o Windows consegue detectar no seu computador: peças, periféricos e controladoras.") { }

        public override Task LoadAsync() => Busy("Identificando as peças do computador…", async () =>
        {
            var r = await Ps.ObjAsync(@"
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$bb = Get-CimInstance Win32_BaseBoard; $bios = Get-CimInstance Win32_BIOS; $cs = Get-CimInstance Win32_ComputerSystem
$slots = (Get-CimInstance Win32_PhysicalMemoryArray | Measure-Object MemoryDevices -Sum).Sum
$mon = @(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorID -ErrorAction SilentlyContinue | ForEach-Object {
  $n = -join ($_.UserFriendlyName | Where-Object { $_ -ne 0 } | ForEach-Object { [char]$_ }); $m = -join ($_.ManufacturerName | Where-Object { $_ -ne 0 } | ForEach-Object { [char]$_ }); ""$m $n"".Trim() })
[pscustomobject]@{
  Cpu = ""$($cpu.Name)"".Trim(); Cores = $cpu.NumberOfCores; Threads = $cpu.NumberOfLogicalProcessors; MaxMHz = $cpu.MaxClockSpeed
  Board = ""$($bb.Manufacturer) $($bb.Product)"".Trim(); Bios = ""$($bios.SMBIOSBIOSVersion)""; Pc = ""$($cs.Manufacturer) $($cs.Model)""; Slots = $slots
  Ram = @(Get-CimInstance Win32_PhysicalMemory | ForEach-Object { [pscustomobject]@{ Slot = ""$($_.DeviceLocator)""; Bank = ""$($_.BankLabel)""; GB = [math]::Round($_.Capacity/1GB); Part = ""$($_.PartNumber)"".Trim(); Maker = ""$($_.Manufacturer)"".Trim(); Speed = $_.ConfiguredClockSpeed; Type = $_.SMBIOSMemoryType } })
  Gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object { [pscustomobject]@{ Name = ""$($_.Name)""; Res = $(if ($_.CurrentHorizontalResolution) { ""$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution) a $($_.CurrentRefreshRate) Hz"" } else { '' }) } })
  Monitors = $mon
  Disks = @(Get-PhysicalDisk | ForEach-Object { [pscustomobject]@{ Name = ""$($_.FriendlyName)""; Media = ""$($_.MediaType)""; Bus = ""$($_.BusType)""; GB = [math]::Round($_.Size/1GB) } })
  Net = @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | ForEach-Object { [pscustomobject]@{ Name = ""$($_.InterfaceDescription)""; Status = ""$($_.Status)""; Speed = ""$($_.LinkSpeed)""; Wifi = ($_.PhysicalMediaType -match '802.11') } })
  Audio = @(Get-PnpDevice -Class AudioEndpoint -PresentOnly -ErrorAction SilentlyContinue | Where-Object Status -eq 'OK' | ForEach-Object { ""$($_.FriendlyName)"" })
  Usb = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match '^(USB|HID)\\VID_' -and $_.Status -eq 'OK' } | ForEach-Object {
    $v = [regex]::Match($_.InstanceId, 'VID_([0-9A-F]{4})&PID_([0-9A-F]{4})'); [pscustomobject]@{ Vid = $v.Groups[1].Value; Pid = $v.Groups[2].Value; Class = ""$($_.Class)""; Name = ""$($_.FriendlyName)"" } })
  Fans = @(Get-CimInstance Win32_Fan -ErrorAction SilentlyContinue | ForEach-Object { ""$($_.Name)"" })
  Battery = [bool](Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue)
}", 240000);

            Body.Children.Clear();
            Body.Children.Add(Ui.Columns(2, ProcessorCard(r), MemoryCard(r)));
            Body.Children.Add(Ui.Columns(2, VideoCard(r), StorageCard(r)));
            Body.Children.Add(PeripheralsCard(r));
            Body.Children.Add(Ui.Columns(2, AudioNetCard(r), CoolingCard(r)));
        });

        static Border Half(string title, params UIElement[] items)
        {
            var c = Ui.Card(title, items);
            c.Margin = new Thickness(0, 0, 14, 14);
            return c;
        }

        Border ProcessorCard(JsonElement r) => Half("Processador e placa-mãe",
            Ui.T(r.S("Cpu"), 15, weight: FontWeights.SemiBold),
            Ui.Hint($"{r.D("Cores") ?? 0:0} núcleos · {r.D("Threads") ?? 0:0} threads · até {r.D("MaxMHz") ?? 0:0} MHz (base)"),
            Ui.Separator(),
            Ui.KV("Placa-mãe", r.S("Board")),
            Ui.KV("BIOS", r.S("Bios")));

        Border MemoryCard(JsonElement r)
        {
            var mods = r.A("Ram").ToList();
            var total = mods.Sum(m => m.D("GB") ?? 0);
            int slots = (int)(r.D("Slots") ?? 0);
            // Canal pela etiqueta do slot (ex.: "P0 CHANNEL A" / "ChannelB-DIMM0")
            var channels = mods.Select(m => Regex.Match(m.S("Bank") + " " + m.S("Slot"), @"(?i)channel\s*-?\s*([A-D])").Groups[1].Value.ToUpper())
                               .Where(c => c.Length > 0).Distinct().Count();
            string mode = mods.Count <= 1 ? "Single channel (um pente só): colocar um segundo pente igual pode dar até 20% mais FPS"
                : channels >= 2 ? "Dual channel: os pentes trabalham juntos, ótimo"
                : channels == 1 ? "Os pentes estão no mesmo canal: mude um deles de slot (veja o manual: geralmente 2º e 4º slot)"
                : "Dual channel provável (não deu para confirmar pelos slots)";
            int type = (int)(mods.FirstOrDefault().D("Type") ?? 0);
            var ddr = type switch { 26 => "DDR4", 34 => "DDR5", 24 => "DDR3", _ => "" };
            var items = new List<UIElement>
            {
                Ui.T($"{total:0} GB {ddr}", 15, weight: FontWeights.SemiBold),
                Ui.Hint($"{mods.Count} pente(s){(slots > 0 ? $" de {slots} slots" : "")}"),
                Ui.T(mode, 13.5, mods.Count <= 1 || channels == 1 ? Ui.Warn : Ui.Good, margin: new Thickness(0, 6, 0, 0)),
                Ui.Separator(),
            };
            foreach (var m in mods)
            {
                var rated = Hardware.RatedSpeed(m.S("Part"));
                var now = (int)(m.D("Speed") ?? 0);
                items.Add(Ui.KV(m.S("Slot"), $"{m.D("GB") ?? 0:0} GB · {now} MHz{(rated > now + 200 ? $" (de fábrica: {rated})" : "")}", rated > now + 200 ? Ui.Warn : null));
            }
            return Half("Memória RAM", items.ToArray());
        }

        Border VideoCard(JsonElement r)
        {
            var items = new List<UIElement>();
            foreach (var g in r.A("Gpus"))
            {
                items.Add(Ui.T(g.S("Name"), 15, weight: FontWeights.SemiBold));
                if (g.S("Res").Length > 0) items.Add(Ui.Hint(g.S("Res")));
            }
            var m = HwMonitor.Shared;
            if (m.GpuMemTotal is double vram) items.Add(Ui.KV("Memória de vídeo", $"{vram / 1024:0} GB"));
            var mons = r.A("Monitors").Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
            if (mons.Count == 0 && r.TryGetProperty("Monitors", out var one) && one.ValueKind == JsonValueKind.String) mons.Add(one.GetString()!);
            items.Add(Ui.Separator());
            items.Add(Ui.KV("Monitores", mons.Count == 0 ? "Não identificado" : string.Join(", ", mons)));
            return Half("Vídeo", items.ToArray());
        }

        Border StorageCard(JsonElement r)
        {
            var items = new List<UIElement>();
            foreach (var d in r.A("Disks"))
                items.Add(Ui.KV(d.S("Name"), $"{d.S("Media")} {d.S("Bus")} · {d.D("GB") ?? 0:0} GB"));
            return Half("Armazenamento", items.ToArray());
        }

        Border PeripheralsCard(JsonElement r)
        {
            // Um item por aparelho USB (VID+PID), com o nome mais útil entre os que o Windows dá
            var usb = r.A("Usb").Where(u => !VirtualDevices.Contains(u.S("Vid") + ":" + u.S("Pid"))).GroupBy(u => u.S("Vid") + u.S("Pid")).Select(g =>
            {
                var classes = g.Select(x => x.S("Class")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var generic = new Regex(@"(?i)^(USB Composite Device|Dispositivo|HID|Mouse compatível|Teclado|Receptor USB)|compatível com HID|compliant");
                // O nome mais útil vem da parte certa do aparelho: a câmera pelo item "Camera", o resto pelo item USB
                string Pick(params string[] prefer) =>
                    prefer.SelectMany(c => g.Where(x => x.S("Class").Equals(c, StringComparison.OrdinalIgnoreCase)))
                          .Concat(g).Select(x => x.S("Name")).FirstOrDefault(n => n.Length > 0 && !generic.IsMatch(n)) ?? g.First().S("Name");
                var all = string.Join(" ", g.Select(x => x.S("Name")));
                string kind, name;
                if (classes.Contains("Camera") || classes.Contains("Image")) (kind, name) = ("Câmera", Pick("Camera", "Image"));
                else if (classes.Contains("MEDIA") && Regex.IsMatch(all, "(?i)mic")) (kind, name) = ("Microfone", Pick("MEDIA"));
                else if (classes.Contains("MEDIA")) (kind, name) = ("Áudio (fone/headset/placa de som)", Pick("MEDIA"));
                else if (classes.Contains("Keyboard") || classes.Contains("Mouse"))
                {
                    name = Pick("USB");
                    bool receiver = Regex.IsMatch(all, "(?i)receiver|receptor|lightspeed|unifying|dongle");
                    bool looksKeyboard = Regex.IsMatch(name, @"(?i)keyboard|teclado|\bTKL\b|\bK\d{2,3}\b|\bG\d{3}\b.*(key|tecl)");
                    bool looksMouse = Regex.IsMatch(name, @"(?i)mouse|superlight|deathadder|viper|\bG\d{3}\b|\bM\d{3}\b");
                    kind = looksKeyboard ? (receiver ? "Teclado sem fio" : "Teclado")
                         : looksMouse ? (receiver ? "Mouse sem fio" : "Mouse")
                         : receiver ? "Receptor sem fio (mouse/teclado)"
                         : classes.Contains("Mouse") && !classes.Contains("Keyboard") ? "Mouse"
                         : "Teclado";
                }
                else if (classes.Contains("Bluetooth")) (kind, name) = ("Bluetooth", Pick("Bluetooth"));
                else if (g.First().S("Vid").Equals("1E71", StringComparison.OrdinalIgnoreCase)) (kind, name) = ("Controladora NZXT", Pick());
                else (kind, name) = ("Outro dispositivo USB", Pick());
                var vendor = Vendors.TryGetValue(g.First().S("Vid"), out var v) ? v : "";
                return (Kind: kind, Name: name, Vendor: vendor);
            }).Where(x => x.Kind != "Outro dispositivo USB" || x.Vendor.Length > 0)
              .OrderBy(x => x.Kind).ToList();

            var items = new List<UIElement>();
            if (usb.Count == 0) items.Add(Ui.Hint("Nenhum periférico USB identificado."));
            foreach (var u in usb)
                items.Add(Ui.KV(u.Kind, (u.Vendor.Length > 0 && !u.Name.Contains(u.Vendor, StringComparison.OrdinalIgnoreCase) ? u.Vendor + " " : "") + u.Name));
            items.Add(Ui.Hint("Mouses e teclados Bluetooth ou ligados no receptor aparecem pelo receptor.", new Thickness(0, 8, 0, 0)));
            return Ui.Card("Periféricos", items.ToArray());
        }

        Border AudioNetCard(JsonElement r)
        {
            var items = new List<UIElement> { Ui.T("Áudio", 13, Ui.Sub, FontWeights.SemiBold, new Thickness(0, 0, 0, 4)) };
            var audio = r.A("Audio").Select(a => a.GetString() ?? "").Where(a => a.Length > 0).Distinct().ToList();
            if (audio.Count == 0 && r.TryGetProperty("Audio", out var one) && one.ValueKind == JsonValueKind.String) audio.Add(one.GetString()!);
            foreach (var a in audio) items.Add(Ui.T("• " + a, 13.5));
            items.Add(Ui.T("Rede", 13, Ui.Sub, FontWeights.SemiBold, new Thickness(0, 12, 0, 4)));
            foreach (var n in r.A("Net"))
                items.Add(Ui.KV(n.B("Wifi") == true ? "Wi-Fi" : "Cabo", $"{n.S("Name")} · {(n.S("Status") == "Up" ? n.S("Speed") : "desconectado")}",
                    n.S("Status") == "Up" ? Ui.Good : Ui.Sub));
            return Half("Áudio e rede", items.ToArray());
        }

        Border CoolingCard(JsonElement r)
        {
            bool nzxt = r.A("Usb").Any(u => u.S("Vid").Equals("1E71", StringComparison.OrdinalIgnoreCase));
            bool corsair = r.A("Usb").Any(u => u.S("Vid").Equals("1B1C", StringComparison.OrdinalIgnoreCase));
            var items = new List<UIElement>
            {
                Ui.KV("Fans e rotação", "Precisa do driver de sensores (próxima versão)", Ui.Sub),
                Ui.Hint("O Windows sozinho não informa quantos fans existem nem a rotação: isso vem do chip da placa-mãe, lido por um driver de sensores.", new Thickness(0, 0, 0, 8)),
                Ui.KV("Controladora NZXT", nzxt ? "Detectada" : "Não detectada pelo USB", nzxt ? Ui.Good : Ui.Warn),
            };
            if (!nzxt)
                items.Add(Ui.Hint("Se você tem uma controladora NZXT e ela não aparece aqui, o cabo USB dela não está conversando com a placa. Confira se está num conector JUSB (USB 2.0, 9 pinos) e não no JCOM/JTPM, que têm formato parecido; teste o outro JUSB; e confirme o cabo SATA de energia.", new Thickness(0, 0, 0, 8)));
            items.Add(Ui.KV("Controladora Corsair (Commander/Lighting)", corsair ? "Detectada" : "Não detectada", corsair ? Ui.Good : Ui.Sub));
            items.Add(Ui.Separator());
            items.Add(Ui.KV("Fonte de energia", "O Windows não identifica", Ui.Sub));
            items.Add(Ui.Hint("Só fontes com cabo USB (ex.: Corsair HXi/RMi) aparecem para o PC. Para saber a sua, veja a etiqueta na lateral da fonte."));
            if (r.B("Battery") == true) items.Add(Ui.KV("Bateria", "Notebook com bateria"));
            return Half("Refrigeração e energia", items.ToArray());
        }
    }
}
