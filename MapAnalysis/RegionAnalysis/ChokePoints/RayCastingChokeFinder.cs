using System.Numerics;
using Algorithms;
using Algorithms.ExtensionMethods;
using MapAnalysis.RegionAnalysis.Persistence;
using SC2APIProtocol;
using SC2Client;
using SC2Client.Debugging.GraphicalDebugging;
using SC2Client.ExtensionMethods;
using SC2Client.Trackers;

namespace MapAnalysis.RegionAnalysis.ChokePoints;

// TODO GD Considering the obstacles (resources, rocks) might be interesting at some point
public class RayCastingChokeFinder : IChokeFinder {
    private readonly ILogger _logger;
    private readonly ITerrainTracker _terrainTracker;
    private readonly IGraphicalDebugger _graphicalDebugger;
    private readonly IMapImageFactory _mapImageFactory;
    private readonly IMapFileNameFormatter _mapFileNameFormatter;
    private readonly string _mapFileName;

    private const bool DrawEnabled = true; // TODO GD Flag this

    private const float ChokeScoreCutOff = 4.4f;
    private const int StartingAngle = 0;
    private const int MaxAngle = 175;
    private const int AngleIncrement = 5;

    public RayCastingChokeFinder(
        ILogger logger,
        ITerrainTracker terrainTracker,
        IGraphicalDebugger graphicalDebugger,
        IMapImageFactory mapImageFactory,
        IMapFileNameFormatter mapFileNameFormatter,
        string mapFileName
    ) {
        _logger = logger.CreateNamed("RayCastingChokeFinder");
        _terrainTracker = terrainTracker;
        _graphicalDebugger = graphicalDebugger;
        _mapImageFactory = mapImageFactory;
        _mapFileNameFormatter = mapFileNameFormatter;
        _mapFileName = mapFileName;
    }

    public List<ChokePoint> FindChokePoints() {
        var chokePointCells = CreateChokePointCells();
        _logger.Info($"Identified {chokePointCells.Count} potential choke point cells");

        var lines = CreateVisionLines(chokePointCells.Count);
        _logger.Info($"Computed {lines.Count} total vision lines");

        var chokePoints = ComputeChokePoints(chokePointCells, lines);

        SaveChokePointsImage(chokePoints);
        SaveChokeScoresImage(chokePointCells.Values.ToList());

        return chokePoints;
    }

    /// <summary>
    /// Creates a dictionary of Vector2 -> ChokePointCell with all walkable cells in the map.
    /// The ChokePointCell will be used to compute choke scores.
    /// </summary>
    /// <returns>A mapping from a walkable Vector2 to a ChokePointCell.</returns>
    private Dictionary<Vector2, ChokePointCell> CreateChokePointCells() {
        var chokePointCells = new Dictionary<Vector2, ChokePointCell>();
        foreach (var cellCenter in _terrainTracker.Cells.Select(cell => cell.AsWorldGridCenter())) {
            chokePointCells[cellCenter] = new ChokePointCell(cellCenter);
        }

        return chokePointCells;
    }

    private List<VisionLine> CreateVisionLines(int chokePointCellsCount) {
        var allLines = new List<VisionLine>();
        for (var angle = StartingAngle; angle <= MaxAngle; angle += AngleIncrement) {
            var lines = CreateLinesAtAnAngle(angle, _terrainTracker.MaxX, _terrainTracker.MaxY);
            lines = BreakDownIntoContinuousSegments(lines);

            var nbCellsCovered = lines.SelectMany(line => line.OrderedTraversedCells).ToHashSet().Count;
            var percentageOfCellsCovered = (float)nbCellsCovered / chokePointCellsCount;
            _logger.Debug($"Created {lines.Count,4} lines at {angle,3} degrees covering {nbCellsCovered,5} cells ({percentageOfCellsCovered,3:P0})");

            allLines.AddRange(lines);
        }

        return allLines;
    }

    private List<VisionLine> CreateLinesAtAnAngle(int angleInDegrees, int maxX, int maxY) {
        var origin = new Vector2 { X = maxX / 2f, Y = maxY / 2f };

        var lineLength = (int)Math.Ceiling(Math.Sqrt(maxX * maxX + maxY * maxY));
        var paddingX = (int)(lineLength / 2f - origin.X);
        var paddingY = (int)(lineLength / 2f - origin.Y);

        var angleInRadians = MathUtils.DegToRad(angleInDegrees);

        var lines = new List<VisionLine>();
        for (var y = -paddingY; y < maxY + paddingY; y++) {
            var start = new Vector2(-paddingX, y).RotateAround(origin, angleInRadians);
            var end = new Vector2(maxX + paddingX, y).RotateAround(origin, angleInRadians);

            lines.Add(new VisionLine(_terrainTracker, start, end, angleInDegrees));
        }

        return lines;
    }

