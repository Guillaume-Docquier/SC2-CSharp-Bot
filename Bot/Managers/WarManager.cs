﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bot.GameData;
using Bot.Wrapper;

namespace Bot.Managers;

public class WarManager: IManager {
    private const int GuardDistance = 8;
    private const int GuardRadius = 8;
    private const int AttackRadius = 999; // Basically the whole map
    private const int SupplyRequiredBeforeAttacking = 18;

    private readonly BattleManager _battleManager;
    private Unit _townHallToDefend;

    private readonly List<BuildOrders.BuildStep> _buildStepRequests = new List<BuildOrders.BuildStep>();
    public IEnumerable<BuildOrders.BuildStep> BuildStepRequests => _buildStepRequests;

    public WarManager() {
        var townHallDefensePosition = GetTownHallDefensePosition(Controller.StartingTownHall, Controller.EnemyLocations[0]);
        _battleManager = new BattleManager();
        _battleManager.Assign(townHallDefensePosition, GuardRadius);
        _townHallToDefend = Controller.StartingTownHall;
    }

    public void OnFrame() {
        // Assign forces
        // TODO GD Use queens
        var newSoldiers = Controller.GetUnits(Controller.NewOwnedUnits, Units.ZergMilitary).ToList();
        _battleManager.Assign(newSoldiers);

        // TODO GD Use multiple managers

        var enemyPosition = Controller.EnemyLocations[0];
        // TODO GD Cache this, buildings don't move
        var currentDistanceToEnemy = Pathfinder.FindPath(_townHallToDefend.Position, enemyPosition).Count; // Not exact, but the distance difference should not matter
        var newTownHallToDefend = Controller.GetUnits(Controller.NewOwnedUnits, Units.Hatchery)
            .FirstOrDefault(townHall => Pathfinder.FindPath(townHall.Position, enemyPosition).Count < currentDistanceToEnemy);

        // TODO GD Fallback on other townhalls when destroyed
        if (newTownHallToDefend != default) {
            _battleManager.Assign(GetTownHallDefensePosition(newTownHallToDefend, Controller.EnemyLocations[0]), GuardRadius);
            _townHallToDefend = newTownHallToDefend;
        }

        if (_battleManager.Force >= SupplyRequiredBeforeAttacking && _buildStepRequests.Count == 0) {
            _buildStepRequests.Add(new BuildOrders.BuildStep(BuildType.Train, 0, Units.Roach, 1000));
            _battleManager.Assign(enemyPosition, AttackRadius);
        }

        GraphicalDebugger.AddLine(_townHallToDefend.Position, _battleManager.Target, Colors.Red);
        GraphicalDebugger.AddSphere(_battleManager.Target, 1, Colors.Red);
        _battleManager.OnFrame();
    }

    public void Retire() {
        throw new System.NotImplementedException();
    }

    public void ReportUnitDeath(Unit deadUnit) {
        // Nothing to do
    }

    private static Vector3 GetTownHallDefensePosition(Unit townHall, Vector3 threatPosition) {
        var pathToThreat = Pathfinder.FindPath(townHall.Position, threatPosition);
        var guardDistance = Math.Min(pathToThreat.Count, GuardDistance);

        return pathToThreat[guardDistance];
    }
}
