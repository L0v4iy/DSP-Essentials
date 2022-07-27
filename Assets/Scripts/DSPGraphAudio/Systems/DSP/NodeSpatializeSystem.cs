using DSPGraphAudio.Components;
using DSPGraphAudio.DSP.Filters;
using DSPGraphAudio.Kernel.Systems;
using Unity.Audio;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DSPGraphAudio.Systems.DSP
{
    [UpdateInGroup(typeof(DSPGroup))]
    [BurstCompile(CompileSynchronously = true)]
    public partial class NodeSpatializeSystem : SystemBase
    {
        private const float MinAttenuation = 0.1f;
        private const float MaxAttenuation = 1f;

        // Ears are 0.5 metres apart (-0.25 - +0.25).
        private const float MidToEarDistance = 0.25f;
        private const int SpeedOfSoundMPerS = 343;

        [BurstCompile]
        protected override void OnUpdate()
        {
            Entity receiverEntity = EntityManager.CreateEntityQuery(typeof(AudioReceiver)).GetSingletonEntity();
            LocalToWorld receiverPos = EntityManager.GetComponentData<LocalToWorld>(receiverEntity);
            
            // get nodes
            Entities.ForEach((Entity e, in WorldAudioEmitter emitter, in LocalToWorld pos) =>
                {
                    AudioSystem audioSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<AudioSystem>();
                    DSPCommandBlock block = audioSystem.CreateCommandBlock();

                    // calculate vector to listener
                    float3 relativeTranslation = pos.Position - receiverPos.Position;
                    Debug.Log($"ee: {e}, re: {receiverEntity}, rt: {relativeTranslation}");

                    SpatializerFilterDSP.Channels channel = relativeTranslation.x < 0
                        ? SpatializerFilterDSP.Channels.Left
                        : SpatializerFilterDSP.Channels.Right;
                    // Set delay samples based on relativeTranslation. How much from the left/right is it coming?
                    float distanceA = math.length(relativeTranslation + new float3(-MidToEarDistance, 0, 0));
                    float distanceB = math.length(relativeTranslation + new float3(+MidToEarDistance, 0, 0));
                    float diff = math.abs(distanceA - distanceB);
                    int sampleRatePerChannel = audioSystem.SampleRate / audioSystem.OutputChannelCount;
                    float samples = diff * sampleRatePerChannel / SpeedOfSoundMPerS;

                    block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>
                        (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.Channel, (float)channel);

                    block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>
                        (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.SampleOffset, samples);


                    DSPConnection connection = emitter.EmitterConnection;
                    float closestDistance = math.min(distanceA, distanceB);
                    // Anything inside 10m has no attenuation.
                    float closestInside10mCircle = math.max(closestDistance - 9, 1);
                    float attenuation = math.clamp(1 / closestInside10mCircle, MinAttenuation, MaxAttenuation);
                    block.SetAttenuation(connection, attenuation);

                    // apply stats
                    block.Complete();
                })
                .Run();
        }
    }
}