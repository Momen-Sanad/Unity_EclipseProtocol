using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EclipseProtocol.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class SceneButtonAction : MonoBehaviour
    {
        private enum ButtonAction
        {
            LoadScene,
            QuitGame
        }

        [SerializeField] private ButtonAction action = ButtonAction.LoadScene;
        [SerializeField] private string sceneName;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Execute);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(Execute);
            }
        }

        private void Execute()
        {
            if (action == ButtonAction.QuitGame)
            {
                QuitGame();
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"[{nameof(SceneButtonAction)}] No scene name assigned.", this);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
