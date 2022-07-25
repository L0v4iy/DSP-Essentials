using DSPGraphAudio.Kernel;
using DSPGraphAudio.Kernel.Audio;
using DSPGraphAudio.Kernel.PlayClip;
using Unity.Audio;
using UnityEngine;

namespace DSPGraphAudio.Components
{
    public class MonoAudioPlayer : MonoBehaviour
    {
        public AudioClip clipToPlay;

        private AudioOutputHandle m_Output;
        private DSPGraph m_Graph;
        private DSPNode m_Node;
        private DSPConnection m_Connection;

        private int m_HandlerID;

        private void Start()
        {
            SoundFormat format = ChannelEnumConverter.GetSoundFormatFromSpeakerMode(AudioSettings.speakerMode);
            int channels = ChannelEnumConverter.GetChannelCountFromSoundFormat(format);
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);

            int sampleRate = AudioSettings.outputSampleRate;

            m_Graph = DSPGraph.Create(format, channels, bufferLength, sampleRate);

            DefaultDSPGraphDriver driver = new DefaultDSPGraphDriver { Graph = m_Graph };
            m_Output = driver.AttachToDefaultOutput();

            // Add an event handler delegate to the graph for ClipStopped. So we are notified
            // of when a clip is stopped in the node and can handle the resources on the main thread.
            m_HandlerID = m_Graph.AddNodeEventHandler<PlayClipNode.ClipStoppedEvent>((node, evt) =>
            {
                Debug.Log("Received ClipStopped event on main thread, cleaning resources");
            });

            // All async interaction with the graph must be done through a DSPCommandBlock.
            // Create it here and complete it once all commands are added.
            DSPCommandBlock block = m_Graph.CreateCommandBlock();

            m_Node = block.CreateDSPNode<PlayClipNode.Parameters, PlayClipNode.SampleProviders, PlayClipNode>();

            // Currently input and output ports are dynamic and added via this API to a node.
            // This will change to a static definition of nodes in the future.
            block.AddOutletPort(m_Node, 2);

            // Connect the node to the root of the graph.
            m_Connection = block.Connect(m_Node, 0, m_Graph.RootDSP, 0);

            // We are done, fire off the command block atomically to the mixer thread.
            block.Complete();
        }

        private void Update()
        {
            m_Graph.Update();
        }

        private void OnDisable()
        {
            // Command blocks can also be completed via the C# 'using' construct for convenience
            using (DSPCommandBlock block = m_Graph.CreateCommandBlock())
            {
                block.Disconnect(m_Connection);
                block.ReleaseDSPNode(m_Node);
            }

            m_Graph.RemoveNodeEventHandler(m_HandlerID);

            m_Output.Dispose();
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
            
            using (DSPCommandBlock block = m_Graph.CreateCommandBlock())
            {
                // Assign the sample provider to the slot of the node.
                block.SetSampleProvider<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(
                    clipToPlay, m_Node, AudioKernel.SampleProviders.DefaultOutput);

                // Kick off playback. This will be done in a better way in the future.
                block.UpdateAudioKernel<AudioKernelUpdate, AudioKernel.Parameters, AudioKernel.SampleProviders,
                    AudioKernel>(new AudioKernelUpdate(), m_Node);
            }
        }
    }
}