using System.Collections.Generic;
using UnityEngine;

namespace OfficeImposter
{
    [RequireComponent(typeof(Seat))]
    public class WorkStation : Interactable
    {
        public static readonly List<WorkStation> All = new List<WorkStation>();

        [SerializeField] string label = "Schreibtisch";
        [SerializeField] Transform standAnchor;
        [SerializeField] Transform focus;

        Seat _seat;
        CoworkerAI _occupant;

        public string Label => label;
        public bool IsOccupied => _occupant != null;
        public Vector3 StandPosition => standAnchor != null ? standAnchor.position : transform.position;

        public override Vector3 FocusPoint => focus != null ? focus.position : transform.position + Vector3.up;
        public override bool IsAvailable => !IsOccupied && !(_seat != null && _seat.IsTaken);
        public override string Prompt => $"[E]   an {label} setzen und so tun als ob";

        void Awake() => _seat = GetComponent<Seat>();

        public void Configure(string newLabel, Transform anchor, Transform focusPoint)
        {
            label = newLabel;
            standAnchor = anchor;
            focus = focusPoint;
        }

        public override void Interact(PlayerController player)
        {
            if (_seat == null) _seat = GetComponent<Seat>();
            if (_seat == null) return;

            player.SitOn(_seat);
            player.Task.Begin();
        }

        public void Claim(CoworkerAI coworker) => _occupant = coworker;

        public void Release(CoworkerAI coworker)
        {
            if (_occupant == coworker) _occupant = null;
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

        public static WorkStation FindNearest(Vector3 position, float maxDistance)
        {
            WorkStation best = null;
            float bestSqr = maxDistance * maxDistance;

            foreach (var station in All)
            {
                if (station == null || station.IsOccupied) continue;

                float sqr = (station.StandPosition - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = station;
                }
            }

            return best;
        }
    }
}
