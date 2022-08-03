using System;
using Unity.Collections;

namespace DSPGraph.Audio
{
    [BurstCompatible]
    public struct NativeFillBuffer : IDisposable
    {
        private int _endPos;
        private NativeArray<float> _buffer;
        public int Length => _endPos;

        public NativeFillBuffer(int length, Allocator allocator) : this()
        {
            _buffer = new NativeArray<float>(length, allocator);
            _endPos = 0;
        }


        public void ShiftBuffer(int offset)
        {
            // zzzz1234|oooo -> 1234|oooooooo
            for (int i = 0; i < _endPos - offset; i++)
            {
                _buffer[i] = _buffer[i + offset];
            }

            /*for (int i = _endPos - offset; i < _endPos; i++)
            {
                _buffer[i] = 0f;
            }*/
            _endPos -= offset;
        }

        /// <summary>
        /// Shift after Read!
        /// </summary>
        public void Read(ref NativeArray<float> to, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                to[i] = _buffer[i + offset];
            }
        }

        public void Write(in NativeArray<float> from, int length)
        {
            for (int i = 0; i < length; i++)
            {
                _buffer[i + _endPos] = from[i];
            }

            _endPos += length;
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();
        }
    }
}