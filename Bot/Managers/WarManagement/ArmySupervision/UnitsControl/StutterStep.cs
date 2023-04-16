﻿using System.Collections.Generic;
using System.Linq;
using Bot.Debugging;
using Bot.Debugging.GraphicalDebugging;
using Bot.ExtensionMethods;
using Bot.GameSense;
using SC2APIProtocol;

namespace Bot.Managers.WarManagement.ArmySupervision.UnitsControl;

public class StutterStep : IUnitsControl {
    private const bool Debug = true;
    private readonly ExecutionTimeDebugger _debugger = new ExecutionTimeDebugger();

    public bool IsExecuting() {
        return false;
    }

    public IReadOnlySet<Unit> Execute(IReadOnlySet<Unit> army) {
        if (army.Count <= 0) {
            return army;
        }

        _debugger.Reset();

        _debugger.StartTimer("Total");
        _debugger.StartTimer("Graph");
        var pressureGraph = BuildPressureGraph(army.Where(unit => !unit.IsBurrowed).ToList());
        // We'll print a green graph without cycles on top of the original red graph
        // The only red lines visible will be those associated with a broken cycle
        DebugPressureGraph(pressureGraph, Colors.Red);
        _debugger.StopTimer("Graph");

        _debugger.StartTimer("Cycles");
        BreakCycles(pressureGraph);
        DebugPressureGraph(pressureGraph, Colors.Green);
        _debugger.StopTimer("Cycles");

        _debugger.StartTimer("Moves");
        var uncontrolledUnits = new HashSet<Unit>(army);
        foreach (var unitThatShouldMove in GetUnitsThatShouldMove(pressureGraph)) {
            var unitStatus = "OK";

            if (unitThatShouldMove.IsEngagingTheEnemy && !unitThatShouldMove.IsReadyToAttack) {
                unitStatus = "PUSH";
                unitThatShouldMove.Move(unitThatShouldMove.EngagedTarget.Position.ToVector2());
                uncontrolledUnits.Remove(unitThatShouldMove);
            }

            if (unitThatShouldMove.IsReadyToAttack) {
                unitStatus = "ATK";
            }

            DebugUnitStatus(unitThatShouldMove, unitStatus);
        }
        _debugger.StopTimer("Moves");
        _debugger.StopTimer("Total");

        var totalTime = _debugger.GetExecutionTime("Total");
        if (totalTime >= 5) {
            _debugger.LogExecutionTimes("StutterStep");
        }

        return uncontrolledUnits;
    }

    public void Reset(IReadOnlyCollection<Unit> army) {}

    /// <summary>
    /// Build a directed pressure graph to represent units that want to move but that are being blocked by other units.
    /// We will consider that a unit is blocked by another one if translating the unit forwards would result in a collision with any other unit in the army.
    /// When a unit is blocked by another one, we will say that it pressures the blocking unit.
    /// The graph may contain cycles.
    /// </summary>
    /// <param name="army">The units to consider</param>
    /// <returns>The pressure graph</returns>
    private static IReadOnlyDictionary<Unit, Pressure> BuildPressureGraph(IReadOnlyCollection<Unit> army) {
        var pressureGraph = army.ToDictionary(soldier => soldier, _ => new Pressure());
        foreach (var soldier in army) {
            var nextPosition = soldier.Position.ToVector2().TranslateInDirection(soldier.Facing, soldier.Radius * 2);

            // TODO GD That's n^2, can we do better?
            // The army is generally small, maybe it doesn't matter
            var blockingUnits = army
                .Where(otherSoldier => otherSoldier != soldier)
                .Where(otherSoldier => otherSoldier.DistanceTo(nextPosition) < otherSoldier.Radius + soldier.Radius)
                .ToList();

            foreach (var blockingUnit in blockingUnits) {
                pressureGraph[soldier].To.Add(blockingUnit);
                pressureGraph[blockingUnit].From.Add(soldier);
            }
        }

        return pressureGraph;
    }

