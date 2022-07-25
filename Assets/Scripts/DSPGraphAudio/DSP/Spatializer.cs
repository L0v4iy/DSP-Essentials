using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace DSPGraphAudio.DSP
{
    // The kernel for a spatializer node.
    // 
    [BurstCompile(CompileSynchronously = true)]
    internal struct SpatializerKernel : IAudioKernel<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders>
    {
        public enum Parameters
        {
            Channel,
            Samples
        }

        public enum Channels
        {
            Left = 1,
            Right = 0
        }

        public enum SampleProviders
        {
            DefaultSlot
        }

        private const int MaxDelay = 1025;

        [NativeDisableContainerSafetyRestriction]
        private NativeArray<float> _delayBuffer;

        private Spatializer _spatializer;

        public void Initialize()
        {
            // During an initialization phase, we have access to a resource context which we can
            // do buffer allocations with safely in the job.
            _delayBuffer = new NativeArray<float>(MaxDelay * 2, Allocator.AudioKernel);

            // Add a Spatializer that does the work.
            _spatializer = new Spatializer();
        }

        public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
        {
            // Input and output buffers that we are going to read and write.
            SampleBuffer inputBuffer = context.Inputs.GetSampleBuffer(0);
            SampleBuffer outputBuffer = context.Outputs.GetSampleBuffer(0);

            float delayInSamplesFloat = context.Parameters.GetFloat(Parameters.Samples, 0);
            int delayInSamples = math.min((int)delayInSamplesFloat, MaxDelay);
            _spatializer.DelayedChannel = (int)context.Parameters.GetFloat(Parameters.Channel, 0);
            _spatializer.DelayInSamples = delayInSamples;

            _spatializer.Delay(
                inputBuffer,
                outputBuffer,
                _delayBuffer
            );
        }

        public void Dispose()
        {
            if (_delayBuffer.IsCreated) _delayBuffer.Dispose();
        }
    }
    
    
    internal struct SpatializerKernelUpdate:
        IAudioKernelUpdate<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>
    {
        public void Update(ref SpatializerKernel audioKernel)
        {
            Debug.Log("Start spatalizer");
        }
    }

    // The "spatializer" can apply a delay to a channel by a number of samples, so that a sound appears to be coming
    // from the other side.
    // Always is stereo.
    [BurstCompile(CompileSynchronously = true)]
    public struct Spatializer
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