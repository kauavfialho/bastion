using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Bastion.Core;

namespace Bastion.Pages
{
    public sealed class StartupPage : PageBase
    {
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        sealed record Entry(string Name, string Command, RegistryKey Hive, string ApprovedKey, bool Machine);

        // Itens que normalmente devem continuar ligados
        static readonly Regex Essential = new(@"SecurityHealth|vgtray|Vanguard|Realtek|RtkAud|Rtk|nvcpl|NVIDIA|amd|Radeon|igfx|Intel|Synaptics|ELAN|Waves|Dolby|Nahimic", RegexOptions.IgnoreCase);

        readonly StackPanel list = new();

        public StartupPage() : base("Inicialização", "Escolha o que abre junto com o Windows. Menos programas = PC que liga mais rápido e sobra mais memória para os jogos.")
        {
            Body.Children.Add(Ui.Card(null,
                AdminNote("mudar os programas instalados para todos os usuários"),
                Ui.Hint("Desligar aqui não desinstala nada: o programa só deixa de abrir sozinho. Dá para religar quando quiser (é o mesmo controle do Gerenciador de Tarefas)."),
                Ui.Separator(),
                list,
                Ui.Buttons(Ui.Btn("Atualizar", () => _ = LoadAsync(), icon: ""), Ui.Btn("Abrir pasta de inicialização", () => Admin.Open("shell:startup"), icon: ""))));
        }

        static IEnumerable<Entry> Read()
        {
            (RegistryKey Hive, string Key, string ApprovedKey, bool Machine)[] sources =
            {
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run", false),
                (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run", true),
                (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32", true),
            };
            foreach (var s in sources)
            {
                using var k = s.Hive.OpenSubKey(s.Key);
                if (k == null) continue;
                foreach (var name in k.GetValueNames())
                    yield return new Entry(name, k.GetValue(name)?.ToString() ?? "", s.Hive, s.ApprovedKey, s.Machine);
            }
            var folders = new[]
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.Startup), Registry.CurrentUser, false),
                (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), Registry.LocalMachine, true),
            };
            foreach (var (dir, hive, machine) in folders)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                    yield return new Entry(Path.GetFileName(f), f, hive, "StartupFolder", machine);
            }
        }

        static bool IsActive(Entry e)
        {
            using var k = e.Hive.OpenSubKey(Approved + e.ApprovedKey);
            return k?.GetValue(e.Name) is not byte[] b || b.Length == 0 || (b[0] & 1) == 0;
        }

        static void SetEnabled(Entry e, bool on)
        {
            using var k = e.Hive.CreateSubKey(Approved + e.ApprovedKey);
            var data = new byte[12];
            data[0] = (byte)(on ? 2 : 3);
            if (!on) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            k.SetValue(e.Name, data, RegistryValueKind.Binary);
        }

        static string Friendly(Entry e)
        {
            var name = Regex.Replace(e.Name, @"^electron\.app\.|\.lnk$|_\{.*\}$", "", RegexOptions.IgnoreCase);
            return name.Length > 0 ? name : e.Name;
        }

        public override Task LoadAsync()
        {
            list.Children.Clear();
            var entries = Read().Where(e => !e.Name.StartsWith("msedge_cleanup", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(e => Friendly(e), StringComparer.OrdinalIgnoreCase).ToList();
            int on = 0;
            foreach (var e in entries)
            {
                bool enabled = IsActive(e);
                if (enabled) on++;
                bool essential = Essential.IsMatch(e.Name + " " + e.Command);
                var exe = Regex.Match(e.Command, "\"?([^\"]+?\\.(exe|lnk|bat|cmd))", RegexOptions.IgnoreCase).Groups[1].Value;
                var hint = (essential ? "Recomendado manter ligado  ·  " : "") + (exe.Length > 0 ? exe : e.Command);
                var sw = Ui.Switch(Friendly(e), hint, enabled);
                if (e.Machine) Ui.NeedsAdmin(sw, "mudar programas instalados para todos os usuários");
                sw.Checked += (_, _) => Toggle(e, true);
                sw.Unchecked += (_, _) => Toggle(e, false);
                list.Children.Add(sw);
            }
            list.Children.Insert(0, Ui.T($"{on} de {entries.Count} programas abrem com o Windows", 15, weight: FontWeights.SemiBold, margin: new Thickness(0, 0, 0, 8)));
            return Task.CompletedTask;
        }

        void Toggle(Entry e, bool on)
        {
            try { SetEnabled(e, on); }
            catch (Exception ex) { Ui.Info("Não foi possível mudar: " + ex.Message); }
            _ = LoadAsync();
        }
    }
}
