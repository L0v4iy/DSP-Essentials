using Unity.Audio;

namespace DSPGraphAudio.Kernel.Systems
{
    public enum ClipStopReason
    {
        ManualStop,
        ClipEnd,
        Error
    }

    public struct ClipStoppedEvent
    {
        public readonly ExecuteContext<AudioKernel.Parameters, AudioKernel.SampleProviders> Context;
        public readonly ClipStopReason Reason;

        public ClipStoppedEvent(ExecuteContext<AudioKernel.Parameters, AudioKernel.SampleProviders> context,
            ClipStopReason reason)
        {
            Context = context;
            Reason = reason;
        }
    }
}