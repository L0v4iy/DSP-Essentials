using UnityEngine;

namespace MonoComponents
{
    public class FPSConfigurator : MonoBehaviour
    {
        [SerializeField] private int frameRate;
        private void Start()
        {
            Application.targetFrameRate = frameRate;
        }
    }
}