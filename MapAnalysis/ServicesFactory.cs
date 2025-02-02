using System.Numerics;
using Algorithms.ExtensionMethods;
using MapAnalysis.ExpandAnalysis;
using MapAnalysis.RegionAnalysis;
using MapAnalysis.RegionAnalysis.ChokePoints;
using MapAnalysis.RegionAnalysis.Persistence;
using MapAnalysis.RegionAnalysis.Ramps;
using SC2Client;
using SC2Client.Debugging.GraphicalDebugging;
using SC2Client.ExtensionMethods;
using SC2Client.GameData;
using SC2Client.Services;
using SC2Client.Trackers;

namespace MapAnalysis;

public static class ServicesFactory {
    private const string DataFolder = "Data";

    public readonly struct Services {
        public ILogger Logger { get; init; }
        public IGameConnection GameConnection { get; init; }
        public ISc2Client Sc2Client { get; init; }
        public IGraphicalDebugger GraphicalDebugger { get; init; }
        public IAnalyzer MapAnalyzer { get; init; }
        public IReadOnlyList<ITracker> Trackers { get; init; }
    }

    public static Services CreateServices(List<ILogSink> logSinks, string mapFileName) {
        var knowledgeBase = new KnowledgeBase();
        var frameClock = new FrameClock();
        var logger = new Logger(logSinks, frameClock);
        var sc2Client = new Sc2Client(logger, GameDisplayMode.FullScreen);
        var footprintCalculator = new FootprintCalculator(knowledgeBase, logger);
        var gameConnection = new LocalGameConnection(logger, sc2Client, knowledgeBase, footprintCalculator, new LocalGameConfiguration(mapFileName));

        var unitsTracker = new UnitsTracker();
        var terrainTracker = new TerrainTracker(logger);
        var graphicalDebugger = new Sc2GraphicalDebugger(terrainTracker);
        var debugger = new Debugger(graphicalDebugger, terrainTracker);

        var pathfinder = new CachedPathfinder<Vector2>(
            logger,
            new PathfinderCache<Vector2>(),
            (from, to) => from.DistanceTo(to),
            cell => terrainTracker.GetReachableNeighbors(cell),
            cell => terrainTracker.GetClosestWalkable(cell).AsWorldGridCenter(),
            vector => vector.ToString()
        );

        var resourceFinder = new ResourceFinder(
            knowledgeBase,
            unitsTracker,
            terrainTracker
        );

        var expandAnalyzer = new ExpandAnalyzer(
            knowledgeBase,
            frameClock,
            logger,
            sc2Client,
            graphicalDebugger,
            terrainTracker,
            resourceFinder,
            pathfinder,
            footprintCalculator
        );

        var mapImageFactory = new MapImageFactory(
            logger,
            terrainTracker
        );

        var mapFileNameFormatter = new MapFileNameFormatter(DataFolder);
        var jsonMapDataRepository = new JsonMapDataRepository<RegionsData>(logger);

        var regionsRepository = new RegionsDataRepository<RegionsData>(
            footprintCalculator,
            mapImageFactory,
            jsonMapDataRepository,
            mapFileNameFormatter
        );

        var chokeFinder = new RayCastingChokeFinder(
            logger,
            terrainTracker,
            graphicalDebugger,
            mapImageFactory,
            mapFileNameFormatter,
            mapFileName
        );

        var rampFinder = new RampFinder(terrainTracker);

        var regionAnalyzer = new RegionAnalyzer(
            logger,
            terrainTracker,
            expandAnalyzer,
            regionsRepository,
            chokeFinder,
            rampFinder,
            mapImageFactory,
            unitsTracker,
            pathfinder,
            footprintCalculator,
            mapFileNameFormatter,
            mapFileName
        );

        // TODO GD Make it simpler to know who's a tracker and in which order to update them
        // You can do that via a base class that registers in the CTOR
        // Since trackers will be created in dependency order, they'll register in that same order and update in that same order
        var trackers = new List<ITracker> { frameClock, unitsTracker, terrainTracker, debugger };

        var analyzers = new List<IAnalyzer> { expandAnalyzer, regionAnalyzer };
        var mapAnalyzer = new MapAnalyzer(logger, analyzers);

        return new Services
        {
            Logger = logger,
            GameConnection = gameConnection,
            Sc2Client = sc2Client,
            GraphicalDebugger = graphicalDebugger,
            MapAnalyzer = mapAnalyzer,
            Trackers = trackers,
        };
    }
}
