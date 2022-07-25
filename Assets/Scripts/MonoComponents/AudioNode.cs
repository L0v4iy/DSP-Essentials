using DSPGraphAudio.Kernel;
using DSPGraphAudio.Kernel.Systems;
using Unity.Audio;
using UnityEngine;

namespace MonoComponents
{

    public class AudioNode : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        
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

            //_node = block.CreateDSPNode<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>();
            _node = AudioKernelNodeUtils.CreateSpatializerNode(block, 2);

            // Currently input and output ports are dynamic and added via this API to a node.
            // This will change to a static definition of nodes in the future.
            block.AddOutletPort(_node, 2);

            // Connect the node to the root of the graph.
            _connection = block.Connect(_node, 0, _graph.RootDSP, 0);

            
            block.SetSampleProvider<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
                (clip, _node, AudioKernel.SampleProviders.DefaultOutput);
            

            // Connect the node to the root of the graph.
            _connection = block.Connect(_node, 0, _graph.RootDSP, 0);
            
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
    }
}