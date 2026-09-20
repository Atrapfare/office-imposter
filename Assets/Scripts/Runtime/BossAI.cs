using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace OfficeImposter
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(VisionCone))]
    public class BossAI : NetworkBehaviour
    {
        public enum State { Patrolling, Investigating, AtMeeting }

        [SerializeField] float suspicionPerSecond = 11f;
        [SerializeField] float waypointPause = 2.5f;
        [SerializeField] float investigateDuration = 7f;
        [SerializeField] Transform[] waypoints;

        public static BossAI Instance { get; private set; }

        readonly NetworkVariable<int> _state = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        NavMeshAgent _agent;
        VisionCone _vision;
        int _waypointIndex;
        float _pauseTimer;
        float _stateTimer;
        Vector3 _meetingSpot;
        bool _hasMeetingSpot;

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

        // Called by coworkers who have had enough of watching someone do nothing.
        public void Investigate(Vector3 position)
        {
            if (!IsServer || CurrentState == State.AtMeeting) return;

            _state.Value = (int)State.Investigating;
            _stateTimer = investigateDuration;
            SetDestination(position);
        }

        public void GoToMeeting(Vector3 position)
        {
            if (!IsServer) return;

            _state.Value = (int)State.AtMeeting;
            _meetingSpot = position;
            _hasMeetingSpot = true;
            SetDestination(position);
        }

        public void EndMeeting()
        {
            if (!IsServer) return;

            _hasMeetingSpot = false;
            _state.Value = (int)State.Patrolling;
            MoveToNextWaypoint();
        }

        void Update()
        {
            if (!IsServer) return;

            switch (CurrentState)
            {
                case State.Patrolling:
                    Patrol();
                    break;
                case State.Investigating:
                    _stateTimer -= Time.deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        _state.Value = (int)State.Patrolling;
                        MoveToNextWaypoint();
                    }
                    break;
                case State.AtMeeting:
                    if (_hasMeetingSpot && ArrivedAtDestination()) transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(Vector3.left), 2f * Time.deltaTime);
                    break;
            }

            ScanForSlackers();
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
            foreach (var player in PlayerStatus.All)
            {
                if (player == null || player.IsCaught) continue;
                if (player.IsExcusedFromWork) continue;
                if (!_vision.CanSee(player.transform)) continue;

                player.AddSuspicion(suspicionPerSecond * Time.deltaTime);
                player.MarkSeenByBoss();
            }
        }
    }
}
