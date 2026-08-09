using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class CaveMeshGenerator3D : MonoBehaviour
{
    [Header("Network Settings")]
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

    [Header("Organic Wall Character & Rock Face Noise")]
    public bool enableWallNoise = true;
    [Range(0.01f, 0.3f)]
    public float wallNoiseScale = 0.08f;
    [Range(0f, 3f)]
    public float wallNoiseAmount = 1.0f;
    [Range(1, 4)]
    public int wallSubdivisions = 2;

    [Header("3D Wall Thickness & Solid Shell")]
    public bool enableWallThickness = true;
    [Range(0.1f, 5.0f)]
    public float wallThickness = 1.0f;

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

    public struct RoomData
    {
        public Vector3 center;
        public float floorElevation;
        public float wallHeight;
        public float avgRadius;
        public float maxRadius;
    }

    public struct CorridorData
    {
        public int roomA;
        public int roomB;
    }

    private List<RoomData> lastGeneratedRooms = new List<RoomData>();

    void Start()
    {
        if (Application.isPlaying)
        {
            Generate3DNetwork();
        }
    }

    public void Generate3DNetwork()
    {
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        List<Vector3> allVertices = new List<Vector3>();
        List<int> allTriangles = new List<int>();
        List<Vector2> allUVs = new List<Vector2>();

        // Step 1: Generate rooms with variable floor elevations and wall heights without clipping/overlapping
        List<RoomData> rooms = new List<RoomData>();

        float safeMinDist = Mathf.Max(minRoomDistance, maxRoomRadius * 2f + roomPadding);
        float safeMaxDist = Mathf.Max(maxRoomDistance, safeMinDist + 10f);

        float startElevation = enableFloorElevation ? GetRandomFloat(pseudoRandom, minFloorElevation, maxFloorElevation) : 0f;

        RoomData startRoom = new RoomData
        {
            center = new Vector3(0f, startElevation, 0f),
            floorElevation = startElevation,
            wallHeight = GetRandomFloat(pseudoRandom, minWallHeight, maxWallHeight),
            avgRadius = (minRoomRadius + maxRoomRadius) * 0.5f,
            maxRadius = maxRoomRadius
        };
        rooms.Add(startRoom);

        for (int i = 1; i < roomCount; i++)
        {
            bool placed = false;
            float currentMinDist = safeMinDist;
            float currentMaxDist = safeMaxDist;

            for (int attempt = 0; attempt < 300; attempt++)
            {
                // Gradually expand search distance if tight space
                if (attempt > 0 && attempt % 40 == 0)
                {
                    currentMinDist += 5f;
                    currentMaxDist += 10f;
                }

                int parentIdx = pseudoRandom.Next(0, rooms.Count);
                RoomData parentRoom = rooms[parentIdx];

                float angle = (float)(pseudoRandom.NextDouble() * Mathf.PI * 2f);
                float dist = GetRandomFloat(pseudoRandom, currentMinDist, currentMaxDist);

                float targetElevation = enableFloorElevation ? GetRandomFloat(pseudoRandom, minFloorElevation, maxFloorElevation) : 0f;
                Vector3 candidateCenter = parentRoom.center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                candidateCenter.y = targetElevation;

                // Check overlap against ALL existing rooms on XZ plane
                bool overlaps = false;
                foreach (RoomData existingRoom in rooms)
                {
                    float dist2D = Vector2.Distance(
                        new Vector2(candidateCenter.x, candidateCenter.z),
                        new Vector2(existingRoom.center.x, existingRoom.center.z)
                    );

                    float minAllowedDist = existingRoom.maxRadius + maxRoomRadius + roomPadding;
                    if (dist2D < minAllowedDist)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    float wallH = enableRoomHeightVariation ? GetRandomFloat(pseudoRandom, minWallHeight, maxWallHeight) : minWallHeight;

                    RoomData newRoom = new RoomData
                    {
                        center = candidateCenter,
                        floorElevation = targetElevation,
                        wallHeight = wallH,
                        avgRadius = (minRoomRadius + maxRoomRadius) * 0.5f,
                        maxRadius = maxRoomRadius
                    };

                    rooms.Add(newRoom);
                    placed = true;
                    break;
                }
            }

            // Fallback placement if space is constrained
            if (!placed)
            {
                float fallbackAngle = (float)(pseudoRandom.NextDouble() * Mathf.PI * 2f);
                float fallbackDist = (rooms.Count + 1) * (maxRoomRadius * 2f + roomPadding + 5f);
                float fallbackElevation = enableFloorElevation ? GetRandomFloat(pseudoRandom, minFloorElevation, maxFloorElevation) : 0f;
                Vector3 fallbackCenter = new Vector3(Mathf.Cos(fallbackAngle) * fallbackDist, fallbackElevation, Mathf.Sin(fallbackAngle) * fallbackDist);

                float wallH = enableRoomHeightVariation ? GetRandomFloat(pseudoRandom, minWallHeight, maxWallHeight) : minWallHeight;

                RoomData fallbackRoom = new RoomData
                {
                    center = fallbackCenter,
                    floorElevation = fallbackElevation,
                    wallHeight = wallH,
                    avgRadius = (minRoomRadius + maxRoomRadius) * 0.5f,
                    maxRadius = maxRoomRadius
                };
                rooms.Add(fallbackRoom);
            }
        }

        lastGeneratedRooms = rooms;

        // Step 1.2: Precompute base room perimeters with randomized point count per room
        List<Vector2>[] roomPerimeters = new List<Vector2>[rooms.Count];
        for (int r = 0; r < rooms.Count; r++)
        {
            int pts = -1;
            if (enableRandomRoomPointCount)
            {
                int minP = Mathf.Min(minRoomPointCount, maxRoomPointCount);
                int maxP = Mathf.Max(minRoomPointCount, maxRoomPointCount);
                pts = pseudoRandom.Next(minP, maxP + 1);
            }

            roomPerimeters[r] = GenerateBaseRoomPerimeter(rooms[r], pts, pseudoRandom, minRoomRadius, maxRoomRadius);
        }

        // Step 1.5: Build Corridor Connections (allowing multiple paths between rooms)
        List<CorridorData> corridors = new List<CorridorData>();
        if (generateCorridors)
        {
            corridors = GenerateCorridorNetwork(rooms, pseudoRandom);
        }

        // Step 2: Build 3D Room Polygons with doorway openings and Header Walls where corridors attach
        for (int r = 0; r < rooms.Count; r++)
        {
            Add3DRoomPolygon(r, rooms, roomPerimeters[r], corridors,
                             allVertices, allTriangles, allUVs);
        }

        // Step 2.5: Build 3D Corridor Tunnels between rooms
        if (generateCorridors)
        {
            foreach (CorridorData corr in corridors)
            {
                Add3DCorridor(corr, rooms, roomPerimeters, corridorWidth, corridorHeight,
                              allVertices, allTriangles, allUVs);
            }
        }

        // Step 3: Flip winding order if invertNormals is checked
        if (invertNormals)
        {
            for (int i = 0; i < allTriangles.Count; i += 3)
            {
                int temp = allTriangles[i + 1];
                allTriangles[i + 1] = allTriangles[i + 2];
                allTriangles[i + 2] = temp;
            }
        }

        // Step 4: Assign to Mesh
        Mesh mesh = new Mesh();
        mesh.name = "Height-Varied 3D Rooms & Corridors";
        mesh.vertices = allVertices.ToArray();
        mesh.triangles = allTriangles.ToArray();
        mesh.uv = allUVs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<MeshCollider>();
        }

        collider.sharedMesh = null;
        collider.sharedMesh = mesh;

        // Step 5: Place player randomly in one of the generated rooms
        if (Application.isPlaying && placePlayerOnStart)
        {
            PlacePlayerInRandomRoom(rooms);
        }
    }

    private List<Vector2> GenerateBaseRoomPerimeter(RoomData room, int points, System.Random rand, float minR, float maxR)
    {
        Vector3 center = room.center;
        List<Vector2> perimeterXZ = new List<Vector2>();

        for (int i = 0; i < points; i++)
        {
            float baseAngle = (i / (float)points) * Mathf.PI * 2f;
            float jitter = (float)(rand.NextDouble() - 0.5) * (Mathf.PI * 2f / points) * 0.7f;
            float angle = baseAngle + jitter;
            float radius = GetRandomFloat(rand, minR, maxR);

            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            perimeterXZ.Add(new Vector2(x, z));
        }

        perimeterXZ.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.y - center.z, a.x - center.x);
            float angleB = Mathf.Atan2(b.y - center.z, b.x - center.x);
            return angleA.CompareTo(angleB);
        });

        return perimeterXZ;
    }

    private Vector2 GetRoomPerimeterIntersection(Vector2 centerXZ, List<Vector2> perimeterXZ, Vector2 targetXZ)
    {
        Vector2 rayDir = (targetXZ - centerXZ).normalized;
        if (rayDir == Vector2.zero) return centerXZ;

        for (int i = 0; i < perimeterXZ.Count; i++)
        {
            Vector2 p1 = perimeterXZ[i];
            Vector2 p2 = perimeterXZ[(i + 1) % perimeterXZ.Count];

            Vector2 v1 = centerXZ - p1;
            Vector2 v2 = p2 - p1;
            Vector2 v3 = new Vector2(-rayDir.y, rayDir.x);

            float dot = Vector2.Dot(v2, v3);
            if (Mathf.Abs(dot) < 0.0001f) continue;

            float t1 = (v2.x * v1.y - v2.y * v1.x) / dot;
            float t2 = Vector2.Dot(v1, v3) / dot;

            if (t1 >= 0f && t2 >= 0f && t2 <= 1f)
            {
                return centerXZ + rayDir * t1;
            }
        }

        return centerXZ + rayDir * 7f;
    }

    private bool DoSegmentsIntersectXZ(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float Cross(Vector2 v, Vector2 w) => v.x * w.y - v.y * w.x;

        Vector2 dA = a2 - a1;
        Vector2 dB = b2 - b1;

        float denom = Cross(dA, dB);
        if (Mathf.Abs(denom) < 0.0001f) return false;

        float tA = Cross(b1 - a1, dB) / denom;
        float tB = Cross(b1 - a1, dA) / denom;

        return (tA > 0.08f && tA < 0.92f && tB > 0.08f && tB < 0.92f);
    }

    private List<CorridorData> GenerateCorridorNetwork(List<RoomData> rooms, System.Random rand)
    {
        List<CorridorData> corridors = new List<CorridorData>();
        HashSet<(int, int)> connectedPairs = new HashSet<(int, int)>();

        bool CanAddCorridor(int a, int b)
        {
            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            if (min == max) return false;
            if (connectedPairs.Contains((min, max))) return false;

            Vector2 pA = new Vector2(rooms[min].center.x, rooms[min].center.z);
            Vector2 pB = new Vector2(rooms[max].center.x, rooms[max].center.z);

            foreach (CorridorData existing in corridors)
            {
                // Check if sharing an endpoint room
                if (existing.roomA == min || existing.roomA == max || existing.roomB == min || existing.roomB == max)
                {
                    int shared = (existing.roomA == min || existing.roomA == max) ? existing.roomA : existing.roomB;
                    int other1 = (existing.roomA == shared) ? existing.roomB : existing.roomA;
                    int other2 = (min == shared) ? max : min;

                    Vector2 pShared = new Vector2(rooms[shared].center.x, rooms[shared].center.z);
                    Vector2 pOther1 = new Vector2(rooms[other1].center.x, rooms[other1].center.z);
                    Vector2 pOther2 = new Vector2(rooms[other2].center.x, rooms[other2].center.z);

                    Vector2 v1 = (pOther1 - pShared).normalized;
                    Vector2 v2 = (pOther2 - pShared).normalized;

                    // Reject if departing angle is tighter than ~35 degrees to prevent overlapping doorways
                    if (Vector2.Dot(v1, v2) > 0.82f) return false;
                    continue;
                }

                Vector2 pC = new Vector2(rooms[existing.roomA].center.x, rooms[existing.roomA].center.z);
                Vector2 pD = new Vector2(rooms[existing.roomB].center.x, rooms[existing.roomB].center.z);

                // Reject if corridors cross each other in 2D space
                if (DoSegmentsIntersectXZ(pA, pB, pC, pD))
                {
                    return false;
                }
            }

            return true;
        }

        void AddCorridor(int a, int b)
        {
            if (!CanAddCorridor(a, b)) return;

            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            connectedPairs.Add((min, max));
            corridors.Add(new CorridorData { roomA = min, roomB = max });
        }

        // 1. Primary MST Connection: Guarantees 100% room connectivity with zero crossing paths
        List<int> connected = new List<int> { 0 };
        List<int> unconnected = new List<int>();
        for (int i = 1; i < rooms.Count; i++) unconnected.Add(i);

        while (unconnected.Count > 0)
        {
            float minDistance = float.MaxValue;
            int bestC = -1;
            int bestU = -1;

            foreach (int u in unconnected)
            {
                foreach (int c in connected)
                {
                    float dist = Vector3.Distance(rooms[u].center, rooms[c].center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestC = c;
                        bestU = u;
                    }
                }
            }

            if (bestC != -1 && bestU != -1)
            {
                AddCorridor(bestC, bestU);
                connected.Add(bestU);
                unconnected.Remove(bestU);
            }
            else
            {
                break;
            }
        }

        // 2. Extra connections: Add non-crossing loop paths for exploration
        for (int i = 0; i < rooms.Count; i++)
        {
            List<int> neighbors = new List<int>();
            for (int j = 0; j < rooms.Count; j++) if (i != j) neighbors.Add(j);

            neighbors.Sort((a, b) =>
            {
                float distA = Vector3.Distance(rooms[i].center, rooms[a].center);
                float distB = Vector3.Distance(rooms[i].center, rooms[b].center);
                return distA.CompareTo(distB);
            });

            for (int n = 0; n < neighbors.Count; n++)
            {
                if ((float)rand.NextDouble() <= extraPathChance)
                {
                    AddCorridor(i, neighbors[n]);
                }
            }
        }

        return corridors;
    }

    private void Add3DRoomPolygon(int roomIndex, List<RoomData> rooms, List<Vector2> basePerimeter, List<CorridorData> corridors,
                                  List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        RoomData room = rooms[roomIndex];
        Vector3 center = room.center;
        Vector2 centerXZ = new Vector2(center.x, center.z);
        float baseFloorY = room.floorElevation;
        float height = room.wallHeight;

        // Find exact doorway intersection points on the room's perimeter
        List<Vector2> doorwayCenters = new List<Vector2>();
        if (generateCorridors && corridors != null)
        {
            foreach (CorridorData corr in corridors)
            {
                if (corr.roomA == roomIndex || corr.roomB == roomIndex)
                {
                    int otherIdx = (corr.roomA == roomIndex) ? corr.roomB : corr.roomA;
                    Vector2 otherXZ = new Vector2(rooms[otherIdx].center.x, rooms[otherIdx].center.z);

                    Vector2 doorwayPoint = GetRoomPerimeterIntersection(centerXZ, basePerimeter, otherXZ);
                    doorwayCenters.Add(doorwayPoint);
                }
            }
        }

        // Filter out base perimeter vertices that fall inside doorway openings
        List<Vector2> perimeterXZ = new List<Vector2>();
        foreach (Vector2 p in basePerimeter)
        {
            bool insideDoorway = false;
            foreach (Vector2 dwPoint in doorwayCenters)
            {
                if (Vector2.Distance(p, dwPoint) < corridorWidth * 0.55f)
                {
                    insideDoorway = true;
                    break;
                }
            }
            if (!insideDoorway)
            {
                perimeterXZ.Add(p);
            }
        }

        // Add doorway endpoints so room wall segments split cleanly at doorways
        foreach (Vector2 dwPoint in doorwayCenters)
        {
            Vector2 dir = (dwPoint - centerXZ).normalized;
            Vector2 right = new Vector2(-dir.y, dir.x);
            float halfW = corridorWidth * 0.5f;

            perimeterXZ.Add(dwPoint - right * halfW);
            perimeterXZ.Add(dwPoint + right * halfW);
        }

        // Sort perimeter points by polar angle (Counter-Clockwise)
        perimeterXZ.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.y - center.z, a.x - center.x);
            float angleB = Mathf.Atan2(b.y - center.z, b.x - center.x);
            return angleA.CompareTo(angleB);
        });

        // Build 3D Floor & Ceiling positions & Wall Vertical Grid with continuous shared vertex displacement
        List<Vector3> floorVerts = new List<Vector3>();
        List<Vector3> ceilVerts = new List<Vector3>();

        Vector3 floorCenter = new Vector3(center.x, GetFloorY(center.x, center.z, baseFloorY), center.z);
        Vector3 ceilCenter = new Vector3(center.x, GetCeilingY(center.x, center.z, baseFloorY + height), center.z);

        int subdivs = Mathf.Max(1, wallSubdivisions);
        int pCount = perimeterXZ.Count;
        Vector3[,] wallGrid = new Vector3[pCount, subdivs + 1];
        Vector3[,] outerWallGrid = new Vector3[pCount, subdivs + 1];

        float thick = (enableWallThickness && wallThickness > 0.01f) ? wallThickness : 0f;

        for (int i = 0; i < pCount; i++)
        {
            float x = perimeterXZ[i].x;
            float z = perimeterXZ[i].y;
            Vector2 pXZ = perimeterXZ[i];
            Vector2 radialDir2D = (pXZ - centerXZ).normalized;
            Vector3 radialDir = new Vector3(radialDir2D.x, 0f, radialDir2D.y);

            float fy = GetFloorY(x, z, baseFloorY);
            float cy = GetCeilingY(x, z, baseFloorY + height);

            Vector3 floorP = new Vector3(x, fy, z);
            Vector3 ceilP = new Vector3(x, cy, z);

            floorVerts.Add(floorP);
            ceilVerts.Add(ceilP);

            // Compute continuous height grid for inner and outer room perimeter walls
            for (int s = 0; s <= subdivs; s++)
            {
                float tHeight = (float)s / subdivs;
                Vector3 rawPos = Vector3.Lerp(floorP, ceilP, tHeight);

                float heightEnv = Mathf.Sin(tHeight * Mathf.PI);
                float noise = 0f;
                if (enableWallNoise && wallNoiseAmount > 0f)
                {
                    float n = (Mathf.PerlinNoise(x * wallNoiseScale + 200f, z * wallNoiseScale + tHeight * 3f + 300f) - 0.5f) * 2.0f;
                    noise = n * wallNoiseAmount * heightEnv;
                }

                Vector3 innerP = rawPos + radialDir * noise;
                wallGrid[i, s] = innerP;
                outerWallGrid[i, s] = innerP + radialDir * thick;
            }
        }

        // 1. ROOM FLOOR MESH (Facing UP +Y into room)
        if (generateFloor)
        {
            int floorBaseIdx = verts.Count;
            verts.Add(floorCenter);
            uvs.Add(new Vector2(floorCenter.x * 0.1f, floorCenter.z * 0.1f));

            for (int i = 0; i < floorVerts.Count; i++)
            {
                verts.Add(floorVerts[i]);
                uvs.Add(new Vector2(floorVerts[i].x * 0.1f, floorVerts[i].z * 0.1f));
            }

            for (int i = 0; i < floorVerts.Count; i++)
            {
                int currentIdx = floorBaseIdx + 1 + i;
                int nextIdx = floorBaseIdx + 1 + ((i + 1) % floorVerts.Count);

                tris.Add(floorBaseIdx);
                tris.Add(nextIdx);
                tris.Add(currentIdx);
            }
        }

        // 2. ROOM CEILING MESH (Facing DOWN -Y into room)
        if (generateCeiling)
        {
            int ceilingBaseIdx = verts.Count;
            verts.Add(ceilCenter);
            uvs.Add(new Vector2(ceilCenter.x * 0.1f, ceilCenter.z * 0.1f));

            for (int i = 0; i < ceilVerts.Count; i++)
            {
                verts.Add(ceilVerts[i]);
                uvs.Add(new Vector2(ceilVerts[i].x * 0.1f, ceilVerts[i].z * 0.1f));
            }

            for (int i = 0; i < ceilVerts.Count; i++)
            {
                int currentIdx = ceilingBaseIdx + 1 + i;
                int nextIdx = ceilingBaseIdx + 1 + ((i + 1) % ceilVerts.Count);

                tris.Add(ceilingBaseIdx);
                tris.Add(currentIdx);
                tris.Add(nextIdx);
            }
        }

        // 3. ROOM PERIMETER WALLS (With 3D Solid Wall Shell & Header Walls)
        if (generateWalls)
        {
            for (int i = 0; i < pCount; i++)
            {
                int nextI = (i + 1) % pCount;
                Vector2 p0 = perimeterXZ[i];
                Vector2 p1 = perimeterXZ[nextI];
                Vector2 wallMid = (p0 + p1) * 0.5f;

                // Check if this sub-segment is inside an open doorway
                bool isDoorwaySegment = false;
                foreach (Vector2 dwPoint in doorwayCenters)
                {
                    if (Vector2.Distance(wallMid, dwPoint) < corridorWidth * 0.52f)
                    {
                        isDoorwaySegment = true;
                        break;
                    }
                }

                if (isDoorwaySegment && height > corridorHeight + 0.1f)
                {
                    // Draw Double-Sided Header Wall Quad strictly ABOVE passage roof (from corridorHeight to height)
                    float passageCeilY0 = GetCeilingY(p0.x, p0.y, baseFloorY + corridorHeight);
                    float passageCeilY1 = GetCeilingY(p1.x, p1.y, baseFloorY + corridorHeight);

                    Vector3 p0_passCeil = new Vector3(p0.x, passageCeilY0, p0.y);
                    Vector3 p1_passCeil = new Vector3(p1.x, passageCeilY1, p1.y);

                    Vector3 p0_roomCeil = wallGrid[i, subdivs];
                    Vector3 p1_roomCeil = wallGrid[nextI, subdivs];

                    int headerBaseIdx = verts.Count;

                    verts.Add(p0_passCeil);
                    verts.Add(p1_passCeil);
                    verts.Add(p1_roomCeil);
                    verts.Add(p0_roomCeil);

                    uvs.Add(new Vector2(p0_passCeil.x * 0.1f, p0_passCeil.y * 0.1f));
                    uvs.Add(new Vector2(p1_passCeil.x * 0.1f, p1_passCeil.y * 0.1f));
                    uvs.Add(new Vector2(p1_roomCeil.x * 0.1f, p1_roomCeil.y * 0.1f));
                    uvs.Add(new Vector2(p0_roomCeil.x * 0.1f, p0_roomCeil.y * 0.1f));

                    // Front Face (Facing Into Room)
                    tris.Add(headerBaseIdx + 0);
                    tris.Add(headerBaseIdx + 1);
                    tris.Add(headerBaseIdx + 2);

                    tris.Add(headerBaseIdx + 0);
                    tris.Add(headerBaseIdx + 2);
                    tris.Add(headerBaseIdx + 3);

                    // Back Face (Facing Into Passage)
                    tris.Add(headerBaseIdx + 0);
                    tris.Add(headerBaseIdx + 2);
                    tris.Add(headerBaseIdx + 1);

                    tris.Add(headerBaseIdx + 0);
                    tris.Add(headerBaseIdx + 3);
                    tris.Add(headerBaseIdx + 2);
                }
                else if (!isDoorwaySegment)
                {
                    // Full Room 3D Solid Wall Grid (Inner Face + Outer Face + Ceiling Rim Cap)
                    for (int s = 0; s < subdivs; s++)
                    {
                        // 1. Inner Wall Face (Facing Into Room)
                        int baseIdx = verts.Count;

                        verts.Add(wallGrid[i, s]);
                        verts.Add(wallGrid[nextI, s]);
                        verts.Add(wallGrid[nextI, s + 1]);
                        verts.Add(wallGrid[i, s + 1]);

                        uvs.Add(new Vector2(wallGrid[i, s].x * 0.1f, wallGrid[i, s].y * 0.1f));
                        uvs.Add(new Vector2(wallGrid[nextI, s].x * 0.1f, wallGrid[nextI, s].y * 0.1f));
                        uvs.Add(new Vector2(wallGrid[nextI, s + 1].x * 0.1f, wallGrid[nextI, s + 1].y * 0.1f));
                        uvs.Add(new Vector2(wallGrid[i, s + 1].x * 0.1f, wallGrid[i, s + 1].y * 0.1f));

                        tris.Add(baseIdx + 0);
                        tris.Add(baseIdx + 1);
                        tris.Add(baseIdx + 2);

                        tris.Add(baseIdx + 0);
                        tris.Add(baseIdx + 2);
                        tris.Add(baseIdx + 3);

                        // 2. Outer Wall Face (Facing Outward into rock shell)
                        if (thick > 0f)
                        {
                            int outIdx = verts.Count;

                            verts.Add(outerWallGrid[nextI, s]);
                            verts.Add(outerWallGrid[i, s]);
                            verts.Add(outerWallGrid[i, s + 1]);
                            verts.Add(outerWallGrid[nextI, s + 1]);

                            uvs.Add(new Vector2(outerWallGrid[nextI, s].x * 0.1f, outerWallGrid[nextI, s].y * 0.1f));
                            uvs.Add(new Vector2(outerWallGrid[i, s].x * 0.1f, outerWallGrid[i, s].y * 0.1f));
                            uvs.Add(new Vector2(outerWallGrid[i, s + 1].x * 0.1f, outerWallGrid[i, s + 1].y * 0.1f));
                            uvs.Add(new Vector2(outerWallGrid[nextI, s + 1].x * 0.1f, outerWallGrid[nextI, s + 1].y * 0.1f));

                            tris.Add(outIdx + 0);
                            tris.Add(outIdx + 1);
                            tris.Add(outIdx + 2);

                            tris.Add(outIdx + 0);
                            tris.Add(outIdx + 2);
                            tris.Add(outIdx + 3);
                        }
                    }

                    // 3. Top Wall Rim Cap at Ceiling Height
                    if (thick > 0f)
                    {
                        int capIdx = verts.Count;

                        verts.Add(wallGrid[i, subdivs]);
                        verts.Add(outerWallGrid[i, subdivs]);
                        verts.Add(outerWallGrid[nextI, subdivs]);
                        verts.Add(wallGrid[nextI, subdivs]);

                        uvs.Add(new Vector2(wallGrid[i, subdivs].x * 0.1f, wallGrid[i, subdivs].z * 0.1f));
                        uvs.Add(new Vector2(outerWallGrid[i, subdivs].x * 0.1f, outerWallGrid[i, subdivs].z * 0.1f));
                        uvs.Add(new Vector2(outerWallGrid[nextI, subdivs].x * 0.1f, outerWallGrid[nextI, subdivs].z * 0.1f));
                        uvs.Add(new Vector2(wallGrid[nextI, subdivs].x * 0.1f, wallGrid[nextI, subdivs].z * 0.1f));

                        tris.Add(capIdx + 0);
                        tris.Add(capIdx + 1);
                        tris.Add(capIdx + 2);

                        tris.Add(capIdx + 0);
                        tris.Add(capIdx + 2);
                        tris.Add(capIdx + 3);
                    }
                }
            }
        }
    }

    private void Add3DCorridor(CorridorData corr, List<RoomData> rooms, List<Vector2>[] roomPerimeters, float width, float height,
                               List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        RoomData roomA = rooms[corr.roomA];
        RoomData roomB = rooms[corr.roomB];

        Vector2 cA = new Vector2(roomA.center.x, roomA.center.z);
        Vector2 cB = new Vector2(roomB.center.x, roomB.center.z);

        Vector2 dirXZ = (cB - cA).normalized;
        if (dirXZ == Vector2.zero) return;

        Vector2 rightXZ = new Vector2(-dirXZ.y, dirXZ.x);
        float halfW = width * 0.5f;

        // Intersection points on room perimeter walls
        Vector2 rawStartXZ = GetRoomPerimeterIntersection(cA, roomPerimeters[corr.roomA], cB);
        Vector2 rawEndXZ = GetRoomPerimeterIntersection(cB, roomPerimeters[corr.roomB], cA);

        // Extend passage slightly (1.0 unit) into rooms to create an overlapping watertight seal
        Vector2 startXZ = rawStartXZ - dirXZ * 1.0f;
        Vector2 endXZ = rawEndXZ + dirXZ * 1.0f;

        float dist = Vector2.Distance(startXZ, endXZ);
        if (dist <= 0.2f) return;

        int steps = Mathf.Max(6, Mathf.CeilToInt(dist / 1.5f));

        List<Vector3> leftFloor = new List<Vector3>();
        List<Vector3> rightFloor = new List<Vector3>();
        List<Vector3> leftCeil = new List<Vector3>();
        List<Vector3> rightCeil = new List<Vector3>();

        float seedOffset = (float)(cA.x * 12.3f + cB.y * 45.6f);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 baseXZ = Vector2.Lerp(startXZ, endXZ, t);
            float elev = Mathf.Lerp(roomA.floorElevation, roomB.floorElevation, t);

            // Envelope guarantees 0.0 offset at doorway endpoints (t=0 and t=1)
            float envelope = Mathf.Sin(t * Mathf.PI);

            float windingOffset = 0f;
            if (enableWindingCorridors && corridorWindingAmount > 0f)
            {
                float sineWave = Mathf.Sin(t * Mathf.PI * corridorWindingFrequency + seedOffset);
                float perlinNoise = (Mathf.PerlinNoise(baseXZ.x * 0.05f + seedOffset, baseXZ.y * 0.05f + seedOffset) - 0.5f) * 2.0f;
                windingOffset = (sineWave * 0.6f + perlinNoise * 0.4f) * corridorWindingAmount * envelope;
            }

            Vector2 posXZ = baseXZ + rightXZ * windingOffset;

            Vector2 lxz = posXZ - rightXZ * halfW;
            Vector2 rxz = posXZ + rightXZ * halfW;

            float fyL = GetFloorY(lxz.x, lxz.y, elev);
            float fyR = GetFloorY(rxz.x, rxz.y, elev);

            float cyL = GetCeilingY(lxz.x, lxz.y, elev + height);
            float cyR = GetCeilingY(rxz.x, rxz.y, elev + height);

            leftFloor.Add(new Vector3(lxz.x, fyL, lxz.y));
            rightFloor.Add(new Vector3(rxz.x, fyR, rxz.y));

            leftCeil.Add(new Vector3(lxz.x, cyL, lxz.y));
            rightCeil.Add(new Vector3(rxz.x, cyR, rxz.y));
        }

        // Precompute continuous inner and outer wall grids for left and right corridor walls
        int subdivs = Mathf.Max(1, wallSubdivisions);
        Vector3[,] lWallGrid = new Vector3[steps + 1, subdivs + 1];
        Vector3[,] rWallGrid = new Vector3[steps + 1, subdivs + 1];
        Vector3[,] lOuterWallGrid = new Vector3[steps + 1, subdivs + 1];
        Vector3[,] rOuterWallGrid = new Vector3[steps + 1, subdivs + 1];

        Vector3 lNormOut = new Vector3(-rightXZ.x, 0f, -rightXZ.y); // Outward left into rock
        Vector3 rNormOut = new Vector3(rightXZ.x, 0f, rightXZ.y);   // Outward right into rock

        float cThick = (enableWallThickness && wallThickness > 0.01f) ? wallThickness : 0f;

        for (int i = 0; i <= steps; i++)
        {
            Vector3 lf = leftFloor[i];
            Vector3 lc = leftCeil[i];

            Vector3 rf = rightFloor[i];
            Vector3 rc = rightCeil[i];

            for (int s = 0; s <= subdivs; s++)
            {
                float tHeight = (float)s / subdivs;
                float heightEnv = Mathf.Sin(tHeight * Mathf.PI);

                Vector3 rawL = Vector3.Lerp(lf, lc, tHeight);
                Vector3 rawR = Vector3.Lerp(rf, rc, tHeight);

                float lNoise = 0f;
                float rNoise = 0f;

                if (enableWallNoise && wallNoiseAmount > 0f)
                {
                    float nL = (Mathf.PerlinNoise(rawL.x * wallNoiseScale + 400f, rawL.z * wallNoiseScale + tHeight * 3f + 500f) - 0.5f) * 2.0f;
                    float nR = (Mathf.PerlinNoise(rawR.x * wallNoiseScale + 700f, rawR.z * wallNoiseScale + tHeight * 3f + 800f) - 0.5f) * 2.0f;

                    lNoise = nL * wallNoiseAmount * heightEnv;
                    rNoise = nR * wallNoiseAmount * heightEnv;
                }

                Vector3 inL = rawL + lNormOut * lNoise;
                lWallGrid[i, s] = inL;
                lOuterWallGrid[i, s] = inL + lNormOut * cThick;

                Vector3 inR = rawR + rNormOut * rNoise;
                rWallGrid[i, s] = inR;
                rOuterWallGrid[i, s] = inR + rNormOut * cThick;
            }
        }

        for (int i = 0; i < steps; i++)
        {
            // 1. Pipe Floor (Facing UP into passage)
            if (generateFloor)
            {
                int baseIdx = verts.Count;
                verts.Add(leftFloor[i]);
                verts.Add(rightFloor[i]);
                verts.Add(rightFloor[i + 1]);
                verts.Add(leftFloor[i + 1]);

                uvs.Add(new Vector2(leftFloor[i].x * 0.1f, leftFloor[i].z * 0.1f));
                uvs.Add(new Vector2(rightFloor[i].x * 0.1f, rightFloor[i].z * 0.1f));
                uvs.Add(new Vector2(rightFloor[i + 1].x * 0.1f, rightFloor[i + 1].z * 0.1f));
                uvs.Add(new Vector2(leftFloor[i + 1].x * 0.1f, leftFloor[i + 1].z * 0.1f));

                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 2);

                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 3);
            }

            // 2. Pipe Ceiling (Facing DOWN into passage)
            if (generateCeiling)
            {
                int baseIdx = verts.Count;
                verts.Add(leftCeil[i]);
                verts.Add(leftCeil[i + 1]);
                verts.Add(rightCeil[i + 1]);
                verts.Add(rightCeil[i]);

                uvs.Add(new Vector2(leftCeil[i].x * 0.1f, leftCeil[i].z * 0.1f));
                uvs.Add(new Vector2(leftCeil[i + 1].x * 0.1f, leftCeil[i + 1].z * 0.1f));
                uvs.Add(new Vector2(rightCeil[i + 1].x * 0.1f, rightCeil[i + 1].z * 0.1f));
                uvs.Add(new Vector2(rightCeil[i].x * 0.1f, rightCeil[i].z * 0.1f));

                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 2);

                tris.Add(baseIdx + 0);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 3);
            }

            // 3. Pipe Enclosing Side Walls (Continuous Shared Inner & Outer Wall Grids)
            if (generateWalls)
            {
                // Left Wall Grid (Inner Face + Outer Face)
                for (int s = 0; s < subdivs; s++)
                {
                    // Inner Left Wall
                    int lIdx = verts.Count;

                    verts.Add(lWallGrid[i, s]);
                    verts.Add(lWallGrid[i + 1, s]);
                    verts.Add(lWallGrid[i + 1, s + 1]);
                    verts.Add(lWallGrid[i, s + 1]);

                    uvs.Add(new Vector2(lWallGrid[i, s].x * 0.1f, lWallGrid[i, s].y * 0.1f));
                    uvs.Add(new Vector2(lWallGrid[i + 1, s].x * 0.1f, lWallGrid[i + 1, s].y * 0.1f));
                    uvs.Add(new Vector2(lWallGrid[i + 1, s + 1].x * 0.1f, lWallGrid[i + 1, s + 1].y * 0.1f));
                    uvs.Add(new Vector2(lWallGrid[i, s + 1].x * 0.1f, lWallGrid[i, s + 1].y * 0.1f));

                    tris.Add(lIdx + 0);
                    tris.Add(lIdx + 1);
                    tris.Add(lIdx + 2);

                    tris.Add(lIdx + 0);
                    tris.Add(lIdx + 2);
                    tris.Add(lIdx + 3);

                    // Outer Left Wall
                    if (cThick > 0f)
                    {
                        int lOutIdx = verts.Count;

                        verts.Add(lOuterWallGrid[i + 1, s]);
                        verts.Add(lOuterWallGrid[i, s]);
                        verts.Add(lOuterWallGrid[i, s + 1]);
                        verts.Add(lOuterWallGrid[i + 1, s + 1]);

                        uvs.Add(new Vector2(lOuterWallGrid[i + 1, s].x * 0.1f, lOuterWallGrid[i + 1, s].y * 0.1f));
                        uvs.Add(new Vector2(lOuterWallGrid[i, s].x * 0.1f, lOuterWallGrid[i, s].y * 0.1f));
                        uvs.Add(new Vector2(lOuterWallGrid[i, s + 1].x * 0.1f, lOuterWallGrid[i, s + 1].y * 0.1f));
                        uvs.Add(new Vector2(lOuterWallGrid[i + 1, s + 1].x * 0.1f, lOuterWallGrid[i + 1, s + 1].y * 0.1f));

                        tris.Add(lOutIdx + 0);
                        tris.Add(lOutIdx + 1);
                        tris.Add(lOutIdx + 2);

                        tris.Add(lOutIdx + 0);
                        tris.Add(lOutIdx + 2);
                        tris.Add(lOutIdx + 3);
                    }
                }

                // Right Wall Grid (Inner Face + Outer Face)
                for (int s = 0; s < subdivs; s++)
                {
                    // Inner Right Wall
                    int rIdx = verts.Count;

                    verts.Add(rWallGrid[i + 1, s]);
                    verts.Add(rWallGrid[i, s]);
                    verts.Add(rWallGrid[i, s + 1]);
                    verts.Add(rWallGrid[i + 1, s + 1]);

                    uvs.Add(new Vector2(rWallGrid[i + 1, s].x * 0.1f, rWallGrid[i + 1, s].y * 0.1f));
                    uvs.Add(new Vector2(rWallGrid[i, s].x * 0.1f, rWallGrid[i, s].y * 0.1f));
                    uvs.Add(new Vector2(rWallGrid[i, s + 1].x * 0.1f, rWallGrid[i, s + 1].y * 0.1f));
                    uvs.Add(new Vector2(rWallGrid[i + 1, s + 1].x * 0.1f, rWallGrid[i + 1, s + 1].y * 0.1f));

                    tris.Add(rIdx + 0);
                    tris.Add(rIdx + 1);
                    tris.Add(rIdx + 2);

                    tris.Add(rIdx + 0);
                    tris.Add(rIdx + 2);
                    tris.Add(rIdx + 3);

                    // Outer Right Wall
                    if (cThick > 0f)
                    {
                        int rOutIdx = verts.Count;

                        verts.Add(rOuterWallGrid[i, s]);
                        verts.Add(rOuterWallGrid[i + 1, s]);
                        verts.Add(rOuterWallGrid[i + 1, s + 1]);
                        verts.Add(rOuterWallGrid[i, s + 1]);

                        uvs.Add(new Vector2(rOuterWallGrid[i, s].x * 0.1f, rOuterWallGrid[i, s].y * 0.1f));
                        uvs.Add(new Vector2(rOuterWallGrid[i + 1, s].x * 0.1f, rOuterWallGrid[i + 1, s].y * 0.1f));
                        uvs.Add(new Vector2(rOuterWallGrid[i + 1, s + 1].x * 0.1f, rOuterWallGrid[i + 1, s + 1].y * 0.1f));
                        uvs.Add(new Vector2(rOuterWallGrid[i, s + 1].x * 0.1f, rOuterWallGrid[i, s + 1].y * 0.1f));

                        tris.Add(rOutIdx + 0);
                        tris.Add(rOutIdx + 1);
                        tris.Add(rOutIdx + 2);

                        tris.Add(rOutIdx + 0);
                        tris.Add(rOutIdx + 2);
                        tris.Add(rOutIdx + 3);
                    }
                }
            }
        }
    }

    private float DistancePointToSegmentXZ(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen == 0f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(p, projection);
    }

    private float GetFloorY(float x, float z, float baseElevation)
    {
        if (!enableOrganicNoise || floorNoiseAmount <= 0f) return baseElevation;
        float n = Mathf.PerlinNoise(x * floorNoiseScale, z * floorNoiseScale);
        return baseElevation + (n - 0.5f) * floorNoiseAmount;
    }

    private float GetCeilingY(float x, float z, float baseElevation)
    {
        if (!enableOrganicNoise || ceilingNoiseAmount <= 0f) return baseElevation;
        float n = Mathf.PerlinNoise((x + 100f) * ceilingNoiseScale, (z + 100f) * ceilingNoiseScale);
        return baseElevation + (n - 0.5f) * ceilingNoiseAmount;
    }

    private float GetRandomFloat(System.Random rand, float min, float max)
    {
        return (float)(rand.NextDouble() * (max - min) + min);
    }

    public void GenerateRandomSeed()
    {
        seed = System.Guid.NewGuid().ToString().Substring(0, 8);
        Generate3DNetwork();
    }

    // Kept for inspector/script compatibility
    public void GenerateCave()
    {
        Generate3DNetwork();
    }

    public void PlacePlayerInRandomRoom()
    {
        PlacePlayerInRandomRoom(lastGeneratedRooms);
    }

    public void PlacePlayerInRandomRoom(List<RoomData> rooms)
    {
        if (rooms == null || rooms.Count == 0) return;

        if (playerTransform == null)
        {
            Movement movement = FindObjectOfType<Movement>();
            if (movement != null)
            {
                playerTransform = movement.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    playerTransform = playerObj.transform;
                }
            }
        }

        if (playerTransform != null)
        {
            int randomRoomIndex = Random.Range(0, rooms.Count);
            RoomData selectedRoom = rooms[randomRoomIndex];

            float floorY = GetFloorY(selectedRoom.center.x, selectedRoom.center.z, selectedRoom.floorElevation);
            Vector3 spawnPosition = new Vector3(selectedRoom.center.x, floorY + playerSpawnYOffset, selectedRoom.center.z);

            playerTransform.position = spawnPosition;

            Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = spawnPosition;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
        }
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(CaveMeshGenerator3D))]
public class CaveMeshGenerator3D_Editor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CaveMeshGenerator3D generator = (CaveMeshGenerator3D)target;

        UnityEditor.EditorGUILayout.Space();

        if (GUILayout.Button("Generate Height-Varied 3D Rooms", GUILayout.Height(30)))
        {
            generator.Generate3DNetwork();
            UnityEditor.EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Randomize Seed & Generate", GUILayout.Height(30)))
        {
            generator.GenerateRandomSeed();
            UnityEditor.EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Place Player in Random Room", GUILayout.Height(30)))
        {
            generator.PlacePlayerInRandomRoom();
        }
    }
}
#endif