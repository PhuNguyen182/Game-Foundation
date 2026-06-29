using UnityEngine;

namespace DracoRuan.Utilities.CalculationExtensions
{
    public static class PhysicsExtensions
    {
        public static void ClampVelocity(this Rigidbody rigidbody, float maxMagnitude) =>
            rigidbody.maxLinearVelocity = maxMagnitude;

        public static Vector3 GetFlatVelocity(this Rigidbody rigidbody) =>
            (rigidbody.linearVelocity.x * Vector3.right).FasterAdd(rigidbody.linearVelocity.z * Vector3.forward);

        public static float GetSquaredFlatSpeed(this Rigidbody rigidbody)
        {
            Vector3 flatVelocity =
                (rigidbody.linearVelocity.x * Vector3.right).FasterAdd(rigidbody.linearVelocity.z * Vector3.forward);
            return flatVelocity.sqrMagnitude;
        }

        public static float GetSquaredSpeed(this Rigidbody rigidbody) => rigidbody.linearVelocity.sqrMagnitude;
    }
}
