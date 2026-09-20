using System.Collections.Generic;
using UnityEngine;

namespace OfficeImposter
{
    [RequireComponent(typeof(Seat))]
    public class MeetingSeat : Interactable
    {
        public static readonly List<MeetingSeat> All = new List<MeetingSeat>();

        [SerializeField] Transform focus;

        Seat _seat;

        public override Vector3 FocusPoint => focus != null ? focus.position : transform.position + Vector3.up * 0.6f;
        public override bool IsAvailable => _seat == null || !_seat.IsTaken;

        public override string Prompt
        {
            get
            {
                var meeting = MeetingSystem.Instance;
                bool running = meeting != null && meeting.CurrentPhase == MeetingSystem.Phase.Running;
                return running ? "[E]   hinsetzen und interessiert wirken" : "[E]   hinsetzen";
            }
        }

        void Awake() => _seat = GetComponent<Seat>();

        public void Configure(Transform focusPoint) => focus = focusPoint;

        public override void Interact(PlayerController player)
        {
            if (_seat == null) _seat = GetComponent<Seat>();
            if (_seat != null) player.SitOn(_seat);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            All.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            All.Remove(this);
        }

        public static bool IsSomeoneSeated(Vector3 position, float radius)
        {
            foreach (var seat in All)
            {
                if (seat == null) continue;
                if ((seat.transform.position - position).sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }
    }
}
