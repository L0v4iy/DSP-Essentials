using Unity.Entities;

namespace DSPGraphAudio.Kernel.Systems
{
    [UpdateBefore(typeof(AudioSystem))]
    public partial class NodePositionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // get nodes
            // calculate vector to listener
            // apply stats
        }
    }
}