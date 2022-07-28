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
            float3 leftEarPos = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.LeftReceiver).Position;
            float3 rightEarPos = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.RightReceiver).Position;
            
            // get nodes
            Entities.ForEach((Entity e, in WorldAudioEmitter emitter, in LocalToWorld pos) =>
                {
                    AudioSystem audioSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<AudioSystem>();
                    
                    // calculate vector to listener
                    float3 relativePositionL = pos.Position - leftEarPos;
                    float3 relativePositionR = pos.Position - rightEarPos;
                    float distanceL = math.length(relativePositionL);
                    float distanceR = math.length(relativePositionR);
                    SpatializerFilterDSP.Channels channel = distanceL < distanceR
                        ? SpatializerFilterDSP.Channels.Left
                        : SpatializerFilterDSP.Channels.Right;
                    // Set delay samples based on relativeTranslation. How much from the left/right is it coming?
                    
                    float diff = math.abs(distanceL - distanceR);
                    int sampleRatePerChannel = audioSystem.SampleRate / audioSystem.OutputChannelCount;
                    float samples = diff * sampleRatePerChannel / SpeedOfSoundMPerS;

                    using (DSPCommandBlock block = audioSystem.CreateCommandBlock())
                    {
                        // set channel delay
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                                SpatializerFilterDSP.AudioKernel>
                            (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.Channel, (float)channel);

                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                                SpatializerFilterDSP.AudioKernel>
                            (emitter.SpatializerNode, SpatializerFilterDSP.Parameters.SampleOffset, samples);
                    
                        // set frequency filters
                        
                        // set attenuation
                        DSPConnection connection = emitter.EmitterConnection;
                        float closestDistance = math.min(distanceL, distanceR);
                        // Anything inside 10m has no attenuation.
                        float closestInside10mCircle = math.max(closestDistance - 9, 1);
                        float attenuation = math.clamp(1 / closestInside10mCircle, MinAttenuation, MaxAttenuation);
                        block.SetAttenuation(connection, attenuation);
                    }
                })
                .Run();
        }
    }
}