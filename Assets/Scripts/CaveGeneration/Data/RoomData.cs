using UnityEngine;

namespace CaveGeneration.Data
{
    /// <summary>
    /// Represents the spatial and physical dimensions of a single procedural cave room.
    /// </summary>
    [System.Serializable]
    public struct RoomData
    {
        public Vector3 center;
        public float floorElevation;
        public float wallHeight;
        public float avgRadius;
        public float maxRadius;

        public RoomData(Vector3 center, float floorElevation, float wallHeight, float avgRadius, float maxRadius)
        {
            this.center = center;
            this.floorElevation = floorElevation;
            this.wallHeight = wallHeight;
            this.avgRadius = avgRadius;
            this.maxRadius = maxRadius;
        }
    }
}
