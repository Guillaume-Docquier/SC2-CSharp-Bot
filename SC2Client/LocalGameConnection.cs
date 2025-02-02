using SC2APIProtocol;
using SC2Client.GameData;

namespace SC2Client;

/// <summary>
/// The local game connection is used to launch SC2, create and join a game against a computer opponent.
/// </summary>
public class LocalGameConnection : IGameConnection {
    private readonly ILogger _logger;
    private readonly ISc2Client _sc2Client;
    private readonly KnowledgeBase _knowledgeBase;
    private readonly FootprintCalculator _footprintCalculator;
    private readonly ILocalGameConfiguration _localGameConfiguration;

    private const string ServerAddress = "127.0.0.1";
    private const int GamePort = 5678;

    /// <summary>
    /// The knowledge base will be initialized upon joining the game.
    /// </summary>
    public LocalGameConnection(
        ILogger logger,
        ISc2Client sc2Client,
        KnowledgeBase knowledgeBase,
        FootprintCalculator footprintCalculator,
        ILocalGameConfiguration localGameConfiguration
    ) {
        _logger = logger.CreateNamed("LocalGameConnection");
        _sc2Client = sc2Client;
        _knowledgeBase = knowledgeBase;
        _footprintCalculator = footprintCalculator;
        _localGameConfiguration = localGameConfiguration;
    }

    public async Task<IGame> JoinGame(Race race) {
        _sc2Client.LaunchSc2(ServerAddress, GamePort);
        await _sc2Client.ConnectToGameClient(ServerAddress, GamePort);
        await _sc2Client.CreateLocalGame(_localGameConfiguration);

        _logger.Info("Joining local game");
        var playerId = await _sc2Client.JoinGame(RequestBuilder.RequestJoinLocalGame(race));

        // TODO GD Need a GameFactory
        return await Game.Create(playerId, _logger, _sc2Client, _knowledgeBase, _footprintCalculator);
    }
}
