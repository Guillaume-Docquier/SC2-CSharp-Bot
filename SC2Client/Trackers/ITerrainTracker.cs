using System.Numerics;

namespace SC2Client.Trackers;

public interface ITerrainTracker {
    /// <summary>
    /// The map's width
    /// </summary>
    int MaxX { get; }

    /// <summary>
    /// The map's height
    /// </summary>
    int MaxY { get; }

    /// <summary>
    /// The set of all Cells that can be played on.
    /// This is the sum of all walkable Cells and obstructed Cells.
    /// The cells are expressed as their corner.
    /// </summary>
    IReadOnlySet<Vector2> Cells { get; }

    /// <summary>
    /// The set of all cells that are currently obstructed.
    /// The cells are expressed as their corner.
    /// </summary>
    IEnumerable<Vector2> ObstructedCells { get; }

    /// <summary>
    /// Adds the terrain height to the given position.
    /// Cells in SC2 are 1x1. Any Vector2 within the same 1x1 position will have the same height.
    /// </summary>
    /// <param name="position">The position to query.</param>
    /// <param name="zOffset">An offset to apply to the world height.</param>
    /// <returns></returns>
    Vector3 WithWorldHeight(Vector2 position, float zOffset = 0);

    /// <summary>
    /// Determines whether a position can be walked on by a ground unit.
    /// Cells in SC2 are 1x1. Any Vector2 within the same 1x1 position will yield the same result.
    /// The SC2 API data does not have the same resolution as the true in game data. Some cells can be considered unwalkable, but units can still walk on parts of that position.
    /// As far as I know, walkable cells are always walkable.
    /// </summary>
    /// <param name="position">The position to query.</param>
    /// <param name="considerObstructions">Whether to consider obstructions. Obstructed cells are not walkable.</param>
    /// <returns>True if the position can be walked on.</returns>
    bool IsWalkable(Vector2 position, bool considerObstructions = true);

    /// <summary>
    /// Determines whether a position can be built on.
    /// Cells in SC2 are 1x1. Any Vector2 within the same 1x1 position will yield the same result.
    /// </summary>
    /// <param name="position">The position to query.</param>
    /// <param name="considerObstructions">Whether to consider obstructions. Obstructed cells are not buildable.</param>
    /// <returns>True if the position can be built on.</returns>
    bool IsBuildable(Vector2 position, bool considerObstructions = true);

    /// <summary>
    /// Determines whether a position is obstructed by destructible neutral units.
    /// This will not consider ally or enemy buildings, only neutral units (rocks, minerals, etc) that can be destroyed.
    /// Cells in SC2 are 1x1. Any Vector2 within the same 1x1 position will yield the same result.
    /// </summary>
    /// <param name="position">The position to query.</param>
    /// <returns>True if the position is obstructed.</returns>
    bool IsObstructed(Vector2 position);

    /// <summary>
    /// Determines whether the given position is within the bounds of the game.
    /// </summary>
    /// <returns></returns>
    bool IsWithinBounds(Vector2 position);

    /// <summary>
    /// Gets all the cell neighbors that can be reached.
    /// </summary>
    /// <param name="cell">The cell to get the neighbors of.</param>
    /// <param name="potentialNeighbors">The set of allowed potential neighbors. If null, all neighbors will be considered allowed.</param>
    /// <param name="considerObstructions">Whether to consider the obstructions from obstacles.</param>
    /// <returns></returns>
    IEnumerable<Vector2> GetReachableNeighbors(Vector2 cell, IReadOnlySet<Vector2>? potentialNeighbors = null, bool considerObstructions = true);

    /// <summary>
    /// Gets the closest walkable Cell around.
    /// </summary>
    /// <param name="position">Can be any Position, will be converted to a Cell.</param>
    /// <param name="searchRadius">The radius within which to perform the search.</param>
    /// <param name="allowedCells">A set of Cells to restrict the search.</param>
    /// <returns>The closest Cell that is walkable.</returns>
    Vector2 GetClosestWalkable(Vector2 position, int searchRadius = 8, HashSet<Vector2>? allowedCells = null);
}
