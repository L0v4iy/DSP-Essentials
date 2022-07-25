using DSPGraphAudio.DSP;
using DSPGraphAudio.Kernel.Systems;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DSPGraphAudio.Kernel
{
    [BurstCompile(CompileSynchronously = true)]
    public struct AudioKernel : IAudioKernel<AudioKernel.Parameters, AudioKernel.SampleProviders>
    {
        private struct Channel
        {
            public float Ch1, Ch2;
        }

        [NativeDisableContainerSafetyRestriction]
        private NativeArray<Channel> _channels;

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

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> ResampleBuffer;
        public Resampler Resampler;
        public bool Playing;

        public void Initialize()
        {
            ResampleBuffer = new NativeArray<float>(1024 * 2, Allocator.AudioKernel);
            Resampler.Position = (double)ResampleBuffer.Length / 2;
        }


        /*public void Initialize()
        {
            _channels = new NativeArray<Channel>(2, Allocator.AudioKernel);
        }*/

        /*public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
        {
            Debug.Log("executing");
            SampleBuffer input = context.Inputs.GetSampleBuffer(0);
            SampleBuffer output = context.Outputs.GetSampleBuffer(0);
            int sampleFrames = output.Samples;

            ParameterData<Parameters> parameters = context.Parameters;
            Filter.Type filterType = (Filter.Type)parameters.GetFloat(Parameters.FilterType, 0);
            float cutoff = parameters.GetFloat(Parameters.Cutoff, 0);
            float q = parameters.GetFloat(Parameters.Q, 0);
            float gain = parameters.GetFloat(Parameters.GainInDBs, 0);
            Filter.Coefficients coefficients = Filter.Design(filterType, cutoff, q, gain, context.SampleRate);

            for (int c = 0; c < _channels.Length; c++)
            {
                float ch1 = _channels[c].Ch1;
                float ch2 = _channels[c].Ch2;

                for (int i = 0; i < sampleFrames; i++)
                {
                    NativeArray<float> inputBuffer = input.GetBuffer(c);
                    NativeArray<float> outputBuffer = output.GetBuffer(c);

                    float x = inputBuffer[i];

                    float v3 = x - ch2;
                    float v1 = coefficients.a1 * ch1 + coefficients.a2 * v3;
                    float v2 = ch2 + coefficients.a2 * ch1 + coefficients.a3 * v3;
                    ch1 = 2 * v1 - ch1;
                    ch2 = 2 * v2 - ch2;
                    outputBuffer[i] = coefficients.A *
                                      (coefficients.m0 * x + coefficients.m1 * v1 + coefficients.m2 * v2);
                }

                _channels[c] = new Channel { Ch1 = ch1, Ch2 = ch2 };
            }
        }*/
        
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
                    context.PostEvent(new AudioSystem.ClipStoppedEvent());
                    Playing = false;
                }
            }
        }

        public void Dispose()
        {
            if (_channels.IsCreated)
                _channels.Dispose();
        }
    }
}