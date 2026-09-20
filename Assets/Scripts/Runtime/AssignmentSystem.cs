using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace OfficeImposter
{
    // Hands out small errands with deadlines. This is what stops the workday from
    // being a waiting game: there is always somewhere you are supposed to be.
    public class AssignmentSystem : NetworkBehaviour
    {
        public static AssignmentSystem Instance { get; private set; }

        [SerializeField] float firstJobDelay = 20f;
        [SerializeField] float betweenJobs = 16f;
        [SerializeField] float jobDuration = 55f;
        [SerializeField] float completeReward = 22f;
        [SerializeField] float missPenalty = 24f;

        readonly Dictionary<PlayerStatus, float> _cooldowns = new Dictionary<PlayerStatus, float>();

        public override void OnNetworkSpawn()
        {
            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsServer) return;

            var game = GameManager.Instance;
            if (game == null || game.DayOver.Value) return;

            bool meetingRunning = MeetingSystem.Instance != null
                                  && MeetingSystem.Instance.CurrentPhase == MeetingSystem.Phase.Running;

            foreach (var player in PlayerStatus.All)
            {
                if (player == null || player.IsCaught) continue;

                if (player.HasJob)
                {
                    TickDeadline(player);
                    continue;
                }

                if (meetingRunning) continue;

                if (!_cooldowns.TryGetValue(player, out float remaining)) remaining = firstJobDelay;
                remaining -= Time.deltaTime;

                if (remaining <= 0f) Assign(player);
                else _cooldowns[player] = remaining;
            }
        }

        void TickDeadline(PlayerStatus player)
        {
            player.JobDeadline.Value -= Time.deltaTime;
            if (player.JobDeadline.Value > 0f) return;

            player.AddSuspicion(missPenalty, SuspicionReason.MissedDeadline);
            player.CountJob(false);
            Clear(player);
        }

        void Assign(PlayerStatus player)
        {
            var kind = (JobKind)Random.Range(1, 4);
            player.JobKind.Value = (int)kind;
            player.JobStage.Value = 1;
            player.JobDeadline.Value = jobDuration;
            _cooldowns[player] = betweenJobs;
        }

        void Clear(PlayerStatus player)
        {
            player.JobKind.Value = 0;
            player.JobStage.Value = 0;
            player.JobDeadline.Value = 0f;
            player.SetCarrying(false);
            _cooldowns[player] = betweenJobs;
        }

        // Called from JobStation on the server when a player uses a station.
        public bool TryAdvance(PlayerStatus player, StationKind station)
        {
            if (!IsServer || player == null || !player.HasJob) return false;

            var kind = (JobKind)player.JobKind.Value;
            int stage = player.JobStage.Value;

            if (kind == JobKind.FileDocuments && station == StationKind.Cabinet)
            {
                Complete(player);
                return true;
            }

            if (stage == 1)
            {
                bool matches = (kind == JobKind.PrintReport && station == StationKind.Printer)
                               || (kind == JobKind.FetchCoffee && station == StationKind.Coffee);
                if (!matches) return false;

                player.JobStage.Value = 2;
                player.SetCarrying(true);
                return true;
            }

            if (stage == 2 && station == StationKind.Delivery)
            {
                Complete(player);
                return true;
            }

            return false;
        }

        void Complete(PlayerStatus player)
        {
            player.AddSuspicion(-completeReward);
            player.CountJob(true);
            Clear(player);
        }
    }
}
