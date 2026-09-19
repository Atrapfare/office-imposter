using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace OfficeImposter
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class BossAI : NetworkBehaviour
    {
        [SerializeField] float viewDistance = 13f;
        [SerializeField] float viewAngle = 80f;
        [SerializeField] float suspicionPerSecond = 11f;
        [SerializeField] float waypointPause = 2.5f;
        [SerializeField] float eyeHeight = 1.6f;
        [SerializeField] Transform[] waypoints;

        NavMeshAgent _agent;
        int _waypointIndex;
        float _pauseTimer;

        public bool IsWatchingLocalPlayer { get; private set; }

        public void Configure(Transform[] patrolPoints) => waypoints = patrolPoints;

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        public override void OnNetworkSpawn()
        {
            // Only the server steers the boss; clients just receive its transform.
            _agent.enabled = IsServer;
            if (IsServer) MoveToNextWaypoint();
        }

        void Update()
        {
            IsWatchingLocalPlayer = false;
            if (!IsServer || waypoints == null || waypoints.Length == 0) return;

            Patrol();
            ScanForSlackers();
        }

        void Patrol()
        {
            if (!_agent.enabled || _agent.pathPending) return;

            if (_agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
            {
                _pauseTimer += Time.deltaTime;
                if (_pauseTimer >= waypointPause)
                {
                    _pauseTimer = 0f;
                    _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
                    MoveToNextWaypoint();
                }
            }
        }

        void MoveToNextWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Transform target = waypoints[_waypointIndex];
            if (target != null && _agent.enabled && _agent.isOnNavMesh) _agent.SetDestination(target.position);
        }

        void ScanForSlackers()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;

            foreach (var player in PlayerStatus.All)
            {
                if (player == null || player.IsCaught || player.IsWorking) continue;
                if (!CanSee(eye, player, out _)) continue;

                player.AddSuspicion(suspicionPerSecond * Time.deltaTime);
            }
        }

        bool CanSee(Vector3 eye, PlayerStatus player, out float distance)
        {
            Vector3 chest = player.transform.position + Vector3.up * 1.2f;
            Vector3 offset = chest - eye;
            distance = offset.magnitude;

            if (distance > viewDistance) return false;
            if (Vector3.Angle(transform.forward, offset.normalized) > viewAngle * 0.5f) return false;

            // Anything solid between the boss and the player breaks line of sight.
            RaycastHit[] hits = Physics.RaycastAll(eye, offset / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(player.transform)) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                return false;
            }

            return true;
        }
    }
}
