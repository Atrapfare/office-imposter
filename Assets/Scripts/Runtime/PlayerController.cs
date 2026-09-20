using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeImposter
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStatus))]
    [RequireComponent(typeof(WorkTaskRunner))]
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] float walkSpeed = 3.4f;
        [SerializeField] float sprintSpeed = 6.2f;
        [SerializeField] float rotationLerp = 14f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float interactRange = 2.6f;

        public static PlayerController Local { get; private set; }

        CharacterController _controller;
        PlayerStatus _status;
        WorkTaskRunner _task;
        ThirdPersonCamera _camera;
        float _fallVelocity;
        float _stepDistance;

        public WorkStation NearbyStation { get; private set; }
        public bool IsSprinting { get; private set; }
        public WorkTaskRunner Task => _task;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _status = GetComponent<PlayerStatus>();
            _task = GetComponent<WorkTaskRunner>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            Local = this;
            MoveToSpawnPoint();

            if (Camera.main != null)
            {
                _camera = Camera.main.GetComponent<ThirdPersonCamera>();
                if (_camera != null) _camera.SetTarget(transform);
            }
            CursorLock.Lock();
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
        }

        // The owner places itself: this transform is owner-authoritative, so a
        // server-side reposition would be overwritten by the next owner update.
        void MoveToSpawnPoint()
        {
            if (!SpawnPoint.TryGet(OwnerClientId, out Vector3 position, out Quaternion rotation)) return;

            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
        }

        void Update()
        {
            if (!IsOwner) return;

            // The HUD owns Escape: it opens the pause menu and frees the cursor.
            Keyboard keyboard = NetworkHUD.IsPaused ? null : Keyboard.current;

            UpdateInteraction(keyboard);
            UpdateMovement(keyboard);
        }

        void UpdateInteraction(Keyboard keyboard)
        {
            NearbyStation = WorkStation.FindNearest(transform.position, interactRange);
            if (keyboard == null) return;

            if (TryAnswerMeetingPrompt(keyboard)) return;

            if (_task.IsActive)
            {
                bool lostDesk = NearbyStation == null || _status.IsCaught;
                if (lostDesk || keyboard.eKey.wasPressedThisFrame) _task.Cancel();
                return;
            }

            if (NearbyStation != null && !_status.IsCaught && keyboard.eKey.wasPressedThisFrame) _task.Begin();
        }

        bool TryAnswerMeetingPrompt(Keyboard keyboard)
        {
            var meeting = MeetingSystem.Instance;
            if (meeting == null || meeting.CurrentPrompt == MeetingSystem.PromptAction.None) return false;

            int action = 0;
            if (keyboard.spaceKey.wasPressedThisFrame) action = (int)MeetingSystem.PromptAction.Nod;
            else if (keyboard.eKey.wasPressedThisFrame) action = (int)MeetingSystem.PromptAction.Agree;
            else if (keyboard.fKey.wasPressedThisFrame) action = (int)MeetingSystem.PromptAction.TakeNotes;

            if (action == 0) return false;

            meeting.RespondServerRpc(action);
            return true;
        }

        void UpdateMovement(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            IsSprinting = false;

            bool canMove = keyboard != null && !_status.IsWorking && !_status.IsCaught;
            if (canMove)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                IsSprinting = keyboard.leftShiftKey.isPressed && input != Vector2.zero;
            }

            Transform reference = _camera != null ? _camera.transform : null;
            Vector3 forward = reference != null
                ? Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized
                : Vector3.forward;
            Vector3 right = reference != null
                ? Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized
                : Vector3.right;

            Vector3 move = forward * input.y + right * input.x;
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, rotationLerp * Time.deltaTime);
            }

            if (_controller.isGrounded && _fallVelocity < 0f) _fallVelocity = -2f;
            _fallVelocity += gravity * Time.deltaTime;

            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 velocity = move * speed + Vector3.up * _fallVelocity;
            _controller.Move(velocity * Time.deltaTime);

            if (move.sqrMagnitude > 0.01f && _controller.isGrounded)
            {
                _stepDistance += speed * Time.deltaTime;
                if (_stepDistance >= 2.1f)
                {
                    _stepDistance = 0f;
                    AudioDirector.Footstep();
                }
            }
        }
    }
}
