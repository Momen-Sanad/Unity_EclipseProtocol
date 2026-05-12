using EclipseProtocol.Audio;
using EclipseProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EclipseProtocol.UI
{
    public class EndSceneController : MonoBehaviour
    {
        [SerializeField] private Button menuButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Text scoreText;
        [SerializeField] private string menuSceneName = "Menu";
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private void Awake()
        {
            if (menuButton != null)
            {
                menuButton.onClick.AddListener(LoadMenu);
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(Retry);
            }

            if (scoreText != null)
            {
                scoreText.text = $"Score {RunScore.LastRunScore:0000}";
            }
        }

        public void LoadMenu()
        {
            AudioManager.Instance?.PlayButtonPress();
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }

        public void Retry()
        {
            AudioManager.Instance?.PlayButtonPress();
            Time.timeScale = 1f;
            RunSeedData.UseRandomSeed();
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
