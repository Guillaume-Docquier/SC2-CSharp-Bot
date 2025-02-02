using SC2Client.Debugging.GraphicalDebugging;
using SC2Client.ExtensionMethods;
using SC2Client.State;
using SC2Client.Trackers;

namespace MapAnalysis;

public class Debugger : ITracker {
    private readonly IGraphicalDebugger _graphicalDebugger;
    private readonly ITerrainTracker _terrainTracker;

    public Debugger(IGraphicalDebugger graphicalDebugger, ITerrainTracker terrainTracker) {
        _graphicalDebugger = graphicalDebugger;
        _terrainTracker = terrainTracker;
    }

    public void Update(IGameState gameState) {
        // ShowUnwalkableCells();
        // ShowAllCells();
    }

    private void ShowUnwalkableCells() {
        foreach (var cell in _terrainTracker.Cells) {
            if (!_terrainTracker.IsWalkable(cell, considerObstructions: false)) {
                _graphicalDebugger.AddGridSquare(_terrainTracker.WithWorldHeight(cell.AsWorldGridCenter()), Colors.Red);
            }
        }
    }

    private void ShowAllCells() {
        foreach (var cell in _terrainTracker.Cells) {
            _graphicalDebugger.AddGridSquare(_terrainTracker.WithWorldHeight(cell.AsWorldGridCenter()), Colors.Green);
        }
    }
}
