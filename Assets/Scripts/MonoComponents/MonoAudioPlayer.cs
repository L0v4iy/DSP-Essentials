using DSPGraphAudio.Kernel;
using DSPGraphAudio.Kernel.Systems;
using Unity.Audio;
using Unity.Entities;
using UnityEngine;

namespace MonoComponents
{
    public class MonoAudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip clipToPlay;


        public void PlayAudioClip()
        {
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<AudioSystem>().PlayClipInHead(clipToPlay);
        }
        
    }
}