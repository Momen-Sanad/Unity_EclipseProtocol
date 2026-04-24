using EclipseProtocol.Core;
using UnityEngine;

namespace EclipseProtocol.AI
{
    public class DroneDetectionSensor : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField, Min(0.1f)] private float detectionRadius = 8f;
        [SerializeField] private LayerMask playerMask;
        [SerializeField] private Transform detectionOrigin;

        private readonly Collider[] _overlapResults = new Collider[8];
        private bool _playerInsideDetection;

        public float DetectionRadius => detectionRadius;

        private void Awake()
        {
            if (balanceData != null)
            {
                detectionRadius = balanceData.droneDetectionRadius;
            }

            if (detectionOrigin == null)
            {
                detectionOrigin = transform;
            }
        }

        private void Update()
        {
            Vector3 origin = detectionOrigin.position;
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                detectionRadius,
                _overlapResults,
                playerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount > 0)
            {
                Transform detectedTarget = _overlapResults[0].transform;
                if (!_playerInsideDetection)
                {
                    _playerInsideDetection = true;
                    Debug.Log($"[DroneDetectionSensor] Player detected by {name}: {detectedTarget.name}", this);
                }
            }
            else
            {
                _playerInsideDetection = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = detectionOrigin != null ? detectionOrigin.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, detectionRadius);
        }
    }
}
