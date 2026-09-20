using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace OfficeImposter
{
    // Colleagues who actually do their job, and resent anyone who doesn't.
    // They raise suspicion gently on their own, but their real threat is fetching the boss.
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(VisionCone))]
    public class CoworkerAI : NetworkBehaviour
    {
        enum Mode { Working, Roaming, Reporting, Meeting }

        public static readonly List<CoworkerAI> All = new List<CoworkerAI>();

        [SerializeField] float suspicionPerSecond = 3.5f;
        [SerializeField] float annoyanceToReport = 3.2f;
        [SerializeField] float reportSuspicionSpike = 14f;
        [SerializeField] float workDurationMin = 12f;
        [SerializeField] float workDurationMax = 26f;
        [SerializeField] float roamDurationMax = 9f;

        NavMeshAgent _agent;
        VisionCone _vision;
        WorkStation _desk;
        Mode _mode = Mode.Roaming;
        float _modeTimer;
        readonly Dictionary<PlayerStatus, float> _annoyance = new Dictionary<PlayerStatus, float>();

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _vision = GetComponent<VisionCone>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            _agent.enabled = IsServer;
            if (IsServer) ChooseDesk();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            ReleaseDesk();
        }

        public void SendToMeeting(Vector3 position)
        {
            if (!IsServer) return;
            ReleaseDesk();
            _mode = Mode.Meeting;
            SetDestination(position);
        }

        public void LeaveMeeting()
        {
            if (!IsServer) return;
            ChooseDesk();
        }

        void Update()
        {
            if (!IsServer) return;

            _modeTimer -= Time.deltaTime;

            switch (_mode)
            {
                case Mode.Working:
                    FaceDesk();
                    if (_modeTimer <= 0f) StartRoaming();
                    break;
                case Mode.Roaming:
                    if (_modeTimer <= 0f || Arrived()) ChooseDesk();
                    break;
                case Mode.Reporting:
                    if (_modeTimer <= 0f || Arrived()) ChooseDesk();
                    break;
            }

            Observe();
        }

        void Observe()
        {
            foreach (var player in PlayerStatus.All)
            {
                if (player == null || player.IsCaught) continue;

                bool slacking = !player.IsExcusedFromWork && _vision.CanSee(player.transform);
                if (!slacking)
                {
                    if (_annoyance.ContainsKey(player)) _annoyance[player] = Mathf.Max(0f, _annoyance[player] - Time.deltaTime * 0.5f);
                    continue;
                }

                player.AddSuspicion(suspicionPerSecond * Time.deltaTime);

                _annoyance.TryGetValue(player, out float value);
                value += Time.deltaTime;
                _annoyance[player] = value;

                if (value >= annoyanceToReport && _mode != Mode.Meeting) ReportToBoss(player);
            }
        }

        void ReportToBoss(PlayerStatus player)
        {
            _annoyance[player] = 0f;
            player.AddSuspicion(reportSuspicionSpike);

            if (BossAI.Instance != null) BossAI.Instance.Investigate(player.transform.position);

            ReleaseDesk();
            _mode = Mode.Reporting;
            _modeTimer = 8f;
            SetDestination(player.transform.position);
        }

        void ChooseDesk()
        {
            ReleaseDesk();

            var free = new List<WorkStation>();
            foreach (var station in WorkStation.All)
            {
                if (station != null && !station.IsOccupied) free.Add(station);
            }

            if (free.Count == 0)
            {
                StartRoaming();
                return;
            }

            _desk = free[Random.Range(0, free.Count)];
            _desk.Claim(this);
            _mode = Mode.Working;
            _modeTimer = Random.Range(workDurationMin, workDurationMax);
            SetDestination(_desk.StandPosition);
        }

        void StartRoaming()
        {
            ReleaseDesk();
            _mode = Mode.Roaming;
            _modeTimer = Random.Range(3f, roamDurationMax);

            Vector3 target = new Vector3(Random.Range(-18f, 6f), 0f, Random.Range(-13f, 13f));
            SetDestination(target);
        }

        void FaceDesk()
        {
            if (_desk == null || !Arrived()) return;

            Vector3 toDesk = _desk.transform.position - transform.position;
            toDesk.y = 0f;
            if (toDesk.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toDesk), 3f * Time.deltaTime);
        }

        void ReleaseDesk()
        {
            if (_desk != null) _desk.Release(this);
            _desk = null;
        }

        bool Arrived()
        {
            if (!_agent.enabled || _agent.pathPending) return false;
            return _agent.remainingDistance <= _agent.stoppingDistance + 0.3f;
        }

        void SetDestination(Vector3 position)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }
    }
}
