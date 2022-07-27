using System;
using System.Collections.Generic;
using DSPGraphAudio.DSP.Filters;
using DSPGraphAudio.DSP.Providers;
using Unity.Audio;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace DSPGraphAudio.Kernel.Systems
{
    [BurstCompile(CompileSynchronously = true)]
    public partial class AudioSystem : SystemBase
    {
        private DSPGraph _graph;
        private List<DSPNode> _freeNodes;
        private List<DSPNode> _playingNodes;
        private Dictionary<DSPNode, DSPNode> _clipToSpatializerMap;
        private Dictionary<DSPNode, DSPConnection> _clipToConnectionMap;
        private Dictionary<DSPNode, DSPNode> _clipToLowpassMap;
        private List<DSPConnection> _connections;
        private AudioOutputHandle _output;

        private int _handlerID;

        private EndSimulationEntityCommandBufferSystem _ecbSystem;

        protected override void OnStartRunning()
        {
            _ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnCreate()
        {
            _freeNodes = new List<DSPNode>();
            _playingNodes = new List<DSPNode>();
            _clipToSpatializerMap = new Dictionary<DSPNode, DSPNode>();
            _clipToConnectionMap = new Dictionary<DSPNode, DSPConnection>();
            _clipToLowpassMap = new Dictionary<DSPNode, DSPNode>();
            _connections = new List<DSPConnection>();

            SoundFormat format = ChannelEnumConverter.GetSoundFormatFromSpeakerMode(AudioSettings.speakerMode);
            int channels = ChannelEnumConverter.GetChannelCountFromSoundFormat(format);
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);

            int sampleRate = AudioSettings.outputSampleRate;

            _graph = DSPGraph.Create(format, channels, bufferLength, sampleRate);

            DefaultDSPGraphDriver driver = new DefaultDSPGraphDriver { Graph = _graph };
            _output = driver.AttachToDefaultOutput();

            // Add an event handler delegate to the graph for ClipStopped. So we are notified
            // of when a clip is stopped in the node and can handle the resources on the main thread.
            _handlerID = _graph.AddNodeEventHandler<ClipStoppedEvent>((node, evt) =>
            {
                Debug.Log("Received ClipStopped event on main thread, cleaning resources");
                _playingNodes.Remove(node);
                _freeNodes.Add(node);
            });
        }

        /// <summary>
        /// Return free node (already added to playing nodes).
        /// Can set up needed nodes and connections.
        /// </summary>
        protected DSPNode GetFreeNode(DSPCommandBlock block, int channels)
        {
            try
            {
                DSPNode node = _freeNodes[0];
                _freeNodes.RemoveAt(0);

                _playingNodes.Add(node);

                return node;
            }
            catch (Exception)
            {
                // No node is available. Create a new one.
                //
                // The structure that is set up:
                //
                // ┌──────────────────────────────┐   ┌──────────────────────────────┐            
                // │         playingNodes         │   │          freeNodes           │            
                // └──────────────────────────────┘   └──────────────────────────────┘            
                //                 │                                  │                           
                //         ┌──────── ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─                            
                //         │                                                                      
                //         ▼                                                                      
                // ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
                // │              │     │              │     │              │     │              │
                // │   Primary    │────▶│  Spatializer │────▶│   Lowpass    │────▶│     Root     │
                // │              │     │              │     │              │     │              │
                // └──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                //         │                    ▲                    ▲                            
                //                              │                                                 
                //         └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘                            
                //          clipToSpatializerMap   clipToLowpassMap                               
                //

                DSPNode node = SampleProviderDSP.CreateNode(block, channels);
                _playingNodes.Add(node);
                //Connect(block, node);

                // Used for directional sound
                DSPNode spatializerNode = SpatializerFilterDSP.CreateNode(block, channels);
                _clipToSpatializerMap.Add(node, spatializerNode);

                // Lowpass based on distance
                DSPNode lowpassFilterNode =
                    EqualizerFilterDSP.CreateNode(block, EqualizerFilterDSP.Type.Lowpass, channels);
                _clipToLowpassMap.Add(node, lowpassFilterNode);

                block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders,
                    EqualizerFilterDSP.AudioKernel>(
                    lowpassFilterNode,
                    EqualizerFilterDSP.Parameters.Cutoff,
                    1000
                );


                _clipToConnectionMap.Add(node, Connect(block, node, spatializerNode));
                Connect(block, spatializerNode, lowpassFilterNode);
                Connect(block, lowpassFilterNode, _graph.RootDSP);

                /*Connect(block, node, spatializerNode);
                Connect(block, spatializerNode, _graph.RootDSP);*/

                return node;
            }
        }

        private DSPConnection Connect(DSPCommandBlock block, DSPNode inNode, DSPNode? outNode = null)
        {
            outNode = outNode ?? _graph.RootDSP;
            DSPConnection connection = block.Connect(inNode, 0, outNode.Value, 0);
            _connections.Add(connection);
            return connection;
        }

        protected override void OnUpdate()
        {
            _graph.Update();
        }

        protected override void OnDestroy()
        {
            // Command blocks can also be completed via the C# 'using' construct for convenience
            using (DSPCommandBlock block = CreateCommandBlock())
            {
                for (int i = 0; i < _connections.Count; i++) block.Disconnect(_connections[i]);
                for (int i = 0; i < _playingNodes.Count; i++) block.ReleaseDSPNode(_playingNodes[i]);
                for (int i = 0; i < _freeNodes.Count; i++) block.ReleaseDSPNode(_freeNodes[i]);
            }

            _graph.RemoveNodeEventHandler(_handlerID);

            _output.Dispose();
        }

        #region GraphFeatures

        public int SampleRate => _graph.SampleRate;
        public int OutputChannelCount => _graph.OutputChannelCount;
        public DSPCommandBlock CreateCommandBlock() => _graph.CreateCommandBlock();

        #endregion
    }
}