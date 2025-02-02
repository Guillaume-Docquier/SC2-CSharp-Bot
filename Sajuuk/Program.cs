using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sajuuk.Actions;
using Sajuuk.Algorithms;
using Sajuuk.Builds;
using Sajuuk.Builds.BuildOrders;
using Sajuuk.Builds.BuildRequests;
using Sajuuk.Debugging;
using Sajuuk.Debugging.GraphicalDebugging;
using Sajuuk.GameData;
using Sajuuk.GameSense;
using Sajuuk.GameSense.EnemyStrategyTracking;
using Sajuuk.GameSense.EnemyStrategyTracking.StrategyInterpretation;
using Sajuuk.GameSense.RegionsEvaluationsTracking;
using Sajuuk.GameSense.RegionsEvaluationsTracking.RegionsEvaluations;
using Sajuuk.Managers;
using Sajuuk.Managers.EconomyManagement;
using Sajuuk.Managers.ScoutManagement;
using Sajuuk.Managers.ScoutManagement.ScoutingStrategies;
using Sajuuk.Managers.ScoutManagement.ScoutingTasks;
using Sajuuk.Managers.WarManagement;
using Sajuuk.Managers.WarManagement.ArmySupervision;
using Sajuuk.Managers.WarManagement.ArmySupervision.RegionalArmySupervision;
using Sajuuk.Managers.WarManagement.ArmySupervision.UnitsControl;
using Sajuuk.Managers.WarManagement.ArmySupervision.UnitsControl.SneakAttackUnitsControl;
using Sajuuk.Managers.WarManagement.States;
using Sajuuk.MapAnalysis;
using Sajuuk.MapAnalysis.ExpandAnalysis;
using Sajuuk.MapAnalysis.RegionAnalysis;
using Sajuuk.MapAnalysis.RegionAnalysis.ChokePoints;
using Sajuuk.MapAnalysis.RegionAnalysis.Ramps;
using Sajuuk.Persistence;
using Sajuuk.Scenarios;
using Sajuuk.Tagging;
using Sajuuk.UnitModules;
using Sajuuk.VideoClips;
using Sajuuk.VideoClips.Manim.Animations;
using Sajuuk.Wrapper;
using SC2APIProtocol;

namespace Sajuuk;

[ExcludeFromCodeCoverage]
public class Program {
    private static readonly List<IScenario> Scenarios = new List<IScenario>
    {
        //new WorkerRushScenario(MapAnalyzer.Instance),
        //new FlyingTerranScumScenario(MapAnalyzer.Instance),
        //new SpawnStuffScenario(UnitsTracker.Instance, MapAnalyzer.Instance),
    };

    private const string Version = "5_0_0";

    private const string MapFileName = Maps.CosmicSapphire;
    private const Race OpponentRace = Race.Random;
    private const Difficulty OpponentDifficulty = Difficulty.CheatInsane;

    private const bool RealTime = false;

    public static bool DebugEnabled { get; private set; }

    public static void Main(string[] args) {
        try {
            switch (args.Length) {
                case 1 when args[0] == "--mapAnalysis":
                    PlayMapAnalysis(
                        new List<string> { Maps.AncientCistern }
                    );
                    break;
                case 1 when args[0] == "--videoClip":
                    PlayVideoClip(MapFileName);
                    break;
                case 0:
                    PlayLocalGame(MapFileName);
                    break;
                default:
                    PlayLadderGame(args);
                    break;
            }
        }
        catch (Exception ex) {
            Logger.Error(ex.ToString());
        }

        Logger.Info("Terminated.");
    }

    private static void PlayMapAnalysis(params string[] mapFileNames) {
        PlayMapAnalysis(mapFileNames.ToList());
    }

    private static void PlayMapAnalysis(List<string> mapFileNames) {
        Logger.Info($"Game launched in map analysis mode ({mapFileNames.Count} maps to analyze)");
        DebugEnabled = true;

        var mapIndex = 1;
        foreach (var mapFileName in mapFileNames) {
            var services = CreateServices(mapFileName, graphicalDebugging: true, dataGeneration: true);

            var game = new LocalGame(
                services.Sc2Client,
                services.RequestBuilder,
                mapFileName,
                Race.Terran,
                Difficulty.VeryEasy,
                realTime: false
            );

            var botRunner = new MapAnalysisBotRunner(
                game,
                services.Sc2Client,
                services.KnowledgeBase,
                services.RequestBuilder,
                services.GraphicalDebugger,
                services.Controller
            );

            Logger.Important($"Analyzing map: {mapFileName} ({mapIndex}/{mapFileNames.Count})");
            botRunner.RunBot(
                new MapAnalysisBot(
                    services.FrameClock,
                    services.TerrainTracker,
                    services.GraphicalDebugger,
                    services.ExpandAnalyzer,
                    services.RegionAnalyzer,
                    services.Sc2Client
                )
            ).Wait();

            mapIndex++;
        }
    }

