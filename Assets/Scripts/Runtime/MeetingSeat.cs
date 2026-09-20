using System.Collections.Generic;
using UnityEngine;

namespace OfficeImposter
{
    public class MeetingSeat : MonoBehaviour
    {
        public static readonly List<MeetingSeat> All = new List<MeetingSeat>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

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
