using System;
using System.Collections.Generic;
using Unity.Audio;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace DSPGraphAudio.Kernel.Systems
{
    [BurstCompile]
    public partial class AudioSystem : SystemBase
    {
        private const float MinAttenuation = 0.1f;

        private const float MaxAttenuation = 1f;

        // Ears are 2 metres apart (-1 - +1).
        private const float MidToEarDistance = 0.25f;
        private const int SpeedOfSoundMPerS = 343;

        // Clip stopped event.
        


        private DSPGraph _graph;
        private List<DSPNode> _freeNodes;
        private List<DSPNode> _playingNodes;
        private Dictionary<DSPNode, DSPNode> _clipToSpatializerMap;
        private Dictionary<DSPNode, DSPConnection> _clipToConnectionMap;
        private Dictionary<DSPNode, DSPNode> _clipToLowpassMap;
        private List<DSPConnection> _connections;
        private AudioOutputHandle _output;

        private int _handlerID;

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
                
                DSPNode node = AudioKernelNodeUtils.CreatePrimaryNode(block, channels);

                Connect(block, node);
                _playingNodes.Add(node);
                
                // Used for directional sound
                DSPNode spatializerNode = AudioKernelNodeUtils.CreateSpatializerNode(block, channels);
                _clipToSpatializerMap.Add(node, spatializerNode);
                
                // Lowpass based on distance
                DSPNode lowpassFilterNode = AudioKernelNodeUtils.CreateLowpassFilterNode(block, 1000, channels);
                _clipToLowpassMap.Add(node, lowpassFilterNode);
               
                
                Connect(block, spatializerNode, lowpassFilterNode);
                _clipToConnectionMap.Add(node, Connect(block, node, spatializerNode));
                Connect(block, lowpassFilterNode, _graph.RootDSP);

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
            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                for (int i = 0; i < _connections.Count; i++) block.Disconnect(_connections[i]);
                for (int i = 0; i < _playingNodes.Count; i++) block.ReleaseDSPNode(_playingNodes[i]);
                for (int i = 0; i < _freeNodes.Count; i++) block.ReleaseDSPNode(_freeNodes[i]);
            }

            _graph.RemoveNodeEventHandler(_handlerID);

            _output.Dispose();
        }
    }
}