using System;
using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Noise;

namespace CaveGeneration.MeshBuilders
{
    /// <summary>
    /// Constructs decorative rock pillars, support columns, stalactites, and stalagmites within cave rooms.
    /// </summary>
    public class RoomFeatureBuilder
    {
        public void BuildPillars(
            RoomData room,
            List<DoorwayData> doorways,
            RoomFeatureSettings settings,
            TerrainHeightSampler heightSampler,
            Vector3 floorCenter,
            List<Vector3> floorVerts,
            Vector3 ceilCenter,
            List<Vector3> ceilVerts,
            CaveMeshData meshData,
            string seed,
            bool invertNormals,
            float corridorWidth)
        {
            if (!settings.enableRoomPillars || settings.maxPillarsPerRoom <= 0 || room.avgRadius < settings.minRoomRadius + 1.0f)
            {
                return;
            }

            Vector3 center = room.center;
            Vector2 centerXZ = new Vector2(center.x, center.z);
            float baseFloorY = room.floorElevation;
            float height = room.wallHeight;

            System.Random pRand = new System.Random((int)(center.x * 37f + center.z * 53f + seed.GetHashCode()));
            int pillarCount = pRand.Next(1, settings.maxPillarsPerRoom + 1);

            for (int p = 0; p < pillarCount; p++)
            {
                float distFromCenter = (float)pRand.NextDouble() * (room.avgRadius * 0.45f);
                float pAngle = (float)pRand.NextDouble() * Mathf.PI * 2.0f;
                Vector2 pCenter = centerXZ + new Vector2(Mathf.Cos(pAngle), Mathf.Sin(pAngle)) * distFromCenter;

                // Ensure pillar is at a safe distance from all doorway entrances
                bool isNearDoorway = false;
                if (doorways != null)
                {
                    foreach (DoorwayData dw in doorways)
                    {
                        if (Vector2.Distance(pCenter, dw.center) < corridorWidth * 1.2f)
                        {
                            isNearDoorway = true;
                            break;
                        }
                    }
                }

                if (isNearDoorway) continue;

                float pRadius = (float)pRand.NextDouble() * (settings.maxPillarRadius - settings.minPillarRadius) + settings.minPillarRadius;
                int pPoints = Mathf.Max(4, settings.pillarPointCount);

                List<Vector3> pillarFloorVerts = new List<Vector3>(pPoints);
                List<Vector3> pillarCeilVerts = new List<Vector3>(pPoints);

                for (int j = 0; j < pPoints; j++)
                {
                    float angleJ = ((float)j / pPoints) * Mathf.PI * 2.0f;
                    float rVar = pRadius * (1.0f + ((float)pRand.NextDouble() - 0.5f) * 0.3f);
                    float px = pCenter.x + Mathf.Cos(angleJ) * rVar;
                    float pz = pCenter.y + Mathf.Sin(angleJ) * rVar;

                    float fy = heightSampler.GetExactRoomFloorY(new Vector2(px, pz), floorCenter, floorVerts, baseFloorY);
                    float cy = heightSampler.GetExactRoomCeilingY(new Vector2(px, pz), ceilCenter, ceilVerts, baseFloorY + height);

                    pillarFloorVerts.Add(new Vector3(px, fy, pz));
                    pillarCeilVerts.Add(new Vector3(px, cy, pz));
                }

                // Render pillar wall quads (facing OUTWARD into room)
                for (int j = 0; j < pPoints; j++)
                {
                    int nextJ = (j + 1) % pPoints;
                    int pBaseIdx = meshData.VertexCount;

                    Vector3 pf0 = pillarFloorVerts[j];
                    Vector3 pf1 = pillarFloorVerts[nextJ];
                    Vector3 pc1 = pillarCeilVerts[nextJ];
                    Vector3 pc0 = pillarCeilVerts[j];

                    meshData.AddVertex(pf0, new Vector2(pf0.x * 0.1f, pf0.z * 0.1f));
                    meshData.AddVertex(pf1, new Vector2(pf1.x * 0.1f, pf1.z * 0.1f));
                    meshData.AddVertex(pc1, new Vector2(pc1.x * 0.1f, pc1.z * 0.1f));
                    meshData.AddVertex(pc0, new Vector2(pc0.x * 0.1f, pc0.z * 0.1f));

                    if (!invertNormals)
                    {
                        meshData.AddTriangleIndices(pBaseIdx + 0, pBaseIdx + 2, pBaseIdx + 1);
                        meshData.AddTriangleIndices(pBaseIdx + 0, pBaseIdx + 3, pBaseIdx + 2);
                    }
                    else
                    {
                        meshData.AddTriangleIndices(pBaseIdx + 0, pBaseIdx + 1, pBaseIdx + 2);
                        meshData.AddTriangleIndices(pBaseIdx + 0, pBaseIdx + 2, pBaseIdx + 3);
                    }
                }
            }
        }

