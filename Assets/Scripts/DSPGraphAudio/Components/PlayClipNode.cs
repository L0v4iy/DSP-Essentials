using DSPGraphAudio.Kernel;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DSPGraphAudio.Components
{
    [BurstCompile(CompileSynchronously = true)]
    internal struct PlayClipNode : IAudioKernel<PlayClipNode.NoteParameters, PlayClipNode.NoteProviders>
    {
        public enum NoteParameters
        {
            Rate
        }

        public enum NoteProviders
        {
            DefaultSlot
        }

        public Resampler resampler;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<float> resampleBuffer;

        public bool isPlaying;

        public void Initialize()
        {
            resampleBuffer = new NativeArray<float>(1024, Allocator.AudioKernel);
            resampler.Position = resampleBuffer.Length;
        }

        public void Execute(ref ExecuteContext<NoteParameters, NoteProviders> context)
        {
            if (!isPlaying)
                return;

            SampleBuffer buffer = context.Outputs.GetSampleBuffer(0);
            SampleProvider provider = context.Providers.GetSampleProvider(NoteProviders.DefaultSlot);
            bool finished = resampler.ResampleLerpRead(provider, resampleBuffer, buffer, context.Parameters,
                NoteParameters.Rate);

            if (!finished)
                return;

            // Post an async event back to the main thread, telling the handler that the clip has stopped playing.
            context.PostEvent(new ClipStoppedEvent());
            isPlaying = false;
        }

        public void Dispose()
        {
            if (resampleBuffer.IsCreated)
                resampleBuffer.Dispose();
        }

        internal struct ClipStoppedEvent
        {
        }
    }
}