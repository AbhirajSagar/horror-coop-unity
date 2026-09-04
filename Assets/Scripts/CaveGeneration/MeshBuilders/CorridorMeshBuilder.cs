using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Geometry;
using CaveGeneration.Noise;

namespace CaveGeneration.MeshBuilders
{
    /// <summary>
    /// Constructs 3D tunnel meshes for corridors connecting cave rooms, including winding curves, floors, ceilings, and walls.
    /// </summary>
    public class CorridorMeshBuilder
    {
        public void BuildCorridor(
            CorridorData corr,
            RoomData roomA,
            RoomData roomB,
            List<Vector2> perimeterA,
            List<Vector2> perimeterB,
            CorridorMeshSettings meshSettings,
            StructureToggleSettings structureSettings,
            TerrainHeightSampler heightSampler,
            CaveMeshData meshData)
        {
            Vector2 cA = new Vector2(roomA.center.x, roomA.center.z);
            Vector2 cB = new Vector2(roomB.center.x, roomB.center.z);

            Vector2 dirXZ = (cB - cA).normalized;
            if (dirXZ == Vector2.zero) return;

            Vector2 rightXZ = new Vector2(-dirXZ.y, dirXZ.x);
            float halfW = meshSettings.corridorWidth * 0.5f;

            // Exact intersection points on room perimeter walls
            Vector2 startXZ = CaveGeometryUtils.GetRoomPerimeterIntersection(cA, perimeterA, cB);
            Vector2 endXZ = CaveGeometryUtils.GetRoomPerimeterIntersection(cB, perimeterB, cA);

            float dist = Vector2.Distance(startXZ, endXZ);
            if (dist <= 0.2f) return;

            int steps = Mathf.Max(6, Mathf.CeilToInt(dist / 1.5f));

            List<Vector3> leftFloor = new List<Vector3>(steps + 1);
            List<Vector3> rightFloor = new List<Vector3>(steps + 1);
            List<Vector3> leftCeil = new List<Vector3>(steps + 1);
            List<Vector3> rightCeil = new List<Vector3>(steps + 1);

            float seedOffset = (float)(cA.x * 12.3f + cB.y * 45.6f);

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 baseXZ = Vector2.Lerp(startXZ, endXZ, t);
                float elev = Mathf.Lerp(roomA.floorElevation, roomB.floorElevation, t);

                // Envelope guarantees 0.0 offset at doorway endpoints (t=0 and t=1)
                float envelope = Mathf.Sin(t * Mathf.PI);

                float windingOffset = 0f;
                if (meshSettings.enableWindingCorridors && meshSettings.corridorWindingAmount > 0f)
                {
                    float sineWave = Mathf.Sin(t * Mathf.PI * meshSettings.corridorWindingFrequency + seedOffset);
                    float perlinNoise = (Mathf.PerlinNoise(baseXZ.x * 0.05f + seedOffset, baseXZ.y * 0.05f + seedOffset) - 0.5f) * 2.0f;
                    windingOffset = (sineWave * 0.6f + perlinNoise * 0.4f) * meshSettings.corridorWindingAmount * envelope;
                }

                Vector2 posXZ = baseXZ + rightXZ * windingOffset;

                Vector2 lxz = posXZ - rightXZ * halfW;
                Vector2 rxz = posXZ + rightXZ * halfW;

                float fyL = heightSampler.GetFloorY(lxz.x, lxz.y, elev);
                float fyR = heightSampler.GetFloorY(rxz.x, rxz.y, elev);

                float cyL = heightSampler.GetCeilingY(lxz.x, lxz.y, elev + meshSettings.corridorHeight);
                float cyR = heightSampler.GetCeilingY(rxz.x, rxz.y, elev + meshSettings.corridorHeight);

                leftFloor.Add(new Vector3(lxz.x, fyL, lxz.y));
                rightFloor.Add(new Vector3(rxz.x, fyR, rxz.y));

                leftCeil.Add(new Vector3(lxz.x, cyL, lxz.y));
                rightCeil.Add(new Vector3(rxz.x, cyR, rxz.y));
            }