        public void BuildStalactitesAndStalagmites(
            RoomData room,
            List<DoorwayData> doorways,
            RoomFeatureSettings settings,
            TerrainHeightSampler heightSampler,
            Vector3 floorCenter,
            List<Vector3> floorVerts,
            Vector3 ceilCenter,
            List<Vector3> ceilVerts,
            CaveMeshData meshData,
            string seed,
            bool invertNormals,
            float corridorWidth)
        {
            if (!settings.enableStalactitesAndStalagmites || settings.maxSpikesPerRoom <= 0)
            {
                return;
            }

            Vector3 center = room.center;
            Vector2 centerXZ = new Vector2(center.x, center.z);
            float baseFloorY = room.floorElevation;
            float height = room.wallHeight;

            System.Random sRand = new System.Random((int)(center.x * 71f + center.z * 89f + seed.GetHashCode()));
            int spikeCount = sRand.Next(2, settings.maxSpikesPerRoom + 1);

            for (int s = 0; s < spikeCount; s++)
            {
                float distFromCenter = (float)sRand.NextDouble() * (room.avgRadius * 0.70f);
                float sAngle = (float)sRand.NextDouble() * Mathf.PI * 2.0f;
                Vector2 sCenter = centerXZ + new Vector2(Mathf.Cos(sAngle), Mathf.Sin(sAngle)) * distFromCenter;

                // Ensure spike is at a safe distance from doorway entrances
                bool isNearDoorway = false;
                if (doorways != null)
                {
                    foreach (DoorwayData dw in doorways)
                    {
                        if (Vector2.Distance(sCenter, dw.center) < corridorWidth * 1.0f)
                        {
                            isNearDoorway = true;
                            break;
                        }
                    }
                }

                if (isNearDoorway) continue;

                bool isStalactite = sRand.NextDouble() > 0.5; // True = Stalactite (Ceiling), False = Stalagmite (Floor)
                float spkHeight = (float)sRand.NextDouble() * (settings.maxSpikeHeight - settings.minSpikeHeight) + settings.minSpikeHeight;
                float spkRadius = (float)sRand.NextDouble() * (settings.maxSpikeRadius - settings.minSpikeRadius) + settings.minSpikeRadius;
                int sides = Mathf.Max(3, settings.spikeSides);

                float fyCenter = heightSampler.GetExactRoomFloorY(sCenter, floorCenter, floorVerts, baseFloorY);
                float cyCenter = heightSampler.GetExactRoomCeilingY(sCenter, ceilCenter, ceilVerts, baseFloorY + height);

                // Cap height if room is too short
                float availableSpace = cyCenter - fyCenter;
                spkHeight = Mathf.Min(spkHeight, availableSpace * 0.65f);

                List<Vector3> baseRing = new List<Vector3>(sides);
                Vector3 apex;

                if (isStalactite)
                {
                    // Ceiling spire pointing DOWN from exact ceiling mesh
                    apex = new Vector3(sCenter.x, cyCenter - spkHeight, sCenter.y);

                    for (int j = 0; j < sides; j++)
                    {
                        float aJ = ((float)j / sides) * Mathf.PI * 2.0f;
                        float rVar = spkRadius * (1.0f + ((float)sRand.NextDouble() - 0.5f) * 0.2f);
                        float bx = sCenter.x + Mathf.Cos(aJ) * rVar;
                        float bz = sCenter.y + Mathf.Sin(aJ) * rVar;
                        float bY = heightSampler.GetExactRoomCeilingY(new Vector2(bx, bz), ceilCenter, ceilVerts, baseFloorY + height);
                        baseRing.Add(new Vector3(bx, bY, bz));
                    }
                }
                else
                {
                    // Floor spire pointing UP from exact floor mesh
                    apex = new Vector3(sCenter.x, fyCenter + spkHeight, sCenter.y);

                    for (int j = 0; j < sides; j++)
                    {
                        float aJ = ((float)j / sides) * Mathf.PI * 2.0f;
                        float rVar = spkRadius * (1.0f + ((float)sRand.NextDouble() - 0.5f) * 0.2f);
                        float bx = sCenter.x + Mathf.Cos(aJ) * rVar;
                        float bz = sCenter.y + Mathf.Sin(aJ) * rVar;
                        float bY = heightSampler.GetExactRoomFloorY(new Vector2(bx, bz), floorCenter, floorVerts, baseFloorY);
                        baseRing.Add(new Vector3(bx, bY, bz));
                    }
                }

                // Render conical side triangles
                for (int j = 0; j < sides; j++)
                {
                    int nextJ = (j + 1) % sides;
                    int sBaseIdx = meshData.VertexCount;

                    Vector3 b0 = baseRing[j];
                    Vector3 b1 = baseRing[nextJ];

                    meshData.AddVertex(b0, new Vector2(b0.x * 0.1f, b0.z * 0.1f));
                    meshData.AddVertex(b1, new Vector2(b1.x * 0.1f, b1.z * 0.1f));
                    meshData.AddVertex(apex, new Vector2(apex.x * 0.1f, apex.z * 0.1f));

                    if (isStalactite)
                    {
                        if (!invertNormals)
                        {
                            meshData.AddTriangleIndices(sBaseIdx + 0, sBaseIdx + 1, sBaseIdx + 2);
                        }
                        else
                        {
                            meshData.AddTriangleIndices(sBaseIdx + 0, sBaseIdx + 2, sBaseIdx + 1);
                        }
                    }
                    else
                    {
                        if (!invertNormals)
                        {
                            meshData.AddTriangleIndices(sBaseIdx + 0, sBaseIdx + 2, sBaseIdx + 1);
                        }
                        else
                        {
                            meshData.AddTriangleIndices(sBaseIdx + 0, sBaseIdx + 1, sBaseIdx + 2);
                        }
                    }
                }
            }
        }
    }
}
