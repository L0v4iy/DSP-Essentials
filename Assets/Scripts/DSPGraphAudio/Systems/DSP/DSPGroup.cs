using DSPGraphAudio.Kernel.Systems;
using Unity.Entities;

namespace DSPGraphAudio.Systems.DSP
{
    [UpdateBefore(typeof(AudioSystem))]
    public class DSPGroup : ComponentSystemGroup
    {
        
    }
}