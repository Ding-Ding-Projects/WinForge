namespace WinForge.Services;

/// <summary>
/// 套件管理檢視深層連結（純 managed）· Package-manager view deep links (pure managed).
/// Keeps tray, command-line aliases, and the WinUI page on the same stable targets.
/// </summary>
public enum PackageManagerViewTarget
{
    Discover = 0,
    Updates = 1,
    Installed = 2,
    Bundles = 3,
    Sources = 4,
    Ignored = 5,
    Setup = 6,
    Settings = 7,
    Operations = 8,
}

/// <summary>
/// 套件管理檢視路由表 · Routing table for Package Manager views.
/// </summary>
public static class PackageManagerViewRouting
{
    public const string ModuleTag = "module.packages";

    /// <summary>建立供 Navigator 使用嘅完整模組＋fragment key · Build a module + fragment key for Navigator.</summary>
    public static string NavigationKey(PackageManagerViewTarget target)
        => ModuleTag + "#" + Fragment(target);

    /// <summary>將已支援 fragment 轉成 ComboBox 檢視索引 · Parse a supported fragment into the view ComboBox index.</summary>
    public static bool TryGetViewIndex(string? fragment, out int index)
    {
        switch (fragment?.Trim().ToLowerInvariant())
        {
            case "discover": index = (int)PackageManagerViewTarget.Discover; return true;
            case "updates": index = (int)PackageManagerViewTarget.Updates; return true;
            case "installed": index = (int)PackageManagerViewTarget.Installed; return true;
            case "bundles": index = (int)PackageManagerViewTarget.Bundles; return true;
            case "sources": index = (int)PackageManagerViewTarget.Sources; return true;
            case "ignored": index = (int)PackageManagerViewTarget.Ignored; return true;
            case "setup": index = (int)PackageManagerViewTarget.Setup; return true;
            case "settings": index = (int)PackageManagerViewTarget.Settings; return true;
            case "operations": index = (int)PackageManagerViewTarget.Operations; return true;
            default:
                index = 0;
                return false;
        }
    }

    private static string Fragment(PackageManagerViewTarget target) => target switch
    {
        PackageManagerViewTarget.Discover => "discover",
        PackageManagerViewTarget.Updates => "updates",
        PackageManagerViewTarget.Installed => "installed",
        PackageManagerViewTarget.Bundles => "bundles",
        PackageManagerViewTarget.Sources => "sources",
        PackageManagerViewTarget.Ignored => "ignored",
        PackageManagerViewTarget.Setup => "setup",
        PackageManagerViewTarget.Settings => "settings",
        PackageManagerViewTarget.Operations => "operations",
        _ => "discover",
    };
}
