﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bot.ExtensionMethods;
using Bot.GameData;
using Bot.GameSense;
using SC2APIProtocol;

namespace Bot.MapKnowledge;

public class MapAnalyzer: INeedUpdating {
    public static readonly MapAnalyzer Instance = new MapAnalyzer();

    public static Vector3 StartingLocation;
    public static Vector3 EnemyStartingLocation;

    public static List<List<float>> HeightMap;
    private static List<List<bool>> _walkMap;

    private static int _maxX;
    private static int _maxY;

    public static bool IsInitialized = false;

    private MapAnalyzer() {}

    public void Update(ResponseObservation observation) {
        if (IsInitialized) {
            return;
        }

        _maxX = Controller.GameInfo.StartRaw.MapSize.X;
        _maxY = Controller.GameInfo.StartRaw.MapSize.Y;

        InitSpawnLocations();
        InitHeightMap();
        InitWalkMap();

        IsInitialized = true;
    }

    private static void InitSpawnLocations() {
        StartingLocation = Controller.GetUnits(UnitsTracker.OwnedUnits, Units.ResourceCenters).First().Position;
        EnemyStartingLocation = Controller.GameInfo.StartRaw.StartLocations
            .Select(startLocation => new Vector3(startLocation.X, startLocation.Y, 0))
            .MaxBy(enemyLocation => StartingLocation.HorizontalDistanceTo(enemyLocation));
    }

    private static void InitHeightMap() {
        HeightMap = new List<List<float>>();
        for (var x = 0; x < _maxX; x++) {
            HeightMap.Add(new List<float>(new float[_maxY]));
        }

        var heightVector = Controller.GameInfo.StartRaw.TerrainHeight.Data
            .ToByteArray()
            .Select(ByteToFloat)
            .ToList();

        for (var x = 0; x < _maxX; x++) {
            for (var y = 0; y < _maxY; y++) {
                HeightMap[x][y] = heightVector[y * _maxX + x]; // heightVector[4] is (4, 0)
            }
        }
    }

    private static float ByteToFloat(byte byteValue) {
        // Computed from 3 unit positions and 3 height map bytes
        // Seems to work fine
        return 0.125f * byteValue - 15.888f;
    }

    private static void InitWalkMap() {
        _walkMap = new List<List<bool>>();
        for (var x = 0; x < _maxX; x++) {
            _walkMap.Add(new List<bool>(new bool[_maxY]));
        }

        var walkVector = Controller.GameInfo.StartRaw.PathingGrid.Data
            .ToByteArray()
            .SelectMany(ByteToBoolArray)
            .ToList();

        for (var x = 0; x < _maxX; x++) {
            for (var y = 0; y < _maxY; y++) {
                _walkMap[x][y] = walkVector[y * _maxX + x]; // walkVector[4] is (4, 0)
            }
        }

        // The walk data makes cells occupied by buildings impassable
        // However, if I want to find a path from my hatch to the enemy, the pathfinding will fail because the hatchery is impassable
        // Lucky for us, when we init the walk map, there's only 1 building so we'll make its cells walkable
        var startingTownHall = Controller.GetUnits(UnitsTracker.OwnedUnits, Units.Hatchery).First();
        var townHallCells = BuildSearchGrid(startingTownHall.Position, Buildings.GetRadius(startingTownHall.UnitType));

        foreach (var cell in townHallCells) {
            _walkMap[(int)cell.X][(int)cell.Y] = true;
        }
    }

    private static bool[] ByteToBoolArray(byte byteValue)
    {
        // Each byte represents 8 grid cells
        var values = new bool[8];

        values[7] = (byteValue & 1) != 0;
        values[6] = (byteValue & 2) != 0;
        values[5] = (byteValue & 4) != 0;
        values[4] = (byteValue & 8) != 0;
        values[3] = (byteValue & 16) != 0;
        values[2] = (byteValue & 32) != 0;
        values[1] = (byteValue & 64) != 0;
        values[0] = (byteValue & 128) != 0;

        return values;
    }

    public static IEnumerable<Vector3> BuildSearchGrid(Vector3 centerPosition, float gridRadius, float stepSize = KnowledgeBase.GameGridCellWidth) {
        var buildSpots = new List<Vector3>();
        for (var x = centerPosition.X - gridRadius; x <= centerPosition.X + gridRadius; x += stepSize) {
            for (var y = centerPosition.Y - gridRadius; y <= centerPosition.Y + gridRadius; y += stepSize) {
                if (!IsInitialized || IsInBounds(x, y)) {
                    buildSpots.Add(new Vector3(x, y, centerPosition.Z));
                }
            }
        }

        return buildSpots.OrderBy(position => Vector3.Distance(centerPosition, position));
    }

    public static bool IsInBounds(Vector3 position) {
        return IsInBounds(position.X, position.Y);
    }

    public static bool IsInBounds(float x, float y) {
        return x >= 0 && x < _maxX && y >= 0 && y < _maxY;
    }

    public static bool IsWalkable(Vector3 position) {
        return _walkMap[(int)position.X][(int)position.Y];
    }
}
