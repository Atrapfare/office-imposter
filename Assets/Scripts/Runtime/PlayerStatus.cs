using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace OfficeImposter
{
    public enum SuspicionReason
    {
        None = 0,
        SeenByBoss = 1,
        ReportedByCoworker = 2,
        MissedMeeting = 3,
        MissedPrompt = 4,
        MissedDeadline = 5,
        Typo = 6,
        BadAnswer = 7,
        Loitering = 8,
        Exhausted = 9,
    }

    public class PlayerStatus : NetworkBehaviour
    {
        public const float MaxSuspicion = 100f;
        public const float MaxEnergy = 100f;

        public static readonly List<PlayerStatus> All = new List<PlayerStatus>();
        public static PlayerStatus Local { get; private set; }

        public readonly NetworkVariable<float> Suspicion = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> Energy = new NetworkVariable<float>(
            MaxEnergy, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> Working = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> Caught = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> SeatedInMeeting = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> Watched = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> Reason = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> ConfrontQuestion = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> ConfrontTimer = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> TasksDone = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> JobsDone = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> JobsMissed = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> Carrying = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> JobKind = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> JobStage = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> JobDeadline = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] float workRecoveryPerSecond = 4f;
        [SerializeField] float slackDriftPerSecond = 1.1f;
        [SerializeField] float energyDrainPerSecond = 0.42f;
        [SerializeField] float energyDrainWorking = 0.85f;
        [SerializeField] float exhaustedDriftBonus = 1.3f;
        [SerializeField] float seenMemorySeconds = 0.6f;
        [SerializeField] float confrontSeconds = 9f;
        // Observers stack: five colleagues plus the boss all looking at once used to
        // burn 0 to 100 in seconds. Their combined pressure is capped instead.
        [SerializeField] float maxObservationPerSecond = 7f;

        float _seenTimer;
        float _reasonTimer;
        float _observationRate;
        SuspicionReason _observationReason = SuspicionReason.None;
        int _correctAnswer;

        public bool IsWorking => Working.Value;
        public bool IsCaught => Caught.Value;
        public bool IsSeated => SeatedInMeeting.Value;
        public bool IsWatched => Watched.Value;
        public bool IsConfronted => ConfrontQuestion.Value >= 0;
        public bool IsExhausted => Energy.Value < 30f;
        public bool IsCarrying => Carrying.Value;
        public bool HasJob => JobKind.Value != 0;

        public bool IsExcusedFromWork => Working.Value || SeatedInMeeting.Value;
        public float SuspicionNormalized => Mathf.Clamp01(Suspicion.Value / MaxSuspicion);
        public float EnergyNormalized => Mathf.Clamp01(Energy.Value / MaxEnergy);
        public SuspicionReason LastReason => (SuspicionReason)Reason.Value;

        // Running on empty makes you visibly sluggish.
        public float SpeedMultiplier => IsExhausted ? Mathf.Lerp(0.72f, 1f, Energy.Value / 30f) : 1f;

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

        public void SetCarrying(bool carrying)
        {
            if (!IsServer) return;
            Carrying.Value = carrying;
        }

        public void AddEnergy(float amount)
        {
            if (!IsServer) return;
            Energy.Value = Mathf.Clamp(Energy.Value + amount, 0f, MaxEnergy);
        }

        public void CountTask()
        {
            if (IsServer) TasksDone.Value++;
        }

        public void CountJob(bool completed)
        {
            if (!IsServer) return;
            if (completed) JobsDone.Value++;
            else JobsMissed.Value++;
        }

        // Observers accumulate pressure here; PlayerStatus applies it once, capped.
        public void ReportObservation(float ratePerSecond, SuspicionReason reason)
        {
            if (!IsServer) return;

            _observationRate += ratePerSecond;
            if (reason == SuspicionReason.SeenByBoss || _observationReason == SuspicionReason.None)
            {
                _observationReason = reason;
            }
        }

        public void MarkSeenByBoss()
        {
            if (!IsServer) return;
            _seenTimer = seenMemorySeconds;
            if (!Watched.Value) Watched.Value = true;
        }

        [ServerRpc]
        public void UseStationServerRpc(int stationKind)
        {
            if (Caught.Value) return;

            var kind = (StationKind)stationKind;
            // Energy values live here rather than on the station so a client cannot ask
            // for an arbitrary amount.
            if (kind == StationKind.Coffee) AddEnergy(45f);
            else if (kind == StationKind.WaterCooler) AddEnergy(12f);

            if (AssignmentSystem.Instance != null) AssignmentSystem.Instance.TryAdvance(this, kind);
        }

        [ServerRpc]
        public void ReportNoiseServerRpc(Vector3 position)
        {
            if (BossAI.Instance != null) BossAI.Instance.HearNoise(position, this);
        }

        public void BeginConfrontation()
        {
            if (!IsServer || IsConfronted || Caught.Value) return;

            int index = Random.Range(0, ConfrontationBank.Count);
            _correctAnswer = ConfrontationBank.Get(index).Correct;
            ConfrontQuestion.Value = index;
            ConfrontTimer.Value = confrontSeconds;
            Working.Value = false;
        }

        [ServerRpc]
        public void AnswerConfrontationServerRpc(int answer)
        {
            if (!IsConfronted) return;

            if (answer == _correctAnswer) AddSuspicion(-18f);
            else AddSuspicion(26f, SuspicionReason.BadAnswer);

            EndConfrontation();
        }

        void EndConfrontation()
        {
            ConfrontQuestion.Value = -1;
            ConfrontTimer.Value = 0f;
            if (BossAI.Instance != null) BossAI.Instance.EndConfrontation();
        }

        void Update()
        {
            if (!IsServer) return;

            if (_seenTimer > 0f)
            {
                _seenTimer -= Time.deltaTime;
                if (_seenTimer <= 0f && Watched.Value) Watched.Value = false;
            }

            if (_reasonTimer > 0f)
            {
                _reasonTimer -= Time.deltaTime;
                if (_reasonTimer <= 0f && Reason.Value != 0) Reason.Value = 0;
            }

            if (Caught.Value) return;

            if (IsConfronted)
            {
                ConfrontTimer.Value -= Time.deltaTime;
                if (ConfrontTimer.Value <= 0f)
                {
                    AddSuspicion(30f, SuspicionReason.BadAnswer);
                    EndConfrontation();
                }
                return;
            }

            Energy.Value = Mathf.Max(0f, Energy.Value -
                (Working.Value ? energyDrainWorking : energyDrainPerSecond) * Time.deltaTime);

            if (GameManager.GraceActive)
            {
                _observationRate = 0f;
                _observationReason = SuspicionReason.None;
                return;
            }

            if (_observationRate > 0f)
            {
                float capped = Mathf.Min(_observationRate, maxObservationPerSecond);
                AddSuspicion(capped * Time.deltaTime, _observationReason);
                _observationRate = 0f;
                _observationReason = SuspicionReason.None;
            }

            float delta;
            if (Working.Value) delta = -workRecoveryPerSecond * Time.deltaTime;
            else if (SeatedInMeeting.Value) delta = 0f;
            else delta = slackDriftPerSecond * Time.deltaTime;

            if (!Working.Value && !SeatedInMeeting.Value && IsExhausted)
            {
                delta += exhaustedDriftBonus * Time.deltaTime;
            }

            AddSuspicion(delta, delta > 0f ? SuspicionReason.Loitering : SuspicionReason.None);
        }

        public void AddSuspicion(float amount) => AddSuspicion(amount, SuspicionReason.None);

        public void AddSuspicion(float amount, SuspicionReason reason)
        {
            if (!IsServer || Caught.Value) return;

            // Only meaningful jumps are worth naming in the HUD; the slow drift is not.
            if (reason != SuspicionReason.None && amount >= 3f)
            {
                Reason.Value = (int)reason;
                _reasonTimer = 3.5f;
            }

            Suspicion.Value = Mathf.Clamp(Suspicion.Value + amount, 0f, MaxSuspicion);
            if (Suspicion.Value >= MaxSuspicion)
            {
                Caught.Value = true;
                Working.Value = false;
                ConfrontQuestion.Value = -1;
            }
        }

        public static string DescribeReason(SuspicionReason reason)
        {
            switch (reason)
            {
                case SuspicionReason.SeenByBoss: return "Der Chef hat dich beim Nichtstun gesehen";
                case SuspicionReason.ReportedByCoworker: return "Ein Kollege hat dich gemeldet";
                case SuspicionReason.MissedMeeting: return "Du fehlst im Meeting";
                case SuspicionReason.MissedPrompt: return "Du hast im Meeting nicht reagiert";
                case SuspicionReason.MissedDeadline: return "Auftrag nicht rechtzeitig erledigt";
                case SuspicionReason.Typo: return "Vertippt — das sieht jeder am Bildschirm";
                case SuspicionReason.BadAnswer: return "Deine Antwort war nicht überzeugend";
                case SuspicionReason.Exhausted: return "Du wirkst völlig übermüdet";
                case SuspicionReason.Loitering: return "Du stehst nur herum";
                default: return string.Empty;
            }
        }
    }
}
