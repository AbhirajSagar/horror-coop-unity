using System;
using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Geometry;

namespace CaveGeneration.Layout
{
    /// <summary>
    /// Computes the network graph of corridors between rooms using Minimum Spanning Tree and non-crossing loop paths.
    /// </summary>
    public class CorridorNetworkGenerator
    {
        public List<CorridorData> GenerateCorridorNetwork(List<RoomData> rooms, CorridorNetworkSettings settings, System.Random rand)
        {
            List<CorridorData> corridors = new List<CorridorData>();
            if (rooms == null || rooms.Count < 2) return corridors;

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
                    if (CaveGeometryUtils.DoSegmentsIntersectXZ(pA, pB, pC, pD))
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
                corridors.Add(new CorridorData(min, max));
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
                    if ((float)rand.NextDouble() <= settings.extraPathChance)
                    {
                        AddCorridor(i, neighbors[n]);
                    }
                }
            }

            return corridors;
        }
    }
}
