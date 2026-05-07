using EclipseProtocol.Audio;
using EclipseProtocol.Player;
using EclipseProtocol.UI;
using EclipseProtocol.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EclipseProtocol.Core
{
    public class GameStateManager : MonoBehaviour
    {
        [SerializeField] private string victorySceneName = "Victory";
        [SerializeField] private string lossSceneName = "Loss";
        [SerializeField] private HUDController hudController;
        [SerializeField] private RunTimer runTimer;

        private PlayerController _player;
        private bool _endingRun;

        public static GameStateManager Instance { get; private set; }
        public bool IsPowerRepaired { get; private set; }
        public bool IsRunActive => !_endingRun;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            if (hudController == null)
            {
                hudController = FindAnyObjectByType<HUDController>();
            }

            if (runTimer == null)
            {
                runTimer = FindAnyObjectByType<RunTimer>();
            }

            RegisterTimer(runTimer);
            hudController?.SetObjective("Repair the power node");
        }

        private void Update()
        {
            if (_endingRun || _player == null)
            {
                return;
            }

            if (_player.CurrentHealth <= 0f)
            {
                TriggerLoss();
            }
        }

        public void RegisterPlayer(PlayerController player)
        {
            _player = player;
            hudController?.SetPlayer(player);
        }

        public void RegisterTimer(RunTimer timer)
        {
            if (timer == null)
            {
                return;
            }

            if (runTimer != null)
            {
                runTimer.Expired -= TriggerLoss;
            }

            runTimer = timer;
            runTimer.Expired += TriggerLoss;
            hudController?.SetTimer(runTimer);
        }

        public void MarkPowerRepaired(RepairNode repairNode)
        {
            if (IsPowerRepaired)
            {
                return;
            }

            IsPowerRepaired = true;
            hudController?.SetObjective("Reach extraction");
            hudController?.ShowMessage("Power restored. Extraction unlocked.", 2.5f);

            ExtractionTrigger[] extractionTriggers = FindObjectsByType<ExtractionTrigger>();
            for (int i = 0; i < extractionTriggers.Length; i++)
            {
                extractionTriggers[i].SetLocked(false);
            }
        }

        public void TryCompleteExtraction()
        {
            if (_endingRun)
            {
                return;
            }

            if (!IsPowerRepaired)
            {
                hudController?.ShowMessage("Repair the power node before extraction.", 2f);
                return;
            }

            _endingRun = true;
            runTimer?.StopTimer();
            Time.timeScale = 1f;
            AudioManager.Instance?.PlayVictory(Vector3.zero);
            SceneManager.LoadScene(victorySceneName);
        }

        public void TriggerLoss()
        {
            if (_endingRun)
            {
                return;
            }

            _endingRun = true;
            runTimer?.StopTimer();
            Time.timeScale = 1f;
            AudioManager.Instance?.PlayLoss(Vector3.zero);
            SceneManager.LoadScene(lossSceneName);
        }
    }
}
