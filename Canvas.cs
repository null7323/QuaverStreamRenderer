using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpExtension;
using SharpExtension.IO;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace QQSConsole
{
    public unsafe class Canvas
    {
        private static readonly short[] DrawMap = new short[128]
        {
            0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19, 21, 23, 24, 26, 28, 29,
            31, 33, 35, 36, 38, 40, 41, 43, 45, 47, 48, 50, 52, 53, 55, 57,
            59, 60, 62, 64, 65, 67, 69, 71, 72, 74, 76, 77, 79, 81, 83, 84,
            86, 88, 89, 91, 93, 95, 96, 98, 100, 101, 103, 105, 107, 108,
            110, 112, 113, 115, 117, 119, 120, 122, 124, 125, 127, 1, 3,
            6, 8, 10, 13, 15, 18, 20, 22, 25, 27, 30, 32, 34, 37, 39, 42, 44,
            46, 49, 51, 54, 56, 58, 61, 63, 66, 68, 70, 73, 75, 78, 80, 82,
            85, 87, 90, 92, 94, 97, 99, 102, 104, 106, 109, 111, 114, 116,
            118, 121, 123, 126
        };
        private static readonly short[] genkeyx = new short[12]
        {
            0, 12, 18, 33, 36, 54, 66, 72, 85, 90, 105, 108
        };
        readonly int Width;
        readonly int Height;
        readonly int keyh;
        readonly int fps;
        CStream ffpipe;
        readonly int[] keyx = new int[128];
        readonly int[] keyw = new int[128];
        public uint[] keycolor = new uint[128];
        readonly uint linc;
        public int CanvasWidth => Width;
        public int CanvasHeight => Height;
        public int KeyHeight => keyh;
        public int FPS => fps;
        uint** data = null;
        uint* keyColors = null;
        uint* emptyFrame = null;
        uint* frame = null;

        //Task renderPush = null;
        //public ConcurrentQueue<IntPtr> FrameList = new();
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public Canvas(int width, int height, int keyHeight, int fps, string videoName, int crf = 13, uint lineColor = 0xFF000080, string ffArg = "")
        {
            Width = width;
            Height = height;
            keyh = keyHeight;
            this.fps = fps;
            string ffarg = string.Concat("ffmpeg -y -hide_banner -f rawvideo -pix_fmt rgba -s ", width, "x", height,
                " -r ", fps, " -i - -pix_fmt yuv420p -preset ultrafast -crf ", crf, " \"", videoName + "\" ", ffArg);
            ffpipe = CStream.OpenPipe(ffarg, "wb");
            frame = (uint*)UnsafeMemory.Allocate((ulong)width * (ulong)height * 4);
            
            UnsafeMemory.Set(frame, 0, (ulong)width * (ulong)height * 4);
            data = (uint**)UnsafeMemory.Allocate((ulong)height * (ulong)sizeof(void*));
            for (uint i = 0; i < height; ++i)
            {
                data[i] = frame + (height - 1 - i) * width;
            }
            for (int i = 0; i != 128; ++i)
            {
                keyx[i] = (i / 12 * 126 + genkeyx[i % 12]) * Width / 1350;
            }
            for (int i = 0; i != 127; ++i)
                keyw[i] = (i % 12) switch
                {
                    1 or 3 or 6 or 8 or 10 => width * 9 / 1350,
                    4 or 11 => keyx[i + 1] - keyx[i],
                    _ => keyx[i + 2] - keyx[i],
                };
            keyw[127] = width - keyx[127];
            linc = lineColor;

            for (int i = 0; i != 128; ++i)
                keycolor[i] = (i % 12) switch
                {
                    1 or 3 or 6 or 8 or 10 => 0xFF000000,
                    _ => 0xFFFFFFFF,
                };
            keyColors = (uint*)UnsafeMemory.Allocate(512);
            fixed (uint* p = keycolor) UnsafeMemory.Copy(keyColors, p, 512);
            emptyFrame = (uint*)UnsafeMemory.Allocate((ulong)width * (ulong)height * 4);
            for (int i = 0; i < Height; ++i)
                for (int j = 0; j < Width; ++j)
                    data[i][j] = 0xFF000000;
            UnsafeMemory.Copy(emptyFrame, frame, (ulong)width * (ulong)height * 4);
            //renderPush = Task.Run(() =>
            //{
            //    while (!destoryed)
            //    {
            //        if (!FrameList.IsEmpty)
            //        {
            //            FrameList.TryDequeue(out IntPtr f);
            //            void* currFrame = f.ToPointer();
            //            ffpipe.Write(currFrame, (ulong)Width * (ulong)Height, 4);
            //            UnsafeMemory.Free(currFrame);
            //        }
            //    }
            //});
        }
        public void Destroy()
        {
            if (data != null)
            {
                UnsafeMemory.Free(data);
                data = null;
            }
            if (ffpipe != null)
            {
                ffpipe.Close();
                ffpipe = null;
            }
            if (keyColors != null)
            {
                UnsafeMemory.Free(keyColors);
            }
            if (frame != null)
            {
                UnsafeMemory.Free(frame);
                frame = null;
            }
            if (emptyFrame != null)
            {
                UnsafeMemory.Free(emptyFrame);
                emptyFrame = null;
            }
            //if (renderPush != null)
            //{
            //    renderPush.Wait();
            //}
            keyColors = null;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            //for (int i = 0; i != 128; ++i)
            //    keycolor[i] = (i % 12) switch
            //    {
            //        1 or 3 or 6 or 8 or 10 => 0xFF000000,
            //        _ => 0xFFFFFFFF,
            //    };
            fixed (uint* p = keycolor) UnsafeMemory.Copy(p, keyColors, 512);
            //for (int i = keyh; i < Height; ++i)
            //{
            //    //for (int j = 0; j < Width; ++j)
            //    //data[i][j] = 0xFF000000;
            //    UnsafeMemory.Copy(data[i], emptyData, (ulong)Width * 4);
            //}
            UnsafeMemory.Copy(frame, emptyFrame, (ulong)(Width * Height) * 4);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFrame()
        {
            ffpipe.WriteWithoutLock(frame, (ulong)Width * (ulong)Height * 4, 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void DrawKeys()
        {
            int i, j, bh = keyh * 64 / 100;
            for (i = 0; i < 75; ++i)
            {
                j = DrawMap[i];
                FillRectangle(keyx[j], 0, keyw[j], keyh, keycolor[j]);
                DrawRectangle(keyx[j], 0, keyw[j] + 1, keyh, 0xFF000000);
            }
            while (i < 128)
            {
                j = DrawMap[i++];
                FillRectangle(keyx[j], keyh - bh, keyw[j], bh, keycolor[j]);
                DrawRectangle(keyx[j], keyh - bh, keyw[j] + 1, bh, 0xFF000000);
            }
            FillRectangle(0, keyh - 2, Width, keyh / 15, linc);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawNote(short k, int y, int h, uint c)
        {
            if (h > 5) --h;
            if (h < 1) h = 1;
            FillRectangle(keyx[k] + 1, y, keyw[k] - 1, h, c);
        }
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void DrawRectangle(int x, int y, int w, int h, uint c)
        {
            int i;
            if (x < Width)
                for (i = y; i < y + h; ++i)
                    data[i][x] = c;
            if (y < Height)
                for (i = x; i < x + w; ++i)
                    data[y][i] = c;
            if (w > 1)
                for (i = y; i < y + h; ++i)
                    data[i][x + w - 1] = c;
            if (h > 1)
                for (i = x; i < x + w; ++i)
                    data[y + h - 1][i] = c;
        }
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void FillRectangle(int x, int y, int w, int h, uint c)
        {
            for (int i = x, xend = x + w; i != xend; ++i)
                for (int j = y, yend = y + h; j != yend; ++j)
                    data[j][i] = c;
        }
    }
}
