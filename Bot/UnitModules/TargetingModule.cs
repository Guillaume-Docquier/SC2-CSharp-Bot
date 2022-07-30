﻿using System.Numerics;

namespace Bot.UnitModules;

public class TargetingModule: IUnitModule {
    public const string Tag = "targeting-module";

    private readonly Unit _unit;
    private readonly Vector3 _target;

    public static void Install(Unit unit, Vector3 target) {
        if (UnitModule.PreInstallCheck(Tag, unit)) {
            unit.Modules.Add(Tag, new TargetingModule(unit, target));
        }
    }

    private TargetingModule(Unit unit, Vector3 target) {
        _unit = unit;
        _target = target;
    }

    public void Execute() {
        throw new System.NotImplementedException();
    }
}
