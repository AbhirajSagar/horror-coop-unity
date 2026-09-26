using System;
using UnityEngine;
using HCoop.Types;

namespace CaveGeneration.Configuration
{
    public enum SpawnLocationType
    {
        [Tooltip("Spawn only inside rooms")]
        RoomsOnly,

        [Tooltip("Spawn only along corridors/passages")]
        CorridorsOnly,

        [Tooltip("Spawn in both rooms and corridors")]
        Everywhere
    }

    public enum SpawnDistributionMode
    {
        [Tooltip("Spawn min-max count per room")]
        PerRoom,

        [Tooltip("Spawn min-max count per corridor")]
        PerCorridor,

        [Tooltip("Spawn min-max count across the entire cave system")]
        GlobalCount
    }

    /// <summary>
    /// Configuration rule defining what prefab to spawn, where, and with what spatial clearance.
    /// </summary>
    [Serializable]
    public class PrefabSpawnRule
    {
        [Tooltip("Descriptive label for this rule in the Inspector")]
        public string ruleName = "New Item Rule";

        [Tooltip("The Item component on the prefab to spawn (weapons, crystals, pickups, etc.)")]
        public Item prefab;

        [Tooltip("Allowed placement regions (rooms, corridors, or both)")]
        public SpawnLocationType locationType = SpawnLocationType.Everywhere;

        [Header("Quantity & Probability")]
        [Tooltip("How item quantities are distributed across the cave")]
        public SpawnDistributionMode distributionMode = SpawnDistributionMode.PerRoom;

        [Range(0f, 1f)]
        [Tooltip("Probability of attempting to spawn this item in a room or corridor")]
        public float spawnChance = 0.8f;

        [Min(0)]
        [Tooltip("Minimum quantity to spawn (per room/corridor or globally)")]
        public int minCount = 1;

        [Min(0)]
        [Tooltip("Maximum quantity to spawn (per room/corridor or globally)")]
        public int maxCount = 3;

        [Header("Open Area & Bounding Box Clearance")]
        [Tooltip("Required minimum open 3D bounding box clearance at the spawn point (Width X, Height Y, Depth Z). Prevents spawning items too large for the space.")]
        public Vector3 clearanceBoxSize = new Vector3(1.2f, 1.8f, 1.2f);

        [Min(0f)]
        [Tooltip("Minimum distance required from room and corridor perimeter walls")]
        public float wallPadding = 0.8f;

        [Min(0f)]
        [Tooltip("Minimum distance required from corridor doorway entrances to prevent blocking passages")]
        public float doorwayPadding = 2.0f;

        [Min(0f)]
        [Tooltip("Minimum distance from other spawned items of any type to prevent overlapping")]
        public float minDistanceBetweenItems = 2.0f;

        [Header("Placement & Transform Options")]
        [Tooltip("Offset applied to the floor position (e.g. if object pivot is at center instead of base)")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("Align the spawned object's Up axis with the terrain floor slope normal")]
        public bool alignToSurfaceNormal = false;

        [Tooltip("Randomize rotation around the up axis (0-360 degrees)")]
        public bool randomYaw = true;

        [Tooltip("Enable randomized scale variation")]
        public bool enableRandomScale = false;

        [Range(0.1f, 5f)]
        [Tooltip("Minimum scale multiplier when random scale is enabled")]
        public float minScale = 0.8f;

        [Range(0.1f, 5f)]
        [Tooltip("Maximum scale multiplier when random scale is enabled")]
        public float maxScale = 1.2f;

        [Header("Corridor Lateral Spread")]
        [Range(0f, 1f)]
        [Tooltip("How far from the corridor center line the item may spawn (0 = centerline only, 1 = up to wall padding)")]
        public float corridorLateralSpread = 0.6f;
    }
}
