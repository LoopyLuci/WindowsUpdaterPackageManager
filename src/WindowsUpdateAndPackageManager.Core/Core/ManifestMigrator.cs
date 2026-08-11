using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WindowsUpdateAndPackageManager.Core;

public interface IManifestMigrator
{
    Task<string?> MigrateAsync(string json, string targetVersion, CancellationToken cancellationToken = default);
}

public sealed class ManifestMigrator : IManifestMigrator
{
    public Task<string?> MigrateAsync(string json, string targetVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var node = JsonNode.Parse(doc.RootElement.GetRawText())!.AsObject();
            if (!node.TryGetPropertyValue("schemaVersion", out var current) || current is not JsonValue currentValue)
            {
                return Task.FromResult<string?>(null);
            }

            var from = currentValue.GetValue<string>() ?? string.Empty;
            if (string.Equals(from, targetVersion, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<string?>(node.ToJsonString());
            }

            if (IsSupportedUpgrade(from, targetVersion))
            {
                node["schemaVersion"] = targetVersion;
                if (!node.ContainsKey("deltaAvailable"))
                {
                    node["deltaAvailable"] = false;
                }
                if (!node.ContainsKey("previousSha256"))
                {
                    node["previousSha256"] = string.Empty;
                }
                return Task.FromResult<string?>(node.ToJsonString());
            }

            if (IsSupportedDowngrade(from, targetVersion))
            {
                node["schemaVersion"] = targetVersion;
                node.Remove("deltaAvailable");
                node.Remove("previousSha256");
                return Task.FromResult<string?>(node.ToJsonString());
            }

            return Task.FromResult<string?>(null);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    private static bool IsSupportedUpgrade(string from, string to)
    {
        return from == "1.0" && to == "2";
    }

    private static bool IsSupportedDowngrade(string from, string to)
    {
        return from == "2" && to == "1.0";
    }
}
