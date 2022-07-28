using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace DSPGraphAudio.DSP.Filters
{
    public struct SpatializerFilterDSP
    {
        private const int SpeedOfSoundMPerS = 343;
        private const int MaxDelay = 1025;

        public enum Channels
        {
            Left = 1,
            Right = 0
        }

        public enum Parameters
        {
            SampleRate,

            RelativeLeftX,
            RelativeLeftY,
            RelativeLeftZ,

            RelativeRightX,
            RelativeRightY,
            RelativeRightZ
        }

        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            [NativeDisableContainerSafetyRestriction]
            private NativeArray<float> _delayBuffer;

            private Spatializer _spatializer;

            public void Initialize()
            {
                _delayBuffer = new NativeArray<float>(MaxDelay * 2, Allocator.AudioKernel);

                // Add a Spatializer that does the work.
                _spatializer = new Spatializer();
            }

            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                SampleBuffer inputBuffer = context.Inputs.GetSampleBuffer(0);
                SampleBuffer outputBuffer = context.Outputs.GetSampleBuffer(0);

                float sampleRate = (int)context.Parameters.GetFloat(Parameters.SampleRate, 0);
                float3 relativePositionL = new float3(
                    context.Parameters.GetFloat(Parameters.RelativeLeftX, 0),
                    context.Parameters.GetFloat(Parameters.RelativeLeftY, 0),
                    context.Parameters.GetFloat(Parameters.RelativeLeftZ, 0)
                );
                float3 relativePositionR = new float3(
                    context.Parameters.GetFloat(Parameters.RelativeRightX, 0),
                    context.Parameters.GetFloat(Parameters.RelativeRightY, 0),
                    context.Parameters.GetFloat(Parameters.RelativeRightZ, 0)
                );

                float distanceL = math.length(relativePositionL);
                float distanceR = math.length(relativePositionR);
                
                float diff = math.abs(distanceL - distanceR);
                int sampleRatePerChannel = (int)(sampleRate / 2);
                int samplesOffset = (int)(diff * sampleRatePerChannel / SpeedOfSoundMPerS);

                Channels channel = distanceL < distanceR
                    ? Channels.Left
                    : Channels.Right;
                
                int delayInSamples = math.min(samplesOffset, MaxDelay);
                _spatializer.DelayedChannel = channel.GetHashCode();
                _spatializer.DelayInSamples = delayInSamples;

                _spatializer.Delay(
                    inputBuffer,
                    outputBuffer,
                    _delayBuffer
                );

                //TODO:2022-07-28 17:34:59 cutoff other channel
            }

            public void Dispose()
            {
                if (_delayBuffer.IsCreated)
                    _delayBuffer.Dispose();
            }
        }

        public struct KernelUpdate : IAudioKernelUpdate<Parameters, SampleProviders, AudioKernel>
        {
            public void Update(ref AudioKernel audioKernel)
            {
                Debug.Log("Start spatializer");
            }
        }

        public static DSPNode CreateNode(DSPCommandBlock block, int channels)
        {
            DSPNode node = block
                .CreateDSPNode<Parameters, SampleProviders, AudioKernel>();

            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);

            return node;
        }

        // The "spatializer" can apply a delay to a channel by a number of samples, so that a sound appears to be coming
        // from the other side.
        // Always is stereo.
        [BurstCompile(CompileSynchronously = true)]
        internal struct Spatializer
        {
            public int DelayedChannel;
            public int DelayInSamples;

            // Delay left or right channel a number of samples.
            public void Delay(
                SampleBuffer input,
                SampleBuffer output,
                NativeArray<float> delayBuffer)
            {
                int sampleDelay = DelayInSamples;

                int delayedCh = DelayedChannel;
                int normalCh = 1 - DelayedChannel;

                NativeArray<float> normalInput = input.GetBuffer(normalCh);
                NativeArray<float> delayedInput = input.GetBuffer(delayedCh);
                NativeArray<float> normalOutput = output.GetBuffer(normalCh);
                NativeArray<float> delayedOutput = output.GetBuffer(delayedCh);


                /*for (int i = 0; i < output.Samples; i++)
                {
                    normalOutput[i] = normalInput[i];
                    delayedOutput[i] = delayedInput[i];
                }
    
                return;*/

                // First, write delay samples from the buffer into the delayed channel.
                // sample Pos 
                int sp = 0;
                for (; sp < sampleDelay; sp++)
                {
                    delayedOutput[sp] = delayBuffer[sp]; // Read from the buffer (can be empty at the start).
                    normalOutput[sp] = normalInput[sp];
                }

                // Then, write the rest up to the delayed part.
                for (; sp < output.Samples; sp++)
                {
                    delayedOutput[sp] = delayedInput[sp - sampleDelay]; // From the delayed input.
                    normalOutput[sp] = normalInput[sp];
                }

                // And write the rest (of the delayed channel) on the delay buffer.
                sp -= sampleDelay;
                for (int i = 0; sp < output.Samples; sp++, i++)
                    delayBuffer[i] = delayedInput[sp]; // Write the rest to the buffer.
            }
        }
    }
}