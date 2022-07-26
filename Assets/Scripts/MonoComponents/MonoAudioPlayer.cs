﻿using DSPGraphAudio.Kernel.Systems;
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
            Vector3 relativeVector = Camera.main.transform.position - transform.position;
            Debug.Log($"relativeVector : {relativeVector}");
            
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<AudioSystem>()
                .PlayClipInWorld(clipToPlay, relativeVector);
        }
    }
}