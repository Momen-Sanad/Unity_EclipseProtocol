using EclipseProtocol.Core;
using EclipseProtocol.Player;
using UnityEngine;
using UnityEngine.UI;

namespace EclipseProtocol.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Bars")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image energyFill;
        [SerializeField] private Image dashCooldownFill;
        [SerializeField] private Image repairProgressFill;
        [SerializeField] private Image timerFill;

        [Header("Text")]
        [SerializeField] private Text healthText;
        [SerializeField] private Text energyText;
        [SerializeField] private Text dashText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text messageText;
        [SerializeField] private GameObject repairProgressRoot;
        [SerializeField] private GameObject pauseOverlay;

        [Header("Bar Colors")]
        [SerializeField] private Color healthColor = new Color(0.25f, 1f, 0.35f);
        [SerializeField] private Color energyColor = new Color(0.2f, 0.78f, 1f);
        [SerializeField] private Color dashCooldownColor = new Color(1f, 0.7f, 0.2f);
        [SerializeField] private Color dashReadyColor = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private Color repairColor = new Color(0.1f, 0.45f, 1f);
        [SerializeField] private Color timerStartColor = new Color(0.25f, 0.9f, 1f);
        [SerializeField] private Color timerEndColor = new Color(1f, 0.1f, 0.08f);

        private PlayerController _player;
        private RunTimer _timer;
        private float _messageTimer;

        public GameObject PauseOverlay => pauseOverlay;

        private void Awake()
        {
            ConfigureFillImage(healthFill, healthColor);
            ConfigureFillImage(energyFill, energyColor);
            ConfigureFillImage(dashCooldownFill, dashReadyColor);
            ConfigureFillImage(repairProgressFill, repairColor);
            ConfigureFillImage(timerFill, timerStartColor);
            SetRepairProgress(0f, false);
            if (pauseOverlay != null)
            {
                pauseOverlay.SetActive(false);
            }

            if (messageText != null)
            {
                messageText.text = string.Empty;
            }
        }

        private void Update()
        {
            UpdatePlayerStats();
            UpdateTimer();
            UpdateMessage();
        }

        public void SetPlayer(PlayerController player)
        {
            _player = player;
        }

        public void SetTimer(RunTimer timer)
        {
            _timer = timer;
        }

        public void SetObjective(string text)
        {
            if (objectiveText != null)
            {
                objectiveText.text = text;
            }
        }

        public void ShowMessage(string text, float duration)
        {
            if (messageText == null)
            {
                return;
            }

            messageText.text = text;
            _messageTimer = duration;
        }

        public void ShowEnergyGain(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            ShowMessage($"+{Mathf.CeilToInt(amount)}", 1.2f);
        }

        public void SetRepairProgress(float normalizedProgress, bool visible)
        {
            if (repairProgressRoot != null)
            {
                repairProgressRoot.SetActive(visible);
            }

            if (repairProgressFill != null)
            {
                repairProgressFill.fillAmount = Mathf.Clamp01(normalizedProgress);
                repairProgressFill.color = repairColor;
            }
        }

        public void SetPauseVisible(bool isVisible)
        {
            if (pauseOverlay != null)
            {
                pauseOverlay.SetActive(isVisible);
            }
        }

        private void UpdatePlayerStats()
        {
            if (_player == null)
            {
                return;
            }

            float health01 = _player.MaxHealth <= 0f ? 0f : _player.CurrentHealth / _player.MaxHealth;
            float energy01 = _player.MaxEnergy <= 0f ? 0f : _player.CurrentEnergy / _player.MaxEnergy;
            float dashCooldown01 = _player.DashCooldownDuration <= 0f ? 0f : _player.DashCooldownRemaining / _player.DashCooldownDuration;
            float dashReady01 = 1f - Mathf.Clamp01(dashCooldown01);

            if (healthFill != null)
            {
                healthFill.fillAmount = Mathf.Clamp01(health01);
                healthFill.color = healthColor;
            }

            if (energyFill != null)
            {
                energyFill.fillAmount = Mathf.Clamp01(energy01);
                energyFill.color = energyColor;
            }

            if (dashCooldownFill != null)
            {
                dashCooldownFill.fillAmount = dashReady01;
                dashCooldownFill.color = _player.DashCooldownRemaining > 0f ? dashCooldownColor : dashReadyColor;
            }

            if (healthText != null)
            {
                healthText.text = $"HP {Mathf.CeilToInt(_player.CurrentHealth)}/{Mathf.CeilToInt(_player.MaxHealth)}";
            }

            if (energyText != null)
            {
                energyText.text = $"Energy {Mathf.CeilToInt(_player.CurrentEnergy)}/{Mathf.CeilToInt(_player.MaxEnergy)}";
            }

            if (dashText != null)
            {
                dashText.text = _player.DashCooldownRemaining > 0f
                    ? $"Shift Dash {Mathf.CeilToInt(_player.DashCooldownRemaining)}s"
                    : "Shift Dash Ready";
            }
        }

        private void UpdateTimer()
        {
            if (_timer == null)
            {
                return;
            }

            if (timerFill != null)
            {
                float remaining01 = Mathf.Clamp01(_timer.NormalizedRemaining);
                timerFill.fillAmount = remaining01;
                timerFill.color = Color.Lerp(timerStartColor, timerEndColor, 1f - remaining01);
            }

            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(_timer.RemainingSeconds);
                int minutes = seconds / 60;
                int remainder = seconds % 60;
                timerText.text = $"{minutes:00}:{remainder:00}";
            }
        }

        private void UpdateMessage()
        {
            if (messageText == null || _messageTimer <= 0f)
            {
                return;
            }

            _messageTimer -= Time.deltaTime;
            if (_messageTimer <= 0f)
            {
                messageText.text = string.Empty;
            }
        }

        private static void ConfigureFillImage(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillClockwise = true;
            image.fillAmount = Mathf.Clamp01(image.fillAmount);
            image.color = color;
        }
    }
}
