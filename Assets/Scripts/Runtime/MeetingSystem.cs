using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace OfficeImposter
{
    // Scheduled meetings you have to show up to and look engaged in.
    // Being absent bleeds suspicion; missing an attention check spikes it.
    public class MeetingSystem : NetworkBehaviour
    {
        public enum Phase { None, Announced, Running }

        public enum PromptAction { None = 0, Nod = 1, Agree = 2, TakeNotes = 3 }

        public static MeetingSystem Instance { get; private set; }

        public static readonly Bounds RoomBounds = new Bounds(new Vector3(14f, 1.5f, 7.5f), new Vector3(12f, 4f, 15f));

        [SerializeField] float announceLeadSeconds = 20f;
        [SerializeField] float meetingDuration = 38f;
        [SerializeField] float promptInterval = 7.5f;
        [SerializeField] float promptWindow = 3f;
        [SerializeField] float absencePerSecond = 5.5f;
        [SerializeField] float missedPromptPenalty = 13f;
        [SerializeField] float hitPromptReward = 7f;
        [SerializeField] float seatRadius = 1.9f;

        readonly NetworkVariable<int> _phase = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> _phaseCountdown = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> _prompt = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> _promptCountdown = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly Queue<float> _schedule = new Queue<float>();
        readonly HashSet<ulong> _answered = new HashSet<ulong>();
        float _promptTimer;

        public Phase CurrentPhase => (Phase)_phase.Value;
        public float Countdown => _phaseCountdown.Value;
        public PromptAction CurrentPrompt => (PromptAction)_prompt.Value;
        public float PromptCountdown => _promptCountdown.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;

            // Two meetings per workday, expressed as time-remaining thresholds.
            _schedule.Enqueue(0.68f);
            _schedule.Enqueue(0.32f);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsServer) return;

            var game = GameManager.Instance;
            if (game == null || game.DayOver.Value)
            {
                if (CurrentPhase != Phase.None) EndMeeting();
                return;
            }

            switch (CurrentPhase)
            {
                case Phase.None:
                    CheckSchedule(game);
                    break;
                case Phase.Announced:
                    TickPhase();
                    if (_phaseCountdown.Value <= 0f) StartMeeting();
                    break;
                case Phase.Running:
                    TickPhase();
                    RunMeeting();
                    if (_phaseCountdown.Value <= 0f) EndMeeting();
                    break;
            }
        }

        void TickPhase()
        {
            _phaseCountdown.Value = Mathf.Max(0f, _phaseCountdown.Value - Time.deltaTime);
        }

        void CheckSchedule(GameManager game)
        {
            if (_schedule.Count == 0) return;

            float progress = game.TimeRemaining.Value / Mathf.Max(1f, game.WorkdaySeconds);
            if (progress > _schedule.Peek()) return;

            _schedule.Dequeue();
            _phase.Value = (int)Phase.Announced;
            _phaseCountdown.Value = announceLeadSeconds;
            Debug.Log("[Meeting] announced");
        }

        void StartMeeting()
        {
            _phase.Value = (int)Phase.Running;
            _phaseCountdown.Value = meetingDuration;
            _promptTimer = promptInterval * 0.6f;
            Debug.Log("[Meeting] started");

            Vector3 head = new Vector3(15f, 0f, 11.5f);
            if (BossAI.Instance != null) BossAI.Instance.GoToMeeting(head);

            int index = 0;
            foreach (var coworker in CoworkerAI.All)
            {
                if (coworker == null) continue;
                coworker.SendToMeeting(new Vector3(13.4f + index % 3 * 1.4f, 0f, index < 3 ? 5.9f : 9.1f));
                index++;
            }
        }

        void RunMeeting()
        {
            foreach (var player in PlayerStatus.All)
            {
                if (player == null || player.IsCaught) continue;

                bool present = RoomBounds.Contains(player.transform.position);
                bool seated = present && MeetingSeat.IsSomeoneSeated(player.transform.position, seatRadius);

                player.SetSeated(seated);
                if (!present) player.AddSuspicion(absencePerSecond * Time.deltaTime);
            }

            UpdatePrompt();
        }

        void UpdatePrompt()
        {
            if (_prompt.Value != 0)
            {
                _promptCountdown.Value -= Time.deltaTime;
                if (_promptCountdown.Value > 0f) return;

                PunishNonResponders();
                _prompt.Value = 0;
                _promptCountdown.Value = 0f;
                _promptTimer = promptInterval;
                return;
            }

            _promptTimer -= Time.deltaTime;
            if (_promptTimer > 0f) return;
            if (_phaseCountdown.Value < promptWindow + 1f) return;

            _answered.Clear();
            _prompt.Value = Random.Range(1, 4);
            _promptCountdown.Value = promptWindow;
            Debug.Log($"[Meeting] prompt {_prompt.Value}");
        }

        void PunishNonResponders()
        {
            foreach (var player in PlayerStatus.All)
            {
                if (player == null || player.IsCaught) continue;
                if (!RoomBounds.Contains(player.transform.position)) continue;
                if (_answered.Contains(player.OwnerClientId)) continue;

                player.AddSuspicion(missedPromptPenalty);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RespondServerRpc(int action, ServerRpcParams rpcParams = default)
        {
            if (CurrentPhase != Phase.Running || _prompt.Value == 0) return;
            if (action != _prompt.Value) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            if (!_answered.Add(sender)) return;

            PlayerStatus status = FindPlayer(sender);
            if (status != null) status.AddSuspicion(-hitPromptReward);
        }

        static PlayerStatus FindPlayer(ulong clientId)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null) return null;
            if (!manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return null;
            return client.PlayerObject != null ? client.PlayerObject.GetComponent<PlayerStatus>() : null;
        }

        void EndMeeting()
        {
            _phase.Value = (int)Phase.None;
            _phaseCountdown.Value = 0f;
            _prompt.Value = 0;
            _promptCountdown.Value = 0f;

            foreach (var player in PlayerStatus.All)
            {
                if (player != null) player.SetSeated(false);
            }

            if (BossAI.Instance != null) BossAI.Instance.EndMeeting();
            foreach (var coworker in CoworkerAI.All)
            {
                if (coworker != null) coworker.LeaveMeeting();
            }
        }

        public static string DescribePrompt(PromptAction action)
        {
            switch (action)
            {
                case PromptAction.Nod: return "Nicken  [LEERTASTE]";
                case PromptAction.Agree: return "Zustimmen  [E]";
                case PromptAction.TakeNotes: return "Notizen machen  [F]";
                default: return string.Empty;
            }
        }
    }
}
