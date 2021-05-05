using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using SharpExtension;
using SharpExtension.Collections;

namespace QQSConsole
{
    public unsafe class RenderFile
    {
        public MidiHeader FileHeader;
        private readonly RenderTrack[] Tracks;
        public UnmanagedArray<RenderNote> Notes;
        public Dictionary<long, Tempo> TempoEvents = new();

        public long NoteCount { get; private set; } = 0;
        public long MaxMidiTime { get; private set; } = 0;

        public static readonly long TrackLoadThreshold = 80000000;
        public static readonly long MaxWaitingTracks = 1000;

        public RenderFile(string fileName, bool quiet = false)
        {
            Dictionary<long, UnmanagedList<RenderNote>> NoteMap;
            using Stream stream = Util.OpenAsStream(fileName);
            FileHeader = Util.ParseMidiHeader(stream);
            var trkHeaders = Util.ParseTrackHeaders(stream);

            Tracks = new RenderTrack[FileHeader.TrackCount];
            ConcurrentQueue<RenderTrack> cTracks = new();
            bool isParsing = true;
            bool isAdding = true;
            List<Task> taskpool = new();
            Task.Run(() =>
            {
                List<Task> completedTasks = new();
                while (isParsing || taskpool.Count != 0)
                {
                    completedTasks.Clear();
                    foreach (var t in taskpool)
                    {
                        if (t.IsCompleted)
                        {
                            completedTasks.Add(t);
                        }
                    }
                    if (completedTasks.Count != 0)
                    {
                        foreach (var t in completedTasks)
                        {
                            taskpool.Remove(t);
                        }
                    }
                    SpinWait.SpinUntil(() => false, 1000);
                }
            });
            Task.Run(() =>
            {
                int parsedTrackCount = 0;
                while (isAdding || !cTracks.IsEmpty)
                {
                    if (cTracks.TryDequeue(out RenderTrack trk))
                    {
                        if (trk.TrackSize > TrackLoadThreshold)
                        {
                            taskpool.Add(Task.Run(() =>
                            {
                                trk.ParseAll();
                                //if (GlobalSettings.EnableParserConsoleOutput)
                                {
                                    NoteCount += trk.NoteCount;
                                    if (!quiet)
                                        Console.Write("Parsed Track " + ++parsedTrackCount + 
                                            " / " + Tracks.Length + ", Total Note Count: " + NoteCount + "        \r");
                                }
                                if (trk.TrackTime > MaxMidiTime) MaxMidiTime = trk.TrackTime;
                            }));
                        }
                        else
                        {
                            trk.ParseAll();
                            //if (GlobalSettings.EnableParserConsoleOutput)
                            {
                                foreach (var v in trk.Notes.Values)
                                {
                                    NoteCount += v.Count;
                                }
                                if (!quiet)
                                    Console.Write("Parsed Track " + ++parsedTrackCount +
                                        " / " + Tracks.Length + ", Total Note Count: " + NoteCount + "        \r");
                            }
                            if (trk.TrackTime > MaxMidiTime) MaxMidiTime = trk.TrackTime;
                        }
                    }
                }
                SpinWait.SpinUntil(() => taskpool.Count == 0);
                isParsing = false;
            });
            for (int i = 0; i != FileHeader.TrackCount; i++)
            {
                Tracks[i] = new RenderTrack(trkHeaders[i], stream, TempoEvents);
                cTracks.Enqueue(Tracks[i]);
                if (cTracks.Count > MaxWaitingTracks)
                {
                    SpinWait.SpinUntil(() => cTracks.Count < MaxWaitingTracks / 2);
                }
            }
            isAdding = false;
            SpinWait.SpinUntil(() => !isParsing);
            if (!quiet) Console.WriteLine("\nSorting Events...");
            NoteMap = new((int)Math.Min(NoteCount, int.MaxValue));
            foreach (var trk in Tracks)
            {
                foreach (var key in trk.Notes.Keys)
                {
                    if (!NoteMap.ContainsKey(key))
                    {
                        NoteMap.Add(key, new(trk.Notes[key].Count + 1));
                    }
                    var l = NoteMap[key];
                    var iterator = new UnmanagedList<Pointer<RenderNote>>.Iterator(trk.Notes[key]);
                    while (iterator.MoveNext())
                    {
                        RenderNote* _Ptr = iterator.Current.Ptr;
                        l.Add(*_Ptr);
                        UnsafeMemory.Free(_Ptr);
                    }
                    trk.Notes[key].Dispose();
                }
                trk.Notes = null;
            }
            Tracks = null;
            //NoteMap = NoteMap.OrderBy(p => p.Key).ToDictionary(p => p.Key, v => v.Value);
            var keys = NoteMap.Keys.ToArray();
            Array.Sort(keys);
            Notes = new(NoteCount);
            RenderNote* _DestPtr = null;
            fixed (RenderNote* first = &Notes[0])
            {
                _DestPtr = first;
            }
            foreach (var k in keys)
            {
                var l = NoteMap[k];
                l.CopyTo(_DestPtr);
                _DestPtr += l.Count;
                l.Dispose();
            }
        }
    }
}
