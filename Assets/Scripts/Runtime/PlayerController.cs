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
        [SerializeField] float walkSpeed = 3.2f;
        [SerializeField] float sprintSpeed = 6.0f;
        [SerializeField] float crouchSpeed = 1.5f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float interactRange = 3.2f;
        [SerializeField] float interactAngle = 42f;
        [SerializeField] float standEyeHeight = 1.62f;
        [SerializeField] float crouchEyeHeight = 1.05f;
        [SerializeField] Transform head;

        public static PlayerController Local { get; private set; }

        CharacterController _controller;
        PlayerStatus _status;
        WorkTaskRunner _task;
        FirstPersonLook _look;
        Seat _seat;

        float _fallVelocity;
        float _stepDistance;
        float _yaw;
        float _seatYaw;
        bool _crouching;

        public Interactable Focus { get; private set; }
        public bool IsSeated => _seat != null;
        public bool IsCrouching => _crouching;
        public bool IsSprinting { get; private set; }
        public float PlanarSpeed { get; private set; }
        public WorkTaskRunner Task => _task;
        public PlayerStatus Status => _status;
        public Transform Head => head;

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
            _yaw = transform.eulerAngles.y;

            AttachCamera();
            HideOwnHead();
            CursorLock.Lock();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            if (_seat != null) _seat.Release(this);
            if (Local == this) Local = null;
        }

        void AttachCamera()
        {
            if (Camera.main == null || head == null) return;

            Transform camera = Camera.main.transform;
            camera.SetParent(head, false);
            camera.localPosition = Vector3.zero;
            camera.localRotation = Quaternion.identity;

            _look = Camera.main.GetComponent<FirstPersonLook>();
            if (_look == null) _look = Camera.main.gameObject.AddComponent<FirstPersonLook>();
            _look.ResetPitch();
        }

        // In first person the player's own head would fill the view from the inside.
        void HideOwnHead()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Head" || renderer.name == "Nose") renderer.enabled = false;
            }
        }

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

            Keyboard keyboard = HudController.IsPaused ? null : Keyboard.current;

            UpdateLookYaw();
            UpdateInteraction(keyboard);
            UpdateMovement(keyboard);
            UpdateHead();
        }

        void UpdateLookYaw()
        {
            if (!CursorLock.IsLocked || HudController.IsPaused || Mouse.current == null) return;

            _yaw += Mouse.current.delta.ReadValue().x * FirstPersonLook.Sensitivity;

            // Seated you can swivel, but not spin around to stare at the wall behind you.
            if (_seat != null)
            {
                float half = _seat.YawRange * 0.5f;
                float delta = Mathf.DeltaAngle(_seatYaw, _yaw);
                _yaw = _seatYaw + Mathf.Clamp(delta, -half, half);
            }

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        void UpdateInteraction(Keyboard keyboard)
        {
            Vector3 eye = head != null ? head.position : transform.position + Vector3.up * standEyeHeight;
            Vector3 forward = Camera.main != null && Camera.main.transform.IsChildOf(transform)
                ? Camera.main.transform.forward
                : transform.forward;

            Focus = _task.IsActive ? null : Interactable.FindBest(eye, forward, interactRange, interactAngle);

            if (keyboard == null) return;
            if (TryAnswerMeetingPrompt(keyboard)) return;
            if (TryAnswerConfrontation(keyboard)) return;

            if (_task.IsActive)
            {
                if (keyboard.eKey.wasPressedThisFrame || _status.IsCaught) _task.Cancel();
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (_seat != null && Focus == null) StandUp();
                else if (Focus != null) Focus.Interact(this);
            }
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

        bool TryAnswerConfrontation(Keyboard keyboard)
        {
            var boss = BossAI.Instance;
            if (boss == null || !_status.IsConfronted) return false;

            int answer = 0;
            if (keyboard.digit1Key.wasPressedThisFrame) answer = 1;
            else if (keyboard.digit2Key.wasPressedThisFrame) answer = 2;
            else if (keyboard.digit3Key.wasPressedThisFrame) answer = 3;

            if (answer == 0) return false;

            _status.AnswerConfrontationServerRpc(answer);
            return true;
        }

        public void SitOn(Seat seat)
        {
            if (seat == null || !seat.TryClaim(this)) return;

            _seat = seat;
            _crouching = false;

            _controller.enabled = false;
            transform.SetPositionAndRotation(seat.Position, seat.Rotation);
            _controller.enabled = true;

            _seatYaw = seat.Rotation.eulerAngles.y;
            _yaw = _seatYaw;
        }

        public void StandUp()
        {
            if (_seat == null) return;

            if (_task.IsActive) _task.Cancel();
            _seat.Release(this);
            _seat = null;

            // Step clear of the chair so the player is not left inside the desk.
            _controller.enabled = false;
            transform.position += transform.forward * -0.7f;
            _controller.enabled = true;
        }

        void UpdateMovement(Keyboard keyboard)
        {
            if (_seat != null)
            {
                PlanarSpeed = 0f;
                return;
            }

            Vector2 input = Vector2.zero;
            IsSprinting = false;

            bool canMove = keyboard != null && !_status.IsWorking && !_status.IsCaught && !_status.IsConfronted;
            if (canMove)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;

                if (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame) _crouching = !_crouching;
                IsSprinting = keyboard.leftShiftKey.isPressed && !_crouching && input != Vector2.zero
                              && _status.Energy.Value > 5f;
            }

            Vector3 move = transform.forward * input.y + transform.right * input.x;
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = _crouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            speed *= _status.SpeedMultiplier;

            if (_controller.isGrounded && _fallVelocity < 0f) _fallVelocity = -2f;
            _fallVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move * speed + Vector3.up * _fallVelocity;
            _controller.Move(velocity * Time.deltaTime);

            PlanarSpeed = new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;
            UpdateFootsteps(move, speed);
        }

        void UpdateFootsteps(Vector3 move, float speed)
        {
            if (move.sqrMagnitude <= 0.01f || !_controller.isGrounded) return;

            _stepDistance += speed * Time.deltaTime;
            float stride = _crouching ? 2.6f : IsSprinting ? 1.7f : 2.1f;
            if (_stepDistance < stride) return;

            _stepDistance = 0f;
            AudioDirector.Footstep();

            // Running is loud: the boss notices it even without line of sight.
            if (IsSprinting) _status.ReportNoiseServerRpc(transform.position);
        }

        void UpdateHead()
        {
            if (head == null) return;

            float target = _seat != null ? _seat.EyeHeight : _crouching ? crouchEyeHeight : standEyeHeight;
            Vector3 local = head.localPosition;
            local.y = Mathf.Lerp(local.y, target, 12f * Time.deltaTime);
            head.localPosition = local;
        }
    }
}
