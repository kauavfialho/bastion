using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Bastion.Core
{
    /// <summary>Contadores de um teste em andamento, lidos pela tela a cada segundo.</summary>
    public sealed class StressRun
    {
        public long CpuOps;
        public long RamPasses, RamErrors, RamBytesChecked, RamAllocated;
        public double DiskWriteMBs, DiskReadMBs, DiskIops;
        public string Phase = "";
    }

    /// <summary>Cargas de teste: CPU (todos os núcleos), RAM (grava e confere padrões) e disco (velocidade real, sem cache).</summary>
    public static class Stress
    {
        /// <summary>Um thread por núcleo fazendo contas de ponto flutuante sem parar.</summary>
        public static Task Cpu(StressRun run, CancellationToken ct)
        {
            var threads = Enumerable.Range(0, Environment.ProcessorCount).Select(i =>
            {
                var t = new Thread(() =>
                {
                    double a = 1.0001 + i, b = 0.9999, c = 0;
                    while (!ct.IsCancellationRequested)
                    {
                        for (int k = 0; k < 2_000_000; k++)
                        {
                            c = c * b + a;           // multiplicação + soma, o que mais esquenta a CPU
                            a = a * 0.999999 + 1e-9;
                            b = b * 1.000001 - 1e-9;
                        }
                        Interlocked.Add(ref run.CpuOps, 2_000_000);
                        if (double.IsNaN(c)) c = 0;
                    }
                }) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
                t.Start();
                return t;
            }).ToList();
            return Task.Run(() => threads.ForEach(t => t.Join()));
        }

        /// <summary>
        /// Enche boa parte da RAM livre com padrões e confere se cada valor volta igual.
        /// Qualquer diferença é erro de memória (RAM com defeito ou instável).
        /// </summary>
        public static Task Ram(StressRun run, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                const int chunkLongs = 8 * 1024 * 1024;              // 64 MB por bloco
                long want = (long)Math.Clamp(HwMonitor.AvailablePhys() * 0.6, 512L << 20, 16L << 30);
                int chunks = (int)(want / (chunkLongs * 8L));
                var blocks = new System.Collections.Generic.List<ulong[]>();
                try
                {
                    for (int i = 0; i < chunks && !ct.IsCancellationRequested; i++)
                    {
                        blocks.Add(GC.AllocateUninitializedArray<ulong>(chunkLongs));
                        Interlocked.Add(ref run.RamAllocated, chunkLongs * 8L);
                    }
                }
                catch (OutOfMemoryException) { }

                int workers = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
                var tasks = Enumerable.Range(0, workers).Select(w => Task.Factory.StartNew(() =>
                {
                    ulong seed = (ulong)(w + 1) * 0x9E3779B97F4A7C15UL;
                    while (!ct.IsCancellationRequested)
                    {
                        seed = seed * 6364136223846793005UL + 1442695040888963407UL;
                        for (int b = w; b < blocks.Count && !ct.IsCancellationRequested; b += workers)
                        {
                            var block = blocks[b];
                            ulong s = seed ^ (ulong)b;
                            for (int i = 0; i < block.Length; i++) block[i] = s ^ ((ulong)i * 0xD6E8FEB86659FD93UL);
                            long errors = 0;
                            for (int i = 0; i < block.Length; i++)
                                if (block[i] != (s ^ ((ulong)i * 0xD6E8FEB86659FD93UL))) errors++;
                            if (errors > 0) Interlocked.Add(ref run.RamErrors, errors);
                            Interlocked.Add(ref run.RamBytesChecked, block.Length * 8L);
                        }
                        Interlocked.Increment(ref run.RamPasses);
                    }
                }, TaskCreationOptions.LongRunning)).ToArray();
                Task.WaitAll(tasks);
                blocks.Clear();
                GC.Collect();
            });
        }

        const FileOptions NoBuffering = (FileOptions)0x20000000;

        /// <summary>
        /// Grava e lê um arquivo de teste direto no disco (sem o cache do Windows) e mede leituras
        /// aleatórias de 4 KB, que é o que mais pesa ao abrir jogos e programas.
        /// </summary>
        public static unsafe Task Disk(StressRun run, string folder, long size, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                const int block = 4 << 20;
                var file = Path.Combine(folder, "bastion-teste-disco.tmp");
                void* buf = NativeMemory.AlignedAlloc(block, 4096);
                try
                {
                    new Random(1).NextBytes(new Span<byte>(buf, block));   // dados aleatórios: SSD não "comprime" o teste
                    using (var h = File.OpenHandle(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None, NoBuffering | FileOptions.WriteThrough, size))
                    {
                        run.Phase = "Gravando";
                        var sw = Stopwatch.StartNew();
                        long done = 0;
                        for (; done < size && !ct.IsCancellationRequested; done += block)
                            RandomAccess.Write(h, new ReadOnlySpan<byte>(buf, block), done);
                        run.DiskWriteMBs = done / 1048576.0 / sw.Elapsed.TotalSeconds;

                        run.Phase = "Lendo";
                        sw.Restart();
                        long read = 0;
                        for (; read < done && !ct.IsCancellationRequested; read += block)
                            RandomAccess.Read(h, new Span<byte>(buf, block), read);
                        run.DiskReadMBs = read / 1048576.0 / sw.Elapsed.TotalSeconds;

                        run.Phase = "Leitura aleatória 4K";
                        var rnd = new Random(2);
                        long pages = Math.Max(1, done / 4096);
                        long ops = 0;
                        sw.Restart();
                        while (sw.Elapsed.TotalSeconds < 8 && !ct.IsCancellationRequested)
                        {
                            RandomAccess.Read(h, new Span<byte>(buf, 4096), rnd.NextInt64(pages) * 4096);
                            ops++;
                        }
                        run.DiskIops = ops / sw.Elapsed.TotalSeconds;
                    }
                }
                finally
                {
                    NativeMemory.AlignedFree(buf);
                    try { File.Delete(file); } catch { }
                    run.Phase = "Concluído";
                }
            });
        }
    }
}
