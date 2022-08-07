using System;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace DSPGraph.Audio.DSP
{
    [BurstCompile(CompileSynchronously = true)]
    public struct Resampler
    {
        public double Position;
        private float _lastLeft;
        private float _lastRight;

        public bool ResampleLerpRead<T>(
            SampleProvider provider,
            NativeArray<float> input, // bufferLength * channelCount
            SampleBuffer outputBuffer,
            ParameterData<T> parameterData,
            T positionParam)
            where T : unmanaged, Enum
        {
            bool finishedSampleProvider = false;

            NativeArray<float> outputL = outputBuffer.GetBuffer(0);
            NativeArray<float> outputR = outputBuffer.GetBuffer(1);
            for (int i = 0; i < outputL.Length; i++)
            {
                Position += parameterData.GetFloat(positionParam, i);

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

        /// <summary>
        /// Read either mono or stereo, always convert to stereo interleaved
        /// fill pattern: 0000000000011111111111
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="destination">NativeSlice with length = provider.output.length*channelCount</param>
        /// <returns></returns>
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
                    // fill bu empty full buffer
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

        public static void ResampleTo(in NativeArray<float> inBuffer, ref NativeArray<float> outBuffer)
        {
            float resampleRate = (float)(inBuffer.Length-1) / (float)(outBuffer.Length-1);

            for (int i = 0; i < outBuffer.Length; i++)
            {
                float samplePositionIn = resampleRate * i;
                float positionFraction = math.abs(i - samplePositionIn);

                int inSampleIndexMin = (int)math.floor(samplePositionIn);
                int inSampleIndexMax = (int)math.ceil(samplePositionIn);

                if (inBuffer.Length <= inSampleIndexMin || inBuffer.Length <= inSampleIndexMax)
                {
                    return;
                }

                float sample = math.lerp(
                    inBuffer[inSampleIndexMin],
                    inBuffer[inSampleIndexMax],
                    positionFraction
                );

                outBuffer[i] = sample;
            }
        }
    }
}