    private static void PlayVideoClip(string mapFileName) {
        DebugEnabled = true;

        var services = CreateServices(mapFileName, graphicalDebugging: true);

        var game = new LocalGame(
            services.Sc2Client,
            services.RequestBuilder,
            mapFileName,
            Race.Terran,
            Difficulty.VeryEasy,
            realTime: true
        );

        var botRunner = new BotRunner(
            services.Sc2Client,
            game,
            services.RequestBuilder,
            services.KnowledgeBase,
            services.FrameClock,
            services.Controller,
            services.ActionService,
            services.GraphicalDebugger,
            services.UnitsTracker,
            services.Pathfinder,
            stepSize: 1
        );

        var videoClipBot = new VideoClipBot(
            services.DebuggingFlagsTracker,
            services.UnitsTracker,
            services.TerrainTracker,
            services.FrameClock,
            services.RequestBuilder,
            services.Sc2Client,
            new AnimationFactory(services.TerrainTracker, services.GraphicalDebugger, services.Controller, services.RequestBuilder, services.Sc2Client),
            mapFileName
        );

        Logger.Info("Game launched in video clip mode");
        botRunner.RunBot(videoClipBot).Wait();
    }

    private static void PlayLocalGame(string mapFileName) {
        DebugEnabled = true;

        var services = CreateServices(mapFileName, graphicalDebugging: true);

        var game = new LocalGame(
            services.Sc2Client,
            services.RequestBuilder,
            mapFileName,
            OpponentRace,
            OpponentDifficulty,
            RealTime
        );

        var botRunner = new BotRunner(
            services.Sc2Client,
            game,
            services.RequestBuilder,
            services.KnowledgeBase,
            services.FrameClock,
            services.Controller,
            services.ActionService,
            services.GraphicalDebugger,
            services.UnitsTracker,
            services.Pathfinder,
            stepSize: 2
        );

        var sajuuk = CreateSajuuk(services, Version, Scenarios);

        Logger.Info("Game launched in local play mode");
        botRunner.RunBot(sajuuk).Wait();
    }

    private static void PlayLadderGame(string[] args) {
        DebugEnabled = false;

        var services = CreateServices(null, graphicalDebugging: false);

        var commandLineArgs = new CommandLineArguments(args);
        var game = new LadderGame(
            services.Sc2Client,
            services.RequestBuilder,
            commandLineArgs.LadderServer,
            commandLineArgs.GamePort,
            commandLineArgs.StartPort
        );

        var botRunner = new BotRunner(
            services.Sc2Client,
            game,
            services.RequestBuilder,
            services.KnowledgeBase,
            services.FrameClock,
            services.Controller,
            services.ActionService,
            services.GraphicalDebugger,
            services.UnitsTracker,
            services.Pathfinder,
            stepSize: 2
        );

        var sajuuk = CreateSajuuk(services, Version, Scenarios);

        Logger.Info("Game launched in ladder play mode");
        botRunner.RunBot(sajuuk).Wait();
    }

