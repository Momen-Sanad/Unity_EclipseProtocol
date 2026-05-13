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
        [SerializeField, Min(0.5f)] private float roamPointMinDistance = 2f;
        [Header("Visuals")]
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private Vector3 visualLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 visualLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 visualLocalScale = Vector3.one;
        [SerializeField] private bool forceVisualRenderersVisible = true;
        [SerializeField] private bool centerVisualBoundsOnRoot = true;

        private int _currentWaypointIndex;
        private float _stuckTimer;
        private float _stuckRepathTimer;
        private Vector3 _homePosition;
        private Vector3 _currentDestination;
        private GameObject _visualInstance;

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

            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                SetNextDestination();
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

            _homePosition = transform.position;
            ConfigureVisual();
        }

        private void Start()
        {
            ApplyAgentSettings();

            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                SetNextDestination();
            }
        }

        private void Update()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            EnforceMovementBounds();

            EnsureActiveDestination();

            if (navMeshAgent.pathPending)
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

        private void ConfigureVisual()
        {
            if (visualPrefab == null || _visualInstance != null)
            {
                return;
            }

            _visualInstance = CreateVisualInstance();
            SetLayerRecursively(_visualInstance, gameObject.layer);

            if (forceVisualRenderersVisible)
            {
                ShowVisualHierarchy(_visualInstance);
            }

            Renderer[] visualRenderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            if (visualRenderers.Length == 0)
            {
                return;
            }

            if (forceVisualRenderersVisible)
            {
                EnableRenderers(visualRenderers);
            }

            if (centerVisualBoundsOnRoot)
            {
                CenterVisualBoundsOnRoot(_visualInstance.transform, visualRenderers);
            }
        }

        private GameObject CreateVisualInstance()
        {
            GameObject instance = Instantiate(visualPrefab, transform);
            instance.name = visualPrefab.name;
            ApplyVisualTransform(instance.transform);
            return instance;
        }

        private void ApplyVisualTransform(Transform visualTransform)
        {
            visualTransform.localPosition = visualLocalPosition;
            visualTransform.localRotation = Quaternion.Euler(visualLocalEulerAngles);
            visualTransform.localScale = visualLocalScale;
        }

        private void CenterVisualBoundsOnRoot(Transform visualTransform, Renderer[] visualRenderers)
        {
            Bounds bounds = CalculateRendererBounds(visualRenderers);
            visualTransform.position += transform.position - bounds.center;
        }

        private static void EnableRenderers(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = true;
            }
        }

        private static void SetLayerRecursively(GameObject targetObject, int layer)
        {
            targetObject.layer = layer;
            Transform targetTransform = targetObject.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
            {
                SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
            }
        }

        private static void ShowVisualHierarchy(GameObject targetObject)
        {
            targetObject.SetActive(true);
            Transform targetTransform = targetObject.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
            {
                ShowVisualHierarchy(targetTransform.GetChild(i).gameObject);
            }
        }

        private static Bounds CalculateRendererBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private void RecoverIfStuck()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                _stuckTimer = 0f;
                _stuckRepathTimer = 0f;
                return;
            }

            if (!navMeshAgent.hasPath)
            {
                _stuckTimer = 0f;
                _stuckRepathTimer = 0f;
                EnsureActiveDestination();
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
            if (_stuckRepathTimer <= 0f)
            {
                _stuckRepathTimer = stuckRepathInterval;
                navMeshAgent.SetDestination(_currentDestination);
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
            if (waypoints.Count >= 2)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Count;
            }

            SetNextDestination();
        }

        private void SetNextDestination()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            if (waypoints.Count >= 2 && waypoints[_currentWaypointIndex] != null)
            {
                SetDestination(waypoints[_currentWaypointIndex].position);
                return;
            }

            SetDestination(FindRoamPoint());
        }

        private void EnsureActiveDestination()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh || navMeshAgent.pathPending)
            {
                return;
            }

            if (!navMeshAgent.hasPath || navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                SetNextDestination();
            }
        }

        private void SetDestination(Vector3 destination)
        {
            _currentDestination = ClampToMovementBounds(destination);
            if (NavMesh.SamplePosition(_currentDestination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                _currentDestination = hit.position;
            }

            navMeshAgent.SetDestination(_currentDestination);
        }

        private Vector3 FindRoamPoint()
        {
            Vector3 origin = navMeshAgent != null && navMeshAgent.isOnNavMesh ? navMeshAgent.transform.position : transform.position;
            for (int i = 0; i < 12; i++)
            {
                Vector3 candidate = constrainToRoom
                    ? new Vector3(
                        Random.Range(movementBounds.min.x + roomEdgePadding, movementBounds.max.x - roomEdgePadding),
                        origin.y,
                        Random.Range(movementBounds.min.z + roomEdgePadding, movementBounds.max.z - roomEdgePadding))
                    : _homePosition + Random.insideUnitSphere * 8f;

                candidate.y = origin.y;
                candidate = ClampToMovementBounds(candidate);
                if ((candidate - origin).sqrMagnitude < roamPointMinDistance * roamPointMinDistance)
                {
                    continue;
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return waypoints.Count == 1 && waypoints[0] != null ? waypoints[0].position : _homePosition;
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
                SetNextDestination();
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
