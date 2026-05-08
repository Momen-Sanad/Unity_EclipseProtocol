using EclipseProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EclipseProtocol.UI
{
    public class MenuController : MonoBehaviour
    {
        [SerializeField] private InputField seedInputField;
        [SerializeField] private Button backButton;
        [SerializeField] private string startScreenSceneName = "Start_Screen";

        private void Awake()
        {
            if (seedInputField != null)
            {
                seedInputField.text = RunSeedData.SeedText;
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
        }

        public void ReturnToStartScreen()
        {
            RunSeedData.SetSeed(seedInputField != null ? seedInputField.text : RunSeedData.SeedText);
            Time.timeScale = 1f;
            SceneManager.LoadScene(startScreenSceneName);
        }
    }
}
