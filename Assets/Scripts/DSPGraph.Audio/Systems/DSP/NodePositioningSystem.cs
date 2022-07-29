using System;
using DSPGraph.Audio.Components;
using DSPGraph.Audio.DSP.Filters;
using Unity.Audio;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
            float3 headPos = EntityManager.GetComponentData<LocalToWorld>(receiverEntity).Position;
            float3 leftEarPos = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.LeftReceiver).Position;
            float3 rightEarPos = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.RightReceiver).Position;

            // get nodes
            Entities.ForEach((Entity e, in WorldAudioEmitter emitter, in LocalToWorld pos, in EqualizerSetter setter) =>
                {
                    if (!emitter.Valid)
                        return;
                    AudioSystem audioSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<AudioSystem>();

                    // calculate vector to listener
                    float3 relativePositionHead = pos.Position - headPos;
                    float3 relativePositionL = pos.Position - leftEarPos;
                    float3 relativePositionR = pos.Position - rightEarPos;
                    float distanceL = math.length(relativePositionL);
                    float distanceR = math.length(relativePositionR);

                    //Debug.Log(relativePositionHead);

                    float closestDistance = math.min(distanceL, distanceR);
                    float diff = math.abs(distanceL - distanceR);
                    int sampleRatePerChannel = audioSystem.SampleRate / audioSystem.OutputChannelCount;
                    float samples = diff * sampleRatePerChannel / SpeedOfSoundMPerS;


                    SpatializerFilterDSP.Channels channel = distanceL < distanceR
                        ? SpatializerFilterDSP.Channels.Left
                        : SpatializerFilterDSP.Channels.Right;

                    float closestInside10mCircle = math.max(closestDistance - 9, 1);

                    using (DSPCommandBlock block = audioSystem.CreateCommandBlock())
                    {
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                                SpatializerFilterDSP.AudioKernel>
                            (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.Channel, (float)channel);

                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                                SpatializerFilterDSP.AudioKernel>
                            (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.SampleOffset, samples);

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
                .Run();
        }

        private static float3 CalculateAngleBetweenReceiverAndEmitter(float3 receiverPos, float3 emitterPos)
        {
            throw new NotImplementedException();
        }
    }
}