    private static IBot CreateSajuuk(Services services, string version, List<IScenario> scenarios) {
        var buildRequestFactory = new BuildRequestFactory(
            services.KnowledgeBase,
            services.Controller,
            services.UnitsTracker
        );

        var buildOrderFactory = new BuildOrderFactory(
            services.UnitsTracker,
            buildRequestFactory
        );

        var economySupervisorFactory = new EconomySupervisorFactory(
            services.UnitsTracker,
            buildRequestFactory,
            services.GraphicalDebugger,
            services.FrameClock,
            services.UnitModuleInstaller
        );

        var scoutSupervisorFactory = new ScoutSupervisorFactory(
            services.UnitsTracker
        );

        var sneakAttackStateFactory = new SneakAttackStateFactory(
            services.UnitsTracker,
            services.TerrainTracker,
            services.FrameClock,
            services.DetectionTracker,
            services.Clustering,
            services.UnitEvaluator
        );

        var unitsControlFactory = new UnitsControlFactory(
            services.UnitsTracker,
            services.TerrainTracker,
            services.GraphicalDebugger,
            services.RegionsTracker,
            services.RegionsEvaluationsTracker,
            services.FrameClock,
            services.Controller,
            services.DetectionTracker,
            services.UnitEvaluator,
            services.Clustering,
            sneakAttackStateFactory
        );

        var armySupervisorStateFactory = new ArmySupervisorStateFactory(
            services.VisibilityTracker,
            services.UnitsTracker,
            services.TerrainTracker,
            services.RegionsTracker,
            services.RegionsEvaluationsTracker,
            services.GraphicalDebugger,
            unitsControlFactory,
            services.FrameClock,
            services.Controller,
            services.UnitEvaluator,
            services.Pathfinder
        );

        var regionalArmySupervisorStateFactory = new RegionalArmySupervisorStateFactory(
            services.RegionsTracker,
            services.RegionsEvaluationsTracker,
            unitsControlFactory,
            services.UnitEvaluator,
            services.Pathfinder
        );

        var warSupervisorFactory = new WarSupervisorFactory(
            services.UnitsTracker,
            services.GraphicalDebugger,
            armySupervisorStateFactory,
            unitsControlFactory,
            regionalArmySupervisorStateFactory,
            services.Clustering,
            services.UnitEvaluator
        );

        var scoutingTaskFactory = new ScoutingTaskFactory(
            services.VisibilityTracker,
            services.UnitsTracker,
            services.TerrainTracker,
            services.GraphicalDebugger,
            services.KnowledgeBase,
            services.FrameClock
        );

        var warManagerBehaviourFactory = new WarManagerBehaviourFactory(
            services.TaggingService,
            services.DebuggingFlagsTracker,
            services.UnitsTracker,
            services.RegionsTracker,
            services.RegionsEvaluationsTracker,
            services.VisibilityTracker,
            services.TerrainTracker,
            services.EnemyRaceTracker,
            scoutSupervisorFactory,
            warSupervisorFactory,
            buildRequestFactory,
            services.GraphicalDebugger,
            scoutingTaskFactory,
            services.TechTree,
            services.Controller,
            services.FrameClock,
            services.UnitEvaluator,
            services.Pathfinder,
            services.UnitModuleInstaller
        );

        var warManagerStateFactory = new WarManagerStateFactory(
            services.UnitsTracker,
            services.TerrainTracker,
            warManagerBehaviourFactory,
            services.FrameClock,
            services.UnitEvaluator
        );

        var scoutingStrategyFactory = new ScoutingStrategyFactory(
            services.RegionsTracker,
            scoutingTaskFactory,
            services.UnitsTracker,
            services.EnemyRaceTracker,
            services.FrameClock
        );

        var managerFactory = new ManagerFactory(
            services.TaggingService,
            services.EnemyStrategyTracker,
            services.UnitsTracker,
            services.EnemyRaceTracker,
            services.TerrainTracker,
            economySupervisorFactory,
            scoutSupervisorFactory,
            warManagerStateFactory,
            buildRequestFactory,
            services.GraphicalDebugger,
            scoutingStrategyFactory,
            services.Controller,
            services.FrameClock,
            services.KnowledgeBase,
            services.SpendingTracker,
            services.UnitModuleInstaller
        );

        var botDebugger = new BotDebugger(
            services.VisibilityTracker,
            services.DebuggingFlagsTracker,
            services.UnitsTracker,
            services.IncomeTracker,
            services.TerrainTracker,
            services.EnemyStrategyTracker,
            services.EnemyRaceTracker,
            services.GraphicalDebugger,
            services.Controller,
            services.KnowledgeBase,
            services.SpendingTracker
        );

        return new SajuukBot(
            version,
            scenarios,
            services.TaggingService,
            services.UnitsTracker,
            services.TerrainTracker,
            managerFactory,
            buildRequestFactory,
            buildOrderFactory,
            botDebugger,
            services.FrameClock,
            services.Controller,
            services.SpendingTracker,
            services.ChatService,
            services.BuildRequestFulfiller
        );
    }

