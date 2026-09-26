using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using HCoop.Types;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Geometry;
using CaveGeneration.Noise;

namespace CaveGeneration.Spawning
{
    /// <summary>
    /// Deterministically spawns items, weapons, crystals, and decorations across rooms and corridors
    /// with strict bounding box and clearance verification against walls, ceilings, doorways, and existing items.
    /// </summary>
    public class CavePrefabSpawner
    {
        private readonly List<SpawnedItemRecord> spawnedRecords = new List<SpawnedItemRecord>();

        public IReadOnlyList<SpawnedItemRecord> SpawnedRecords => spawnedRecords;

        public void ClearSpawnedProps(Transform parentContainer)
        {
            if (parentContainer != null)
            {
                int childCount = parentContainer.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    Transform child = parentContainer.GetChild(i);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                        continue;
                    }
#endif
                    NetworkObject netObj = child.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    {
                        netObj.Despawn(true);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(child.gameObject);
                    }
                }
            }
            spawnedRecords.Clear();
        }

        public void SpawnPrefabs(
            List<PrefabSpawnRule> rules,
            List<RoomData> rooms,
            List<Vector2>[] roomPerimeters,
            List<CorridorData> corridors,
            float corridorWidth,
            float corridorHeight,
            bool enableWindingCorridors,
            float corridorWindingAmount,
            float corridorWindingFrequency,
            TerrainHeightSampler heightSampler,
            string seed,
            Transform parentContainer,
            List<Vector3> playerSpawnPositions = null)
        {
            ClearSpawnedProps(parentContainer);

            if (rules == null || rules.Count == 0 || rooms == null || rooms.Count == 0)
            {
                return;
            }

            // Deterministic pseudo-random sequence tied to seed
            System.Random rand = new System.Random(seed.GetHashCode() ^ 0x4F7A21B9);

            bool isMultiplayerClient = Application.isPlaying &&
                                      NetworkManager.Singleton != null &&
                                      NetworkManager.Singleton.IsListening &&
                                      !NetworkManager.Singleton.IsServer;

            foreach (PrefabSpawnRule rule in rules)
            {
                if (rule == null || rule.prefab == null) continue;

                // If running as client in Netcode multiplayer, don't instantiate networked objects (server replicates them)
                bool isNetworkedPrefab = rule.prefab.GetComponent<NetworkObject>() != null;
                if (isMultiplayerClient && isNetworkedPrefab)
                {
                    continue;
                }

                switch (rule.distributionMode)
                {
                    case SpawnDistributionMode.PerRoom:
                        SpawnPerRoom(rule, rooms, roomPerimeters, corridors, corridorWidth, heightSampler, rand, parentContainer, playerSpawnPositions);
                        break;

                    case SpawnDistributionMode.PerCorridor:
                        SpawnPerCorridor(rule, rooms, roomPerimeters, corridors, corridorWidth, corridorHeight,
                                         enableWindingCorridors, corridorWindingAmount, corridorWindingFrequency,
                                         heightSampler, rand, parentContainer);
                        break;

                    case SpawnDistributionMode.GlobalCount:
                        SpawnGlobalCount(rule, rooms, roomPerimeters, corridors, corridorWidth, corridorHeight,
                                         enableWindingCorridors, corridorWindingAmount, corridorWindingFrequency,
                                         heightSampler, rand, parentContainer, playerSpawnPositions);
                        break;
                }
            }
        }

        private void SpawnPerRoom(
            PrefabSpawnRule rule,
            List<RoomData> rooms,
            List<Vector2>[] roomPerimeters,
            List<CorridorData> corridors,
            float corridorWidth,
            TerrainHeightSampler heightSampler,
            System.Random rand,
            Transform parentContainer,
            List<Vector3> playerSpawnPositions)
        {
            if (rule.locationType == SpawnLocationType.CorridorsOnly) return;

            for (int r = 0; r < rooms.Count; r++)
            {
                if ((float)rand.NextDouble() > rule.spawnChance) continue;

                int count = rand.Next(rule.minCount, rule.maxCount + 1);
                List<DoorwayData> doorways = CaveGeometryUtils.CalculateDoorways(
                    r, rooms, roomPerimeters[r], corridors, corridorWidth);

                for (int i = 0; i < count; i++)
                {
                    TrySpawnInRoom(rule, r, rooms[r], roomPerimeters[r], doorways, heightSampler, rand, parentContainer, playerSpawnPositions);
                }
            }
        }

        private void SpawnPerCorridor(
            PrefabSpawnRule rule,
            List<RoomData> rooms,
            List<Vector2>[] roomPerimeters,
            List<CorridorData> corridors,
            float corridorWidth,
            float corridorHeight,
            bool enableWindingCorridors,
            float corridorWindingAmount,
            float corridorWindingFrequency,
            TerrainHeightSampler heightSampler,
            System.Random rand,
            Transform parentContainer)
        {
            if (rule.locationType == SpawnLocationType.RoomsOnly || corridors == null || corridors.Count == 0) return;

            foreach (CorridorData corr in corridors)
            {
                if ((float)rand.NextDouble() > rule.spawnChance) continue;

                int count = rand.Next(rule.minCount, rule.maxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    TrySpawnInCorridor(rule, corr, rooms, roomPerimeters, corridorWidth, corridorHeight,
                                       enableWindingCorridors, corridorWindingAmount, corridorWindingFrequency,
                                       heightSampler, rand, parentContainer);
                }
            }
        }

        private void SpawnGlobalCount(
            PrefabSpawnRule rule,
            List<RoomData> rooms,
            List<Vector2>[] roomPerimeters,
            List<CorridorData> corridors,
            float corridorWidth,
            float corridorHeight,
            bool enableWindingCorridors,
            float corridorWindingAmount,
            float corridorWindingFrequency,
            TerrainHeightSampler heightSampler,
            System.Random rand,
            Transform parentContainer,
            List<Vector3> playerSpawnPositions)
        {
            int total = rand.Next(rule.minCount, rule.maxCount + 1);
            if (total <= 0) return;

            bool canSpawnRooms = rule.locationType != SpawnLocationType.CorridorsOnly && rooms.Count > 0;
            bool canSpawnCorridors = rule.locationType != SpawnLocationType.RoomsOnly && corridors != null && corridors.Count > 0;

            for (int i = 0; i < total; i++)
            {
                bool targetRoom = canSpawnRooms;
                if (canSpawnRooms && canSpawnCorridors)
                {
                    targetRoom = rand.NextDouble() < 0.6; // 60% bias towards rooms
                }

                if (targetRoom)
                {
                    int rIdx = rand.Next(0, rooms.Count);
                    List<DoorwayData> doorways = CaveGeometryUtils.CalculateDoorways(
                        rIdx, rooms, roomPerimeters[rIdx], corridors, corridorWidth);
                    TrySpawnInRoom(rule, rIdx, rooms[rIdx], roomPerimeters[rIdx], doorways, heightSampler, rand, parentContainer, playerSpawnPositions);
                }
                else if (canSpawnCorridors)
                {
                    int cIdx = rand.Next(0, corridors.Count);
                    TrySpawnInCorridor(rule, corridors[cIdx], rooms, roomPerimeters, corridorWidth, corridorHeight,
                                       enableWindingCorridors, corridorWindingAmount, corridorWindingFrequency,
                                       heightSampler, rand, parentContainer);
                }
            }
        }

        private bool TrySpawnInRoom(
            PrefabSpawnRule rule,
            int roomIndex,
            RoomData room,
            List<Vector2> perimeter,
            List<DoorwayData> doorways,
            TerrainHeightSampler heightSampler,
            System.Random rand,
            Transform parentContainer,
            List<Vector3> playerSpawnPositions)
        {
            float requiredWallMargin = Mathf.Max(rule.clearanceBoxSize.x, rule.clearanceBoxSize.z) * 0.5f + rule.wallPadding;

            // Attempt candidate placement
            for (int attempt = 0; attempt < 35; attempt++)
            {
                float angle = (float)(rand.NextDouble() * Mathf.PI * 2f);
                float maxRadius = Mathf.Max(0.5f, room.avgRadius - requiredWallMargin);
                float dist = (float)rand.NextDouble() * maxRadius;

                float x = room.center.x + Mathf.Cos(angle) * dist;
                float z = room.center.z + Mathf.Sin(angle) * dist;
                Vector2 p2D = new Vector2(x, z);

                // 1. Polygon containment check
                if (!CaveGeometryUtils.IsPointInsidePolygon(p2D, perimeter)) continue;

                // 2. Wall clearance check
                float distToWall = CaveGeometryUtils.GetDistanceToPolygonBoundary(p2D, perimeter);
                if (distToWall < requiredWallMargin) continue;

                // 3. Vertical floor-to-ceiling clearance check
                float floorY = heightSampler.GetFloorY(x, z, room.floorElevation);
                float ceilY = heightSampler.GetCeilingY(x, z, room.floorElevation + room.wallHeight);
                float availableHeight = ceilY - floorY;

                if (availableHeight < rule.clearanceBoxSize.y + rule.positionOffset.y) continue;

                // 4. Doorway clearance check
                bool nearDoorway = false;
                if (doorways != null)
                {
                    foreach (DoorwayData dw in doorways)
                    {
                        if (Vector2.Distance(p2D, dw.center) < rule.doorwayPadding)
                        {
                            nearDoorway = true;
                            break;
                        }
                    }
                }
                if (nearDoorway) continue;

                // 5. Player spawn position clearance check
                Vector3 candidatePos = new Vector3(x, floorY, z);
                if (playerSpawnPositions != null)
                {
                    bool nearPlayer = false;
                    foreach (Vector3 spawnPos in playerSpawnPositions)
                    {
                        if (Vector3.Distance(candidatePos, spawnPos) < 2.5f)
                        {
                            nearPlayer = true;
                            break;
                        }
                    }
                    if (nearPlayer) continue;
                }

                // 6. Overlap check against previously spawned items
                Vector3 finalPos = candidatePos + rule.positionOffset;
                if (IsOverlappingExisting(finalPos, rule.clearanceBoxSize, rule.minDistanceBetweenItems))
                {
                    continue;
                }

                // Valid spawn position found!
                InstantiateAndRecord(rule, candidatePos, room.floorElevation, heightSampler, rand, parentContainer);
                return true;
            }

            return false;
        }

        private bool TrySpawnInCorridor(
            PrefabSpawnRule rule,
            CorridorData corr,
            List<RoomData> rooms,
            List<Vector2>[] roomPerimeters,
            float corridorWidth,
            float corridorHeight,
            bool enableWindingCorridors,
            float corridorWindingAmount,
            float corridorWindingFrequency,
            TerrainHeightSampler heightSampler,
            System.Random rand,
            Transform parentContainer)
        {
            RoomData roomA = rooms[corr.roomA];
            RoomData roomB = rooms[corr.roomB];

            Vector2 cA = new Vector2(roomA.center.x, roomA.center.z);
            Vector2 cB = new Vector2(roomB.center.x, roomB.center.z);

            Vector2 dirXZ = (cB - cA).normalized;
            if (dirXZ == Vector2.zero) return false;

            Vector2 rightXZ = new Vector2(-dirXZ.y, dirXZ.x);

            Vector2 startXZ = CaveGeometryUtils.GetRoomPerimeterIntersection(cA, roomPerimeters[corr.roomA], cB);
            Vector2 endXZ = CaveGeometryUtils.GetRoomPerimeterIntersection(cB, roomPerimeters[corr.roomB], cA);

            float corridorLength = Vector2.Distance(startXZ, endXZ);
            if (corridorLength < 3.0f) return false;

            float itemWidth = Mathf.Max(rule.clearanceBoxSize.x, rule.clearanceBoxSize.z);
            float maxAllowedLateral = (corridorWidth * 0.5f) - (itemWidth * 0.5f) - rule.wallPadding;
            if (maxAllowedLateral < 0f) return false; // Item too wide to fit in corridor

            float seedOffset = (float)(cA.x * 12.3f + cB.y * 45.6f);

            for (int attempt = 0; attempt < 35; attempt++)
            {
                // Sample progress along corridor avoiding entrance doorways
                float t = (float)rand.NextDouble() * 0.6f + 0.2f;
                Vector2 baseXZ = Vector2.Lerp(startXZ, endXZ, t);
                float elev = Mathf.Lerp(roomA.floorElevation, roomB.floorElevation, t);

                float envelope = Mathf.Sin(t * Mathf.PI);
                float windingOffset = 0f;
                if (enableWindingCorridors && corridorWindingAmount > 0f)
                {
                    float sineWave = Mathf.Sin(t * Mathf.PI * corridorWindingFrequency + seedOffset);
                    float perlinNoise = (Mathf.PerlinNoise(baseXZ.x * 0.05f + seedOffset, baseXZ.y * 0.05f + seedOffset) - 0.5f) * 2.0f;
                    windingOffset = (sineWave * 0.6f + perlinNoise * 0.4f) * corridorWindingAmount * envelope;
                }

                Vector2 centerXZ = baseXZ + rightXZ * windingOffset;

                // Lateral offset from corridor center line
                float lateralOffset = ((float)rand.NextDouble() * 2f - 1f) * (maxAllowedLateral * Mathf.Clamp01(rule.corridorLateralSpread));
                Vector2 posXZ = centerXZ + rightXZ * lateralOffset;

                // Check vertical clearance
                float floorY = heightSampler.GetFloorY(posXZ.x, posXZ.y, elev);
                float ceilY = heightSampler.GetCeilingY(posXZ.x, posXZ.y, elev + corridorHeight);
                float availableHeight = ceilY - floorY;

                if (availableHeight < rule.clearanceBoxSize.y + rule.positionOffset.y) continue;

                Vector3 candidatePos = new Vector3(posXZ.x, floorY, posXZ.y);
                Vector3 finalPos = candidatePos + rule.positionOffset;

                // Overlap check
                if (IsOverlappingExisting(finalPos, rule.clearanceBoxSize, rule.minDistanceBetweenItems))
                {
                    continue;
                }

                // Valid corridor position found!
                InstantiateAndRecord(rule, candidatePos, elev, heightSampler, rand, parentContainer);
                return true;
            }

            return false;
        }

        private bool IsOverlappingExisting(Vector3 candidatePos, Vector3 candidateSize, float minSeparation)
        {
            foreach (SpawnedItemRecord record in spawnedRecords)
            {
                if (record.Overlaps(candidatePos, candidateSize, minSeparation))
                {
                    return true;
                }
            }
            return false;
        }

        private void InstantiateAndRecord(
            PrefabSpawnRule rule,
            Vector3 candidateFloorPos,
            float baseElevation,
            TerrainHeightSampler heightSampler,
            System.Random rand,
            Transform parentContainer)
        {
            Vector3 spawnPos = candidateFloorPos + rule.positionOffset;

            // Rotation
            float yaw = rule.randomYaw ? (float)rand.NextDouble() * 360f : 0f;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

            if (rule.alignToSurfaceNormal && heightSampler != null)
            {
                float eps = 0.25f;
                float hC = candidateFloorPos.y;
                float hR = heightSampler.GetFloorY(candidateFloorPos.x + eps, candidateFloorPos.z, baseElevation);
                float hF = heightSampler.GetFloorY(candidateFloorPos.x, candidateFloorPos.z + eps, baseElevation);

                Vector3 vR = new Vector3(eps, hR - hC, 0f);
                Vector3 vF = new Vector3(0f, hF - hC, eps);
                Vector3 normal = Vector3.Cross(vF, vR).normalized;
                if (normal.y < 0f) normal = -normal;

                rot = Quaternion.FromToRotation(Vector3.up, normal) * rot;
            }

            // Scale
            Vector3 localScale = Vector3.one;
            if (rule.enableRandomScale)
            {
                float s = Mathf.Lerp(rule.minScale, rule.maxScale, (float)rand.NextDouble());
                localScale = Vector3.one * s;
            }

            // Instantiate
            Item instance = null;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject obj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(rule.prefab.gameObject, parentContainer);
                if (obj != null)
                {
                    obj.transform.position = spawnPos;
                    obj.transform.rotation = rot;
                    if (rule.enableRandomScale) obj.transform.localScale = localScale;
                    instance = obj.GetComponent<Item>();
                    if (instance != null)
                    {
                        instance.Initialize();
                    }
                    UnityEditor.Undo.RegisterCreatedObjectUndo(obj, "Spawn Cave Item");
                }
            }
            else
#endif
            {
                instance = UnityEngine.Object.Instantiate(rule.prefab, spawnPos, rot, parentContainer);
                if (instance != null)
                {
                    if (rule.enableRandomScale) instance.transform.localScale = localScale;
                    instance.Initialize();

                    NetworkObject netObj = instance.GetComponent<NetworkObject>();
                    if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.IsListening)
                    {
                        netObj.Spawn();
                    }
                }
            }

            // Record bounds
            Bounds bounds = new Bounds(spawnPos, rule.clearanceBoxSize);
            spawnedRecords.Add(new SpawnedItemRecord(spawnPos, rule.clearanceBoxSize, bounds, instance));
        }
    }
}
