using System;
using DSPGraph.Audio.DSP.Utils;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace DSPGraph.Audio.DSP.Filters
{
    public struct SpatializerFilterDSP
    {
        private const int MaxDelay = 1024 * 8;

        public enum Parameters
        {
            // in samples
            ReceiverDistanceL,
            ChannelOffsetL,
            TransverseL,
            SagittalL,
            CoronalL,

            ReceiverDistanceR,
            ChannelOffsetR,
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
            private ResamplerSingle _resamplerL;
            private ResamplerSingle _resamplerR;
            private NativeArray<float> _resampleBuffer;

            private Delayer _delayer;
            private Distorer _distorerL;
            private Distorer _distorerR;

            public void Initialize()
            {
                _delayer = new Delayer(MaxDelay);
                _distorerL = Distorer.CreateDistorer();
                _distorerR = Distorer.CreateDistorer();

                int ChannelSampleSize = 1024;
                _resampleBuffer = new NativeArray<float>(ChannelSampleSize * 2, Allocator.AudioKernel);
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

                float transverseL = context.Parameters.GetFloat(Parameters.TransverseL, 0);
                float distanceL = context.Parameters.GetFloat(Parameters.ReceiverDistanceL, 0);
                float sagittalL = context.Parameters.GetFloat(Parameters.SagittalL, 0);
                float coronalL = context.Parameters.GetFloat(Parameters.CoronalL, 0);

                float transverseR = context.Parameters.GetFloat(Parameters.TransverseR, 0);
                float distanceR = context.Parameters.GetFloat(Parameters.ReceiverDistanceR, 0);
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
                _distorerL.Distort(ref intermediateBufferL, distanceL, transverseL, sagittalL, coronalL);
                _distorerR.Distort(ref intermediateBufferR, distanceR, transverseR, sagittalR, coronalR);

                // recalculate samples to output buffer

                _resamplerL.ResampleTo(in intermediateBufferL, ref outputL);
                _resamplerR.ResampleTo(in intermediateBufferR, ref outputR);

                /*InfillBuffer(in intermediateBufferL, ref outputL);
                InfillBuffer(in intermediateBufferR, ref outputR);*/

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
                float distanceToReceiver,
                float transverseFactor,
                float sagittalFactor,
                float coronalFactor
            )
            {
                // 0 to 1
                float transverseFactorLerp = (transverseFactor + 1f) / 2f;
                float sagittalFactorLerp = (sagittalFactor + 1f) / 2f;
                float coronalFactorLerp = (coronalFactor + 1f) / 2f;

                // 50 - 1/2 output Hz
                const float absFreq = 22000.0f;
                // 1-100f
                const float absQ = 1.0f;

                // db/m
                float soundLevel = SubstanceUtil.CalculateSoundLevel(distanceToReceiver);
                float linearGain = math.pow(10, soundLevel / 20);

                float distanceFreq = math.floor(distanceToReceiver * 10);
                float transverseFreq = math.floor(math.lerp(absFreq / 128, absFreq, transverseFactorLerp));
                float sagittalFreq = absFreq;
                float coronalFreq = math.floor(math.lerp(2000f, absFreq, coronalFactorLerp));

                float distanceQ = math.floor(math.lerp(10, 1, coronalFactorLerp));
                float transverseQ = math.lerp(absQ * 64f, absQ, transverseFactorLerp);
                float sagittalQ = absQ;
                float coronalQ = math.lerp(absQ, absQ * 32f, coronalFactorLerp);

                float distanceGain = linearGain;
                float transverseGain = linearGain;
                float sagittalGain = math.lerp(linearGain / 1.41f, linearGain, sagittalFactorLerp);
                float coronalGain = math.lerp(linearGain / 1.04f, linearGain, coronalFactorLerp);


                FilterDesigner.Coefficients distanceCoeff =
                    FilterDesigner.Design(FilterDesigner.Type.Lowpass, distanceFreq, distanceQ, distanceGain);

                // down/up
                FilterDesigner.Coefficients transverseCoeff = LerpCoefficients
                (
                    FilterDesigner.Design(FilterDesigner.Type.Lowshelf, transverseFreq, transverseQ, transverseGain),
                    FilterDesigner.Design(FilterDesigner.Type.Highshelf, transverseFreq, transverseQ, transverseGain),
                    transverseFactorLerp
                );

                // left/right
                FilterDesigner.Coefficients sagittalCoeff = LerpCoefficients
                (
                    FilterDesigner.Design(FilterDesigner.Type.Lowpass, sagittalFreq, sagittalQ, sagittalGain),
                    FilterDesigner.Design(FilterDesigner.Type.Lowpass, sagittalFreq, sagittalQ, sagittalGain),
                    sagittalFactorLerp
                );

                // back/front
                FilterDesigner.Coefficients coronalCoeff =
                    LerpCoefficients(
                        FilterDesigner.Design(FilterDesigner.Type.Lowpass, coronalFreq, coronalQ, coronalGain),
                        FilterDesigner.Design(FilterDesigner.Type.Notch, coronalFreq, coronalQ, coronalGain),
                        coronalFactorLerp
                    );


                FilterDesigner.Coefficients midCoefficient = new FilterDesigner.Coefficients()
                {
                    A = Mid(distanceCoeff.A, transverseCoeff.A, sagittalCoeff.A, coronalCoeff.A),
                    g = Mid(distanceCoeff.g, transverseCoeff.g, sagittalCoeff.g, coronalCoeff.g), // not in use
                    k = Mid(distanceCoeff.k, transverseCoeff.k, sagittalCoeff.k, coronalCoeff.k), // not in use
                    a1 = Mid(distanceCoeff.a1, transverseCoeff.a1, sagittalCoeff.a1, coronalCoeff.a1),
                    a2 = Mid(distanceCoeff.a2, transverseCoeff.a2, sagittalCoeff.a2, coronalCoeff.a2),
                    a3 = Mid(distanceCoeff.a3, transverseCoeff.a3, sagittalCoeff.a3, coronalCoeff.a3),
                    m0 = Mid(distanceCoeff.m0, transverseCoeff.m0, sagittalCoeff.m0, coronalCoeff.m0),
                    m1 = Mid(distanceCoeff.m1, transverseCoeff.m1, sagittalCoeff.m1, coronalCoeff.m1),
                    m2 = Mid(distanceCoeff.m2, transverseCoeff.m2, sagittalCoeff.m2, coronalCoeff.m2)
                };

                ProcessFilter(
                    midCoefficient,
                    ref sampleBuffer
                );
            }

            private void ProcessFilter(
                FilterDesigner.Coefficients coefficients,
                ref NativeArray<float> sampleBuffer
            )
            {
                //0f-1f
                float gain = coefficients.A;

                for (int i = 0; i < sampleBuffer.Length; ++i)
                {
                    // default value
                    float x = sampleBuffer[i];
                    float v3 = x - z2;
                    float v1 = coefficients.a1 * z1 + coefficients.a2 * v3;
                    float v2 = z2 + coefficients.a2 * z1 + coefficients.a3 * v3;
                    z1 = 2 * v1 - z1;
                    z2 = 2 * v2 - z2;
                    sampleBuffer[i] = gain *
                                      (coefficients.m0 * x + coefficients.m1 * v1 + coefficients.m2 * v2);
                }
            }

            private static float Mid(float val0, float val1, float val2, float val3)
            {
                return (val0 + val1 + val2 + val3) / 4;
            }

            private static FilterDesigner.Coefficients LerpCoefficients(
                FilterDesigner.Coefficients a,
                FilterDesigner.Coefficients b,
                float lerpVal
            )
            {
                return new FilterDesigner.Coefficients()
                {
                    A = math.lerp(a.A, b.A, lerpVal),
                    g = math.lerp(a.g, b.g, lerpVal),
                    k = math.lerp(a.k, b.k, lerpVal),
                    a1 = math.lerp(a.a1, b.a1, lerpVal),
                    a2 = math.lerp(a.a2, b.a2, lerpVal),
                    a3 = math.lerp(a.a3, b.a3, lerpVal),
                    m0 = math.lerp(a.m0, b.m0, lerpVal),
                    m1 = math.lerp(a.m1, b.m1, lerpVal),
                    m2 = math.lerp(a.m2, b.m2, lerpVal),
                };
            }
        }

        #endregion
    }
}