            for (int i = 0; i < steps; i++)
            {
                // 1. Pipe Floor (Facing UP into passage)
                if (structureSettings.generateFloor)
                {
                    int baseIdx = meshData.VertexCount;
                    meshData.AddVertex(leftFloor[i], new Vector2(leftFloor[i].x * 0.1f, leftFloor[i].z * 0.1f));
                    meshData.AddVertex(rightFloor[i], new Vector2(rightFloor[i].x * 0.1f, rightFloor[i].z * 0.1f));
                    meshData.AddVertex(rightFloor[i + 1], new Vector2(rightFloor[i + 1].x * 0.1f, rightFloor[i + 1].z * 0.1f));
                    meshData.AddVertex(leftFloor[i + 1], new Vector2(leftFloor[i + 1].x * 0.1f, leftFloor[i + 1].z * 0.1f));

                    meshData.AddQuadIndices(baseIdx, baseIdx + 1, baseIdx + 2, baseIdx + 3);
                }

                // 2. Pipe Ceiling (Facing DOWN into passage)
                if (structureSettings.generateCeiling)
                {
                    int baseIdx = meshData.VertexCount;
                    meshData.AddVertex(leftCeil[i], new Vector2(leftCeil[i].x * 0.1f, leftCeil[i].z * 0.1f));
                    meshData.AddVertex(leftCeil[i + 1], new Vector2(leftCeil[i + 1].x * 0.1f, leftCeil[i + 1].z * 0.1f));
                    meshData.AddVertex(rightCeil[i + 1], new Vector2(rightCeil[i + 1].x * 0.1f, rightCeil[i + 1].z * 0.1f));
                    meshData.AddVertex(rightCeil[i], new Vector2(rightCeil[i].x * 0.1f, rightCeil[i].z * 0.1f));

                    meshData.AddQuadIndices(baseIdx, baseIdx + 1, baseIdx + 2, baseIdx + 3);
                }

                // 3. Pipe Enclosing Side Walls (Left & Right Walls along passage)
                if (structureSettings.generateWalls)
                {
                    // Left Side Wall of Pipe (Facing INWARD into passage)
                    int lBaseIdx = meshData.VertexCount;
                    meshData.AddVertex(leftFloor[i], new Vector2(leftFloor[i].x * 0.1f, leftFloor[i].y * 0.1f));
                    meshData.AddVertex(leftFloor[i + 1], new Vector2(leftFloor[i + 1].x * 0.1f, leftFloor[i + 1].z * 0.1f));
                    meshData.AddVertex(leftCeil[i + 1], new Vector2(leftCeil[i + 1].x * 0.1f, leftCeil[i + 1].y * 0.1f));
                    meshData.AddVertex(leftCeil[i], new Vector2(leftCeil[i].x * 0.1f, leftCeil[i].y * 0.1f));

                    meshData.AddQuadIndices(lBaseIdx, lBaseIdx + 1, lBaseIdx + 2, lBaseIdx + 3);

                    // Right Side Wall of Pipe (Facing INWARD into passage)
                    int rBaseIdx = meshData.VertexCount;
                    meshData.AddVertex(rightFloor[i + 1], new Vector2(rightFloor[i + 1].x * 0.1f, rightFloor[i + 1].z * 0.1f));
                    meshData.AddVertex(rightFloor[i], new Vector2(rightFloor[i].x * 0.1f, rightFloor[i].z * 0.1f));
                    meshData.AddVertex(rightCeil[i], new Vector2(rightCeil[i].x * 0.1f, rightCeil[i].y * 0.1f));
                    meshData.AddVertex(rightCeil[i + 1], new Vector2(rightCeil[i + 1].x * 0.1f, rightCeil[i + 1].y * 0.1f));

                    meshData.AddQuadIndices(rBaseIdx, rBaseIdx + 1, rBaseIdx + 2, rBaseIdx + 3);
                }
            }
        }
    }
}
