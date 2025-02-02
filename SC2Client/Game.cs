using SC2APIProtocol;
using SC2Client.GameData;
using SC2Client.State;

namespace SC2Client;

public class Game : IGame {
    private readonly ILogger _logger;
    private readonly ISc2Client _sc2Client;
    private readonly KnowledgeBase _knowledgeBase;
    private readonly GameState _state;

    public IGameState State => _state;
    public bool IsOver => State.Result != Result.Undecided;

    private Game(
        ILogger logger,
        ISc2Client sc2Client,
        uint playerId,
        KnowledgeBase knowledgeBase,
        FootprintCalculator footprintCalculator,
        ResponseGameInfo gameInfo,
        Status gameStatus,
        ResponseObservation observation
    ) {
        _logger = logger.CreateNamed("Game");
        _sc2Client = sc2Client;
        _knowledgeBase = knowledgeBase;
        _state = new GameState(playerId, logger, knowledgeBase, footprintCalculator, gameInfo, gameStatus, observation);
    }

    /// <summary>
    /// Creates a Game.
    /// The knowledge base will be initialized
    /// </summary>
    /// <param name="playerId"></param>
    /// <param name="logger"></param>
    /// <param name="sc2Client"></param>
    /// <param name="knowledgeBase"></param>
    /// <param name="footprintCalculator"></param>
    /// <returns></returns>
    public static async Task<Game> Create(
        uint playerId,
        ILogger logger,
        ISc2Client sc2Client,
        KnowledgeBase knowledgeBase,
        FootprintCalculator footprintCalculator
    ) {
        // TODO GD Can I request the KB data before joining the game? Initializing it here feels dumb.
        var dataResponse = await sc2Client.SendRequest(RequestBuilder.RequestData());
        knowledgeBase.Init(dataResponse.Data);

        var gameInfoResponse = await sc2Client.SendRequest(RequestBuilder.RequestGameInfo());
        var observationResponse = await sc2Client.SendRequest(RequestBuilder.RequestObservation(0));

        return new Game(
            logger,
            sc2Client,
            playerId,
            knowledgeBase,
            footprintCalculator,
            gameInfoResponse.GameInfo,
            observationResponse.Status,
            observationResponse.Observation
        );
    }

    public async Task Step(uint stepSize, List<SC2APIProtocol.Action> actions) {
        if (actions.Count > 0) {
            var response = await _sc2Client.SendRequest(RequestBuilder.RequestAction(actions));

            // TODO GD Return this
            var unsuccessfulActions = actions
                .Zip(response.Action.Result, (action, result) => (action, result))
                .Where(action => action.result != ActionResult.Success)
                .Select(action => $"({_knowledgeBase.GetAbilityData(action.action.ActionRaw.UnitCommand.AbilityId).FriendlyName}, {action.result})")
                .ToList();

            if (unsuccessfulActions.Count > 0) {
                _logger.Warning($"Unsuccessful actions: [{string.Join("; ", unsuccessfulActions)}]");
            }
        }

        await _sc2Client.SendRequest(RequestBuilder.RequestStep(stepSize));

        // TODO GD Maybe I can request both at the same time
        var gameInfoResponse = await _sc2Client.SendRequest(RequestBuilder.RequestGameInfo());
        var observationResponse = await _sc2Client.SendRequest(RequestBuilder.RequestObservation(State.CurrentFrame + stepSize));

        _state.Update(observationResponse.Status, gameInfoResponse.GameInfo, observationResponse.Observation);
    }

    public void Quit() {
        _logger.Info("Quitting the game...");
        _sc2Client.LeaveCurrentGame().Wait();
    }
}
