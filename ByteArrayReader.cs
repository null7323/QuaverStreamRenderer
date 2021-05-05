using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace QQSConsole
{
    internal class ByteArrayReader : IDisposable
    {
        private byte[] content;
        public uint ContentSize { get; private set; }
        public uint Index;
        public ByteArrayReader(byte[] _Content)
        {
            if (_Content is null)
            {
                throw new ArgumentNullException(nameof(_Content));
            }
            content = _Content;
            ContentSize = (uint)_Content.Length;
            Index = 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Read()
        {
            return content[Index++];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(in uint len)
        {
            Index += len;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveBack()
        {
            if (Index != 0)
            {
                --Index;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveNext()
        {
            if (Index + 1 != ContentSize)
            {
                ++Index;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            content = null;
            ContentSize = 0;
            Index = 0;
            GC.SuppressFinalize(this);
        }
    }
}
