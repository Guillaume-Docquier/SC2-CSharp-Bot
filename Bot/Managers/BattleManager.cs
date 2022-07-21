﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bot.GameData;
using Bot.UnitModules;
using Bot.Wrapper;

namespace Bot.Managers;

public class BattleManager: IManager {
    public const float AcceptableDistanceToTarget = 3;

    public readonly List<Unit> Army = new List<Unit>();
    public Vector3 Target;
    private float _blastRadius;

    public float Force => GetForceOf(Army);

    private float RetreatForceThreshold => _initialForce * 0.5f;
    private float _initialForce;

    private float AttackForceThreshold => _strongestForce * 1.2f;
    private float _strongestForce;

    private Stage _currentStage = Stage.Attack;

    private readonly List<BuildOrders.BuildStep> _buildStepRequests = new List<BuildOrders.BuildStep>();
    public IEnumerable<BuildOrders.BuildStep> BuildStepRequests => _buildStepRequests;

    private enum Stage {
        Retreat,
        Grow,
        Attack,
        Defend,
    }

    public void Assign(Vector3 target, float blastRadius) {
        Target = target.WithWorldHeight();
        _blastRadius = blastRadius;
        _initialForce = Force;
        _strongestForce = Force;
        _currentStage = Stage.Attack;
    }

    public void Assign(List<Unit> soldiers) {
        soldiers.ForEach(unit => {
            unit.AddDeathWatcher(this);

            if (unit.UnitType == Units.Roach) {
                BurrowMicroModule.Install(unit);
            }
        });

        // TODO GD Use a targeting module
        Army.AddRange(soldiers);
    }

    public void OnFrame() {
        if (Army.Count < 1) {
            return;
        }

        var clusters = Clustering.DBSCAN(Army, 3, 3).OrderByDescending(cluster => cluster.Count).ToList();
        var biggestCluster = clusters.FirstOrDefault();

        if (biggestCluster == null || ShouldRetreat(biggestCluster)) {
            _currentStage = Stage.Retreat;

            Retreat(Clustering.GetCenter(Army), Army);
        }
        else if (ShouldGrowStronger(biggestCluster)) {
            _currentStage = Stage.Grow;
            _initialForce = GetForceOf(biggestCluster);

            GraphicalDebugger.AddTextGroup(
                new[]
                {
                    $"Force: {GetForceOf(biggestCluster)}",
                    $"Strongest: {_strongestForce}",
                    $"Attack at: {AttackForceThreshold}"
                },
                worldPos: Clustering.GetCenter(biggestCluster).Translate(1f, 1f).ToPoint());

            Retreat(Clustering.GetCenter(Army), Army);
        }
        else if (_currentStage != Stage.Defend && Vector3.Distance(Target, Clustering.GetCenter(biggestCluster)) > AcceptableDistanceToTarget) {
            _currentStage = Stage.Attack;
            _strongestForce = Math.Max(_strongestForce, GetForceOf(biggestCluster));

            GraphicalDebugger.AddTextGroup(
                new[]
                {
                    $"Force: {GetForceOf(biggestCluster)}",
                    $"Initial: {_initialForce}",
                    $"Retreat at: {RetreatForceThreshold}"
                },
                worldPos: Clustering.GetCenter(biggestCluster).Translate(1f, 1f).ToPoint());

            Attack(Target, biggestCluster);
            Rally(Clustering.GetCenter(biggestCluster), Army.Where(soldier => !biggestCluster.Contains(soldier)).ToList());
        }
        else {
            _currentStage = Stage.Defend;

            GraphicalDebugger.AddTextGroup(
                new[]
                {
                    $"Force: {GetForceOf(biggestCluster)}",
                },
                worldPos: Clustering.GetCenter(biggestCluster).Translate(1f, 1f).ToPoint());

            Defend(Target, biggestCluster);
            Rally(Clustering.GetCenter(biggestCluster), Army.Where(soldier => !biggestCluster.Contains(soldier)).ToList());
        }
    }

    public void Retire() {
        Army.ForEach(soldier => UnitModule.Uninstall<BurrowMicroModule>(soldier));
    }

    public void ReportUnitDeath(Unit deadUnit) {
        Army.Remove(deadUnit);
    }

