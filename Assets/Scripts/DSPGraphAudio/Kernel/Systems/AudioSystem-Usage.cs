/*using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Common.Audio
{

        protected override void OnUpdate()
        {
            var audioListenerTranslation = audioListener.transform.position;

            Entities.ForEach(
                (Entity entity, ref OneShot oneShot, in Translation translation) =>
                {
                    // An enum id identifying the sound to be played.
                    var soundsId = oneShot.Value;

                    if (soundsId == Sounds.Id.None) return;

                    var audioClip = ... // Get AudioClip from soundsId.

                    // Play audio relative to the listener.
                    float3 relativeTranslation = translation.Value - (float3)audioListenerTranslation;
                    // Use memoized audioSystem to play one shot.
                    audioSystem.playOneShot(audioClip, relativeTranslation);
                }
            ).WithName("OneShotSystem").WithoutBurst().Run();
        }
}*/

