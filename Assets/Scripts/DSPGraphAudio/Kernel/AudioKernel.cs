using DSPGraphAudio.DSP;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DSPGraphAudio.Kernel
{
    [BurstCompile(CompileSynchronously = true)]
    public struct AudioKernel : IAudioKernel<AudioKernel.Parameters, AudioKernel.SampleProviders>
    {
        public enum Parameters
        {
            [ParameterDefault((float)Filter.Type.Lowpass)]
            [ParameterRange(
                (float)Filter.Type.Lowpass,
                (float)Filter.Type.Highshelf
            )]
            FilterType,

            [ParameterDefault(5000.0f)] [ParameterRange(10.0f, 22000.0f)]
            Cutoff,

            [ParameterDefault(1.0f)] [ParameterRange(1.0f, 100.0f)]
            Q,

            [ParameterDefault(0.0f)] [ParameterRange(-80.0f, 0.0f)]
            GainInDBs,
            
            Rate
  
        }

        public enum SampleProviders
        {
            DefaultSlot
        }

        public Resampler Resampler;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> ResampleBuffer;

        public bool Playing;

        public void Initialize()
        {
            ResampleBuffer = new NativeArray<float>(1025 * 2, Allocator.AudioKernel);
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
                    context.PostEvent(new ClipStoppedEvent(context, ClipStopReason.ClipEnd));
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