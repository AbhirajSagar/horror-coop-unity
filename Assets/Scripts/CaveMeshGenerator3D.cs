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

    [Header("Room Polygon Settings")]
    [Range(3, 32)]
    public int roomPointCount = 12;
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

        // Step 1.5: Build Corridor Connections (allowing multiple paths between rooms)
        List<CorridorData> corridors = new List<CorridorData>();
        if (generateCorridors)
        {
            corridors = GenerateCorridorNetwork(rooms, pseudoRandom);
        }

        // Step 2: Build 3D Room Polygons with doorway openings where corridors attach
        for (int r = 0; r < rooms.Count; r++)
        {
            Add3DRoomPolygon(r, rooms, corridors, roomPointCount, minRoomRadius, maxRoomRadius,
                             pseudoRandom, allVertices, allTriangles, allUVs);
        }

        // Step 2.5: Build 3D Corridor Tunnels between rooms
        if (generateCorridors)
        {
            foreach (CorridorData corr in corridors)
            {
                Add3DCorridor(rooms[corr.roomA], rooms[corr.roomB], corridorWidth, corridorHeight,
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

    private List<CorridorData> GenerateCorridorNetwork(List<RoomData> rooms, System.Random rand)
    {
        List<CorridorData> corridors = new List<CorridorData>();
        HashSet<(int, int)> connectedPairs = new HashSet<(int, int)>();

        void AddCorridor(int a, int b)
        {
            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            if (min == max) return;
            if (connectedPairs.Contains((min, max))) return;

            connectedPairs.Add((min, max));
            corridors.Add(new CorridorData { roomA = min, roomB = max });
        }

        // 1. Primary connections: Each room connects to its nearest neighbor (guarantees connectivity)
        for (int i = 0; i < rooms.Count; i++)
        {
            int closest = -1;
            float minDist = float.MaxValue;

            for (int j = 0; j < rooms.Count; j++)
            {
                if (i == j) continue;
                float dist = Vector3.Distance(rooms[i].center, rooms[j].center);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = j;
                }
            }

            if (closest != -1)
            {
                AddCorridor(i, closest);
            }
        }

        // 2. Extra connections: Connect each room to 2nd/3rd closest neighbors to create multiple paths & loops
        for (int i = 0; i < rooms.Count; i++)
        {
            List<int> neighbors = new List<int>();
            for (int j = 0; j < rooms.Count; j++)
            {
                if (i != j) neighbors.Add(j);
            }

            neighbors.Sort((a, b) =>
            {
                float distA = Vector3.Distance(rooms[i].center, rooms[a].center);
                float distB = Vector3.Distance(rooms[i].center, rooms[b].center);
                return distA.CompareTo(distB);
            });

            int connectionsCount = Mathf.Min(maxConnectionsPerRoom, neighbors.Count);
            for (int n = 0; n < connectionsCount; n++)
            {
                int targetRoom = neighbors[n];
                if (n == 0 || (float)rand.NextDouble() <= extraPathChance)
                {
                    AddCorridor(i, targetRoom);
                }
            }
        }

        return corridors;
    }

    private void Add3DRoomPolygon(int roomIndex, List<RoomData> rooms, List<CorridorData> corridors,
                                  int points, float minR, float maxR,
                                  System.Random rand,
                                  List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        RoomData room = rooms[roomIndex];
        Vector3 center = room.center;
        Vector2 centerXZ = new Vector2(center.x, center.z);
        float baseFloorY = room.floorElevation;
        float height = room.wallHeight;

        List<Vector2> perimeterXZ = new List<Vector2>();

        // Find all doorway points for corridors connected to this room
        List<Vector2> doorwayCenters = new List<Vector2>();
        if (generateCorridors && corridors != null)
        {
            foreach (CorridorData corr in corridors)
            {
                if (corr.roomA == roomIndex || corr.roomB == roomIndex)
                {
                    int otherIdx = (corr.roomA == roomIndex) ? corr.roomB : corr.roomA;
                    Vector2 otherXZ = new Vector2(rooms[otherIdx].center.x, rooms[otherIdx].center.z);
                    Vector2 dirXZ = (otherXZ - centerXZ).normalized;
                    Vector2 doorwayPoint = centerXZ + dirXZ * (room.maxRadius * 0.85f);
                    doorwayCenters.Add(doorwayPoint);
                }
            }
        }

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

        // Build 3D Floor positions with organic terrain noise
        List<Vector3> floorVerts = new List<Vector3>();
        List<Vector3> ceilVerts = new List<Vector3>();

        Vector3 floorCenter = new Vector3(center.x, GetFloorY(center.x, center.z, baseFloorY), center.z);
        Vector3 ceilCenter = new Vector3(center.x, GetCeilingY(center.x, center.z, baseFloorY + height), center.z);

        for (int i = 0; i < perimeterXZ.Count; i++)
        {
            float x = perimeterXZ[i].x;
            float z = perimeterXZ[i].y;

            float fy = GetFloorY(x, z, baseFloorY);
            float cy = GetCeilingY(x, z, baseFloorY + height);

            floorVerts.Add(new Vector3(x, fy, z));
            ceilVerts.Add(new Vector3(x, cy, z));
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

        // 3. ROOM PERIMETER WALLS (Only open the exact doorway segment!)
        if (generateWalls)
        {
            for (int i = 0; i < perimeterXZ.Count; i++)
            {
                Vector2 p0 = perimeterXZ[i];
                Vector2 p1 = perimeterXZ[(i + 1) % perimeterXZ.Count];
                Vector2 wallMid = (p0 + p1) * 0.5f;

                // Check if this specific sub-segment is inside an open doorway
                bool isDoorwaySegment = false;
                foreach (Vector2 dwPoint in doorwayCenters)
                {
                    if (Vector2.Distance(wallMid, dwPoint) < corridorWidth * 0.52f)
                    {
                        isDoorwaySegment = true;
                        break;
                    }
                }

                if (isDoorwaySegment) continue; // Skip ONLY the doorway opening segment!

                Vector3 p0_floor = floorVerts[i];
                Vector3 p1_floor = floorVerts[(i + 1) % floorVerts.Count];

                Vector3 p0_ceil = ceilVerts[i];
                Vector3 p1_ceil = ceilVerts[(i + 1) % ceilVerts.Count];

                int wallBaseIdx = verts.Count;

                verts.Add(p0_floor);
                verts.Add(p1_floor);
                verts.Add(p1_ceil);
                verts.Add(p0_ceil);

                uvs.Add(new Vector2(p0_floor.x * 0.1f, p0_floor.y * 0.1f));
                uvs.Add(new Vector2(p1_floor.x * 0.1f, p1_floor.y * 0.1f));
                uvs.Add(new Vector2(p1_ceil.x * 0.1f, p1_ceil.y * 0.1f));
                uvs.Add(new Vector2(p0_ceil.x * 0.1f, p0_ceil.y * 0.1f));

                tris.Add(wallBaseIdx + 0);
                tris.Add(wallBaseIdx + 1);
                tris.Add(wallBaseIdx + 2);

                tris.Add(wallBaseIdx + 0);
                tris.Add(wallBaseIdx + 2);
                tris.Add(wallBaseIdx + 3);
            }
        }
    }

    private void Add3DCorridor(RoomData roomA, RoomData roomB, float width, float height,
                               List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        Vector2 cA = new Vector2(roomA.center.x, roomA.center.z);
        Vector2 cB = new Vector2(roomB.center.x, roomB.center.z);

        Vector2 dirXZ = (cB - cA).normalized;
        if (dirXZ == Vector2.zero) return;

        Vector2 rightXZ = new Vector2(-dirXZ.y, dirXZ.x);
        float halfW = width * 0.5f;

        // Start and End at the room boundaries so the passage doesn't extrude inside rooms
        Vector2 startXZ = cA + dirXZ * (roomA.maxRadius * 0.85f);
        Vector2 endXZ = cB - dirXZ * (roomB.maxRadius * 0.85f);

        float dist = Vector2.Distance(startXZ, endXZ);
        if (dist <= 0.5f) return;

        int steps = Mathf.Max(2, Mathf.CeilToInt(dist / 2.0f));

        List<Vector3> leftFloor = new List<Vector3>();
        List<Vector3> rightFloor = new List<Vector3>();
        List<Vector3> leftCeil = new List<Vector3>();
        List<Vector3> rightCeil = new List<Vector3>();

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 posXZ = Vector2.Lerp(startXZ, endXZ, t);
            float elev = Mathf.Lerp(roomA.floorElevation, roomB.floorElevation, t);

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

            // 3. Pipe Enclosing Side Walls (Left & Right Walls along passage)
            if (generateWalls)
            {
                // Left Side Wall of Pipe (Facing INWARD into passage)
                int lBaseIdx = verts.Count;
                verts.Add(leftFloor[i]);
                verts.Add(leftFloor[i + 1]);
                verts.Add(leftCeil[i + 1]);
                verts.Add(leftCeil[i]);

                uvs.Add(new Vector2(leftFloor[i].x * 0.1f, leftFloor[i].y * 0.1f));
                uvs.Add(new Vector2(leftFloor[i + 1].x * 0.1f, leftFloor[i + 1].y * 0.1f));
                uvs.Add(new Vector2(leftCeil[i + 1].x * 0.1f, leftCeil[i + 1].y * 0.1f));
                uvs.Add(new Vector2(leftCeil[i].x * 0.1f, leftCeil[i].y * 0.1f));

                tris.Add(lBaseIdx + 0);
                tris.Add(lBaseIdx + 1);
                tris.Add(lBaseIdx + 2);

                tris.Add(lBaseIdx + 0);
                tris.Add(lBaseIdx + 2);
                tris.Add(lBaseIdx + 3);

                // Right Side Wall of Pipe (Facing INWARD into passage)
                int rBaseIdx = verts.Count;
                verts.Add(rightFloor[i + 1]);
                verts.Add(rightFloor[i]);
                verts.Add(rightCeil[i]);
                verts.Add(rightCeil[i + 1]);

                uvs.Add(new Vector2(rightFloor[i + 1].x * 0.1f, rightFloor[i + 1].y * 0.1f));
                uvs.Add(new Vector2(rightFloor[i].x * 0.1f, rightFloor[i].y * 0.1f));
                uvs.Add(new Vector2(rightCeil[i].x * 0.1f, rightCeil[i].y * 0.1f));
                uvs.Add(new Vector2(rightCeil[i + 1].x * 0.1f, rightCeil[i + 1].y * 0.1f));

                tris.Add(rBaseIdx + 0);
                tris.Add(rBaseIdx + 1);
                tris.Add(rBaseIdx + 2);

                tris.Add(rBaseIdx + 0);
                tris.Add(rBaseIdx + 2);
                tris.Add(rBaseIdx + 3);
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