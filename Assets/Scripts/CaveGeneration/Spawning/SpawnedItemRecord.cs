using UnityEngine;
using HCoop.Types;

namespace CaveGeneration.Spawning
{
    /// <summary>
    /// Record of an already spawned object used for spatial collision and overlap detection.
    /// </summary>
    public struct SpawnedItemRecord
    {
        public Vector3 position;
        public Vector3 clearanceSize;
        public Bounds bounds;
        public Item spawnedItem;

        public GameObject spawnedInstance => spawnedItem != null ? spawnedItem.gameObject : null;

        public SpawnedItemRecord(Vector3 position, Vector3 clearanceSize, Bounds bounds, Item item = null)
        {
            this.position = position;
            this.clearanceSize = clearanceSize;
            this.bounds = bounds;
            this.spawnedItem = item;
        }

        public bool Overlaps(Vector3 candidatePos, Vector3 candidateSize, float minSeparation)
        {
            float horizontalDistance = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(candidatePos.x, candidatePos.z)
            );

            float radiusA = Mathf.Max(clearanceSize.x, clearanceSize.z) * 0.5f;
            float radiusB = Mathf.Max(candidateSize.x, candidateSize.z) * 0.5f;

            if (horizontalDistance < radiusA + radiusB + minSeparation)
            {
                return true;
            }

            Bounds candidateBounds = new Bounds(candidatePos, candidateSize);
            Bounds expanded = bounds;
            expanded.Expand(minSeparation);

            return expanded.Intersects(candidateBounds);
        }
    }
}
