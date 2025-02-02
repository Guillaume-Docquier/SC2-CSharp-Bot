using System.Numerics;

namespace SC2Client.State;

/// <summary>
/// Provides game state data about the terrain.
/// </summary>
public interface ITerrain {
    /// <summary>
    /// The map's width
    /// </summary>
    int MaxX { get; }

    /// <summary>
    /// The map's height
    /// </summary>
    int MaxY { get; }

    /// <summary>
    /// Contains the heights of all cells in the game.
    /// The cells are expressed as their corner.
    /// </summary>
    IReadOnlyDictionary<Vector2, float> CellHeights { get; }

    /// <summary>
    /// Contains all cells that are part of the terrain.
    /// These cells can be buildable/walkable over the course of the game.
    /// This set does not change over the course of the game.
    /// The cells are expressed as their corner.
    /// </summary>
    IReadOnlySet<Vector2> Cells { get; }

    /// <summary>
    /// Contains all cells that are obstructed by neutral units that can be destroyed.
    /// The cells are expressed as their corner.
    /// </summary>
    IReadOnlySet<Vector2> ObstructedCells { get; }

    /// <summary>
    /// Contains all cells that are walkable.
    /// The cells are expressed as their corner.
    /// </summary>
    IReadOnlySet<Vector2> WalkableCells { get; }

    /// <summary>
    /// Contains all cells that are buildable.
    /// The cells are expressed as their corner.
    /// </summary>
    IReadOnlySet<Vector2> BuildableCells { get; }
}
