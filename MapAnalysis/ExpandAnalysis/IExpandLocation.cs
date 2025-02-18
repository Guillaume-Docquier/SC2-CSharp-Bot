﻿using System.Numerics;
using SC2Client.State;

namespace MapAnalysis.ExpandAnalysis;

/// <summary>
/// Represents an expand location located near a resource cluster.
/// </summary>
public interface IExpandLocation {
    /// <summary>
    /// The optimal position where you should build a townhall to collect the resources of this expand location.
    /// </summary>
    public Vector2 OptimalTownHallPosition { get; }

    /// <summary>
    /// The type of this expand location.
    /// </summary>
    public ExpandType ExpandType { get; }

    /// <summary>
    /// The resources of this expand location.
    /// </summary>
    public IReadOnlyList<IUnit> Resources { get; }
}
