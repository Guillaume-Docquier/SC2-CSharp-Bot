using SC2APIProtocol;
using SC2Client.GameData;

namespace SC2Client;

/// <summary>
/// The ladder game connection is used to connect to an already set up ladder game against a non-computer opponent.
/// </summary>
public class LadderGameConnection : IGameConnection {
    private readonly ILogger _logger;
    private readonly ISc2Client _sc2Client;
    private readonly KnowledgeBase _knowledgeBase;
    private readonly FootprintCalculator _footprintCalculator;
    private readonly string _serverAddress;
    private readonly int _gamePort;
    private readonly int _startPort;

    /// <summary>
    /// The knowledge base will be initialized upon joining the game.
    /// </summary>
    public LadderGameConnection(
        ILogger logger,
        ISc2Client sc2Client,
        KnowledgeBase knowledgeBase,
        FootprintCalculator footprintCalculator,
        string serverAddress,
        int gamePort,
        int startPort
    ) {
        _logger = logger.CreateNamed("LadderGameConnection");
        _sc2Client = sc2Client;
        _knowledgeBase = knowledgeBase;
        _footprintCalculator = footprintCalculator;
        _serverAddress = serverAddress;
        _gamePort = gamePort;
        _startPort = startPort;
    }

    public async Task<IGame> JoinGame(Race race) {
        await _sc2Client.ConnectToGameClient(_serverAddress, _gamePort);

        _logger.Info("Joining ladder game");
        var playerId = await _sc2Client.JoinGame(RequestBuilder.RequestJoinLadderGame(race, _startPort));

        // TODO GD Need a GameFactory
        return await Game.Create(playerId, _logger, _sc2Client, _knowledgeBase, _footprintCalculator);
    }
}
