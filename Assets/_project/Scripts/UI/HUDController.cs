using EclipseProtocol.Core;
using EclipseProtocol.Player;
using EclipseProtocol.World;
using UnityEngine;
using UnityEngine.UI;

namespace EclipseProtocol.UI
{
    [ExecuteAlways]
    public class HUDController : MonoBehaviour
    {
        private const int PixelHudSegmentCount = 10;
        private const string PixelHudRootName = "PixelStatusHud";
        private const string RepairedNodesTextName = "RepairedNodesText";
        private const string PixelTimerTextName = "PixelTimerText";
        private const string PixelPauseOverlayName = "PixelPauseOverlay";

        [Header("Bars")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image energyFill;
        [SerializeField] private Image dashCooldownFill;
        [SerializeField] private Image repairProgressFill;
        [SerializeField] private Image timerFill;

        [Header("Health HUD Source")]
        [SerializeField] private bool usePackagedHealthHud = true;

        [Header("Text")]
        [SerializeField] private Text healthText;
        [SerializeField] private Text energyText;
        [SerializeField] private Text dashText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text messageText;
        [SerializeField] private GameObject repairProgressRoot;
        [SerializeField] private GameObject pauseOverlay;

        [Header("Bar Colors")]
        [SerializeField] private Color healthLowColor = new Color(1f, 0.1f, 0.08f);
        [SerializeField] private Color healthMidColor = new Color(1f, 0.78f, 0.12f);
        [SerializeField] private Color healthHighColor = new Color(0.25f, 1f, 0.35f);
        [SerializeField] private Color energyLowColor = new Color(0.18f, 0.28f, 0.45f);
        [SerializeField] private Color energyHighColor = new Color(0.2f, 0.78f, 1f);
        [SerializeField] private Color dashEmptyColor = new Color(1f, 0.34f, 0.12f);
        [SerializeField] private Color dashReadyColor = new Color(0.35f, 1f, 0.45f);
        [SerializeField] private Color repairEmptyColor = new Color(0.05f, 0.16f, 0.42f);
        [SerializeField] private Color repairFullColor = new Color(0.1f, 0.55f, 1f);
        [SerializeField] private Color timerFullColor = new Color(0.25f, 0.9f, 1f);
        [SerializeField] private Color timerMidColor = new Color(1f, 0.78f, 0.12f);
        [SerializeField] private Color timerLowColor = new Color(1f, 0.1f, 0.08f);

        [Header("Pixel HUD")]
        [SerializeField] private bool usePixelHud = true;
        [SerializeField] private Font pixelHudFont;
        [SerializeField, Min(0.5f)] private float pixelHudScale = 1.5f;
        [SerializeField] private Color pixelHudPanelColor = new Color(0.04f, 0.1f, 0.2f, 0.82f);
        [SerializeField] private Color pixelHudBorderColor = new Color(0.38f, 0.58f, 0.9f, 1f);
        [SerializeField] private Color pixelHudHealthColor = new Color(0.35f, 1f, 0.52f, 1f);
        [SerializeField] private Color pixelHudEnergyColor = new Color(1f, 0.86f, 0.28f, 1f);
        [SerializeField] private Color pixelHudEmptySegmentColor = new Color(0.1f, 0.18f, 0.32f, 0.95f);

        [Header("Dash HUD")]
        [SerializeField] private Sprite dashIconSprite;
        [SerializeField] private Vector2 dashIconAnchoredPosition = new Vector2(24f, 24f);
        [SerializeField, Min(24f)] private float dashIconSize = 600f;
        [SerializeField] private Color dashReadyIconColor = Color.white;
        [SerializeField] private Color dashCooldownIconColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        private PlayerController _player;
        private RunTimer _timer;
        private RunScore _score;
        private EnergyCellSystem _energyCellSystem;
        private float _messageTimer;
        private Image[] _pixelHealthSegments;
        private Image[] _pixelEnergySegments;
        private Text _pixelCellCountText;
        private Image _dashIconImage;
        private Text _dashCooldownText;
        private Text _repairedNodesText;
        private Text _pixelTimerText;
        private int _collectedCellCount;

        public GameObject PauseOverlay => pauseOverlay;

        private void OnValidate()
        {
            EnsurePixelHudState();
        }

        private void Awake()
        {
            EnsurePixelHudState();
            if (Application.isPlaying)
            {
                ConfigureRuntimeHud();
            }
        }

        private void EnsurePixelHudState()
        {
            if (usePixelHud)
            {
                BuildPixelHud();
                SetClassicResourceHudVisible(false);
                return;
            }

            SetPixelHudVisible(false);
            SetClassicResourceHudVisible(true);
        }

        private void ConfigureRuntimeHud()
        {
            if (usePackagedHealthHud)
            {
                SetLegacyHealthHudVisible(false);
            }
            else
            {
                ConfigureFillImage(healthFill, healthHighColor);
            }

            ConfigureFillImage(energyFill, energyHighColor);
            ConfigureFillImage(dashCooldownFill, dashReadyColor);
            ConfigureFillImage(repairProgressFill, repairEmptyColor);
            ConfigureFillImage(timerFill, timerFullColor);
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

        private void OnEnable()
        {
            EnsurePixelHudState();
            if (Application.isPlaying)
            {
                SetEnergyCellSystem(EnergyCellSystem.Instance);
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                SetEnergyCellSystem(null);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UpdatePlayerStats();
            UpdateTimer();
            UpdateScore();
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

        public void SetScore(RunScore score)
        {
            _score = score;
            EnsureScoreText();
            UpdateScore();
        }

        public void SetEnergyCellSystem(EnergyCellSystem energyCellSystem)
        {
            if (_energyCellSystem == energyCellSystem)
            {
                return;
            }

            if (_energyCellSystem != null)
            {
                _energyCellSystem.EnergyRestored -= HandleEnergyRestored;
            }

            _energyCellSystem = energyCellSystem;

            if (_energyCellSystem != null)
            {
                _energyCellSystem.EnergyRestored += HandleEnergyRestored;
            }
        }

        public void SetObjective(string text)
        {
            if (objectiveText != null)
            {
                objectiveText.text = text;
            }
        }

        public void SetRepairedNodes(int repairedCount, int totalCount)
        {
            if (_repairedNodesText == null && transform != null)
            {
                Transform repairedText = transform.Find(RepairedNodesTextName);
                _repairedNodesText = repairedText != null ? repairedText.GetComponent<Text>() : null;
            }

            if (_repairedNodesText != null)
            {
                _repairedNodesText.text = $"Repaired nodes: {Mathf.Max(0, repairedCount)}/{Mathf.Max(0, totalCount)}";
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
                float repair01 = Mathf.Clamp01(normalizedProgress);
                repairProgressFill.fillAmount = repair01;
                repairProgressFill.color = Color.Lerp(repairEmptyColor, repairFullColor, repair01);
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

            if (!usePackagedHealthHud && healthFill != null)
            {
                float clampedHealth = Mathf.Clamp01(health01);
                healthFill.fillAmount = clampedHealth;
                healthFill.color = EvaluateThreePointColor(clampedHealth, healthLowColor, healthMidColor, healthHighColor);
            }

            if (energyFill != null)
            {
                float clampedEnergy = Mathf.Clamp01(energy01);
                energyFill.fillAmount = clampedEnergy;
                energyFill.color = Color.Lerp(energyLowColor, energyHighColor, clampedEnergy);
            }

            if (dashCooldownFill != null)
            {
                dashCooldownFill.fillAmount = dashReady01;
                dashCooldownFill.color = Color.Lerp(dashEmptyColor, dashReadyColor, dashReady01);
            }

            UpdateDashHud(_player.DashCooldownRemaining);

            if (!usePackagedHealthHud && healthText != null)
            {
                healthText.text = $"HP {Mathf.CeilToInt(_player.CurrentHealth)}/{Mathf.CeilToInt(_player.MaxHealth)}";
            }

            if (!usePixelHud && energyText != null)
            {
                energyText.text = $"Energy {Mathf.CeilToInt(_player.CurrentEnergy)}/{Mathf.CeilToInt(_player.MaxEnergy)}";
            }

            if (!usePixelHud && dashText != null)
            {
                dashText.text = _player.DashCooldownRemaining > 0f
                    ? $"Shift Dash {Mathf.CeilToInt(_player.DashCooldownRemaining)}s"
                    : "Shift Dash Ready";
            }

            UpdatePixelHud(health01, energy01);
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
                timerFill.color = EvaluateThreePointColor(remaining01, timerLowColor, timerMidColor, timerFullColor);
            }

            if (timerText != null)
            {
                int seconds = Mathf.CeilToInt(_timer.RemainingSeconds);
                int minutes = seconds / 60;
                int remainder = seconds % 60;
                timerText.text = $"{minutes:00}:{remainder:00}";
            }

            if (_pixelTimerText != null)
            {
                int seconds = Mathf.CeilToInt(_timer.RemainingSeconds);
                int minutes = seconds / 60;
                int remainder = seconds % 60;
                _pixelTimerText.text = $"{minutes:00}:{remainder:00}";
            }
        }

        private void UpdateScore()
        {
            if (_score == null)
            {
                return;
            }

            EnsureScoreText();
            if (scoreText != null)
            {
                scoreText.text = $"Score {_score.CurrentScore:0000}";
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

        private void HandleEnergyRestored(EnergyCellPickup pickup, PlayerController player, float restoredEnergy)
        {
            _collectedCellCount++;
            if (_pixelCellCountText != null)
            {
                _pixelCellCountText.text = _collectedCellCount.ToString("00");
            }

            ShowEnergyGain(restoredEnergy);
        }

        private void BuildPixelHud()
        {
            if (transform == null)
            {
                return;
            }

            Font hudFont = pixelHudFont != null ? pixelHudFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (hudFont == null)
            {
                Debug.LogError("[HUDController] Pixel HUD needs a font assigned.", this);
                return;
            }

            Transform existingRoot = transform.Find(PixelHudRootName);
            if (existingRoot != null)
            {
                existingRoot.gameObject.SetActive(true);
                if (existingRoot is RectTransform existingRect)
                {
                    ConfigurePixelRoot(existingRect);
                }

                CollectPixelHudReferences(existingRoot);
                BuildDashHud(hudFont);
                BuildRepairedNodesText(hudFont);
                BuildPixelTimerText(hudFont);
                BuildPauseOverlay(hudFont);
                return;
            }

            RectTransform root = CreateRect(PixelHudRootName, transform);
            ConfigurePixelRoot(root);

            Image panel = root.gameObject.AddComponent<Image>();
            panel.color = pixelHudPanelColor;

            AddOutline(root, pixelHudBorderColor, 2f);

            _pixelHealthSegments = CreatePixelBar(root, "Health", new Vector2(12f, -14f), "HEALTH", pixelHudHealthColor, hudFont);
            _pixelEnergySegments = CreatePixelBar(root, "Energy", new Vector2(12f, -54f), "ENERGY", pixelHudEnergyColor, hudFont);

            RectTransform cellsFrame = CreateRect("CellsFrame", root);
            cellsFrame.anchorMin = new Vector2(0f, 1f);
            cellsFrame.anchorMax = new Vector2(0f, 1f);
            cellsFrame.pivot = new Vector2(0f, 1f);
            cellsFrame.anchoredPosition = new Vector2(12f, -96f);
            cellsFrame.sizeDelta = new Vector2(150f, 32f);
            Image cellsFrameImage = cellsFrame.gameObject.AddComponent<Image>();
            cellsFrameImage.color = new Color(0.04f, 0.12f, 0.24f, 0.92f);
            AddOutline(cellsFrame, pixelHudBorderColor, 1.5f);

            Text cellsLabel = CreatePixelText("CellsLabel", cellsFrame, hudFont, "CELLS", 14, Color.white);
            RectTransform cellsLabelRect = cellsLabel.rectTransform;
            cellsLabelRect.anchorMin = new Vector2(0f, 0f);
            cellsLabelRect.anchorMax = new Vector2(0f, 1f);
            cellsLabelRect.pivot = new Vector2(0f, 0.5f);
            cellsLabelRect.anchoredPosition = new Vector2(12f, 0f);
            cellsLabelRect.sizeDelta = new Vector2(82f, 0f);

            _pixelCellCountText = CreatePixelText("CellsCount", cellsFrame, hudFont, "00", 14, Color.white);
            RectTransform cellsCountRect = _pixelCellCountText.rectTransform;
            cellsCountRect.anchorMin = new Vector2(1f, 0f);
            cellsCountRect.anchorMax = new Vector2(1f, 1f);
            cellsCountRect.pivot = new Vector2(1f, 0.5f);
            cellsCountRect.anchoredPosition = new Vector2(-12f, 0f);
            cellsCountRect.sizeDelta = new Vector2(44f, 0f);
            _pixelCellCountText.alignment = TextAnchor.MiddleRight;

            BuildDashHud(hudFont);
            BuildRepairedNodesText(hudFont);
            BuildPixelTimerText(hudFont);
            BuildPauseOverlay(hudFont);
        }

        private void ConfigurePixelRoot(RectTransform root)
        {
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(8f, -8f);
            root.sizeDelta = new Vector2(390f, 138f);
            root.localScale = new Vector3(pixelHudScale, pixelHudScale, 1f);
        }

        private Image[] CreatePixelBar(RectTransform parent, string name, Vector2 position, string label, Color fillColor, Font font)
        {
            RectTransform frame = CreateRect(name + "Frame", parent);
            frame.anchorMin = new Vector2(0f, 1f);
            frame.anchorMax = new Vector2(0f, 1f);
            frame.pivot = new Vector2(0f, 1f);
            frame.anchoredPosition = position;
            frame.sizeDelta = new Vector2(360f, 32f);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.05f, 0.13f, 0.24f, 0.95f);
            AddOutline(frame, pixelHudBorderColor, 1.5f);

            Text labelText = CreatePixelText(name + "Label", frame, font, label, 14, fillColor);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(12f, 0f);
            labelRect.sizeDelta = new Vector2(104f, 0f);

            Image[] segments = new Image[PixelHudSegmentCount];
            for (int i = 0; i < segments.Length; i++)
            {
                RectTransform segment = CreateRect(name + "Segment_" + (i + 1).ToString("00"), frame);
                segment.anchorMin = new Vector2(0f, 0.5f);
                segment.anchorMax = new Vector2(0f, 0.5f);
                segment.pivot = new Vector2(0f, 0.5f);
                segment.anchoredPosition = new Vector2(120f + i * 23f, 0f);
                segment.sizeDelta = new Vector2(17f, 18f);
                Image segmentImage = segment.gameObject.AddComponent<Image>();
                segmentImage.color = fillColor;
                segments[i] = segmentImage;
            }

            return segments;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            return rectObject.GetComponent<RectTransform>();
        }

        private static Text CreatePixelText(string name, Transform parent, Font font, string value, int size, Color color)
        {
            RectTransform textRect = CreateRect(name, parent);
            Text text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void AddOutline(RectTransform target, Color color, float distance)
        {
            Outline outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private void BuildDashHud(Font hudFont)
        {
            const string dashRootName = "DashCooldownIcon";

            Transform existingRoot = transform.Find(dashRootName);
            if (existingRoot != null)
            {
                existingRoot.gameObject.SetActive(true);
                if (existingRoot is RectTransform existingRect)
                {
                    ConfigureDashRoot(existingRect);
                }

                CollectDashHudReferences(existingRoot);
                ConfigureDashHudVisuals();
                return;
            }

            RectTransform root = CreateRect(dashRootName, transform);
            ConfigureDashRoot(root);

            _dashIconImage = root.gameObject.AddComponent<Image>();
            _dashIconImage.raycastTarget = false;
            _dashIconImage.preserveAspect = true;

            _dashCooldownText = CreatePixelText("DashCooldownText", root, hudFont, string.Empty, 26, Color.white);
            RectTransform textRect = _dashCooldownText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _dashCooldownText.alignment = TextAnchor.MiddleCenter;
            AddOutline(textRect, Color.black, 2f);

            ConfigureDashHudVisuals();
        }

        private void ConfigureDashRoot(RectTransform root)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = dashIconAnchoredPosition;
            root.sizeDelta = new Vector2(dashIconSize, dashIconSize);
            root.localScale = Vector3.one;
        }

        private void ConfigureDashHudVisuals()
        {
            if (_dashIconImage != null)
            {
                _dashIconImage.sprite = dashIconSprite;
                _dashIconImage.color = dashReadyIconColor;
            }

            if (_dashCooldownText != null)
            {
                _dashCooldownText.gameObject.SetActive(false);
                _dashCooldownText.text = string.Empty;
            }
        }

        private void CollectDashHudReferences(Transform root)
        {
            _dashIconImage = root.GetComponent<Image>();
            Transform cooldownText = root.Find("DashCooldownText");
            _dashCooldownText = cooldownText != null ? cooldownText.GetComponent<Text>() : null;
        }

        private void BuildRepairedNodesText(Font hudFont)
        {
            Transform existingText = transform.Find(RepairedNodesTextName);
            if (existingText != null)
            {
                existingText.gameObject.SetActive(true);
                _repairedNodesText = existingText.GetComponent<Text>();
                if (_repairedNodesText != null)
                {
                    ConfigureRepairedNodesText(_repairedNodesText);
                }
                return;
            }

            _repairedNodesText = CreatePixelText(RepairedNodesTextName, transform, hudFont, "Repaired nodes: 0/0", 22, Color.white);
            ConfigureRepairedNodesText(_repairedNodesText);
            AddOutline(_repairedNodesText.rectTransform, Color.black, 2f);
        }

        private static void ConfigureRepairedNodesText(Text repairedText)
        {
            repairedText.alignment = TextAnchor.MiddleCenter;
            repairedText.horizontalOverflow = HorizontalWrapMode.Overflow;
            repairedText.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform textRect = repairedText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -12f);
            textRect.sizeDelta = new Vector2(520f, 40f);
            textRect.localScale = Vector3.one;
        }

        private void BuildPixelTimerText(Font hudFont)
        {
            Transform existingText = transform.Find(PixelTimerTextName);
            if (existingText != null)
            {
                existingText.gameObject.SetActive(true);
                _pixelTimerText = existingText.GetComponent<Text>();
                if (_pixelTimerText != null)
                {
                    ConfigurePixelTimerText(_pixelTimerText);
                }
                return;
            }

            _pixelTimerText = CreatePixelText(PixelTimerTextName, transform, hudFont, "00:00", 22, Color.white);
            ConfigurePixelTimerText(_pixelTimerText);
            AddOutline(_pixelTimerText.rectTransform, Color.black, 2f);
        }

        private static void ConfigurePixelTimerText(Text timer)
        {
            timer.alignment = TextAnchor.MiddleRight;
            timer.horizontalOverflow = HorizontalWrapMode.Overflow;
            timer.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform textRect = timer.rectTransform;
            textRect.anchorMin = new Vector2(1f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(1f, 1f);
            textRect.anchoredPosition = new Vector2(-16f, -12f);
            textRect.sizeDelta = new Vector2(180f, 40f);
            textRect.localScale = Vector3.one;
        }

        private void BuildPauseOverlay(Font hudFont)
        {
            Transform existingOverlay = transform.Find(PixelPauseOverlayName);
            if (existingOverlay != null)
            {
                pauseOverlay = existingOverlay.gameObject;
                ConfigurePauseOverlay(existingOverlay as RectTransform);
                Transform pauseText = existingOverlay.Find("PauseText");
                if (pauseText != null && pauseText.TryGetComponent(out Text existingText))
                {
                    ConfigurePauseText(existingText);
                }
                pauseOverlay.SetActive(false);
                return;
            }

            RectTransform overlay = CreateRect(PixelPauseOverlayName, transform);
            pauseOverlay = overlay.gameObject;
            ConfigurePauseOverlay(overlay);

            Image overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.45f);
            overlayImage.raycastTarget = false;

            Text createdText = CreatePixelText("PauseText", overlay, hudFont, "pasued", 42, Color.white);
            ConfigurePauseText(createdText);
            AddOutline(createdText.rectTransform, Color.black, 3f);

            pauseOverlay.SetActive(false);
        }

        private static void ConfigurePauseOverlay(RectTransform overlay)
        {
            if (overlay == null)
            {
                return;
            }

            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.pivot = new Vector2(0.5f, 0.5f);
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.localScale = Vector3.one;
        }

        private static void ConfigurePauseText(Text text)
        {
            text.text = "pasued";
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(360f, 96f);
            textRect.localScale = Vector3.one;
        }

        private void UpdatePixelHud(float health01, float energy01)
        {
            SetPixelSegments(_pixelHealthSegments, health01, pixelHudHealthColor);
            SetPixelSegments(_pixelEnergySegments, energy01, pixelHudEnergyColor);
        }

        private void UpdateDashHud(float cooldownRemaining)
        {
            if (_dashIconImage == null && transform != null)
            {
                Transform dashRoot = transform.Find("DashCooldownIcon");
                if (dashRoot != null)
                {
                    CollectDashHudReferences(dashRoot);
                }
            }

            bool isCoolingDown = cooldownRemaining > 0.05f;
            if (_dashIconImage != null)
            {
                _dashIconImage.color = isCoolingDown ? dashCooldownIconColor : dashReadyIconColor;
            }

            if (_dashCooldownText != null)
            {
                _dashCooldownText.gameObject.SetActive(isCoolingDown);
                _dashCooldownText.text = isCoolingDown ? Mathf.CeilToInt(cooldownRemaining).ToString() : string.Empty;
            }
        }

        private void CollectPixelHudReferences(Transform root)
        {
            _pixelHealthSegments = CollectSegments(root, "HealthFrame", "HealthSegment_");
            _pixelEnergySegments = CollectSegments(root, "EnergyFrame", "EnergySegment_");
            Transform cellsCount = root.Find("CellsFrame/CellsCount");
            _pixelCellCountText = cellsCount != null ? cellsCount.GetComponent<Text>() : null;
        }

        private static Image[] CollectSegments(Transform root, string frameName, string segmentPrefix)
        {
            Image[] segments = new Image[PixelHudSegmentCount];
            for (int i = 0; i < segments.Length; i++)
            {
                Transform segment = root.Find(frameName + "/" + segmentPrefix + (i + 1).ToString("00"));
                segments[i] = segment != null ? segment.GetComponent<Image>() : null;
            }

            return segments;
        }

        private void SetPixelSegments(Image[] segments, float value01, Color activeColor)
        {
            if (segments == null)
            {
                return;
            }

            int activeCount = Mathf.CeilToInt(Mathf.Clamp01(value01) * segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].color = i < activeCount ? activeColor : pixelHudEmptySegmentColor;
                }
            }
        }

        private void EnsureScoreText()
        {
            if (scoreText != null || timerText == null)
            {
                return;
            }

            scoreText = Instantiate(timerText, timerText.transform.parent);
            scoreText.name = "ScoreText";
            scoreText.text = string.Empty;

            if (scoreText.transform is RectTransform scoreRect && timerText.transform is RectTransform timerRect)
            {
                scoreRect.anchorMin = timerRect.anchorMin;
                scoreRect.anchorMax = timerRect.anchorMax;
                scoreRect.pivot = timerRect.pivot;
                scoreRect.anchoredPosition = timerRect.anchoredPosition + new Vector2(0f, -28f);
                scoreRect.sizeDelta = timerRect.sizeDelta;
            }
        }

        private void SetClassicResourceHudVisible(bool isVisible)
        {
            SetImageRootVisible(healthFill, isVisible);
            SetImageRootVisible(energyFill, isVisible);
            SetImageRootVisible(dashCooldownFill, isVisible);

            if (healthText != null)
            {
                healthText.gameObject.SetActive(isVisible);
            }

            if (energyText != null)
            {
                energyText.gameObject.SetActive(isVisible);
            }

            if (dashText != null)
            {
                dashText.gameObject.SetActive(isVisible);
            }
        }

        private void SetLegacyHealthHudVisible(bool isVisible)
        {
            if (healthFill != null)
            {
                SetImageRootVisible(healthFill, isVisible);
            }

            if (healthText != null)
            {
                healthText.gameObject.SetActive(isVisible);
            }
        }

        private void SetPixelHudVisible(bool isVisible)
        {
            Transform pixelHud = transform != null ? transform.Find(PixelHudRootName) : null;
            if (pixelHud != null)
            {
                pixelHud.gameObject.SetActive(isVisible);
            }

            Transform dashHud = transform != null ? transform.Find("DashCooldownIcon") : null;
            if (dashHud != null)
            {
                dashHud.gameObject.SetActive(isVisible);
            }

            Transform repairedNodesText = transform != null ? transform.Find(RepairedNodesTextName) : null;
            if (repairedNodesText != null)
            {
                repairedNodesText.gameObject.SetActive(isVisible);
            }

            Transform pixelTimerText = transform != null ? transform.Find(PixelTimerTextName) : null;
            if (pixelTimerText != null)
            {
                pixelTimerText.gameObject.SetActive(isVisible);
            }
        }

        private static void SetImageRootVisible(Image image, bool isVisible)
        {
            if (image == null)
            {
                return;
            }

            Transform root = image.transform.parent != null ? image.transform.parent : image.transform;
            root.gameObject.SetActive(isVisible);
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

        private static Color EvaluateThreePointColor(float value, Color low, Color mid, Color high)
        {
            float clampedValue = Mathf.Clamp01(value);
            if (clampedValue < 0.5f)
            {
                return Color.Lerp(low, mid, clampedValue * 2f);
            }

            return Color.Lerp(mid, high, (clampedValue - 0.5f) * 2f);
        }
    }
}
