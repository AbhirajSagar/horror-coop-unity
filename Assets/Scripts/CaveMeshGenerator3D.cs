using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Geometry;
using CaveGeneration.Layout;
using CaveGeneration.MeshBuilders;
using CaveGeneration.Noise;
using CaveGeneration.Spawning;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
[RequireComponent(typeof(NetworkObject))]
public class CaveMeshGenerator3D : NetworkBehaviour
{
    [Header("Network Settings")]
    public bool generateOnAwake = true;
    [Range(2, 20)]
    public int roomCount = 6;
    public string seed = "DefaultSeed";

    [Header("Player Placement")]
    public bool placePlayerOnStart = true;
    public Transform playerTransform;
    public float playerSpawnYOffset = 1.5f;

    [Header("3D Verticality & Height Variation")]
    public bool enableRoomHeightVariation = true;
    [Range(2f, 15f)]
    public float minWallHeight = 3.5f;
    [Range(2f, 15f)]
    public float maxWallHeight = 9.0f;

    [Header("Floor Elevation Variation")]
    public bool enableFloorElevation = true;
    [Range(-50f, 50f)]
    public float minFloorElevation = -10.0f;
    [Range(-50f, 50f)]
    public float maxFloorElevation = 10.0f;

    [Header("Organic Terrain Noise")]
    public bool enableOrganicNoise = true;
    [Range(0.01f, 0.3f)]
    public float floorNoiseScale = 0.08f;
    [Range(0f, 3f)]
    public float floorNoiseAmount = 0.8f;
    [Range(0.01f, 0.3f)]
    public float ceilingNoiseScale = 0.06f;
    [Range(0f, 5f)]
    public float ceilingNoiseAmount = 1.5f;

    [Header("Structure Toggles")]
    public bool generateFloor = true;
    public bool generateCeiling = true;
    public bool generateWalls = true;
    public bool invertNormals = false;

    [Header("Corridor & Path Settings")]
    public bool generateCorridors = true;
    [Range(2f, 10f)]
    public float corridorWidth = 4.0f;
    [Range(2f, 10f)]
    public float corridorHeight = 3.5f;
    [Range(1, 5)]
    public int maxConnectionsPerRoom = 3;
    [Range(0f, 1f)]
    public float extraPathChance = 0.6f;

    [Header("Winding & Branching Passages")]
    public bool enableWindingCorridors = true;
    [Range(0f, 15f)]
    public float corridorWindingAmount = 1.5f;
    [Range(0.5f, 5f)]
    public float corridorWindingFrequency = 2.0f;
    public bool enableBranchingCorridors = true;
    [Range(0f, 1f)]
    public float branchChance = 0.5f;

    [Header("Room Features — Pillars & Columns")]
    public bool enableRoomPillars = true;
    [Range(0, 5)]
    public int maxPillarsPerRoom = 2;
    [Range(0.5f, 3.0f)]
    public float minPillarRadius = 0.8f;
    [Range(0.5f, 4.0f)]
    public float maxPillarRadius = 2.0f;
    [Range(4, 16)]
    public int pillarPointCount = 8;

    [Header("Room Features — Stalactites & Stalagmites")]
    public bool enableStalactitesAndStalagmites = true;
    [Range(0, 15)]
    public int maxSpikesPerRoom = 8;
    [Range(0.5f, 5.0f)]
    public float minSpikeHeight = 1.0f;
    [Range(0.5f, 8.0f)]
    public float maxSpikeHeight = 3.0f;
    [Range(0.2f, 2.0f)]
    public float minSpikeRadius = 0.3f;
    [Range(0.2f, 2.5f)]
    public float maxSpikeRadius = 1.0f;
    [Range(3, 12)]
    public int spikeSides = 6;

    [Header("Dramatic Room Shapes & Alcoves")]
    public bool enableVariedRoomShapes = true;
    [Range(0f, 1f)]
    public float alcoveChance = 0.6f;
    [Range(1.0f, 6.0f)]
    public float maxAlcoveDepth = 3.5f;
    public bool enableElongatedChasms = true;
    [Range(0f, 1f)]
    public float chasmChance = 0.35f;