    private List<VisionLine> BreakDownIntoContinuousSegments(IEnumerable<VisionLine> lines) {
        return lines.SelectMany(BreakDownIntoContinuousSegments).ToList();
    }

    private List<VisionLine> BreakDownIntoContinuousSegments(VisionLine visionLine) {
        var lines = new List<VisionLine>();

        var cellIndex = 0;
        while (cellIndex < visionLine.OrderedTraversedCells.Count) {
            var startCellIndex = GoToStartOfNextLine(visionLine, cellIndex);
            if (startCellIndex < 0) {
                break;
            }

            var endCellIndex = GoToEndOfLine(visionLine, startCellIndex);

            var cells = visionLine.OrderedTraversedCells
                .Skip(startCellIndex)
                .Take(endCellIndex - startCellIndex + 1)
                .ToList();

            lines.Add(new VisionLine(_terrainTracker, cells, visionLine.Angle));

            cellIndex = endCellIndex + 1;
        }

        return lines;
    }

    /// <summary>
    /// Iterate over the line's cells until we meet a walkable cell.
    /// Returns the index of that cell, or -1 if not found.
    /// </summary>
    /// <param name="visionLine">The line to iterate over</param>
    /// <param name="startCellIndex">The cell index to start at</param>
    /// <returns>The index of the next walkable cell or -1 if none</returns>
    private int GoToStartOfNextLine(VisionLine visionLine, int startCellIndex) {
        var currentCellIndex = startCellIndex;
        while (currentCellIndex < visionLine.OrderedTraversedCells.Count) {
            // TODO GD Add a method like isWithinGameCells or something, to avoid forgetting .AsWorldGridCorner()
            if (_terrainTracker.Cells.Contains(visionLine.OrderedTraversedCells[currentCellIndex].AsWorldGridCorner())) {
                return currentCellIndex;
            }

            currentCellIndex++;
        }

        return -1;
    }

    /// <summary>
    /// Iterate over the line's cells until we meet an unwalkable cell.
    /// Returns the index of the last walkable cell encountered or -1 if none.
    /// </summary>
    /// <param name="visionLine">The line to iterate over</param>
    /// <param name="startCellIndex">The cell index to start at</param>
    /// <returns>The index of the last walkable cell or -1 if none</returns>
    private int GoToEndOfLine(VisionLine visionLine, int startCellIndex) {
        if (startCellIndex < 0) {
            return startCellIndex;
        }

        var currentCellIndex = startCellIndex;

        while (currentCellIndex < visionLine.OrderedTraversedCells.Count) {
            // TODO GD Add a method like isWithinGameCells or something, to avoid forgetting .AsWorldGridCorner()
            if (!_terrainTracker.Cells.Contains(visionLine.OrderedTraversedCells[currentCellIndex].AsWorldGridCorner())) {
                return currentCellIndex - 1;
            }

            currentCellIndex++;
        }

        return currentCellIndex - 1;
    }

    /// <summary>
    /// Computes the choke points given the choke point cells and the vision lines.
    /// </summary>
    /// <param name="potentialChokePointCells">All the cells that could be part of a choke point.</param>
    /// <param name="visionLines">All the vision lines.</param>
    /// <returns>All the computed choke points.</returns>
    private List<ChokePoint> ComputeChokePoints(Dictionary<Vector2, ChokePointCell> potentialChokePointCells, List<VisionLine> visionLines) {
        AddLinesToTraversedChokePointCells(potentialChokePointCells, visionLines);
        UpdateChokeScores(potentialChokePointCells);

        LogChokeScoresDistribution(potentialChokePointCells.Values.Select(chokePointCell => chokePointCell.ChokeScore).ToList());

        var chokePointCells = EliminateChokePointCellOutliers(potentialChokePointCells.Values);
        var chokeLines = chokePointCells
            .SelectMany(chokePointCell => chokePointCell.MostLikelyChokeLines)
            .GroupBy(line => line)
            .Where(group => group.Count() >= 2) // Some lines are length 0, some touch the same wall. This discards them but might also discard too many.
            .Where(group => group.Count() >= group.Key.Length * 0.5)
            .Select(group => group.Key)
            .ToList();

        DebugLines(chokeLines, Colors.Orange);

        var (lineCentersClusters, _) = Clustering.DBSCAN(chokeLines, 1.5f, 1);
        var chokePoints = new List<ChokePoint>();
        foreach (var lineCentersCluster in lineCentersClusters) {
            var clusterCenter = Clustering.GetCenter(lineCentersCluster);

            var shortestCenterLine = lineCentersCluster.MinBy(line => line.Length + line.Position.ToVector2().DistanceTo(clusterCenter) * 0.5)!;
            DebugLines(new[] { shortestCenterLine }, Colors.LimeGreen);
            chokePoints.Add(new ChokePoint(shortestCenterLine.Start, shortestCenterLine.End, _terrainTracker));
        }

        return chokePoints;
    }

