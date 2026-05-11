using System.Collections.Generic;
using EclipseProtocol.Core;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseProtocol.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class DronePatrolAI : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField, Min(0.05f)] private float waypointTolerance = 0.3f;
        [SerializeField] private bool constrainToRoom;
        [SerializeField] private Bounds movementBounds;
        [SerializeField, Min(0.1f)] private float roomEdgePadding = 0.75f;
        [SerializeField, Min(0.1f)] private float stuckVelocityThreshold = 0.08f;
        [SerializeField, Min(0.25f)] private float stuckRecoverySeconds = 1.25f;
        [SerializeField, Min(0.1f)] private float stuckRepathInterval = 0.4f;

        private int _currentWaypointIndex;
        private float _stuckTimer;
        private float _stuckRepathTimer;

        public IReadOnlyList<Transform> Waypoints => waypoints;
        public GameBalanceData BalanceData => balanceData;

        public void Initialize(GameBalanceData data, IReadOnlyList<Transform> patrolWaypoints)
        {
            balanceData = data;
            waypoints.Clear();
            if (patrolWaypoints != null)
            {
                for (int i = 0; i < patrolWaypoints.Count; i++)
                {
                    if (patrolWaypoints[i] != null)
                    {
                        waypoints.Add(patrolWaypoints[i]);
                    }
                }
            }

            _currentWaypointIndex = 0;
            ApplyAgentSettings();

            if (navMeshAgent != null && navMeshAgent.isOnNavMesh && waypoints.Count > 0)
            {
                navMeshAgent.SetDestination(ClampToMovementBounds(waypoints[0].position));
            }
        }

        public void SetMovementBounds(Bounds bounds)
        {
            movementBounds = bounds;
            constrainToRoom = true;
            EnforceMovementBounds();
        }

        private void Reset()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        private void Awake()
        {
            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }
        }

        private void Start()
        {
            ApplyAgentSettings();

            if (waypoints.Count > 0 && waypoints[0] != null)
            {
                navMeshAgent.SetDestination(ClampToMovementBounds(waypoints[0].position));
            }
        }

        private void Update()
        {
            EnforceMovementBounds();

            if (waypoints.Count == 0 || navMeshAgent.pathPending)
            {
                return;
            }

            RecoverIfStuck();

            if (navMeshAgent.remainingDistance <= Mathf.Max(waypointTolerance, navMeshAgent.stoppingDistance))
            {
                AdvanceToNextWaypoint();
            }
        }

        private void ApplyAgentSettings()
        {
            if (balanceData == null)
            {
                return;
            }

            navMeshAgent.speed = balanceData.droneMoveSpeed;
            navMeshAgent.acceleration = balanceData.droneAcceleration;
            navMeshAgent.stoppingDistance = balanceData.droneStoppingDistance;
            navMeshAgent.autoBraking = false;
            navMeshAgent.autoRepath = true;
            navMeshAgent.avoidancePriority = 30 + Mathf.Abs(GetInstanceID()) % 40;
        }

        private void RecoverIfStuck()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh || !navMeshAgent.hasPath)
            {
                _stuckTimer = 0f;
                _stuckRepathTimer = 0f;
                return;
            }

            float arrivalDistance = Mathf.Max(waypointTolerance, navMeshAgent.stoppingDistance) + 0.25f;
            bool tryingToMove = navMeshAgent.desiredVelocity.sqrMagnitude > stuckVelocityThreshold * stuckVelocityThreshold;
            bool barelyMoving = navMeshAgent.velocity.sqrMagnitude < stuckVelocityThreshold * stuckVelocityThreshold;
            bool stillFarFromDestination = navMeshAgent.remainingDistance > arrivalDistance;
            if (!tryingToMove || !barelyMoving || !stillFarFromDestination)
            {
                _stuckTimer = 0f;
                _stuckRepathTimer = 0f;
                return;
            }

            _stuckTimer += Time.deltaTime;
            _stuckRepathTimer -= Time.deltaTime;
            if (_stuckRepathTimer <= 0f && waypoints[_currentWaypointIndex] != null)
            {
                _stuckRepathTimer = stuckRepathInterval;
                navMeshAgent.SetDestination(ClampToMovementBounds(waypoints[_currentWaypointIndex].position));
            }

            if (_stuckTimer < stuckRecoverySeconds)
            {
                return;
            }

            navMeshAgent.ResetPath();
            _stuckTimer = 0f;
            _stuckRepathTimer = 0f;
            AdvanceToNextWaypoint();
        }

        private void AdvanceToNextWaypoint()
        {
            if (waypoints.Count == 0)
            {
                return;
            }

            _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Count;
            Transform nextWaypoint = waypoints[_currentWaypointIndex];
            if (nextWaypoint != null)
            {
                navMeshAgent.SetDestination(ClampToMovementBounds(nextWaypoint.position));
            }
        }

        private void EnforceMovementBounds()
        {
            if (!constrainToRoom || IsInsideMovementBounds(transform.position))
            {
                return;
            }

            Vector3 clampedPosition = ClampToMovementBounds(transform.position);
            if (NavMesh.SamplePosition(clampedPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                clampedPosition = hit.position;
            }

            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.Warp(clampedPosition);
                if (waypoints.Count > 0 && waypoints[_currentWaypointIndex] != null)
                {
                    navMeshAgent.SetDestination(ClampToMovementBounds(waypoints[_currentWaypointIndex].position));
                }
            }
            else
            {
                transform.position = clampedPosition;
            }
        }

        private bool IsInsideMovementBounds(Vector3 position)
        {
            if (!constrainToRoom)
            {
                return true;
            }

            Vector3 min = movementBounds.min;
            Vector3 max = movementBounds.max;
            return position.x >= min.x + roomEdgePadding
                && position.x <= max.x - roomEdgePadding
                && position.z >= min.z + roomEdgePadding
                && position.z <= max.z - roomEdgePadding;
        }

        private Vector3 ClampToMovementBounds(Vector3 position)
        {
            if (!constrainToRoom)
            {
                return position;
            }

            Vector3 min = movementBounds.min;
            Vector3 max = movementBounds.max;
            position.x = Mathf.Clamp(position.x, min.x + roomEdgePadding, max.x - roomEdgePadding);
            position.z = Mathf.Clamp(position.z, min.z + roomEdgePadding, max.z - roomEdgePadding);
            return position;
        }
    }
}
