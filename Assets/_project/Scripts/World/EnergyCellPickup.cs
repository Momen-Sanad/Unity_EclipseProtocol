using EclipseProtocol.Core;
using EclipseProtocol.Player;
using UnityEngine;

namespace EclipseProtocol.World
{
    [RequireComponent(typeof(Collider))]
    public class EnergyCellPickup : MonoBehaviour
    {
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField, Min(1f)] private float energyRestoreAmount = 25f;
        [Header("Visuals")]
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private string visualAssetPath;
        [SerializeField] private Vector3 visualLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 visualLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 visualLocalScale = Vector3.one;
        [SerializeField] private bool forceVisualRenderersVisible = true;
        [SerializeField] private bool centerVisualBoundsOnRoot = true;

        private GameObject _visualInstance;

        public float EnergyRestoreAmount => energyRestoreAmount;

        private void Awake()
        {
            ConfigureVisual();

            if (balanceData != null)
            {
                energyRestoreAmount = balanceData.GetEffectiveEnergyCellRestoreAmount();
            }

            Collider pickupCollider = GetComponent<Collider>();
            pickupCollider.isTrigger = true;
        }

        private void ConfigureVisual()
        {
            GameObject resolvedVisualPrefab = ResolveVisualPrefab();
            if (resolvedVisualPrefab == null || _visualInstance != null)
            {
                return;
            }

            _visualInstance = CreateVisualInstance(resolvedVisualPrefab);
            SetLayerRecursively(_visualInstance, gameObject.layer);

            if (forceVisualRenderersVisible)
            {
                ShowVisualHierarchy(_visualInstance);
            }

            Renderer[] visualRenderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            if (visualRenderers.Length == 0)
            {
                return;
            }

            if (forceVisualRenderersVisible)
            {
                EnableRenderers(visualRenderers);
            }

            if (centerVisualBoundsOnRoot)
            {
                CenterVisualBoundsOnRoot(_visualInstance.transform, visualRenderers);
            }
        }

        private GameObject ResolveVisualPrefab()
        {
            if (visualPrefab != null)
            {
                return visualPrefab;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(visualAssetPath))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(visualAssetPath);
            }
#endif

            return null;
        }

        private GameObject CreateVisualInstance(GameObject resolvedVisualPrefab)
        {
            GameObject instance = Instantiate(resolvedVisualPrefab, transform);
            instance.name = resolvedVisualPrefab.name;
            ApplyVisualTransform(instance.transform);
            return instance;
        }

        private void ApplyVisualTransform(Transform visualTransform)
        {
            visualTransform.localPosition = visualLocalPosition;
            visualTransform.localRotation = Quaternion.Euler(visualLocalEulerAngles);
            visualTransform.localScale = visualLocalScale;
        }

        private void CenterVisualBoundsOnRoot(Transform visualTransform, Renderer[] visualRenderers)
        {
            Bounds bounds = CalculateRendererBounds(visualRenderers);
            visualTransform.position += transform.position - bounds.center;
        }

        private static void EnableRenderers(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = true;
            }
        }

        private static void SetLayerRecursively(GameObject targetObject, int layer)
        {
            targetObject.layer = layer;
            Transform targetTransform = targetObject.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
            {
                SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
            }
        }

        private static void ShowVisualHierarchy(GameObject targetObject)
        {
            targetObject.SetActive(true);
            Transform targetTransform = targetObject.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
            {
                ShowVisualHierarchy(targetTransform.GetChild(i).gameObject);
            }
        }

        private static Bounds CalculateRendererBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PlayerController playerController))
            {
                return;
            }

            EnergyCellSystem.Instance.TryCollect(this, playerController);
        }

        public void Consume()
        {
            gameObject.SetActive(false);
        }
    }
}
