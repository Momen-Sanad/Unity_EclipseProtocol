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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color FallbackVisualColor = new Color(1f, 0.82f, 0.12f);

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
            if (_visualInstance != null)
            {
                Debug.Log($"[EnergyCellPickup] {name} already has visual instance '{_visualInstance.name}'.", this);
                return;
            }

            _visualInstance = resolvedVisualPrefab != null
                ? CreateVisualInstance(resolvedVisualPrefab)
                : CreateFallbackVisual();

            Debug.Log($"[EnergyCellPickup] {name} visual source={(resolvedVisualPrefab != null ? "prefab" : "fallback")} prefab={(resolvedVisualPrefab != null ? resolvedVisualPrefab.name : "null")} path='{visualAssetPath}' root='{_visualInstance.name}' localPosition={_visualInstance.transform.localPosition} localScale={_visualInstance.transform.localScale}.", this);

            SetLayerRecursively(_visualInstance, gameObject.layer);

            if (forceVisualRenderersVisible)
            {
                ShowVisualHierarchy(_visualInstance);
            }

            Renderer[] visualRenderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"[EnergyCellPickup] {name} visual renderer count={visualRenderers.Length}.", this);
            if (visualRenderers.Length == 0)
            {
                Debug.LogWarning($"[EnergyCellPickup] {name} has no visual renderers after ConfigureVisual.", this);
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

            Bounds finalBounds = CalculateRendererBounds(visualRenderers);
            Debug.Log($"[EnergyCellPickup] {name} visual bounds center={finalBounds.center} size={finalBounds.size} active={_visualInstance.activeInHierarchy}.", this);
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
                GameObject editorVisual = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(visualAssetPath);
                Debug.Log($"[EnergyCellPickup] Editor AssetDatabase lookup path='{visualAssetPath}' result={(editorVisual != null ? editorVisual.name : "null")}.", this);
                return editorVisual;
            }
#endif

            return null;
        }

        private GameObject CreateFallbackVisual()
        {
            GameObject visualRoot = new GameObject("EnergyCellFallbackVisual");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = visualLocalPosition;
            visualRoot.transform.localRotation = Quaternion.Euler(visualLocalEulerAngles);
            visualRoot.transform.localScale = Vector3.one;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "EnergyCore";
            core.transform.SetParent(visualRoot.transform, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            core.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
            ApplyFallbackMaterial(core.GetComponent<Renderer>(), FallbackVisualColor);
            DisableCollider(core);

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "EnergyGlow";
            cap.transform.SetParent(visualRoot.transform, false);
            cap.transform.localPosition = Vector3.zero;
            cap.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            ApplyFallbackMaterial(cap.GetComponent<Renderer>(), new Color(0.15f, 0.95f, 1f));
            DisableCollider(cap);

            return visualRoot;
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

        private static void ApplyFallbackMaterial(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            Material sourceMaterial = Resources.Load<Material>("RuntimeSolidColor");
            Material material;
            if (sourceMaterial != null)
            {
                material = new Material(sourceMaterial);
            }
            else
            {
                Shader shader = Shader.Find("Eclipse Protocol/Runtime Solid Color");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                if (shader == null)
                {
                    return;
                }

                material = new Material(shader);
            }

            material.name = "Energy Cell Runtime Material";
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }

            renderer.sharedMaterial = material;
        }

        private static void DisableCollider(GameObject target)
        {
            Collider visualCollider = target.GetComponent<Collider>();
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
            }
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
