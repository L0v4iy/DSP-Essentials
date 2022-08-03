using System;
using System.Diagnostics;
using Unity.Collections;

namespace DSPGraph.Audio
{
    [DebuggerDisplay("Length = {Length}")]
    [BurstCompatible]
    public struct NativeFillBuffer : IDisposable
    {
        public int Length { get; private set; }

        private NativeArray<float> _buffer;

        public NativeFillBuffer(int length, Allocator allocator) : this()
        {
            _buffer = new NativeArray<float>(length, allocator);
            Length = 0;
        }

        public void ShiftBuffer(int offset)
        {
            // |: length marker
            // z: elements replaced with shift
            // o: undefined elements
            // zzzz1234|oooo -> 1234|1234oooo
            for (int i = 0; i < Length - offset; i++)
            {
                _buffer[i] = _buffer[i + offset];
            }

            Length -= offset;
        }

        /// <summary>
        /// Shift after Read!
        /// </summary>
        public void Read(ref NativeArray<float> to, int offset, int length)
        {
            // \read start (offset)
            // /read end   (length)
            // 1234\1234123/41234
            for (int i = 0; i < length; i++)
            {
                to[i] = _buffer[i + offset];
            }
        }

        public void Write(in NativeArray<float> from, int length)
        {
            // |: write after ("Length" marker)
            // /: write ends ("length" marker)
            // 12341234|1234/ooooo
            for (int i = 0; i < length; i++)
            {
                _buffer[i + Length] = from[i];
            }

            Length += length;
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();
        }
    }
}