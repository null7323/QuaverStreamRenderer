using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SharpExtension;
using SharpExtension.Collections;

namespace QQSConsole
{
    public unsafe class RenderTrack
    {
        public Dictionary<long, UnmanagedList<Pointer<RenderNote>>> Notes = new();
        public Dictionary<long, Tempo> Tempos;
        
        private ForwardLinkedList<Pointer<RenderNote>>[] Unended;

        public long NoteCount = 0;
        private bool parsed = false;
        public bool TrackParsed => parsed;

        private long trkTime = 0;
        public long TrackTime => trkTime;

        private ByteArrayReader Reader;

        public int TrackIndex { get; }
        public long TrackSize { get; }

        private byte PreviousCommand;

        public RenderTrack(in TrackHeader header, Stream stream, Dictionary<long, Tempo> tempos)
        {
            TrackIndex = header.TrackIndex;
            TrackSize = header.TrackSize;

            Util.CopyTrackData(stream, header, out var buffer);
            Reader = buffer.GetByteIterator();

            Unended = new ForwardLinkedList<Pointer<RenderNote>>[256 * 16];
            Util.InitializeEach(Unended);

            Tempos = tempos;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseAll()
        {
            while (!parsed)
            {
                ParseNext();
            }
            Reader.Dispose();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureKey<T>(Dictionary<long, T> dict, in long key) where T : new()
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, new T());
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EndTrack()
        {
            foreach (var ll in Unended)
            {
                foreach (var n in ll)
                {
                    n.Ptr->End = trkTime;
                }
                ll.Clear();
            }
            Unended = null;
            parsed = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void ParseNext()
        {
            trkTime += Reader.ReadVariableLength();
            byte Command = Reader.Read();
            if (Command < 0x80)
            {
                Command = PreviousCommand;
                Reader.MoveBack();
            }
            PreviousCommand = Command;

            // get the high 4 bits of the command to simplify the comparison
            switch (Command & 0b11110000)
            {
                case 0x80:
                    // note off event.
                    {
                        byte channel = (byte)(Command & 0b00001111);
                        byte key = Reader.Read();
                        _ = Reader.Read();

                        var unended = Unended[key << 4 | channel];
                        if (unended.Any())
                        {
                            var n = unended.Pop();
                            n.Ptr->End = trkTime;
                        }
                    }
                    return;
                case 0x90:
                    // note on event
                    {
                        byte channel = (byte)(Command & 0b00001111);
                        byte key = Reader.Read();
                        byte vel = Reader.Read();

                        var unended = Unended[key << 4 | channel];
                        if (vel != 0)
                        {
                            RenderNote* n = (RenderNote*)UnsafeMemory.New<RenderNote>();
                            n->Key = key;
                            n->Start = trkTime;
                            n->Track = TrackIndex;
                            Pointer<RenderNote> p = new(n);
                            unended.Add(p);
                            EnsureKey(Notes, trkTime);
                            Notes[trkTime].Add(p);
                            ++NoteCount;
                        }
                        else
                        {
                            if (unended.Any())
                            {
                                var n = unended.Pop();
                                n.Ptr->End = trkTime;
                            }
                        }
                    }
                    return;
                case 0xA0:
                case 0xB0:
                case 0xE0:
                    // 0xA0: after touch
                    // 0xB0: control
                    // 0xE0: pitch wheel
                    {
                        Reader.Skip(2);
                    }
                    return;
                case 0xC0:
                case 0xD0:
                    // 0xC0: instrument event
                    // 0xD0: channel pressure
                    {
                        Reader.MoveNext();
                    }
                    return;
            }

            switch (Command)
            {
                case 0xF0:
                    while (Reader.Read() != 0xF7) ;
                    return;
                case 0xF1:
                    return;
                case 0xF2:
                case 0xF3:
                    Reader.Skip((uint)(0xF4 - Command));
                    return;
            }
            if (Command > 0xF3 && Command < 0xFF)
            {
                return;
            }

            Command = Reader.Read();
            if (Command > 0x00 && Command < 0x0A)
            {
                Reader.Skip(Reader.ReadVariableLength());
                return;
            }

            switch (Command)
            {
                case 0x00:
                    Reader.AssertByte(2);
                    Reader.Skip(2);
                    break;
                case 0x0A:
                    Reader.ParseData();
                    break;
                case 0x20:
                case 0x21:
                    Reader.Skip(2);
                    break;
                case 0x2F:
                    Reader.AssertByte(0);
                    EndTrack();
                    break;
                case 0x51:
                    Reader.AssertByte(3);
                    EnsureKey(Tempos, trkTime);
                    uint tempo = Reader.ReadInt24();
                    Tempos[trkTime] = new Tempo
                    {
                        Time = trkTime,
                        Value = tempo,
                        BPM = 60000000.0 / tempo
                    };
                    break;
                case 0xF4:
                case 0x58:
                    Reader.Skip(5);
                    break;
                case 0x59:
                    Reader.Skip(3);
                    break;
                case 0x7F:
                    Reader.Skip(Reader.ReadVariableLength());
                    break;
                default:
                    throw new TrackCorruptedException();
            }
        }
    }
}
