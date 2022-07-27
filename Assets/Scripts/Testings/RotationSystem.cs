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
                    r.CurrentAngle += r.Speed * td;
                    rotation.Value = quaternion.RotateY(math.radians(r.CurrentAngle));
                })
                .Run();
        }
    }
}