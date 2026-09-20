using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace OfficeImposter
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(VisionCone))]
    public class BossAI : NetworkBehaviour
    {
        public enum State { Patrolling, Investigating, AtMeeting, Confronting }

        [SerializeField] float suspicionPerSecond = 6.5f;
        [SerializeField] float waypointPause = 2.5f;
        [SerializeField] float investigateDuration = 8f;
        [SerializeField] float confrontRange = 3.6f;
        [SerializeField] float hearingRange = 11f;
        [SerializeField] Transform[] waypoints;

        public static BossAI Instance { get; private set; }

        readonly NetworkVariable<int> _state = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        NavMeshAgent _agent;
        VisionCone _vision;
        PlayerStatus _confronting;
        int _waypointIndex;
        float _pauseTimer;
        float _stateTimer;

        public State CurrentState => (State)_state.Value;

        public void Configure(Transform[] patrolPoints) => waypoints = patrolPoints;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _vision = GetComponent<VisionCone>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _agent.enabled = IsServer;
            if (IsServer) MoveToNextWaypoint();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        public void Investigate(Vector3 position)
        {
            if (!IsServer || CurrentState == State.AtMeeting || CurrentState == State.Confronting) return;

            _state.Value = (int)State.Investigating;
            _stateTimer = investigateDuration;
            SetDestination(position);
        }

        // Sprinting carries. The boss does not see through walls, but he hears through them.
        public void HearNoise(Vector3 position, PlayerStatus source)
        {
            if (!IsServer || CurrentState != State.Patrolling) return;
            if ((position - transform.position).sqrMagnitude > hearingRange * hearingRange) return;

            Investigate(position);
        }

        public void GoToMeeting(Vector3 position)
        {
            if (!IsServer) return;

            AbortConfrontation();
            _state.Value = (int)State.AtMeeting;
            SetDestination(position);
        }

        public void EndMeeting()
        {
            if (!IsServer) return;

            _state.Value = (int)State.Patrolling;
            MoveToNextWaypoint();
        }

        public void EndConfrontation()
        {
            if (!IsServer) return;

            _confronting = null;
            if (CurrentState != State.AtMeeting)
            {
                _state.Value = (int)State.Patrolling;
                if (_agent.enabled) _agent.isStopped = false;
                MoveToNextWaypoint();
            }
        }

        void AbortConfrontation()
        {
            if (_confronting != null) _confronting = null;
            if (_agent.enabled) _agent.isStopped = false;
        }

        void Update()
        {
            if (!IsServer) return;

            switch (CurrentState)
            {
                case State.Patrolling:
                    Patrol();
                    ScanForSlackers();
                    break;

                case State.Investigating:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        _state.Value = (int)State.Patrolling;
                        MoveToNextWaypoint();
                    }
                    ScanForSlackers();
                    break;

                case State.Confronting:
                    HoldConfrontation();
                    break;

                case State.AtMeeting:
                    ScanForSlackers();
                    break;
            }
        }

        void HoldConfrontation()
        {
            if (_confronting == null || !_confronting.IsConfronted)
            {
                EndConfrontation();
                return;
            }

            Vector3 toPlayer = _confronting.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(toPlayer), 6f * Time.deltaTime);
            }
        }

        void Patrol()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            if (!ArrivedAtDestination()) return;

            _pauseTimer += Time.deltaTime;
            if (_pauseTimer < waypointPause) return;

            _pauseTimer = 0f;
            _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
            MoveToNextWaypoint();
        }

        bool ArrivedAtDestination()
        {
            if (!_agent.enabled || _agent.pathPending) return false;
            return _agent.remainingDistance <= _agent.stoppingDistance + 0.25f;
        }

        void MoveToNextWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Transform target = waypoints[_waypointIndex];
            if (target != null) SetDestination(target.position);
        }

        void SetDestination(Vector3 position)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }

        void ScanForSlackers()
        {
            if (GameManager.GraceActive) return;

            foreach (var player in PlayerStatus.All)
            {
                if (player == null || player.IsCaught || player.IsConfronted) continue;
                if (player.IsExcusedFromWork) continue;
                if (!_vision.CanSee(player.transform)) continue;

                float distance = Vector3.Distance(player.transform.position, transform.position);
                if (distance <= confrontRange && CurrentState != State.AtMeeting)
                {
                    StartConfrontation(player);
                    return;
                }

                player.ReportObservation(suspicionPerSecond, SuspicionReason.SeenByBoss);
                player.MarkSeenByBoss();
            }
        }

        void StartConfrontation(PlayerStatus player)
        {
            _confronting = player;
            _state.Value = (int)State.Confronting;
            if (_agent.enabled) _agent.isStopped = true;

            player.BeginConfrontation();
            player.MarkSeenByBoss();
        }
    }
}
