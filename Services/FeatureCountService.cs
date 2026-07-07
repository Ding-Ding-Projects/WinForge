using WinForge.Catalog;

namespace WinForge.Services;

/// <summary>Canonical counts for app, docs, and website metadata.</summary>
public static class FeatureCountService
{
    public static int TweakFeatureCount => TweakCatalog.Count;
    public static int ModuleCount => ModuleRegistry.All.Count;
    public static int CategoryCount => Categories.All.Length;
    public static int FullFeatureCount => TweakFeatureCount + ModuleCount;
}
