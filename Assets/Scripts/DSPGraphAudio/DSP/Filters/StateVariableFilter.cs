﻿using System;
using System.Diagnostics;
using System.Reflection;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace DSPGraphAudio.DSP
{
    [Obsolete("Duplicated Filter")]
    public struct StateVariableFilter
    {
        public enum FilterType
        {
            Lowpass,
            Highpass,
            Bandpass,
            Bell,
            Notch,
            Lowshelf,
            Highshelf
        }

        public static DSPNode Create(DSPCommandBlock block, FilterType type)
        {
            DSPNode node = block.CreateDSPNode<AudioKernel.Parameters, AudioKernel.Providers, AudioKernel>();
            block.AddInletPort(node, 2);
            block.AddOutletPort(node, 2);
            block.SetFloat<AudioKernel.Parameters, AudioKernel.Providers, AudioKernel>(node,
                AudioKernel.Parameters.FilterType, (float)type);

            return node;
        }

        private struct Coefficients
        {
            public float A, g, k, a1, a2, a3, m0, m1, m2;
        }

        private static Coefficients DesignBell(float fc, float quality, float linearGain)
        {
            float A = linearGain;
            float g = Mathf.Tan(Mathf.PI * fc);
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
            float g = Mathf.Tan(Mathf.PI * normalizedFrequency);
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
            Coefficients coefficients = Design(FilterType.Lowpass, normalizedFrequency, Q, linearGain);
            coefficients.m1 = 1;
            coefficients.m2 = 0;
            return coefficients;
        }

        private static Coefficients DesignHighpass(float normalizedFrequency, float Q, float linearGain)
        {
            Coefficients coefficients = Design(FilterType.Lowpass, normalizedFrequency, Q, linearGain);
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
            float g = Mathf.Tan(Mathf.PI * normalizedFrequency) / Mathf.Sqrt(A);
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
            float g = Mathf.Tan(Mathf.PI * normalizedFrequency) / Mathf.Sqrt(A);
            float k = 1 / Q;
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            float m0 = A * A;
            float m1 = k * (1 - A) * A;
            float m2 = 1 - A * A;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }

        private static Coefficients Design(FilterType type, float normalizedFrequency, float Q, float linearGain)
        {
            switch (type)
            {
                case FilterType.Lowpass: return DesignLowpass(normalizedFrequency, Q, linearGain);
                case FilterType.Highpass: return DesignHighpass(normalizedFrequency, Q, linearGain);
                case FilterType.Bandpass: return DesignBandpass(normalizedFrequency, Q, linearGain);
                case FilterType.Bell: return DesignBell(normalizedFrequency, Q, linearGain);
                case FilterType.Notch: return DesignNotch(normalizedFrequency, Q, linearGain);
                case FilterType.Lowshelf: return DesignLowshelf(normalizedFrequency, Q, linearGain);
                case FilterType.Highshelf: return DesignHighshelf(normalizedFrequency, Q, linearGain);
                default:
                    ThrowUnknownFilterTypeError(type);
                    return default;
            }
        }

        private static void ThrowUnknownFilterTypeError(FilterType type)
        {
            ThrowUnknownFilterTypeErrorMono(type);
            ThrowUnknownFilterTypeErrorBurst(type);
        }

        [BurstDiscard]
        private static void ThrowUnknownFilterTypeErrorMono(FilterType type)
        {
            throw new ArgumentException("Unknown filter type", nameof(type));
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void ThrowUnknownFilterTypeErrorBurst(FilterType type)
        {
            throw new ArgumentException("Unknown filter type", nameof(type));
        }

        private static Coefficients Design(FilterType filterType, float cutoff, float Q, float gainInDBs,
            float sampleRate)
        {
            float linearGain = Mathf.Pow(10, gainInDBs / 20);
            switch (filterType)
            {
                case FilterType.Lowpass:
                    return DesignLowpass(cutoff / sampleRate, Q, linearGain);
                case FilterType.Highpass:
                    return DesignHighpass(cutoff / sampleRate, Q, linearGain);
                case FilterType.Bandpass:
                    return DesignBandpass(cutoff / sampleRate, Q, linearGain);
                case FilterType.Bell:
                    return DesignBell(cutoff / sampleRate, Q, linearGain);
                case FilterType.Notch:
                    return DesignNotch(cutoff / sampleRate, Q, linearGain);
                case FilterType.Lowshelf:
                    return DesignLowshelf(cutoff / sampleRate, Q, linearGain);
                case FilterType.Highshelf:
                    return DesignHighshelf(cutoff / sampleRate, Q, linearGain);
                default:
                    ThrowUnknownFilterTypeError(filterType);
                    return default;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<AudioKernel.Parameters, AudioKernel.Providers>
        {
            public struct Channel
            {
                public float z1, z2;
            }

            [NativeDisableContainerSafetyRestriction]
            public NativeArray<Channel> Channels;

            public enum Parameters
            {
                [ParameterDefault((float)StateVariableFilter.FilterType.Lowpass)]
                [ParameterRange((float)StateVariableFilter.FilterType.Lowpass,
                    (float)StateVariableFilter.FilterType.Highshelf)]
                FilterType,

                [ParameterDefault(5000.0f)] [ParameterRange(10.0f, 22000.0f)]
                Cutoff,

                [ParameterDefault(1.0f)] [ParameterRange(1.0f, 100.0f)]
                Q,

                [ParameterDefault(0.0f)] [ParameterRange(-80.0f, 0.0f)]
                GainInDBs
            }

            public enum Providers
            {
            }

            public void Initialize()
            {
                Channels = new NativeArray<Channel>(2, Allocator.AudioKernel);
            }

            public void Execute(ref ExecuteContext<Parameters, Providers> context)
            {
                SampleBuffer input = context.Inputs.GetSampleBuffer(0);
                SampleBuffer output = context.Outputs.GetSampleBuffer(0);
                int channelCount = output.Channels;
                int sampleFrames = output.Samples;

                if (Channels.Length == 0)
                {
                    for (int channel = 0; channel < channelCount; ++channel)
                    {
                        NativeArray<float> outputBuffer = output.GetBuffer(channel);
                        for (int n = 0; n < outputBuffer.Length; n++)
                            outputBuffer[n] = 0.0f;
                    }

                    return;
                }

                ParameterData<Parameters> parameters = context.Parameters;
                FilterType filterType = (FilterType)parameters.GetFloat(Parameters.FilterType, 0);
                float cutoff = parameters.GetFloat(Parameters.Cutoff, 0);
                float q = parameters.GetFloat(Parameters.Q, 0);
                float gain = parameters.GetFloat(Parameters.GainInDBs, 0);
                Coefficients coefficients = Design(filterType, cutoff, q, gain, context.SampleRate);

                for (int c = 0; c < Channels.Length; c++)
                {
                    NativeArray<float> inputBuffer = input.GetBuffer(c);
                    NativeArray<float> outputBuffer = output.GetBuffer(c);

                    float z1 = Channels[c].z1;
                    float z2 = Channels[c].z2;

                    for (int i = 0; i < sampleFrames; ++i)
                    {
                        float x = inputBuffer[i];
                        float v3 = x - z2;
                        float v1 = coefficients.a1 * z1 + coefficients.a2 * v3;
                        float v2 = z2 + coefficients.a2 * z1 + coefficients.a3 * v3;
                        z1 = 2 * v1 - z1;
                        z2 = 2 * v2 - z2;
                        outputBuffer[i] = coefficients.A *
                                          (coefficients.m0 * x + coefficients.m1 * v1 + coefficients.m2 * v2);
                    }

                    Channels[c] = new Channel { z1 = z1, z2 = z2 };
                }
            }

            public void Dispose()
            {
                if (Channels.IsCreated)
                    Channels.Dispose();
            }
        }
    }
}