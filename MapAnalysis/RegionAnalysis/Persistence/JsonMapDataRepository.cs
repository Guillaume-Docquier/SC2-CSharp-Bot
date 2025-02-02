using System.Text.Json;
using System.Text.Json.Serialization;
using SC2Client;

namespace MapAnalysis.RegionAnalysis.Persistence;

public class JsonMapDataRepository<TData> : IMapDataRepository<TData> {
    private readonly ILogger _logger;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        MaxDepth = 256,
        Converters =
        {
            new JsonSerializers.Vector2JsonConverter(),
            new JsonSerializers.Vector3JsonConverter(),
        },
    };

    public JsonMapDataRepository(ILogger logger) {
        _logger = logger;
    }

    public void Save(TData data, string fileName) {
        var fileNameWithExtension = $"{fileName}.{FileExtensions.Json}";
        Directory.CreateDirectory(Path.GetDirectoryName(fileNameWithExtension)!);

        var jsonString = JsonSerializer.Serialize(data, _jsonSerializerOptions);
        File.WriteAllText(fileNameWithExtension, jsonString);

        // Make sure to copy and set properties to 'Copy if newer'
        _logger.Success($"JSON data saved to {fileNameWithExtension}");
    }

    public TData Load(string fileName) {
        var fileNameWithExtension = $"{fileName}.{FileExtensions.Json}";
        if (!File.Exists(fileNameWithExtension)) {
            return default;
        }

        var jsonString = File.ReadAllText(fileNameWithExtension);
        return JsonSerializer.Deserialize<TData>(jsonString, _jsonSerializerOptions)!;
    }
}
