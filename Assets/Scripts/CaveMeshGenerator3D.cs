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

    private struct RoomData
    {
        public Vector3 center;
        public float floorElevation;
        public float wallHeight;
        public float avgRadius;
        public float maxRadius;
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

        // Step 2: Build 3D Room Polygons with height variations & organic terrain noise
        for (int r = 0; r < rooms.Count; r++)
        {
            Add3DRoomPolygon(rooms[r], roomPointCount, minRoomRadius, maxRoomRadius,
                             pseudoRandom, allVertices, allTriangles, allUVs);
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
        mesh.name = "Height-Varied 3D Rooms";
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

    private void Add3DRoomPolygon(RoomData room, int points, float minR, float maxR,
                                  System.Random rand,
                                  List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        Vector3 center = room.center;
        float baseFloorY = room.floorElevation;
        float height = room.wallHeight;

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

        // 3. ROOM PERIMETER WALLS (Fully enclosed walls around each room)
        if (generateWalls)
        {
            for (int i = 0; i < perimeterXZ.Count; i++)
            {
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

                // Quad facing INWARD into room interior
                tris.Add(wallBaseIdx + 0);
                tris.Add(wallBaseIdx + 1);
                tris.Add(wallBaseIdx + 2);

                tris.Add(wallBaseIdx + 0);
                tris.Add(wallBaseIdx + 2);
                tris.Add(wallBaseIdx + 3);
            }
        }
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