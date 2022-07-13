﻿using System.Collections.Generic;
using System.Linq;
using Bot.UnitModules;
using SC2APIProtocol;

namespace Bot.Managers;

public class MiningManager: IManager {
    private const int MaxGas = 2;
    private const int MaxExtractorsPerGas = 1;
    private const int MaxPerExtractor = 3;
    private const int MaxMinerals = 8;
    private const int IdealPerMinerals = 2;
    private const int MaxPerMinerals = 3;
    private const int MaxDistanceToExpand = 10;

    public readonly Unit TownHall;
    private readonly Color _color;

    private readonly List<Unit> _workers = new List<Unit>();
    private readonly List<Unit> _extractors = new List<Unit>();
    private readonly List<Unit> _minerals;
    private readonly List<Unit> _gasses;

    public int IdealAvailableCapacity => _minerals.Count * IdealPerMinerals + _extractors.Count(extractor => extractor.IsOperational) * MaxPerExtractor - _workers.Count;
    public int SaturatedAvailableCapacity => IdealAvailableCapacity + _minerals.Count; // Can allow 1 more per mineral patch

    public MiningManager(Unit townHall, Color color) {
        TownHall = townHall;
        _color = color;

        DebugLocationModule.Install(TownHall, _color);

        _minerals = Controller.GetUnits(Controller.NeutralUnits, Units.MineralFields)
            .Where(mineral => mineral.DistanceTo(TownHall) < MaxDistanceToExpand)
            .Where(mineral => !UnitUtils.IsResourceManaged(mineral))
            .Take(MaxMinerals)
            .ToList();

        _minerals.ForEach(mineral => {
            CapacityModule.Install(mineral, MaxPerMinerals);
            DebugLocationModule.Install(mineral, _color);
        });

        _gasses = Controller.GetUnits(Controller.NeutralUnits, Units.GasGeysers)
            .Where(gas => gas.DistanceTo(TownHall) < MaxDistanceToExpand)
            .Where(gas => !UnitUtils.IsResourceManaged(gas))
            .Take(MaxGas)
            .ToList();

        _gasses.ForEach(gas => {
            CapacityModule.Install(gas, MaxExtractorsPerGas);
            DebugLocationModule.Install(gas, _color);
        });

        DiscoverExtractors();
    }

    public void AssignWorker(Unit worker) {
        AssignWorkers(new List<Unit> { worker });
    }

    public void AssignWorkers(List<Unit> workers) {
        _workers.AddRange(workers);

        foreach (var worker in workers) {
            worker.AddDeathWatcher(this);
            DebugLocationModule.Install(worker, _color);
        }

        DispatchWorkers(workers);
    }

    public void OnFrame() {
        // Get new extractors
        if (_extractors.Count < _gasses.Count) {
            DiscoverExtractors();
        }

        DispatchWorkers(GetIdleWorkers());

        if (_minerals.Sum(mineral => CapacityModule.GetFrom(mineral).AvailableCapacity) <= _minerals.Count) {
            FillExtractors();
        }

        _workers.ForEach(worker => {
            MiningModule.GetFrom(worker).Execute();
            DebugLocationModule.GetFrom(worker).Execute();
        });

        DebugLocationModule.GetFrom(TownHall).Execute();
        _minerals.ForEach(mineral => DebugLocationModule.GetFrom(mineral).Execute());
        _gasses.ForEach(gas => DebugLocationModule.GetFrom(gas).Execute());
        _extractors.ForEach(extractor => DebugLocationModule.GetFrom(extractor).Execute());
    }

