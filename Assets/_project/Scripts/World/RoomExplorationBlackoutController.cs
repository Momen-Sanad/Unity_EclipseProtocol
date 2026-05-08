using System.Collections.Generic;
using UnityEngine;

namespace EclipseProtocol.World
{
    public struct RoomExplorationRegion
    {
        public Bounds Bounds;

        public RoomExplorationRegion(Bounds bounds)
        {
            Bounds = bounds;
        }

        public bool Contains(Vector3 position, float padding)
        {
            Vector3 min = Bounds.min;
            Vector3 max = Bounds.max;
            return position.x >= min.x - padding
                && position.x <= max.x + padding
                && position.z >= min.z - padding
                && position.z <= max.z + padding;
        }

        public Vector4 ToRect(float padding)
        {
            Vector3 min = Bounds.min;
            Vector3 max = Bounds.max;
            return new Vector4(min.x - padding, min.z - padding, max.x + padding, max.z + padding);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class RoomExplorationBlackoutController : MonoBehaviour
    {
        private const int MaxExploredRects = 32;
        private const string ShaderName = "Eclipse Protocol/Room Exploration Blackout";

        private static readonly int BlackColorId = Shader.PropertyToID("_BlackColor");
        private static readonly int OverlayAlphaId = Shader.PropertyToID("_OverlayAlpha");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int PlayAreaRectId = Shader.PropertyToID("_PlayAreaRect");
        private static readonly int ExploredRectCountId = Shader.PropertyToID("_ExploredRectCount");
        private static readonly int ExploredRectsId = Shader.PropertyToID("_ExploredRects");

        [SerializeField] private Material blackoutMaterial;
        [SerializeField] private Color blackoutColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 1f;
        [SerializeField, Min(0f)] private float feather = 0.35f;
        [SerializeField, Min(0f)] private float revealPadding = 0.2f;
        [SerializeField, Min(0f)] private float overlayHeight = 5.5f;
        [SerializeField, Min(1f)] private float overlayMargin = 48f;

        private readonly Vector4[] _exploredRectBuffer = new Vector4[MaxExploredRects];
        private RoomExplorationRegion[] _rooms = new RoomExplorationRegion[0];
        private bool[] _exploredRooms = new bool[0];
        private Transform _player;
        private Bounds _playAreaBounds;
        private Material _materialInstance;
        private Mesh _overlayMesh;

        public void Configure(
            Material material,
            IReadOnlyList<RoomExplorationRegion> rooms,
            Transform player,
            float height,
            float roomRevealPadding,
            float meshMargin)
        {
            blackoutMaterial = material;
            _player = player;
            overlayHeight = height;
            revealPadding = roomRevealPadding;
            overlayMargin = meshMargin;

            int count = Mathf.Min(rooms != null ? rooms.Count : 0, MaxExploredRects);
            _rooms = new RoomExplorationRegion[count];
            _exploredRooms = new bool[count];
            for (int i = 0; i < count; i++)
            {
                _rooms[i] = rooms[i];
            }

            if (count == 0)
            {
                enabled = false;
                return;
            }

            _playAreaBounds = _rooms[0].Bounds;
            for (int i = 1; i < _rooms.Length; i++)
            {
                _playAreaBounds.Encapsulate(_rooms[i].Bounds);
            }

            EnsureMaterial();
            BuildOverlayMesh();
            RevealRoomAtPlayer();
            PushShaderData();
        }

        private void Update()
        {
            if (_player == null || _rooms.Length == 0)
            {
                return;
            }

            if (RevealRoomAtPlayer())
            {
                PushShaderData();
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(_materialInstance);
            DestroyRuntimeObject(_overlayMesh);
        }

        private bool RevealRoomAtPlayer()
        {
            bool changed = false;
            Vector3 playerPosition = _player.position;
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_exploredRooms[i] || !_rooms[i].Contains(playerPosition, revealPadding))
                {
                    continue;
                }

                _exploredRooms[i] = true;
                changed = true;
            }

            return changed;
        }

        private void EnsureMaterial()
        {
            if (_materialInstance != null)
            {
                return;
            }

            if (blackoutMaterial != null)
            {
                _materialInstance = new Material(blackoutMaterial);
            }
            else
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"[{nameof(RoomExplorationBlackoutController)}] Could not find shader '{ShaderName}'.", this);
                    enabled = false;
                    return;
                }

                _materialInstance = new Material(shader);
            }

            _materialInstance.name = "Room Exploration Blackout (Runtime)";
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = _materialInstance;
        }

        private void BuildOverlayMesh()
        {
            Bounds meshBounds = _playAreaBounds;
            meshBounds.Expand(new Vector3(overlayMargin * 2f, 0f, overlayMargin * 2f));

            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            Vector3[] vertices =
            {
                new Vector3(min.x, overlayHeight, min.z),
                new Vector3(max.x, overlayHeight, min.z),
                new Vector3(max.x, overlayHeight, max.z),
                new Vector3(min.x, overlayHeight, max.z)
            };

            _overlayMesh = new Mesh
            {
                name = "Room Exploration Blackout Mesh",
                vertices = vertices,
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                bounds = new Bounds(meshBounds.center, new Vector3(meshBounds.size.x, 0.1f, meshBounds.size.z))
            };
            _overlayMesh.RecalculateNormals();

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            meshFilter.sharedMesh = _overlayMesh;
        }

        private void PushShaderData()
        {
            if (_materialInstance == null)
            {
                return;
            }

            int exploredCount = 0;
            for (int i = 0; i < _rooms.Length && exploredCount < MaxExploredRects; i++)
            {
                if (!_exploredRooms[i])
                {
                    continue;
                }

                _exploredRectBuffer[exploredCount] = _rooms[i].ToRect(revealPadding);
                exploredCount++;
            }

            _materialInstance.SetColor(BlackColorId, blackoutColor);
            _materialInstance.SetFloat(OverlayAlphaId, overlayAlpha);
            _materialInstance.SetFloat(FeatherId, feather);
            _materialInstance.SetVector(PlayAreaRectId, BoundsToRect(_playAreaBounds));
            _materialInstance.SetInt(ExploredRectCountId, exploredCount);
            _materialInstance.SetVectorArray(ExploredRectsId, _exploredRectBuffer);
        }

        private static Vector4 BoundsToRect(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new Vector4(min.x, min.z, max.x, max.z);
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
