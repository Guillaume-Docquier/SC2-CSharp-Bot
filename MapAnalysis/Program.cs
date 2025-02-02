using MapAnalysis;
using SC2APIProtocol;
using SC2Client;
using SC2Client.Trackers;

var mapsToAnalyze = new List<string> { Maps.AncientCistern };

var logSinks = new List<ILogSink>
{
    new FileLogSink($"Logs/{DateTime.UtcNow:yyyy-MM-dd HH.mm.ss}.log"),
    new ConsoleLogSink()
};
var logger = new Logger(logSinks, new FrameClock()).CreateNamed("Program");

foreach (var (mapToAnalyze, mapIndex) in mapsToAnalyze.Select((map, i) => (map, i + 1))) {
    var services = ServicesFactory.CreateServices(logSinks, mapToAnalyze);

    logger.Important($"Analyzing map: {mapToAnalyze} ({mapIndex}/{mapsToAnalyze.Count})");

    var game = await services.GameConnection.JoinGame(Race.Zerg);
    while (!services.MapAnalyzer.IsAnalysisComplete) {
        foreach (var tracker in services.Trackers) {
            tracker.Update(game.State);
        }

        services.MapAnalyzer.OnFrame(game.State);

        // TODO GD This doesn't seem proper
        var debugRequest = services.GraphicalDebugger.GetDebugRequest();
        if (debugRequest != null) {
            await services.Sc2Client.SendRequest(debugRequest);
        }

        await game.Step(stepSize: 1, new List<SC2APIProtocol.Action>());
    }

    logger.Success($"Analysis on {mapToAnalyze} complete!");

    game.Quit();
}

logger.Success($"Map analysis complete! ({mapsToAnalyze.Count}/{mapsToAnalyze.Count})");
