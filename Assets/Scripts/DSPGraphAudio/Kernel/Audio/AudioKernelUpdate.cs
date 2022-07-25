using Unity.Audio;
using Unity.Burst;

namespace DSPGraphAudio.Kernel.Audio
{
    [BurstCompile(CompileSynchronously = true)]
    internal struct AudioKernelUpdate :
        IAudioKernelUpdate<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
    {
        /// <summary>
        /// Call 1000/fps ms.
        /// </summary>
        public void Update(ref AudioKernel audioKernel)
        {
            // recalculate listener position job
        }
    }
}