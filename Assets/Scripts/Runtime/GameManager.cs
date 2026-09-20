using Unity.Netcode;
using UnityEngine;

namespace OfficeImposter
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] float workdaySeconds = 180f;
        [SerializeField] float graceSeconds = 20f;

        public readonly NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> DayOver = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // You have just arrived: nobody is judging you yet. Without this the first
        // seconds are spent already losing, before the player has got their bearings.
        public readonly NetworkVariable<float> Grace = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public float WorkdaySeconds => workdaySeconds;
        public bool InGrace => Grace.Value > 0f;

        public static bool GraceActive => Instance != null && Instance.InGrace;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                TimeRemaining.Value = workdaySeconds;
                DayOver.Value = false;
                Grace.Value = graceSeconds;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsServer || DayOver.Value) return;

            if (Grace.Value > 0f) Grace.Value = Mathf.Max(0f, Grace.Value - Time.deltaTime);
            TimeRemaining.Value -= Time.deltaTime;
            if (TimeRemaining.Value <= 0f)
            {
                TimeRemaining.Value = 0f;
                DayOver.Value = true;
            }
        }
    }
}
