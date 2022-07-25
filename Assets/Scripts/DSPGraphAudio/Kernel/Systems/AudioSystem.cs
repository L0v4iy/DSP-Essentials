using System;
using System.Collections.Generic;
using DSPGraphAudio.DSP;
using DSPGraphAudio.Kernel.Audio;
using Unity.Audio;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DSPGraphAudio.Kernel.Systems
{
    public partial class AudioSystem : SystemBase
    {
        private const float MinAttenuation = 0.1f;

        private const float MaxAttenuation = 1f;

        // Ears are 2 metres apart (-1 - +1).
        private const float MidToEarDistance = 0.25f;
        private const int SpeedOfSoundMPerS = 343;

        // Clip stopped event.
        public struct ClipStopped
        {
        }

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

            if (!_graph.Valid)
            {
                Debug.Log("DSPGraph not valid!");
                return;
            }

            DefaultDSPGraphDriver driver = new DefaultDSPGraphDriver { Graph = _graph };
            _output = driver.AttachToDefaultOutput();

            // Add an event handler delegate to the graph for ClipStopped. So we are notified
            // of when a clip is stopped in the node and can handle the resources on the main thread.
            _handlerID = _graph.AddNodeEventHandler<ClipStopped>(
                (node, evt) =>
                {
                    // Debug.Log(
                    //           "Received ClipStopped event on main thread, cleaning resources"
                    //          );
                    _playingNodes.Remove(node);
                    _freeNodes.Add(node);
                }
            );
            SetupGraph(channels);
        }

        protected void SetupGraph(int channels)
        {
            // All async interaction with the graph must be done through a DSPCommandBlock.
            // Create it here and complete it once all commands are added.
            DSPCommandBlock block = _graph.CreateCommandBlock();

            // var node = createPlayClipNode(block, channels);
            // connect(block, inNode: node, outNode: )

            // We are done, fire off the command block atomically to the mixer thread.
            block.Complete();
        }

        // Play a one shot (relative to the listener).
        // 1. Get free node.
        // 2. Set up playclip params.
        // 3. Set up spatializer params.
        // 4. Set connection attenuation.
        // 5. Set lowpass filter.
        public void playOneShot(AudioClip audioClip, float3 relativeTranslation)
        {
            DSPCommandBlock block = _graph.CreateCommandBlock();

            DSPNode clipNode = GetFreeNode(block, _graph.OutputChannelCount);

            // Decide on playback rate here by taking the provider input rate and the output settings of the system
            /*float resampleRate = (float)audioClip.frequency / AudioSettings.outputSampleRate;
            block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
            (clipNode, AudioKernel.Parameters.Rate, resampleRate
            );*/

            // Assign the sample provider to the slot of the node.
            block.SetSampleProvider<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
            (audioClip, clipNode, AudioKernel.SampleProviders.DefaultOutput
            );

            // Set spatializer node parameters.
            _clipToSpatializerMap.TryGetValue(clipNode, out DSPNode spatializerNode);
            // Set delay channel based on relativeTranslation. Is it coming from left or right?
            SpatializerKernel.Channels channel = relativeTranslation.x < 0
                ? SpatializerKernel.Channels.Left
                : SpatializerKernel.Channels.Right;
            // Set delay samples based on relativeTranslation. How much from the left/right is it coming?
            float distanceA = math.length(relativeTranslation + new float3(-MidToEarDistance, 0, 0));
            float distanceB = math.length(relativeTranslation + new float3(+MidToEarDistance, 0, 0));
            float diff = math.abs(distanceA - distanceB);
            int sampleRatePerChannel = _graph.SampleRate / _graph.OutputChannelCount;
            float samples = diff * sampleRatePerChannel / SpeedOfSoundMPerS;

            block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
                spatializerNode,
                SpatializerKernel.Parameters.Channel,
                (float)
                channel
            );
            block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
                spatializerNode,
                SpatializerKernel.Parameters.Samples,
                samples
            );
            // Set attenuation based on distance.
            _clipToConnectionMap.TryGetValue(clipNode, out DSPConnection connection);
            float closestDistance = math.min(distanceA, distanceB);
            // Anything inside 10m has no attenuation.
            float closestInside10mCircle = math.max(closestDistance - 9, 1);
            block.SetAttenuation(connection, math.clamp(1 / closestInside10mCircle, MinAttenuation, MaxAttenuation));

            // Set lowpass based on distance.
            _clipToLowpassMap.TryGetValue(clipNode, out DSPNode lowpassFilterNode);
            block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>(
                lowpassFilterNode,
                AudioKernel.Parameters.Cutoff,
                math.clamp(
                    1 / closestInside10mCircle * sampleRatePerChannel,
                    1000,
                    sampleRatePerChannel
                )
            );
            // Kick off playback.
            block.UpdateAudioKernel<AudioKernelUpdate, AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
                (new AudioKernelUpdate(), clipNode);

            block.Complete();
        }

        // Return free node (already added to playing nodes). 
        // Also sets up needed nodes and connections.
        protected DSPNode GetFreeNode(DSPCommandBlock block, int channels)
        {
            try
            {
                DSPNode node = _freeNodes[0];
                _freeNodes.RemoveAt(0);

                _playingNodes.Add(node);

                return node;
            }
            catch (Exception e)
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
                // │   PlayClip   │────▶│ Spatializer  │────▶│   Lowpass    │────▶│     Root    │
                // │              │     │              │     │              │     │              │
                // └──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                //         │                    ▲                    ▲                            
                //                              │                                                 
                //         └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘                            
                //          clipToSpatializerMap   clipToLowpassMap                               
                //                              
                DSPNode node = CreatePlayClipNode(block, channels);
                DSPNode spatializerNode = CreateSpatializerNode(block, channels);
                _playingNodes.Add(node);
                _clipToSpatializerMap.Add(node, spatializerNode);

                // Used for directional sound.
                DSPConnection nodeSpatializerConnection = Connect(block, node, spatializerNode);
                _clipToConnectionMap.Add(node, nodeSpatializerConnection);

                // Lowpass based on distance.
                DSPNode lowpassFilterNode = CreateLowpassFilterNode(block, 1000, channels);
                _clipToLowpassMap.Add(node, lowpassFilterNode);

                // Insert lowpass filter node between spatializer and root node.
                Connect(block, spatializerNode, lowpassFilterNode);
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

        private DSPNode CreatePlayClipNode(DSPCommandBlock block, int channels)
        {
            DSPNode node =
                block.CreateDSPNode<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>();

            // Currently input and output ports are dynamic and added via this API to a node.
            // This will change to a static definition of nodes in the future.
            block.AddOutletPort(node, channels);

            return node;
        }

        // Create a spatializer node.
        //
        // Setting parameters:
        // block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
        //                                                                                                    node,
        //                                                                                                    SpatializerKernel.
        //                                                                                                        Parameters.
        //                                                                                                        Channel,
        //                                                                                                    0
        //                                                                                                   );
        // block.SetFloat<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>(
        //                                                                                                    node,
        //                                                                                                    SpatializerKernel.
        //                                                                                                        Parameters.
        //                                                                                                        Samples,
        //                                                                                                    500
        //                                                                                                   );
        private DSPNode CreateSpatializerNode(DSPCommandBlock block, int channels)
        {
            DSPNode node = block
                .CreateDSPNode<SpatializerKernel.Parameters, SpatializerKernel.SampleProviders, SpatializerKernel>();

            block.AddInletPort(node, channels);
            block.AddOutletPort(node, channels);

            return node;
        }

        // Create lowpass filter node.
        //
        // Setting parameters:
        // block.
        //     SetFloat<Filter.AudioKernel.Parameters, Filter.AudioKernel.Providers,
        //         Filter.AudioKernel>(
        //                         lowpassFilterNode,
        //                         Filter.AudioKernel.Parameters.Cutoff,
        //                         cutoffHz
        //                        );
        private DSPNode CreateLowpassFilterNode(DSPCommandBlock block, float cutoffHz, int channels)
        {
            DSPNode node = AudioKernelUtils.CreateNode(block, Filter.Type.Lowpass, channels);
            block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders,
                AudioKernel>(
                node,
                AudioKernel.Parameters.Cutoff,
                cutoffHz
            );
            return node;
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
                for (int i = 0; i < _freeNodes.Count; i++) block.ReleaseDSPNode(_freeNodes[i]);
                for (int i = 0; i < _playingNodes.Count; i++) block.ReleaseDSPNode(_playingNodes[i]);
            }

            _graph.RemoveNodeEventHandler(_handlerID);

            _output.Dispose();
        }
    }
}