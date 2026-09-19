using Unity.Netcode;
using UnityEngine;

namespace OfficeImposter
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] float workdaySeconds = 180f;

        public readonly NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> DayOver = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public float WorkdaySeconds => workdaySeconds;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                TimeRemaining.Value = workdaySeconds;
                DayOver.Value = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsServer || DayOver.Value) return;

            TimeRemaining.Value -= Time.deltaTime;
            if (TimeRemaining.Value <= 0f)
            {
                TimeRemaining.Value = 0f;
                DayOver.Value = true;
            }
        }
    }
}
