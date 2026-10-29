using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record CanvasAsset(string Id, string Name, int Width, int Height, string? Description = null, long DescriptionRevision = 0);
public sealed record CanvasCombinationSize(double Width, double Height);
public sealed record CanvasCombination(string Id, string Name, IReadOnlyList<CanvasNode> Nodes, CanvasCombinationSize? Size = null, CanvasBehavior? Behavior = null)
{
    public static string Digest(IReadOnlyList<CanvasNode> nodes, CanvasCombinationSize? size = null, CanvasBehavior? behavior = null)
    {
        // Null retains the digest of existing v2 combinations. New snapshots bind their physical size too.
        string json = behavior is not null ? CanvasDeclaration.Serialize(new { nodes, size, behavior }) :
            size is null ? CanvasDeclaration.Serialize(nodes) : CanvasDeclaration.Serialize(new { nodes, size });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
public sealed record CanvasWork(string Id, string Name, CanvasDesign Design, IReadOnlyList<string>? LibraryGroups = null, bool ShowContentReference = true);

/// <summary>Configuration-scoped design data, never content collection ownership.</summary>
public interface ICanvasWorkspace
{
    (int Width, int Height) ValidateImage(byte[] bytes);
    CanvasAsset Import(string name, byte[] bytes);
    byte[] ReadAsset(string id);
    IReadOnlyList<CanvasAsset> Assets();
    CanvasAsset DescribeAsset(string id, string description, long expectedRevision);
    IReadOnlyList<CanvasWork> Works();
    void SaveWork(string id, string name, CanvasDesign design, bool showContentReference = true);
    IReadOnlyList<CanvasCombination> Combinations();
    void SaveCombination(CanvasCombination combination);
    IReadOnlyList<CanvasBehaviorPrefab> Behaviors();
    void SaveBehavior(CanvasBehaviorPrefab prefab);
    void Verify(CanvasDesign design);
}
