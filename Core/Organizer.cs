using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bastion.Core
{
    /// <summary>
    /// Imagens vão para Imagens, vídeos para Vídeos, e todo o resto fica onde está.
    /// Observa Downloads, Vídeos (para tirar imagens de lá) e a pasta Vídeos antiga do perfil.
    /// </summary>
    public sealed class Organizer : IDisposable
    {
        public static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic", ".heif", ".jfif", ".avif", ".tif", ".tiff" };
        public static readonly string[] VideoExt = { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".wmv", ".m4v", ".flv", ".3gp", ".mpg", ".mpeg" };

        public static string LogPath => Path.Combine(Folders.AppData, "organizador.log");

        readonly List<FileSystemWatcher> watchers = new();
        readonly HashSet<string> pending = new(StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<string> WatchedFolders()
        {
            var oldVideos = Path.Combine(Folders.UserProfile, "Videos");
            return new[] { Folders.Downloads, Folders.Videos, oldVideos }
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public void Start()
        {
            foreach (var folder in WatchedFolders())
            {
                var w = new FileSystemWatcher(folder) { IncludeSubdirectories = false, InternalBufferSize = 64 * 1024 };
                // Navegadores baixam como .crdownload/.tmp e renomeiam no fim, por isso Renamed também
                w.Created += (_, e) => Queue(e.FullPath);
                w.Renamed += (_, e) => Queue(e.FullPath);
                w.EnableRaisingEvents = true;
                watchers.Add(w);
            }
            Log("Iniciado. Observando: " + string.Join(", ", WatchedFolders()));
        }

        static string? Destination(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var folder = Path.GetDirectoryName(path) ?? "";
            if (ImageExt.Contains(ext)) return string.Equals(folder, Folders.Pictures, StringComparison.OrdinalIgnoreCase) ? null : Folders.Pictures;
            // Vídeos que já estão na pasta Vídeos ficam
            if (VideoExt.Contains(ext)) return string.Equals(folder, Folders.Videos, StringComparison.OrdinalIgnoreCase) ? null : Folders.Videos;
            return null;
        }

        void Queue(string path)
        {
            if (Destination(path) == null) return;
            lock (pending) { if (!pending.Add(path)) return; }
            Task.Run(async () =>
            {
                try { await WaitUntilReady(path); Move(path); }
                catch (Exception ex) { Log($"ERRO {path}: {ex.Message}"); }
                finally { lock (pending) pending.Remove(path); }
            });
        }

        /// <summary>Espera o arquivo parar de crescer e ficar livre (download terminado).</summary>
        static async Task WaitUntilReady(string path)
        {
            long last = -1;
            for (int i = 0; i < 600; i++)
            {
                await Task.Delay(1000);
                if (!File.Exists(path)) return;
                long len;
                try { len = new FileInfo(path).Length; } catch { continue; }
                if (len > 0 && len == last)
                {
                    try { using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return; }
                    catch (IOException) { }
                }
                last = len;
            }
        }

        static bool Move(string path)
        {
            var dest = Destination(path);
            if (dest == null || !File.Exists(path) || new FileInfo(path).Length == 0) return false;
            Directory.CreateDirectory(dest);
            var target = Folders.Unique(dest, Path.GetFileName(path));
            File.Move(path, target);
            Log($"{path} -> {target}");
            return true;
        }

        /// <summary>Organiza o que já está em Downloads agora mesmo.</summary>
        public static int OrganizeNow()
        {
            int n = 0;
            foreach (var folder in WatchedFolders())
            {
                foreach (var f in Directory.EnumerateFiles(folder))
                {
                    try { if (Move(f)) n++; }
                    catch (Exception ex) { Log($"ERRO {f}: {ex.Message}"); }
                }
            }
            return n;
        }

        static readonly object logLock = new();
        public static void Log(string msg)
        {
            lock (logLock)
            {
                try { File.AppendAllText(LogPath, $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}  {msg}{Environment.NewLine}"); } catch { }
            }
        }

        public void Dispose()
        {
            foreach (var w in watchers) w.Dispose();
            watchers.Clear();
        }
    }
}
