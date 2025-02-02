namespace MapAnalysis.RegionAnalysis.Persistence;

public class MapFileNameFormatter : IMapFileNameFormatter {
    private readonly string _path;

    public MapFileNameFormatter(string path) {
        _path = path;
    }

    public string Format(string topic, string mapFileName) {
        return $"{_path}/{topic}_{mapFileName.Replace(".SC2Map", "").Replace(" ", "").ToLower()}";
    }
}
