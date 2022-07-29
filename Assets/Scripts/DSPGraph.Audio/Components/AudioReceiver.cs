using Unity.Entities;

namespace DSPGraph.Audio.Components
{
    [GenerateAuthoringComponent]
    public struct AudioReceiver : IComponentData
    {
        public Entity LeftReceiver;
        public Entity RightReceiver;
    }
}