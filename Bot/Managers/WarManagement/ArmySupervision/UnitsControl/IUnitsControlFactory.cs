﻿using Bot.Managers.WarManagement.ArmySupervision.UnitsControl.SneakAttackUnitsControl;
using Bot.Managers.WarManagement.ArmySupervision.UnitsControl.StutterStepUnitsControl;

namespace Bot.Managers.WarManagement.ArmySupervision.UnitsControl;

public interface IUnitsControlFactory {
    public SneakAttack CreateSneakAttack();
    public StutterStep CreateStutterStep();
    public BurrowHealing CreateBurrowHealing();
    public DefensiveUnitsControl CreateDefensiveUnitsControl();
    public DisengagementKiting CreateDisengagementKiting();
    public MineralWalkKiting CreateMineralWalkKiting();
    public OffensiveUnitsControl CreateOffensiveUnitsControl();
}
