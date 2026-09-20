using UnityEngine;

namespace OfficeImposter
{
    // Shared line-of-sight test for anyone who can catch the player slacking.
    public class VisionCone : MonoBehaviour
    {
        [SerializeField] float viewDistance = 13f;
        [SerializeField] float viewAngle = 80f;
        [SerializeField] float eyeHeight = 1.6f;

        public float ViewDistance => viewDistance;

        public void Configure(float distance, float angle)
        {
            viewDistance = distance;
            viewAngle = angle;
        }

        public bool CanSee(Transform target, float targetHeight = 1.2f)
        {
            if (target == null) return false;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 offset = target.position + Vector3.up * targetHeight - eye;
            float distance = offset.magnitude;

            if (distance > viewDistance || distance < 0.01f) return false;
            if (Vector3.Angle(transform.forward, offset / distance) > viewAngle * 0.5f) return false;

            foreach (var hit in Physics.RaycastAll(eye, offset / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(target)) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                return false;
            }

            return true;
        }
    }
}
