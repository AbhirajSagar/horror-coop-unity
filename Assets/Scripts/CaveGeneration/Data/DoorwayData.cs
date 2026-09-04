using UnityEngine;

namespace CaveGeneration.Data
{
    /// <summary>
    /// Represents doorway opening geometry on the 2D perimeter of a cave room.
    /// </summary>
    public struct DoorwayData
    {
        public Vector2 center;
        public Vector2 left;
        public Vector2 right;

        public DoorwayData(Vector2 center, Vector2 left, Vector2 right)
        {
            this.center = center;
            this.left = left;
            this.right = right;
        }
    }
}
