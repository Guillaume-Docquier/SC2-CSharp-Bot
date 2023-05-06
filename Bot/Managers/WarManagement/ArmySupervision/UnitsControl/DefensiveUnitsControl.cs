﻿using System.Collections.Generic;
using Bot.Debugging.GraphicalDebugging;
using Bot.GameSense;
using Bot.GameSense.RegionsEvaluationsTracking;

namespace Bot.Managers.WarManagement.ArmySupervision.UnitsControl;

public class DefensiveUnitsControl : AggregateUnitsControl {
    public DefensiveUnitsControl(
        IUnitsTracker unitsTracker,
        ITerrainTracker terrainTracker,
        IRegionsTracker regionsTracker,
        IRegionsEvaluationsTracker regionsEvaluationsTracker,
        IGraphicalDebugger graphicalDebugger
    ) : base(new List<IUnitsControl>
        {
            new BurrowHealing(unitsTracker, terrainTracker, regionsTracker, regionsEvaluationsTracker),
            new DisengagementKiting(unitsTracker, graphicalDebugger),
        }) {}
}
