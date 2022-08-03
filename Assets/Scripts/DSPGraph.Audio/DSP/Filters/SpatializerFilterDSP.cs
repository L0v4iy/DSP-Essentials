using System;
using System.Diagnostics;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DSPGraph.Audio.DSP.Filters
{
    public struct SpatializerFilterDSP
    {
        private const int MaxDelay = 1024 * 8;

        public enum Parameters
        {
            // in samples
            RightChannelOffset,
            LeftChannelOffset
        }

        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            [NativeDisableContainerSafetyRestriction]
            private NativeFillBuffer _delayBufferL;

            [NativeDisableContainerSafetyRestriction]
            private NativeFillBuffer _delayBufferR;

            private Delayer _delayer;

            public void Initialize()
            {
                _delayBufferL = new NativeFillBuffer(MaxDelay, Allocator.AudioKernel);
                _delayBufferR = new NativeFillBuffer(MaxDelay, Allocator.AudioKernel);

                _delayer = new Delayer();
            }


            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                SampleBuffer inputBuffer = context.Inputs.GetSampleBuffer(0);
                SampleBuffer outputBuffer = context.Outputs.GetSampleBuffer(0);

                int delayInSamplesL = math.min(
                    (int)context.Parameters.GetFloat(Parameters.LeftChannelOffset, 0),
                    MaxDelay
                );
                int delayInSamplesR = math.min(
                    (int)context.Parameters.GetFloat(Parameters.RightChannelOffset, 0),
                    MaxDelay
                );

                _delayer.DelayInSamplesL = delayInSamplesL;
                _delayer.DelayInSamplesR = delayInSamplesR;

                _delayer.Delay(
                    inputBuffer,
                    outputBuffer,
                    ref _delayBufferL,
                    ref _delayBufferR
                );
            }

            public void Dispose()
            {
                _delayBufferL.Dispose();
                _delayBufferR.Dispose();
            }
        }

        public struct KernelUpdate : IAudioKernelUpdate<Parameters, SampleProviders, AudioKernel>
        {
            public void Update(ref AudioKernel audioKernel)
            {
            }
        }

        public static DSPNode CreateNode(DSPCommandBlock block, int channels)
        {
            DSPNode node = block
                .CreateDSPNode<Parameters, SampleProviders, AudioKernel>();

            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);

            return node;
        }

        internal struct Delayer
        {
            internal int DelayInSamplesL;
            internal int DelayInSamplesR;

            // Delay left or right channel a number of samples.
            public void Delay(
                SampleBuffer input,
                SampleBuffer output,
                ref NativeFillBuffer delayBufferL,
                ref NativeFillBuffer delayBufferR
            )
            {
                NativeArray<float> inputL = input.GetBuffer(0);
                NativeArray<float> inputR = input.GetBuffer(1);
                NativeArray<float> outputL = output.GetBuffer(0);
                NativeArray<float> outputR = output.GetBuffer(1);

                if (inputL.Length != inputR.Length)
                    throw new ApplicationException("Input buffers are different size.");

                // push to delay buffers
                //TODO:2022-08-03 14:09:40  required optimization
                delayBufferL.Write(inputL, inputL.Length);
                delayBufferR.Write(inputR, inputR.Length);

                int bufferSizeL = delayBufferL.Length;
                int bufferSizeR = delayBufferR.Length;
                int readSamplesFromBufferL = bufferSizeL - DelayInSamplesL < 0 ? 0 : bufferSizeL - DelayInSamplesL;
                int readSamplesFromBufferR = bufferSizeR - DelayInSamplesR < 0 ? 0 : bufferSizeR - DelayInSamplesR;
                
                NativeArray<float> delayedSampleArrayL = new NativeArray<float>(readSamplesFromBufferL, Allocator.AudioKernel);
                NativeArray<float> delayedSampleArrayR = new NativeArray<float>(readSamplesFromBufferR, Allocator.AudioKernel);
                delayBufferL.Read(ref delayedSampleArrayL, 0, readSamplesFromBufferL);
                delayBufferR.Read(ref delayedSampleArrayR, 0, readSamplesFromBufferR);
                // recalculate samples to output buffer
                InfillBuffer(in delayedSampleArrayL, ref outputL);
                InfillBuffer(in delayedSampleArrayR, ref outputR);
                
                // service
                delayBufferL.ShiftBuffer(readSamplesFromBufferL);
                delayBufferR.ShiftBuffer(readSamplesFromBufferR);
                delayedSampleArrayL.Dispose();
                delayedSampleArrayR.Dispose();
            }

            /// <summary>
            /// Resize buffer data
            /// </summary>
            private void InfillBuffer(in NativeArray<float> from, ref NativeArray<float> to)
            {
                if (to.Length > from.Length)
                {
                    // 1234 | nnnnnnnn -> 00001234
                    int fromToDif = to.Length - from.Length;
                    for (int i = fromToDif; i < to.Length; i++)
                    {
                        to[i] = from[i-fromToDif];
                    }
                    for (int i = 0; i < fromToDif; i++)
                    {
                        to[i] = 0f;
                    }
                }
                else if (to.Length < from.Length)
                {
                    // 1234567890123456 | nnnnnnnn -> 24685246
                    //TODO:2022-08-02 19:37:27  resample
                    // now cut off
                    for (int i = 0; i < to.Length; i++)
                    {
                        // now cutted
                        to[i] = from[i];
                    }
                }
                else
                {
                    for (int i = 0; i < to.Length; i++)
                    {
                        // now cutted
                        to[i] = from[i];
                    }
                }
            }
        }
    }
}