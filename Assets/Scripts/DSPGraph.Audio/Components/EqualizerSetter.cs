using Unity.Entities;
using UnityEngine;

namespace DSPGraph.Audio.Components
{
    [GenerateAuthoringComponent]
    public struct EqualizerSetter : IComponentData
    {
        [Range(10f, 22000f)]
        public float Cutoff;
        [Range(1f, 100f)]
        public float Q;
        [Range(-80f, 0f)]
        public float GainInDBs;
        
    }
}