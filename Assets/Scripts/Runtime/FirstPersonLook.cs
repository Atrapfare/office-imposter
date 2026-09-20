using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeImposter
{
    // Pitch lives on the camera, yaw lives on the body: the standard first-person split.
    public class FirstPersonLook : MonoBehaviour
    {
        public static float Sensitivity = 0.12f;

        [SerializeField] float minPitch = -80f;
        [SerializeField] float maxPitch = 80f;
        [SerializeField] float bobAmount = 0.035f;
        [SerializeField] float bobSpeed = 9f;

        float _pitch;
        float _bobPhase;

        public void ResetPitch() => _pitch = 0f;

        void LateUpdate()
        {
            bool canLook = CursorLock.IsLocked && !HudController.IsPaused && Mouse.current != null;
            if (canLook)
            {
                float deltaY = Mouse.current.delta.ReadValue().y;
                _pitch = Mathf.Clamp(_pitch - deltaY * Sensitivity, minPitch, maxPitch);
            }

            float bob = 0f;
            var player = PlayerController.Local;
            if (player != null && player.PlanarSpeed > 0.3f)
            {
                _bobPhase += Time.deltaTime * bobSpeed * Mathf.Clamp01(player.PlanarSpeed / 3.4f);
                bob = Mathf.Sin(_bobPhase) * bobAmount;
            }
            else
            {
                _bobPhase = 0f;
            }

            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            transform.localPosition = new Vector3(0f, bob, 0f);
        }
    }
}
