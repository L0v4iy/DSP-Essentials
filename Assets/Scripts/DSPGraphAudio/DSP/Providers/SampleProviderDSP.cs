using DSPGraphAudio.Kernel;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace DSPGraphAudio.DSP.Providers
{

    public class SampleProviderDSP
    {
        public enum Parameters
        {
            SamplePosition
        }

        public enum SampleProviders
        {
            DefaultOutput
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            private Resampler _resampler;

            [NativeDisableContainerSafetyRestriction]
            private NativeArray<float> _resampleBuffer;

            public bool Playing;
            
            public void Initialize()
            {
                _resampleBuffer = new NativeArray<float>(1025 * 2, Allocator.AudioKernel);
                _resampler.Position = (double)_resampleBuffer.Length / 2;
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
                    SampleProvider provider = context.Providers.GetSampleProvider(SampleProviders.DefaultOutput);

                    // We pass the provider to the resampler. If the resampler finishes streaming all the samples, it returns
                    // true.
                    bool finished = _resampler.ResampleLerpRead(
                        provider,
                        _resampleBuffer,
                        buffer,
                        context.Parameters,
                        Parameters.SamplePosition
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
                if (_resampleBuffer.IsCreated)
                    _resampleBuffer.Dispose();
            }
        }

        public struct KernelUpdate : IAudioKernelUpdate<Parameters, SampleProviders, AudioKernel>
        {
            public void Update(ref AudioKernel audioKernel)
            {
                // recalculate listener position job
                audioKernel.Playing = true;
                Debug.Log("AudioKernelUpdate");
            }
        }

        public static DSPNode CreateNode(DSPCommandBlock block, int channels)
        {
            DSPNode node = block.CreateDSPNode<Parameters, SampleProviders, AudioKernel>();
            block.AddOutletPort(node, channels);

            return node;
        }
    }
}