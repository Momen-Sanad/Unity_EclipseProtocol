using System.Collections.Generic;
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
        private readonly HashSet<RepairNode> _repairNodes = new HashSet<RepairNode>();
        private readonly HashSet<RepairNode> _repairedNodes = new HashSet<RepairNode>();
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
            if (_repairNodes.Count > 0)
            {
                UpdateRepairObjective();
            }
            else
            {
                hudController?.SetObjective("Repair the power node");
            }
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

        public void ResetRunObjectives()
        {
            IsPowerRepaired = false;
            _repairNodes.Clear();
            _repairedNodes.Clear();
            hudController?.SetObjective("Repair node 1");
        }

        public void RegisterRepairNode(RepairNode repairNode)
        {
            if (repairNode == null || !_repairNodes.Add(repairNode))
            {
                return;
            }

            UpdateRepairObjective();
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

            if (repairNode != null)
            {
                _repairNodes.Add(repairNode);
                _repairedNodes.Add(repairNode);
            }

            if (_repairNodes.Count > 0 && _repairedNodes.Count < _repairNodes.Count)
            {
                UpdateRepairObjective();
                hudController?.ShowMessage("Door unlocked. Move forward.", 2f);
                return;
            }

            IsPowerRepaired = true;
            hudController?.SetObjective("Reach extraction");
            hudController?.ShowMessage("All nodes repaired. Extraction unlocked.", 2.5f);

            ExtractionTrigger[] extractionTriggers = FindObjectsByType<ExtractionTrigger>();
            for (int i = 0; i < extractionTriggers.Length; i++)
            {
                extractionTriggers[i].SetLocked(false);
            }
        }

        private void UpdateRepairObjective()
        {
            int total = Mathf.Max(1, _repairNodes.Count);
            int nextNode = Mathf.Clamp(_repairedNodes.Count + 1, 1, total);
            hudController?.SetObjective($"Repair node {nextNode}/{total}");
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
