using Unity.Audio;
using Unity.Entities;

namespace DSPGraph.Audio.Components
{
    /// <summary>
    /// Node collection
    /// </summary>
    [GenerateAuthoringComponent]
    public struct WorldAudioEmitter : IComponentData
    {
        // pipeline works as fields here
        public DSPNode SampleProviderNode;
        public DSPNode SpatializerNode;


        public ChannelData LeftChannelData;
        public ChannelData RightChannelData;
        
        // -1: left is left | 0: mono | 1: left is right
        public float ChannelInvertRate;


        public bool Valid;
    }

    public struct ChannelData
    {
        public float SampleDelay;
        public float Attenuation;


        // Factors 
        // range value: -1 to 1
        /// <summary>
        /// Down to Up
        /// </summary>
        public float TransverseFactor;

        /// <summary>
        /// Against side receiver to In side receiver
        /// Left | Right
        /// </summary>
        public float SagittalFactor;

        /// <summary>
        /// Back to front
        /// </summary>
        public float CoronalFactor;
    }
}