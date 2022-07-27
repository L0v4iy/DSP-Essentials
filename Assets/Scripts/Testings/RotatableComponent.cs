using Unity.Entities;

namespace Testings
{
    [GenerateAuthoringComponent]
    public struct RotatableComponent : IComponentData
    {
        public float CurrentAngle;
        public float Speed;
    }
}