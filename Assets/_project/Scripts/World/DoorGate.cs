using EclipseProtocol.Player;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseProtocol.World
{
    [DisallowMultipleComponent]
    public class DoorGate : MonoBehaviour
    {
        [SerializeField] private Transform panel;
        [SerializeField] private Collider blockerCollider;
        [SerializeField] private Collider oneWayTrigger;
        [SerializeField] private NavMeshObstacle navMeshObstacle;
        [SerializeField, Min(0.25f)] private float openHeight = 3.4f;
        [SerializeField, Min(0.1f)] private float slideSpeed = 5f;
        [SerializeField, Min(0.1f)] private float crossingDistance = 1.25f;
        [SerializeField] private Color lockedColor = new Color(1f, 0.35f, 0.12f);
        [SerializeField] private Color openColor = new Color(0.25f, 0.9f, 1f);

        private Renderer _panelRenderer;
        private MaterialPropertyBlock _panelPropertyBlock;
        private Vector3 _closedLocalPosition;
        private Vector3 _openLocalPosition;
        private bool _isOpen;
        private bool _closedBehindPlayer;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            CaptureReferences();
            SetOpen(false);
        }

        private void Update()
        {
            if (panel == null)
            {
                return;
            }

            Vector3 target = _isOpen ? _openLocalPosition : _closedLocalPosition;
            panel.localPosition = Vector3.MoveTowards(panel.localPosition, target, slideSpeed * Time.deltaTime);
        }

        public void Configure(Transform panelTransform, Collider blocker, Collider trigger, NavMeshObstacle obstacle, float slideHeight)
        {
            panel = panelTransform;
            blockerCollider = blocker;
            oneWayTrigger = trigger;
            navMeshObstacle = obstacle;
            openHeight = Mathf.Max(0.25f, slideHeight);
            CaptureReferences();
            SetOpen(false);
        }

        public void UnlockAndOpen()
        {
            if (_closedBehindPlayer)
            {
                return;
            }

            SetOpen(true);
        }

        public void CloseBehindPlayer()
        {
            _closedBehindPlayer = true;
            SetOpen(false);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_isOpen || _closedBehindPlayer || !other.TryGetComponent(out PlayerController _))
            {
                return;
            }

            Vector3 toPlayer = other.transform.position - transform.position;
            if (Vector3.Dot(toPlayer, transform.forward) >= crossingDistance)
            {
                CloseBehindPlayer();
            }
        }

        private void CaptureReferences()
        {
            if (panel != null)
            {
                _panelRenderer = panel.GetComponentInChildren<Renderer>();
                _closedLocalPosition = panel.localPosition;
                _openLocalPosition = _closedLocalPosition + Vector3.up * openHeight;
            }

            if (oneWayTrigger != null)
            {
                oneWayTrigger.isTrigger = true;
            }
        }

        private void SetOpen(bool isOpen)
        {
            _isOpen = isOpen;
            if (blockerCollider != null)
            {
                blockerCollider.enabled = !isOpen;
            }

            if (oneWayTrigger != null)
            {
                oneWayTrigger.enabled = isOpen && !_closedBehindPlayer;
            }

            if (navMeshObstacle != null)
            {
                navMeshObstacle.enabled = !isOpen;
            }

            if (_panelRenderer != null)
            {
                _panelPropertyBlock ??= new MaterialPropertyBlock();
                _panelRenderer.GetPropertyBlock(_panelPropertyBlock);
                _panelPropertyBlock.SetColor("_BaseColor", isOpen ? openColor : lockedColor);
                _panelPropertyBlock.SetColor("_Color", isOpen ? openColor : lockedColor);
                _panelRenderer.SetPropertyBlock(_panelPropertyBlock);
            }
        }
    }
}
