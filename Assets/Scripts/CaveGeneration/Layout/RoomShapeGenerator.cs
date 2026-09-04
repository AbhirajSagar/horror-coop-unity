using System;
using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Geometry;

namespace CaveGeneration.Layout
{
    /// <summary>
    /// Generates organic 2D room perimeter polygons with starburst lobes, chasms, and alcove niches.
    /// </summary>
    public class RoomShapeGenerator
    {
        public List<Vector2> GenerateBaseRoomPerimeter(RoomData room, RoomShapeSettings settings, System.Random rand, int pointCountOverride = -1)
        {
            Vector3 center = room.center;
            List<Vector2> baseVerts = new List<Vector2>();

            int points;
            if (pointCountOverride > 0)
            {
                points = pointCountOverride;
            }
            else if (settings.enableRandomRoomPointCount)
            {
                int minP = Mathf.Min(settings.minRoomPointCount, settings.maxRoomPointCount);
                int maxP = Mathf.Max(settings.minRoomPointCount, settings.maxRoomPointCount);
                points = rand.Next(minP, maxP + 1);
            }
            else
            {
                points = Mathf.Max(6, settings.minRoomPointCount);
            }

            double shapeRoll = rand.NextDouble();
            bool isChasm = settings.enableElongatedChasms && (shapeRoll < settings.chasmChance);
            bool isStarburst = settings.enableVariedRoomShapes && (!isChasm) && (shapeRoll < settings.chasmChance + 0.40f);

            float chasmAngle = (float)rand.NextDouble() * Mathf.PI * 2.0f;
            float seedOffset = (float)rand.NextDouble() * 10.0f;

            for (int i = 0; i < points; i++)
            {
                float baseAngle = (i / (float)points) * Mathf.PI * 2f;
                float jitter = (float)(rand.NextDouble() - 0.5) * (Mathf.PI * 2f / points) * 0.7f;
                float angle = baseAngle + jitter;
                float radius = CaveGeometryUtils.GetRandomFloat(rand, settings.minRoomRadius, settings.maxRoomRadius);

                if (isChasm)
                {
                    // Elongated Chasm / Ravine Hall Cavern
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, chasmAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                    float elongationFactor = Mathf.Lerp(2.0f, 0.6f, Mathf.Sin(angleDiff));
                    radius *= elongationFactor;
                }
                else if (isStarburst)
                {
                    // Multi-Lobed Starburst Cavern
                    float harmonic = 1.0f + 0.35f * Mathf.Sin(angle * 3.0f + seedOffset) + 0.20f * Mathf.Sin(angle * 5.0f);
                    radius *= harmonic;
                }

                float x = center.x + Mathf.Cos(angle) * radius;
                float z = center.z + Mathf.Sin(angle) * radius;
                baseVerts.Add(new Vector2(x, z));
            }

            baseVerts.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - center.z, a.x - center.x);
                float angleB = Mathf.Atan2(b.y - center.z, b.x - center.x);
                return angleA.CompareTo(angleB);
            });

            // Procedural Bay Alcoves & Side Niches
            List<Vector2> finalPerimeter = new List<Vector2>();

            for (int i = 0; i < baseVerts.Count; i++)
            {
                int nextI = (i + 1) % baseVerts.Count;
                Vector2 p0 = baseVerts[i];
                Vector2 p1 = baseVerts[nextI];

                finalPerimeter.Add(p0);

                if (settings.enableVariedRoomShapes && settings.maxAlcoveDepth > 0.5f && rand.NextDouble() < settings.alcoveChance)
                {
                    Vector2 segDir = (p1 - p0).normalized;
                    Vector2 outNorm = new Vector2(-segDir.y, segDir.x); // Radial outward normal
                    Vector2 mid = (p0 + p1) * 0.5f;

                    float depth = (float)rand.NextDouble() * (settings.maxAlcoveDepth - 1.0f) + 1.0f;
                    Vector2 alcoveP = mid + outNorm * depth;

                    finalPerimeter.Add(alcoveP);
                }
            }

            finalPerimeter.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - center.z, a.x - center.x);
                float angleB = Mathf.Atan2(b.y - center.z, b.x - center.x);
                return angleA.CompareTo(angleB);
            });

            return finalPerimeter;
        }
    }
}
