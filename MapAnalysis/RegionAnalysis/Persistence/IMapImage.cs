using System.Drawing;
using System.Numerics;

namespace MapAnalysis.RegionAnalysis.Persistence;

public interface IMapImage {
    /// <summary>
    /// Sets the color of the given game grid cell.
    /// </summary>
    /// <param name="cell">The cell to color.</param>
    /// <param name="color">The color to set.</param>
    void SetCellColor(Vector2 cell, Color color);

    /// <summary>
    /// Sets the color of the game grid cell at the (x, y) coordinates.
    /// </summary>
    /// <param name="x">The pixel X.</param>
    /// <param name="y">The pixel Y.</param>
    /// <param name="color">The color to set.</param>
    void SetCellColor(int x, int y, Color color);

    /// <summary>
    /// Saves the image of the map.
    /// The image can be upscaled, the base is 1 pixel per map grid cell.
    /// </summary>
    /// <param name="fileName">The name of the file to save to, without the extension.</param>
    /// <param name="upscalingFactor">The factor to upscale the image before saving. An upscaling factor of 2 would double the width and height.</param>
    void Save(string fileName, int upscalingFactor = 4);
}
