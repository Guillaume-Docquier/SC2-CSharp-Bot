using System.Drawing;
using System.Numerics;
using SC2Client;
using SC2Client.ExtensionMethods;
using SC2Client.Trackers;

namespace MapAnalysis.RegionAnalysis.Persistence;

/// <summary>
/// Creates an image that represents the map
/// </summary>
public class MapImage : IMapImage {
    private readonly ILogger _logger;
    private readonly Bitmap _image;

    public MapImage(ILogger logger, ITerrainTracker terrainTracker) {
        _logger = logger;

        if (!OperatingSystem.IsWindows()) {
            _logger.Warning("Cannot create map images on platforms other than Windows because we are using System.Drawing");
            _image = null!; // Just to avoid compiler warnings on the ctor.

            return;
        }

        _image = new Bitmap(terrainTracker.MaxX, terrainTracker.MaxY);
        for (var x = 0; x < _image.Width; x++) {
            for (var y = 0; y < _image.Height; y++) {
                var color = terrainTracker.IsWalkable(new Vector2(x, y), considerObstructions: false)
                    ? Color.White
                    : Color.Black;

                _image.SetPixel(x, y, color);
            }
        }
    }

    public void SetCellColor(Vector2 cell, Color color) {
        var adjustedCell = cell.AsWorldGridCorner();

        SetCellColor((int)adjustedCell.X, (int)adjustedCell.Y, color);
    }

    public void SetCellColor(int x, int y, Color color) {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        _image.SetPixel(x, y, color);
    }

    public void Save(string fileName, int upscalingFactor = 4) {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        var scaledImage = ScaleImage(_image, upscalingFactor);
        // SC2 (0, 0) is bottom left, but bitmap (0, 0) is top left, so we flip to end up with the correct image orientation
        scaledImage.RotateFlip(RotateFlipType.RotateNoneFlipY);

        var fileNameWithExtension = $"{fileName}.{FileExtensions.Png}";
        Directory.CreateDirectory(Path.GetDirectoryName(fileNameWithExtension)!);
        scaledImage.Save(fileNameWithExtension);
        _logger.Success($"Map image saved to {fileNameWithExtension}");
    }

    /// <summary>
    /// Scales the image so that it is bigger.
    /// This method only works on windows.
    /// </summary>
    /// <param name="originalImage">The original image.</param>
    /// <param name="scalingFactor">A scaling multiplier to indicate how much to scale the image.</param>
    /// <returns>The new, scaled, image.</returns>
    private static Bitmap ScaleImage(Bitmap originalImage, int scalingFactor) {
        if (!OperatingSystem.IsWindows()) {
            return originalImage;
        }

        var scaledWidth = originalImage.Width * scalingFactor;
        var scaledHeight = originalImage.Height * scalingFactor;
        var scaledImage = new Bitmap(scaledWidth, scaledHeight);

        for (var x = 0; x < originalImage.Width; x++) {
            for (var y = 0; y < originalImage.Height; y++) {
                for (var virtualX = 0; virtualX < scalingFactor; virtualX++) {
                    for (var virtualY = 0; virtualY < scalingFactor; virtualY++) {
                        scaledImage.SetPixel(
                            x * scalingFactor + virtualX,
                            y * scalingFactor + virtualY,
                            originalImage.GetPixel(x, y)
                        );
                    }
                }
            }
        }

        return scaledImage;
    }
}
