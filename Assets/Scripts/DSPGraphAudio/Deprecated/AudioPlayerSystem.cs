using DSPGraphAudio.Kernel.PlayClip;
using Unity.Audio;
using Unity.Entities;
using UnityEngine;

namespace DSPGraphAudio.Deprecated
{
    public partial class AudioPlayerSystem : SystemBase
    {
        // Output device
        private AudioOutputHandle _outputHandler;

        // Mixing engine container which provides methods to create DSPNodes and DSPConnections.
        private DSPGraph _graph;

        // A node (element) in a DSPGraph
        private DSPNode _node;

        // A handle representing a connection between two DSPNodes
        private DSPConnection _connection;
        private int _handlerId;

        protected override void OnStartRunning()
        {
            SoundFormat format = ChannelEnumConverter.GetSoundFormatFromSpeakerMode(AudioSettings.speakerMode);
            int outputChannels = ChannelEnumConverter.GetChannelCountFromSoundFormat(format);
            AudioSettings.GetDSPBufferSize(out int bufferSize, out int numBuffers);
            int sampleRate = AudioSettings.outputSampleRate;

            _graph = DSPGraph.Create(format, outputChannels, bufferSize, sampleRate);

            DefaultDSPGraphDriver driver = new DefaultDSPGraphDriver
            {
                Graph = _graph
            };
            _outputHandler = driver.AttachToDefaultOutput();

            // Add an event handler delegate to the graph for ClipStopped. So we are notified
            // of when a clip is stopped in the node and can handle the resources on the main thread.
            // Callback OnPlayerStop
            _handlerId = _graph.AddNodeEventHandler<PlayClipNode.ClipStoppedEvent>((node, evt) =>
            {
                Debug.Log("Received ClipStopped event on main thread, cleaning resources");
            });

            // All async interaction with the graph must be done through a DSPCommandBlock.
            // Create it here and complete it once all commands are added.
            DSPCommandBlock block = _graph.CreateCommandBlock();

            _node = block.CreateDSPNode<PlayClipNode.Parameters, PlayClipNode.SampleProviders, PlayClipNode>();

            // Currently input and output ports are dynamic and added via this API to a node.
            // This will change to a static definition of nodes in the future.
            block.AddOutletPort(_node, 2);

            // Connect the node to the root of the graph.
            _connection = block.Connect(_node, 0, _graph.RootDSP, 0);

            // We are done, fire off the command block atomically to the mixer thread.
            block.Complete();
        }

        protected override void OnDestroy()
        {
            // Command blocks can also be completed via the C# 'using' construct for convenience
            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                block.Disconnect(_connection);
                block.ReleaseDSPNode(_node);
            }

            _graph.RemoveNodeEventHandler(_handlerId);
            _outputHandler.Dispose();
        }

        public void PlaySoundOnEntity(Entity emitter, AudioClip clip)
        {
            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                // Decide on playback rate here by taking the provider input rate and the output settings of the system
                float resampleRate = (float)clip.frequency / AudioSettings.outputSampleRate;
                block.SetFloat<PlayClipNode.Parameters, PlayClipNode.SampleProviders, PlayClipNode>(_node,
                    PlayClipNode.Parameters.Rate, resampleRate);

                // Assign the sample provider to the slot of the node.
                block.SetSampleProvider<PlayClipNode.Parameters, PlayClipNode.SampleProviders, PlayClipNode>(
                    clip, _node, PlayClipNode.SampleProviders.DefaultSlot);

                // Kick off playback. This will be done in a better way in the future.
                block.UpdateAudioKernel<PlayClipKernelUpdate, PlayClipKernel.Parameters, PlayClipKernel.SampleProviders,
                    PlayClipKernel>(new PlayClipKernelUpdate(), _node);
            }
        }

        protected override void OnUpdate()
        {
        }
    }
}