using System.Collections.Generic;
using UnityEngine;

namespace OfficeImposter
{
    public class SpawnPoint : MonoBehaviour
    {
        public static readonly List<SpawnPoint> All = new List<SpawnPoint>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static bool TryGet(ulong clientId, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (All.Count == 0) return false;

            SpawnPoint point = All[(int)(clientId % (ulong)All.Count)];
            if (point == null) return false;

            position = point.transform.position;
            rotation = point.transform.rotation;
            return true;
        }
    }
}
