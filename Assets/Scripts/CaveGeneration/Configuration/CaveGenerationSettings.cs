using System;
using UnityEngine;

namespace CaveGeneration.Configuration
{
    [Serializable]
    public struct RoomLayoutSettings
    {
        public int roomCount;
        public float minRoomDistance;
        public float maxRoomDistance;
        public float roomPadding;
        public float minRoomRadius;
        public float maxRoomRadius;
        public bool enableFloorElevation;
        public float minFloorElevation;
        public float maxFloorElevation;
        public bool enableRoomHeightVariation;
        public float minWallHeight;
        public float maxWallHeight;

        public static RoomLayoutSettings Default => new RoomLayoutSettings
        {
            roomCount = 6,
            minRoomDistance = 20f,
            maxRoomDistance = 35f,
            roomPadding = 3.0f,
            minRoomRadius = 5f,
            maxRoomRadius = 9f,
            enableFloorElevation = true,
            minFloorElevation = -10.0f,
            maxFloorElevation = 10.0f,
            enableRoomHeightVariation = true,
            minWallHeight = 3.5f,
            maxWallHeight = 9.0f
        };
    }

    [Serializable]
    public struct RoomShapeSettings
    {
        public bool enableRandomRoomPointCount;
        public int minRoomPointCount;
        public int maxRoomPointCount;
        public float minRoomRadius;
        public float maxRoomRadius;
        public bool enableVariedRoomShapes;
        public float alcoveChance;
        public float maxAlcoveDepth;
        public bool enableElongatedChasms;
        public float chasmChance;

        public static RoomShapeSettings Default => new RoomShapeSettings
        {
            enableRandomRoomPointCount = true,
            minRoomPointCount = 6,
            maxRoomPointCount = 16,
            minRoomRadius = 5f,
            maxRoomRadius = 9f,
            enableVariedRoomShapes = true,
            alcoveChance = 0.6f,
            maxAlcoveDepth = 3.5f,
            enableElongatedChasms = true,
            chasmChance = 0.35f
        };
    }

    [Serializable]
    public struct CorridorNetworkSettings
    {
        public bool generateCorridors;
        public float corridorWidth;
        public float corridorHeight;
        public int maxConnectionsPerRoom;
        public float extraPathChance;

        public static CorridorNetworkSettings Default => new CorridorNetworkSettings
        {
            generateCorridors = true,
            corridorWidth = 4.0f,
            corridorHeight = 3.5f,
            maxConnectionsPerRoom = 3,
            extraPathChance = 0.6f
        };
    }

    [Serializable]
    public struct CorridorMeshSettings
    {
        public bool enableWindingCorridors;
        public float corridorWindingAmount;
        public float corridorWindingFrequency;
        public bool enableBranchingCorridors;
        public float branchChance;
        public float corridorWidth;
        public float corridorHeight;

        public static CorridorMeshSettings Default => new CorridorMeshSettings
        {
            enableWindingCorridors = true,
            corridorWindingAmount = 1.5f,
            corridorWindingFrequency = 2.0f,
            enableBranchingCorridors = true,
            branchChance = 0.5f,
            corridorWidth = 4.0f,
            corridorHeight = 3.5f
        };
    }

    [Serializable]
    public struct TerrainNoiseSettings
    {
        public bool enableOrganicNoise;
        public float floorNoiseScale;
        public float floorNoiseAmount;
        public float ceilingNoiseScale;
        public float ceilingNoiseAmount;

        public static TerrainNoiseSettings Default => new TerrainNoiseSettings
        {
            enableOrganicNoise = true,
            floorNoiseScale = 0.08f,
            floorNoiseAmount = 0.8f,
            ceilingNoiseScale = 0.06f,
            ceilingNoiseAmount = 1.5f
        };
    }

    [Serializable]
    public struct RoomFeatureSettings
    {
        public bool enableRoomPillars;
        public int maxPillarsPerRoom;
        public float minPillarRadius;
        public float maxPillarRadius;
        public int pillarPointCount;

        public bool enableStalactitesAndStalagmites;
        public int maxSpikesPerRoom;
        public float minSpikeHeight;
        public float maxSpikeHeight;
        public float minSpikeRadius;
        public float maxSpikeRadius;
        public int spikeSides;

        public float minRoomRadius;

        public static RoomFeatureSettings Default => new RoomFeatureSettings
        {
            enableRoomPillars = true,
            maxPillarsPerRoom = 2,
            minPillarRadius = 0.8f,
            maxPillarRadius = 2.0f,
            pillarPointCount = 8,
            enableStalactitesAndStalagmites = true,
            maxSpikesPerRoom = 8,
            minSpikeHeight = 1.0f,
            maxSpikeHeight = 3.0f,
            minSpikeRadius = 0.3f,
            maxSpikeRadius = 1.0f,
            spikeSides = 6,
            minRoomRadius = 5.0f
        };
    }

    [Serializable]
    public struct StructureToggleSettings
    {
        public bool generateFloor;
        public bool generateCeiling;
        public bool generateWalls;
        public bool invertNormals;

        public static StructureToggleSettings Default => new StructureToggleSettings
        {
            generateFloor = true,
            generateCeiling = true,
            generateWalls = true,
            invertNormals = false
        };
    }

    [Serializable]
    public struct PlayerSpawnSettings
    {
        public bool placePlayerOnStart;
        public Transform playerTransform;
        public float playerSpawnYOffset;

        public static PlayerSpawnSettings Default => new PlayerSpawnSettings
        {
            placePlayerOnStart = true,
            playerTransform = null,
            playerSpawnYOffset = 1.5f
        };
    }
}
