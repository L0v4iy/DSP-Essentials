using Unity.Entities;
using Unity.Mathematics;

namespace Testings
{
    [GenerateAuthoringComponent]
    public struct RotatableComponent : IComponentData
    {
        public float3 CurrentAngle;
        public float3 Direction;
        public float Speed;
    }
}