﻿using System.Linq;
using Bot.UnitModules;

namespace Bot.Managers;

public partial class TownHallManager {
    private const int MaxExtractorsPerGas = 1;

    private void LogAssignment(Unit unit) {
        Logger.Debug("({0}) Assigned {1}", this, unit);
    }

    public void AssignQueen(Unit queen) {
        if (Queen != null) {
            Logger.Error("({0}) Trying to assign queen, but we already have one", this);
            return;
        }

        LogAssignment(queen);

        queen.Supervisor = this;
        queen.AddDeathWatcher(this);

        Queen = queen;

        DebugLocationModule.Install(queen, _color);
        QueenMicroModule.Install(queen, TownHall);
    }

    public void AssignWorker(Unit worker) {
        LogAssignment(worker);

        worker.Supervisor = this;
        worker.AddDeathWatcher(this);

        _workers.Add(worker);

        DebugLocationModule.Install(worker, _color);
        MiningModule.Install(worker, null);

        // TODO GD Do not dispatch inside assign. Dispatch later
        DispatchWorker(worker);
    }

    private void AssignMineral(Unit mineral) {
        LogAssignment(mineral);

        mineral.Supervisor = this;
        mineral.AddDeathWatcher(this);

        _minerals.Add(mineral);

        DebugLocationModule.Install(mineral, _color);
        CapacityModule.Install(mineral, MaxPerMinerals);
    }

    private void AssignGas(Unit gas) {
        LogAssignment(gas);

        gas.Supervisor = this;

        _gasses.Add(gas);

        DebugLocationModule.Install(gas, _color);
        CapacityModule.Install(gas, MaxExtractorsPerGas);
    }

    private void AssignExtractor(Unit extractor) {
        LogAssignment(extractor);

        extractor.Supervisor = this;
        extractor.AddDeathWatcher(this);

        _extractors.Add(extractor);

        DebugLocationModule.Install(extractor, _color);
        CapacityModule.Install(extractor, MaxPerExtractor);
        UnitModule.Get<CapacityModule>(_gasses.First(gas => gas.DistanceTo(extractor) < 1)).Assign(extractor);
    }

    private void AssignTownHall(Unit townHall) {
        if (TownHall != null) {
            Logger.Error("({0}) Trying to assign town hall, but we already have one", this);
            return;
        }

        LogAssignment(townHall);

        townHall.Supervisor = this;
        townHall.AddDeathWatcher(this);

        TownHall = townHall;

        DebugLocationModule.Install(TownHall, _color);
    }
}
