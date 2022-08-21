using System;
using DSPGraph.Audio.Components;
using DSPGraph.Audio.DSP.Filters;
using Unity.Audio;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DSPGraph.Audio.Systems.DSP
{
    [UpdateInGroup(typeof(DSPGroup))]
    [BurstCompile(CompileSynchronously = true)]
    public partial class NodePositioningSystem : SystemBase
    {
        private const int SpeedOfSoundMPerS = 343;


        [BurstCompile]
        protected override void OnUpdate()
        {
            // get ears
            Entity receiverEntity = EntityManager.CreateEntityQuery(typeof(AudioReceiver)).GetSingletonEntity();
            AudioReceiver audioReceiver = EntityManager.GetComponentData<AudioReceiver>(receiverEntity);
            LocalToWorld localToWorldL = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.LeftReceiver);
            LocalToWorld localToWorldR = EntityManager.GetComponentData<LocalToWorld>(audioReceiver.RightReceiver);
            float3 receiverPosL = localToWorldL.Position;
            float3 receiverPosR = localToWorldR.Position;
            Quaternion quatL = localToWorldL.Rotation;
            Quaternion quatR = localToWorldR.Rotation;

            AudioSystem audioSystem = World.GetOrCreateSystem<AudioSystem>();
            int sampleRate = audioSystem.SampleRate;
            int sampleRatePerChannel = sampleRate / 2;

            Entities.ForEach(
                    (Entity e, ref WorldAudioEmitter emitter, in LocalToWorld pos) =>
                    {
                        if (!emitter.Valid)
                            return;

                        // calculate vector to listener
                        float3 relativePositionL = pos.Position - receiverPosL;
                        float3 relativePositionR = pos.Position - receiverPosR;
                        float distanceL = math.length(relativePositionL);
                        float distanceR = math.length(relativePositionR);

                        // define euler angles (xyz)
                        // dont do this
                        float3 emitterEuler = float3.zero;

                        // normal | mono | invert 
                        emitter.ChannelInvertRate = 0;

                        float3 relativeNormalizedL = math.normalize(relativePositionL);
                        float3 relativeNormalizedR = math.normalize(relativePositionR);
                        // left config
                        emitter.LeftChannelData.SampleDelay = distanceL * sampleRatePerChannel / SpeedOfSoundMPerS;
                        emitter.LeftChannelData.DistanceToReceiver = distanceL;
                        emitter.LeftChannelData.TransverseFactor = math.dot
                        (
                            RotateVectorByQuaternion(math.up(), quatL),
                            relativeNormalizedL
                        );
                        emitter.LeftChannelData.SagittalFactor = math.dot
                        (
                            RotateVectorByQuaternion(math.left(), quatL),
                            relativeNormalizedL
                        );
                        emitter.LeftChannelData.CoronalFactor = math.dot(
                            RotateVectorByQuaternion(math.forward(), quatL),
                            relativeNormalizedL
                        );

                        // right config
                        emitter.RightChannelData.SampleDelay = distanceR * sampleRatePerChannel / SpeedOfSoundMPerS;
                        emitter.RightChannelData.DistanceToReceiver = distanceR;
                        emitter.RightChannelData.TransverseFactor = math.dot
                        (
                            RotateVectorByQuaternion(math.up(), quatR),
                            relativeNormalizedR
                        );
                        emitter.RightChannelData.SagittalFactor = math.dot
                        (
                            RotateVectorByQuaternion(math.left(), quatR),
                            relativeNormalizedR
                        );
                        emitter.RightChannelData.CoronalFactor = math.dot
                        (
                            RotateVectorByQuaternion(math.forward(), quatR),
                            relativeNormalizedR
                        );
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
                        // L
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.ChannelOffsetL, leftData.SampleDelay);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.ReceiverDistanceL, leftData.DistanceToReceiver);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.TransverseL, leftData.TransverseFactor);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.SagittalL, leftData.SagittalFactor);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.CoronalL, leftData.CoronalFactor);

                        // R
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.ChannelOffsetR, rightData.SampleDelay);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.ReceiverDistanceR, rightData.DistanceToReceiver);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.TransverseR, rightData.TransverseFactor);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.SagittalR, rightData.SagittalFactor);
                        block.SetFloat<SpatializerFilterDSP.Parameters, SpatializerFilterDSP.SampleProviders,
                            SpatializerFilterDSP.AudioKernel>(emitter.SpatializerNode,
                            SpatializerFilterDSP.Parameters.CoronalR, rightData.CoronalFactor);
                    }
                })
                .WithoutBurst()
                .Run();
        }


        private static float3 CalculateAngleBetweenReceiverAndEmitter(float3 receiverPos, float3 emitterPos)
        {
            throw new NotImplementedException();
        }

        private static float3 RotateVectorByQuaternion(in float3 v, in quaternion q)
        {
            // Extract the vector part of the quaternion
            float4 qv = q.value;
            float3 u = new float3(qv.x, qv.y, qv.z);

            // Extract the scalar part of the quaternion
            float s = qv.w;

            // Do the math
            return 2.0f * math.dot(u, v) * u
                   + (s * s - math.dot(u, u)) * v
                   + 2.0f * s * math.cross(u, v);
        }
    }
}