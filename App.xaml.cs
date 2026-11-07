using System;
using System.Linq;
using System.Windows;
using Bastion.Core;

namespace Bastion
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (_, a) =>
            {
                MessageBox.Show(a.Exception.Message, "Bastion", MessageBoxButton.OK, MessageBoxImage.Warning);
                a.Handled = true;
            };

            TrayHost.MigrateFromPcManager();
            I18n.LoadSaved();
            // "--idioma en": abre num idioma sem mudar o que ficou salvo (útil para testar)
            var li = Array.FindIndex(e.Args, a => a.Equals("--idioma", StringComparison.OrdinalIgnoreCase));
            if (li >= 0 && li + 1 < e.Args.Length) I18n.Force(e.Args[li + 1]);

            // "--organizar": roda só o organizador de arquivos, escondido perto do relógio
            if (e.Args.Contains("--organizar", StringComparer.OrdinalIgnoreCase))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                TrayHost.Start();
                return;
            }

            Theme.LoadSaved();
            var main = new MainWindow();
            MainWindow = main;
            TestPlacement(main);
            main.Show();

            // "--pagina Limpeza": abre direto numa página
            var i = Array.FindIndex(e.Args, a => a.Equals("--pagina", StringComparison.OrdinalIgnoreCase));
            if (i >= 0 && i + 1 < e.Args.Length) main.Navigate(e.Args[i + 1]);

            I18n.Changed += RebuildWindow;
        }

        /// <summary>Troca de idioma: abre a janela de novo, no mesmo lugar e na mesma tela.</summary>
        void RebuildWindow()
        {
            if (MainWindow is not MainWindow old) return;
            var fresh = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = old.Left,
                Top = old.Top,
            };
            MainWindow = fresh;
            fresh.Show();
            fresh.Navigate(old.CurrentPage);
            old.Close();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            HwMonitor.Shared.Dispose();
            base.OnExit(e);
        }
    
        /// <summary>
        /// Só para quem desenvolve: BASTION_TESTE_JANELA="x,y" abre a janela nessa posição e sem roubar o foco
        /// (por exemplo, no segundo monitor enquanto um jogo roda no principal). Usuários nunca têm essa variável.
        /// </summary>
        static void TestPlacement(Window w)
        {
            var v = Environment.GetEnvironmentVariable("BASTION_TESTE_JANELA");
            if (string.IsNullOrEmpty(v)) return;
            var p = v.Split(',');
            if (p.Length != 2 || !double.TryParse(p[0], out var x) || !double.TryParse(p[1], out var y)) return;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Left = x; w.Top = y;
            w.ShowActivated = false;
        }
    }
}