    /// <summary>
    /// Takes all vision lines and adds them to the cell that they traverse.
    /// </summary>
    /// <param name="chokePointCells">The choke point cells to add the lines to.</param>
    /// <param name="visionLines">The vision lines.</param>
    private static void AddLinesToTraversedChokePointCells(IReadOnlyDictionary<Vector2, ChokePointCell> chokePointCells, List<VisionLine> visionLines) {
        foreach (var visionLine in visionLines) {
            foreach (var traversedCell in visionLine.OrderedTraversedCells) {
                chokePointCells[traversedCell].VisionLines.Add(visionLine);
            }
        }
    }

    /// <summary>
    /// Updates the choke scores of all the choke point cells.
    /// </summary>
    /// <param name="chokePointCells">The choke point cells to update the choke scores of.</param>
    private static void UpdateChokeScores(Dictionary<Vector2, ChokePointCell> chokePointCells) {
        foreach (var chokePointCell in chokePointCells.Values) {
            chokePointCell.UpdateChokeScore();
        }
    }

    /// <summary>
    /// Eliminates the choke point cell outliers and returns the list of all valid choke point cells.
    /// This is done by clustering the cells and comparing the scores within each cluster, discarding scores that are too far from the cluster average.
    /// </summary>
    /// <param name="potentialChokePointCells">The choke point cells to remove outliers from.</param>
    /// <returns>The choke point cells that can be considered part of a choke point.</returns>
    private List<ChokePointCell> EliminateChokePointCellOutliers(IEnumerable<ChokePointCell> potentialChokePointCells) {
        var initialChokePointCells = potentialChokePointCells.Where(chokePointCell => chokePointCell.ChokeScore > ChokeScoreCutOff).ToList();
        var (chokePointCellsClusters, _) = Clustering.DBSCAN(initialChokePointCells, 1.5f, 4);

        // Eliminate outliers from choke cells clusters.
        // We compute a dispersion score based on the average, median and std.
        // In low dispersion clusters, we are tolerant to outliers.
        // In high dispersion cluster, we are sensitive to outliers.
        var chokePointCells = new List<ChokePointCell>();
        foreach (var chokePointCellsCluster in chokePointCellsClusters) {
            var average = chokePointCellsCluster.Average(chokePointCell => chokePointCell.ChokeScore);
            var median = chokePointCellsCluster.OrderBy(chokePointCell => chokePointCell.ChokeScore).ToList()[chokePointCellsCluster.Count / 2].ChokeScore;
            var std = Math.Sqrt(chokePointCellsCluster.Average(chokePointCell => Math.Pow(chokePointCell.ChokeScore - average, 2)));

            // Dispersion is proportional to the relative std
            var dispersionScore = std / Math.Max(average, median);

            // Make high dispersion scores higher and small ones smaller
            dispersionScore = Math.Pow(dispersionScore + 0.5, 2) - 0.5;

            // Try to nullify the cut in low disparity clusters
            // A low cut means an inclusive cluster
            var cut = Math.Min(average, median) - (1 - dispersionScore) * std;

            DebugScores(chokePointCellsCluster, average, median, std, dispersionScore, cut);

            // Keep the cells that make the cut
            chokePointCells.AddRange(chokePointCellsCluster.Where(chokePointCell => chokePointCell.ChokeScore >= cut));
        }

        return chokePointCells;
    }

    /// <summary>
    /// Saves an image representing the choke points as green lines.
    /// </summary>
    /// <param name="chokePoints">All the choke points.</param>
    private void SaveChokePointsImage(IEnumerable<ChokePoint> chokePoints) {
        var mapImage = _mapImageFactory.CreateMapImage();

        var chokePointCells = chokePoints.SelectMany(chokePoint => chokePoint.Edge);
        foreach (var chokePointCell in chokePointCells) {
            mapImage.SetCellColor(chokePointCell, System.Drawing.Color.Lime);
        }

        mapImage.Save(_mapFileNameFormatter.Format("ChokePoints", _mapFileName));
    }

    /// <summary>
    /// Saves a png image representing the choke scores.
    /// High scores will be red, low scores will be grey.
    /// Only the choke scores within choke points are shown.
    /// </summary>
    /// <param name="chokePointCells">All the cells to display the choke scores of.</param>
    private void SaveChokeScoresImage(List<ChokePointCell> chokePointCells) {
        var mapImage = _mapImageFactory.CreateMapImage();
        var minChokeScore = chokePointCells.Min(chokePointCell => chokePointCell.ChokeScore);
        var maxChokeScore = chokePointCells.Max(chokePointCell => chokePointCell.ChokeScore);

        foreach (var chokePointCell in chokePointCells) {
            var gradientStrength = MathUtils.LogScale(chokePointCell.ChokeScore, minChokeScore, maxChokeScore);
            var textColor = System.Drawing.Color.White.Gradient(System.Drawing.Color.DarkRed, gradientStrength);
            mapImage.SetCellColor(chokePointCell.Position.ToVector2(), textColor);
        }

        mapImage.Save(_mapFileNameFormatter.Format("ChokeScores", _mapFileName));
    }

