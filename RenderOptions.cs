using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QQSConsole
{
    public struct RenderOptions
    {
        public int FPS;
        public int CRF;
        public bool Quiet;
        public bool NoFFMpegLog;
        public bool Preview;
        public int MaxThread;
        public uint BarColor;
        public int Width;
        public int Height;
        public int KeyHeight;
        public float NoteSpeed;
    }
}
