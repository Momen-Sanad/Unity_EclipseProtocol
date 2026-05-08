using EclipseProtocol.Core;
using EclipseProtocol.Audio;
using EclipseProtocol.Player;
using EclipseProtocol.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private PlayerController _playerInside;
        private HUDController _hudController;
        private MaterialPropertyBlock _statusPropertyBlock;
        private float _progressSeconds;
        private bool _promptShown;

        public bool IsRepaired { get; private set; }
        public float Progress01 => RepairSeconds <= 0f ? 1f : Mathf.Clamp01(_progressSeconds / RepairSeconds);
        private float RepairSeconds => balanceData != null ? balanceData.repairHoldSeconds : fallbackRepairSeconds;

        private void Awake()
        {
            Collider repairCollider = GetComponent<Collider>();
            repairCollider.isTrigger = true;

            if (statusRenderer == null)
            {
                statusRenderer = GetComponentInChildren<Renderer>();
            }
        }

        private void Start()
        {
            _hudController = FindAnyObjectByType<HUDController>();
            SetStatusColor(repairingColor);
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

                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool isHoldingRepair = keyboard != null && keyboard.eKey.isPressed;
            if (isHoldingRepair)
            {
                _progressSeconds += Time.deltaTime;
                _hudController?.SetRepairProgress(Progress01, true);

                if (_progressSeconds >= RepairSeconds)
                {
                    CompleteRepair();
                }
            }
            else if (_progressSeconds > 0f)
            {
                _progressSeconds = Mathf.Max(0f, _progressSeconds - Time.deltaTime);
                _hudController?.SetRepairProgress(Progress01, true);
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

            _hudController?.SetRepairProgress(Progress01, !IsRepaired);
            return true;
        }

        private void CompleteRepair()
        {
            IsRepaired = true;
            _progressSeconds = RepairSeconds;
            SetStatusColor(repairedColor);
            _hudController?.SetRepairProgress(1f, false);
            linkedDoor?.UnlockAndOpen();
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
    }
}
