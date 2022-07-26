using System;
using DSPGraphAudio.DSP;
using DSPGraphAudio.DSP.Filters;
using DSPGraphAudio.Kernel;
using Unity.Audio;
using UnityEngine;

namespace MonoComponents
{
    public class ScheduleParameter : MonoBehaviour
    {
        public float cutoff = 5000.0f;
        public float q = 1.0f;
        public float gain;

        private DSPGraph _graph;
        private DSPNode _noiseFilter;
        private DSPNode _lowPass;

        private void Start()
        {
            SoundFormat format = ChannelEnumConverter.GetSoundFormatFromSpeakerMode(AudioSettings.speakerMode);
            int channels = ChannelEnumConverter.GetChannelCountFromSoundFormat(format);
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
            int sampleRate = AudioSettings.outputSampleRate;

            _graph = DSPGraph.Create(format, channels, bufferLength, sampleRate);

            DefaultDSPGraphDriver driver = new DefaultDSPGraphDriver { Graph = _graph };
            driver.AttachToDefaultOutput();

            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                _noiseFilter = block.CreateDSPNode<NoiseFilter.Parameters, NoiseFilter.Providers, NoiseFilter>();
                block.AddOutletPort(_noiseFilter, 2);

                _lowPass = EqualizerFilterDSP.CreateNode(block, EqualizerFilterDSP.Type.Lowpass, 2);

                block.Connect(_noiseFilter, 0, _lowPass, 0);
                block.Connect(_lowPass, 0, _graph.RootDSP, 0);
            }
        }

        private void Update()
        {
            _graph.Update();
        }

        private void OnDestroy()
        {
            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                block.ReleaseDSPNode(_noiseFilter);
                block.ReleaseDSPNode(_lowPass);
            }
        }

        private void OnGUI()
        {
            using (DSPCommandBlock block = _graph.CreateCommandBlock())
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(100, 70, 300, 30), "Lowpass Cutoff:");
                float newCutoff = GUI.HorizontalSlider(new Rect(100, 100, 300, 30), cutoff, 10.0f, 22000.0f);
                if (Math.Abs(newCutoff - cutoff) > 0.01f)
                {
                    block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders, EqualizerFilterDSP.AudioKernel>
                        (_lowPass, EqualizerFilterDSP.Parameters.Cutoff, newCutoff);
                    cutoff = newCutoff;
                }

                GUI.Label(new Rect(100, 160, 300, 30), "Lowpass Q:");
                float newq = GUI.HorizontalSlider(new Rect(100, 190, 300, 30), q, 1.0f, 100.0f);
                if (Math.Abs(newq - q) > 0.01f)
                {
                    block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders, EqualizerFilterDSP.AudioKernel>
                        (_lowPass, EqualizerFilterDSP.Parameters.Q, newq);
                    q = newq;
                }

                GUI.Label(new Rect(100, 250, 300, 30), "Gain in dB:");
                float newGain = GUI.HorizontalSlider(new Rect(100, 280, 300, 30), gain, -80.0f, 0.0f);
                if (Math.Abs(newGain - gain) > 0.01f)
                {
                    block.SetFloat<EqualizerFilterDSP.Parameters, EqualizerFilterDSP.SampleProviders, EqualizerFilterDSP.AudioKernel>
                        (_lowPass, EqualizerFilterDSP.Parameters.GainInDBs, newGain);
                    gain = newGain;
                }
            }
        }
    }
}