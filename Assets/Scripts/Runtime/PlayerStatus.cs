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

        public readonly NetworkVariable<bool> SeatedInMeeting = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> Watched = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] float workRecoveryPerSecond = 5f;
        [SerializeField] float slackDriftPerSecond = 1.2f;
        [SerializeField] float seenMemorySeconds = 0.6f;

        float _seenTimer;

        public bool IsWorking => Working.Value;
        public bool IsCaught => Caught.Value;
        public bool IsSeated => SeatedInMeeting.Value;
        public bool IsWatched => Watched.Value;

        // Someone doing their job — or sitting in a meeting looking engaged — is not
        // a target for suspicion, whoever happens to be looking at them.
        public bool IsExcusedFromWork => Working.Value || SeatedInMeeting.Value;

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

        public void SetSeated(bool seated)
        {
            if (!IsServer) return;
            SeatedInMeeting.Value = seated;
        }

        public void MarkSeenByBoss()
        {
            if (!IsServer) return;
            _seenTimer = seenMemorySeconds;
            if (!Watched.Value) Watched.Value = true;
        }

        void Update()
        {
            if (!IsServer) return;

            if (_seenTimer > 0f)
            {
                _seenTimer -= Time.deltaTime;
                if (_seenTimer <= 0f && Watched.Value) Watched.Value = false;
            }

            if (Caught.Value) return;

            // Looking busy buys suspicion back; loitering gives you away even unseen.
            // A meeting seat holds the line without actively helping.
            float delta;
            if (Working.Value) delta = -workRecoveryPerSecond * Time.deltaTime;
            else if (SeatedInMeeting.Value) delta = 0f;
            else delta = slackDriftPerSecond * Time.deltaTime;

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