    /// <summary>
    /// Break cycles in the given pressure graph.
    /// We will do a depth first traversal of the graph starting from the "root" nodes, that is the units in the front that do not pressure anyone.
    /// While traversing, we keep track of the current branch and if we visit a member of the branch twice before reaching the end, we cut the last edge.
    ///
    /// The pressure graph will be mutated.
    /// </summary>
    /// <param name="pressureGraph">The pressure graph to break the cycles of</param>
    private static void BreakCycles(IReadOnlyDictionary<Unit, Pressure> pressureGraph) {
        var fullyCleared = new HashSet<Unit>();
        foreach (var (soldier, _) in pressureGraph.Where(kv => !kv.Value.To.Any())) {
            var currentBranchSet = new HashSet<Unit>();
            var currentBranchStack = new Stack<Unit>();

            var explorationStack = new Stack<Unit>();
            explorationStack.Push(soldier);

            // Depth first search
            // When backtracking (reaching the end of a branch, no pressure from), clear the currentBranchSet because there were no cycles
            var backtracking = false;
            while (explorationStack.Any()) {
                var toExplore = explorationStack.Pop();

                if (backtracking) {
                    while (!pressureGraph[currentBranchStack.Peek()].From.Contains(toExplore)) {
                        fullyCleared.Add(currentBranchStack.Pop());
                    }

                    currentBranchSet = currentBranchStack.ToHashSet();
                    backtracking = false;
                }

                currentBranchSet.Add(toExplore);
                currentBranchStack.Push(toExplore);

                var leafNode = true;
                foreach (var pressureFrom in pressureGraph[toExplore].From) {
                    if (currentBranchSet.Contains(pressureFrom)) {
                        // Cycle, break it and backtrack
                        pressureGraph[pressureFrom].To.Remove(toExplore);
                        pressureGraph[toExplore].From.Remove(pressureFrom);
                        backtracking = true;
                    }
                    else if (!fullyCleared.Contains(pressureFrom)) {
                        leafNode = false;
                        explorationStack.Push(pressureFrom);
                    }
                }

                // Leaf node, no cycle, let's backtrack
                if (leafNode) {
                    fullyCleared.Add(toExplore);
                    backtracking = true;
                }
            }
        }
    }

    /// <summary>
    /// Get the units that should move based on the pressure graph.
    /// Starting from the leaf nodes (units in the back that have no pressure on them), we traverse the graph an propagate the move intentions.
    /// If a unit needs to move, we ask the units that are being pressures on to move as well.
    /// A unit "needs to move" if all of these are true
    /// - It has an order that makes it move
    /// - It is pressuring other units
    /// - It is not engaging an enemy
    ///
    /// A unit will be pressured to move of all of these are true
    /// - A unit is pressuring it
    /// - It is engaging an enemy
    /// - It is on cooldown
    /// </summary>
    /// <param name="pressureGraph">The pressure graph to respect</param>
    /// <returns>The list of units that should move given the pressure graph</returns>
    private static IEnumerable<Unit> GetUnitsThatShouldMove(IReadOnlyDictionary<Unit, Pressure> pressureGraph) {
        var unitsThatNeedToMove = new HashSet<Unit>();

        var explorationQueue = new Queue<(Unit unit, Pressure pressure)>();
        foreach (var (unit, pressure) in pressureGraph.Where(kv => !kv.Value.From.Any())) {
            explorationQueue.Enqueue((unit, pressure));
        }

        while (explorationQueue.Any()) {
            var (soldier, pressure) = explorationQueue.Dequeue();
            var shouldMove = false;

            // You need to move if you want to attack but you cannot
            // TODO GD More orders can probably cause you to want to move
            shouldMove |= pressure.To.Any() && (soldier.IsMoving() || soldier.IsAttacking()) && !soldier.IsFightingTheEnemy;

            // You need to move if you're pressured and engaging but on cooldown
            // TODO GD Maybe you should be considered moving regardless of cooldown (we just won't ask you to move, but the move propagation will happen)
            shouldMove |= pressure.From.Any() && soldier.IsEngagingTheEnemy && !soldier.IsReadyToAttack;

            Program.GraphicalDebugger.AddText($"{shouldMove}", worldPos: soldier.Position.ToPoint(yOffset: -0.51f), color: Colors.Yellow);

            if (!shouldMove) {
                continue;
            }

            unitsThatNeedToMove.Add(soldier);
            foreach (var pressured in pressure.To.Where(pressured => !unitsThatNeedToMove.Contains(pressured))) {
                explorationQueue.Enqueue((pressured, pressureGraph[pressured]));
            }
        }

        return unitsThatNeedToMove;
    }

    /// <summary>
    /// Display the pressure graph.
    /// </summary>
    /// <param name="pressureGraph">The pressure graph</param>
    /// <param name="color">The color of the pressure graph</param>
    private static void DebugPressureGraph(IReadOnlyDictionary<Unit, Pressure> pressureGraph, Color color) {
        if (!Debug) {
            return;
        }

        foreach (var (soldier, pressure) in pressureGraph) {
            if (color == Colors.Green) {
                // Hacky but whatever
                Program.GraphicalDebugger.AddText($"{pressure.To.Count}-{pressure.From.Count}", worldPos: soldier.Position.ToPoint(yOffset: -0.17f), color: Colors.Yellow);
            }

            foreach (var pressured in pressure.To) {
                Program.GraphicalDebugger.AddArrowedLine(soldier.Position.Translate(zTranslation: 1), pressured.Position.Translate(zTranslation: 1), color);
            }
        }
    }

    private static void DebugUnitStatus(Unit unit, string unitStatus) {
        if (!Debug) {
            return;
        }

        Program.GraphicalDebugger.AddText(unitStatus, worldPos: unit.Position.ToPoint(yOffset: 0.51f));
    }
}

internal struct Pressure {
    public readonly HashSet<Unit> From = new HashSet<Unit>();
    public readonly HashSet<Unit> To = new HashSet<Unit>();

    public Pressure() {}
}
