﻿using System.Collections.Generic;
using Bot.ExtensionMethods;
using Bot.Wrapper;

namespace Bot.UnitModules;

public class CapacityModule: IUnitModule, IWatchUnitsDie {
    public const string Tag = "CapacityModule";

    private readonly Unit _unit;
    private readonly int _maxCapacity;
    public readonly List<Unit> AssignedUnits = new List<Unit>();

    public int AvailableCapacity => _maxCapacity - AssignedUnits.Count;

    public static void Install(Unit unit, int maxCapacity) {
        if (UnitModule.PreInstallCheck(Tag, unit)) {
            unit.Modules.Add(Tag, new CapacityModule(unit, maxCapacity));
        }
    }

    private CapacityModule(Unit unit, int maxCapacity) {
        _unit = unit;
        _maxCapacity = maxCapacity;
    }

    public void Assign(Unit unit) {
        Assign(new List<Unit> { unit });
    }

    public void Assign(List<Unit> units) {
        units.ForEach(unit => unit.AddDeathWatcher(this));
        AssignedUnits.AddRange(units);
    }

    public Unit ReleaseOne() {
        if (AssignedUnits.Count <= 0) {
            Logger.Error("Trying to release one unit from CapacityModule, but there's no assigned units");
            return null;
        }

        var unitToRelease = AssignedUnits[0];

        Release(unitToRelease);

        return unitToRelease;
    }

    public void Release(Unit unitToRelease) {
        unitToRelease.RemoveDeathWatcher(this);
        AssignedUnits.Remove(unitToRelease);
    }

    public void Execute() {
        GraphicalDebugger.AddText(AssignedUnits.Count.ToString(), worldPos: _unit.Position.ToPoint());
    }

    public void ReportUnitDeath(Unit deadUnit) {
        AssignedUnits.Remove(deadUnit);
    }
}
