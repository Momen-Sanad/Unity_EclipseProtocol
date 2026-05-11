using EclipseProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EclipseProtocol.UI
{
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private InputField seedInputField;
        [SerializeField] private Dropdown difficultyDropdown;
        [SerializeField] private Button backButton;
        [SerializeField] private string startScreenSceneName = "Start_Screen";

        private void Awake()
        {
            if (seedInputField != null)
            {
                seedInputField.text = RunSeedData.SeedText;
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.value = (int)RunDifficultyData.CurrentDifficulty;
                difficultyDropdown.onValueChanged.AddListener(SetDifficultyFromDropdown);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(ReturnToStartScreen);
            }
        }

        private void OnDestroy()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(ReturnToStartScreen);
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.onValueChanged.RemoveListener(SetDifficultyFromDropdown);
            }
        }

        public void SetEasyDifficulty()
        {
            RunDifficultyData.SetDifficulty(DifficultyMode.Easy);
        }

        public void SetMediumDifficulty()
        {
            RunDifficultyData.SetDifficulty(DifficultyMode.Medium);
        }

        public void SetHardDifficulty()
        {
            RunDifficultyData.SetDifficulty(DifficultyMode.Hard);
        }

        public void ReturnToStartScreen()
        {
            RunSeedData.SetSeed(seedInputField != null ? seedInputField.text : RunSeedData.SeedText);
            Time.timeScale = 1f;
            SceneManager.LoadScene(startScreenSceneName);
        }

        private static void SetDifficultyFromDropdown(int value)
        {
            RunDifficultyData.SetDifficulty((DifficultyMode)Mathf.Clamp(value, 0, 2));
        }
    }
}
