using EclipseProtocol.Player;
using EclipseProtocol.UI;
using UnityEngine;

namespace EclipseProtocol.Core
{
    public class RunScore : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField, Min(0)] private int fallbackStartingScore = 5000;
        [SerializeField, Min(0f)] private float fallbackScoreLossPerSecond = 10f;
        [SerializeField, Min(0f)] private float fallbackScoreLossPerHealthPoint = 25f;
        [SerializeField] private bool startAutomatically = true;

        private PlayerController _player;
        private bool _isRunning;
        private float _score;

        public static int LastRunScore { get; private set; }
        public int CurrentScore => Mathf.Max(0, Mathf.CeilToInt(_score));
        public bool IsRunning => _isRunning;

        private float StartingScore => balanceData != null ? balanceData.startingScore : fallbackStartingScore;
        private float ScoreLossPerSecond => balanceData != null ? balanceData.scoreLossPerSecond : fallbackScoreLossPerSecond;
        private float ScoreLossPerHealthPoint => balanceData != null ? balanceData.scoreLossPerHealthPoint : fallbackScoreLossPerHealthPoint;

        private void Awake()
        {
            ResetScore();
        }

        private void Start()
        {
            GameStateManager.Instance?.RegisterScore(this);
            FindAnyObjectByType<HUDController>()?.SetScore(this);

            if (startAutomatically)
            {
                StartScoring();
            }
        }

        private void OnDisable()
        {
            SetPlayer(null);
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            DeductScore(ScoreLossPerSecond * Time.deltaTime);
        }

        public void Configure(GameBalanceData data)
        {
            balanceData = data;
            ResetScore();
        }

        public void SetPlayer(PlayerController player)
        {
            if (_player == player)
            {
                return;
            }

            if (_player != null)
            {
                _player.DamageTaken -= HandlePlayerDamageTaken;
            }

            _player = player;

            if (_player != null)
            {
                _player.DamageTaken += HandlePlayerDamageTaken;
            }
        }

        public void StartScoring()
        {
            _isRunning = true;
        }

        public void StopScoring()
        {
            _isRunning = false;
            LastRunScore = CurrentScore;
        }

        public void ResetScore()
        {
            _score = StartingScore;
            LastRunScore = CurrentScore;
        }

        private void HandlePlayerDamageTaken(float healthLost)
        {
            DeductScore(healthLost * ScoreLossPerHealthPoint);
        }

        private void DeductScore(float amount)
        {
            if (amount <= 0f || _score <= 0f)
            {
                return;
            }

            _score = Mathf.Max(0f, _score - amount);
        }
    }
}
