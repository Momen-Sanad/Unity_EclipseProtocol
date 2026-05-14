using System.Collections;
using UnityEngine;

namespace EclipseProtocol.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerDamageFeedback : MonoBehaviour
    {
        [Header("Flash")]
        [SerializeField, Min(0f)] private float flashDuration = 1f;
        [SerializeField, Min(0.01f)] private float flashInterval = 0.12f;

        private PlayerController _playerController;
        private Renderer[] _renderers;
        private bool[] _rendererEnabledStates;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();

            RefreshRendererCache();
        }

        private void OnEnable()
        {
            if (_playerController == null)
            {
                _playerController = GetComponent<PlayerController>();
            }

            if (_playerController != null)
            {
                _playerController.DamageTaken += HandleDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (_playerController != null)
            {
                _playerController.DamageTaken -= HandleDamageTaken;
            }

            StopFlash();
        }

        private void OnDestroy()
        {
            StopFlash();
        }

        private void HandleDamageTaken(float healthLost)
        {
            StartFlash();
        }

        private void StartFlash()
        {
            RefreshRendererCache();

            if (flashDuration <= 0f || _renderers.Length == 0)
            {
                return;
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                RestoreRendererVisibility();
            }

            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float endTime = Time.realtimeSinceStartup + flashDuration;
            bool flashOn = false;

            while (Time.realtimeSinceStartup < endTime)
            {
                flashOn = !flashOn;
                SetRendererVisibility(flashOn);
                yield return new WaitForSecondsRealtime(flashInterval);
            }

            RestoreRendererVisibility();
            _flashRoutine = null;
        }

        private void StopFlash()
        {
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                RestoreRendererVisibility();
                _flashRoutine = null;
            }
        }

        private void RefreshRendererCache()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _rendererEnabledStates = new bool[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                _rendererEnabledStates[i] = _renderers[i] != null && _renderers[i].enabled;
            }
        }

        private void SetRendererVisibility(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.enabled = visible && _rendererEnabledStates[i];
            }
        }

        private void RestoreRendererVisibility()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.enabled = _rendererEnabledStates[i];
            }
        }
    }
}
