using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EclipseProtocol.Player
{
    [DisallowMultipleComponent]
    public class PlayerOcclusionOutline : MonoBehaviour
    {
        private const string OutlineShaderName = "Eclipse Protocol/Player Occlusion Outline";

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Shader outlineShader;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField] private Color outlineColor = new Color(0.15f, 0.9f, 1f, 0.85f);
        [SerializeField, Min(0f)] private float outlineWidth = 0.045f;
        [SerializeField, Min(0.05f)] private float visibilityCheckInterval = 0.08f;
        [SerializeField, Min(0f)] private float targetHeightOffset = 0.9f;

        private readonly List<Renderer> _outlineRenderers = new List<Renderer>();
        private readonly List<GameObject> _outlineObjects = new List<GameObject>();
        private Material _outlineMaterial;
        private float _nextVisibilityCheckTime;
        private bool _isOutlined;

        private void Awake()
        {
            CreateOutlineMaterial();
        }

        private void Start()
        {
            RebuildOutlineRenderers();
            SetOutlineVisible(false);
        }

        private void LateUpdate()
        {
            if (Time.time < _nextVisibilityCheckTime)
            {
                return;
            }

            _nextVisibilityCheckTime = Time.time + visibilityCheckInterval;

            if (_outlineRenderers.Count == 0)
            {
                RebuildOutlineRenderers();
            }

            SetOutlineVisible(IsPlayerOccluded());
        }

        private void OnDestroy()
        {
            if (_outlineMaterial != null)
            {
                Destroy(_outlineMaterial);
            }
        }

        private void CreateOutlineMaterial()
        {
            Shader shaderToUse = outlineShader != null ? outlineShader : Shader.Find(OutlineShaderName);
            if (shaderToUse == null)
            {
                Debug.LogError($"[{nameof(PlayerOcclusionOutline)}] Could not find shader '{OutlineShaderName}'.", this);
                return;
            }

            _outlineMaterial = new Material(shaderToUse)
            {
                name = "Player Occlusion Outline (Runtime)"
            };
            _outlineMaterial.SetColor("_OutlineColor", outlineColor);
            _outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
        }

        private void RebuildOutlineRenderers()
        {
            if (_outlineMaterial == null)
            {
                return;
            }

            for (int i = 0; i < _outlineObjects.Count; i++)
            {
                if (_outlineObjects[i] != null)
                {
                    Destroy(_outlineObjects[i]);
                }
            }

            _outlineRenderers.Clear();
            _outlineObjects.Clear();

            Renderer[] sourceRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer sourceRenderer in sourceRenderers)
            {
                if (sourceRenderer == null || sourceRenderer.name.EndsWith("_Outline", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (sourceRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    CreateSkinnedOutlineRenderer(skinnedMeshRenderer);
                }
                else if (sourceRenderer is MeshRenderer meshRenderer)
                {
                    CreateMeshOutlineRenderer(meshRenderer);
                }
            }

            _isOutlined = false;
        }

        private void CreateSkinnedOutlineRenderer(SkinnedMeshRenderer sourceRenderer)
        {
            if (sourceRenderer.sharedMesh == null)
            {
                return;
            }

            GameObject outlineObject = new GameObject($"{sourceRenderer.name}_Outline");
            outlineObject.transform.SetParent(sourceRenderer.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;
            outlineObject.layer = sourceRenderer.gameObject.layer;
            _outlineObjects.Add(outlineObject);

            SkinnedMeshRenderer outlineRenderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
            outlineRenderer.sharedMesh = sourceRenderer.sharedMesh;
            outlineRenderer.rootBone = sourceRenderer.rootBone;
            outlineRenderer.bones = sourceRenderer.bones;
            outlineRenderer.localBounds = sourceRenderer.localBounds;
            ConfigureOutlineRenderer(outlineRenderer, sourceRenderer.sharedMesh.subMeshCount);
        }

        private void CreateMeshOutlineRenderer(MeshRenderer sourceRenderer)
        {
            MeshFilter sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            {
                return;
            }

            GameObject outlineObject = new GameObject($"{sourceRenderer.name}_Outline");
            outlineObject.transform.SetParent(sourceRenderer.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;
            outlineObject.layer = sourceRenderer.gameObject.layer;
            _outlineObjects.Add(outlineObject);

            MeshFilter outlineMeshFilter = outlineObject.AddComponent<MeshFilter>();
            outlineMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

            MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            ConfigureOutlineRenderer(outlineRenderer, sourceMeshFilter.sharedMesh.subMeshCount);
        }

        private void ConfigureOutlineRenderer(Renderer outlineRenderer, int subMeshCount)
        {
            int materialCount = Mathf.Max(1, subMeshCount);
            Material[] outlineMaterials = new Material[materialCount];
            for (int i = 0; i < outlineMaterials.Length; i++)
            {
                outlineMaterials[i] = _outlineMaterial;
            }

            outlineRenderer.sharedMaterials = outlineMaterials;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
            outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outlineRenderer.enabled = false;
            _outlineRenderers.Add(outlineRenderer);
        }

        private bool IsPlayerOccluded()
        {
            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            if (cameraToUse == null)
            {
                return false;
            }

            Vector3 targetPoint = transform.position + Vector3.up * targetHeightOffset;
            Vector3 cameraPosition = cameraToUse.transform.position;
            Vector3 direction = targetPoint - cameraPosition;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(cameraPosition, direction / distance, distance, occlusionMask, QueryTriggerInteraction.Ignore);
            float nearestBlockerDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == null || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                nearestBlockerDistance = Mathf.Min(nearestBlockerDistance, hits[i].distance);
            }

            return nearestBlockerDistance < distance;
        }

        private void SetOutlineVisible(bool visible)
        {
            if (_isOutlined == visible)
            {
                return;
            }

            _isOutlined = visible;
            for (int i = 0; i < _outlineRenderers.Count; i++)
            {
                if (_outlineRenderers[i] != null)
                {
                    _outlineRenderers[i].enabled = visible;
                }
            }
        }
    }
}
