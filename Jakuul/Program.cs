using Jakuul;
using SC2APIProtocol;
using SC2Client;
using SC2Client.GameData;
using SC2Client.Trackers;

// CLI
var commandLineArgs = new CommandLineArguments(args);

// DI
var knowledgeBase = new KnowledgeBase();
var frameClock = new FrameClock();
var logSinks = new List<ILogSink>
{
    new FileLogSink($"Logs/{DateTime.UtcNow:yyyy-MM-dd HH.mm.ss}.log"),
    new ConsoleLogSink()
};
var logger = new Logger(logSinks, frameClock);
var sc2Client = new Sc2Client(logger, GameDisplayMode.FullScreen);
var footprintCalculator = new FootprintCalculator(knowledgeBase, logger);

IGameConnection sc2GameConnection = commandLineArgs.LadderServerAddress != null
    ? new LadderGameConnection(logger, sc2Client, knowledgeBase, footprintCalculator, commandLineArgs.LadderServerAddress, commandLineArgs.GamePort, commandLineArgs.StartPort)
    : new LocalGameConnection(logger, sc2Client, knowledgeBase, footprintCalculator, new LocalGameConfiguration());

// Play
var game = await sc2GameConnection.JoinGame(Race.Zerg); // TODO GD The bot's race will be defined by the bot
while (game.State.Result == Result.Undecided) {
    // bot.Play(game.State)
    await game.Step(stepSize: 2, new List<SC2APIProtocol.Action>()); // TODO GD Provide the actions
}

logger.Success($"Game has ended: {game.State.Result}");
