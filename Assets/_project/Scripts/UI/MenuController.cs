using EclipseProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EclipseProtocol.UI
{
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private InputField seedInputField;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(StartGame);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        public void StartGame()
        {
            RunSeedData.SetSeed(seedInputField != null ? seedInputField.text : string.Empty);
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
