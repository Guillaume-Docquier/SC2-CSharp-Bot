﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bot.UnitModules;

namespace Bot.Managers;

public class BattleManager: IManager {
    public Vector3 Target;
    public readonly List<Unit> Army = new List<Unit>();

    public BattleManager(Vector3 target) {
        Target = target;
    }

    public void Assign(Vector3 target) {
        Target = target;
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
        Army.Where(unit => unit.Orders.All(order => order.AbilityId != Abilities.Attack))
            .Where(unit => unit.DistanceTo(Target) > 3)
            .ToList()
            .ForEach(unit => unit.AttackMove(Target));
    }

    public void ReportUnitDeath(Unit deadUnit) {
        Army.Remove(deadUnit);
    }
}
