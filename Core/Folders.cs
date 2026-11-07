using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Bastion.Core
{
    public static class Folders
    {
        static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

        public static string Downloads
        {
            get
            {
                if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out var p) == 0)
                {
                    var s = Marshal.PtrToStringUni(p)!;
                    Marshal.FreeCoTaskMem(p);
                    return s;
                }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }

        public static string Pictures => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        public static string Videos => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        /// <summary>Pasta de dados do próprio app.</summary>
        public static string AppData
        {
            get
            {
                var d = Path.Combine(LocalAppData, "Bastion");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static readonly EnumerationOptions Deep = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        public static long SizeOf(string path, string pattern = "*")
        {
            long total = 0;
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;
                foreach (var f in new DirectoryInfo(path).EnumerateFiles(pattern, Deep))
                {
                    try { total += f.Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        /// <summary>Apaga o conteúdo de uma pasta (não a pasta). Arquivos em uso são pulados.</summary>
        public static long DeleteContents(string path, string pattern = "*")
        {
            long freed = 0;
            if (IsPersonal(path)) return 0;
            if (File.Exists(path))
            {
                try { var len = new FileInfo(path).Length; File.Delete(path); return len; } catch { return 0; }
            }
            if (!Directory.Exists(path)) return 0;
            foreach (var f in new DirectoryInfo(path).EnumerateFiles(pattern, Deep))
            {
                try
                {
                    var len = f.Length;
                    if (f.IsReadOnly) f.IsReadOnly = false;
                    f.Delete();
                    freed += len;
                }
                catch { }
            }
            if (pattern == "*")
            {
                foreach (var d in new DirectoryInfo(path).EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                {
                    try { d.Delete(true); } catch { }
                }
            }
            return freed;
        }

        /// <summary>
        /// Trava de segurança da limpeza: nunca apaga a raiz de um disco, a pasta do usuário nem as pastas
        /// pessoais (Downloads, Área de Trabalho, Documentos, Imagens, Vídeos, Músicas), nem nada dentro delas.
        /// </summary>
        public static bool IsPersonal(string path)
        {
            string full;
            try { full = Path.GetFullPath(path).TrimEnd('\\') + "\\"; } catch { return true; }
            if (Path.GetPathRoot(full)?.TrimEnd('\\') + "\\" == full) return true;
            var personal = new[]
            {
                Downloads, Desktop, Pictures, Videos,
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            };
            foreach (var p in personal.Where(p => !string.IsNullOrEmpty(p)))
                if (full.StartsWith(p.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(full, UserProfile.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Caminho livre no destino: "foto.jpg", "foto (1).jpg", ...</summary>
        public static string Unique(string folder, string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var target = Path.Combine(folder, fileName);
            for (int n = 1; File.Exists(target); n++) target = Path.Combine(folder, $"{name} ({n}){ext}");
            return target;
        }
    }
}
