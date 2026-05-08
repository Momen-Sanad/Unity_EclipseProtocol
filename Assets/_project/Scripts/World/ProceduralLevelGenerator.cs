using System.Collections.Generic;
using EclipseProtocol.AI;
using EclipseProtocol.Core;
using EclipseProtocol.Player;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace EclipseProtocol.World
{
    public class ProceduralLevelGenerator : MonoBehaviour
    {
        private static readonly Vector2Int North = new Vector2Int(0, 1);
        private static readonly Vector2Int East = new Vector2Int(1, 0);
        private static readonly Vector2Int South = new Vector2Int(0, -1);
        private static readonly Vector2Int West = new Vector2Int(-1, 0);
        private static readonly Vector2Int[] CardinalDirections = { North, East, South, West };

        [Header("Config")]
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private Transform levelRoot;
        [SerializeField] private NavMeshSurface navMeshSurface;

        [Header("Legacy Room Prefabs")]
        [SerializeField] private RoomModule startRoomPrefab;
        [SerializeField] private RoomModule[] corridorRoomPrefabs;
        [SerializeField] private RoomModule nodeRoomPrefab;
        [SerializeField] private RoomModule extractionRoomPrefab;

        [Header("Gameplay Prefabs")]
        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private DronePatrolAI patrolDronePrefab;
        [SerializeField] private HunterDroneAI hunterDronePrefab;
        [SerializeField] private EnergyCellPickup energyCellPrefab;
        [SerializeField] private RepairNode repairNodePrefab;
        [SerializeField] private ExtractionTrigger extractionTriggerPrefab;

        [Header("Seeded Rooms")]
        [SerializeField, Min(3)] private int minRoomCount = 5;
        [SerializeField, Min(3)] private int maxRoomCount = 8;
        [SerializeField] private Vector2 roomWidthRange = new Vector2(15f, 24f);
        [SerializeField] private Vector2 roomDepthRange = new Vector2(14f, 22f);
        [SerializeField, Min(0.5f)] private float floorThickness = 0.35f;
        [SerializeField, Min(2f)] private float wallHeight = 4.2f;
        [SerializeField, Min(0.1f)] private float wallThickness = 0.45f;
        [SerializeField, Min(1f)] private float obstacleHeight = 1.4f;
        [SerializeField, Min(0f)] private float roomSeparationPadding = 1.5f;

        [Header("Room Content Scaling")]
        [SerializeField, Min(0)] private int maxEnergyCellsFirstRoom = 3;
        [SerializeField, Min(0)] private int minEnergyCellsLastRoom = 0;
        [SerializeField, Min(0)] private int minEnemiesFirstRoom = 0;
        [SerializeField, Min(0)] private int maxEnemiesLastRoom = 4;
        [SerializeField, Min(0)] private int minPowerCellsFirstRoom = 1;
        [SerializeField, Min(0)] private int maxPowerCellsLastRoom = 4;
        [SerializeField, Min(0)] private int minObstaclesPerRoom = 1;
        [SerializeField, Min(0)] private int maxObstaclesPerRoom = 5;

        [Header("Enemy Mix")]
        [SerializeField, Min(1)] private int firstHunterRoomNumber = 3;
        [SerializeField, Range(0f, 1f)] private float hunterChanceFirstEligibleRoom = 0.2f;
        [SerializeField, Range(0f, 1f)] private float hunterChanceLastRoom = 0.55f;
        [SerializeField] private bool guaranteeHunterByFinalRoom = true;

        [Header("Runtime Progression")]
        [SerializeField] private bool requireRepairPerRoom = true;
        [SerializeField, Min(2f)] private float doorWidth = 5f;
        [SerializeField, Min(2f)] private float doorHeight = 3.2f;
        [SerializeField, Min(0.1f)] private float doorThickness = 0.35f;
        [SerializeField, Min(1f)] private float doorSlideHeight = 3.6f;
        [SerializeField] private Color floorColor = new Color(0.13f, 0.16f, 0.18f);
        [SerializeField] private Color wallColor = new Color(0.28f, 0.32f, 0.36f);
        [SerializeField] private Color obstacleColor = new Color(0.18f, 0.22f, 0.27f);
        [SerializeField] private Color sealedDoorColor = new Color(0.18f, 0.22f, 0.28f);
        [SerializeField] private Color lockedDoorColor = new Color(1f, 0.35f, 0.12f);

        [Header("Exploration Blackout")]
        [SerializeField] private Material roomBlackoutMaterial;
        [SerializeField, Min(0f)] private float blackoutOverlayHeight = 2.2f;
        [SerializeField, Min(0f)] private float blackoutRevealPadding = 0.25f;
        [SerializeField, Min(1f)] private float blackoutOverlayMargin = 48f;

        private readonly List<RoomModule> _spawnedRooms = new List<RoomModule>();
        private readonly List<GeneratedRoom> _generatedRooms = new List<GeneratedRoom>();
        private readonly List<Bounds> _occupiedBounds = new List<Bounds>();
        private readonly List<DoorGate> _progressionDoors = new List<DoorGate>();
        private readonly List<string> _roomDebugSummaries = new List<string>();
        private System.Random _rng;
        private PlayerController _player;
        private RoomExplorationBlackoutController _blackoutController;
        private int _enemyCount;
        private int _hunterCount;
        private int _energyCellCount;
        private int _repairNodeCount;

        public IReadOnlyList<RoomModule> SpawnedRooms => _spawnedRooms;
        public int ActiveSeed { get; private set; }

        private void Start()
        {
            if (generateOnStart)
            {
                Generate();
            }
        }

        public void Generate()
        {
            if (!HasRequiredReferences())
            {
                return;
            }

            ActiveSeed = RunSeedData.GetOrCreateSeed();
            _rng = new System.Random(ActiveSeed);
            _spawnedRooms.Clear();
            _generatedRooms.Clear();
            _occupiedBounds.Clear();
            _progressionDoors.Clear();
            _roomDebugSummaries.Clear();
            _enemyCount = 0;
            _hunterCount = 0;
            _energyCellCount = 0;
            _repairNodeCount = 0;
            _blackoutController = null;

            EnsureLevelRoot();
            ClearLevelRoot();
            GameStateManager.Instance?.ResetRunObjectives();

            BuildRoomPlan();
            BuildRoomGeometry();
            RebuildNavMesh();
            SpawnProgressionDoors();
            SpawnPlayer();
            SpawnRoomContent();
            SpawnExtraction();
            SetupExplorationBlackout();

            Debug.Log($"[ProceduralLevelGenerator] Generated seed {ActiveSeed}: {_generatedRooms.Count} rooms, {_repairNodeCount} power cells, {_energyCellCount} energy cells, {_enemyCount} enemies ({_hunterCount} hunters). Rooms: {string.Join(" | ", _roomDebugSummaries)}", this);
        }

        private bool HasRequiredReferences()
        {
            List<string> missing = new List<string>();
            if (balanceData == null) missing.Add(nameof(balanceData));
            if (playerPrefab == null) missing.Add(nameof(playerPrefab));
            if (patrolDronePrefab == null) missing.Add(nameof(patrolDronePrefab));
            if (hunterDronePrefab == null) missing.Add(nameof(hunterDronePrefab));
            if (energyCellPrefab == null) missing.Add(nameof(energyCellPrefab));
            if (repairNodePrefab == null) missing.Add(nameof(repairNodePrefab));
            if (extractionTriggerPrefab == null) missing.Add(nameof(extractionTriggerPrefab));

            if (missing.Count == 0)
            {
                return true;
            }

            Debug.LogError($"[ProceduralLevelGenerator] Runtime generation is missing references: {string.Join(", ", missing)}.", this);
            return false;
        }

        private void EnsureLevelRoot()
        {
            if (levelRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("GeneratedLevel");
            levelRoot = root.transform;
        }

        private void ClearLevelRoot()
        {
            for (int i = levelRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = levelRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void BuildRoomPlan()
        {
            int minCount = Mathf.Max(3, minRoomCount);
            int maxCount = Mathf.Max(minCount, maxRoomCount);
            int roomCount = _rng.Next(minCount, maxCount + 1);

            GeneratedRoom firstRoom = new GeneratedRoom(0, Vector3.zero, RollRoomWidth(), RollRoomDepth());
            _generatedRooms.Add(firstRoom);
            _occupiedBounds.Add(firstRoom.BuildBounds(roomSeparationPadding));

            Vector2Int previousExit = Vector2Int.zero;
            for (int i = 1; i < roomCount; i++)
            {
                GeneratedRoom previousRoom = _generatedRooms[i - 1];
                GeneratedRoom nextRoom = null;
                Vector2Int exitDirection = Vector2Int.zero;

                for (int attempt = 0; attempt < 32 && nextRoom == null; attempt++)
                {
                    Vector2Int direction = PickExitDirection(previousExit, attempt);
                    GeneratedRoom candidate = CreateAdjacentRoom(i, previousRoom, direction);
                    if (!OverlapsExistingGeneratedRoom(candidate.BuildBounds(roomSeparationPadding), previousRoom))
                    {
                        nextRoom = candidate;
                        exitDirection = direction;
                    }
                }

                if (nextRoom == null)
                {
                    exitDirection = previousExit == Vector2Int.zero ? North : previousExit;
                    nextRoom = CreateAdjacentRoom(i, previousRoom, exitDirection);
                    Debug.LogWarning($"[ProceduralLevelGenerator] Seed {ActiveSeed} needed fallback placement for room {i + 1}.", this);
                }

                previousRoom.ExitDirection = exitDirection;
                nextRoom.EntryDirection = -exitDirection;
                _generatedRooms.Add(nextRoom);
                _occupiedBounds.Add(nextRoom.BuildBounds(roomSeparationPadding));
                previousExit = exitDirection;
            }
        }

        private GeneratedRoom CreateAdjacentRoom(int index, GeneratedRoom previousRoom, Vector2Int direction)
        {
            float width = RollRoomWidth();
            float depth = RollRoomDepth();
            Vector3 offset = DirectionToVector(direction);
            float distance = IsEastWest(direction)
                ? (previousRoom.Width + width) * 0.5f
                : (previousRoom.Depth + depth) * 0.5f;
            Vector3 center = previousRoom.Center + offset * distance;
            return new GeneratedRoom(index, center, width, depth);
        }

        private Vector2Int PickExitDirection(Vector2Int previousExit, int attempt)
        {
            List<Vector2Int> options = new List<Vector2Int>(CardinalDirections);
            if (previousExit != Vector2Int.zero && options.Count > 1)
            {
                options.Remove(-previousExit);
            }

            if (previousExit != Vector2Int.zero && attempt < 16 && _rng.NextDouble() < 0.35)
            {
                return previousExit;
            }

            return options[_rng.Next(0, options.Count)];
        }

        private bool OverlapsExistingGeneratedRoom(Bounds candidateBounds, GeneratedRoom allowedTouchingRoom)
        {
            for (int i = 0; i < _generatedRooms.Count; i++)
            {
                if (_generatedRooms[i] == allowedTouchingRoom)
                {
                    continue;
                }

                if (_generatedRooms[i].BuildBounds(roomSeparationPadding).Intersects(candidateBounds))
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildRoomGeometry()
        {
            for (int i = 0; i < _generatedRooms.Count; i++)
            {
                GeneratedRoom room = _generatedRooms[i];
                room.Root = new GameObject($"Room_{i + 1:00}");
                room.Root.transform.SetParent(levelRoot, false);

                CreateFloor(room);
                CreateWalls(room);
                CreateRoomObstacles(room);
            }
        }

        private void CreateFloor(GeneratedRoom room)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(room.Root.transform, false);
            floor.transform.position = room.Center + Vector3.down * (floorThickness * 0.5f);
            floor.transform.localScale = new Vector3(room.Width, floorThickness, room.Depth);
            Tint(floor, floorColor);
        }

        private void CreateWalls(GeneratedRoom room)
        {
            CreateWallSide(room, North);
            CreateWallSide(room, East);
            CreateWallSide(room, South);
            CreateWallSide(room, West);
        }

        private void CreateWallSide(GeneratedRoom room, Vector2Int side)
        {
            bool hasDoor = room.HasDoorOn(side);
            if (!hasDoor)
            {
                CreateWallSegment(room, side, 0f, SideLength(room, side), wallHeight, wallHeight * 0.5f, "Wall");
                return;
            }

            float sideLength = SideLength(room, side);
            float cappedDoorWidth = Mathf.Min(doorWidth, Mathf.Max(1f, sideLength - 2f));
            float sideSegmentLength = Mathf.Max(0f, (sideLength - cappedDoorWidth) * 0.5f);
            if (sideSegmentLength > 0.05f)
            {
                float segmentOffset = cappedDoorWidth * 0.5f + sideSegmentLength * 0.5f;
                CreateWallSegment(room, side, -segmentOffset, sideSegmentLength, wallHeight, wallHeight * 0.5f, "Wall_DoorLeft");
                CreateWallSegment(room, side, segmentOffset, sideSegmentLength, wallHeight, wallHeight * 0.5f, "Wall_DoorRight");
            }

            float headerHeight = Mathf.Max(0f, wallHeight - doorHeight);
            if (headerHeight > 0.05f)
            {
                CreateWallSegment(room, side, 0f, cappedDoorWidth, headerHeight, doorHeight + headerHeight * 0.5f, "Wall_DoorHeader");
            }
        }

        private void CreateWallSegment(GeneratedRoom room, Vector2Int side, float sideOffset, float length, float height, float centerY, string name)
        {
            Vector3 outward = DirectionToVector(side);
            Vector3 tangent = IsEastWest(side) ? Vector3.forward : Vector3.right;
            Vector3 position = room.Center + outward * (SideDistance(room, side) + wallThickness * 0.5f) + tangent * sideOffset;
            position.y = centerY;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(room.Root.transform, false);
            wall.transform.position = position;
            wall.transform.localScale = IsEastWest(side)
                ? new Vector3(wallThickness, height, length)
                : new Vector3(length, height, wallThickness);
            Tint(wall, wallColor);
        }

        private void CreateRoomObstacles(GeneratedRoom room)
        {
            int maxCount = Mathf.Max(minObstaclesPerRoom, maxObstaclesPerRoom);
            int obstacleCount = _rng.Next(minObstaclesPerRoom, maxCount + 1);
            for (int i = 0; i < obstacleCount; i++)
            {
                if (!TryGetOpenRoomPosition(room, 2.4f, room.ReservedPositions, out Vector3 position))
                {
                    continue;
                }

                float width = RandomRange(1.4f, 3.4f);
                float depth = RandomRange(1.4f, 3.2f);
                float height = RandomRange(0.75f, obstacleHeight);

                GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = $"Obstacle_{i + 1:00}";
                obstacle.transform.SetParent(room.Root.transform, false);
                obstacle.transform.position = position + Vector3.up * (height * 0.5f);
                obstacle.transform.rotation = Quaternion.Euler(0f, _rng.Next(0, 4) * 90f, 0f);
                obstacle.transform.localScale = new Vector3(width, height, depth);
                Tint(obstacle, obstacleColor);
                room.ReservedPositions.Add(position);
            }
        }

        private void RebuildNavMesh()
        {
            if (navMeshSurface == null)
            {
                navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
            }

            navMeshSurface?.BuildNavMesh();
        }

        private void SpawnProgressionDoors()
        {
            for (int i = 0; i < _generatedRooms.Count - 1; i++)
            {
                GeneratedRoom room = _generatedRooms[i];
                Vector3 doorPosition = room.GetDoorPosition(room.ExitDirection);
                Quaternion doorRotation = Quaternion.LookRotation(DirectionToVector(room.ExitDirection), Vector3.up);
                DoorGate gate = CreateDoorGate(doorPosition, doorRotation, i);
                room.ForwardDoor = gate;
                _progressionDoors.Add(gate);
            }
        }

        private DoorGate CreateDoorGate(Vector3 position, Quaternion rotation, int index)
        {
            GameObject gateObject = new GameObject($"OneWayDoor_{index + 1:00}");
            gateObject.transform.SetParent(levelRoot, false);
            gateObject.transform.SetPositionAndRotation(position, rotation);

            Rigidbody gateRigidbody = gateObject.AddComponent<Rigidbody>();
            gateRigidbody.isKinematic = true;
            gateRigidbody.useGravity = false;

            BoxCollider trigger = gateObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(doorWidth, doorHeight, 2.5f);
            trigger.center = new Vector3(0f, doorHeight * 0.5f, 1.15f);

            NavMeshObstacle obstacle = gateObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.size = new Vector3(doorWidth, doorHeight, doorThickness);
            obstacle.center = new Vector3(0f, doorHeight * 0.5f, 0f);

            GameObject panelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelObject.name = "SlidingPanel";
            panelObject.transform.SetParent(gateObject.transform, false);
            panelObject.transform.localPosition = new Vector3(0f, doorHeight * 0.5f, 0f);
            panelObject.transform.localScale = new Vector3(doorWidth, doorHeight, doorThickness);
            Tint(panelObject, lockedDoorColor);

            DoorGate gate = gateObject.AddComponent<DoorGate>();
            gate.Configure(panelObject.transform, panelObject.GetComponent<Collider>(), trigger, obstacle, doorSlideHeight);
            return gate;
        }

        private void SpawnPlayer()
        {
            if (playerPrefab == null || _generatedRooms.Count == 0)
            {
                return;
            }

            GeneratedRoom startRoom = _generatedRooms[0];
            Vector3 awayFromDoor = startRoom.ExitDirection == Vector2Int.zero ? -North.ToVector3() : -DirectionToVector(startRoom.ExitDirection);
            Vector3 spawnPosition = startRoom.Center + awayFromDoor * Mathf.Min(4f, Mathf.Min(startRoom.Width, startRoom.Depth) * 0.25f);
            spawnPosition.y = 1f;
            Quaternion spawnRotation = startRoom.ExitDirection == Vector2Int.zero
                ? Quaternion.identity
                : Quaternion.LookRotation(DirectionToVector(startRoom.ExitDirection), Vector3.up);

            _player = Instantiate(playerPrefab, spawnPosition, spawnRotation, levelRoot);
            _player.name = "Player";
            GameStateManager.Instance?.RegisterPlayer(_player);
            FindAnyObjectByType<EclipseProtocol.UI.HUDController>()?.SetPlayer(_player);
            FindAnyObjectByType<CameraFollow3D>()?.SetTarget(_player.transform);
        }

        private void SpawnRoomContent()
        {
            int previousEnergyCount = int.MaxValue;
            int previousEnemyCount = 0;
            int previousPowerCount = 0;

            for (int i = 0; i < _generatedRooms.Count; i++)
            {
                GeneratedRoom room = _generatedRooms[i];
                int energyCount = Mathf.Min(previousEnergyCount, RollEnergyCellCount(i));
                int enemyCount = Mathf.Max(previousEnemyCount, RollEnemyCount(i));
                int powerCount = Mathf.Max(previousPowerCount, RollPowerCellCount(i));

                if (room.ForwardDoor != null && requireRepairPerRoom)
                {
                    powerCount = Mathf.Max(1, powerCount);
                }
                else if (room.ForwardDoor != null)
                {
                    powerCount = 0;
                }

                int placedEnergyCount = SpawnEnergyCells(room, energyCount);
                int placedPowerCount = SpawnPowerCells(room, powerCount);
                if (room.ForwardDoor != null)
                {
                    room.ForwardDoor.SetRequiredRepairCount(requireRepairPerRoom ? placedPowerCount : 0);
                }

                int placedEnemyCount = SpawnEnemies(room, enemyCount);

                previousEnergyCount = placedEnergyCount;
                previousEnemyCount = placedEnemyCount;
                previousPowerCount = placedPowerCount;

                _roomDebugSummaries.Add($"R{i + 1}: energy={placedEnergyCount}, enemies={placedEnemyCount}, power={placedPowerCount}, size={room.Width:0}x{room.Depth:0}, exit={DirectionName(room.ExitDirection)}");
            }
        }

        private int RollEnergyCellCount(int roomIndex)
        {
            float t = RoomProgress01(roomIndex);
            int ceiling = Mathf.RoundToInt(Mathf.Lerp(maxEnergyCellsFirstRoom, minEnergyCellsLastRoom, t));
            ceiling = Mathf.Max(0, ceiling);
            return ceiling <= 0 ? 0 : _rng.Next(0, ceiling + 1);
        }

        private int RollEnemyCount(int roomIndex)
        {
            float t = RoomProgress01(roomIndex);
            int ceiling = Mathf.RoundToInt(Mathf.Lerp(minEnemiesFirstRoom, maxEnemiesLastRoom, t));
            ceiling = Mathf.Max(0, ceiling);
            int floor = roomIndex == 0 ? 0 : Mathf.Min(ceiling, Mathf.Max(0, minEnemiesFirstRoom));
            return _rng.Next(floor, ceiling + 1);
        }

        private int RollPowerCellCount(int roomIndex)
        {
            float t = RoomProgress01(roomIndex);
            int ceiling = Mathf.RoundToInt(Mathf.Lerp(minPowerCellsFirstRoom, maxPowerCellsLastRoom, t));
            ceiling = Mathf.Max(0, ceiling);
            int floor = Mathf.Min(ceiling, Mathf.Max(0, minPowerCellsFirstRoom));
            return _rng.Next(floor, ceiling + 1);
        }

        private int SpawnEnergyCells(GeneratedRoom room, int count)
        {
            if (energyCellPrefab == null)
            {
                return 0;
            }

            int placedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetOpenRoomPosition(room, 2.2f, room.ReservedPositions, out Vector3 position))
                {
                    continue;
                }

                position.y = 0.75f;
                Instantiate(energyCellPrefab, position, Quaternion.identity, levelRoot);
                room.ReservedPositions.Add(position);
                _energyCellCount++;
                placedCount++;
            }

            return placedCount;
        }

        private int SpawnPowerCells(GeneratedRoom room, int count)
        {
            if (repairNodePrefab == null)
            {
                return 0;
            }

            int placedCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetOpenRoomPosition(room, 2.8f, room.ReservedPositions, out Vector3 position))
                {
                    continue;
                }

                RepairNode repairNode = Instantiate(repairNodePrefab, position, Quaternion.identity, levelRoot);
                repairNode.name = $"PowerNode_R{room.Index + 1:00}_{i + 1:00}";
                repairNode.Configure(balanceData, room.ForwardDoor);
                GameStateManager.Instance?.RegisterRepairNode(repairNode);
                room.ReservedPositions.Add(position);
                _repairNodeCount++;
                placedCount++;
            }

            return placedCount;
        }

        private int SpawnEnemies(GeneratedRoom room, int count)
        {
            if (patrolDronePrefab == null || hunterDronePrefab == null || _player == null)
            {
                return 0;
            }

            int placedCount = 0;
            int hunterSlot = ShouldSpawnHunterInRoom(room, count) ? count - 1 : -1;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetOpenRoomPosition(room, 3f, room.ReservedPositions, out Vector3 desiredPosition))
                {
                    continue;
                }

                Vector3 spawnPosition = SampleNavMesh(desiredPosition);
                IReadOnlyList<Transform> patrolRoute = CreatePatrolRoute(room, i);
                bool spawnHunter = i == hunterSlot;
                if (spawnHunter)
                {
                    HunterDroneAI hunter = Instantiate(hunterDronePrefab, spawnPosition, Quaternion.identity, levelRoot);
                    hunter.name = $"HunterDrone_R{room.Index + 1:00}_{i + 1:00}";
                    hunter.Initialize(balanceData, _player.transform, patrolRoute);
                    hunter.SetMovementBounds(room.BuildMovementBounds());
                    _hunterCount++;
                }
                else
                {
                    DronePatrolAI drone = Instantiate(patrolDronePrefab, spawnPosition, Quaternion.identity, levelRoot);
                    drone.name = $"PatrolDrone_R{room.Index + 1:00}_{i + 1:00}";
                    drone.Initialize(balanceData, patrolRoute);
                    drone.SetMovementBounds(room.BuildMovementBounds());
                }

                room.ReservedPositions.Add(spawnPosition);
                _enemyCount++;
                placedCount++;
            }

            return placedCount;
        }

        private bool ShouldSpawnHunterInRoom(GeneratedRoom room, int enemySlots)
        {
            if (enemySlots <= 0 || room.Index + 1 < firstHunterRoomNumber)
            {
                return false;
            }

            if (guaranteeHunterByFinalRoom && _hunterCount == 0 && room.Index == _generatedRooms.Count - 1)
            {
                return true;
            }

            float chance = Mathf.Lerp(hunterChanceFirstEligibleRoom, hunterChanceLastRoom, RoomProgress01(room.Index));
            return _rng.NextDouble() < chance;
        }

        private IReadOnlyList<Transform> CreatePatrolRoute(GeneratedRoom room, int enemyIndex)
        {
            List<Transform> waypoints = new List<Transform>();
            int waypointCount = _rng.Next(3, 5);
            GameObject routeRoot = new GameObject($"PatrolRoute_R{room.Index + 1:00}_{enemyIndex + 1:00}");
            routeRoot.transform.SetParent(room.Root.transform, false);

            for (int i = 0; i < waypointCount; i++)
            {
                if (!TryGetOpenRoomPosition(room, 2f, waypoints.ConvertAll(waypoint => waypoint.position), out Vector3 position))
                {
                    position = room.Center;
                }

                GameObject waypointObject = new GameObject($"Waypoint_{i + 1:00}");
                waypointObject.transform.SetParent(routeRoot.transform, false);
                waypointObject.transform.position = SampleNavMesh(position);
                waypoints.Add(waypointObject.transform);
            }

            return waypoints;
        }

        private void SpawnExtraction()
        {
            if (extractionTriggerPrefab == null || _generatedRooms.Count == 0)
            {
                return;
            }

            GeneratedRoom lastRoom = _generatedRooms[_generatedRooms.Count - 1];
            Vector3 awayFromEntry = lastRoom.EntryDirection == Vector2Int.zero ? North.ToVector3() : -DirectionToVector(lastRoom.EntryDirection);
            Vector3 position = lastRoom.Center + awayFromEntry * Mathf.Min(4f, Mathf.Min(lastRoom.Width, lastRoom.Depth) * 0.25f);
            position.y = 0f;
            Quaternion rotation = Quaternion.LookRotation(-awayFromEntry, Vector3.up);
            ExtractionTrigger extraction = Instantiate(extractionTriggerPrefab, position, rotation, levelRoot);
            extraction.name = "ExtractionTrigger";
            extraction.SetLocked(true);
        }

        private void SetupExplorationBlackout()
        {
            if (_player == null || _generatedRooms.Count == 0)
            {
                return;
            }

            List<RoomExplorationRegion> regions = new List<RoomExplorationRegion>(_generatedRooms.Count);
            for (int i = 0; i < _generatedRooms.Count; i++)
            {
                Bounds revealBounds = _generatedRooms[i].BuildVisibilityBounds(0f);
                Bounds maskBounds = _generatedRooms[i].BuildVisibilityBounds(wallThickness);
                regions.Add(new RoomExplorationRegion(revealBounds, maskBounds));
            }

            GameObject blackoutObject = new GameObject("RoomExplorationBlackout");
            blackoutObject.transform.SetParent(levelRoot, false);
            _blackoutController = blackoutObject.AddComponent<RoomExplorationBlackoutController>();
            _blackoutController.Configure(
                roomBlackoutMaterial,
                regions,
                _progressionDoors,
                _player.transform,
                blackoutOverlayHeight,
                blackoutRevealPadding,
                blackoutOverlayMargin);
        }

        private bool TryGetOpenRoomPosition(GeneratedRoom room, float clearance, IReadOnlyList<Vector3> reservedPositions, out Vector3 position)
        {
            float halfWidth = Mathf.Max(1f, room.Width * 0.5f - clearance);
            float halfDepth = Mathf.Max(1f, room.Depth * 0.5f - clearance);

            for (int attempt = 0; attempt < 48; attempt++)
            {
                Vector3 candidate = room.Center + new Vector3(RandomRange(-halfWidth, halfWidth), 0f, RandomRange(-halfDepth, halfDepth));
                if (IsReserved(candidate, reservedPositions, clearance) || IsNearDoorLane(room, candidate, clearance))
                {
                    continue;
                }

                position = candidate;
                return true;
            }

            position = room.Center;
            return false;
        }

        private bool IsReserved(Vector3 candidate, IReadOnlyList<Vector3> reservedPositions, float clearance)
        {
            float sqrClearance = clearance * clearance;
            for (int i = 0; i < reservedPositions.Count; i++)
            {
                Vector3 delta = candidate - reservedPositions[i];
                delta.y = 0f;
                if (delta.sqrMagnitude < sqrClearance)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsNearDoorLane(GeneratedRoom room, Vector3 candidate, float clearance)
        {
            if (room.EntryDirection != Vector2Int.zero && IsNearDoor(room, room.EntryDirection, candidate, clearance))
            {
                return true;
            }

            return room.ExitDirection != Vector2Int.zero && IsNearDoor(room, room.ExitDirection, candidate, clearance);
        }

        private bool IsNearDoor(GeneratedRoom room, Vector2Int direction, Vector3 candidate, float clearance)
        {
            Vector3 doorPosition = room.GetDoorPosition(direction);
            Vector3 delta = candidate - doorPosition;
            delta.y = 0f;
            return delta.sqrMagnitude < Mathf.Pow(Mathf.Max(clearance, doorWidth * 0.65f), 2f);
        }

        private Vector3 SampleNavMesh(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : position;
        }

        private float RollRoomWidth()
        {
            return RandomRange(Mathf.Min(roomWidthRange.x, roomWidthRange.y), Mathf.Max(roomWidthRange.x, roomWidthRange.y));
        }

        private float RollRoomDepth()
        {
            return RandomRange(Mathf.Min(roomDepthRange.x, roomDepthRange.y), Mathf.Max(roomDepthRange.x, roomDepthRange.y));
        }

        private float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)_rng.NextDouble());
        }

        private float RoomProgress01(int roomIndex)
        {
            return _generatedRooms.Count <= 1 ? 0f : Mathf.Clamp01(roomIndex / (float)(_generatedRooms.Count - 1));
        }

        private static float SideLength(GeneratedRoom room, Vector2Int side)
        {
            return IsEastWest(side) ? room.Depth : room.Width;
        }

        private static float SideDistance(GeneratedRoom room, Vector2Int side)
        {
            return IsEastWest(side) ? room.Width * 0.5f : room.Depth * 0.5f;
        }

        private static bool IsEastWest(Vector2Int direction)
        {
            return direction.x != 0;
        }

        private static Vector3 DirectionToVector(Vector2Int direction)
        {
            return new Vector3(direction.x, 0f, direction.y);
        }

        private static string DirectionName(Vector2Int direction)
        {
            if (direction == North) return "N";
            if (direction == East) return "E";
            if (direction == South) return "S";
            if (direction == West) return "W";
            return "none";
        }

        private static void Tint(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private sealed class GeneratedRoom
        {
            public GeneratedRoom(int index, Vector3 center, float width, float depth)
            {
                Index = index;
                Center = center;
                Width = width;
                Depth = depth;
                ReservedPositions.Add(center);
            }

            public int Index { get; }
            public Vector3 Center { get; }
            public float Width { get; }
            public float Depth { get; }
            public Vector2Int EntryDirection { get; set; }
            public Vector2Int ExitDirection { get; set; }
            public GameObject Root { get; set; }
            public DoorGate ForwardDoor { get; set; }
            public List<Vector3> ReservedPositions { get; } = new List<Vector3>();

            public bool HasDoorOn(Vector2Int direction)
            {
                return direction != Vector2Int.zero && (direction == EntryDirection || direction == ExitDirection);
            }

            public Vector3 GetDoorPosition(Vector2Int direction)
            {
                Vector3 outward = DirectionToVector(direction);
                float distance = IsEastWest(direction) ? Width * 0.5f : Depth * 0.5f;
                return Center + outward * (distance + 0.01f);
            }

            public Bounds BuildBounds(float padding)
            {
                return new Bounds(
                    Center,
                    new Vector3(Width + padding, 8f, Depth + padding));
            }

            public Bounds BuildMovementBounds()
            {
                return new Bounds(
                    Center + Vector3.up * 2f,
                    new Vector3(Width, 4f, Depth));
            }

            public Bounds BuildVisibilityBounds(float padding)
            {
                return new Bounds(
                    Center + Vector3.up * 2f,
                    new Vector3(Width + padding * 2f, 4f, Depth + padding * 2f));
            }
        }
    }

    internal static class Vector2IntRoomGenerationExtensions
    {
        public static Vector3 ToVector3(this Vector2Int direction)
        {
            return new Vector3(direction.x, 0f, direction.y);
        }
    }
}
