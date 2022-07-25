using DSPGraphAudio.Kernel.Systems;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DSPGraphAudio.Kernel.PlayClipKernel
{
    // From the DSPGraph 0.1.0-preview.11 samples, with small modifications.
    //
    // The 'audio job'. This is the kernel that defines a running DSP node inside the
    // DSPGraph. It is a struct that implements the IAudioKernel interface. It can contain
    // internal state, and will have the Execute function called as part of the graph
    // traversal during an audio frame.
    //
    [BurstCompile(CompileSynchronously = true)]
    internal struct PlayClipKernel : IAudioKernel<PlayClipKernel.Parameters, PlayClipKernel.SampleProviders>
    {
        // Parameters are currently defined with enumerations. Each enum value corresponds to
        // a parameter within the node. Setting a value for a parameter uses these enum values.
        public enum Parameters
        {
            Rate
        }

        // Sample providers are defined with enumerations. Each enum value defines a slot where
        // a sample provider can live on a IAudioKernel. Sample providers are used to get samples from
        // AudioClips and VideoPlayers. They will eventually be able to pull samples from microphones and other concepts.
        public enum SampleProviders
        {
            DefaultSlot
        }

        // The clip sample rate might be different to the output rate used by the system. Therefore we use a resampler
        // here.
        public Resampler Resampler;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> ResampleBuffer;

        // Updated by the PlayClipKernelUpdate.
        public bool Playing;

        public void Initialize()
        {
            // During an initialization phase, we have access to a resource context which we can
            // do buffer allocations with safely in the job.
            ResampleBuffer = new NativeArray<float>(1025 * 2, Allocator.AudioKernel);

            // set position to "end of buffer", to force pulling data on first iteration
            Resampler.Position = (double)ResampleBuffer.Length / 2;
        }

        public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
        {
            if (Playing)
            {
                // During the creation phase of this node we added an output port to feed samples to.
                // This API gives access to that output buffer.
                SampleBuffer buffer = context.Outputs.GetSampleBuffer(0);

                // Get the sample provider for the AudioClip currently being played. This allows
                // streaming of samples from the clip into a buffer.
                SampleProvider provider = context.Providers.GetSampleProvider(SampleProviders.DefaultSlot);

                // We pass the provider to the resampler. If the resampler finishes streaming all the samples, it returns
                // true.
                bool finished = Resampler.ResampleLerpRead(
                    provider,
                    ResampleBuffer,
                    buffer,
                    context.Parameters,
                    Parameters.Rate
                );

                if (finished)
                {
                    // Post an async event back to the main thread, telling the handler that the clip has stopped playing.
                    context.PostEvent(new AudioSystem.ClipStopped());
                    Playing = false;
                }
            }
        }

        public void Dispose()
        {
            if (ResampleBuffer.IsCreated)
                ResampleBuffer.Dispose();
        }
    }
}