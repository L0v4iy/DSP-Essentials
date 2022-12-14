using System;
using DSPGraph.Audio.Components;
using DSPGraph.Audio.DSP.Filters;
using DSPGraph.Audio.DSP.Providers;
using Unity.Audio;
using Unity.Entities;
using UnityEngine;

namespace DSPGraph.Audio.Systems
{
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class AudioSystem
    {
        /// <summary>
        /// Play a one shot (relative to the listener).
        /// 1. Get free node.
        /// 2. Set up <param name="audioClip"></param> params.
        /// 3. Set up spatializer params.
        /// 4. Set connection attenuation.
        /// 5. Set lowpass filter.
        /// </summary>
        /// <param name="audioClip"></param>
        public void PlayClipInWorld(AudioClip audioClip)
        {
            Entity entity = World.EntityManager.CreateEntityQuery(typeof(WorldAudioEmitter))
                .GetSingletonEntity();


            using (DSPCommandBlock block = CreateCommandBlock())
            {
                WorldAudioEmitter emitter = new WorldAudioEmitter();
                
                emitter.SampleProviderNode = SampleProviderDSP.CreateNode(block, OutputChannelCount);
                emitter.SpatializerNode = SpatializerFilterDSP.CreateNode(block, OutputChannelCount);

                
                Connect(block, emitter.SampleProviderNode, emitter.SpatializerNode);
                emitter.Valid = true;
                
                Connect(block, emitter.SpatializerNode, _graph.RootDSP);


                // set source;
                float resampleRate = (float)audioClip.frequency / AudioSettings.outputSampleRate;
                block.SetFloat<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders,
                    SampleProviderDSP.AudioKernel>(emitter.SampleProviderNode,
                    SampleProviderDSP.Parameters.ResampleCoeff, resampleRate);

                // Assign the sample provider to the slot of the node.
                block.SetSampleProvider<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders,
                    SampleProviderDSP.AudioKernel>(
                    audioClip, emitter.SampleProviderNode, SampleProviderDSP.SampleProviders.DefaultOutput);

                // Kick off playback. This will be done in a better way in the future.
                block.UpdateAudioKernel<SampleProviderDSP.KernelUpdate, SampleProviderDSP.Parameters,
                    SampleProviderDSP.SampleProviders,
                    SampleProviderDSP.AudioKernel>(new SampleProviderDSP.KernelUpdate(), emitter.SampleProviderNode);

                EntityManager.SetComponentData(entity, emitter);

            }
        }

        public void PlayClipInHead(AudioClip audioClip)
        {
            if (audioClip == null)
                throw new ArgumentNullException(nameof(audioClip));

            using (DSPCommandBlock block = CreateCommandBlock())
            {
                DSPNode node = GetFreeNode(block, 2);
                // Decide on playback rate here by taking the provider input rate and the output settings of the system
                float resampleRate = (float)audioClip.frequency / AudioSettings.outputSampleRate;
                block.SetFloat<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders,
                    SampleProviderDSP.AudioKernel>(node,
                    SampleProviderDSP.Parameters.ResampleCoeff, resampleRate);

                // Assign the sample provider to the slot of the node.
                block.SetSampleProvider<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders,
                    SampleProviderDSP.AudioKernel>(
                    audioClip, node, SampleProviderDSP.SampleProviders.DefaultOutput);

                // Kick off playback. This will be done in a better way in the future.
                block.UpdateAudioKernel<SampleProviderDSP.KernelUpdate, SampleProviderDSP.Parameters,
                    SampleProviderDSP.SampleProviders,
                    SampleProviderDSP.AudioKernel>(new SampleProviderDSP.KernelUpdate(), node);
            }
        }
    }
}