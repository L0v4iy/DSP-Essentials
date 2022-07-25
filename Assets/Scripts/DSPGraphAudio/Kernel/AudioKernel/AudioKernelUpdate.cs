using Unity.Audio;
using Unity.Burst;

namespace DSPGraphAudio.Kernel
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