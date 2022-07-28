using Unity.Entities;

namespace DSPGraphAudio.Components
{
    [GenerateAuthoringComponent]
    public struct AudioReceiver : IComponentData
    {
        public Entity LeftReceiver;
        public Entity RightReceiver;
    }
}