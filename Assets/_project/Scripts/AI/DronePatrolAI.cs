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

        private int _currentWaypointIndex;

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
                navMeshAgent.SetDestination(waypoints[0].position);
            }
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
                navMeshAgent.SetDestination(waypoints[0].position);
            }
        }

        private void Update()
        {
            if (waypoints.Count == 0 || navMeshAgent.pathPending)
            {
                return;
            }

            if (navMeshAgent.remainingDistance <= Mathf.Max(waypointTolerance, navMeshAgent.stoppingDistance))
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Count;
                Transform nextWaypoint = waypoints[_currentWaypointIndex];
                if (nextWaypoint != null)
                {
                    navMeshAgent.SetDestination(nextWaypoint.position);
                }
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
        }
    }
}
