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
        public static float CalculateAttenuationFactor(float distance, float attenuationCoeff)
        {
            const float soundPress = 1f;
            return soundPress * math.exp(-0.1151f * attenuationCoeff * distance);
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