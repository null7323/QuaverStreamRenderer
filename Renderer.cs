using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using SharpExtension;
using SharpExtension.Collections;

namespace QQSConsole
{
    public class Renderer : IDisposable
    {
        RenderFile file;
        Canvas canvas;
        UnmanagedArray<RenderNote> notes;
        UnmanagedList<Tempo> tempos = new();
        public void Dispose()
        {
            file = null;
            notes.Dispose();
            tempos.Clear();
            tempos.Dispose();
            notes = null;
            tempos = null;
            GC.SuppressFinalize(this);
        }
        public Renderer(RenderFile renderFile)
        {
            file = renderFile;
            notes = renderFile.Notes;
            foreach (var t in file.TempoEvents.Keys)
            {
                tempos.Add(file.TempoEvents[t]);
            }
        }
        public double RenderProgress { get; private set; } = 0;
        public long NotesOnScreen { get; private set; } = 0;
        public double RenderFPS { get; private set; } = 0;
        public long FrameRendered { get; private set; } = 0;
        public bool IsRendering { get; private set; } = false;
        public double AverageFPS { get; private set; } = 0;
        private bool NoFFLog = false;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run(RenderOptions options, string path)
        {
            Run(options.Width, options.Height, options.KeyHeight, options.FPS, path,
                options.NoteSpeed, options.CRF, options.BarColor, options.NoFFMpegLog,
                options.Quiet, options.Preview);
        }


        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Run(int width, int height, int keyHeight, int fps, string path, float noteSpeed = 1, int crf = 13, uint lineColor = 0xFF000080, bool noFFMpegLog = false, 
            bool quiet = false, bool realtimePreview = false) // to do: 将全部参数打包成一个结构体
        {
            string ffarg = (noFFMpegLog || quiet ? "-loglevel quiet " : string.Empty) + (realtimePreview ? "-f sdl preview" : string.Empty);
            canvas = new Canvas(width, height, keyHeight, fps, path, crf, lineColor, ffarg);
            float ppb = 520.0F / file.FileHeader.Division * noteSpeed;
            UnmanagedList<Tempo>.Iterator spdptr = new(tempos);
            UnmanagedArray<RenderNote>.Enumerator noteptr;
            UnmanagedArray<RenderNote>.Enumerator noteStart = new(notes);
            noteStart.MoveNext();
            spdptr.MoveNext();
            //UnmanagedList<RenderNote>.Iterator noteEnd = notes.End();
            long fileTick = file.MaxMidiTime;
            double tick = -noteStart.Current.Start * 0.15, tickup, spd = (double)file.FileHeader.Division * 2 / canvas.FPS;
            int ppq = file.FileHeader.Division;
            uint j, k, l;
            System.Diagnostics.Stopwatch sw = new();
            System.Diagnostics.Stopwatch totalStopwatch = new();
            double deltaTick = (double)(height - keyHeight) / ppb;
            RenderProgress = 0;
            NotesOnScreen = 0;
            RenderFPS = 0;
            FrameRendered = 0;
            IsRendering = true;
            Task.Run(() =>
            {
                if (!NoFFLog) return;
                if (quiet) return;
                Console.WriteLine("\nRender Information: ");
                (int Left, int Top) = Console.GetCursorPosition();
                Console.CursorVisible = false;
                Tempo[] Tempos = file.TempoEvents.Values.ToArray();
                TimeSpan fullMidiTime = Util.ToMidiTime(file.MaxMidiTime, file.FileHeader.Division, Tempos);
                long totalFrameCount = (long)(fullMidiTime.TotalSeconds * fps);
                while (IsRendering)
                {
                    TimeSpan currMidiTime = Util.ToMidiTime(tick < 0 ? 0 : (long)tick, file.FileHeader.Division, Tempos);
                    Console.WriteLine("Progress: {0:F2}%        ", tick * 100 / fileTick);
                    Console.WriteLine("Time Elapsed: {0} min {1} s        ", totalStopwatch.Elapsed.Minutes, totalStopwatch.Elapsed.Seconds);
                    Console.WriteLine("Midi Time: {0} min {1} s / {2} min {3} s        ", currMidiTime.Minutes, currMidiTime.Seconds,
                        fullMidiTime.Minutes, fullMidiTime.Seconds);
                    Console.WriteLine("Frame: {0} / {1}       ", FrameRendered, totalFrameCount);
                    Console.WriteLine("Realtime FPS: {0:F3} ({1:F3}x)        ", RenderFPS, RenderFPS / fps);
                    Console.WriteLine("Average FPS: {0:F3} ({1:F3}x)        ", AverageFPS, AverageFPS / fps);
                    Console.WriteLine("Notes on Screen: {0}        ", NotesOnScreen);
                    //Console.Write("Progress: ");
                    Console.SetCursorPosition(Left, Top);
                }
                Console.CursorVisible = true;
                Console.WriteLine("Finished Render.       ");
                Console.WriteLine("Render Time Elapsed: {0} min {1} s    ", totalStopwatch.Elapsed.Minutes, totalStopwatch.Elapsed.Seconds);
            });
            totalStopwatch.Start();
            for (; tick <= fileTick; tick += spd)
            {
                sw.Restart();
                bool flg = false;
                noteptr = noteStart;
                canvas.Clear();
                tickup = tick + deltaTick;
                long notesDrawn = 0;
                while (spdptr.Current.Time <= tick)
                {
                    spd = (double)1e6 / spdptr.Current.Value * ppq / fps;
                    if (!spdptr.MoveNext()) break;
                }
                while (noteptr.Current.Start < tickup)
                {
                    RenderNote n = noteptr.Current;
                    if (n.End >= tick)
                    {
                        l = Shared.Color[n.Track % 96];
                        if (!flg && (flg = true)) noteStart = noteptr;
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
                    if (!noteptr.MoveNext()) break;
                }
                canvas.DrawKeys();
                canvas.WriteFrame();

                RenderProgress = tick / file.MaxMidiTime;
                NotesOnScreen = notesDrawn;
                RenderFPS = 10000000.0 / sw.ElapsedTicks;
                AverageFPS = (double)FrameRendered / totalStopwatch.ElapsedMilliseconds * 1000;
                ++FrameRendered;
                //System.Threading.SpinWait.SpinUntil(() => sw.ElapsedTicks > 60000);
                //Console.WriteLine("{0:F2}%      Notes Drawn: {1}        FPS: {2:F2}", tick * 100 / file.MaxMidiTime, noteDrawn,
                //    10000000.0 / sw.ElapsedTicks);
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
