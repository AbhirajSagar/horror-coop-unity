using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Geometry;
using CaveGeneration.Noise;

namespace CaveGeneration.MeshBuilders
{
    /// <summary>
    /// Constructs 3D meshes for room floors, ceilings, perimeter walls, doorway header walls, and arch soffits.
    /// </summary>
    public class RoomMeshBuilder
    {
        public void BuildRoomMesh(
            int roomIndex,
            RoomData room,
            List<Vector2> basePerimeter,
            List<DoorwayData> doorways,
            float corridorWidth,
            float corridorHeight,
            StructureToggleSettings structureSettings,
            TerrainHeightSampler heightSampler,
            CaveMeshData meshData,
            out List<Vector3> floorVerts,
            out List<Vector3> ceilVerts,
            out Vector3 floorCenter,
            out Vector3 ceilCenter)
        {
            Vector3 center = room.center;
            float baseFloorY = room.floorElevation;
            float height = room.wallHeight;

            List<Vector2> perimeterXZ = CaveGeometryUtils.BuildDoorwayAdjustedPerimeter(basePerimeter, doorways, center, corridorWidth);

            floorVerts = new List<Vector3>(perimeterXZ.Count);
            ceilVerts = new List<Vector3>(perimeterXZ.Count);

            floorCenter = new Vector3(center.x, heightSampler.GetFloorY(center.x, center.z, baseFloorY), center.z);
            ceilCenter = new Vector3(center.x, heightSampler.GetCeilingY(center.x, center.z, baseFloorY + height), center.z);

            for (int i = 0; i < perimeterXZ.Count; i++)
            {
                float x = perimeterXZ[i].x;
                float z = perimeterXZ[i].y;

                float fy = heightSampler.GetFloorY(x, z, baseFloorY);
                float cy = heightSampler.GetCeilingY(x, z, baseFloorY + height);

                floorVerts.Add(new Vector3(x, fy, z));
                ceilVerts.Add(new Vector3(x, cy, z));
            }

            // 1. ROOM FLOOR MESH (Facing UP +Y into room)
            if (structureSettings.generateFloor)
            {
                int floorBaseIdx = meshData.VertexCount;
                meshData.AddVertex(floorCenter, new Vector2(floorCenter.x * 0.1f, floorCenter.z * 0.1f));

                for (int i = 0; i < floorVerts.Count; i++)
                {
                    meshData.AddVertex(floorVerts[i], new Vector2(floorVerts[i].x * 0.1f, floorVerts[i].z * 0.1f));
                }

                for (int i = 0; i < floorVerts.Count; i++)
                {
                    int currentIdx = floorBaseIdx + 1 + i;
                    int nextIdx = floorBaseIdx + 1 + ((i + 1) % floorVerts.Count);

                    meshData.AddTriangleIndices(floorBaseIdx, nextIdx, currentIdx);
                }
            }

            // 2. ROOM CEILING MESH (Facing DOWN -Y into room)
            if (structureSettings.generateCeiling)
            {
                int ceilingBaseIdx = meshData.VertexCount;
                meshData.AddVertex(ceilCenter, new Vector2(ceilCenter.x * 0.1f, ceilCenter.z * 0.1f));

                for (int i = 0; i < ceilVerts.Count; i++)
                {
                    meshData.AddVertex(ceilVerts[i], new Vector2(ceilVerts[i].x * 0.1f, ceilVerts[i].z * 0.1f));
                }

                for (int i = 0; i < ceilVerts.Count; i++)
                {
                    int currentIdx = ceilingBaseIdx + 1 + i;
                    int nextIdx = ceilingBaseIdx + 1 + ((i + 1) % ceilVerts.Count);

                    meshData.AddTriangleIndices(ceilingBaseIdx, currentIdx, nextIdx);
                }
            }

            // 3. ROOM PERIMETER WALLS (With Double-Sided Header Walls above Doorways)
            if (structureSettings.generateWalls)
            {
                for (int i = 0; i < perimeterXZ.Count; i++)
                {
                    int nextI = (i + 1) % perimeterXZ.Count;
                    Vector2 p0 = perimeterXZ[i];
                    Vector2 p1 = perimeterXZ[nextI];

                    // Check if this segment matches a doorway opening pair
                    bool isDoorwaySegment = false;
                    foreach (DoorwayData dw in doorways)
                    {
                        if ((Vector2.Distance(p0, dw.left) < 0.2f && Vector2.Distance(p1, dw.right) < 0.2f) ||
                            (Vector2.Distance(p0, dw.right) < 0.2f && Vector2.Distance(p1, dw.left) < 0.2f))
                        {
                            isDoorwaySegment = true;
                            break;
                        }
                    }

                    Vector3 p0_floor = floorVerts[i];
                    Vector3 p1_floor = floorVerts[nextI];
                    Vector3 p0_ceil = ceilVerts[i];
                    Vector3 p1_ceil = ceilVerts[nextI];

                    if (isDoorwaySegment)
                    {
                        float passCeilY0 = heightSampler.GetCeilingY(p0.x, p0.y, baseFloorY + corridorHeight);
                        float passCeilY1 = heightSampler.GetCeilingY(p1.x, p1.y, baseFloorY + corridorHeight);

                        Vector3 p0_passCeil = new Vector3(p0.x, passCeilY0, p0.y);
                        Vector3 p1_passCeil = new Vector3(p1.x, passCeilY1, p1.y);

                        Vector3 p0_roomCeil = p0_ceil;
                        Vector3 p1_roomCeil = p1_ceil;

                        float yMin0 = Mathf.Min(p0_passCeil.y, p0_roomCeil.y);
                        float yMax0 = Mathf.Max(p0_passCeil.y, p0_roomCeil.y);
                        float yMin1 = Mathf.Min(p1_passCeil.y, p1_roomCeil.y);
                        float yMax1 = Mathf.Max(p1_passCeil.y, p1_roomCeil.y);

                        if (yMax0 - yMin0 > 0.02f || yMax1 - yMin1 > 0.02f)
                        {
                            Vector3 bL = new Vector3(p0.x, yMin0, p0.y);
                            Vector3 bR = new Vector3(p1.x, yMin1, p1.y);
                            Vector3 tR = new Vector3(p1.x, yMax1, p1.y);
                            Vector3 tL = new Vector3(p0.x, yMax0, p0.y);

                            // 1. Room-Facing Header Wall Quad (Facing INTO Room)
                            int rIdx = meshData.VertexCount;
                            meshData.AddVertex(bL, new Vector2(bL.x * 0.1f, bL.y * 0.1f));
                            meshData.AddVertex(bR, new Vector2(bR.x * 0.1f, bR.y * 0.1f));
                            meshData.AddVertex(tR, new Vector2(tR.x * 0.1f, tR.y * 0.1f));
                            meshData.AddVertex(tL, new Vector2(tL.x * 0.1f, tL.y * 0.1f));

                            meshData.AddQuadIndices(rIdx, rIdx + 1, rIdx + 2, rIdx + 3);

                            // 2. Passage-Facing Header Wall Quad (Facing INTO Passage)
                            int pIdx = meshData.VertexCount;
                            meshData.AddVertex(bR, new Vector2(bR.x * 0.1f, bR.y * 0.1f));
                            meshData.AddVertex(bL, new Vector2(bL.x * 0.1f, bL.y * 0.1f));
                            meshData.AddVertex(tL, new Vector2(tL.x * 0.1f, tL.y * 0.1f));
                            meshData.AddVertex(tR, new Vector2(tR.x * 0.1f, tR.y * 0.1f));

                            meshData.AddQuadIndices(pIdx, pIdx + 1, pIdx + 2, pIdx + 3);
                        }

                        // 3. Doorway Archway Underside Lintel / Soffit Quad (Facing DOWN into walkway)
                        Vector2 segDir = (p1 - p0).normalized;
                        Vector2 outNorm = new Vector2(-segDir.y, segDir.x);

                        Vector3 p0_ext = p0_passCeil + new Vector3(outNorm.x, 0f, outNorm.y) * 0.6f;
                        Vector3 p1_ext = p1_passCeil + new Vector3(outNorm.x, 0f, outNorm.y) * 0.6f;

                        int sIdx = meshData.VertexCount;
                        meshData.AddVertex(p0_passCeil, new Vector2(p0_passCeil.x * 0.1f, p0_passCeil.z * 0.1f));
                        meshData.AddVertex(p1_passCeil, new Vector2(p1_passCeil.x * 0.1f, p1_passCeil.z * 0.1f));
                        meshData.AddVertex(p1_ext, new Vector2(p1_ext.x * 0.1f, p1_ext.z * 0.1f));
                        meshData.AddVertex(p0_ext, new Vector2(p0_ext.x * 0.1f, p0_ext.z * 0.1f));

                        meshData.AddQuadIndices(sIdx, sIdx + 1, sIdx + 2, sIdx + 3);
                    }
                    else
                    {
                        // Full Room Wall Quad from floor to room ceiling
                        int wallBaseIdx = meshData.VertexCount;
                        meshData.AddVertex(p0_floor, new Vector2(p0_floor.x * 0.1f, p0_floor.y * 0.1f));
                        meshData.AddVertex(p1_floor, new Vector2(p1_floor.x * 0.1f, p1_floor.y * 0.1f));
                        meshData.AddVertex(p1_ceil, new Vector2(p1_ceil.x * 0.1f, p1_ceil.y * 0.1f));
                        meshData.AddVertex(p0_ceil, new Vector2(p0_ceil.x * 0.1f, p0_ceil.y * 0.1f));

                        meshData.AddQuadIndices(wallBaseIdx, wallBaseIdx + 1, wallBaseIdx + 2, wallBaseIdx + 3);
                    }
                }
            }
        }
    }
}
