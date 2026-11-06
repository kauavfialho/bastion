using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bastion.Core
{
    /// <summary>
    /// Libera RAM: tira da memória as páginas que os programas não estão usando agora
    /// e, como administrador, esvazia a lista de memória em espera do Windows.
    /// Nada é fechado; os programas recarregam o que precisarem.
    /// </summary>
    public static class MemoryCleaner
    {
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, PROCESS_SET_QUOTA = 0x0100;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr h);

        [DllImport("ntdll.dll")]
        static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attributes; }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool LookupPrivilegeValue(string? system, string name, out long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, int len, IntPtr prev, IntPtr retLen);

        /// <summary>Retorna quantos bytes de RAM ficaram livres a mais.</summary>
        public static long Clean()
        {
            var before = (long)HwMonitor.AvailablePhys();
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, p.Id);
                    if (h == IntPtr.Zero) continue;
                    EmptyWorkingSet(h);
                    CloseHandle(h);
                }
            }
            if (Admin.IsAdmin) PurgeStandby();
            return Math.Max(0, (long)HwMonitor.AvailablePhys() - before);
        }

        static void PurgeStandby()
        {
            if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) return;
            int command = 4; // MemoryPurgeStandbyList
            NtSetSystemInformation(80, ref command, sizeof(int)); // SystemMemoryListInformation
        }

        static bool EnablePrivilege(string name)
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out var token)) return false; // ADJUST | QUERY
            try
            {
                if (!LookupPrivilegeValue(null, name, out var luid)) return false;
                var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attributes = 0x2 }; // SE_PRIVILEGE_ENABLED
                return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero) && Marshal.GetLastWin32Error() == 0;
            }
            finally { CloseHandle(token); }
        }
    }
}
