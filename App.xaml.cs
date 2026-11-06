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
            main.Show();

            // "--pagina Limpeza": abre direto numa página
            var i = Array.FindIndex(e.Args, a => a.Equals("--pagina", StringComparison.OrdinalIgnoreCase));
            if (i >= 0 && i + 1 < e.Args.Length) main.Navigate(e.Args[i + 1]);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            HwMonitor.Shared.Dispose();
            base.OnExit(e);
        }
    }
}
