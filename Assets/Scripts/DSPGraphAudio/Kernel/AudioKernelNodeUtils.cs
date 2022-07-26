﻿using DSPGraphAudio.DSP;
using Unity.Audio;

namespace DSPGraphAudio.Kernel
{
    public static class AudioKernelNodeUtils
    {
        public static DSPNode CreatePrimaryNode(DSPCommandBlock block, int channels)
        {
            DSPNode node = block.CreateDSPNode<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>();
            block.AddOutletPort(node, channels);

            return node;
        }

        public static DSPNode CreateTypeNode(DSPCommandBlock block, Filter.Type type, int channels)
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

        // Create a spatializer node.
        //
        // Setting parameters:
        // block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
        //                                                                                                    node,
        //                                                                                                    SpatializerKernel.
        //                                                                                                        Parameters.
        //                                                                                                        Channel,
        //                                                                                                    0
        //                                                                                                   );
        // block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
        //                                                                                                    node,
        //                                                                                                    SpatializerKernel.
        //                                                                                                        Parameters.
        //                                                                                                        Samples,
        //                                                                                                    500
        //                                                                                                   );
        public static DSPNode CreateSpatializerNode(DSPCommandBlock block, int channels)
        {
            DSPNode node = block
                .CreateDSPNode<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>();

            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);

            return node;
        }

        // Create lowpass filter node.
        //
        // Setting parameters:
        // block.
        //     SetFloat<Filter.AudioKernel.Parameters, Filter.AudioKernel.Providers,
        //         Filter.AudioKernel>(
        //                         lowpassFilterNode,
        //                         Filter.AudioKernel.Parameters.Cutoff,
        //                         cutoffHz
        //                        );
        public static DSPNode CreateLowpassFilterNode(DSPCommandBlock block, float cutoffHz, int channels)
        {
            DSPNode node = CreateTypeNode(block, Filter.Type.Lowpass, channels);
            block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders,
                AudioKernel>(
                node,
                AudioKernel.Parameters.Cutoff,
                cutoffHz
            );
            return node;
        }
    }
}