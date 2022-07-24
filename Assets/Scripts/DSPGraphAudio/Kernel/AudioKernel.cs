using DSPGraphAudio.DSP;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DSPGraphAudio.Kernel
{
    [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<AudioKernel.Parameters, AudioKernel.Providers>
        {
            private struct Channel
            {
                public float z1, z2;
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
                GainInDBs
            }

            public enum Providers
            {
            }

            public void Initialize()
            {
                _channels = new NativeArray<Channel>(2, Allocator.AudioKernel);
            }

            public void Execute(ref ExecuteContext<Parameters, Providers> context)
            {
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
                    float z1 = _channels[c].z1;
                    float z2 = _channels[c].z2;

                    for (int i = 0; i < sampleFrames; i++)
                    {
                        NativeArray<float> inputBuffer = input.GetBuffer(c);
                        NativeArray<float> outputBuffer = output.GetBuffer(c);

                        float x = inputBuffer[i];

                        float v3 = x - z2;
                        float v1 = coefficients.a1 * z1 + coefficients.a2 * v3;
                        float v2 = z2 + coefficients.a2 * z1 + coefficients.a3 * v3;
                        z1 = 2 * v1 - z1;
                        z2 = 2 * v2 - z2;
                        outputBuffer[i] = coefficients.A *
                                          (coefficients.m0 * x + coefficients.m1 * v1 + coefficients.m2 * v2);
                    }

                    _channels[c] = new Channel { z1 = z1, z2 = z2 };
                }
            }

            public void Dispose()
            {
                if (_channels.IsCreated)
                    _channels.Dispose();
            }
        }
}