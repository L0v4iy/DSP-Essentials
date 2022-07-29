using Unity.Audio;
using Unity.Burst;

namespace DSPGraph.Audio.DSP
{
    internal class ExampleDSP
    {
        public enum Parameters
        {
        }

        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            public void Initialize()
            {
                throw new System.NotImplementedException();
            }

            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                throw new System.NotImplementedException();
            }

            public void Dispose()
            {
                throw new System.NotImplementedException();
            }
        }

        public struct KernelUpdate : IAudioKernelUpdate<Parameters, SampleProviders, AudioKernel>
        {
            public void Update(ref AudioKernel audioKernel)
            {
                throw new System.NotImplementedException();
            }
        }

        public static DSPNode CreateNode(DSPCommandBlock block, int channels)
        {
            throw new System.NotImplementedException();
        }
    }
}