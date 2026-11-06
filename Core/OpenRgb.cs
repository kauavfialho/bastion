using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Bastion.Core
{
    /// <summary>
    /// Cliente do servidor do OpenRGB (porta 6742). O OpenRGB conversa com RAM, placa-mãe, teclados,
    /// mouses e controladoras de vários fabricantes; o Bastion só manda as cores para ele.
    /// Protocolo: https://gitlab.com/CalcProgrammer1/OpenRGB/-/wikis/OpenRGB-SDK-Documentation
    /// </summary>
    public sealed class OpenRgb : IDisposable
    {
        public const int Port = 6742;
        const uint ClientProtocol = 3;   // o servidor responde com a menor versão entre as duas

        public sealed class Mode
        {
            public string Name = "";
            public int Value;
            public uint Flags, SpeedMin, SpeedMax, BrightMin, BrightMax, ColorsMin, ColorsMax, Speed, Bright, Direction, ColorMode;
            public List<(byte R, byte G, byte B)> Colors = new();
            public bool HasPerLedColor => (Flags & (1 << 5)) != 0;
            public bool HasModeColors => (Flags & (1 << 6)) != 0;
        }

        public sealed class Device
        {
            public int Index;
            public int Type;
            public string Name = "", Vendor = "", Description = "", Location = "";
            public int ActiveMode;
            public List<Mode> Modes = new();
            public int LedCount;
            public string TypeName => Type switch
            {
                0 => "Placa-mãe", 1 => "Memória RAM", 2 => "Placa de vídeo", 3 => "Cooler", 4 => "Fita de LED",
                5 => "Teclado", 6 => "Mouse", 7 => "Mousepad", 8 => "Headset", 9 => "Suporte de headset", 10 => "Controle",
                11 => "Lâmpada", 12 => "Caixa de som", 13 => "Virtual", 14 => "Armazenamento", 15 => "Gabinete", 16 => "Microfone",
                17 => "Acessório", 18 => "Teclado numérico", 19 => "Notebook", 20 => "Monitor", _ => "Dispositivo",
            };
        }

        TcpClient? tcp;
        NetworkStream? stream;
        uint protocol;

        public static bool IsRunning() => Process.GetProcessesByName("OpenRGB").Length > 0;

        public static string? ExePath()
        {
            foreach (var p in new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenRGB", "OpenRGB.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "OpenRGB", "OpenRGB.exe"),
                Path.Combine(Folders.LocalAppData, "Programs", "OpenRGB", "OpenRGB.exe"),
            })
                if (File.Exists(p)) return p;
            return null;
        }

        /// <summary>Abre o OpenRGB escondido, com o servidor ligado. Como admin, ele enxerga também RAM e placa-mãe.</summary>
        public static void Launch(bool elevated)
        {
            var exe = ExePath() ?? throw new FileNotFoundException("OpenRGB não está instalado.");
            var psi = new ProcessStartInfo(exe, $"--server --server-port {Port} --startminimized") { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! };
            if (elevated && !Admin.IsAdmin) psi.Verb = "runas";
            Process.Start(psi);
        }

        public async Task<bool> ConnectAsync(int timeoutMs = 1500)
        {
            try
            {
                tcp = new TcpClient();
                var connect = tcp.ConnectAsync("127.0.0.1", Port);
                if (await Task.WhenAny(connect, Task.Delay(timeoutMs)) != connect || !tcp.Connected) return false;
                stream = tcp.GetStream();
                stream.ReadTimeout = 5000;
                await SendAsync(0, 50, Encoding.ASCII.GetBytes("Bastion\0"));             // SET_CLIENT_NAME
                await SendAsync(0, 40, BitConverter.GetBytes(ClientProtocol));             // REQUEST_PROTOCOL_VERSION
                var (_, data) = await ReceiveAsync(40);
                protocol = Math.Min(ClientProtocol, data.Length >= 4 ? BitConverter.ToUInt32(data) : 0);
                return true;
            }
            catch { return false; }
        }

        public async Task<List<Device>> DevicesAsync()
        {
            await SendAsync(0, 0, Array.Empty<byte>());                                     // REQUEST_CONTROLLER_COUNT
            var (_, c) = await ReceiveAsync(0);
            int count = (int)BitConverter.ToUInt32(c);
            var list = new List<Device>();
            for (int i = 0; i < count; i++)
            {
                await SendAsync((uint)i, 1, BitConverter.GetBytes(protocol));               // REQUEST_CONTROLLER_DATA
                var (_, blob) = await ReceiveAsync(1);
                var d = Parse(blob);
                d.Index = i;
                list.Add(d);
            }
            return list;
        }

        /// <summary>Pinta todas as luzes do dispositivo. Tenta o modo direto/estático primeiro.</summary>
        public async Task SetColorAsync(Device d, IList<(byte R, byte G, byte B)> colors)
        {
            await SendAsync((uint)d.Index, 1100, Array.Empty<byte>());                       // SETCUSTOMMODE (modo direto)
            int n = Math.Max(1, d.LedCount);
            var body = new MemoryStream();
            var w = new BinaryWriter(body);
            w.Write((uint)(4 + 2 + n * 4));
            w.Write((ushort)n);
            for (int i = 0; i < n; i++)
            {
                var c = colors[colors.Count == 1 ? 0 : i * colors.Count / n];
                w.Write(c.R); w.Write(c.G); w.Write(c.B); w.Write((byte)0);
            }
            await SendAsync((uint)d.Index, 1050, body.ToArray());                            // UPDATELEDS
        }

        /// <summary>Usa um efeito do próprio dispositivo (ex.: Breathing, Rainbow), com a cor escolhida quando ele aceita.</summary>
        public async Task SetModeAsync(Device d, int modeIndex, (byte R, byte G, byte B)? color)
        {
            var m = d.Modes[modeIndex];
            if (color is { } c && m.HasModeColors && m.ColorsMax > 0)
            {
                m.ColorMode = 2; // MODE_COLORS_MODE_SPECIFIC
                int count = (int)Math.Max(1, m.ColorsMin);
                m.Colors = Enumerable.Repeat(c, count).ToList();
            }
            var body = new MemoryStream();
            var w = new BinaryWriter(body);
            w.Write((uint)0);           // tamanho, preenchido abaixo
            w.Write(modeIndex);
            WriteMode(w, m);
            var bytes = body.ToArray();
            BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 0);
            await SendAsync((uint)d.Index, 1101, bytes);                                    // UPDATEMODE
        }

        /// <summary>Salva as cores atuais como um perfil do OpenRGB (para aplicar sozinho ao ligar o PC).</summary>
        public Task SaveProfileAsync(string name) => SendAsync(0, 151, Encoding.ASCII.GetBytes(name + "\0"));   // REQUEST_SAVE_PROFILE

        // ---------- Protocolo ----------

        async Task SendAsync(uint device, uint packet, byte[] data)
        {
            var header = new byte[16];
            Encoding.ASCII.GetBytes("ORGB").CopyTo(header, 0);
            BitConverter.GetBytes(device).CopyTo(header, 4);
            BitConverter.GetBytes(packet).CopyTo(header, 8);
            BitConverter.GetBytes((uint)data.Length).CopyTo(header, 12);
            await stream!.WriteAsync(header);
            if (data.Length > 0) await stream.WriteAsync(data);
        }

        async Task<(uint Device, byte[] Data)> ReceiveAsync(uint expectedPacket)
        {
            while (true)
            {
                var header = await ReadExactly(16);
                if (Encoding.ASCII.GetString(header, 0, 4) != "ORGB") throw new IOException("Resposta inválida do OpenRGB.");
                uint dev = BitConverter.ToUInt32(header, 4), id = BitConverter.ToUInt32(header, 8), size = BitConverter.ToUInt32(header, 12);
                var data = size > 0 ? await ReadExactly((int)size) : Array.Empty<byte>();
                if (id == expectedPacket) return (dev, data);
                // Outros avisos do servidor (ex.: lista de dispositivos mudou) são ignorados
            }
        }

        async Task<byte[]> ReadExactly(int n)
        {
            var buf = new byte[n];
            int read = 0;
            while (read < n)
            {
                int r = await stream!.ReadAsync(buf.AsMemory(read, n - read));
                if (r == 0) throw new IOException("O OpenRGB fechou a conexão.");
                read += r;
            }
            return buf;
        }

        Device Parse(byte[] blob)
        {
            var r = new BinaryReader(new MemoryStream(blob));
            string Str() { int len = r.ReadUInt16(); var s = Encoding.UTF8.GetString(r.ReadBytes(len)); return s.TrimEnd('\0'); }
            var d = new Device();
            r.ReadUInt32();                         // tamanho
            d.Type = r.ReadInt32();
            d.Name = Str();
            if (protocol >= 1) d.Vendor = Str();
            d.Description = Str();
            Str();                                  // versão
            Str();                                  // número de série
            d.Location = Str();
            int modes = r.ReadUInt16();
            d.ActiveMode = r.ReadInt32();
            for (int i = 0; i < modes; i++)
            {
                var m = new Mode { Name = Str(), Value = r.ReadInt32(), Flags = r.ReadUInt32(), SpeedMin = r.ReadUInt32(), SpeedMax = r.ReadUInt32() };
                if (protocol >= 3) { m.BrightMin = r.ReadUInt32(); m.BrightMax = r.ReadUInt32(); }
                m.ColorsMin = r.ReadUInt32(); m.ColorsMax = r.ReadUInt32(); m.Speed = r.ReadUInt32();
                if (protocol >= 3) m.Bright = r.ReadUInt32();
                m.Direction = r.ReadUInt32(); m.ColorMode = r.ReadUInt32();
                int nc = r.ReadUInt16();
                for (int k = 0; k < nc; k++) { var b = r.ReadBytes(4); m.Colors.Add((b[0], b[1], b[2])); }
                d.Modes.Add(m);
            }
            int zones = r.ReadUInt16();
            for (int i = 0; i < zones; i++)
            {
                Str(); r.ReadInt32(); r.ReadUInt32(); r.ReadUInt32(); r.ReadUInt32();   // nome, tipo, min, max, quantidade
                int matrix = r.ReadUInt16();
                if (matrix > 0) r.ReadBytes(matrix);
            }
            d.LedCount = r.ReadUInt16();
            return d;                               // nomes dos LEDs e cores atuais não são usados
        }

        void WriteMode(BinaryWriter w, Mode m)
        {
            var name = Encoding.UTF8.GetBytes(m.Name + "\0");
            w.Write((ushort)name.Length); w.Write(name);
            w.Write(m.Value); w.Write(m.Flags); w.Write(m.SpeedMin); w.Write(m.SpeedMax);
            if (protocol >= 3) { w.Write(m.BrightMin); w.Write(m.BrightMax); }
            w.Write(m.ColorsMin); w.Write(m.ColorsMax); w.Write(m.Speed);
            if (protocol >= 3) w.Write(m.Bright);
            w.Write(m.Direction); w.Write(m.ColorMode);
            w.Write((ushort)m.Colors.Count);
            foreach (var c in m.Colors) { w.Write(c.R); w.Write(c.G); w.Write(c.B); w.Write((byte)0); }
        }

        public void Dispose()
        {
            stream?.Dispose();
            tcp?.Dispose();
        }
    }
}
