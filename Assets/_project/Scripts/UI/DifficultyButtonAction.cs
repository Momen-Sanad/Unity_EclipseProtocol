using EclipseProtocol.Audio;
using EclipseProtocol.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EclipseProtocol.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class DifficultyButtonAction : MonoBehaviour
    {
        [SerializeField] private DifficultyMode difficulty = DifficultyMode.Medium;
        [SerializeField] private string gameplaySceneName = "Gameplay";

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(SelectDifficulty);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(SelectDifficulty);
            }
        }

        private void SelectDifficulty()
        {
            AudioManager.Instance?.PlayButtonPress();
            RunDifficultyData.SetDifficulty(difficulty);

            if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
        }
    }
}
