using System.Collections.Generic;
using UnityEngine;

namespace OfficeImposter
{
    // Anything the player can look at and press E on. Selection is gaze-based rather
    // than proximity-based, which is what first person needs.
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public abstract string Prompt { get; }
        public virtual bool IsAvailable => true;
        public virtual Vector3 FocusPoint => transform.position;

        public abstract void Interact(PlayerController player);

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public static Interactable FindBest(Vector3 eye, Vector3 forward, float range, float maxAngle)
        {
            Interactable best = null;
            float bestScore = float.MaxValue;

            foreach (var candidate in All)
            {
                if (candidate == null || !candidate.IsAvailable) continue;

                Vector3 offset = candidate.FocusPoint - eye;
                float distance = offset.magnitude;
                if (distance > range || distance < 0.01f) continue;

                float angle = Vector3.Angle(forward, offset / distance);
                if (angle > maxAngle) continue;

                // Angle dominates so the thing you are actually looking at wins.
                float score = angle + distance * 6f;
                if (score >= bestScore) continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }
    }
}
