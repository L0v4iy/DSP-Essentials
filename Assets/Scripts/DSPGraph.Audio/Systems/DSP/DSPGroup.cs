using Unity.Entities;

namespace DSPGraph.Audio.Systems.DSP
{
    [UpdateBefore(typeof(AudioSystem))]
    public class DSPGroup : ComponentSystemGroup
    {
        
    }
}