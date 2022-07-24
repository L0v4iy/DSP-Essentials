using System;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

// From the DSPGraph 0.1.0-preview.11 samples, with modifications.
//
namespace DSPGraphAudio.DSP
{
    public struct Filter
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


        #region Designs

        internal struct Coefficients
        {
            public float A, g, k, a1, a2, a3, m0, m1, m2;
        }

        internal static Coefficients Design(Type type, float normalizedFrequency, float Q, float linearGain)
        {
            switch (type)
            {
                case Type.Lowpass: return DesignLowpass(normalizedFrequency, Q, linearGain);
                case Type.Highpass: return DesignHighpass(normalizedFrequency, Q, linearGain);
                case Type.Bandpass: return DesignBandpass(normalizedFrequency, Q, linearGain);
                case Type.Bell: return DesignBell(normalizedFrequency, Q, linearGain);
                case Type.Notch: return DesignNotch(normalizedFrequency, Q, linearGain);
                case Type.Lowshelf: return DesignLowshelf(normalizedFrequency, Q, linearGain);
                case Type.Highshelf: return DesignHighshelf(normalizedFrequency, Q, linearGain);
                default:
                    throw new ArgumentException("Unknown filter type", nameof(type));
            }
        }

        internal static Coefficients Design(Type type, float cutoff, float Q, float gainInDBs, float sampleRate)
        {
            float linearGain = Mathf.Pow(10, gainInDBs / 20);
            switch (type)
            {
                case Type.Lowpass:
                    return DesignLowpass(cutoff / sampleRate, Q, linearGain);
                case Type.Highpass:
                    return DesignHighpass(cutoff / sampleRate, Q, linearGain);
                case Type.Bandpass:
                    return DesignBandpass(cutoff / sampleRate, Q, linearGain);
                case Type.Bell:
                    return DesignBell(cutoff / sampleRate, Q, linearGain);
                case Type.Notch:
                    return DesignNotch(cutoff / sampleRate, Q, linearGain);
                case Type.Lowshelf:
                    return DesignLowshelf(cutoff / sampleRate, Q, linearGain);
                case Type.Highshelf:
                    return DesignHighshelf(cutoff / sampleRate, Q, linearGain);
                default:
                    throw new ArgumentException("Unknown filter type", nameof(type));
            }
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
            Coefficients coefficients = Design(Type.Lowpass, normalizedFrequency, Q, linearGain);
            coefficients.m1 = 1;
            coefficients.m2 = 0;
            return coefficients;
        }

        private static Coefficients DesignHighpass(float normalizedFrequency, float Q, float linearGain)
        {
            Coefficients coefficients = Design(Type.Lowpass, normalizedFrequency, Q, linearGain);
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

        #endregion


        
    }
}