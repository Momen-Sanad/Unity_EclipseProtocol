using System.Collections.Generic;
using UnityEngine;

namespace EclipseProtocol.World
{
    public struct RoomExplorationRegion
    {
        public Bounds Bounds;
        public Bounds RevealBounds;

        public RoomExplorationRegion(Bounds bounds)
        {
            Bounds = bounds;
            RevealBounds = bounds;
        }

        public RoomExplorationRegion(Bounds revealBounds, Bounds maskBounds)
        {
            RevealBounds = revealBounds;
            Bounds = maskBounds;
        }

        public bool Contains(Vector3 position, float padding)
        {
            Vector3 min = RevealBounds.min;
            Vector3 max = RevealBounds.max;
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
        private const int MaxRoomRects = 32;
        private const float HalfPi = Mathf.PI * 0.5f;
        private const float VisibleRevealThreshold = 0.5f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color blackoutColor = Color.black;
        [SerializeField, Min(0f)] private float revealPadding = 0.2f;
        [SerializeField, Min(0f)] private float overlayHeight = 2.2f;
        [SerializeField, Min(1f)] private float overlayMargin = 48f;
        [SerializeField, Range(0f, 1f)] private float clearedRoomPreviewReveal = 0.2f;
        [SerializeField, Range(0f, 1f)] private float nearRoomPreviewReveal = 0.92f;
        [SerializeField, Min(0.1f)] private float approachRevealDistance = 8f;
        [SerializeField, Min(0.1f)] private float revealSlerpSpeed = 5f;

        private RoomExplorationRegion[] _rooms = new RoomExplorationRegion[0];
        private DoorGate[] _entryDoors = new DoorGate[0];
        private bool[] _enteredRooms = new bool[0];
        private float[] _revealAmounts = new float[0];
        private float[] _targetRevealAmounts = new float[0];
        private Transform _player;
        private Bounds _playAreaBounds;
        private Material _materialInstance;
        private Mesh _overlayMesh;
        private int _currentRoomIndex = -1;

        public void Configure(
            Material material,
            IReadOnlyList<RoomExplorationRegion> rooms,
            IReadOnlyList<DoorGate> progressionDoors,
            Transform player,
            float height,
            float roomRevealPadding,
            float meshMargin)
        {
            _ = material;
            _player = player;
            overlayHeight = height;
            revealPadding = roomRevealPadding;
            overlayMargin = meshMargin;

            int count = Mathf.Min(rooms != null ? rooms.Count : 0, MaxRoomRects);
            _rooms = new RoomExplorationRegion[count];
            _entryDoors = new DoorGate[count];
            _enteredRooms = new bool[count];
            _revealAmounts = new float[count];
            _targetRevealAmounts = new float[count];
            for (int i = 0; i < count; i++)
            {
                _rooms[i] = rooms[i];
                _entryDoors[i] = i > 0 && progressionDoors != null && i - 1 < progressionDoors.Count
                    ? progressionDoors[i - 1]
                    : null;
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
            UpdateTargetRevealAmounts();
            for (int i = 0; i < _revealAmounts.Length; i++)
            {
                _revealAmounts[i] = _targetRevealAmounts[i];
            }

            BuildOverlayMesh();
        }

        private void Update()
        {
            if (_player == null || _rooms.Length == 0)
            {
                return;
            }

            UpdateTargetRevealAmounts();
            if (UpdateRevealAmounts())
            {
                BuildOverlayMesh();
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(_materialInstance);
            DestroyRuntimeObject(_overlayMesh);
        }

        private void UpdateTargetRevealAmounts()
        {
            Vector3 playerPosition = _player.position;
            _currentRoomIndex = FindContainingRoomIndex(playerPosition);
            if (_currentRoomIndex >= 0)
            {
                _enteredRooms[_currentRoomIndex] = true;
            }

            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_enteredRooms[i])
                {
                    _targetRevealAmounts[i] = 1f;
                    continue;
                }

                _targetRevealAmounts[i] = IsRoomPreviewEligible(i)
                    ? CalculatePreviewRevealAmount(i, playerPosition)
                    : 0f;
            }
        }

        private bool UpdateRevealAmounts()
        {
            bool changed = false;
            float step = Application.isPlaying
                ? 1f - Mathf.Exp(-revealSlerpSpeed * Time.deltaTime)
                : 1f;

            for (int i = 0; i < _revealAmounts.Length; i++)
            {
                float next = Slerp01(_revealAmounts[i], _targetRevealAmounts[i], step);
                if (Mathf.Abs(next - _targetRevealAmounts[i]) < 0.001f)
                {
                    next = _targetRevealAmounts[i];
                }

                if (!Mathf.Approximately(next, _revealAmounts[i]))
                {
                    _revealAmounts[i] = next;
                    changed = true;
                }
            }

            return changed;
        }

        private int FindContainingRoomIndex(Vector3 playerPosition)
        {
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_rooms[i].Contains(playerPosition, revealPadding))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsRoomPreviewEligible(int roomIndex)
        {
            if (roomIndex <= 0)
            {
                return false;
            }

            DoorGate entryDoor = _entryDoors[roomIndex];
            if (entryDoor != null)
            {
                return entryDoor.IsOpen;
            }

            return _enteredRooms[roomIndex - 1];
        }

        private float CalculatePreviewRevealAmount(int roomIndex, Vector3 playerPosition)
        {
            float distance = DistanceToBoundsXZ(_rooms[roomIndex].RevealBounds, playerPosition);
            float approach01 = 1f - Mathf.Clamp01(distance / approachRevealDistance);
            return Mathf.Lerp(clearedRoomPreviewReveal, nearRoomPreviewReveal, approach01);
        }

        private void EnsureMaterial()
        {
            if (_materialInstance != null)
            {
                return;
            }

            Material sourceMaterial = Resources.Load<Material>("RuntimeSolidColor");
            if (sourceMaterial != null)
            {
                _materialInstance = new Material(sourceMaterial)
                {
                    name = "Room Exploration Blackout (Runtime)",
                    renderQueue = (int)UnityEngine.Rendering.RenderQueue.GeometryLast
                };
                ApplyMaterialColor(_materialInstance, blackoutColor);
                AssignMaterial();
                return;
            }

            Shader shader = Shader.Find("Eclipse Protocol/Runtime Solid Color");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogError($"[{nameof(RoomExplorationBlackoutController)}] Could not find a built-in unlit shader.", this);
                enabled = false;
                return;
            }

            _materialInstance = new Material(shader);
            _materialInstance.name = "Room Exploration Blackout (Runtime)";
            _materialInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.GeometryLast;
            ApplyMaterialColor(_materialInstance, blackoutColor);
            AssignMaterial();
        }

