namespace WinForge.Services;

public sealed record TestModuleDefinition(string Tag);

public static class ModuleRegistry
{
    public static IReadOnlyList<TestModuleDefinition> All { get; } =
    [
        new("module.awake"),
        new("module.cmdpalette")
    ];
}
