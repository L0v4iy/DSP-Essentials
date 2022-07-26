using System;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace DSPGraphAudio.DSP.Filters
{
    // Use like so:
    // lowPassNode = StateVariableFilter.CreateNode(block, StateVariableFilter.FilterType.Lowpass, 2);
    // block.Connect(node, 0, lowPassNode, 0);
    // block.Connect(lowPassNode, 0, graph.RootDSP, 0);
    //
    // Set parameters like so:
    // block.
    //     SetFloat<Filter.AudioKernel.Parameters, Filter.AudioKernel.Providers,
    //         Filter.AudioKernel>(
    //                                          filterNode,
    //                                          Filter.AudioKernel.Parameters.Cutoff,
    //                                          500f // Cutoff Hz
    //                                         );
    //
    // Lowpass
    // Cutoff: 10.0f - 22000.0f
    // Q: 1f - 100f
    // GainInDBs: -80f - 0f
    //
    
    public struct EqualizerFilterDSP
    {
        public enum Type
        {
            Lowpass,
            Highpass,
            Bandpass,
            Bell,
            Notch,
            Lowshelf,
            Highshelf
        }

        public enum Parameters
        {
            [ParameterDefault((float)Type.Lowpass)]
            [ParameterRange(
                (float)Type.Lowpass,
                (float)Type.Highshelf
            )]
            FilterType,

            [ParameterDefault(5000.0f)] [ParameterRange(10.0f, 22000.0f)]
            Cutoff,

            [ParameterDefault(1.0f)] [ParameterRange(1.0f, 100.0f)]
            Q,

            [ParameterDefault(0.0f)] [ParameterRange(-80.0f, 0.0f)]
            GainInDBs
        }


        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            public struct Channel
            {
                public float z1, z2;
            }

            [NativeDisableContainerSafetyRestriction]
            private NativeArray<Channel> _channels;


            public void Initialize()
            {
                _channels = new NativeArray<Channel>(2, Allocator.AudioKernel);
            }

            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                SampleBuffer input = context.Inputs.GetSampleBuffer(0);
                SampleBuffer output = context.Outputs.GetSampleBuffer(0);
                int sampleFrames = output.Samples;

                ParameterData<Parameters> parameters = context.Parameters;
                Type filterType = (Type)parameters.GetFloat(Parameters.FilterType, 0);
                float cutoff = parameters.GetFloat(Parameters.Cutoff, 0);
                float q = parameters.GetFloat(Parameters.Q, 0);
                float gain = parameters.GetFloat(Parameters.GainInDBs, 0);
                FilterDesigner.Coefficients coefficients = FilterDesigner.Design(filterType, cutoff, q, gain, context.SampleRate);

                for (int c = 0; c < _channels.Length; c++)
                {
                    float z1 = _channels[c].z1;
                    float z2 = _channels[c].z2;

                    for (int i = 0; i < sampleFrames; i++)
                    {
                        NativeArray<float> inputBuffer = input.GetBuffer(c);
                        NativeArray<float> outputBuffer = output.GetBuffer(c);

                        float x = inputBuffer[i];

                        float v3 = x - z2;
                        float v1 = coefficients.a1 * z1 + coefficients.a2 * v3;
                        float v2 = z2 + coefficients.a2 * z1 + coefficients.a3 * v3;
                        z1 = 2 * v1 - z1;
                        z2 = 2 * v2 - z2;
                        outputBuffer[i] = coefficients.A *
                                          (coefficients.m0 * x + coefficients.m1 * v1 + coefficients.m2 * v2);
                    }

                    _channels[c] = new Channel { z1 = z1, z2 = z2 };
                }
            }

            public void Dispose()
            {
                if (_channels.IsCreated)
                    _channels.Dispose();
            }
        }

        public static DSPNode CreateNode(DSPCommandBlock block, Type type, int channels)
        {
            DSPNode node = block.CreateDSPNode<Parameters, SampleProviders, AudioKernel>();
            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);
            block.SetFloat<Parameters, SampleProviders, AudioKernel>(
                node,
                Parameters.FilterType,
                (float)type
            );
            return node;
        }
        
    }

    internal static class FilterDesigner
    {
        internal struct Coefficients
        {
            public float A, g, k, a1, a2, a3, m0, m1, m2;
        }

        internal static Coefficients Design(EqualizerFilterDSP.Type type, float normalizedFrequency, float Q,
            float linearGain)
        {
            switch (type)
            {
                case EqualizerFilterDSP.Type.Lowpass: return DesignLowpass(normalizedFrequency, Q, linearGain);
                case EqualizerFilterDSP.Type.Highpass: return DesignHighpass(normalizedFrequency, Q, linearGain);
                case EqualizerFilterDSP.Type.Bandpass: return DesignBandpass(normalizedFrequency, Q, linearGain);
                case EqualizerFilterDSP.Type.Bell: return DesignBell(normalizedFrequency, Q, linearGain);
                case EqualizerFilterDSP.Type.Notch: return DesignNotch(normalizedFrequency, Q, linearGain);
                case EqualizerFilterDSP.Type.Lowshelf: return DesignLowshelf(normalizedFrequency, Q, linearGain);
                case EqualizerFilterDSP.Type.Highshelf: return DesignHighshelf(normalizedFrequency, Q, linearGain);
                default:
                    throw new ArgumentException("Unknown filter type", nameof(type));
            }
        }

        internal static Coefficients Design(EqualizerFilterDSP.Type type, float cutoff, float Q, float gainInDBs,
            float sampleRate)
        {
            float linearGain = Mathf.Pow(10, gainInDBs / 20);
            switch (type)
            {
                case EqualizerFilterDSP.Type.Lowpass:
                    return DesignLowpass(cutoff / sampleRate, Q, linearGain);
                case EqualizerFilterDSP.Type.Highpass:
                    return DesignHighpass(cutoff / sampleRate, Q, linearGain);
                case EqualizerFilterDSP.Type.Bandpass:
                    return DesignBandpass(cutoff / sampleRate, Q, linearGain);
                case EqualizerFilterDSP.Type.Bell:
                    return DesignBell(cutoff / sampleRate, Q, linearGain);
                case EqualizerFilterDSP.Type.Notch:
                    return DesignNotch(cutoff / sampleRate, Q, linearGain);
                case EqualizerFilterDSP.Type.Lowshelf:
                    return DesignLowshelf(cutoff / sampleRate, Q, linearGain);
                case EqualizerFilterDSP.Type.Highshelf:
                    return DesignHighshelf(cutoff / sampleRate, Q, linearGain);
                default:
                    throw new ArgumentException("Unknown filter type", nameof(type));
            }
        }

        private static Coefficients DesignBell(float fc, float quality, float linearGain)
        {
            float A = linearGain;
            float g = math.tan(math.PI * fc);
            float k = 1 / (quality * A);
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            int m0 = 1;
            float m1 = k * (A * A - 1);
            int m2 = 0;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }

        private static Coefficients DesignLowpass(float normalizedFrequency, float Q, float linearGain)
        {
            float A = linearGain;
            float g = math.tan(math.PI * normalizedFrequency);
            float k = 1 / Q;
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            int m0 = 0;
            int m1 = 0;
            int m2 = 1;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }

        private static Coefficients DesignBandpass(float normalizedFrequency, float Q, float linearGain)
        {
            Coefficients coefficients = Design(EqualizerFilterDSP.Type.Lowpass, normalizedFrequency, Q, linearGain);
            coefficients.m1 = 1;
            coefficients.m2 = 0;
            return coefficients;
        }

        private static Coefficients DesignHighpass(float normalizedFrequency, float Q, float linearGain)
        {
            Coefficients coefficients = Design(EqualizerFilterDSP.Type.Lowpass, normalizedFrequency, Q, linearGain);
            coefficients.m0 = 1;
            coefficients.m1 = -coefficients.k;
            coefficients.m2 = -1;
            return coefficients;
        }

        private static Coefficients DesignNotch(float normalizedFrequency, float Q, float linearGain)
        {
            Coefficients coefficients = DesignLowpass(normalizedFrequency, Q, linearGain);
            coefficients.m0 = 1;
            coefficients.m1 = -coefficients.k;
            coefficients.m2 = 0;
            return coefficients;
        }

        private static Coefficients DesignLowshelf(float normalizedFrequency, float Q, float linearGain)
        {
            float A = linearGain;
            float g = math.tan(math.PI * normalizedFrequency) / math.sqrt(A);
            float k = 1 / Q;
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            int m0 = 1;
            float m1 = k * (A - 1);
            float m2 = A * A - 1;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }

        private static Coefficients DesignHighshelf(float normalizedFrequency, float Q, float linearGain)
        {
            float A = linearGain;
            float g = math.tan(math.PI * normalizedFrequency) / math.sqrt(A);
            float k = 1 / Q;
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            float m0 = A * A;
            float m1 = k * (1 - A) * A;
            float m2 = 1 - A * A;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }
    }
}