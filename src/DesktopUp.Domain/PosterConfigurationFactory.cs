namespace DesktopUp.Domain;

// The UI sources share one supported configuration; identities/placement are allocated by the command.
public static class PosterConfigurationFactory
{
    public static PosterDefinition NewBasicBox(string title, string monitorDeviceName) =>
        new("pending", title, "", monitorDeviceName, 0, 0, 300, 360,
            0, 0, .2, .4, false, false, false, PosterPresentationPolicy.AdaptiveGridLayout,
            PosterPresentationPolicy.Serialize(PosterPresentationPolicy.Defaults("basic-box")), "basic-box");
}
