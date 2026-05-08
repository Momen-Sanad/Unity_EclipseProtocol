using System;
using EclipseProtocol.UI;
using UnityEngine;

namespace EclipseProtocol.Core
{
    public class RunTimer : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField, Min(1f)] private float fallbackDurationSeconds = 180f;
        [SerializeField] private bool startAutomatically = true;

        private bool _isRunning;
        private bool _hasExpired;

        public event Action Expired;
        public float DurationSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float NormalizedRemaining => DurationSeconds <= 0f ? 0f : Mathf.Clamp01(RemainingSeconds / DurationSeconds);
        public bool HasExpired => _hasExpired;

        private void Awake()
        {
            DurationSeconds = balanceData != null ? balanceData.runTimerSeconds : fallbackDurationSeconds;
            RemainingSeconds = DurationSeconds;
        }

        private void Start()
        {
            GameStateManager.Instance?.RegisterTimer(this);
            FindAnyObjectByType<HUDController>()?.SetTimer(this);

            if (startAutomatically)
            {
                StartTimer();
            }
        }

        private void Update()
        {
            if (!_isRunning || _hasExpired)
            {
                return;
            }

            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
            if (RemainingSeconds <= 0f)
            {
                _hasExpired = true;
                _isRunning = false;
                Expired?.Invoke();
            }
        }

        public void Configure(GameBalanceData data)
        {
            balanceData = data;
            DurationSeconds = balanceData != null ? balanceData.runTimerSeconds : fallbackDurationSeconds;
            RemainingSeconds = DurationSeconds;
            _hasExpired = false;
        }

        public void StartTimer()
        {
            _isRunning = true;
        }

        public void StopTimer()
        {
            _isRunning = false;
        }
    }
}
