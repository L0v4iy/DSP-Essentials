using DSPGraphAudio.Kernel.Systems;
using Unity.Entities;
using UnityEngine;

namespace MonoComponents
{
    public class AudioNode : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;

        private void Start()
        {
            if (clip == null)
            {
                Debug.Log("No clip assigned, not playing (" + gameObject.name + ")");
                return;
            }

            World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<AudioSystem>()
                .PlayOneShot(clip, transform.position);
        }
    }
}