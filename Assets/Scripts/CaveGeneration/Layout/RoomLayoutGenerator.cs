using System;
using System.Collections.Generic;
using UnityEngine;
using CaveGeneration.Configuration;
using CaveGeneration.Data;
using CaveGeneration.Geometry;

namespace CaveGeneration.Layout
{
    /// <summary>
    /// Computes non-overlapping spatial positions, floor elevations, and wall heights for cave rooms.
    /// </summary>
    public class RoomLayoutGenerator
    {
        public List<RoomData> GenerateRooms(RoomLayoutSettings settings, System.Random rand)
        {
            List<RoomData> rooms = new List<RoomData>();
            if (settings.roomCount <= 0) return rooms;

            float safeMinDist = Mathf.Max(settings.minRoomDistance, settings.maxRoomRadius * 2f + settings.roomPadding);
            float safeMaxDist = Mathf.Max(settings.maxRoomDistance, safeMinDist + 10f);

            float startElevation = settings.enableFloorElevation 
                ? CaveGeometryUtils.GetRandomFloat(rand, settings.minFloorElevation, settings.maxFloorElevation) 
                : 0f;

            RoomData startRoom = new RoomData
            {
                center = new Vector3(0f, startElevation, 0f),
                floorElevation = startElevation,
                wallHeight = CaveGeometryUtils.GetRandomFloat(rand, settings.minWallHeight, settings.maxWallHeight),
                avgRadius = (settings.minRoomRadius + settings.maxRoomRadius) * 0.5f,
                maxRadius = settings.maxRoomRadius
            };
            rooms.Add(startRoom);

            for (int i = 1; i < settings.roomCount; i++)
            {
                bool placed = false;
                float currentMinDist = safeMinDist;
                float currentMaxDist = safeMaxDist;

                for (int attempt = 0; attempt < 300; attempt++)
                {
                    // Gradually expand search distance if tight space
                    if (attempt > 0 && attempt % 40 == 0)
                    {
                        currentMinDist += 5f;
                        currentMaxDist += 10f;
                    }

                    int parentIdx = rand.Next(0, rooms.Count);
                    RoomData parentRoom = rooms[parentIdx];

                    float angle = (float)(rand.NextDouble() * Mathf.PI * 2f);
                    float dist = CaveGeometryUtils.GetRandomFloat(rand, currentMinDist, currentMaxDist);

                    float targetElevation = settings.enableFloorElevation 
                        ? CaveGeometryUtils.GetRandomFloat(rand, settings.minFloorElevation, settings.maxFloorElevation) 
                        : 0f;

                    Vector3 candidateCenter = parentRoom.center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                    candidateCenter.y = targetElevation;

                    // Check overlap against all existing rooms on XZ plane
                    bool overlaps = false;
                    foreach (RoomData existingRoom in rooms)
                    {
                        float dist2D = Vector2.Distance(
                            new Vector2(candidateCenter.x, candidateCenter.z),
                            new Vector2(existingRoom.center.x, existingRoom.center.z)
                        );

                        float minAllowedDist = existingRoom.maxRadius + settings.maxRoomRadius + settings.roomPadding;
                        if (dist2D < minAllowedDist)
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        float wallH = settings.enableRoomHeightVariation 
                            ? CaveGeometryUtils.GetRandomFloat(rand, settings.minWallHeight, settings.maxWallHeight) 
                            : settings.minWallHeight;

                        RoomData newRoom = new RoomData
                        {
                            center = candidateCenter,
                            floorElevation = targetElevation,
                            wallHeight = wallH,
                            avgRadius = (settings.minRoomRadius + settings.maxRoomRadius) * 0.5f,
                            maxRadius = settings.maxRoomRadius
                        };

                        rooms.Add(newRoom);
                        placed = true;
                        break;
                    }
                }

                // Fallback placement if space is constrained
                if (!placed)
                {
                    float fallbackAngle = (float)(rand.NextDouble() * Mathf.PI * 2f);
                    float fallbackDist = (rooms.Count + 1) * (settings.maxRoomRadius * 2f + settings.roomPadding + 5f);
                    float fallbackElevation = settings.enableFloorElevation 
                        ? CaveGeometryUtils.GetRandomFloat(rand, settings.minFloorElevation, settings.maxFloorElevation) 
                        : 0f;
                    Vector3 fallbackCenter = new Vector3(Mathf.Cos(fallbackAngle) * fallbackDist, fallbackElevation, Mathf.Sin(fallbackAngle) * fallbackDist);

                    float wallH = settings.enableRoomHeightVariation 
                        ? CaveGeometryUtils.GetRandomFloat(rand, settings.minWallHeight, settings.maxWallHeight) 
                        : settings.minWallHeight;

                    RoomData fallbackRoom = new RoomData
                    {
                        center = fallbackCenter,
                        floorElevation = fallbackElevation,
                        wallHeight = wallH,
                        avgRadius = (settings.minRoomRadius + settings.maxRoomRadius) * 0.5f,
                        maxRadius = settings.maxRoomRadius
                    };
                    rooms.Add(fallbackRoom);
                }
            }

            return rooms;
        }
    }
}
