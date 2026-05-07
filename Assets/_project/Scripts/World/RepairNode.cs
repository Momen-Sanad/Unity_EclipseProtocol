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
        [SerializeField, Min(0.1f)] private float fallbackRepairSeconds = 3f;
        [SerializeField] private Color repairingColor = new Color(1f, 0.7f, 0.15f);
        [SerializeField] private Color repairedColor = new Color(0.2f, 1f, 0.75f);

        private PlayerController _playerInside;
        private HUDController _hudController;
        private float _progressSeconds;

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
            if (IsRepaired || _playerInside == null)
            {
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

        public void Configure(GameBalanceData data)
        {
            balanceData = data;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PlayerController playerController))
            {
                return;
            }

            _playerInside = playerController;
            _hudController?.ShowMessage("Hold E to repair power node", 2f);
            _hudController?.SetRepairProgress(Progress01, !IsRepaired);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_playerInside == null || !other.TryGetComponent(out PlayerController playerController) || playerController != _playerInside)
            {
                return;
            }

            _playerInside = null;
            _hudController?.SetRepairProgress(0f, false);
        }

        private void CompleteRepair()
        {
            IsRepaired = true;
            _progressSeconds = RepairSeconds;
            SetStatusColor(repairedColor);
            _hudController?.SetRepairProgress(1f, false);
            AudioManager.Instance?.PlayRepairComplete(transform.position);
            GameStateManager.Instance?.MarkPowerRepaired(this);
        }

        private void SetStatusColor(Color color)
        {
            if (statusRenderer != null)
            {
                statusRenderer.material.color = color;
            }

            if (statusLight != null)
            {
                statusLight.color = color;
            }
        }
    }
}
