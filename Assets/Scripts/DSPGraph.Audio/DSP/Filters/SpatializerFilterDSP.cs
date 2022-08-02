using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace DSPGraph.Audio.DSP.Filters
{
    public struct SpatializerFilterDSP
    {
        private const int MaxDelay = 1024*8;

        public enum Parameters
        {
            // in samples
            RightChannelOffset,
            LeftChannelOffset
        }
        
        public enum SampleProviders
        {
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct AudioKernel : IAudioKernel<Parameters, SampleProviders>
        {
            [NativeDisableContainerSafetyRestriction]
            private NativeArray<float> _delayBufferL;
            private NativeArray<float> _delayBufferR;

            private Delayer _delayer;

            public void Initialize()
            {
                _delayBufferL = new NativeArray<float>(MaxDelay, Allocator.AudioKernel);
                _delayBufferR = new NativeArray<float>(MaxDelay, Allocator.AudioKernel);
                
                _delayer = new Delayer();
            }


            public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
            {
                SampleBuffer inputBuffer = context.Inputs.GetSampleBuffer(0);
                SampleBuffer outputBuffer = context.Outputs.GetSampleBuffer(0);
                
                int delayInSamplesL = math.min(
                    (int)context.Parameters.GetFloat(Parameters.LeftChannelOffset, 0), 
                    MaxDelay
                    );
                int delayInSamplesR = math.min(
                    (int)context.Parameters.GetFloat(Parameters.RightChannelOffset, 0), 
                    MaxDelay
                    );

                _delayer.DelayInSamplesL = delayInSamplesL;
                _delayer.DelayInSamplesR = delayInSamplesR;
                _delayer.Delay(
                    inputBuffer,
                    outputBuffer,
                    _delayBufferL,
                    _delayBufferR
                );
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

        internal struct Delayer
        {
            public int DelayedChannel;
            public int DelayInSamples;
            
            public int DelayInSamplesL;
            public int DelayInSamplesR;

            // Delay left or right channel a number of samples.
            public void Delay(
                SampleBuffer input,
                SampleBuffer output,
                NativeArray<float> delayBufferL,
                NativeArray<float> delayBufferR
                )
            {
                NativeArray<float> inputL = input.GetBuffer(0);
                NativeArray<float> inputR = input.GetBuffer(1);
                NativeArray<float> outputL = output.GetBuffer(0);
                NativeArray<float> outputR = output.GetBuffer(1);
                
                NativeStream stream = new NativeStream();
                stream.
                


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
                    outputR[sp] = delayBuffer[sp]; // Read from the buffer (can be empty at the start).
                    outputL[sp] = inputL[sp];
                }

                // Then, write the rest up to the delayed part.
                for (; sp < output.Samples; sp++)
                {
                    outputR[sp] = delayedInput[sp - sampleDelay]; // From the delayed input.
                    outputL[sp] = inputL[sp];
                }

                // And write the rest (of the delayed channel) on the delay buffer.
                sp -= sampleDelay;
                for (int i = 0; sp < output.Samples; sp++, i++)
                    delayBuffer[i] = delayedInput[sp]; // Write the rest to the buffer.
            }
        }
    }
}