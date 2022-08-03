﻿using System;
using DSPGraph.Audio.Components;
using DSPGraph.Audio.DSP.Filters;
using DSPGraph.Audio.DSP.Utils;
using Unity.Audio;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DSPGraph.Audio.Systems.DSP
{
    [UpdateInGroup(typeof(DSPGroup))]
    [BurstCompile(CompileSynchronously = true)]
    public partial class NodePositioningSystem : SystemBase
    {
        private const float MinAttenuation = 0.1f;
        private const float MaxAttenuation = 1f;

        private const int SpeedOfSoundMPerS = 343;


        [BurstCompile]
        protected override void OnUpdate()
        {
            // get ears
            Entity receiverEntity = EntityManager.CreateEntityQuery(typeof(AudioReceiver)).GetSingletonEntity();
            AudioReceiver audioReceiver = EntityManager.GetComponentData<AudioReceiver>(receiverEntity);
            float3 receiverPosL = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.LeftReceiver).Position;
            float3 receiverPosR = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.RightReceiver).Position;
            float3 receiverEulerL = float3.zero;
            float3 receiverEulerR = float3.zero;


            AudioSystem audioSystem = World.GetOrCreateSystem<AudioSystem>();
            int sampleRate = audioSystem.SampleRate;
            int outputChannelCount = audioSystem.OutputChannelCount;
            float soundAbsorptCoeff = SubstanceUtil.GetSubstanceSoundAbsorptionCoefficient(SurroundedSubstance.Air);


            Entities.ForEach(
                    (Entity e, ref WorldAudioEmitter emitter, in LocalToWorld pos, in EqualizerSetter setter) =>
                    {
                        if (!emitter.Valid)
                            return;

                        int sampleRatePerChannel = sampleRate / outputChannelCount;

                        // calculate vector to listener
                        float3 relativePositionL = pos.Position - receiverPosL;
                        float3 relativePositionR = pos.Position - receiverPosR;
                        float distanceL = math.length(relativePositionL);
                        float distanceR = math.length(relativePositionR);
                        
                        // define euler angles (xyz)
                        float3 emitterEuler = float3.zero;

                        // normal | mono | invert 
                        emitter.ChannelInvertRate = 0;
                        
                        float3 relativeNormalizedL = math.normalize(relativePositionL);
                        float3 relativeNormalizedR = math.normalize(relativePositionR);
                        // left config
                        emitter.LeftChannelData.SampleDelay = distanceL * sampleRatePerChannel / SpeedOfSoundMPerS;
                        emitter.LeftChannelData.Attenuation =
                            SubstanceUtil.CalculateAttenuationFactor(distanceL, soundAbsorptCoeff);
                        emitter.LeftChannelData.TransverseFactor = math.dot(math.up() + receiverEulerL, relativeNormalizedL);
                        emitter.LeftChannelData.SagittalFactor = math.dot(math.left() + receiverEulerL, relativeNormalizedL);
                        emitter.LeftChannelData.CoronalFactor = math.dot(math.forward() + receiverEulerL, relativeNormalizedL);

                        // right config
                        emitter.RightChannelData.SampleDelay = distanceR * sampleRatePerChannel / SpeedOfSoundMPerS;
                        emitter.RightChannelData.Attenuation =
                            SubstanceUtil.CalculateAttenuationFactor(distanceR, soundAbsorptCoeff);
                        emitter.RightChannelData.TransverseFactor = math.dot(math.up() + receiverEulerR, relativeNormalizedR);
                        emitter.RightChannelData.SagittalFactor =  math.dot(math.right() + receiverEulerR, relativeNormalizedR);
                        emitter.RightChannelData.CoronalFactor =  math.dot(math.forward() + receiverEulerR, relativeNormalizedR);
                    })
                .Run();

            Entities.ForEach((Entity e, in WorldAudioEmitter emitter) =>
                {
                    if (!emitter.Valid)
                        return;
                    
                    ChannelData leftData = emitter.LeftChannelData;
                    ChannelData rightData = emitter.RightChannelData;

                    using (DSPCommandBlock block = audioSystem.CreateCommandBlock())
                    {
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                                SpatializerFilterDSP.AudioKernel>
                            (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.LeftChannelOffset, leftData.SampleDelay);
        
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                                SpatializerFilterDSP.AudioKernel>
                            (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.RightChannelOffset, rightData.SampleDelay);

                        // set frequency filters
                        /*block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders,
                            EqualizerFilterDSP.AudioKernel>(
                            emitter.EqualizerFilterNode,
                            EqualizerFilterDSP.Parameters.Cutoff,
                            math.clamp(
                                1 / closestInside10mCircle * sampleRatePerChannel,
                                1000,
                                sampleRatePerChannel
                            )
                        );*/
                        /*block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders,
                                EqualizerFilterDSP.AudioKernel>
                            (emitter.EqualizerFilterNode, EqualizerFilterDSP.Parameters.Cutoff, setter.Cutoff);
                        block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders,
                                EqualizerFilterDSP.AudioKernel>
                            (emitter.EqualizerFilterNode, EqualizerFilterDSP.Parameters.Q, setter.Q);*/
                        /*block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders,
                                EqualizerFilterDSP.AudioKernel>
                            (emitter.EqualizerFilterNode, EqualizerFilterDSP.Parameters.GainInDBs, setter.GainInDBs);*/

                        // set attenuation
                        /*DSPConnection connection = emitter.EmitterConnection;
                        float attenuation = math.clamp(1 / closestInside10mCircle, MinAttenuation, MaxAttenuation);
                        block.SetAttenuation(connection, attenuation);*/
                        
                    }
                })
                .WithoutBurst()
                .Run();


            /*float closestDistance = math.min(distanceL, distanceR);
float diff = math.abs(distanceL - distanceR);

float samples = diff * ;
SpatializerFilterDSP.Channels channel = distanceL < distanceR
    ? SpatializerFilterDSP.Channels.Left
    : SpatializerFilterDSP.Channels.Right;

float closestInside10mCircle = math.max(closestDistance - 9, 1);*/
        }


        private static float3 CalculateAngleBetweenReceiverAndEmitter(float3 receiverPos, float3 emitterPos)
        {
            throw new NotImplementedException();
        }
    }
}