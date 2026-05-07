using UnityEngine;
using UnityEngine.InputSystem;

namespace EclipseProtocol.UI
{
    public class PauseController : MonoBehaviour
    {
        [SerializeField] private HUDController hudController;

        private bool _isPaused;

        public bool IsPaused => _isPaused;

        private void Start()
        {
            if (hudController == null)
            {
                hudController = FindAnyObjectByType<HUDController>();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetPaused(!_isPaused);
            }
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void SetPaused(bool isPaused)
        {
            _isPaused = isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
            hudController?.SetPauseVisible(_isPaused);
        }
    }
}
