using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class GamingPage : PageBase
    {
        const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        const string HighGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        readonly StackPanel plans = new();
        readonly StackPanel toggles = new();
        readonly StackPanel netResult = new();
        bool syncing; // true enquanto o app ajusta um interruptor sozinho, sem aplicar nada

        public GamingPage() : base("Jogos", "Ajustes do Windows que ajudam no desempenho e na resposta durante os jogos.")
        {
            Body.Children.Add(Ui.Card("Plano de energia", Ui.Hint("\"Alto desempenho\" ou \"Desempenho máximo\" deixam o processador sempre pronto. Em notebook gasta mais bateria.", new Thickness(0, 0, 0, 8)), plans));
            Body.Children.Add(Ui.Card("Ajustes do Windows", AdminNote("mudar o agendamento de GPU"), toggles));
            Body.Children.Add(Ui.Card("Teste de conexão",
                Ui.Hint("Mede o ping, a variação (jitter) e a perda de pacotes até o seu roteador e até a internet. Rode com o jogo fechado.", new Thickness(0, 0, 0, 8)),
                netResult,
                Ui.Buttons(Ui.BtnAsync("Testar conexão", TestNetwork, "primary", ""))));
            Body.Children.Add(Ui.Card("Dica de ouro: XMP / EXPO",
                Ui.T("Se a sua RAM roda abaixo da velocidade de fábrica (veja em Saúde do PC), ligar o XMP na BIOS pode render até 15–20% de FPS em processadores AMD Ryzen. " +
                     "Só ligue se o PC estiver estável, sem telas azuis.", 14, Ui.Sub)));
        }

        public override Task LoadAsync() => Busy("Lendo configurações…", async () =>
        {
            await LoadPlans();
            LoadToggles();
        });

        async Task LoadPlans()
        {
            var txt = await Ps.RunAsync("powercfg /list");
            plans.Children.Clear();
            var found = new List<string>();
            foreach (Match m in Regex.Matches(txt, @"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\s+\(([^)]+)\)(\s*\*)?", RegexOptions.IgnoreCase))
            {
                var guid = m.Groups[1].Value;
                bool active = m.Groups[3].Success && m.Groups[3].Value.Contains('*');
                found.Add(guid.ToLowerInvariant());
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = Ui.T(m.Groups[2].Value, 14, active ? Ui.Good : Ui.Fg, active ? FontWeights.SemiBold : FontWeights.Normal);
                name.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(name);
                UIElement right = active ? Ui.Pill("Em uso", Ui.Good) : Ui.BtnAsync("Usar este", async () =>
                {
                    await Ps.RunAsync($"powercfg /setactive {guid}");
                    await LoadPlans();
                });
                if (right is Button b) b.Margin = new Thickness(0);
                Grid.SetColumn(right, 1);
                row.Children.Add(right);
                plans.Children.Add(row);
            }
            var add = new List<UIElement>();
            if (!found.Contains(HighGuid))
                add.Add(Ui.BtnAsync("Adicionar Alto desempenho", async () => { await Ps.RunAsync($"powercfg -duplicatescheme {HighGuid}"); await LoadPlans(); }, icon: ""));
            if (!found.Any(g => g == UltimateGuid) && !Regex.IsMatch(txt, "Desempenho M[aá]ximo|Ultimate", RegexOptions.IgnoreCase))
                add.Add(Ui.BtnAsync("Adicionar Desempenho máximo", async () => { await Ps.RunAsync($"powercfg -duplicatescheme {UltimateGuid}"); await LoadPlans(); }, "primary", ""));
            if (add.Count > 0) plans.Children.Add(Ui.Buttons(add.ToArray()));
        }

        // ---------- Interruptores ----------

        static object? Get(RegistryKey hive, string key, string name)
        {
            using var k = hive.OpenSubKey(key);
            return k?.GetValue(name);
        }

        static void Set(RegistryKey hive, string key, string name, object value, RegistryValueKind kind = RegistryValueKind.DWord)
        {
            using var k = hive.CreateSubKey(key);
            k.SetValue(name, value, kind);
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint action, uint param, int[] vparam, uint winIni);

        void LoadToggles()
        {
            toggles.Children.Clear();

            Add("Modo de Jogo", "O Windows prioriza o jogo aberto e segura atualizações em segundo plano.",
                Get(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled") is not int gm || gm == 1,
                on => Set(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", on ? 1 : 0));

            bool dvr = Get(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled") is not int d || d == 1;
            Add("Desligar gravação em segundo plano (Xbox Game Bar)", "A gravação contínua dos últimos minutos custa FPS. Desligue se não usa.",
                !dvr,
                on =>
                {
                    Set(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", on ? 0 : 1);
                    Set(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", on ? 0 : 1);
                });

            bool accel = (Get(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed") as string) != "0";
            Add("Desligar aceleração do mouse", "Deixa a mira consistente: o cursor anda sempre a mesma distância para o mesmo movimento.",
                !accel,
                on =>
                {
                    var v = on ? new[] { 0, 0, 0 } : new[] { 6, 10, 1 };
                    SystemParametersInfo(0x0004, 0, v, 0x01 | 0x02); // SPI_SETMOUSE, salva e avisa os programas
                });

            // A chave só existe se alguém mudou; sem ela vale o padrão do Windows (ligado em placas compatíveis).
            // O estado real vem do DirectX logo abaixo.
            var hagsReg = Get(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode") as int?;
            var hagsSw = Add("Agendamento de GPU acelerado por hardware", "Reduz um pouco a latência em placas recentes. Precisa reiniciar o PC.  ·  Verificando…",
                hagsReg == 2,
                on => Set(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", on ? 2 : 1));
            Ui.NeedsAdmin(hagsSw, "mudar o agendamento de GPU");
            _ = CheckGpuScheduling(hagsSw);

            bool fse = Get(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode") is int f && f == 2;
            Add("Otimizações de tela cheia desligadas", "Alguns jogos antigos rodam melhor assim. Na maioria dos casos não muda nada.",
                fse,
                on => Set(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", on ? 2 : 0));

            toggles.Children.Add(Ui.Buttons(
                Ui.Btn("Configurações de gráficos do Windows", () => Admin.Open("ms-settings:display-advancedgraphics"), icon: ""),
                Ui.Btn("Configurações de jogos", () => Admin.Open("ms-settings:gaming-gamebar"), icon: "")));
        }

        async Task CheckGpuScheduling(CheckBox sw)
        {
            var state = await Hardware.GpuSchedulingAsync();
            var hint = ((StackPanel)sw.Content).Children.OfType<TextBlock>().Last();
            const string baseHint = "Reduz um pouco a latência em placas recentes. Precisa reiniciar o PC.";
            if (state is not (bool supported, bool enabled))
            {
                hint.Text = baseHint;
                return;
            }
            if (!supported)
            {
                hint.Text = "Sua placa de vídeo ou o driver não suportam essa opção.";
                sw.IsEnabled = false;
                return;
            }
            syncing = true;
            sw.IsChecked = enabled;
            syncing = false;
            hint.Text = baseHint + (enabled ? "  ·  Ligado agora" : "  ·  Desligado agora");
        }

        // ---------- Teste de conexão ----------

        sealed record PingStats(string Name, string Host, double Avg, double Min, double Max, double Jitter, double LossPct);

        static async Task<PingStats> Measure(string name, string host, int count = 20)
        {
            var times = new List<double>();
            using var ping = new System.Net.NetworkInformation.Ping();
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var r = await ping.SendPingAsync(host, 1500);
                    if (r.Status == System.Net.NetworkInformation.IPStatus.Success) times.Add(r.RoundtripTime);
                }
                catch { }
                await Task.Delay(150);
            }
            double loss = 100.0 * (count - times.Count) / count;
            if (times.Count == 0) return new(name, host, 0, 0, 0, 0, loss);
            double jitter = times.Count > 1 ? times.Zip(times.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
            return new(name, host, times.Average(), times.Min(), times.Max(), jitter, loss);
        }

        static string? Gateway()
        {
            foreach (var n in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                var gw = n.GetIPProperties().GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any));
                if (gw != null) return gw.Address.ToString();
            }
            return null;
        }

        async Task TestNetwork()
        {
            netResult.Children.Clear();
            List<PingStats> results = new();
            await Busy("Testando conexão… (uns 5 segundos)", async () =>
            {
                var targets = new List<(string, string)>();
                if (Gateway() is string gw) targets.Add(("Roteador", gw));
                targets.Add(("Cloudflare", "1.1.1.1"));
                targets.Add(("Google", "8.8.8.8"));
                results = (await Task.WhenAll(targets.Select(t => Measure(t.Item1, t.Item2)))).ToList();
            });

            foreach (var r in results)
            {
                var txt = r.LossPct >= 100 ? "não responde"
                    : $"{r.Avg:0} ms  (mín {r.Min:0} / máx {r.Max:0})  ·  jitter {r.Jitter:0} ms  ·  perda {r.LossPct:0}%";
                var lvl = r.LossPct >= 100 ? 1 : r.LossPct > 2 || r.Avg > 120 ? 2 : r.Avg > 60 || r.Jitter > 15 ? 1 : 0;
                netResult.Children.Add(Ui.KV($"{r.Name}  ({r.Host})", txt, Ui.Level(lvl)));
            }

            var router = results.FirstOrDefault(r => r.Name == "Roteador");
            var inet = results.Where(r => r.Name != "Roteador" && r.LossPct < 100).ToList();
            string verdict;
            int level;
            if (inet.Count == 0)
                (verdict, level) = ("Nenhum servidor da internet respondeu: sem acesso à internet ou ping bloqueado pela rede.", 2);
            else if (inet.Any(r => r.LossPct > 2))
                (verdict, level) = ("Há perda de pacotes até a internet. Isso causa teleportes e travadas online. Teste com cabo; se continuar, fale com a operadora.", 2);
            else if (router != null && router.LossPct < 100 && (router.Avg > 8 || router.Jitter > 5))
                (verdict, level) = ("O ping até o seu roteador está alto ou instável: típico de Wi-Fi. Para jogar, use cabo de rede ou Wi-Fi 5 GHz perto do roteador.", 1);
            else if (inet.Average(r => r.Jitter) > 15)
                (verdict, level) = ("A conexão oscila bastante (jitter alto). Feche downloads, streams e atualizações rodando ao fundo.", 1);
            else if (inet.Average(r => r.Avg) > 60)
                (verdict, level) = ("Ping alto até a internet, mas estável. A rede de casa está ok; o atraso vem da operadora ou da distância.", 1);
            else
                (verdict, level) = ("Conexão ótima para jogar: ping baixo, estável e sem perda de pacotes.", 0);
            if (router != null && router.LossPct >= 100) verdict += " (Seu roteador não responde a ping, alguns modelos bloqueiam. Isso por si só não é problema.)";
            netResult.Children.Add(Ui.T(verdict, 14, Ui.Level(level), FontWeights.SemiBold, new Thickness(0, 10, 0, 6)));
        }

        CheckBox Add(string label, string hint, bool on, Action<bool> apply)
        {
            var sw = Ui.Switch(label, hint, on);
            sw.Checked += (_, _) => { if (!syncing) Apply(apply, true); };
            sw.Unchecked += (_, _) => { if (!syncing) Apply(apply, false); };
            toggles.Children.Add(sw);
            return sw;
        }

        static void Apply(Action<bool> apply, bool on)
        {
            try { apply(on); }
            catch (Exception ex) { Ui.Info("Não foi possível aplicar: " + ex.Message); }
        }
    }
}
