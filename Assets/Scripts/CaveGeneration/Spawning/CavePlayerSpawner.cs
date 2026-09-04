using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Data;
using CaveGeneration.Noise;

namespace CaveGeneration.Spawning
{
    /// <summary>
    /// Handles player positioning, room-to-client mapping, physics stabilization, and spawn calculations.
    /// </summary>
    public class CavePlayerSpawner
    {
        public Vector3 GetSpawnPosition(
            List<RoomData> rooms,
            int roomIndex,
            float playerSpawnYOffset,
            TerrainHeightSampler heightSampler,
            Vector3 fallbackPosition)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return fallbackPosition;
            }

            int clampedIndex = Mathf.Clamp(roomIndex, 0, rooms.Count - 1);
            RoomData selectedRoom = rooms[clampedIndex];

            float floorY = heightSampler != null
                ? heightSampler.GetFloorY(selectedRoom.center.x, selectedRoom.center.z, selectedRoom.floorElevation)
                : selectedRoom.floorElevation;

            return new Vector3(selectedRoom.center.x, floorY + playerSpawnYOffset, selectedRoom.center.z);
        }

        public Vector3 GetRandomRoomSpawnPosition(
            List<RoomData> rooms,
            float playerSpawnYOffset,
            TerrainHeightSampler heightSampler,
            Vector3 fallbackPosition)
        {
            if (rooms == null || rooms.Count == 0) return fallbackPosition;
            int randomRoomIndex = Random.Range(0, rooms.Count);
            return GetSpawnPosition(rooms, randomRoomIndex, playerSpawnYOffset, heightSampler, fallbackPosition);
        }

        public Vector3 GetSpawnPositionForPlayer(
            List<RoomData> rooms,
            ulong clientId,
            float playerSpawnYOffset,
            TerrainHeightSampler heightSampler,
            Vector3 fallbackPosition)
        {
            if (rooms == null || rooms.Count == 0) return fallbackPosition;
            int roomIndex = (int)(clientId % (ulong)rooms.Count);
            return GetSpawnPosition(rooms, roomIndex, playerSpawnYOffset, heightSampler, fallbackPosition);
        }

        public void PlacePlayer(Transform targetTransform, Vector3 spawnPosition)
        {
            if (targetTransform == null) return;

            targetTransform.position = spawnPosition;

            Rigidbody rb = targetTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = spawnPosition;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
        }

        public void PlacePlayersInRooms(
            List<RoomData> rooms,
            Transform fallbackPlayerTransform,
            float playerSpawnYOffset,
            TerrainHeightSampler heightSampler)
        {
            if (rooms == null || rooms.Count == 0) return;

            Movement[] movements = Object.FindObjectsByType<Movement>(FindObjectsSortMode.None);
            if (movements != null && movements.Length > 0)
            {
                foreach (Movement m in movements)
                {
                    int roomIdx = (int)(m.OwnerClientId % (ulong)rooms.Count);
                    Vector3 spawnPos = GetSpawnPosition(rooms, roomIdx, playerSpawnYOffset, heightSampler, Vector3.zero);
                    PlacePlayer(m.transform, spawnPos);
                }
                return;
            }

            Transform target = fallbackPlayerTransform;
            if (target == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }

            if (target != null)
            {
                Vector3 spawnPos = GetRandomRoomSpawnPosition(rooms, playerSpawnYOffset, heightSampler, Vector3.zero);
                PlacePlayer(target, spawnPos);
            }
        }
    }
}