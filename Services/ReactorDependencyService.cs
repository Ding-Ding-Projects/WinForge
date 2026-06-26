using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// Defines app modules that need live simulated reactor generation before they can run.
/// The check is deliberately pure so navigation, UI, and tests all use the same rules.
/// </summary>
public sealed record ReactorDependency(
    string Tag,
    string NameEn,
    string NameZh,
    double MinimumElectricMW,
    string ReasonEn,
    string ReasonZh);

public sealed record ReactorDependencyCheck(
    bool IsSatisfied,
    string StatusEn,
    string StatusZh,
    string DetailEn,
    string DetailZh);

public sealed record ReactorDependencyPageContext(string TargetTag, ReactorDependency Dependency);

public static class ReactorDependencyService
{
    private static readonly ReactorDependency[] Items =
    {
        new(
            "module.cakefactory",
            "Cake Factory & Farm",
            "蛋糕工廠與農場",
            35,
            "Farm drives, cold rooms, mixers, ovens and packaging line are tied to the reactor bus.",
            "農場驅動、冷藏房、攪拌機、焗爐同包裝線都接駁到反應堆電網。"),
        new(
            "module.ollama",
            "Ollama",
            "本地大模型",
            80,
            "Local model serving is treated as a high-load compute plant and needs reactor generation.",
            "本地模型服務視為高負載運算設備，需要反應堆發電。"),
        new(
            "module.blender",
            "Blender (3D / Render)",
            "Blender（3D／算圖）",
            180,
            "Render jobs are locked to the reactor bus so heavy 3D workloads only run while generating.",
            "算圖工作鎖定反應堆電網，重型 3D 負載只會喺發電時運行。"),
        new(
            "module.docker",
            "Docker",
            "Docker",
            55,
            "Container orchestration depends on the simulated station service bus.",
            "容器編排依賴模擬廠用電匯流排。"),
        new(
            "module.wslvm",
            "WSL & VM Launcher",
            "WSL 與 VM 啟動器",
            120,
            "Linux distros and sandbox VMs require an energized reactor-backed compute bus.",
            "Linux 發行版同沙盒虛擬機需要反應堆供電嘅運算匯流排。"),
        new(
            "module.virtualbox",
            "VirtualBox Manager",
            "VirtualBox 管理",
            150,
            "Virtual machines are held offline until reactor electrical output is stable.",
            "虛擬機會保持離線，直至反應堆電功率穩定。"),
        new(
            "module.packer",
            "Packer (Image Builder)",
            "Packer（映像建置器）",
            210,
            "Image builds are reactor-powered batch jobs and need a larger generation margin.",
            "映像建置係反應堆供電批次工作，需要較大發電裕度。"),
        new(
            "module.minecraftserver",
            "Minecraft Server",
            "Minecraft 伺服器",
            65,
            "The game server rack stays locked out until the reactor is on the grid.",
            "遊戲伺服器機櫃會鎖定，直至反應堆併網供電。"),
        new(
            "module.emulator",
            "Android Emulator",
            "Android 模擬器",
            95,
            "Emulator acceleration is treated as reactor-backed compute capacity.",
            "模擬器加速視為由反應堆支援嘅運算容量。"),
    };

    public static IReadOnlyList<ReactorDependency> All => Items;

    public static bool Requires(string tag) => Items.Any(d => SameTag(d.Tag, tag));

    public static bool TryGet(string tag, out ReactorDependency dependency)
    {
        dependency = Items.FirstOrDefault(d => SameTag(d.Tag, tag))!;
        return dependency is not null;
    }

    public static string BadgeFor(string tag)
        => TryGet(tag, out var d) ? $"⚛ {d.MinimumElectricMW:0} MWe reactor bus" : "";

    public static ReactorDependencyCheck Evaluate(string tag, ReactorStatusSnapshot snapshot, bool apiEnabled = true)
    {
        if (!TryGet(tag, out var dependency))
        {
            return new ReactorDependencyCheck(
                true,
                "No reactor dependency",
                "無反應堆相依",
                "This module can run without reactor power.",
                "呢個模組唔需要反應堆供電。");
        }

        return Evaluate(dependency, snapshot, apiEnabled);
    }

    public static ReactorDependencyCheck Evaluate(ReactorDependency dependency, ReactorStatusSnapshot snapshot, bool apiEnabled = true)
    {
        if (!apiEnabled)
        {
            return new ReactorDependencyCheck(
                false,
                "Reactor status API disabled",
                "反應堆狀態 API 已停用",
                "Enable the public reactor status API so dependent apps can read bus power.",
                "請啟用對外反應堆狀態 API，讓相依 app 讀取電網功率。");
        }

        if (snapshot.IsMeltdown)
        {
            return new ReactorDependencyCheck(
                false,
                "Reactor unavailable: meltdown",
                "反應堆不可用：熔毀",
                "Core damage locks out every reactor-dependent app until the simulation is reset.",
                "爐心受損會鎖定所有反應堆相依 app，直至模擬重置。");
        }

        if (snapshot.IsScrammed)
        {
            return new ReactorDependencyCheck(
                false,
                "Reactor unavailable: SCRAM",
                "反應堆不可用：SCRAM",
                "The reactor is tripped; reset and recover generation before opening dependent apps.",
                "反應堆已跳機；請重置並恢復發電，先再開啟相依 app。");
        }

        if (!snapshot.IsGenerating)
        {
            return new ReactorDependencyCheck(
                false,
                "Waiting for reactor generation",
                "等待反應堆發電",
                $"Open the reactor, bring it on-load, and close the generator breaker. {dependency.NameEn} needs {dependency.MinimumElectricMW:0} MWe.",
                $"請開啟反應堆、帶載並合上發電機斷路器。{dependency.NameZh} 需要 {dependency.MinimumElectricMW:0} MWe。");
        }

        if (snapshot.ElectricMW < dependency.MinimumElectricMW)
        {
            return new ReactorDependencyCheck(
                false,
                "Reactor output too low",
                "反應堆輸出太低",
                $"{dependency.NameEn} needs {dependency.MinimumElectricMW:0} MWe; the bus currently has {snapshot.ElectricMW:0.0} MWe.",
                $"{dependency.NameZh} 需要 {dependency.MinimumElectricMW:0} MWe；目前電網只有 {snapshot.ElectricMW:0.0} MWe。");
        }

        return new ReactorDependencyCheck(
            true,
            "Reactor bus energized",
            "反應堆電網已供電",
            $"{dependency.NameEn} is cleared to run on {snapshot.ElectricMW:0.0} MWe.",
            $"{dependency.NameZh} 可使用目前 {snapshot.ElectricMW:0.0} MWe 運行。");
    }

    private static bool SameTag(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
