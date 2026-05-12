using System.Collections.Generic;
using EclipseProtocol.Audio;
using EclipseProtocol.Core;
using EclipseProtocol.Player;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseProtocol.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyContactDamage))]
    public class HunterDroneAI : MonoBehaviour
    {
        private enum HunterState
        {
            Idle,
            Patrol,
            Chase,
            WindUp,
            Lunge,
            Recover,
            Return
        }

        [SerializeField] private GameBalanceData balanceData;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindPlayerTarget = true;
        [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
        [SerializeField] private Renderer stateRenderer;
        [SerializeField] private Color patrolColor = new Color(0.25f, 0.8f, 1f);
        [SerializeField] private Color chaseColor = new Color(1f, 0.55f, 0.15f);
        [SerializeField] private Color attackColor = new Color(1f, 0.1f, 0.08f);
        [SerializeField, Min(0.05f)] private float waypointTolerance = 0.35f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.15f;
        [SerializeField] private bool constrainToRoom;
        [SerializeField] private Bounds movementBounds;
        [SerializeField, Min(0.1f)] private float roomEdgePadding = 0.75f;
        [SerializeField, Min(0.5f)] private float roamPointMinDistance = 2f;
        [SerializeField, Min(0.1f)] private float stalledVelocityThreshold = 0.08f;
        [SerializeField, Min(0.1f)] private float stalledRepathSeconds = 0.5f;

        private EnemyContactDamage _contactDamage;
        private HunterState _state = HunterState.Idle;
        private Vector3 _homePosition;
        private Vector3 _lungeDirection;
        private Vector3 _patrolDestination;
        private MaterialPropertyBlock _statePropertyBlock;
        private float _stateTimer;
        private float _repathTimer;
        private float _stalledRepathTimer;
        private float _nextAttackTime;
        private int _patrolIndex;

        public string CurrentStateName => _state.ToString();

        public void Initialize(GameBalanceData data, Transform chaseTarget, IReadOnlyList<Transform> route)
        {
            balanceData = data;
            target = chaseTarget;
            patrolPoints.Clear();
            if (route != null)
            {
                for (int i = 0; i < route.Count; i++)
                {
                    if (route[i] != null)
                    {
                        patrolPoints.Add(route[i]);
                    }
                }
            }

            ApplyAgentSettings();
            EnterState(HunterState.Patrol);
        }

        public void SetMovementBounds(Bounds bounds)
        {
            movementBounds = bounds;
            constrainToRoom = true;
            _homePosition = ClampToMovementBounds(_homePosition);
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

            _contactDamage = GetComponent<EnemyContactDamage>();
            _contactDamage.SetDamageEnabled(false);
            _homePosition = transform.position;

            if (stateRenderer == null)
            {
                stateRenderer = GetComponentInChildren<Renderer>();
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

        private void Start()
        {
            EnterState(HunterState.Patrol);
        }

        private void Update()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            EnforceMovementBounds();

            switch (_state)
            {
                case HunterState.Idle:
                    TickIdle();
                    break;
                case HunterState.Patrol:
                    TickPatrol();
                    break;
                case HunterState.Chase:
                    TickChase();
                    break;
                case HunterState.WindUp:
                    TickWindUp();
                    break;
                case HunterState.Lunge:
                    TickLunge();
                    break;
                case HunterState.Recover:
                    TickRecover();
                    break;
                case HunterState.Return:
                    TickReturn();
                    break;
            }

            EnforceMovementBounds();
        }

        private void TickIdle()
        {
            if (CanDetectTarget())
            {
                EnterState(HunterState.Chase);
            }
        }

        private void TickPatrol()
        {
            if (CanDetectTarget())
            {
                EnterState(HunterState.Chase);
                return;
            }

            EnsurePatrolDestination();

            if (navMeshAgent.pathPending)
            {
                return;
            }

            if (navMeshAgent.remainingDistance <= Mathf.Max(waypointTolerance, navMeshAgent.stoppingDistance))
            {
                AdvancePatrolDestination();
            }
            else
            {
                RecoverPatrolPathIfNeeded();
            }
        }

        private void TickChase()
        {
            if (!CanDetectTarget())
            {
                EnterState(HunterState.Return);
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= AttackRange && Time.time >= _nextAttackTime)
            {
                EnterState(HunterState.WindUp);
                return;
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                navMeshAgent.SetDestination(ClampToMovementBounds(target.position));
            }
        }

        private void TickWindUp()
        {
            if (target == null)
            {
                EnterState(HunterState.Return);
                return;
            }

            FaceTarget();
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
            {
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            _lungeDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
            EnterState(HunterState.Lunge);
        }

        private void TickLunge()
        {
            _stateTimer -= Time.deltaTime;
            navMeshAgent.Move(_lungeDirection * LungeSpeed * Time.deltaTime);

            if (_stateTimer <= 0f)
            {
                EnterState(HunterState.Recover);
            }
        }

        private void TickRecover()
        {
            _stateTimer -= Time.deltaTime;
            if (_stateTimer > 0f)
            {
                return;
            }

            EnterState(CanDetectTarget() ? HunterState.Chase : HunterState.Return);
        }

        private void TickReturn()
        {
            if (CanDetectTarget())
            {
                EnterState(HunterState.Chase);
                return;
            }

            EnsurePatrolDestination();

            if (navMeshAgent.pathPending)
            {
                return;
            }

            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= Mathf.Max(waypointTolerance, navMeshAgent.stoppingDistance))
            {
                EnterState(HunterState.Patrol);
            }
            else
            {
                RecoverPatrolPathIfNeeded();
            }
        }

        private void EnterState(HunterState nextState)
        {
            _state = nextState;
            _contactDamage?.SetDamageEnabled(nextState == HunterState.Lunge);
            bool canUseAgent = navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh;

            switch (_state)
            {
                case HunterState.Idle:
                    if (canUseAgent)
                    {
                        navMeshAgent.isStopped = true;
                    }
                    SetStateColor(patrolColor);
                    break;
                case HunterState.Patrol:
                    if (canUseAgent)
                    {
                        navMeshAgent.isStopped = false;
                    }
                    SetStateColor(patrolColor);
                    if (canUseAgent)
                    {
                        SetPatrolDestination();
                    }
                    break;
                case HunterState.Chase:
                    if (canUseAgent)
                    {
                        navMeshAgent.isStopped = false;
                    }
                    _repathTimer = 0f;
                    SetStateColor(chaseColor);
                    break;
                case HunterState.WindUp:
                    if (canUseAgent)
                    {
                        navMeshAgent.ResetPath();
                        navMeshAgent.isStopped = true;
                    }
                    _stateTimer = WindupSeconds;
                    SetStateColor(attackColor);
                    AudioManager.Instance?.PlayWarning(transform.position);
                    break;
                case HunterState.Lunge:
                    if (canUseAgent)
                    {
                        navMeshAgent.isStopped = false;
                    }
                    _stateTimer = LungeSeconds;
                    _nextAttackTime = Time.time + AttackCooldown;
                    SetStateColor(attackColor);
                    AudioManager.Instance?.PlayLunge(transform.position);
                    break;
                case HunterState.Recover:
                    if (canUseAgent)
                    {
                        navMeshAgent.ResetPath();
                        navMeshAgent.isStopped = true;
                    }
                    _stateTimer = 0.25f;
                    SetStateColor(chaseColor);
                    break;
                case HunterState.Return:
                    if (canUseAgent)
                    {
                        navMeshAgent.isStopped = false;
                        SetPatrolDestination();
                    }
                    SetStateColor(patrolColor);
                    break;
            }
        }

        private void AdvancePatrolDestination()
        {
            if (patrolPoints.Count >= 2)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Count;
            }

            SetPatrolDestination();
        }

        private void EnsurePatrolDestination()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh || navMeshAgent.pathPending)
            {
                return;
            }

            if (!navMeshAgent.hasPath || navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                SetPatrolDestination();
            }
        }

        private void RecoverPatrolPathIfNeeded()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh || navMeshAgent.pathPending)
            {
                return;
            }

            float arrivalDistance = Mathf.Max(waypointTolerance, navMeshAgent.stoppingDistance) + 0.25f;
            bool hasValidPath = navMeshAgent.hasPath && navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid;
            bool tryingToMove = navMeshAgent.desiredVelocity.sqrMagnitude > stalledVelocityThreshold * stalledVelocityThreshold;
            bool barelyMoving = navMeshAgent.velocity.sqrMagnitude < stalledVelocityThreshold * stalledVelocityThreshold;
            bool stillFarFromDestination = navMeshAgent.remainingDistance > arrivalDistance;
            if (hasValidPath && (!tryingToMove || !barelyMoving || !stillFarFromDestination))
            {
                _stalledRepathTimer = 0f;
                return;
            }

            _stalledRepathTimer += Time.deltaTime;
            if (_stalledRepathTimer < stalledRepathSeconds)
            {
                return;
            }

            _stalledRepathTimer = 0f;
            if (hasValidPath)
            {
                AdvancePatrolDestination();
            }
            else
            {
                SetPatrolDestination();
            }
        }

        private void SetPatrolDestination()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh)
            {
                return;
            }

            Vector3 destination = patrolPoints.Count >= 2 && patrolPoints[_patrolIndex] != null
                ? patrolPoints[_patrolIndex].position
                : FindRoamPoint();

            _patrolDestination = ClampToMovementBounds(destination);
            if (NavMesh.SamplePosition(_patrolDestination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                _patrolDestination = hit.position;
            }

            navMeshAgent.SetDestination(_patrolDestination);
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

            return patrolPoints.Count == 1 && patrolPoints[0] != null ? patrolPoints[0].position : _homePosition;
        }

        private void SetStateColor(Color color)
        {
            if (stateRenderer != null)
            {
                _statePropertyBlock ??= new MaterialPropertyBlock();
                stateRenderer.GetPropertyBlock(_statePropertyBlock);
                _statePropertyBlock.SetColor("_BaseColor", color);
                _statePropertyBlock.SetColor("_Color", color);
                stateRenderer.SetPropertyBlock(_statePropertyBlock);
            }
        }

        private bool CanDetectTarget()
        {
            if (target == null)
            {
                return false;
            }

            if (constrainToRoom && !IsInsideMovementBounds(target.position))
            {
                return false;
            }

            float sqrDistance = (target.position - transform.position).sqrMagnitude;
            return sqrDistance <= DetectionRadius * DetectionRadius;
        }

        private void FaceTarget()
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), 16f * Time.deltaTime);
        }

        private void ApplyAgentSettings()
        {
            if (navMeshAgent == null)
            {
                return;
            }

            float baseSpeed = balanceData != null ? balanceData.droneMoveSpeed : 3.5f;
            float baseAcceleration = balanceData != null ? balanceData.droneAcceleration : 8f;
            float baseStoppingDistance = balanceData != null ? balanceData.droneStoppingDistance : 0.2f;

            navMeshAgent.speed = baseSpeed * 1.15f;
            navMeshAgent.acceleration = baseAcceleration;
            navMeshAgent.stoppingDistance = baseStoppingDistance;
            navMeshAgent.autoRepath = true;
            navMeshAgent.avoidancePriority = 20 + Mathf.Abs(GetInstanceID()) % 30;

            if (_contactDamage != null)
            {
                _contactDamage.Configure(Damage, 0.5f, false);
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
                navMeshAgent.ResetPath();
            }
            else
            {
                transform.position = clampedPosition;
            }

            EnterState(HunterState.Return);
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

        private float DetectionRadius => balanceData != null ? balanceData.droneDetectionRadius : 8f;
        private float AttackRange => balanceData != null ? balanceData.hunterAttackRange : 2.25f;
        private float WindupSeconds => balanceData != null ? balanceData.hunterWindupSeconds : 0.45f;
        private float LungeSeconds => balanceData != null ? balanceData.hunterLungeSeconds : 0.45f;
        private float LungeSpeed => balanceData != null ? balanceData.hunterLungeSpeed : 11f;
        private float AttackCooldown => balanceData != null ? balanceData.hunterAttackCooldown : 1.75f;
        private float Damage => balanceData != null ? balanceData.hunterDamage : 15f;
    }
}
