using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text;
using SharpExtension.IO;
using SharpExtension.Collections;

namespace QQSConsole
{
#pragma warning disable IDE0049
    public enum MidiFormat
    {
        SingleTrack,
        MultiSyncTracks, // 多个同步音轨
        MultiAsyncTracks // 暂不支持
    }
    public struct MidiHeader
    {
        public Int32 HeaderSize;
        public Int32 TrackCount;
        public Int32 Division;
        public MidiFormat Format;
    }
    public struct TrackHeader
    {
        public Int64 TrackStartPosition;
        public UInt32 TrackSize;
        public UInt16 TrackIndex;
    }
    public struct Tempo
    {
        public long Time;
        public uint Value;
        public double BPM;
    }
    internal static class Util
    {
        public static MidiHeader ParseMidiHeader(Stream stream)
        {
            stream.Position = 0;
            foreach (var c in "MThd")
            {
                if (stream.ReadByte() != c)
                {
                    throw new MidiHeaderCorruptedException();
                }
            }
            UInt32 hdrSize = stream.ReadInt32();
            if (hdrSize != 6)
            {
                throw new MidiHeaderCorruptedException();
            }
            MidiHeader hdr = new()
            {
                Format = GetFormat(stream),
                TrackCount = stream.ReadInt16(),
                Division = stream.ReadInt16(),
                HeaderSize = Convert.ToInt32(hdrSize)
            };
            return hdr;
        }
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static TrackHeader[] ParseTrackHeaders(Stream stream)
        {
            stream.Position = 10;
            UInt16 trackCount = stream.ReadInt16();
            stream.ReadInt16();
            TrackHeader[] headers = new TrackHeader[trackCount];
            // parse each track
            for (UInt16 trackIndex = 0; trackIndex != trackCount; ++trackIndex)
            {
                foreach (var c in "MTrk")
                {
                    if (stream.ReadByte() != c)
                    {
                        throw new TrackHeaderCorruptedException();
                    }
                }
                headers[trackIndex] = new TrackHeader
                {
                    TrackIndex = trackIndex,
                    TrackSize = stream.ReadInt32(),
                    TrackStartPosition = stream.Position
                };
                stream.Position += headers[trackIndex].TrackSize;
            }
            return headers;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CopyTrackData(Stream stream, in TrackHeader header, out Byte[] buffer)
        {
            buffer = new byte[header.TrackSize];

            stream.Position = header.TrackStartPosition;
            stream.Read(buffer, 0, Convert.ToInt32(header.TrackSize));

            // to do: 使用非托管内存替换
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Stream OpenAsStream(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException();
            }
            return new StreamReader(filePath).BaseStream;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Stream OpenAsStream(Uri uri)
        {
            return new StreamReader(uri.AbsoluteUri).BaseStream;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16 ReadInt16(this Stream stream)
        {
            //UInt16 result = 0;
            //result = (UInt16)((result << 8) | stream.ReadByte());
            //result = (UInt16)((result << 8) | stream.ReadByte());
            return (UInt16)((stream.ReadByte() << 8) | stream.ReadByte());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt16 ReadInt16(this ByteArrayReader reader)
        {
            return (UInt16)((reader.Read() << 8) | reader.Read());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 ReadInt32(this Stream stream)
        {
            return (UInt32)((((((stream.ReadByte() << 8) | stream.ReadByte()) << 8) | stream.ReadByte()) << 8) | stream.ReadByte());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 ReadInt32(this ByteArrayReader reader)
        {
            return (UInt32)((((((reader.Read() << 8) | reader.Read()) << 8) | reader.Read()) << 8) | reader.Read());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 ReadInt24(this ByteArrayReader reader)
        {
            return (UInt32)((((reader.Read() << 8) | reader.Read()) << 8) | reader.Read());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string ParseString(this ByteArrayReader reader)
        {
            UInt32 len = reader.ReadVariableLength();
            Char[] data = new Char[len];
            for (UInt32 i = 0; i != len; i++)
            {
                data[i] = (char)reader.Read();
            }
            return new string(data);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Byte[] ParseData(this ByteArrayReader reader)
        {
            UInt32 len = reader.ReadVariableLength();
            Byte[] data = new Byte[len];
            for (UInt32 i = 0; i != len; i++)
            {
                data[i] = reader.Read();
            }
            return data;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string AsString(this byte[] data)
        {
            char[] str = new char[data.Length];
            for (int i = 0, maxLoop = data.Length; i != maxLoop; i++)
            {
                str[i] = (char)data[i];
            }
            return new string(str);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MidiFormat GetFormat(this Stream fileStream)
        {
            fileStream.Position = 8;
            UInt16 format = ReadInt16(fileStream);
            return (MidiFormat)format;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 ReadVariableLength(Stream stream)
        {
            UInt32 result = 0;
            Byte b;
            do
            {
                b = (Byte)stream.ReadByte();
                result = (result << 7) | (UInt32)(b & 0x7F);
            }
            while ((b & 0b10000000) != 0);
            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt32 ReadVariableLength(this ByteArrayReader reader)
        {
            UInt32 result = 0;
            Byte b;
            do
            {
                b = reader.Read();
                result = (result << 7) | (UInt32)(b & 0x7F);
            }
            while ((b & 0b10000000) != 0);
            return result;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ByteArrayReader GetByteIterator(this Byte[] byteArray)
        {
            return new ByteArrayReader(byteArray);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AssertByte(this ByteArrayReader reader, in Byte val)
        {
            Byte result = reader.Read();
            if (result != val)
            {
                throw new AssertionFailedException(val, result);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeEach<T>(T[] allocatedUninitializedArray) where T : new()
        {
            int arrayLength = allocatedUninitializedArray.Length;
            for (int i = 0; i != arrayLength; ++i)
            {
                allocatedUninitializedArray[i] = new T();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static TimeSpan ToMidiTime(long MaxMidiTime, int Division, Tempo[] TempoList)
        {
            if (TempoList == null) return new TimeSpan(5000000 / Division * MaxMidiTime);
            else
            {
                if (MaxMidiTime == 0) return new TimeSpan(0);
                long ticks = 0;
                uint tempo = 500000;
                long lastEventTime = 0;
                foreach (var Tempo in TempoList)
                {
                    if (Tempo.Time > MaxMidiTime) break;
                    long deltaTime = Tempo.Time - lastEventTime;
                    ticks += tempo * 10 / Division * deltaTime;
                    lastEventTime = Tempo.Time;
                    tempo = Tempo.Value;
                }
                ticks += tempo * 10 / Division * (MaxMidiTime - lastEventTime);
                return new TimeSpan(ticks);
            }
        }
    }
#pragma warning restore IDE0049
}
