using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpExtension;
using SharpExtension.Collections;

namespace QQSConsole
{
    public struct RenderNote
    {
        public byte Key;
        public int Track;
        public long Start;
        public long End;
    }
    
    public unsafe class MTRenderer : IDisposable // 多线程
    {
        RenderFile file;
        Canvas canvas;
        readonly long noteStart;
        UnmanagedList<Tempo> tempos = new();
        public MTRenderer(RenderFile renderFile, bool removeOverlap = true)
        {
            file = renderFile;
            for (int i = 0; i != 128; ++i)
            {
                notes.Add(i, new());
            }
            foreach (var n in renderFile.Notes)
            {
                notes[n.Key].Add(n);
            }
            foreach (var t in renderFile.TempoEvents.Keys)
            {
                tempos.Add(renderFile.TempoEvents[t]);
            }
            //for (int i = 0; i != 128; ++i)
            //{
            //    iteratorBegins[i] = 0;
            //}
            Console.WriteLine("Processing Midi File...");
            noteStart = renderFile.Notes[0].Start;
            if (!removeOverlap) return;
            Parallel.ForEach(notes.Values, (nl) =>
            {
                for (long index = 0, maximum = nl.Count - 2; index != maximum; ++index)
                {
                    ref RenderNote curr = ref nl[index];
                    ref RenderNote next = ref nl[index + 1];
                    if (curr.Start < next.Start && curr.End > next.Start && curr.End < next.End)
                    {
                        curr.End = next.Start;
                    }
                    else if (curr.Start == next.Start && curr.End <= next.End)
                    {
                        curr.End = curr.Start;
                    }
                }
            });
            foreach (var noteList in notes.Values)
            {
                noteList.TrimExcess();
            }
        }
        public double RenderProgress { get; private set; } = 0;
        public long NotesOnScreen { get; private set; } = 0;
        public double RenderFPS { get; private set; } = 0;
        public long FrameRendered { get; private set; } = 0;
        public double AverageFPS { get; private set; } = 0;
        public bool IsRendering { get; private set; } = false;
        Dictionary<int, UnmanagedList<RenderNote>> notes = new();
        //long[] iteratorBegins = new long[128];
        public void Dispose()
        {
            if (notes != null)
            {
                foreach (var l in notes.Values)
                {
                    l.Dispose();
                }
            }
            notes = null;
            file = null;
            tempos.Dispose();
            tempos = null;
            //iteratorBegins = null;
            GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run(RenderOptions options, string path)
        {
            Run(options.Width, options.Height, options.KeyHeight, options.FPS, path,
                options.NoteSpeed, options.CRF, options.BarColor, options.MaxThread,
                options.NoFFMpegLog, options.Quiet, options.Preview);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Run(int width, int height, int keyHeight, int fps, string path, float noteSpeed = 1, int crf = 13, uint lineColor = 0xFF000080,
            int maxThread = -1, bool noFFArg = false, bool quiet = false, bool realtimePreview = false)
        {
            string ffarg = (noFFArg || quiet ? "-loglevel quiet " : string.Empty) + (realtimePreview ? "-f sdl preview" : string.Empty);
            maxThread = maxThread <= 0 ? Environment.ProcessorCount : maxThread;
            canvas = new Canvas(width, height, keyHeight, fps, path, crf, lineColor, ffarg);
            float ppb = 520.0F / file.FileHeader.Division * noteSpeed;
            UnmanagedList<Tempo>.Iterator spdptr = new(tempos);
            spdptr.MoveNext();
            long fileTick = file.MaxMidiTime;
            double tick = -noteStart * 0.15, tickup, spd = (double)file.FileHeader.Division * 2 / canvas.FPS;
            int ppq = file.FileHeader.Division;
            System.Diagnostics.Stopwatch sw = new();
            System.Diagnostics.Stopwatch totalStopwatch = new();
            double deltaTick = (double)(height - keyHeight) / ppb;
            RenderProgress = 0;
            NotesOnScreen = 0;
            RenderFPS = 0;
            FrameRendered = 0;
            IsRendering = true;
            long* iteratorBegins = stackalloc long[128];
            UnsafeMemory.Set(iteratorBegins, 0, 1024);
            Task.Run(() =>
            {
                if (!noFFArg) return;
                if (quiet) return;
                Console.WriteLine("\nRender Information: ");
                Console.WriteLine("Progress: ");
                (int Left, int Top) = Console.GetCursorPosition();
                Console.CursorVisible = false;
                Tempo[] Tempos = file.TempoEvents.Values.ToArray();

                TimeSpan fullMidiTime = Util.ToMidiTime(file.MaxMidiTime, file.FileHeader.Division, Tempos);
                long totalFrameCount = (long)(fullMidiTime.TotalSeconds * fps);
                (int Left, int Top) endpos = new();
                StringBuilder progressBuilder = new();
                while (IsRendering)
                {
                    TimeSpan currMidiTime = Util.ToMidiTime(tick < 0 ? 0 : (long)tick, file.FileHeader.Division, Tempos);
                    //Console.WriteLine("Progress: {0:F2}%        ", tick * 100 / fileTick);
                    //Console.WriteLine("Time Elapsed: {0} min {1} s        ", totalStopwatch.Elapsed.Minutes, totalStopwatch.Elapsed.Seconds);
                    //Console.WriteLine("Midi Time: {0} min {1} s / {2} min {3} s        ", currMidiTime.Minutes, currMidiTime.Seconds, 
                    //    fullMidiTime.Minutes, fullMidiTime.Seconds);
                    //Console.WriteLine("Frame: {0} / {1}       ", FrameRendered, totalFrameCount);
                    //Console.WriteLine("Realtime FPS: {0:F3} ({1:F3}x)        ", RenderFPS, RenderFPS / fps);
                    //Console.WriteLine("Average FPS: {0:F3} ({1:F3}x)        ", AverageFPS, AverageFPS / fps);
                    //Console.WriteLine("Notes on Screen: {0}        ", NotesOnScreen);
                    Console.Write("{0:F2}%\t  ", tick * 100 / fileTick);
                    int totalBlockCount = 50;
                    int printBlockCount = (int)(tick * 100 / fileTick / 2);
                    progressBuilder.Clear();
                    progressBuilder.Append(Shared.ProgressBegin);
                    for (int k = 0; k != printBlockCount; ++k)
                    {
                        progressBuilder.Append(Shared.ProgressFill);
                    }
                    for (int k = 0; k != totalBlockCount - printBlockCount; ++k)
                    {
                        progressBuilder.Append('─');
                    }
                    progressBuilder.Append(Shared.ProgressEnd);
                    progressBuilder.Append("    ");
                    progressBuilder.Append("FPS: ");
                    progressBuilder.Append((int)RenderFPS);
                    progressBuilder.Append("        ");

                    Console.Write(progressBuilder.ToString());
                    Console.WriteLine("    \n");
                    Console.WriteLine("Time Elapsed: {0} min {1} s        ", totalStopwatch.Elapsed.Minutes, totalStopwatch.Elapsed.Seconds);
                    Console.WriteLine("Midi Time: {0} min {1} s / {2} min {3} s        ", currMidiTime.Minutes, currMidiTime.Seconds,
                        fullMidiTime.Minutes, fullMidiTime.Seconds);
                    Console.WriteLine("Frame: {0} / {1}       ", FrameRendered, totalFrameCount);
                    Console.WriteLine("Realtime FPS: {0:F3} ({1:F3}x)        ", RenderFPS, RenderFPS / fps);
                    Console.WriteLine("Average FPS: {0:F3} ({1:F3}x)        ", AverageFPS, AverageFPS / fps);
                    Console.WriteLine("Notes on Screen: {0}        ", NotesOnScreen);
                    endpos = Console.GetCursorPosition();
                    Console.SetCursorPosition(Left, Top);
                }
                Console.CursorVisible = true;
                Console.SetCursorPosition(endpos.Left, endpos.Top);
                Console.WriteLine("Finished Render.       ");
                Console.WriteLine("Render Time Elapsed: {0} min {1} s    ", totalStopwatch.Elapsed.Minutes, totalStopwatch.Elapsed.Seconds);
            });
            totalStopwatch.Start();
            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = maxThread };
            for (; tick <= fileTick; tick += spd)
            {
                canvas.Clear();
                tickup = tick + deltaTick;
                long notesDrawn = 0;
                sw.Restart();
                while (spdptr.Current.Time <= tick)
                {
                    spd = (double)1e6 / spdptr.Current.Value * ppq / fps;
                    if (!spdptr.MoveNext()) break;
                }
                Parallel.For(0, 128, parallelOptions, (i) =>
                {
                    UnmanagedList<RenderNote> nl = notes[i];
                    uint j, k, l;
                    bool flg = false;
                    long noteptr = iteratorBegins[i];
                    long count = nl.Count - 1;
                    RenderNote n;
                    while (nl[noteptr].Start < tickup)
                    {
                        n = nl[noteptr];
                        if (n.End >= tick)
                        {
                            l = Shared.Color[n.Track % 96];
                            if (!flg && (flg = true)) iteratorBegins[i] = noteptr;
                            if (n.Start < tick)
                            {
                                k = (uint)keyHeight;
                                j = (uint)((float)(n.End - tick) * ppb);
                                canvas.keycolor[n.Key] = l;
                            }
                            else
                            {
                                k = (uint)((n.Start - tick) * ppb + keyHeight);
                                j = (uint)((n.End - n.Start) * ppb);
                            }
                            if (j + k > height) j = (uint)(height - k);
                            canvas.DrawNote(n.Key, (int)k, (int)j, l);
                            ++notesDrawn;
                        }
                        if (noteptr >= count) break;
                        ++noteptr;
                    }
                });
                canvas.DrawKeys();
                canvas.WriteFrame();

                RenderProgress = tick / file.MaxMidiTime;
                NotesOnScreen = notesDrawn;
                RenderFPS = 10000000.0 / sw.ElapsedTicks;
                AverageFPS = (double)FrameRendered * 1000 / totalStopwatch.ElapsedMilliseconds;
                ++FrameRendered;
            }
            canvas.Clear();
            canvas.DrawKeys();
            for (int i = 0; i != 300; i++)
            {
                canvas.WriteFrame();
            }
            canvas.Destroy();
            IsRendering = false;
        }
    }
}
