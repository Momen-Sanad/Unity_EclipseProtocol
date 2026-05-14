using EclipseProtocol.Core;
using EclipseProtocol.Audio;
using EclipseProtocol.Player;
using EclipseProtocol.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

namespace EclipseProtocol.World
{
    [RequireComponent(typeof(Collider))]
    public class RepairNode : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField] private Renderer statusRenderer;
        [SerializeField] private Light statusLight;
        [SerializeField] private DoorGate linkedDoor;
        [SerializeField, Min(0.1f)] private float fallbackRepairSeconds = 3f;
        [SerializeField, Min(0.5f)] private float playerDetectionRadius = 2.25f;
        [SerializeField] private Color repairingColor = new Color(1f, 0.7f, 0.15f);
        [SerializeField] private Color repairedColor = new Color(0.2f, 1f, 0.75f);
        [SerializeField] private AudioClip repairLoopClip;
        [SerializeField, Range(0f, 1f)] private float repairLoopVolume = 0.75f;
        [SerializeField, Min(0.1f)] private float repairIndicatorLength = 1f;
        [SerializeField, Min(0.02f)] private float repairIndicatorWidth = 0.1f;
        [SerializeField, Min(0.01f)] private float repairIndicatorHeight = 0.04f;
        [SerializeField, Min(0.1f)] private float repairIndicatorYOffset = 3f;
        [SerializeField] private Color repairIndicatorIdleColor = new Color(0.05f, 0.22f, 1f);
        [SerializeField] private Color repairIndicatorActiveColor = new Color(0.1f, 1f, 0.35f);
        [SerializeField] private bool blockEnemyNavigation = true;
        [SerializeField] private Vector3 navigationBlockerSize = new Vector3(1.6f, 2f, 1.6f);
        [Header("Visuals")]
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private string visualAssetPath;
        [SerializeField] private Vector3 visualLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 visualLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 visualLocalScale = Vector3.one;
        [SerializeField] private bool forceVisualRenderersVisible = true;
        [SerializeField] private bool centerVisualBoundsOnRoot = true;

        private PlayerController _playerInside;
        private HUDController _hudController;
        private MaterialPropertyBlock _statusPropertyBlock;
        private MaterialPropertyBlock _repairIndicatorPropertyBlock;
        private Renderer _repairIndicatorRenderer;
        private AudioSource _repairLoopSource;
        private GameObject _visualInstance;
        private float _progressSeconds;
        private bool _promptShown;
        private static RepairNode _activeRepairNode;

        public bool IsRepaired { get; private set; }
        public float Progress01 => RepairSeconds <= 0f ? 1f : Mathf.Clamp01(_progressSeconds / RepairSeconds);
        private float RepairSeconds => balanceData != null ? balanceData.repairHoldSeconds : fallbackRepairSeconds;

        private void Awake()
        {
            Collider repairCollider = GetComponent<Collider>();
            // repairCollider.isTrigger = true;
            ConfigureVisual();

            if (statusRenderer == null)
            {
                statusRenderer = GetComponentInChildren<Renderer>();
            }

            ConfigureNavigationBlocker();
        }

        private void ConfigureVisual()
        {
            GameObject resolvedVisualPrefab = ResolveVisualPrefab();
            if (resolvedVisualPrefab == null || _visualInstance != null)
            {
                return;
            }

            _visualInstance = CreateVisualInstance(resolvedVisualPrefab);
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

            if (statusRenderer == null)
            {
                statusRenderer = visualRenderers[0];
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

        private GameObject ResolveVisualPrefab()
        {
            if (visualPrefab != null)
            {
                return visualPrefab;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(visualAssetPath))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(visualAssetPath);
            }
#endif

            return null;
        }

        private GameObject CreateVisualInstance(GameObject resolvedVisualPrefab)
        {
            GameObject instance = Instantiate(resolvedVisualPrefab, transform);
            instance.name = resolvedVisualPrefab.name;
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

        private void Start()
        {
            _hudController = FindAnyObjectByType<HUDController>();
            CreateRepairIndicator();
            SetStatusColor(repairingColor);
            SetRepairIndicatorActive(false);
        }

        private void OnDestroy()
        {
            StopRepairLoop();
            ReleaseRepairClaim();
        }

        private void Update()
        {
            RefreshPlayerPresence();

            if (IsRepaired || _playerInside == null)
            {
                if (_playerInside == null && _progressSeconds > 0f)
                {
                    _progressSeconds = 0f;
                    _hudController?.SetRepairProgress(0f, false);
                }

                ReleaseRepairClaim();
                StopRepairLoop();
                SetRepairIndicatorActive(IsRepaired);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool isHoldingRepair = keyboard != null && keyboard.eKey.isPressed;
            if (isHoldingRepair)
            {
                if (!TryClaimRepair())
                {
                    StopRepairLoop();
                    return;
                }

                StartRepairLoop();
                _progressSeconds += Time.deltaTime;
                _hudController?.SetRepairProgress(Progress01, true);

                if (_progressSeconds >= RepairSeconds)
                {
                    CompleteRepair();
                }
            }
            else if (_progressSeconds > 0f)
            {
                ReleaseRepairClaim();
                StopRepairLoop();
                SetRepairIndicatorActive(false);
                _progressSeconds = Mathf.Max(0f, _progressSeconds - Time.deltaTime);
                _hudController?.SetRepairProgress(Progress01, true);
            }
            else
            {
                ReleaseRepairClaim();
                StopRepairLoop();
                SetRepairIndicatorActive(false);
            }
        }

        public void Configure(GameBalanceData data, DoorGate doorGate = null)
        {
            balanceData = data;
            linkedDoor = doorGate;
        }

        public void SetLinkedDoor(DoorGate doorGate)
        {
            linkedDoor = doorGate;
        }

        private void OnTriggerEnter(Collider other)
        {
            TrySetPlayer(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TrySetPlayer(other);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController playerController = other.GetComponentInParent<PlayerController>();
            if (_playerInside == null || playerController == null || playerController != _playerInside)
            {
                return;
            }

            _playerInside = null;
            _promptShown = false;
            ReleaseRepairClaim();
            StopRepairLoop();
            SetRepairIndicatorActive(IsRepaired);
            _hudController?.SetRepairProgress(0f, false);
        }

        private void RefreshPlayerPresence()
        {
            if (_playerInside != null)
            {
                float sqrRange = playerDetectionRadius * playerDetectionRadius;
                if ((_playerInside.transform.position - transform.position).sqrMagnitude <= sqrRange)
                {
                    return;
                }

                _playerInside = null;
                _promptShown = false;
                ReleaseRepairClaim();
                StopRepairLoop();
                SetRepairIndicatorActive(IsRepaired);
                _hudController?.SetRepairProgress(0f, false);
                return;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, playerDetectionRadius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                if (TrySetPlayer(hits[i]))
                {
                    return;
                }
            }
        }

        private bool TrySetPlayer(Collider other)
        {
            PlayerController playerController = other.GetComponentInParent<PlayerController>();
            if (playerController == null)
            {
                return false;
            }

            _playerInside = playerController;
            if (!_promptShown)
            {
                _promptShown = true;
                _hudController?.ShowMessage("Hold E to repair power node", 2f);
            }

            if (_activeRepairNode == null || _activeRepairNode == this)
            {
                _hudController?.SetRepairProgress(Progress01, !IsRepaired);
            }

            return true;
        }

        private void CompleteRepair()
        {
            IsRepaired = true;
            _progressSeconds = RepairSeconds;
            ReleaseRepairClaim();
            StopRepairLoop();
            SetStatusColor(repairedColor);
            SetRepairIndicatorActive(true);
            _hudController?.SetRepairProgress(1f, false);
            if (linkedDoor != null && linkedDoor.NotifyRepairNodeCompleted())
            {
                _hudController?.ShowMessage("Door unlocked. Move forward.", 2f);
            }
            AudioManager.Instance?.PlayRepairComplete(transform.position);
            GameStateManager.Instance?.MarkPowerRepaired(this);
        }

        private void SetStatusColor(Color color)
        {
            if (statusRenderer != null)
            {
                _statusPropertyBlock ??= new MaterialPropertyBlock();
                statusRenderer.GetPropertyBlock(_statusPropertyBlock);
                _statusPropertyBlock.SetColor("_BaseColor", color);
                _statusPropertyBlock.SetColor("_Color", color);
                statusRenderer.SetPropertyBlock(_statusPropertyBlock);
            }

            if (statusLight != null)
            {
                statusLight.color = color;
            }
        }

        private void CreateRepairIndicator()
        {
            if (_repairIndicatorRenderer != null)
            {
                return;
            }

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "RepairIndicatorLine";
            indicator.transform.SetParent(transform, false);
            indicator.transform.localPosition = Vector3.up * repairIndicatorYOffset;
            indicator.transform.localRotation = Quaternion.identity;
            indicator.transform.localScale = new Vector3(repairIndicatorLength, repairIndicatorHeight, repairIndicatorWidth);

            Collider indicatorCollider = indicator.GetComponent<Collider>();
            if (indicatorCollider != null)
            {
                indicatorCollider.enabled = false;
            }

            _repairIndicatorRenderer = indicator.GetComponent<Renderer>();
        }

        private void SetRepairIndicatorActive(bool isActive)
        {
            if (_repairIndicatorRenderer == null)
            {
                return;
            }

            _repairIndicatorPropertyBlock ??= new MaterialPropertyBlock();
            _repairIndicatorRenderer.GetPropertyBlock(_repairIndicatorPropertyBlock);
            Color color = isActive ? repairIndicatorActiveColor : repairIndicatorIdleColor;
            _repairIndicatorPropertyBlock.SetColor("_BaseColor", color);
            _repairIndicatorPropertyBlock.SetColor("_Color", color);
            _repairIndicatorRenderer.SetPropertyBlock(_repairIndicatorPropertyBlock);
        }

        private void ConfigureNavigationBlocker()
        {
            if (!blockEnemyNavigation)
            {
                return;
            }

            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
            }

            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = navigationBlockerSize;
            obstacle.center = Vector3.zero;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingMoveThreshold = 0.05f;
            obstacle.carvingTimeToStationary = 0.1f;
        }

        private void StartRepairLoop()
        {
            if (repairLoopClip == null)
            {
                return;
            }

            if (_repairLoopSource == null)
            {
                _repairLoopSource = gameObject.AddComponent<AudioSource>();
                _repairLoopSource.playOnAwake = false;
                _repairLoopSource.loop = true;
                _repairLoopSource.spatialBlend = 0.65f;
                _repairLoopSource.rolloffMode = AudioRolloffMode.Linear;
                _repairLoopSource.maxDistance = 18f;
            }

            _repairLoopSource.clip = repairLoopClip;
            _repairLoopSource.volume = repairLoopVolume;
            if (!_repairLoopSource.isPlaying)
            {
                _repairLoopSource.Play();
            }
        }

        private void StopRepairLoop()
        {
            if (_repairLoopSource != null && _repairLoopSource.isPlaying)
            {
                _repairLoopSource.Stop();
            }
        }

        private bool TryClaimRepair()
        {
            if (_activeRepairNode == null || _activeRepairNode == this)
            {
                _activeRepairNode = this;
                return true;
            }

            return false;
        }

        private void ReleaseRepairClaim()
        {
            if (_activeRepairNode == this)
            {
                _activeRepairNode = null;
            }
        }
    }
}
