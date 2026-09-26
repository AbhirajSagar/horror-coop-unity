using System;
using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Data;

namespace CaveGeneration.Geometry
{
    /// <summary>
    /// Math and 2D/3D geometry utilities for cave room perimeters, ray intersections, and doorway calculations.
    /// </summary>
    public static class CaveGeometryUtils
    {
        public static float GetRandomFloat(System.Random rand, float min, float max)
        {
            return (float)(rand.NextDouble() * (max - min) + min);
        }

        public static bool DoSegmentsIntersectXZ(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
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

        public static Vector2 GetRoomPerimeterIntersection(Vector2 centerXZ, List<Vector2> perimeterXZ, Vector2 targetXZ)
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

        public static float DistancePointToSegmentXZ(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen == 0f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
            Vector2 projection = a + t * ab;
            return Vector2.Distance(p, projection);
        }

        public static bool IsPointInsidePolygon(Vector2 point, List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;
            bool inside = false;
            int count = polygon.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];
                if (((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static float GetDistanceToPolygonBoundary(Vector2 point, List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 2) return float.MaxValue;
            float minDistance = float.MaxValue;
            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 p1 = polygon[i];
                Vector2 p2 = polygon[(i + 1) % count];
                float dist = DistancePointToSegmentXZ(point, p1, p2);
                if (dist < minDistance)
                {
                    minDistance = dist;
                }
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates all doorway frames (center, left, right) for a room based on connected corridors.
        /// </summary>
        public static List<DoorwayData> CalculateDoorways(
            int roomIndex,
            List<RoomData> rooms,
            List<Vector2> basePerimeter,
            List<CorridorData> corridors,
            float corridorWidth)
        {
            List<DoorwayData> doorways = new List<DoorwayData>();
            if (corridors == null || corridors.Count == 0) return doorways;

            Vector3 center = rooms[roomIndex].center;
            Vector2 centerXZ = new Vector2(center.x, center.z);
            float halfW = corridorWidth * 0.5f;

            foreach (CorridorData corr in corridors)
            {
                if (corr.roomA == roomIndex || corr.roomB == roomIndex)
                {
                    int otherIdx = (corr.roomA == roomIndex) ? corr.roomB : corr.roomA;
                    Vector2 cA = (corr.roomA == roomIndex) ? centerXZ : new Vector2(rooms[otherIdx].center.x, rooms[otherIdx].center.z);
                    Vector2 cB = (corr.roomA == roomIndex) ? new Vector2(rooms[otherIdx].center.x, rooms[otherIdx].center.z) : centerXZ;

                    Vector2 dirXZ = (cB - cA).normalized;
                    if (dirXZ == Vector2.zero) continue;

                    Vector2 rightXZ = new Vector2(-dirXZ.y, dirXZ.x);

                    Vector2 doorwayPoint = GetRoomPerimeterIntersection(centerXZ, basePerimeter, (corr.roomA == roomIndex) ? cB : cA);
                    Vector2 dwL = doorwayPoint - rightXZ * halfW;
                    Vector2 dwR = doorwayPoint + rightXZ * halfW;

                    doorways.Add(new DoorwayData(doorwayPoint, dwL, dwR));
                }
            }

            return doorways;
        }

        /// <summary>
        /// Cuts doorway gaps out of base perimeter vertices, inserts doorway left/right vertices, and sorts counter-clockwise.
        /// </summary>
        public static List<Vector2> BuildDoorwayAdjustedPerimeter(
            List<Vector2> basePerimeter,
            List<DoorwayData> doorways,
            Vector3 center,
            float corridorWidth)
        {
            List<Vector2> perimeterXZ = new List<Vector2>();

            foreach (Vector2 p in basePerimeter)
            {
                bool insideDoorway = false;
                foreach (DoorwayData doorway in doorways)
                {
                    Vector2 seg = doorway.right - doorway.left;
                    float sqrLen = seg.sqrMagnitude;
                    if (sqrLen > 0.001f)
                    {
                        float t = Vector2.Dot(p - doorway.left, seg) / sqrLen;
                        Vector2 proj = doorway.left + Mathf.Clamp01(t) * seg;
                        float distToSeg = Vector2.Distance(p, proj);

                        if (t > -0.1f && t < 1.1f && distToSeg < corridorWidth * 0.7f)
                        {
                            insideDoorway = true;
                            break;
                        }
                    }
                }

                if (!insideDoorway)
                {
                    perimeterXZ.Add(p);
                }
            }

            foreach (DoorwayData doorway in doorways)
            {
                perimeterXZ.Add(doorway.left);
                perimeterXZ.Add(doorway.right);
            }

            // Sort counter-clockwise
            perimeterXZ.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - center.z, a.x - center.x);
                float angleB = Mathf.Atan2(b.y - center.z, b.x - center.x);
                return angleA.CompareTo(angleB);
            });

            return perimeterXZ;
        }
    }
}
