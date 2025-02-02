namespace MapAnalysis.RegionAnalysis.Persistence;

public interface IMapFileNameFormatter {
    /// <summary>
    /// Creates a standardized file name from a topic and the name of an SC2 map.
    /// </summary>
    /// <param name="topic">A differentiating topic, like "Regions" or "StateDump1", etc.</param>
    /// <param name="mapFileName">The map file name.</param>
    /// <returns></returns>
    public string Format(string topic, string mapFileName);
}
