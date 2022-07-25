using DSPGraphAudio.Kernel;
using DSPGraphAudio.Kernel.Systems;
using Unity.Audio;
using UnityEngine;

namespace MonoComponents
{
    public class MonoAudioPlayer : MonoBehaviour
    {
        public AudioClip clipToPlay;

        private AudioOutputHandle _output;
        private DSPGraph _graph;
        private DSPNode _node;
        private DSPConnection _connection;

        private int _handlerID;

        private void Start()
        {
            SoundFormat format = ChannelEnumConverter.GetSoundFormatFromSpeakerMode(AudioSettings.speakerMode);
            int channels = ChannelEnumConverter.GetChannelCountFromSoundFormat(format);
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);

            int sampleRate = AudioSettings.outputSampleRate;

            _graph = DSPGraph.Create(format, channels, bufferLength, sampleRate);

            DefaultDSPGraphDriver driver = new DefaultDSPGraphDriver { Graph = _graph };
            _output = driver.AttachToDefaultOutput();

            // Add an event handler delegate to the graph for ClipStopped. So we are notified
            // of when a clip is stopped in the node and can handle the resources on the main thread.
            _handlerID = _graph.AddNodeEventHandler<AudioSystem.ClipStoppedEvent>((node, evt) =>
            {
                Debug.Log("Received ClipStopped event on main thread, cleaning resources");
            });

            // All async interaction with the graph must be done through a DSPCommandBlock.
            // Create it here and complete it once all commands are added.
            DSPCommandBlock block = _graph.CreateCommandBlock();

            _node = block.CreateDSPNode<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>();

            // Currently input and output ports are dynamic and added via this API to a node.
            // This will change to a static definition of nodes in the future.
            block.AddOutletPort(_node, 2);

            // Connect the node to the root of the graph.
            _connection = block.Connect(_node, 0, _graph.RootDSP, 0);

            // We are done, fire off the command block atomically to the mixer thread.
            block.Complete();
        }

        private void Update()
        {
            _graph.Update();
        }

        private void OnDisable()
        {
            // Command blocks can also be completed via the C# 'using' construct for convenience
            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                block.Disconnect(_connection);
                block.ReleaseDSPNode(_node);
            }

            _graph.RemoveNodeEventHandler(_handlerID);

            _output.Dispose();
        }

        public void PlayAudioClip()
        {
            if (clipToPlay == null)
            {
                Debug.Log("No clip assigned, not playing (" + gameObject.name + ")");
                return;
            }

            /*
            using (DSPCommandBlock block = m_Graph.CreateCommandBlock())
            {
                // Decide on playback rate here by taking the provider input rate and the output settings of the system
                float resampleRate = (float)clipToPlay.frequency / AudioSettings.outputSampleRate;
                block.SetFloat<PlayClipNode.Parameters, PlayClipNode.SampleProviders, PlayClipNode>(m_Node,
                    PlayClipNode.Parameters.Rate, resampleRate);

                // Assign the sample provider to the slot of the node.
                block.SetSampleProvider<PlayClipNode.Parameters, PlayClipNode.SampleProviders, PlayClipNode>(
                    clipToPlay, m_Node, PlayClipNode.SampleProviders.DefaultSlot);

                // Kick off playback. This will be done in a better way in the future.
                block.UpdateAudioKernel<PlayClipKernelUpdate, PlayClipKernel.Parameters, PlayClipKernel.SampleProviders,
                    PlayClipKernel>(new PlayClipKernelUpdate(), m_Node);
            }
            */

            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                // Decide on playback rate here by taking the provider input rate and the output settings of the system
                // Decide on playback rate here by taking the provider input rate and the output settings of the system
                float resampleRate = (float)clipToPlay.frequency / AudioSettings.outputSampleRate;
                block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(_node,
                    AudioKernel.Parameters.Rate, resampleRate);

                // Assign the sample provider to the slot of the node.
                block.SetSampleProvider<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(
                    clipToPlay, _node, AudioKernel.SampleProviders.DefaultOutput);

                // Kick off playback. This will be done in a better way in the future.
                block.UpdateAudioKernel<AudioKernelUpdate, AudioKernel.Parameters, AudioKernel.SampleProviders,
                    AudioKernel>(new AudioKernelUpdate(), _node);
                /*block.UpdateAudioKernel<PlayClipKernelUpdate, PlayClipKernel.Parameters, PlayClipKernel.SampleProviders,
                    PlayClipKernel>(new PlayClipKernelUpdate(), _node);*/
            }
        }
    }
}