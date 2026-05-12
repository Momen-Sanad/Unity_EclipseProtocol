using System.Collections;
using UnityEngine;

namespace EclipseProtocol.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerDamageFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Hit Stop")]
        [SerializeField, Min(0f)] private float hitStopDuration = 0.08f;
        [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.03f;

        [Header("Flash")]
        [SerializeField, Min(0f)] private float flashDuration = 0.14f;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private Color flashEmissionColor = new Color(1f, 0.18f, 0.08f, 1f);

        private PlayerController _playerController;
        private Renderer[] _renderers;
        private MaterialPropertyBlock[] _propertyBlocks;
        private Color[] _baseColors;
        private Color[] _emissionColors;
        private Coroutine _hitStopRoutine;
        private Coroutine _flashRoutine;
        private float _defaultFixedDeltaTime;
        private float _timeScaleBeforeHitStop = 1f;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _propertyBlocks = new MaterialPropertyBlock[_renderers.Length];
            _baseColors = new Color[_renderers.Length];
            _emissionColors = new Color[_renderers.Length];
            _defaultFixedDeltaTime = Time.fixedDeltaTime;

            for (int i = 0; i < _renderers.Length; i++)
            {
                _propertyBlocks[i] = new MaterialPropertyBlock();
                Material sharedMaterial = _renderers[i].sharedMaterial;
                _baseColors[i] = GetMaterialColor(sharedMaterial, BaseColorId, ColorId, Color.white);
                _emissionColors[i] = GetMaterialColor(sharedMaterial, EmissionColorId, 0, Color.black);
            }
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

            StopFeedback();
        }

        private void OnDestroy()
        {
            StopFeedback();
        }

        private void HandleDamageTaken(float healthLost)
        {
            StartHitStop();
            StartFlash();
        }

        private void StartHitStop()
        {
            if (hitStopDuration <= 0f)
            {
                return;
            }

            if (_hitStopRoutine != null)
            {
                StopCoroutine(_hitStopRoutine);
                RestoreTimeScale();
            }

            _timeScaleBeforeHitStop = Time.timeScale;
            Time.timeScale = Mathf.Min(Time.timeScale, hitStopTimeScale);
            Time.fixedDeltaTime = _defaultFixedDeltaTime * Time.timeScale;
            _hitStopRoutine = StartCoroutine(HitStopRoutine());
        }

        private IEnumerator HitStopRoutine()
        {
            yield return new WaitForSecondsRealtime(hitStopDuration);
            RestoreTimeScale();
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = _timeScaleBeforeHitStop;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * Time.timeScale;
            _hitStopRoutine = null;
        }

        private void StartFlash()
        {
            if (flashDuration <= 0f || _renderers.Length == 0)
            {
                return;
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                RestoreRendererColors();
            }

            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            SetRendererColors(flashColor, flashEmissionColor);
            yield return new WaitForSecondsRealtime(flashDuration);
            RestoreRendererColors();
            _flashRoutine = null;
        }

        private void StopFeedback()
        {
            if (_hitStopRoutine != null)
            {
                StopCoroutine(_hitStopRoutine);
                RestoreTimeScale();
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                RestoreRendererColors();
                _flashRoutine = null;
            }
        }

        private void SetRendererColors(Color baseColor, Color emissionColor)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock propertyBlock = _propertyBlocks[i];
                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, baseColor);
                propertyBlock.SetColor(ColorId, baseColor);
                propertyBlock.SetColor(EmissionColorId, emissionColor);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void RestoreRendererColors()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock propertyBlock = _propertyBlocks[i];
                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, _baseColors[i]);
                propertyBlock.SetColor(ColorId, _baseColors[i]);
                propertyBlock.SetColor(EmissionColorId, _emissionColors[i]);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static Color GetMaterialColor(Material material, int preferredId, int fallbackId, Color fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            if (material.HasProperty(preferredId))
            {
                return material.GetColor(preferredId);
            }

            return fallbackId != 0 && material.HasProperty(fallbackId) ? material.GetColor(fallbackId) : fallback;
        }
    }
}
