using System;
using DSPGraphAudio.DSP;
using DSPGraphAudio.Kernel;
using DSPGraphAudio.Kernel.AudioKernel;
using Unity.Audio;
using UnityEngine;

namespace Simple_DSPGraph_examples.ScheduleParameter
{
    public class ScheduleParameter : MonoBehaviour
    {
        public float Cutoff = 5000.0f;
        public float Q = 1.0f;
        public float Gain;

        private DSPGraph m_Graph;
        private DSPNode m_NoiseFilter;
        private DSPNode m_LowPass;

        private void Start()
        {
            SoundFormat format = ChannelEnumConverter.GetSoundFormatFromSpeakerMode(AudioSettings.speakerMode);
            int channels = ChannelEnumConverter.GetChannelCountFromSoundFormat(format);
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
            int sampleRate = AudioSettings.outputSampleRate;

            m_Graph = DSPGraph.Create(format, channels, bufferLength, sampleRate);

            DefaultDSPGraphDriver driver = new DefaultDSPGraphDriver { Graph = m_Graph };
            driver.AttachToDefaultOutput();

            using (DSPCommandBlock block = m_Graph.CreateCommandBlock())
            {
                m_NoiseFilter = block.CreateDSPNode<NoiseFilter.Parameters, NoiseFilter.Providers, NoiseFilter>();
                block.AddOutletPort(m_NoiseFilter, 2);

                m_LowPass = AudioKernelFacade.CreateNode(block, Filter.Type.Lowpass, 2);

                block.Connect(m_NoiseFilter, 0, m_LowPass, 0);
                block.Connect(m_LowPass, 0, m_Graph.RootDSP, 0);
            }
        }

        private void Update()
        {
            m_Graph.Update();
        }

        private void OnDestroy()
        {
            using (DSPCommandBlock block = m_Graph.CreateCommandBlock())
            {
                block.ReleaseDSPNode(m_NoiseFilter);
                block.ReleaseDSPNode(m_LowPass);
            }
        }

        private void OnGUI()
        {
            using (DSPCommandBlock block = m_Graph.CreateCommandBlock())
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(100, 70, 300, 30), "Lowpass Cutoff:");
                float newCutoff = GUI.HorizontalSlider(new Rect(100, 100, 300, 30), Cutoff, 10.0f, 22000.0f);
                if (Math.Abs(newCutoff - Cutoff) > 0.01f)
                {
                    block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
                        (m_LowPass, AudioKernel.Parameters.Cutoff, newCutoff);
                    Cutoff = newCutoff;
                }

                GUI.Label(new Rect(100, 160, 300, 30), "Lowpass Q:");
                float newq = GUI.HorizontalSlider(new Rect(100, 190, 300, 30), Q, 1.0f, 100.0f);
                if (Math.Abs(newq - Q) > 0.01f)
                {
                    block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
                        (m_LowPass, AudioKernel.Parameters.Q, newq);
                    Q = newq;
                }

                GUI.Label(new Rect(100, 250, 300, 30), "Gain in dB:");
                float newGain = GUI.HorizontalSlider(new Rect(100, 280, 300, 30), Gain, -80.0f, 0.0f);
                if (Math.Abs(newGain - Gain) > 0.01f)
                {
                    block.SetFloat<AudioKernel.Parameters, AudioKernel.SampleProviders, AudioKernel>
                        (m_LowPass, AudioKernel.Parameters.GainInDBs, newGain);
                    Gain = newGain;
                }
            }
        }
    }
}