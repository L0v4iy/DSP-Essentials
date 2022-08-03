using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DSPGraph.Audio.DSP.Filters
{
    // Use like so:
    // lowPassNode = StateVariableFilter.CreateNode(block, StateVariableFilter.FilterType.Lowpass, 2);
    // block.Connect(node, 0, lowPassNode, 0);
    // block.Connect(lowPassNode, 0, graph.RootDSP, 0);
    //
    // Set parameters like so:
    // block.
    //     SetFloat<Filter.AudioKernel.Parameters, Filter.AudioKernel.Providers,
    //         Filter.AudioKernel>(
    //                                          filterNode,
    //                                          Filter.AudioKernel.Parameters.Cutoff,
    //                                          500f // Cutoff Hz
    //                                         );
    //
    // Lowpass
    // Cutoff: 10.0f - 22000.0f
    // Q: 1f - 100f
    // GainInDBs: -80f - 0f
    //

    public struct EqualizerFilterDSP
    {
        public enum Parameters
        {
            [ParameterDefault((float)FilterDesigner.Type.Lowpass)]
            [ParameterRange(
                (float)FilterDesigner.Type.Lowpass,
                (float)FilterDesigner.Type.Highshelf
            )]
            FilterType,

            /// <summary>
            /// cuts other frequency (higher than value)
            /// </summary>
            [ParameterDefault(5000.0f)] [ParameterRange(10.0f, 22050.0f)]
            Cutoff,

            /// <summary>
            /// Crystallize (whistling sound)
            /// </summary>
            [ParameterDefault(1.0f)] [ParameterRange(1.0f, 100.0f)]
            Q,

            [ParameterDefault(0.0f)] [ParameterRange(-80.0f, 0.0f)]
            GainInDBs
        }


        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            public struct Channel
            {
                public float z1, z2;
            }

            [NativeDisableContainerSafetyRestriction]
            private NativeArray<Channel> _channels;


            public void Initialize()
            {
                _channels = new NativeArray<Channel>(2, Allocator.AudioKernel);
            }

            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                SampleBuffer input = context.Inputs.GetSampleBuffer(0);
                SampleBuffer output = context.Outputs.GetSampleBuffer(0);
                int channelCount = output.Channels;
                int sampleFrames = output.Samples;

                // fill buffer by 0
                if (_channels.Length == 0)
                {
                    for (int channel = 0; channel < channelCount; ++channel)
                    {
                        NativeArray<float> outputBuffer = output.GetBuffer(channel);
                        for (int n = 0; n < outputBuffer.Length; n++)
                            outputBuffer[n] = 0.0f;
                    }

                    return;
                }

                ParameterData<Parameters> parameters = context.Parameters;
                FilterDesigner.Type filterType = (FilterDesigner.Type)parameters.GetFloat(Parameters.FilterType, 0);
                float cutoff = parameters.GetFloat(Parameters.Cutoff, 0);
                float q = parameters.GetFloat(Parameters.Q, 0);
                float gain = parameters.GetFloat(Parameters.GainInDBs, 0);
                FilterDesigner.Coefficients coefficients =
                    FilterDesigner.Design(filterType, cutoff, q, gain, context.SampleRate);

                for (int c = 0; c < _channels.Length; c++)
                {
                    NativeArray<float> inputBuffer = input.GetBuffer(c);
                    NativeArray<float> outputBuffer = output.GetBuffer(c);

                    float z1 = _channels[c].z1;
                    float z2 = _channels[c].z2;

                    for (int i = 0; i < sampleFrames; ++i)
                    {
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

        public static DSPNode CreateNode(DSPCommandBlock block, FilterDesigner.Type type, int channels)
        {
            DSPNode node = block.CreateDSPNode<Parameters, SampleProviders, AudioKernel>();
            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);
            block.SetFloat<Parameters, SampleProviders, AudioKernel>(
                node,
                Parameters.FilterType,
                (float)type
            );
            return node;
        }
    }
}