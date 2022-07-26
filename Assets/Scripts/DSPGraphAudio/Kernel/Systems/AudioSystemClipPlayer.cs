using System;
using DSPGraphAudio.DSP;
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
        public void PlayOneShot(AudioClip audioClip, float3 relativeTranslation)
        {
            DSPCommandBlock block = _graph.CreateCommandBlock();

            DSPNode clipNode = GetFreeNode(block, _graph.OutputChannelCount);

            // Decide on playback rate here by taking the provider input rate and the output settings of the system
            /*float resampleRate = (float)audioClip.frequency / AudioSettings.outputSampleRate;
            block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
            (clipNode, AudioKernel.Parameters.Rate, resampleRate
            );*/

            // Assign the sample provider to the slot of the node.
            block.SetSampleProvider<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
            (audioClip, clipNode, AudioKernel.SampleProviders.DefaultSlot
            );

            // Set spatializer node parameters.
            _clipToSpatializerMap.TryGetValue(clipNode, out DSPNode spatializerNode);
            // Set delay channel based on relativeTranslation. Is it coming from left or right?
            SpatializerKernel.Channels channel = relativeTranslation.x < 0
                ? SpatializerKernel.Channels.Left
                : SpatializerKernel.Channels.Right;
            // Set delay samples based on relativeTranslation. How much from the left/right is it coming?
            float distanceA = math.length(relativeTranslation + new float3(-MidToEarDistance, 0, 0));
            float distanceB = math.length(relativeTranslation + new float3(+MidToEarDistance, 0, 0));
            float diff = math.abs(distanceA - distanceB);
            int sampleRatePerChannel = _graph.SampleRate / _graph.OutputChannelCount;
            float sampleOffset = diff * sampleRatePerChannel / SpeedOfSoundMPerS;

            block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
                spatializerNode,
                SpatializerKernel.Parameters.Channel,
                (float)
                channel
            );
            block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
                spatializerNode,
                SpatializerKernel.Parameters.SampleOffset,
                sampleOffset
            );
            // Set attenuation based on distance.
            _clipToConnectionMap.TryGetValue(clipNode, out DSPConnection connection);
            float closestDistance = math.min(distanceA, distanceB);
            // Anything inside 10m has no attenuation.
            float closestInside10mCircle = math.max(closestDistance - 9, 1);
            block.SetAttenuation(connection, math.clamp(1 / closestInside10mCircle, MinAttenuation, MaxAttenuation));

            // Set lowpass based on distance.
            _clipToLowpassMap.TryGetValue(clipNode, out DSPNode lowpassFilterNode);
            block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(
                lowpassFilterNode,
                AudioKernel.Parameters.Cutoff,
                math.clamp(
                    1 / closestInside10mCircle * sampleRatePerChannel,
                    1000,
                    sampleRatePerChannel
                )
            );
            // Kick off playback.
            block.UpdateAudioKernel<AudioKernelUpdate, AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
                (new AudioKernelUpdate(), clipNode);

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
                block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(node,
                    AudioKernel.Parameters.Rate, resampleRate);

                // Assign the sample provider to the slot of the node.
                block.SetSampleProvider<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(
                    audioClip, node, AudioKernel.SampleProviders.DefaultSlot);

                // Kick off playback. This will be done in a better way in the future.
                block.UpdateAudioKernel<AudioKernelUpdate, AudioKernel.Parameters, AudioKernel.SampleProviders,
                    AudioKernel>(new AudioKernelUpdate(), node);
            }
        }
    }
}