    public void ReportUnitDeath(Unit deadUnit) {
        if (deadUnit.UnitType == Units.Drone) {
            _workers.Remove(deadUnit);
        }
        else if (Units.MineralFields.Contains(deadUnit.UnitType)) {
            _minerals.Remove(deadUnit);
            CapacityModule.GetFrom(deadUnit).AssignedUnits.ForEach(worker => MiningModule.Uninstall(worker));
        }
        else if (deadUnit.UnitType == Units.Extractor) {
            _extractors.Remove(deadUnit);
            CapacityModule.GetFrom(deadUnit).AssignedUnits.ForEach(worker => MiningModule.Uninstall(worker));
        }
        else if (Units.GasGeysers.Contains(deadUnit.UnitType)) {
            // TODO GD Is this how gasses die?
            _gasses.Remove(deadUnit);
            var extractor = CapacityModule.GetFrom(deadUnit).AssignedUnits.FirstOrDefault();
            if (extractor != null) {
                _extractors.Remove(extractor);
                extractor.RemoveDeathWatcher(this);
                CapacityModule.GetFrom(extractor).AssignedUnits.ForEach(worker => MiningModule.Uninstall(worker));
            }
        }
    }

    private IEnumerable<Unit> GetIdleWorkers() {
        return _workers.Where(worker => MiningModule.GetFrom(worker) == null);
    }

    private void DispatchWorkers(IEnumerable<Unit> workers) {
        foreach (var worker in workers) {
            var assignedResource = GetClosestExtractorWithAvailableCapacity(worker);
            assignedResource ??= GetClosestMineralWithAvailableCapacity(worker, minAvailableCapacity: 1);
            assignedResource ??= GetClosestMineralWithAvailableCapacity(worker, minAvailableCapacity: 0);

            MiningModule.Install(worker, assignedResource);
            CapacityModule.GetFrom(assignedResource).Assign(worker);
        }
    }

    private void FillExtractors() {
        foreach (var extractor in _extractors.Where(extractor => extractor.IsOperational)) {
            // TODO GD Select from busiest minerals instead
            var workersToReassign = _workers
                .Where(worker => MiningModule.GetFrom(worker).ResourceType == UnitUtils.ResourceType.Mineral)
                .Take(CapacityModule.GetFrom(extractor).AvailableCapacity)
                .ToList();

            foreach (var worker in workersToReassign) {
                UpdateWorkerAssignment(worker, extractor);
            }
        }
    }

    private void DiscoverExtractors() {
        var newExtractors = Controller.GetUnits(Controller.OwnedUnits, Units.Extractor)
            .Where(extractor => extractor.DistanceTo(TownHall) < MaxDistanceToExpand)
            .Where(extractor => !_extractors.Contains(extractor))
            .ToList();

        ManageExtractors(newExtractors);
    }

    private void ManageExtractors(List<Unit> extractors) {
        foreach (var extractor in extractors) {
            extractor.AddDeathWatcher(this);

            DebugLocationModule.Install(extractor, _color);
            CapacityModule.Install(extractor, MaxPerExtractor);
            CapacityModule.GetFrom(_gasses.First(gas => gas.DistanceTo(extractor) < 1)).Assign(extractor);
        }

        _extractors.AddRange(extractors);
    }

    private Unit GetClosestExtractorWithAvailableCapacity(Unit worker) {
        return _extractors
            .Where(extractor => extractor.IsOperational)
            .Where(extractor => CapacityModule.GetFrom(extractor).AvailableCapacity > 0)
            .OrderBy(extractor => extractor.DistanceTo(worker))
            .FirstOrDefault();
    }

    private Unit GetClosestMineralWithAvailableCapacity(Unit worker, int minAvailableCapacity) {
        return _minerals
            .Where(mineral => CapacityModule.GetFrom(mineral).AvailableCapacity > minAvailableCapacity)
            .OrderBy(mineral => mineral.DistanceTo(worker))
            .FirstOrDefault();
    }

    private void UpdateWorkerAssignment(Unit worker, Unit assignedResource) {
        var oldMiningModule = MiningModule.Uninstall(worker);
        CapacityModule.GetFrom(oldMiningModule.AssignedResource).Release(worker);

        MiningModule.Install(worker, assignedResource);
        CapacityModule.GetFrom(assignedResource).Assign(worker);
    }
}
