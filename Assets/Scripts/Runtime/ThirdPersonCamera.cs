using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeImposter
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] float distance = 4.6f;
        [SerializeField] float focusHeight = 1.5f;
        public static float Sensitivity = 0.12f;
        [SerializeField] float minPitch = -20f;
        [SerializeField] float maxPitch = 42f;
        // Above the ceiling the room's single-sided ceiling is culled away and the
        // player would stare into empty space, so the rig stays below it.
        [SerializeField] float maxHeight = 3.1f;

        Transform _target;
        float _yaw;
        float _pitch = 14f;

        public float Yaw => _yaw;

        public void SetTarget(Transform target)
        {
            _target = target;
            if (target != null) _yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (_target == null) return;

            if (CursorLock.IsLocked && !NetworkHUD.IsPaused && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * Sensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * Sensitivity, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focus = _target.position + Vector3.up * focusHeight;
            Vector3 desired = focus - rotation * Vector3.forward * distance;

            Vector3 resolved = ResolveWallClipping(focus, desired);
            resolved.y = Mathf.Min(resolved.y, maxHeight);

            transform.position = resolved;
            transform.rotation = rotation;
        }

        // Pull the camera in front of whatever geometry is between it and the player,
        // ignoring the player's own collider.
        Vector3 ResolveWallClipping(Vector3 focus, Vector3 desired)
        {
            Vector3 direction = desired - focus;
            float length = direction.magnitude;
            if (length < 0.01f) return desired;

            RaycastHit[] hits = Physics.RaycastAll(focus, direction / length, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = length;
            bool blocked = false;

            foreach (var hit in hits)
            {
                if (_target != null && hit.collider.transform.IsChildOf(_target)) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    blocked = true;
                }
            }

            return blocked ? focus + direction / length * Mathf.Max(nearest - 0.25f, 0.4f) : desired;
        }
    }
}
