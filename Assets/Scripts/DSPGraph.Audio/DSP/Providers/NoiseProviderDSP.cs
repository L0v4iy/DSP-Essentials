using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Random = Unity.Mathematics.Random;

namespace DSPGraph.Audio.DSP.Providers
{
    public struct NoiseProviderDSP
    {
        public enum Parameters
        {
            [ParameterDefault(0.0f)] [ParameterRange(-1.0f, 1.0f)]
            Offset
        }

        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            private Random _random;
            
            public void Initialize()
            {
            }

            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                if (context.Outputs.Count == 0)
                    return;

                if (_random.state == 0)
                    _random.InitState(2747636419u);

                SampleBuffer outputSampleBuffer = context.Outputs.GetSampleBuffer(0);
                int outputChannels = outputSampleBuffer.Channels;
                ParameterData<Parameters> parameters = context.Parameters;
                int inputCount = context.Inputs.Count;

                for (int channel = 0; channel < outputChannels; ++channel)
                {
                    NativeArray<float> outputBuffer = outputSampleBuffer.GetBuffer(channel);
                    for (int i = 0; i < inputCount; i++)
                    {
                        NativeArray<float> inputBuff = context.Inputs.GetSampleBuffer(i).GetBuffer(channel);
                        for (int s = 0; s < outputBuffer.Length; s++)
                            outputBuffer[s] += inputBuff[s];
                    }

                    for (int s = 0; s < outputBuffer.Length; s++)
                        outputBuffer[s] += _random.NextFloat() * 2.0f - 1.0f + parameters.GetFloat(Parameters.Offset, s);
                }
            }

            public void Dispose()
            {
            }
        }

        public struct KernelUpdate : IAudioKernelUpdate<Parameters, SampleProviders, AudioKernel>
        {
            public void Update(ref AudioKernel audioKernel)
            {
                
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