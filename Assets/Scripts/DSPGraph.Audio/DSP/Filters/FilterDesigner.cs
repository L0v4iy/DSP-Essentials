using System;
using UnityEngine;

namespace DSPGraph.Audio.DSP.Filters
{
    public static class FilterDesigner
    {
        public enum Type
        {
            Lowpass,
            Highpass,
            Bandpass,//boom boom
            Bell,    //zin zin
            Notch,   //default?
            Lowshelf,
            Highshelf
        }

        internal struct Coefficients
        {
            public float
                A, // linear gain
                g, // normalize frequency 
                k, // quality (tone)
                a1, // 
                a2, // 
                a3, // 
                m0, // 
                m1, // 
                m2; // 
        }

        internal static Coefficients Design(Type type, float normalizedFrequency, float quality,
            float linearGain)
        {
            switch (type)
            {
                case Type.Lowpass: return DesignLowpass(normalizedFrequency, quality, linearGain);
                case Type.Highpass: return DesignHighpass(normalizedFrequency, quality, linearGain);
                case Type.Bandpass: return DesignBandpass(normalizedFrequency, quality, linearGain);
                case Type.Bell: return DesignBell(normalizedFrequency, quality, linearGain);
                case Type.Notch: return DesignNotch(normalizedFrequency, quality, linearGain);
                case Type.Lowshelf: return DesignLowshelf(normalizedFrequency, quality, linearGain);
                case Type.Highshelf: return DesignHighshelf(normalizedFrequency, quality, linearGain);
                default:
                    throw new ArgumentException("Unknown filter type", nameof(type));
            }
        }

        internal static Coefficients Design(Type type, float cutoff, float quality, float gainInDBs,
            float sampleRate)
        {
            float linearGain = Mathf.Pow(10, gainInDBs / 20);
            switch (type)
            {
                case Type.Lowpass:
                    return DesignLowpass(cutoff / sampleRate, quality, linearGain);
                case Type.Highpass:
                    return DesignHighpass(cutoff / sampleRate, quality, linearGain);
                case Type.Bandpass:
                    return DesignBandpass(cutoff / sampleRate, quality, linearGain);
                case Type.Bell:
                    return DesignBell(cutoff / sampleRate, quality, linearGain);
                case Type.Notch:
                    return DesignNotch(cutoff / sampleRate, quality, linearGain);
                case Type.Lowshelf:
                    return DesignLowshelf(cutoff / sampleRate, quality, linearGain);
                case Type.Highshelf:
                    return DesignHighshelf(cutoff / sampleRate, quality, linearGain);
                default:
                    throw new ArgumentException("Unknown filter type", nameof(type));
            }
        }

        private static Coefficients DesignBell(float normalizedFrequency, float quality, float linearGain)
        {
            float A = linearGain;
            float g = Mathf.Tan(Mathf.PI * normalizedFrequency);
            float k = 1 / (quality * A);
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            int m0 = 1;
            float m1 = k * (A * A - 1);
            int m2 = 0;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }

        private static Coefficients DesignLowpass(float normalizedFrequency, float quality, float linearGain)
        {
            float A = linearGain;
            float g = Mathf.Tan(Mathf.PI * normalizedFrequency);
            float k = 1 / quality;
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            int m0 = 0;
            int m1 = 0;
            int m2 = 1;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }

        private static Coefficients DesignBandpass(float normalizedFrequency, float quality, float linearGain)
        {
            Coefficients coefficients = Design(Type.Lowpass, normalizedFrequency, quality, linearGain);
            coefficients.m1 = 1;
            coefficients.m2 = 0;
            return coefficients;
        }

        private static Coefficients DesignHighpass(float normalizedFrequency, float quality, float linearGain)
        {
            Coefficients coefficients = Design(Type.Lowpass, normalizedFrequency, quality, linearGain);
            coefficients.m0 = 1;
            coefficients.m1 = -coefficients.k;
            coefficients.m2 = -1;
            return coefficients;
        }

        private static Coefficients DesignNotch(float normalizedFrequency, float quality, float linearGain)
        {
            Coefficients coefficients = DesignLowpass(normalizedFrequency, quality, linearGain);
            coefficients.m0 = 1;
            coefficients.m1 = -coefficients.k;
            coefficients.m2 = 0;
            return coefficients;
        }

        private static Coefficients DesignLowshelf(float normalizedFrequency, float quality, float linearGain)
        {
            float A = linearGain;
            float g = Mathf.Tan(Mathf.PI * normalizedFrequency) / Mathf.Sqrt(A);
            float k = 1 / quality;
            float a1 = 1 / (1 + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;
            int m0 = 1;
            float m1 = k * (A - 1);
            float m2 = A * A - 1;
            return new Coefficients { A = A, g = g, k = k, a1 = a1, a2 = a2, a3 = a3, m0 = m0, m1 = m1, m2 = m2 };
        }

        private static Coefficients DesignHighshelf(float normalizedFrequency, float quality, float linearGain)
        {
            float A = linearGain;
            float g = Mathf.Tan(Mathf.PI * normalizedFrequency) / Mathf.Sqrt(A);
            float k = 1 / quality;
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