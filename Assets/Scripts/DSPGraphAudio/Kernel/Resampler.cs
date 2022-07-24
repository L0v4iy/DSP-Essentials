using System;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DSPGraphAudio.Kernel
{
    [BurstCompile(CompileSynchronously = true)]
    public struct Resampler
    {
        public double Position;
        private float _lastLeft;
        private float _lastRight;

        public bool ResampleLerpRead<T>(
            SampleProvider provider,
            NativeArray<float> input,
            SampleBuffer outputBuffer,
            ParameterData<T> parameterData,
            T rateParam)
            where T : unmanaged, Enum
        {
            bool finishedSampleProvider = false;

            NativeArray<float> outputL = outputBuffer.GetBuffer(0);
            NativeArray<float> outputR = outputBuffer.GetBuffer(1);
            for (int i = 0; i < outputL.Length; i++)
            {
                Position += parameterData.GetFloat(rateParam, i);

                int length = input.Length / 2;

                while (Position >= length - 1)
                {
                    _lastLeft = input[length - 1];
                    _lastRight = input[input.Length - 1];

                    finishedSampleProvider |= ReadSamples(provider, new NativeSlice<float>(input, 0));

                    Position -= length;
                }

                double positionFloor = Math.Floor(Position);
                double positionFraction = Position - positionFloor;
                int previousSampleIndex = (int)positionFloor;
                int nextSampleIndex = previousSampleIndex + 1;

                float prevSampleL = previousSampleIndex < 0 ? _lastLeft : input[previousSampleIndex];
                float prevSampleR = previousSampleIndex < 0 ? _lastRight : input[previousSampleIndex + length];
                float sampleL = input[nextSampleIndex];
                float sampleR = input[nextSampleIndex + length];

                outputL[i] = (float)(prevSampleL + (sampleL - prevSampleL) * positionFraction);
                outputR[i] = (float)(prevSampleR + (sampleR - prevSampleR) * positionFraction);
            }

            return finishedSampleProvider;
        }

        // read either mono or stereo, always convert to stereo interleaved
        private static unsafe bool ReadSamples(SampleProvider provider, NativeSlice<float> destination)
        {
            if (!provider.Valid)
                return true;

            bool finished = false;

            // Read from SampleProvider and convert to stereo if needed
            int destinationFrames = destination.Length / 2;
            if (provider.ChannelCount == 2)
            {
                int read = provider.Read(destination.Slice(0, destination.Length));
                if (read < destinationFrames)
                {
                    for (int i = read; i < destinationFrames; i++)
                    {
                        destination[i] = 0;
                        destination[i + destinationFrames] = 0;
                    }

                    return true;
                }
            }
            else
            {
                NativeSlice<float> buffer = destination.Slice(0, destinationFrames);
                int read = provider.Read(buffer);

                if (read < destinationFrames)
                {
                    for (int i = read; i < destinationFrames; i++)
                        destination[i] = 0;

                    finished = true;
                }

                float* left = (float*)destination.GetUnsafePtr();
                float* right = left + read;
                UnsafeUtility.MemCpy(right, left, read * UnsafeUtility.SizeOf<float>());
            }

            return finished;
        }
    }
}