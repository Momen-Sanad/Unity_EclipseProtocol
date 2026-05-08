using EclipseProtocol.Audio;
using EclipseProtocol.Core;
using EclipseProtocol.Player;
using UnityEngine;

namespace EclipseProtocol.World
{
    [RequireComponent(typeof(Collider))]
    public class ExtractionTrigger : MonoBehaviour
    {
        [SerializeField] private bool startsLocked = true;
        [SerializeField] private Renderer statusRenderer;
        [SerializeField] private Color lockedColor = new Color(1f, 0.35f, 0.15f);
        [SerializeField] private Color unlockedColor = new Color(0.25f, 0.85f, 1f);

        private MaterialPropertyBlock _statusPropertyBlock;

        public bool IsLocked { get; private set; }

        private void Awake()
        {
            Collider extractionCollider = GetComponent<Collider>();
            extractionCollider.isTrigger = true;

            if (statusRenderer == null)
            {
                statusRenderer = GetComponentInChildren<Renderer>();
            }

            SetLocked(startsLocked);
        }

        public void SetLocked(bool isLocked)
        {
            IsLocked = isLocked;
            if (statusRenderer != null)
            {
                _statusPropertyBlock ??= new MaterialPropertyBlock();
                statusRenderer.GetPropertyBlock(_statusPropertyBlock);
                _statusPropertyBlock.SetColor("_BaseColor", IsLocked ? lockedColor : unlockedColor);
                _statusPropertyBlock.SetColor("_Color", IsLocked ? lockedColor : unlockedColor);
                statusRenderer.SetPropertyBlock(_statusPropertyBlock);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PlayerController _))
            {
                return;
            }

            if (IsLocked)
            {
                AudioManager.Instance?.PlayLocked(transform.position);
            }

            GameStateManager.Instance?.TryCompleteExtraction();
        }
    }
}
