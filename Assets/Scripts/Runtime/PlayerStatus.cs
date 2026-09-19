using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace OfficeImposter
{
    public class PlayerStatus : NetworkBehaviour
    {
        public const float MaxSuspicion = 100f;

        public static readonly List<PlayerStatus> All = new List<PlayerStatus>();
        public static PlayerStatus Local { get; private set; }

        public readonly NetworkVariable<float> Suspicion = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> Working = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> Caught = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] float workRecoveryPerSecond = 16f;
        [SerializeField] float slackDriftPerSecond = 1.2f;

        public bool IsWorking => Working.Value;
        public bool IsCaught => Caught.Value;
        public float SuspicionNormalized => Mathf.Clamp01(Suspicion.Value / MaxSuspicion);

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsOwner) Local = this;
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }

        [ServerRpc]
        public void SetWorkingServerRpc(bool working)
        {
            Working.Value = !Caught.Value && working;
        }

        void Update()
        {
            if (!IsServer || Caught.Value) return;

            // Looking busy buys suspicion back; loitering slowly gives you away even unseen.
            float delta = Working.Value
                ? -workRecoveryPerSecond * Time.deltaTime
                : slackDriftPerSecond * Time.deltaTime;

            AddSuspicion(delta);
        }

        public void AddSuspicion(float amount)
        {
            if (!IsServer || Caught.Value) return;

            Suspicion.Value = Mathf.Clamp(Suspicion.Value + amount, 0f, MaxSuspicion);
            if (Suspicion.Value >= MaxSuspicion)
            {
                Caught.Value = true;
                Working.Value = false;
            }
        }
    }
}