    /// <summary>
    /// Displays the choke scores using the graphical debugger using a gradient from grey (low score) to red (high score).
    /// Cells that are under the given cutScore will be blue (not part of their choke cluster).
    /// Additionally, the provided stats about the cells are displayed.
    /// </summary>
    /// <param name="chokePointCells">The cells to display the scores of.</param>
    /// <param name="average">The average choke score of the cells.</param>
    /// <param name="median">The median choke score of the cells.</param>
    /// <param name="std">The standard deviation of the choke score of the cells.</param>
    /// <param name="dispersionScore">A score representing the dispersion of scores of the cells.</param>
    /// <param name="cutScore">The minimum score to reach to be considered part of a choke point.</param>
    private void DebugScores(List<ChokePointCell> chokePointCells, double average, double median, double std, double dispersionScore, double cutScore) {
        if (!DrawEnabled) {
            return;
        }

        var textGroup = new []
        {
            $"Avg: {average,4:F2}",
            $"Med: {median,4:F2}",
            $"Std: {std,4:F2}",
            $"Dsp: {dispersionScore,4:F2}",
            $"Cut: {cutScore,4:F2}",
        };
        var chokePointCellsClusterCenter = _terrainTracker.WithWorldHeight(Clustering.GetCenter(chokePointCells));
        _graphicalDebugger.AddTextGroup(textGroup, worldPos: chokePointCellsClusterCenter.ToPoint(zOffset: 5));
        _graphicalDebugger.AddLink(chokePointCellsClusterCenter, chokePointCellsClusterCenter.Translate(zTranslation: 5), Colors.SunbrightOrange);

        var minScore = chokePointCells.Min(chokePointCell => chokePointCell.ChokeScore);
        var maxScore = chokePointCells.Max(chokePointCell => chokePointCell.ChokeScore);

        foreach (var chokePointCell in chokePointCells) {
            var textColor = Colors.Gradient(Colors.DarkGrey, Colors.DarkRed, MathUtils.LogScale(chokePointCell.ChokeScore, minScore, maxScore));
            if (chokePointCell.ChokeScore < cutScore) {
                textColor = Colors.Blue;
            }

            _graphicalDebugger.AddText($"{chokePointCell.ChokeScore:F1}", worldPos: _terrainTracker.WithWorldHeight(chokePointCell.Position.ToVector2()).ToPoint(), color: textColor, size: 13);
        }
    }

    /// <summary>
    /// Displays the given VisionLines with the given color using the graphical debugger.
    /// </summary>
    /// <param name="lines">The lines to display.</param>
    /// <param name="color">The color to use.</param>
    private void DebugLines(IEnumerable<VisionLine> lines, Color color) {
        if (!DrawEnabled) {
            return;
        }

        foreach (var line in lines) {
            _graphicalDebugger.AddLink(
                _terrainTracker.WithWorldHeight(line.Start, zOffset: 0.5f),
                _terrainTracker.WithWorldHeight(line.End, zOffset: 0.5f),
                color
            );
        }
    }

    /// <summary>
    /// Logs the choke scores distribution to the console.
    /// The scores are grouped by their rounded value.
    /// </summary>
    /// <param name="chokeScores">The choke scores</param>
    private void LogChokeScoresDistribution(List<float> chokeScores) {
        var minScore = (int)chokeScores.Min();
        var maxScore = (int)chokeScores.Max();

        var chokeScoresDistribution = new Dictionary<int, int>();
        for (var chokeScore = minScore; chokeScore <= maxScore; chokeScore++) {
            chokeScoresDistribution[chokeScore] = 0;
        }

        foreach (var chokeScore in chokeScores) {
            chokeScoresDistribution[(int)chokeScore] += 1;
        }

        _logger.Info("Choke score distribution:");
        var cumulativeCount = 0;
        for (var chokeScore = minScore; chokeScore <= maxScore; chokeScore++) {
            if (chokeScoresDistribution[chokeScore] <= 0) {
                continue;
            }

            var count = chokeScoresDistribution[chokeScore];
            cumulativeCount += count;

            var percentage = (float)count / chokeScores.Count;
            var cumulativePercentage = (float)cumulativeCount / chokeScores.Count;
            _logger.Debug($"{chokeScore,3}: {count,4} ({percentage,6:P2}) [{cumulativePercentage,6:P2}]");
        }
    }
}
