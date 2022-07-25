using DSPGraphAudio.DSP;
using Unity.Audio;

namespace DSPGraphAudio.Kernel.AudioKernel
{
    public struct AudioKernelFacade
    {
        public static DSPNode CreateNode(DSPCommandBlock block, Filter.Type type, int channels)
        {
            DSPNode node = block.CreateDSPNode<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>();
            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);
            block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(
                node,
                AudioKernel.Parameters.FilterType,
                (float)type
            );
            return node;
        }
    }
}