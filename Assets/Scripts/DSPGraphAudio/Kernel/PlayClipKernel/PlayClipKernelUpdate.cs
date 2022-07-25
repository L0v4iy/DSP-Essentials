using Unity.Audio;
using Unity.Burst;

namespace DSPGraphAudio.Kernel.PlayClipKernel
{
    // From the DSPGraph 0.1.0-preview.11 samples, with small modifications.
    [BurstCompile(CompileSynchronously = true)]
    internal struct PlayClipKernelUpdate : IAudioKernelUpdate<PlayClipKernel.Parameters, PlayClipKernel.SampleProviders,
        PlayClipKernel>
    {
        // This update job is used to kick off playback of the node.
        public void Update(ref PlayClipKernel audioKernel)
        {
            audioKernel.Playing = true;
        }
        
    }
}