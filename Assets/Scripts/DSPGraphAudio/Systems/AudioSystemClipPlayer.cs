using System;
using DSPGraphAudio.DSP;
using DSPGraphAudio.DSP.Filters;
using DSPGraphAudio.DSP.Providers;
using Unity.Audio;
using Unity.Mathematics;
using UnityEngine;

namespace DSPGraphAudio.Kernel.Systems
{
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
        /// <param name="relativeTranslation"></param>
        public void PlayClipInWorld(AudioClip audioClip, float3 relativeTranslation)
        {
            DSPCommandBlock block = _graph.CreateCommandBlock();

            DSPNode clipNode = GetFreeNode(block, _graph.OutputChannelCount);

            // Decide on playback rate here by taking the provider input rate and the output settings of the system
            float resampleRate = (float)audioClip.frequency / AudioSettings.outputSampleRate;
            block.SetFloat<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders,
                SampleProviderDSP.AudioKernel>(
                clipNode,
                SampleProviderDSP.Parameters.SamplePosition,
                resampleRate
            );

            // Assign the sample provider to the slot of the node.
            block.SetSampleProvider<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders,
                SampleProviderDSP.AudioKernel>(
                audioClip,
                clipNode,
                SampleProviderDSP.SampleProviders.DefaultOutput
            );

            // Set spatializer node parameters.
            _clipToSpatializerMap.TryGetValue(clipNode, out DSPNode spatializerNode);
            // Set delay channel based on relativeTranslation. Is it coming from left or right?
            SpatializerFilterDSP.Channels channel = relativeTranslation.x < 0
                ? SpatializerFilterDSP.Channels.Left
                : SpatializerFilterDSP.Channels.Right;
            // Set delay samples based on relativeTranslation. How much from the left/right is it coming?
            float distanceA = math.length(relativeTranslation + new float3(-MidToEarDistance, 0, 0));
            float distanceB = math.length(relativeTranslation + new float3(+MidToEarDistance, 0, 0));
            float diff = math.abs(distanceA - distanceB);
            int sampleRatePerChannel = _graph.SampleRate / _graph.OutputChannelCount;
            float samples = diff * sampleRatePerChannel / SpeedOfSoundMPerS;

            block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders, SpatializerFilterDSP.AudioKernel>
                (spatializerNode, SpatializerFilterDSP.Parameters.Channel, (float)channel);

            block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders, SpatializerFilterDSP.AudioKernel>
                (spatializerNode, SpatializerFilterDSP.Parameters.SampleOffset, samples);

            // Set attenuation based on distance.
            _clipToConnectionMap.TryGetValue(clipNode, out DSPConnection connection);
            float closestDistance = math.min(distanceA, distanceB);
            // Anything inside 10m has no attenuation.
            float closestInside10mCircle = math.max(closestDistance - 9, 1);
            float attenuation = math.clamp(1 / closestInside10mCircle, MinAttenuation, MaxAttenuation);
            block.SetAttenuation(connection, attenuation);

            // Set lowpass based on distance.
            _clipToLowpassMap.TryGetValue(clipNode, out DSPNode lowpassFilterNode);
            block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders,
                EqualizerFilterDSP.AudioKernel>
            (lowpassFilterNode,
                EqualizerFilterDSP.Parameters.Cutoff,
                math.clamp(1 / closestInside10mCircle * sampleRatePerChannel, 1000, sampleRatePerChannel)
            );
            // Kick off playback.
            block.UpdateAudioKernel<SampleProviderDSP.KernelUpdate, SampleProviderDSP.Parameters,
                SampleProviderDSP.SampleProviders,
                SampleProviderDSP.AudioKernel>(new SampleProviderDSP.KernelUpdate(), clipNode);

            block.Complete();
        }

        public void PlayClipInHead(AudioClip audioClip)
        {
            if (audioClip == null)
                throw new ArgumentNullException(nameof(audioClip));

            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                DSPNode node = GetFreeNode(block, 2);
                // Decide on playback rate here by taking the provider input rate and the output settings of the system
                float resampleRate = (float)audioClip.frequency / AudioSettings.outputSampleRate;
                block.SetFloat<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders,
                    SampleProviderDSP.AudioKernel>(node,
                    SampleProviderDSP.Parameters.SamplePosition, resampleRate);

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