    [Header("Room Polygon Settings")]
    public bool enableRandomRoomPointCount = true;
    [Range(3, 32)]
    public int minRoomPointCount = 6;
    [Range(3, 32)]
    public int maxRoomPointCount = 16;
    [Range(3, 32)]
    public float minRoomRadius = 5f;
    public float maxRoomRadius = 9f;

    [Header("Room Spacing Settings")]
    public float minRoomDistance = 20f;
    public float maxRoomDistance = 35f;
    [Range(0f, 20f)]
    public float roomPadding = 3.0f;

    [Header("Prefab & Prop Spawning")]
    public bool enablePrefabSpawning = true;
    public Transform propsParentContainer;
    public List<PrefabSpawnRule> spawnRules = new List<PrefabSpawnRule>();

    public static CaveMeshGenerator3D Instance { get; private set; }

    // Modular Generator Pipeline
    private readonly RoomLayoutGenerator roomLayoutGenerator = new RoomLayoutGenerator();
    private readonly RoomShapeGenerator roomShapeGenerator = new RoomShapeGenerator();
    private readonly CorridorNetworkGenerator corridorNetworkGenerator = new CorridorNetworkGenerator();
    private readonly RoomMeshBuilder roomMeshBuilder = new RoomMeshBuilder();
    private readonly RoomFeatureBuilder roomFeatureBuilder = new RoomFeatureBuilder();
    private readonly CorridorMeshBuilder corridorMeshBuilder = new CorridorMeshBuilder();
    private readonly CavePlayerSpawner playerSpawner = new CavePlayerSpawner();
    private readonly CavePrefabSpawner prefabSpawner = new CavePrefabSpawner();
    private TerrainHeightSampler heightSampler;

    private List<RoomData> lastGeneratedRooms = new List<RoomData>();
    private List<CorridorData> lastGeneratedCorridors = new List<CorridorData>();

    public IReadOnlyList<RoomData> GeneratedRooms => lastGeneratedRooms;
    public IReadOnlyList<CorridorData> GeneratedCorridors => lastGeneratedCorridors;
    public IReadOnlyList<SpawnedItemRecord> SpawnedProps => prefabSpawner.SpawnedRecords;

