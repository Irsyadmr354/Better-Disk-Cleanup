using System.Text.Json;
using System.Text.Json.Serialization;
using BetterDiskCleanup.Core.Recovery;

namespace BetterDiskCleanup.Infrastructure.Recovery;

internal sealed class RecoveryManifestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public RecoveryManifest? Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<RecoveryManifest>(json, SerializerOptions);
    }

    public void Save(string manifestPath, RecoveryManifest manifest)
    {
        var directory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(manifest, SerializerOptions);
        File.WriteAllText(manifestPath, json);
    }
}
