using UnityEngine;

namespace EclipseProtocol.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class PlayerVisualLoader : MonoBehaviour
    {
        [SerializeField] private string robotResourcePath = "Player/low_poly_animated_robot";
        [SerializeField, Min(0.01f)] private float targetHeight = 2.4f;
        [SerializeField] private Vector3 localEulerAngles;

        private GameObject _visualInstance;

        private void Awake()
        {
            LoadVisual();
        }

        private void LoadVisual()
        {
            if (_visualInstance != null)
            {
                return;
            }

            GameObject visualPrefab = Resources.Load<GameObject>(robotResourcePath);
            if (visualPrefab == null)
            {
                Debug.LogError($"[PlayerVisualLoader] Could not load robot visual at Resources/{robotResourcePath}.", this);
                return;
            }

            _visualInstance = Instantiate(visualPrefab, transform);
            _visualInstance.name = "RobotVisual";
            _visualInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(localEulerAngles));
            _visualInstance.transform.localScale = Vector3.one;

            RemoveGameplayComponents(_visualInstance);
            ScaleAndGroundVisual();
            SetLayerRecursively(_visualInstance, gameObject.layer);
        }

        private void ScaleAndGroundVisual()
        {
            Renderer[] renderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = CalculateBounds(renderers);
            float scale = targetHeight / Mathf.Max(bounds.size.y, 0.001f);
            _visualInstance.transform.localScale = Vector3.one * scale;

            renderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            bounds = CalculateBounds(renderers);

            Vector3 localMin = transform.InverseTransformPoint(bounds.min);
            _visualInstance.transform.localPosition += Vector3.up * (-1f - localMin.y);
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void RemoveGameplayComponents(GameObject root)
        {
            foreach (Collider visualCollider in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(visualCollider);
            }

            foreach (Rigidbody visualRigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(visualRigidbody);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
