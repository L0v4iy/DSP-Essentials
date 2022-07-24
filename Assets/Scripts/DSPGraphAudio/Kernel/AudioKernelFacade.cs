using DSPGraphAudio.DSP;
using Unity.Audio;

namespace DSPGraphAudio.Kernel
{
    public struct AudioKernelFacade
    {
        public static DSPNode CreateNode(DSPCommandBlock block, Filter.Type type, int channels)
        {
            DSPNode node = block.CreateDSPNode<AudioKernel.Parameters, AudioKernel.Providers, AudioKernel>();
            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);
            block.SetFloat<AudioKernel.Parameters, AudioKernel.Providers, AudioKernel>(
                node,
                AudioKernel.Parameters.FilterType,
                (float)type
            );
            return node;
        }
    }
}