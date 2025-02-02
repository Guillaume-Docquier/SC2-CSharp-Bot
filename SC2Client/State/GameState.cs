using System.Numerics;
using Algorithms.ExtensionMethods;
using SC2APIProtocol;
using SC2Client.GameData;

namespace SC2Client.State;

public class GameState : IGameState {
    private readonly Units _units;
    private readonly Terrain _terrain;

    public uint PlayerId { get; }
    public string MapName { get; }
    public uint CurrentFrame { get; private set; }
    public Result Result { get; private set; }
    public Vector2 StartingLocation { get; }
    public Vector2 EnemyStartingLocation { get; }
    public ITerrain Terrain => _terrain;
    public IUnits Units => _units;

    public GameState(
        uint playerId,
        ILogger logger,
        KnowledgeBase knowledgeBase,
        FootprintCalculator footprintCalculator,
        ResponseGameInfo gameInfo,
        Status gameStatus,
        ResponseObservation observation
    ) {
        PlayerId = playerId;
        MapName = gameInfo.MapName;
        CurrentFrame = observation.Observation.GameLoop;
        Result = GetGameResult(gameStatus, observation);
        _units = new Units(logger, knowledgeBase, observation);
        _terrain = new Terrain(footprintCalculator, gameInfo, _units);

        var startingTownHallPosition = UnitQueries.GetUnits(_units.OwnedUnits, UnitTypeId.TownHalls).First().Position.ToVector2();
        var startLocations = gameInfo.StartRaw.StartLocations
            .Select(startLocation => new Vector2(startLocation.X, startLocation.Y))
            .ToList();

        StartingLocation = startLocations.MinBy(startLocation => startLocation.DistanceTo(startingTownHallPosition));
        EnemyStartingLocation = startLocations.MaxBy(startLocation => startLocation.DistanceTo(startingTownHallPosition));
    }

    /// <summary>
    /// Updates the game state.
    /// </summary>
    /// <param name="gameStatus">The status of the game.</param>
    /// <param name="observation">The current game state observation.</param>
    public void Update(Status gameStatus, ResponseGameInfo gameInfo, ResponseObservation observation) {
        CurrentFrame = observation.Observation.GameLoop;
        Result = GetGameResult(gameStatus, observation);

        _units.Update(observation);
        _terrain.Update(gameInfo, _units);
    }

    /// <summary>
    /// Gets the game result from the response.
    /// </summary>
    /// <param name="gameStatus">The status of the game.</param>
    /// <param name="observation">The current game state observation.</param>
    /// <returns>The current game result.</returns>
    private Result GetGameResult(Status gameStatus, ResponseObservation observation) {
        switch (gameStatus) {
            case Status.Quit:
                return Result.Defeat;
            case Status.Ended:
                return observation.PlayerResult.First(result => result.PlayerId == PlayerId).Result;
            default:
                return Result.Undecided;
        }
    }
}
