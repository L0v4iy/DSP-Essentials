using DSPGraphAudio.DSP.Providers;
using Unity.Audio;

namespace DSPGraphAudio.Kernel
{
    public enum ClipStopReason
    {
        ManualStop,
        ClipEnd,
        Error
    }

    public struct ClipStoppedEvent
    {
        public readonly ExecuteContext<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders> Context;
        public readonly ClipStopReason Reason;

        public ClipStoppedEvent(ExecuteContext<SampleProviderDSP.Parameters, SampleProviderDSP.SampleProviders> context,
            ClipStopReason reason)
        {
            Context = context;
            Reason = reason;
        }
    }
}