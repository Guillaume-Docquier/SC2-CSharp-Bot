﻿using System;
using System.Collections.Generic;
using Bot.Scenarios;
using Bot.Wrapper;
using SC2APIProtocol;

namespace Bot;

public class Program {
    private static readonly List<IScenario> Scenarios = new List<IScenario>
    {
        new WorkerRushScenario(),
    };

    private static readonly IBot Bot = new SajuukBot("2_1_0", scenarios: Scenarios);

    private const string MapFileName = Maps.Season_2022_4.FileNames.Berlingrad;
    private const Race OpponentRace = Race.Terran;
    private const Difficulty OpponentDifficulty = Difficulty.Hard;

    private const bool RealTime = false;

    public static GameConnection GameConnection { get; private set; }
    public static bool DebugEnabled { get; private set; }

    // TODO Setter made public for tests. Should we make an execution path that sets it instead?
    public static IGraphicalDebugger GraphicalDebugger { get; set; }

    public static void Main(string[] args) {
        try {
            if (args.Length == 0) {
                DebugEnabled = true;
                GraphicalDebugger = new LocalGraphicalDebugger();

                GameConnection = new GameConnection(runEvery: 2);
                GameConnection.RunLocal(Bot, MapFileName, OpponentRace, OpponentDifficulty, RealTime).Wait();
            }
            else {
                DebugEnabled = false;
                GraphicalDebugger = new LadderGraphicalDebugger();

                // On the ladder, for some reason, actions have a 1 frame delay before being received and applied
                // We will run every 2 frames, this way we won't notice the delay
                GameConnection = new GameConnection(runEvery: 2);
                GameConnection.RunLadder(Bot, args).Wait();
            }
        }
        catch (Exception ex) {
            Logger.Info(ex.ToString());
        }

        Logger.Info("Terminated.");
    }
}