    private static Services CreateServices(string mapFileName, bool graphicalDebugging, bool dataGeneration = false) {
        var knowledgeBase = new KnowledgeBase();
        var requestBuilder = new RequestBuilder(knowledgeBase);
        var sc2Client = new Sc2Client(requestBuilder);

        var frameClock = new FrameClock();
        var visibilityTracker = new VisibilityTracker(frameClock);

        var actionBuilder = new ActionBuilder(knowledgeBase);
        var actionService = new ActionService();
        var unitsTracker = new UnitsTracker(visibilityTracker);

        var prerequisiteFactory = new PrerequisiteFactory(unitsTracker);
        var techTree = new TechTree(prerequisiteFactory);

        var terrainTracker = new TerrainTracker(visibilityTracker, unitsTracker, knowledgeBase);

        IGraphicalDebugger graphicalDebugger = graphicalDebugging ? new Sc2GraphicalDebugger(terrainTracker, requestBuilder) : new NullGraphicalDebugger();

        var pathfinder = new Pathfinder(terrainTracker, graphicalDebugger);
        var clustering = new Clustering(terrainTracker, graphicalDebugger);

        var buildingTracker = new BuildingTracker(unitsTracker, terrainTracker, knowledgeBase, graphicalDebugger, requestBuilder, sc2Client);

        var chatTracker = new ChatTracker();
        var debuggingFlagsTracker = new DebuggingFlagsTracker(chatTracker);
        var mapImageFactory = new MapImageFactory(terrainTracker);
        var regionsDataRepository = new RegionsDataRepository(terrainTracker, clustering, pathfinder, new FootprintCalculator(terrainTracker), mapImageFactory, unitsTracker);
        var expandUnitsAnalyzer = new ExpandUnitsAnalyzer(unitsTracker, terrainTracker, knowledgeBase, clustering);
        var regionsTracker = new RegionsTracker(terrainTracker, debuggingFlagsTracker, unitsTracker, regionsDataRepository, expandUnitsAnalyzer, graphicalDebugger);

        var creepTracker = new CreepTracker(visibilityTracker, unitsTracker, terrainTracker, frameClock, graphicalDebugger);
        var unitEvaluator = new UnitEvaluator(regionsTracker);
        var regionsEvaluatorFactory = new RegionsEvaluatorFactory(unitsTracker, frameClock, unitEvaluator, pathfinder);
        var regionsEvaluationsTracker = new RegionsEvaluationsTracker(debuggingFlagsTracker, terrainTracker, regionsTracker, graphicalDebugger, regionsEvaluatorFactory);

        var chatService = new ChatService(actionService, actionBuilder);
        var taggingService = new TaggingService(frameClock, chatService);
        var enemyRaceTracker = new EnemyRaceTracker(taggingService, unitsTracker);
        var strategyInterpreterFactory = new StrategyInterpreterFactory(frameClock, knowledgeBase, enemyRaceTracker, regionsTracker);
        var enemyStrategyTracker = new EnemyStrategyTracker(taggingService, unitsTracker, strategyInterpreterFactory);

        var incomeTracker = new IncomeTracker(taggingService, unitsTracker, frameClock);

        var expandAnalyzer = new ExpandAnalyzer(terrainTracker, buildingTracker, expandUnitsAnalyzer, frameClock, graphicalDebugger, clustering, pathfinder);

        var chokeFinder = new RayCastingChokeFinder(terrainTracker, graphicalDebugger, clustering, mapImageFactory, mapFileName);
        var rampFinder = new RampFinder(terrainTracker, clustering);
        var regionFactory = new RegionFactory(terrainTracker, clustering, pathfinder, unitsTracker);
        var regionAnalyzer = new RegionAnalyzer(terrainTracker, expandAnalyzer, clustering, regionsDataRepository, chokeFinder, rampFinder, regionFactory, mapImageFactory, unitsTracker, new FootprintCalculator(terrainTracker), mapFileName);

        var spendingTracker = new SpendingTracker(incomeTracker, knowledgeBase);

        // TODO GD Compute update order. For now they are in declaration order (which is fine, but prone to errors)
        // TODO GD It's whack but that'll do until we get multiple controllers and game connections
        var trackers = dataGeneration
            ? new List<INeedUpdating>
            {
                frameClock,
                actionService,
                visibilityTracker,
                unitsTracker,
                terrainTracker,
                buildingTracker,
                expandAnalyzer,
                regionAnalyzer,
            }
            : new List<INeedUpdating>
            {
                frameClock,
                actionService,
                visibilityTracker,
                unitsTracker,
                terrainTracker,
                buildingTracker,
                chatTracker,
                debuggingFlagsTracker,
                regionsTracker,
                creepTracker,
                regionsEvaluationsTracker,
                enemyRaceTracker,
                enemyStrategyTracker,
                incomeTracker,
            };

        var controller = new Controller(
            unitsTracker,
            regionsTracker,
            techTree,
            knowledgeBase,
            chatService,
            trackers
        );

        var detectionTracker = new DetectionTracker(unitsTracker, controller, knowledgeBase);
        var unitModuleInstaller = new UnitModuleInstaller(unitsTracker, graphicalDebugger, buildingTracker, regionsTracker, creepTracker, pathfinder, visibilityTracker, terrainTracker, frameClock);

        var buildRequestFulfiller = new BuildRequestFulfiller(techTree, knowledgeBase, unitsTracker, buildingTracker, pathfinder, terrainTracker, controller, regionsTracker);

        // We do this to avoid circular dependencies between unit, unitsTracker, terrainTracker and regionsTracker
        // I don't 100% like it but it seems worth it.
        var unitsFactory = new UnitFactory(frameClock, knowledgeBase, actionBuilder, actionService, terrainTracker, regionsTracker, unitsTracker);
        unitsTracker.WithUnitsFactory(unitsFactory);

        // Logger is not yet an instance
        Logger.SetFrameClock(frameClock);

        return new Services
        {
            FrameClock = frameClock,
            VisibilityTracker = visibilityTracker,
            UnitsTracker = unitsTracker,
            TechTree = techTree,
            KnowledgeBase = knowledgeBase,
            TerrainTracker= terrainTracker,
            BuildingTracker = buildingTracker,
            ChatTracker = chatTracker,
            DebuggingFlagsTracker = debuggingFlagsTracker,
            RegionsTracker = regionsTracker,
            CreepTracker = creepTracker,
            GraphicalDebugger = graphicalDebugger,
            RegionsEvaluationsTracker = regionsEvaluationsTracker,
            TaggingService = taggingService,
            EnemyRaceTracker = enemyRaceTracker,
            EnemyStrategyTracker = enemyStrategyTracker,
            IncomeTracker = incomeTracker,
            RequestBuilder = requestBuilder,
            Controller = controller,
            SpendingTracker = spendingTracker,
            Clustering = clustering,
            Pathfinder = pathfinder,
            UnitEvaluator = unitEvaluator,
            DetectionTracker = detectionTracker,
            ChatService = chatService,
            ActionService = actionService,
            Sc2Client = sc2Client,
            UnitModuleInstaller = unitModuleInstaller,
            BuildRequestFulfiller = buildRequestFulfiller,
            ExpandAnalyzer = expandAnalyzer, // TODO GD These should not be here when not running in analysis mode, needs a different BotRunner implementation
            RegionAnalyzer = regionAnalyzer, // TODO GD These should not be here when not running in analysis mode, needs a different BotRunner implementation
        };
    }

