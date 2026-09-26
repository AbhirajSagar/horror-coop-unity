using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Configuration;

namespace CaveGeneration.Noise
{
    /// <summary>
    /// Handles terrain elevation sampling, Perlin noise displacement, and barycentric mesh height interpolation.
    /// </summary>
    public class TerrainHeightSampler
    {
        public TerrainNoiseSettings Settings { get; set; }

        public TerrainHeightSampler(TerrainNoiseSettings settings)
        {
            Settings = settings;
        }

        public float GetFloorY(float x, float z, float baseElevation)
        {
            // Floor is always generated flat, without bumps
            return baseElevation;
        }

        public float GetCeilingY(float x, float z, float baseElevation)
        {
            if (!Settings.enableOrganicNoise || Settings.ceilingNoiseAmount <= 0f) return baseElevation;
            float n = Mathf.PerlinNoise((x + 100f) * Settings.ceilingNoiseScale, (z + 100f) * Settings.ceilingNoiseScale);
            return baseElevation + (n - 0.5f) * Settings.ceilingNoiseAmount;
        }

        public bool GetTriangleBarycentricY(Vector2 p, Vector3 a, Vector3 b, Vector3 c, out float interpolatedY)
        {
            interpolatedY = 0f;
            Vector2 v0 = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 v1 = new Vector2(c.x - a.x, c.z - a.z);
            Vector2 v2 = p - new Vector2(a.x, a.z);

            float den = v0.x * v1.y - v1.x * v0.y;
            if (Mathf.Abs(den) < 0.00001f) return false;

            float v = (v2.x * v1.y - v1.x * v2.y) / den;
            float w = (v0.x * v2.y - v2.x * v0.y) / den;
            float u = 1.0f - v - w;

            if (u >= -0.01f && v >= -0.01f && w >= -0.01f)
            {
                interpolatedY = u * a.y + v * b.y + w * c.y;
                return true;
            }

            return false;
        }

        public float GetExactRoomFloorY(Vector2 p, Vector3 floorCenter, List<Vector3> floorVerts, float fallbackElevation)
        {
            int count = floorVerts.Count;
            for (int i = 0; i < count; i++)
            {
                int nextI = (i + 1) % count;
                Vector3 a = floorCenter;
                Vector3 b = floorVerts[nextI];
                Vector3 c = floorVerts[i];

                if (GetTriangleBarycentricY(p, a, b, c, out float exactY))
                {
                    return exactY;
                }
            }
            return GetFloorY(p.x, p.y, fallbackElevation);
        }

        public float GetExactRoomCeilingY(Vector2 p, Vector3 ceilCenter, List<Vector3> ceilVerts, float fallbackElevation)
        {
            int count = ceilVerts.Count;
            for (int i = 0; i < count; i++)
            {
                int nextI = (i + 1) % count;
                Vector3 a = ceilCenter;
                Vector3 b = ceilVerts[i];
                Vector3 c = ceilVerts[nextI];

                if (GetTriangleBarycentricY(p, a, b, c, out float exactY))
                {
                    return exactY;
                }
            }
            return GetCeilingY(p.x, p.y, fallbackElevation);
        }
    }
}
