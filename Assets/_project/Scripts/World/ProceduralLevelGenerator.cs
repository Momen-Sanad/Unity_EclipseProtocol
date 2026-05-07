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
        [Header("Config")]
        [SerializeField] private GameBalanceData balanceData;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private Transform levelRoot;
        [SerializeField] private NavMeshSurface navMeshSurface;

        [Header("Rooms")]
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

        [Header("Runtime Progression")]
        [SerializeField] private bool requireRepairPerRoom = true;
        [SerializeField, Min(2f)] private float doorWidth = 5f;
        [SerializeField, Min(2f)] private float doorHeight = 3.2f;
        [SerializeField, Min(0.1f)] private float doorThickness = 0.35f;
        [SerializeField, Min(1f)] private float doorSlideHeight = 3.6f;

        private readonly List<RoomModule> _spawnedRooms = new List<RoomModule>();
        private readonly List<Bounds> _occupiedBounds = new List<Bounds>();
        private readonly List<Transform> _pendingEnemySpawns = new List<Transform>();
        private readonly List<Transform> _pendingPickupSpawns = new List<Transform>();
        private readonly List<Transform[]> _pendingPatrolRoutes = new List<Transform[]>();
        private readonly List<DoorGate> _progressionDoors = new List<DoorGate>();
        private System.Random _rng;
        private PlayerController _player;
        private int _patrolDroneCount;
        private int _energyCellCount;
        private int _repairNodeCount;
        private bool _hunterSpawned;

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
            _occupiedBounds.Clear();
            _pendingEnemySpawns.Clear();
            _pendingPickupSpawns.Clear();
            _pendingPatrolRoutes.Clear();
            _progressionDoors.Clear();
            _patrolDroneCount = 0;
            _energyCellCount = 0;
            _repairNodeCount = 0;
            _hunterSpawned = false;

            EnsureLevelRoot();
            ClearLevelRoot();
            GameStateManager.Instance?.ResetRunObjectives();

            RoomModule startRoom = PlaceFirstRoom(startRoomPrefab);
            RoomModule currentRoom = startRoom;

            int corridorCount = balanceData != null ? balanceData.corridorRoomCount : 2;
            for (int i = 0; i < corridorCount; i++)
            {
                currentRoom = PlaceNextRoom(currentRoom, PickCorridorPrefab());
            }

            RoomModule nodeRoom = PlaceNextRoom(currentRoom, nodeRoomPrefab);
            RoomModule extractionRoom = PlaceNextRoom(nodeRoom, extractionRoomPrefab);

            RebuildNavMesh();
            SpawnPlayer(startRoom);
            SpawnProgressionDoors();
            SpawnRepairNodes(nodeRoom);
            SpawnExtraction(extractionRoom);
            SpawnRoomContent();

            Debug.Log($"[ProceduralLevelGenerator] Generated seed {ActiveSeed}: {_spawnedRooms.Count} rooms, {_repairNodeCount} repair nodes, {_progressionDoors.Count} doors, {_energyCellCount} energy cells, {_patrolDroneCount} patrol drones, hunter spawned: {_hunterSpawned}.", this);
        }

        private bool HasRequiredReferences()
        {
            List<string> missing = new List<string>();
            if (balanceData == null) missing.Add(nameof(balanceData));
            if (startRoomPrefab == null) missing.Add(nameof(startRoomPrefab));
            if (corridorRoomPrefabs == null || corridorRoomPrefabs.Length == 0) missing.Add(nameof(corridorRoomPrefabs));
            if (corridorRoomPrefabs != null)
            {
                for (int i = 0; i < corridorRoomPrefabs.Length; i++)
                {
                    if (corridorRoomPrefabs[i] == null)
                    {
                        missing.Add($"{nameof(corridorRoomPrefabs)}[{i}]");
                    }
                }
            }
            if (nodeRoomPrefab == null) missing.Add(nameof(nodeRoomPrefab));
            if (extractionRoomPrefab == null) missing.Add(nameof(extractionRoomPrefab));
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

        private RoomModule PlaceFirstRoom(RoomModule prefab)
        {
            RoomModule room = Instantiate(prefab, levelRoot);
            room.name = prefab.name;
            room.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            RegisterRoom(room);
            return room;
        }

        private RoomModule PlaceNextRoom(RoomModule previousRoom, RoomModule prefab)
        {
            if (previousRoom == null || previousRoom.ExitAnchor == null || prefab == null || prefab.EntryAnchor == null)
            {
                return previousRoom;
            }

            Transform previousExit = previousRoom.ExitAnchor.transform;
            Transform prefabEntry = prefab.EntryAnchor.transform;
            Quaternion rotation = previousExit.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(prefabEntry.localRotation);
            Vector3 position = previousExit.position - rotation * prefabEntry.localPosition;

            Bounds candidateBounds = prefab.BuildWorldBounds(position, rotation, balanceData != null ? balanceData.roomOverlapPadding : 0.25f);
            if (OverlapsExistingRoom(candidateBounds))
            {
                Debug.LogWarning($"[ProceduralLevelGenerator] Candidate room {prefab.name} overlapped existing geometry for seed {ActiveSeed}.", this);
            }

            RoomModule room = Instantiate(prefab, position, rotation, levelRoot);
            room.name = prefab.name;
            RegisterRoom(room);
            return room;
        }

        private RoomModule PickCorridorPrefab()
        {
            if (corridorRoomPrefabs == null || corridorRoomPrefabs.Length == 0)
            {
                return null;
            }

            return corridorRoomPrefabs[_rng.Next(0, corridorRoomPrefabs.Length)];
        }

        private void RegisterRoom(RoomModule room)
        {
            _spawnedRooms.Add(room);
            _occupiedBounds.Add(room.BuildWorldBounds(room.transform.position, room.transform.rotation, balanceData != null ? balanceData.roomOverlapPadding : 0.25f));

            if (room.EnemySpawnPoints != null && room.EnemySpawnPoints.Length > 0)
            {
                _pendingEnemySpawns.AddRange(room.EnemySpawnPoints);
            }

            if (room.PickupSpawnPoints != null && room.PickupSpawnPoints.Length > 0)
            {
                _pendingPickupSpawns.AddRange(room.PickupSpawnPoints);
            }

            if (room.PatrolWaypointPoints != null && room.PatrolWaypointPoints.Length > 1)
            {
                _pendingPatrolRoutes.Add(room.PatrolWaypointPoints);
            }
        }

        private bool OverlapsExistingRoom(Bounds candidateBounds)
        {
            for (int i = 0; i < _occupiedBounds.Count; i++)
            {
                if (_occupiedBounds[i].Intersects(candidateBounds))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildNavMesh()
        {
            if (navMeshSurface == null)
            {
                navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
            }

            navMeshSurface?.BuildNavMesh();
        }

        private void SpawnPlayer(RoomModule startRoom)
        {
            if (playerPrefab == null || startRoom == null)
            {
                return;
            }

            Transform spawnPoint = startRoom.PlayerSpawnPoint != null ? startRoom.PlayerSpawnPoint : startRoom.transform;
            _player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation, levelRoot);
            _player.name = "Player";
            GameStateManager.Instance?.RegisterPlayer(_player);
            FindAnyObjectByType<EclipseProtocol.UI.HUDController>()?.SetPlayer(_player);
            FindAnyObjectByType<CameraFollow3D>()?.SetTarget(_player.transform);
        }

        private void SpawnProgressionDoors()
        {
            for (int i = 0; i < _spawnedRooms.Count - 1; i++)
            {
                RoomModule room = _spawnedRooms[i];
                if (room == null || room.ExitAnchor == null)
                {
                    continue;
                }

                _progressionDoors.Add(CreateDoorGate(room.ExitAnchor.transform, i));
            }
        }

        private DoorGate CreateDoorGate(Transform exitAnchor, int index)
        {
            GameObject gateObject = new GameObject($"OneWayDoor_{index + 1:00}");
            gateObject.transform.SetParent(levelRoot, false);
            gateObject.transform.SetPositionAndRotation(exitAnchor.position, exitAnchor.rotation);

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

            Renderer panelRenderer = panelObject.GetComponent<Renderer>();
            if (panelRenderer != null)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                panelRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", new Color(1f, 0.35f, 0.12f));
                propertyBlock.SetColor("_Color", new Color(1f, 0.35f, 0.12f));
                panelRenderer.SetPropertyBlock(propertyBlock);
            }

            DoorGate gate = gateObject.AddComponent<DoorGate>();
            gate.Configure(panelObject.transform, panelObject.GetComponent<Collider>(), trigger, obstacle, doorSlideHeight);
            return gate;
        }

        private void SpawnRepairNodes(RoomModule fallbackNodeRoom)
        {
            if (repairNodePrefab == null)
            {
                return;
            }

            if (!requireRepairPerRoom)
            {
                SpawnRepairNode(fallbackNodeRoom, _progressionDoors.Count > 0 ? _progressionDoors[_progressionDoors.Count - 1] : null, 0);
                return;
            }

            for (int i = 0; i < _spawnedRooms.Count - 1; i++)
            {
                DoorGate linkedDoor = i < _progressionDoors.Count ? _progressionDoors[i] : null;
                SpawnRepairNode(_spawnedRooms[i], linkedDoor, i);
            }
        }

        private void SpawnRepairNode(RoomModule room, DoorGate linkedDoor, int index)
        {
            if (room == null)
            {
                return;
            }

            Transform socket = room.ObjectiveSocket != null ? room.ObjectiveSocket : room.transform;
            RepairNode repairNode = Instantiate(repairNodePrefab, socket.position, socket.rotation, levelRoot);
            repairNode.name = $"PowerNode_{index + 1:00}";
            repairNode.Configure(balanceData, linkedDoor);
            GameStateManager.Instance?.RegisterRepairNode(repairNode);
            _repairNodeCount++;
        }

        private void SpawnExtraction(RoomModule extractionRoom)
        {
            if (extractionTriggerPrefab == null || extractionRoom == null)
            {
                return;
            }

            Transform socket = extractionRoom.ObjectiveSocket != null ? extractionRoom.ObjectiveSocket : extractionRoom.transform;
            ExtractionTrigger extraction = Instantiate(extractionTriggerPrefab, socket.position, socket.rotation, levelRoot);
            extraction.name = "ExtractionTrigger";
            extraction.SetLocked(true);
        }

        private void SpawnRoomContent()
        {
            SpawnEnergyCells();
            SpawnPatrolDrones();
            SpawnHunter();
        }

        private void SpawnEnergyCells()
        {
            if (energyCellPrefab == null)
            {
                return;
            }

            int maxEnergyCells = balanceData != null ? balanceData.maxEnergyCells : 3;
            float chance = balanceData != null ? balanceData.corridorEnergyCellChance : 0.65f;
            for (int i = 0; i < _pendingPickupSpawns.Count && _energyCellCount < maxEnergyCells; i++)
            {
                bool shouldGuaranteeFirst = _energyCellCount == 0;
                if (!shouldGuaranteeFirst && _rng.NextDouble() > chance)
                {
                    continue;
                }

                Instantiate(energyCellPrefab, _pendingPickupSpawns[i].position, _pendingPickupSpawns[i].rotation, levelRoot);
                _energyCellCount++;
            }
        }

        private void SpawnPatrolDrones()
        {
            if (patrolDronePrefab == null)
            {
                return;
            }

            int maxPatrolDrones = balanceData != null ? balanceData.maxPatrolDrones : 2;
            float chance = balanceData != null ? balanceData.corridorPatrolDroneChance : 0.6f;
            for (int i = 0; i < _pendingPatrolRoutes.Count && _patrolDroneCount < maxPatrolDrones; i++)
            {
                bool shouldGuaranteeFirst = _patrolDroneCount == 0;
                if (!shouldGuaranteeFirst && _rng.NextDouble() > chance)
                {
                    continue;
                }

                Transform[] route = _pendingPatrolRoutes[i];
                Vector3 spawnPosition = SampleNavMesh(route[0].position);
                DronePatrolAI drone = Instantiate(patrolDronePrefab, spawnPosition, route[0].rotation, levelRoot);
                drone.name = "PatrolDrone";
                drone.Initialize(balanceData, route);
                _patrolDroneCount++;
            }
        }

        private void SpawnHunter()
        {
            if (hunterDronePrefab == null || _player == null || _pendingEnemySpawns.Count == 0)
            {
                return;
            }

            Transform spawn = _pendingEnemySpawns[_pendingEnemySpawns.Count - 1];
            HunterDroneAI hunter = Instantiate(hunterDronePrefab, SampleNavMesh(spawn.position), spawn.rotation, levelRoot);
            hunter.name = "HunterDrone";
            hunter.Initialize(balanceData, _player.transform, null);
            _hunterSpawned = true;
        }

        private Vector3 SampleNavMesh(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : position;
        }
    }
}