    private readonly struct Services {
        public IFrameClock FrameClock { get; init; }
        public IVisibilityTracker VisibilityTracker { get; init; }
        public IUnitsTracker UnitsTracker { get; init; }
        public TechTree TechTree { get; init; } // TODO GD Needs interface
        public KnowledgeBase KnowledgeBase { get; init; } // TODO GD Needs interface
        public ITerrainTracker TerrainTracker { get; init; }
        public IBuildingTracker BuildingTracker { get; init; }
        public IChatTracker ChatTracker { get; init; }
        public IDebuggingFlagsTracker DebuggingFlagsTracker { get; init; }
        public IRegionsTracker RegionsTracker { get; init; }
        public ICreepTracker CreepTracker { get; init; }
        public IGraphicalDebugger GraphicalDebugger { get; init; }
        public IRegionsEvaluationsTracker RegionsEvaluationsTracker { get; init; }
        public ITaggingService TaggingService { get; init; }
        public IEnemyRaceTracker EnemyRaceTracker { get; init; }
        public IEnemyStrategyTracker EnemyStrategyTracker { get; init; }
        public IIncomeTracker IncomeTracker { get; init; }
        public IRequestBuilder RequestBuilder { get; init; }
        public IController Controller { get; init; }
        public ISpendingTracker SpendingTracker { get; init; }
        public IClustering Clustering { get; init; }
        public IPathfinder Pathfinder { get; init; }
        public IUnitEvaluator UnitEvaluator { get; init; }
        public IDetectionTracker DetectionTracker { get; init; }
        public IChatService ChatService { get; init; }
        public IActionService ActionService { get; init; }
        public ISc2Client Sc2Client { get; init; }
        public IUnitModuleInstaller UnitModuleInstaller { get; init; }
        public IBuildRequestFulfiller BuildRequestFulfiller { get; init; }

        public IExpandAnalyzer ExpandAnalyzer { get; init; }
        public IRegionAnalyzer RegionAnalyzer { get; init; }
    }
}
