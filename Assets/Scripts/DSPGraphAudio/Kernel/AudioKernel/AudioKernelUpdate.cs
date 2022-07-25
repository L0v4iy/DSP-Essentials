using Unity.Audio;
using Unity.Burst;

namespace DSPGraphAudio.Kernel.AudioKernel
{
    [BurstCompile(CompileSynchronously = true)]
    internal struct AudioKernelUpdate :
        IAudioKernelUpdate<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
    {
        public void Update(ref AudioKernel audioKernel)
        {
        }
    }
}