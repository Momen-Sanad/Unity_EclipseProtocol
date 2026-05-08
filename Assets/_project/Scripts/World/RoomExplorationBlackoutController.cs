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
        private const string ShaderName = "Eclipse Protocol/Room Exploration Blackout";
        private const float HalfPi = Mathf.PI * 0.5f;

        private static readonly int BlackColorId = Shader.PropertyToID("_BlackColor");
        private static readonly int OverlayAlphaId = Shader.PropertyToID("_OverlayAlpha");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int PlayAreaRectId = Shader.PropertyToID("_PlayAreaRect");
        private static readonly int RoomRectCountId = Shader.PropertyToID("_RoomRectCount");
        private static readonly int RoomRectsId = Shader.PropertyToID("_RoomRects");
        private static readonly int RoomRevealAmountsId = Shader.PropertyToID("_RoomRevealAmounts");

        [SerializeField] private Material blackoutMaterial;
        [SerializeField] private Color blackoutColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float overlayAlpha = 1f;
        [SerializeField, Min(0f)] private float feather = 1.1f;
        [SerializeField, Min(0f)] private float revealPadding = 0.2f;
        [SerializeField, Min(0f)] private float overlayHeight = 2.2f;
        [SerializeField, Min(1f)] private float overlayMargin = 48f;
        [SerializeField, Range(0f, 1f)] private float clearedRoomPreviewReveal = 0.2f;
        [SerializeField, Range(0f, 1f)] private float nearRoomPreviewReveal = 0.92f;
        [SerializeField, Min(0.1f)] private float approachRevealDistance = 8f;
        [SerializeField, Min(0.1f)] private float revealSlerpSpeed = 5f;

        private readonly Vector4[] _roomRectBuffer = new Vector4[MaxRoomRects];
        private readonly Vector4[] _roomRevealBuffer = new Vector4[MaxRoomRects];
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
            blackoutMaterial = material;
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
            BuildOverlayMesh();
            UpdateTargetRevealAmounts();
            for (int i = 0; i < _revealAmounts.Length; i++)
            {
                _revealAmounts[i] = _targetRevealAmounts[i];
            }

            PushShaderData();
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
                PushShaderData();
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

            int roomCount = Mathf.Min(_rooms.Length, MaxRoomRects);
            for (int i = 0; i < roomCount; i++)
            {
                _roomRectBuffer[i] = _rooms[i].ToRect(0f);
                _roomRevealBuffer[i] = new Vector4(_revealAmounts[i], 0f, 0f, 0f);
            }

            _materialInstance.SetColor(BlackColorId, blackoutColor);
            _materialInstance.SetFloat(OverlayAlphaId, overlayAlpha);
            _materialInstance.SetFloat(FeatherId, feather);
            _materialInstance.SetVector(PlayAreaRectId, BoundsToRect(_playAreaBounds));
            _materialInstance.SetInt(RoomRectCountId, roomCount);
            _materialInstance.SetVectorArray(RoomRectsId, _roomRectBuffer);
            _materialInstance.SetVectorArray(RoomRevealAmountsId, _roomRevealBuffer);
        }

        private static Vector4 BoundsToRect(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new Vector4(min.x, min.z, max.x, max.z);
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
