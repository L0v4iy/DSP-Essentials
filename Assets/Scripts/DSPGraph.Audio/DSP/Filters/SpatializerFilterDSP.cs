using System;
using System.ComponentModel;
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
            private Distorer _distorerL;
            private Distorer _distorerR;

            public void Initialize()
            {
                _delayer = new Delayer(MaxDelay);
                _distorerL = Distorer.CreateDistorer();
                _distorerR = Distorer.CreateDistorer();
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


                // set Transverse, Sagittal, Coronal to samples
                _distorerL.Distort(ref intermediateBufferL, attenuationL, transverseL, sagittalL, coronalL);
                _distorerR.Distort(ref intermediateBufferR, attenuationR, transverseR, sagittalR, coronalR);


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

        private struct Distorer
        {
            
            public static Distorer CreateDistorer()
            {
                return new Distorer()
                {
                    z1 = 0,
                    z2 = 0
                };
            }

            private float z1;
            private float z2;

            public void Distort
            (
                ref NativeArray<float> sampleBuffer,
                float attenuation,
                float transverseFactor,
                float sagittalFactor,
                float coronalFactor
            )
            {
                // 50 - 1/2 output Hz
                float transverseNormalizedFreq = 22050f;
                float sagittalNormalizedFreq = 22050f;
                float coronalNormalizedFreq = 22050f;

                // 1-100f
                float transverseQ = 1f;
                float sagittalQ = 1f;
                float coronalQ = 1f;

                // 0 to -100;
                float gain = math.lerp(100f, 0f, 1f - attenuation);

                FilterDesigner.Coefficients transverseCoeff = transverseFactor < 0
                    ? FilterDesigner.Design(FilterDesigner.Type.Lowpass, transverseNormalizedFreq, transverseQ, gain)
                    : FilterDesigner.Design(FilterDesigner.Type.Highpass, transverseNormalizedFreq, transverseQ, gain);

                FilterDesigner.Coefficients sagittalCoeff = sagittalFactor < 0
                    ? FilterDesigner.Design(FilterDesigner.Type.Lowshelf, sagittalNormalizedFreq, sagittalQ, gain)
                    : FilterDesigner.Design(FilterDesigner.Type.Highshelf, sagittalNormalizedFreq, sagittalQ, gain);

                FilterDesigner.Coefficients coronalCoeff = coronalFactor < 0
                    ? FilterDesigner.Design(FilterDesigner.Type.Bandpass, coronalNormalizedFreq / 8 * coronalFactor, coronalQ, gain)
                    : FilterDesigner.Design(FilterDesigner.Type.Bandpass, coronalNormalizedFreq, coronalQ, gain);

                ProcessFilter(
                    transverseCoeff,
                    sagittalCoeff,
                    coronalCoeff,
                    ref sampleBuffer
                );
            }

            private void ProcessFilter(
                FilterDesigner.Coefficients tCoefficients,
                FilterDesigner.Coefficients sCoefficients,
                FilterDesigner.Coefficients cCoefficients,
                ref NativeArray<float> sampleBuffer
            )
            {
                /*for (int i = 0; i < sampleFrames; ++i)
                {
                    float x = inputBuffer[i];
                    float v3 = x - z2;
                    float v1 = coefficients.a1 * z1 + coefficients.a2 * v3;
                    float v2 = z2 + coefficients.a2 * z1 + coefficients.a3 * v3;
                    z1 = 2 * v1 - z1;
                    z2 = 2 * v2 - z2;
                    outputBuffer[i] = coefficients.A *
                                      (coefficients.m0 * x + coefficients.m1 * v1 + coefficients.m2 * v2);
                }*/
                for (int i = 0; i < sampleBuffer.Length; ++i)
                {
                    float x = sampleBuffer[i];
                    float v3 = x - z2;
                    float v1 = Mid(tCoefficients.a1, sCoefficients.a1, cCoefficients.a1)
                               * z1
                               + Mid(tCoefficients.a2, sCoefficients.a2, cCoefficients.a2) * v3;

                    float v2 = z2 + Mid(tCoefficients.a2, sCoefficients.a2, cCoefficients.a2)
                                  * z1
                                  + Mid(tCoefficients.a3, sCoefficients.a3, cCoefficients.a3) * v3;
                    z1 = 2 * v1 - z1;
                    z2 = 2 * v2 - z2;
                    /*sampleBuffer[i] = Mid(tCoefficients.A, sCoefficients.A, cCoefficients.A)
                                * (
                                    Mid(tCoefficients.m0, sCoefficients.m0, cCoefficients.m0) * x
                                    +
                                    Mid(tCoefficients.m1, sCoefficients.m1, cCoefficients.m1) * v1
                                    +
                                    Mid(tCoefficients.m2, sCoefficients.m2, cCoefficients.m2) * v2
                                );*/
                    sampleBuffer[i] = Mid(tCoefficients.A, sCoefficients.A, cCoefficients.A) * x;
                }
            }

            private static float Mid(float a, float b, float c)
            {
                return (a + b + c) / 3;
            }
        }

        #endregion
    }
}