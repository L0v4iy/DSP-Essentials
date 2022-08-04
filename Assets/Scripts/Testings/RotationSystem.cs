using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Testings
{
    public partial class RotationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float td = Time.DeltaTime;
            Entities.ForEach((Entity e, ref Rotation rotation, ref RotatableComponent r) =>
                {
                    r.CurrentAngle += r.Direction * r.Speed * td;
                    float rX = math.radians(r.CurrentAngle.x);
                    float rY = math.radians(r.CurrentAngle.y);
                    float rZ = math.radians(r.CurrentAngle.z);
                    rotation.Value = quaternion.Euler(rX, rY, rZ);
                })
                .Run();
        }
    }
}