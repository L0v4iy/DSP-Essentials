using System;
using Unity.Burst;
using Unity.Mathematics;

namespace DSPGraph.Audio.DSP.Utils
{
    public enum SurroundedSubstance
    {
        Air
    }

    [BurstCompile]
    public static class SubstanceUtil
    {
        private static readonly double Io = Math.Pow(10d, -12d);

        public static float CalculateSoundLevel(float distance)
        {
            return (float)CalculateSoundLevel(10d, 0d, distance);
        }

        private static double CalculateSoundLevel(double R1, double B1, double R2)
        {
            double B2 = B1 + 20 * Math.Log(R1 / R2, 10);
            return B2;
        }

        private static double CalculateSoundLevelByIntencity(double I)
        {
            double B = 10d * Math.Log(I / Io, 10d);
            return B;
        }

        public static float GetSubstanceSoundAbsorptionCoefficient(SurroundedSubstance substance)
        {
            return substance switch
            {
                SurroundedSubstance.Air => 3.58f,
                _ => throw new ArgumentOutOfRangeException(nameof(substance), substance, null)
            };
        }
    }
}