    private void Retreat(Vector3 retreatPosition, IReadOnlyCollection<Unit> soldiers) {
        if (soldiers.Count <= 0) {
            return;
        }

        GraphicalDebugger.AddSphere(retreatPosition, AcceptableDistanceToTarget, Colors.Yellow);
        GraphicalDebugger.AddText("Retreat", worldPos: retreatPosition.ToPoint());

        soldiers.Where(unit => unit.Orders.All(order => order.TargetWorldSpacePos == null || !order.TargetWorldSpacePos.Equals(retreatPosition.ToPoint())))
            .Where(unit => unit.DistanceTo(retreatPosition) > AcceptableDistanceToTarget)
            .ToList()
            .ForEach(unit => unit.Move(retreatPosition));

        foreach (var soldier in soldiers) {
            GraphicalDebugger.AddLine(soldier.Position, retreatPosition, Colors.Yellow);
        }
    }

    private static void Attack(Vector3 targetToAttack, IReadOnlyCollection<Unit> soldiers) {
        if (soldiers.Count <= 0) {
            return;
        }

        GraphicalDebugger.AddSphere(targetToAttack, AcceptableDistanceToTarget, Colors.Red);
        GraphicalDebugger.AddText("Attack", worldPos: targetToAttack.ToPoint());

        soldiers.Where(unit => unit.Orders.All(order => order.AbilityId is Abilities.Move or Abilities.Attack))
            .Where(unit => !unit.RawUnitData.IsBurrowed)
            .Where(unit => unit.DistanceTo(targetToAttack) > AcceptableDistanceToTarget)
            .ToList()
            .ForEach(unit => unit.AttackMove(targetToAttack));

        foreach (var soldier in soldiers) {
            GraphicalDebugger.AddLine(soldier.Position, targetToAttack, Colors.Red);
        }
    }

    private static void Rally(Vector3 rallyPoint, IReadOnlyCollection<Unit> soldiers) {
        if (soldiers.Count <= 0) {
            return;
        }

        GraphicalDebugger.AddSphere(rallyPoint, AcceptableDistanceToTarget, Colors.Blue);
        GraphicalDebugger.AddText("Rally", worldPos: rallyPoint.ToPoint());

        soldiers.Where(unit => unit.DistanceTo(rallyPoint) > AcceptableDistanceToTarget)
            .ToList()
            .ForEach(unit => unit.AttackMove(rallyPoint));

        foreach (var soldier in soldiers) {
            GraphicalDebugger.AddLine(soldier.Position, rallyPoint, Colors.Blue);
        }
    }

    private void Defend(Vector3 targetToDefend, IReadOnlyCollection<Unit> soldiers) {
        if (soldiers.Count <= 0) {
            return;
        }

        GraphicalDebugger.AddSphere(targetToDefend, AcceptableDistanceToTarget, Colors.Green);
        GraphicalDebugger.AddTextGroup(new[] { "Defend", $"Radius: {_blastRadius}" }, worldPos: targetToDefend.ToPoint());

        var targetList = Controller.EnemyUnits
            .Where(enemy => enemy.DistanceTo(targetToDefend) < _blastRadius)
            .OrderBy(enemy => enemy.DistanceTo(targetToDefend))
            .ToList();

        if (targetList.Any()) {
            soldiers.Where(unit => unit.Orders.All(order => order.AbilityId is Abilities.Move or Abilities.Attack))
                .Where(unit => !unit.RawUnitData.IsBurrowed)
                .ToList()
                .ForEach(soldier => {
                    var closestEnemy = targetList.Take(5).OrderBy(enemy => enemy.DistanceTo(soldier)).First();

                    soldier.AttackMove(closestEnemy.Position);
                    GraphicalDebugger.AddLine(soldier.Position, closestEnemy.Position, Colors.Red);
                    GraphicalDebugger.AddLine(soldier.Position, targetToDefend, Colors.Green);
                });
        }
        else {
            Rally(targetToDefend, soldiers);
        }
    }

    private static float GetForceOf(IEnumerable<Unit> soldiers) {
        return soldiers.Sum(soldier => soldier.FoodRequired);
    }

    private bool ShouldRetreat(IEnumerable<Unit> soldiers) {
        return _currentStage is Stage.Attack or Stage.Defend && GetForceOf(soldiers) < RetreatForceThreshold;
    }

    private bool ShouldGrowStronger(IEnumerable<Unit> soldiers) {
        return _currentStage is Stage.Retreat or Stage.Grow && GetForceOf(soldiers) < AttackForceThreshold && !Controller.IsSupplyCapped; // TODO GD Not exactly
    }
}
