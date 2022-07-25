using Unity.Audio;
using Unity.Burst;
using UnityEngine;

namespace DSPGraphAudio.Kernel
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
            audioKernel.Playing = true;
            Debug.Log("AudioKernelUpdate");
        }
    }
}