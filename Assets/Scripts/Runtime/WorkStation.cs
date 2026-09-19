using System.Collections.Generic;
using UnityEngine;

namespace OfficeImposter
{
    public class WorkStation : MonoBehaviour
    {
        public static readonly List<WorkStation> All = new List<WorkStation>();

        [SerializeField] string label = "Schreibtisch";
        [SerializeField] Transform standAnchor;

        public string Label => label;
        public Vector3 StandPosition => standAnchor != null ? standAnchor.position : transform.position;

        public void Configure(string newLabel, Transform anchor)
        {
            label = newLabel;
            standAnchor = anchor;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static WorkStation FindNearest(Vector3 position, float maxDistance)
        {
            WorkStation best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (var station in All)
            {
                if (station == null) continue;
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
