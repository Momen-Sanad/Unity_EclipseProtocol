using UnityEngine;

namespace EclipseProtocol.Core
{
    public class CameraFollow3D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -6f);
        [SerializeField] private Vector3 fixedEulerRotation = new Vector3(60f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.2f;
        [SerializeField] private bool preventClipping = true;
        [SerializeField] private LayerMask clippingMask = ~0;
        [SerializeField, Min(0f)] private float clippingPadding = 0.2f;

        private Vector3 _velocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;

            if (preventClipping && Physics.Linecast(target.position, desiredPosition, out RaycastHit hitInfo, clippingMask, QueryTriggerInteraction.Ignore))
            {
                desiredPosition = hitInfo.point + hitInfo.normal * clippingPadding;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, smoothTime);
            transform.rotation = Quaternion.Euler(fixedEulerRotation);
        }
    }
}
