using DSPGraphAudio.Kernel.Systems;
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

        public void PlaySpatialized()
        {
            // U're at center
            Vector3 relativeVector = transform.position;
            Debug.Log($"relativeVector : {relativeVector}");
            
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<AudioSystem>()
                .PlayClipInWorld(clipToPlay);
        }
    }
}