using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DesktopUp.Domain;

namespace DesktopUp.Application;

// Only for command/legacy catalog request hashes, never for storage or portable export.
// v6 had no delay member and implies 3. Omit exactly that semantic default to retain
// its receipt hash; non-default delays must participate to reject request-id reuse.
public static class StateRequestFingerprint
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    public static string Compute<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Options)));

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(PosterDefinition)) return;
            info.Properties.Single(p => p.Name == nameof(PosterDefinition.AutoCollapseDelaySeconds)).ShouldSerialize =
                (_, value) => value is not int seconds || seconds != PosterAutoPosturePolicy.DefaultDelaySeconds;
        });
        return new() { TypeInfoResolver = resolver };
    }
}
