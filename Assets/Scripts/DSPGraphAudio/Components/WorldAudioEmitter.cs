using Unity.Audio;
using Unity.Entities;

namespace DSPGraphAudio.Components
{
    /// <summary>
    /// Node collection
    /// </summary>
    [GenerateAuthoringComponent]
    public struct WorldAudioEmitter : IComponentData
    {
        // pipeline works as fields here
        public DSPNode SampleProviderNode;
        public DSPConnection EmitterConnection;
        public DSPNode SpatializerNode;
        public DSPNode EqualizerFilterNode;

        public bool Valid;
    }
}