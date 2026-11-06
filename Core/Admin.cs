using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace Bastion.Core
{
    public static class Admin
    {
        public static bool IsAdmin { get; } =
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        public static string ExePath => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;

        /// <summary>Reabre o app pedindo permissão de administrador (UAC), voltando para a mesma tela.</summary>
        public static void Relaunch()
        {
            try
            {
                var page = (Application.Current.MainWindow as MainWindow)?.CurrentPage;
                var args = page == null ? "" : $"--pagina \"{page}\"";
                Process.Start(new ProcessStartInfo(ExePath, args) { UseShellExecute = true, Verb = "runas" });
                Application.Current.Shutdown();
            }
            catch (Win32Exception)
            {
                // Usuário cancelou o aviso do Windows
            }
        }

        public static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { Ui.Info("Não foi possível abrir: " + ex.Message); }
        }
    }
}
