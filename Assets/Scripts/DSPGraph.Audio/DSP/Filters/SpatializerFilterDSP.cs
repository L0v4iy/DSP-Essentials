using System;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace DSPGraph.Audio.DSP.Filters
{
    public struct SpatializerFilterDSP
    {
        private const int MaxDelay = 1024 * 8;

        public enum Parameters
        {
            // in samples
            ChannelOffsetL,
            AttenuationL,
            TransverseL,
            SagittalL,
            CoronalL,

            ChannelOffsetR,
            AttenuationR,
            TransverseR,
            SagittalR,
            CoronalR
        }

        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            private Delayer _delayer;

            public void Initialize()
            {
                _delayer = new Delayer(MaxDelay);
            }


            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                SampleBuffer inputBuffer = context.Inputs.GetSampleBuffer(0);
                SampleBuffer outputBuffer = context.Outputs.GetSampleBuffer(0);

                NativeArray<float> inputL = inputBuffer.GetBuffer(0);
                NativeArray<float> inputR = inputBuffer.GetBuffer(1);
                NativeArray<float> outputL = outputBuffer.GetBuffer(0);
                NativeArray<float> outputR = outputBuffer.GetBuffer(1);


                int delayInSamplesL = math.min(
                    (int)context.Parameters.GetFloat(Parameters.ChannelOffsetL, 0),
                    MaxDelay - inputL.Length
                );
                int delayInSamplesR = math.min(
                    (int)context.Parameters.GetFloat(Parameters.ChannelOffsetR, 0),
                    MaxDelay - inputR.Length
                );

                float attenuationL = context.Parameters.GetFloat(Parameters.AttenuationL, 0);
                float transverseL = context.Parameters.GetFloat(Parameters.TransverseL, 0);
                float sagittalL = context.Parameters.GetFloat(Parameters.SagittalL, 0);
                float coronalL = context.Parameters.GetFloat(Parameters.CoronalL, 0);

                float attenuationR = context.Parameters.GetFloat(Parameters.AttenuationR, 0);
                float transverseR = context.Parameters.GetFloat(Parameters.TransverseR, 0);
                float sagittalR = context.Parameters.GetFloat(Parameters.SagittalR, 0);
                float coronalR = context.Parameters.GetFloat(Parameters.CoronalR, 0);


                _delayer.Delay(
                    delayInSamplesL,
                    delayInSamplesR,
                    inputL,
                    inputR,
                    out NativeArray<float> intermediateBufferL,
                    out NativeArray<float> intermediateBufferR
                );


                // set Transverse to samples


                // set Sagittal to samples


                // set Coronal to samples


                // recalculate samples to output buffer
                InfillBuffer(in intermediateBufferL, ref outputL);
                InfillBuffer(in intermediateBufferR, ref outputR);
                // service
                intermediateBufferL.Dispose();
                intermediateBufferR.Dispose();
            }

            public void Dispose()
            {
                _delayer.Dispose();
            }

            #region Utils

            /// <summary>
            /// Resize buffer data
            /// </summary>
            private static void InfillBuffer(in NativeArray<float> from, ref NativeArray<float> to)
            {
                if (to.Length > from.Length)
                {
                    // 1234 | nnnnnnnn -> 00001234
                    int fromToDif = to.Length - from.Length;
                    for (int i = fromToDif; i < to.Length; i++)
                    {
                        to[i] = from[i - fromToDif];
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

            #endregion
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

        #region SubComponents

        private struct Delayer : IDisposable
        {
            [NativeDisableContainerSafetyRestriction]
            private NativeFillBuffer _delayBufferL;

            [NativeDisableContainerSafetyRestriction]
            private NativeFillBuffer _delayBufferR;

            public Delayer(int maxDelay)
            {
                _delayBufferL = new NativeFillBuffer(maxDelay, Allocator.AudioKernel);
                _delayBufferR = new NativeFillBuffer(maxDelay, Allocator.AudioKernel);
            }

            // Delay left or right channel a number of samples.
            public void Delay(
                int delayInSamplesL,
                int delayInSamplesR,
                in NativeArray<float> inputL,
                in NativeArray<float> inputR,
                out NativeArray<float> spatChannelL,
                out NativeArray<float> spatChannelR
            )
            {
                if (inputL.Length != inputR.Length)
                    throw new ApplicationException("Input buffers are different size.");

                // push to delay buffers
                //TODO:2022-08-03 14:09:40  required optimization
                _delayBufferL.Write(inputL, inputL.Length);
                _delayBufferR.Write(inputR, inputR.Length);

                int bufferSizeL = _delayBufferL.Length;
                int bufferSizeR = _delayBufferR.Length;
                int readSamplesFromBufferL = bufferSizeL - delayInSamplesL < 0 ? 0 : bufferSizeL - delayInSamplesL;
                int readSamplesFromBufferR = bufferSizeR - delayInSamplesR < 0 ? 0 : bufferSizeR - delayInSamplesR;

                spatChannelL = new NativeArray<float>(readSamplesFromBufferL, Allocator.AudioKernel);
                spatChannelR = new NativeArray<float>(readSamplesFromBufferR, Allocator.AudioKernel);

                _delayBufferL.Read(ref spatChannelL, 0, readSamplesFromBufferL);
                _delayBufferR.Read(ref spatChannelR, 0, readSamplesFromBufferR);

                _delayBufferL.ShiftBuffer(readSamplesFromBufferL);
                _delayBufferR.ShiftBuffer(readSamplesFromBufferR);
            }

            public void Dispose()
            {
                _delayBufferL.Dispose();
                _delayBufferR.Dispose();
            }
        }

        #endregion
    }
}