using EclipseProtocol.Core;
using EclipseProtocol.Player;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseProtocol.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class HunterDroneAI : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindPlayerTarget = true;
        [SerializeField, Min(0f)] private float repathInterval = 0.15f;
        [SerializeField, Min(0.1f)] private float speedMultiplier = 1.15f;

        private float _repathTimer;

        public void Initialize(GameBalanceData data, Transform chaseTarget)
        {
            balanceData = data;
            target = chaseTarget;
            ApplyAgentSettings();
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

            if (autoFindPlayerTarget && target == null)
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }
            }

            ApplyAgentSettings();
        }

        private void Update()
        {
            if (target == null || !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer > 0f)
            {
                return;
            }

            _repathTimer = repathInterval;
            navMeshAgent.SetDestination(target.position);
        }

        private void ApplyAgentSettings()
        {
            float baseSpeed = balanceData != null ? balanceData.droneMoveSpeed : 3.5f;
            float baseAcceleration = balanceData != null ? balanceData.droneAcceleration : 8f;
            float baseStoppingDistance = balanceData != null ? balanceData.droneStoppingDistance : 0.2f;

            navMeshAgent.speed = baseSpeed * speedMultiplier;
            navMeshAgent.acceleration = baseAcceleration;
            navMeshAgent.stoppingDistance = baseStoppingDistance;
        }
    }
}