    private NetworkVariable<FixedString64Bytes> syncedSeed = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Keep reference to active instance
        }
        Instance = this;

        if (Application.isPlaying && generateOnAwake)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                seed = System.Guid.NewGuid().ToString().Substring(0, 8);
                Generate3DNetwork();
            }
        }
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (Application.isPlaying && !generateOnAwake)
        {
            Generate3DNetwork();
        }

        SetupNetworkManagerIntegration();
    }

    private void Update()
    {
        if (Application.isPlaying && NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.ConnectionApprovalCallback == null)
            {
                SetupNetworkManagerIntegration();
            }
        }
    }

    private void SetupNetworkManagerIntegration()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ConnectionApprovalCheck;
        }
    }

    private void ConnectionApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = true;

        Vector3 spawnPos = GetSpawnPositionForPlayer(request.ClientNetworkId);
        response.Position = spawnPos;
        response.Rotation = Quaternion.identity;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            syncedSeed.Value = new FixedString64Bytes(seed);
        }
        else
        {
            syncedSeed.OnValueChanged += OnSeedNetworkChanged;
            string serverSeed = syncedSeed.Value.ToString();
            if (!string.IsNullOrEmpty(serverSeed))
            {
                ApplyNetworkSeed(serverSeed);
            }
        }
    }

    private void OnSeedNetworkChanged(FixedString64Bytes oldSeed, FixedString64Bytes newSeed)
    {
        if (!IsServer)
        {
            ApplyNetworkSeed(newSeed.ToString());
        }
    }

    private void ApplyNetworkSeed(string newSeed)
    {
        if (string.IsNullOrEmpty(newSeed)) return;
        if (seed != newSeed)
        {
            seed = newSeed;
            Generate3DNetwork();
        }
    }

    /// <summary>
    /// Executes the complete procedural generation pipeline: layout -> shapes -> corridors -> mesh builders.
    /// </summary>
    public void Generate3DNetwork()
    {
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        RoomLayoutSettings layoutSettings = GetRoomLayoutSettings();
        RoomShapeSettings shapeSettings = GetRoomShapeSettings();
        CorridorNetworkSettings corridorNetworkSettings = GetCorridorNetworkSettings();
        CorridorMeshSettings corridorMeshSettings = GetCorridorMeshSettings();
        TerrainNoiseSettings noiseSettings = GetTerrainNoiseSettings();
        RoomFeatureSettings featureSettings = GetRoomFeatureSettings();
        StructureToggleSettings structureSettings = GetStructureToggleSettings();

        heightSampler = new TerrainHeightSampler(noiseSettings);

        // Step 1: Generate rooms with variable floor elevations and wall heights without clipping/overlapping
        List<RoomData> rooms = roomLayoutGenerator.GenerateRooms(layoutSettings, pseudoRandom);
        lastGeneratedRooms = rooms;

        // Step 1.2: Precompute base room perimeters with randomized point count per room
        List<Vector2>[] roomPerimeters = new List<Vector2>[rooms.Count];
        for (int r = 0; r < rooms.Count; r++)
        {
            roomPerimeters[r] = roomShapeGenerator.GenerateBaseRoomPerimeter(rooms[r], shapeSettings, pseudoRandom);
        }

        // Step 1.5: Build Corridor Connections (allowing multiple paths between rooms)
        List<CorridorData> corridors = new List<CorridorData>();
        if (generateCorridors)
        {
            corridors = corridorNetworkGenerator.GenerateCorridorNetwork(rooms, corridorNetworkSettings, pseudoRandom);
        }
        lastGeneratedCorridors = corridors;

        CaveMeshData meshData = new CaveMeshData();

        // Step 2: Build 3D Room Polygons, Wall Quads, Doorway Headers, Pillars, and Stalactites/Stalagmites
        for (int r = 0; r < rooms.Count; r++)
        {
            List<DoorwayData> doorways = CaveGeometryUtils.CalculateDoorways(
                r, rooms, roomPerimeters[r], corridors, corridorWidth);

            roomMeshBuilder.BuildRoomMesh(
                r,
                rooms[r],
                roomPerimeters[r],
                doorways,
                corridorWidth,
                corridorHeight,
                structureSettings,
                heightSampler,
                meshData,
                out List<Vector3> floorVerts,
                out List<Vector3> ceilVerts,
                out Vector3 floorCenter,
                out Vector3 ceilCenter
            );

            roomFeatureBuilder.BuildPillars(
                rooms[r],
                doorways,
                featureSettings,
                heightSampler,
                floorCenter,
                floorVerts,
                ceilCenter,
                ceilVerts,
                meshData,
                seed,
                invertNormals,
                corridorWidth
            );

            roomFeatureBuilder.BuildStalactitesAndStalagmites(
                rooms[r],
                doorways,
                featureSettings,
                heightSampler,
                floorCenter,
                floorVerts,
                ceilCenter,
                ceilVerts,
                meshData,
                seed,
                invertNormals,
                corridorWidth
            );
        }

        // Step 2.5: Build 3D Corridor Tunnels between rooms
        if (generateCorridors)
        {
            foreach (CorridorData corr in corridors)
            {
                corridorMeshBuilder.BuildCorridor(
                    corr,
                    rooms[corr.roomA],
                    rooms[corr.roomB],
                    roomPerimeters[corr.roomA],
                    roomPerimeters[corr.roomB],
                    corridorMeshSettings,
                    structureSettings,
                    heightSampler,
                    meshData
                );
            }
        }

        // Step 3: Flip winding order if invertNormals is checked
        if (invertNormals)
        {
            meshData.InvertNormals();
        }

        // Step 4: Assign to Mesh & Collider
        Mesh mesh = new Mesh();
        mesh.name = "Height-Varied 3D Rooms & Corridors";
        meshData.ApplyToMesh(mesh);

        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<MeshCollider>();
        }

        collider.sharedMesh = null;
        collider.sharedMesh = mesh;

        // Step 5: Spawn prefabs and props across rooms and corridors
        if (enablePrefabSpawning)
        {
            Transform container = GetOrCreatePropsContainer();
            List<Vector3> playerSpawnPositions = new List<Vector3>();
            for (int r = 0; r < rooms.Count; r++)
            {
                playerSpawnPositions.Add(playerSpawner.GetSpawnPosition(rooms, r, playerSpawnYOffset, heightSampler, rooms[r].center));
            }

            prefabSpawner.SpawnPrefabs(
                spawnRules,
                rooms,
                roomPerimeters,
                corridors,
                corridorWidth,
                corridorHeight,
                enableWindingCorridors,
                corridorWindingAmount,
                corridorWindingFrequency,
                heightSampler,
                seed,
                container,
                playerSpawnPositions
            );
        }

        // Step 6: Place player randomly in one of the generated rooms
        if (Application.isPlaying && placePlayerOnStart)
        {
            PlacePlayerInRandomRoom(rooms);
        }
    }

    public void ClearSpawnedPrefabs()
    {
        Transform container = GetOrCreatePropsContainer();
        prefabSpawner.ClearSpawnedProps(container);
    }

    private Transform GetOrCreatePropsContainer()
    {
        if (propsParentContainer != null) return propsParentContainer;

        Transform existing = transform.Find("GeneratedProps");
        if (existing != null) return existing;

        GameObject containerObj = new GameObject("GeneratedProps");
        containerObj.transform.SetParent(transform);
        containerObj.transform.localPosition = Vector3.zero;
        containerObj.transform.localRotation = Quaternion.identity;
        containerObj.transform.localScale = Vector3.one;
        return containerObj.transform;
    }

    public void GenerateRandomSeed()
    {
        seed = System.Guid.NewGuid().ToString().Substring(0, 8);
        Generate3DNetwork();
    }

    public void GenerateCave()
    {
        Generate3DNetwork();
    }

    public Vector3 GetSpawnPosition(int roomIndex = 0)
    {
        if (lastGeneratedRooms == null || lastGeneratedRooms.Count == 0)
        {
            if (Application.isPlaying)
            {
                Generate3DNetwork();
            }
        }

        if (heightSampler == null)
        {
            heightSampler = new TerrainHeightSampler(GetTerrainNoiseSettings());
        }

        return playerSpawner.GetSpawnPosition(lastGeneratedRooms, roomIndex, playerSpawnYOffset, heightSampler, transform.position);
    }

    public Vector3 GetRandomRoomSpawnPosition()
    {
        if (lastGeneratedRooms == null || lastGeneratedRooms.Count == 0) return GetSpawnPosition(0);
        int randomRoomIndex = Random.Range(0, lastGeneratedRooms.Count);
        return GetSpawnPosition(randomRoomIndex);
    }

    public Vector3 GetSpawnPositionForPlayer(ulong clientId)
    {
        if (lastGeneratedRooms == null || lastGeneratedRooms.Count == 0) return GetSpawnPosition(0);
        if (heightSampler == null)
        {
            heightSampler = new TerrainHeightSampler(GetTerrainNoiseSettings());
        }
        return playerSpawner.GetSpawnPositionForPlayer(lastGeneratedRooms, clientId, playerSpawnYOffset, heightSampler, transform.position);
    }

    public void PlacePlayer(Transform targetTransform, int roomIndex = -1)
    {
        if (targetTransform == null) return;
        Vector3 spawnPosition = (roomIndex < 0) ? GetRandomRoomSpawnPosition() : GetSpawnPosition(roomIndex);
        playerSpawner.PlacePlayer(targetTransform, spawnPosition);
    }

    public void PlacePlayerInRandomRoom()
    {
        PlacePlayerInRandomRoom(lastGeneratedRooms);
    }

    public void PlacePlayerInRandomRoom(List<RoomData> rooms)
    {
        if (heightSampler == null)
        {
            heightSampler = new TerrainHeightSampler(GetTerrainNoiseSettings());
        }
        playerSpawner.PlacePlayersInRooms(rooms, playerTransform, playerSpawnYOffset, heightSampler);
    }

    #region Settings Factory Methods

    public RoomLayoutSettings GetRoomLayoutSettings() => new RoomLayoutSettings
    {
        roomCount = roomCount,
        minRoomDistance = minRoomDistance,
        maxRoomDistance = maxRoomDistance,
        roomPadding = roomPadding,
        minRoomRadius = minRoomRadius,
        maxRoomRadius = maxRoomRadius,
        enableFloorElevation = enableFloorElevation,
        minFloorElevation = minFloorElevation,
        maxFloorElevation = maxFloorElevation,
        enableRoomHeightVariation = enableRoomHeightVariation,
        minWallHeight = minWallHeight,
        maxWallHeight = maxWallHeight
    };

    public RoomShapeSettings GetRoomShapeSettings() => new RoomShapeSettings
    {
        enableRandomRoomPointCount = enableRandomRoomPointCount,
        minRoomPointCount = minRoomPointCount,
        maxRoomPointCount = maxRoomPointCount,
        minRoomRadius = minRoomRadius,
        maxRoomRadius = maxRoomRadius,
        enableVariedRoomShapes = enableVariedRoomShapes,
        alcoveChance = alcoveChance,
        maxAlcoveDepth = maxAlcoveDepth,
        enableElongatedChasms = enableElongatedChasms,
        chasmChance = chasmChance
    };

    public CorridorNetworkSettings GetCorridorNetworkSettings() => new CorridorNetworkSettings
    {
        generateCorridors = generateCorridors,
        corridorWidth = corridorWidth,
        corridorHeight = corridorHeight,
        maxConnectionsPerRoom = maxConnectionsPerRoom,
        extraPathChance = extraPathChance
    };

    public CorridorMeshSettings GetCorridorMeshSettings() => new CorridorMeshSettings
    {
        enableWindingCorridors = enableWindingCorridors,
        corridorWindingAmount = corridorWindingAmount,
        corridorWindingFrequency = corridorWindingFrequency,
        enableBranchingCorridors = enableBranchingCorridors,
        branchChance = branchChance,
        corridorWidth = corridorWidth,
        corridorHeight = corridorHeight
    };

    public TerrainNoiseSettings GetTerrainNoiseSettings() => new TerrainNoiseSettings
    {
        enableOrganicNoise = enableOrganicNoise,
        floorNoiseScale = floorNoiseScale,
        floorNoiseAmount = floorNoiseAmount,
        ceilingNoiseScale = ceilingNoiseScale,
        ceilingNoiseAmount = ceilingNoiseAmount
    };

    public RoomFeatureSettings GetRoomFeatureSettings() => new RoomFeatureSettings
    {
        enableRoomPillars = enableRoomPillars,
        maxPillarsPerRoom = maxPillarsPerRoom,
        minPillarRadius = minPillarRadius,
        maxPillarRadius = maxPillarRadius,
        pillarPointCount = pillarPointCount,
        enableStalactitesAndStalagmites = enableStalactitesAndStalagmites,
        maxSpikesPerRoom = maxSpikesPerRoom,
        minSpikeHeight = minSpikeHeight,
        maxSpikeHeight = maxSpikeHeight,
        minSpikeRadius = minSpikeRadius,
        maxSpikeRadius = maxSpikeRadius,
        spikeSides = spikeSides,
        minRoomRadius = minRoomRadius
    };

    public StructureToggleSettings GetStructureToggleSettings() => new StructureToggleSettings
    {
        generateFloor = generateFloor,
        generateCeiling = generateCeiling,
        generateWalls = generateWalls,
        invertNormals = invertNormals
    };

    #endregion
}