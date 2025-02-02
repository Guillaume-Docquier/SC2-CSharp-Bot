using System.Drawing;
using MapAnalysis.ExpandAnalysis;
using SC2Client;

namespace MapAnalysis.RegionAnalysis.Persistence;

public class RegionsDataRepository<TRegionsData> : IMapDataRepository<TRegionsData> where TRegionsData : IRegionsData {
    private const string RegionsTopic = "Regions";

    private readonly FootprintCalculator _footprintCalculator;
    private readonly IMapImageFactory _mapImageFactory;
    private readonly IMapDataRepository<TRegionsData> _jsonMapDataRepository;
    private readonly IMapFileNameFormatter _mapFileNameFormatter;

    public RegionsDataRepository(
        FootprintCalculator footprintCalculator,
        IMapImageFactory mapImageFactory,
        IMapDataRepository<TRegionsData> jsonMapDataRepository,
        IMapFileNameFormatter mapFileNameFormatter
    ) {
        _footprintCalculator = footprintCalculator;
        _mapImageFactory = mapImageFactory;
        _jsonMapDataRepository = jsonMapDataRepository;
        _mapFileNameFormatter = mapFileNameFormatter;
    }

    /// <summary>
    /// Saves the regions data as JSON, and saves a PNG image of the saved regions.
    /// </summary>
    /// <param name="regionsData">The regions data to save.</param>
    /// <param name="mapFileName"></param>
    public void Save(TRegionsData regionsData, string mapFileName) {
        _jsonMapDataRepository.Save(regionsData, FormatMapFileName(mapFileName));
        SaveAsImage(regionsData.Regions, mapFileName);
    }

    /// <summary>
    /// Loads the regions data.
    /// </summary>
    /// <returns>The loaded regions data.</returns>
    public TRegionsData Load(string mapFileName) {
        return _jsonMapDataRepository.Load(FormatMapFileName(mapFileName));
    }

    /// <summary>
    /// Saves the regions as an image where each region has a different color than its neighbors.
    /// </summary>
    /// <param name="regions">The regions to represent.</param>
    /// <param name="mapFileName">The file name of the current map.</param>
    private void SaveAsImage(List<IRegion> regions, string mapFileName) {
        var mapImage = _mapImageFactory.CreateMapImage();
        foreach (var region in regions) {
            foreach (var cell in region.Cells) {
                mapImage.SetCellColor(cell, RegionsDataColors.RegionColorsMapping[region.Color]);
            }

            if (region.ExpandLocation != null) {
                PaintExpandLocation(mapImage, region.ExpandLocation);
            }
        }

        mapImage.Save(FormatMapFileName(mapFileName));
    }

    /// <summary>
    /// Paints the expand location and its resources on the map image.
    /// </summary>
    /// <param name="mapImage">The map image to paint on.</param>
    /// <param name="expandLocation">The expand location data to paint.</param>
    private void PaintExpandLocation(IMapImage mapImage, IExpandLocation expandLocation) {
        mapImage.SetCellColor(expandLocation.OptimalTownHallPosition, RegionsDataColors.ExpandColor);

        foreach (var resource in expandLocation.Resources) {
            var resourceColor = Resources.GetResourceType(resource) switch
            {
                Resources.ResourceType.Mineral => RegionsDataColors.MineralColor,
                Resources.ResourceType.Gas => RegionsDataColors.GasColor,
                _ => Color.Black
            };

            foreach (var resourceCell in _footprintCalculator.GetFootprint(resource)) {
                mapImage.SetCellColor(resourceCell, resourceColor);
            }
        }
    }

    private string FormatMapFileName(string mapFileName) {
        return _mapFileNameFormatter.Format(RegionsTopic, mapFileName);
    }
}
