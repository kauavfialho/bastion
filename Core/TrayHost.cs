using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Bastion.Core
{
    /// <summary>Organizador rodando escondido, com ícone perto do relógio.</summary>
    public static class TrayHost
    {
        const string MutexName = @"Local\Bastion.Organizer";
        const string StopName = @"Local\Bastion.Organizer.Stop";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "Bastion Organizador";

        static Mutex? mutex;
        static Organizer? organizer;
        static Forms.NotifyIcon? icon;

        public static bool IsRunning
        {
            get
            {
                try { using var m = Mutex.OpenExisting(MutexName); return true; }
                catch { return false; }
            }
        }

        public static bool AutoStart
        {
            get
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                return k?.GetValue(RunValue) is string;
            }
            set
            {
                using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) k.SetValue(RunValue, $"\"{Admin.ExePath}\" --organizar");
                else k.DeleteValue(RunValue, false);
            }
        }

        /// <summary>
        /// O app se chamava "PC Manager": leva a inicialização automática e o registro
        /// do organizador antigos para os nomes novos.
        /// </summary>
        public static void MigrateFromPcManager()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (k?.GetValue("PCManager Organizador") != null)
                {
                    k.DeleteValue("PCManager Organizador", false);
                    k.SetValue(RunValue, $"\"{Admin.ExePath}\" --organizar");
                }
                var oldLog = System.IO.Path.Combine(Folders.LocalAppData, "PCManager", "organizador.log");
                if (System.IO.File.Exists(oldLog) && !System.IO.File.Exists(Organizer.LogPath))
                    System.IO.File.Move(oldLog, Organizer.LogPath);
            }
            catch { }
        }

        /// <summary>Abre o organizador em segundo plano, como processo separado.</summary>
        public static void LaunchOrganizer()
        {
            if (IsRunning) return;
            Process.Start(new ProcessStartInfo(Admin.ExePath, "--organizar") { UseShellExecute = true });
        }

        public static void RequestStop()
        {
            try { using var ev = EventWaitHandle.OpenExisting(StopName); ev.Set(); } catch { }
        }

        public static void Start()
        {
            mutex = new Mutex(true, MutexName, out bool created);
            if (!created) { System.Windows.Application.Current.Shutdown(); return; }

            organizer = new Organizer();
            organizer.Start();

            icon = new Forms.NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Admin.ExePath) ?? SystemIcons.Application,
                Text = I18n.Tr("Bastion - organizador ativo"),
                Visible = true,
            };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add(I18n.Tr("Abrir Bastion"), null, (_, _) => OpenApp());
            menu.Items.Add(I18n.Tr("Organizar agora"), null, (_, _) =>
            {
                int n = Organizer.OrganizeNow();
                icon.ShowBalloonTip(3000, "Bastion", I18n.Tr("{0} arquivo(s) organizado(s).", n), Forms.ToolTipIcon.Info);
            });
            menu.Items.Add(I18n.Tr("Ver registro"), null, (_, _) => Admin.Open(Organizer.LogPath));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(I18n.Tr("Parar organizador"), null, (_, _) => Stop());
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += (_, _) => OpenApp();

            var stop = new EventWaitHandle(false, EventResetMode.AutoReset, StopName);
            ThreadPool.RegisterWaitForSingleObject(stop, (_, _) =>
                System.Windows.Application.Current.Dispatcher.Invoke(Stop), null, -1, true);
        }

        static void OpenApp() => Process.Start(new ProcessStartInfo(Admin.ExePath) { UseShellExecute = true });

        static void Stop()
        {
            organizer?.Dispose();
            if (icon != null) { icon.Visible = false; icon.Dispose(); }
            try { mutex?.ReleaseMutex(); } catch { }
            Organizer.Log("Parado.");
            System.Windows.Application.Current.Shutdown();
        }
    }
}