        private void AssignMaterial()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = _materialInstance;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }
        }

        private void BuildOverlayMesh()
        {
            if (_overlayMesh == null)
            {
                _overlayMesh = new Mesh
                {
                    name = "Room Exploration Blackout Mesh"
                };

                MeshFilter meshFilter = GetComponent<MeshFilter>();
                meshFilter.sharedMesh = _overlayMesh;
            }

            Bounds meshBounds = _playAreaBounds;
            meshBounds.Expand(new Vector3(overlayMargin * 2f, 0f, overlayMargin * 2f));

            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;

            List<float> xCuts = new List<float> { min.x, max.x };
            List<float> zCuts = new List<float> { min.z, max.z };

            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_revealAmounts.Length <= i || _revealAmounts[i] < VisibleRevealThreshold)
                {
                    continue;
                }

                Vector4 rect = _rooms[i].ToRect(revealPadding);
                AddCut(xCuts, Mathf.Clamp(rect.x, min.x, max.x));
                AddCut(xCuts, Mathf.Clamp(rect.z, min.x, max.x));
                AddCut(zCuts, Mathf.Clamp(rect.y, min.z, max.z));
                AddCut(zCuts, Mathf.Clamp(rect.w, min.z, max.z));
            }

            xCuts.Sort();
            zCuts.Sort();

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            for (int x = 0; x < xCuts.Count - 1; x++)
            {
                float x0 = xCuts[x];
                float x1 = xCuts[x + 1];
                if (x1 - x0 <= 0.01f)
                {
                    continue;
                }

                for (int z = 0; z < zCuts.Count - 1; z++)
                {
                    float z0 = zCuts[z];
                    float z1 = zCuts[z + 1];
                    if (z1 - z0 <= 0.01f)
                    {
                        continue;
                    }

                    Vector2 center = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
                    if (IsRevealedCell(center))
                    {
                        continue;
                    }

                    int start = vertices.Count;
                    vertices.Add(new Vector3(x0, overlayHeight, z0));
                    vertices.Add(new Vector3(x1, overlayHeight, z0));
                    vertices.Add(new Vector3(x1, overlayHeight, z1));
                    vertices.Add(new Vector3(x0, overlayHeight, z1));
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 1);
                    triangles.Add(start);
                    triangles.Add(start + 3);
                    triangles.Add(start + 2);
                }
            }

            _overlayMesh.Clear();
            _overlayMesh.SetVertices(vertices);
            _overlayMesh.SetTriangles(triangles, 0);
            _overlayMesh.bounds = new Bounds(meshBounds.center, new Vector3(meshBounds.size.x, 0.1f, meshBounds.size.z));
            _overlayMesh.RecalculateNormals();
        }

        private bool IsRevealedCell(Vector2 center)
        {
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_revealAmounts.Length <= i || _revealAmounts[i] < VisibleRevealThreshold)
                {
                    continue;
                }

                Vector4 rect = _rooms[i].ToRect(revealPadding);
                if (center.x >= rect.x && center.x <= rect.z && center.y >= rect.y && center.y <= rect.w)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddCut(List<float> cuts, float value)
        {
            for (int i = 0; i < cuts.Count; i++)
            {
                if (Mathf.Abs(cuts[i] - value) <= 0.01f)
                {
                    return;
                }
            }

            cuts.Add(value);
        }

        private static float DistanceToBoundsXZ(Bounds bounds, Vector3 position)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float dx = Mathf.Max(min.x - position.x, 0f, position.x - max.x);
            float dz = Mathf.Max(min.z - position.z, 0f, position.z - max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float Slerp01(float from, float to, float step)
        {
            from = Mathf.Clamp01(from);
            to = Mathf.Clamp01(to);
            step = Mathf.Clamp01(step);
            if (Mathf.Approximately(from, to))
            {
                return to;
            }

            Vector3 fromPoint = UnitArcPoint(from);
            Vector3 toPoint = UnitArcPoint(to);
            Vector3 value = Vector3.Slerp(fromPoint, toPoint, step);
            float angle = Mathf.Atan2(value.y, value.x);
            return Mathf.Clamp01(angle / HalfPi);
        }

        private static Vector3 UnitArcPoint(float value)
        {
            float angle = Mathf.Clamp01(value) * HalfPi;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
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
