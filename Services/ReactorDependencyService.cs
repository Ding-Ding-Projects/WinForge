using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// Defines deliberately playful app-module power gates. Live simulated reactor generation is the
/// preferred source; an explicitly enabled, manually started feature-bus EDG may satisfy the same
/// entry gate. The check is deliberately pure so navigation, UI, and tests all use the same rules.
/// </summary>
public sealed record ReactorDependency(
    string Tag,
    string NameEn,
    string NameZh,
    double MinimumElectricMW,
    string ReasonEn,
    string ReasonZh);

public enum ReactorDependencyPowerSource
{
    None,
    Nuclear,
    EmergencyDiesel,
}

public sealed record ReactorDependencyCheck(
    bool IsSatisfied,
    string StatusEn,
    string StatusZh,
    string DetailEn,
    string DetailZh,
    ReactorDependencyPowerSource Source = ReactorDependencyPowerSource.None);

public sealed record ReactorDependencyPageContext(
    string TargetTag,
    ReactorDependency Dependency,
    string? OwnerToken = null);

public sealed record FeaturePoweredModuleContext(
    string? Fragment,
    string OwnerToken);

public static class ReactorDependencyService
{
    private static readonly ReactorDependency[] Items =
    {
        new(
            "module.cakefactory",
            "Cake Factory & Farm",
            "蛋糕工廠與農場",
            35,
            "Farm drives, cold rooms, mixers, ovens and packaging need a live feature-bus source.",
            "農場驅動、冷藏房、攪拌機、焗爐同包裝線都需要即時功能電源。"),
        new(
            "module.ollama",
            "Ollama",
            "本地大模型",
            80,
            "Local model serving is treated as a high-load compute plant on the playful feature bus.",
            "本地模型服務視為接駁玩味功能匯流排嘅高負載運算設備。"),
        new(
            "module.blender",
            "Blender (3D / Render)",
            "Blender（3D／算圖）",
            180,
            "Render jobs use one powered feature-bus outlet before heavy 3D work can open.",
            "算圖工作要先佔用一個功能電源插槽，先可以開啟重型 3D 負載。"),
        new(
            "module.docker",
            "Docker",
            "Docker",
            55,
            "Container orchestration depends on the simulated feature-power bus.",
            "容器編排依賴模擬功能電源匯流排。"),
        new(
            "module.wslvm",
            "WSL & VM Launcher",
            "WSL 與 VM 啟動器",
            120,
            "Linux distros and sandbox VMs require an energized playful compute bus.",
            "Linux 發行版同沙盒虛擬機需要已供電嘅玩味運算匯流排。"),
        new(
            "module.virtualbox",
            "VirtualBox Manager",
            "VirtualBox 管理",
            150,
            "Virtual machines stay locked until a stable feature-power source is available.",
            "虛擬機會保持鎖定，直至有穩定功能電源可用。"),
        new(
            "module.packer",
            "Packer (Image Builder)",
            "Packer（映像建置器）",
            210,
            "Image builds are playful high-load batch jobs and need the largest feature-bus threshold.",
            "映像建置係玩味高負載批次工作，需要最高功能電源門檻。"),
        new(
            "module.minecraftserver",
            "Minecraft Server",
            "Minecraft 伺服器",
            65,
            "The game server rack stays locked until one feature-power source is ready.",
            "遊戲伺服器機櫃會鎖定，直至有一個功能電源準備好。"),
        new(
            "module.emulator",
            "Android Emulator",
            "Android 模擬器",
            95,
            "Emulator acceleration is treated as a powered feature-bus compute load.",
            "模擬器加速視為需要功能電源嘅運算負載。"),
    };

    public static IReadOnlyList<ReactorDependency> All => Items;

    public static bool Requires(string tag) => Items.Any(d => SameTag(d.Tag, tag));

    public static bool TryGet(string tag, out ReactorDependency dependency)
    {
        dependency = Items.FirstOrDefault(d => SameTag(d.Tag, tag))!;
        return dependency is not null;
    }

    public static string BadgeFor(
        string tag,
        bool allowEmergencyDieselFallback = false,
        bool cantonese = false)
        => TryGet(tag, out var d)
            ? allowEmergencyDieselFallback
                ? cantonese
                    ? $"⚛ {d.MinimumElectricMW:0} MWe 核電 · ⛽ 要入油／2 個插槽"
                    : $"⚛ {d.MinimumElectricMW:0} MWe nuclear · ⛽ fuel + 2 slots"
                : cantonese
                    ? $"⚛ {d.MinimumElectricMW:0} MWe 核電"
                    : $"⚛ {d.MinimumElectricMW:0} MWe nuclear"
            : "";

    public static ReactorDependencyCheck Evaluate(
        string tag,
        ReactorStatusSnapshot snapshot,
        bool apiEnabled = true,
        bool allowEmergencyDieselFallback = false,
        FeatureEmergencyDieselSnapshot emergencyDiesel = default,
        bool dieselModuleSlotAvailable = true)
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

        return Evaluate(
            dependency,
            snapshot,
            apiEnabled,
            allowEmergencyDieselFallback,
            emergencyDiesel,
            dieselModuleSlotAvailable);
    }

    public static ReactorDependencyCheck Evaluate(
        ReactorDependency dependency,
        ReactorStatusSnapshot snapshot,
        bool apiEnabled = true,
        bool allowEmergencyDieselFallback = false,
        FeatureEmergencyDieselSnapshot emergencyDiesel = default,
        bool dieselModuleSlotAvailable = true)
    {
        var nuclear = EvaluateNuclear(dependency, snapshot, apiEnabled);
        if (nuclear.IsSatisfied || !allowEmergencyDieselFallback)
            return nuclear;

        if (emergencyDiesel.State == FeatureEmergencyDieselState.Starting)
        {
            return new ReactorDependencyCheck(
                false,
                "Emergency diesel starting",
                "應急柴油發電機啟動中",
                $"Backup power is starting ({emergencyDiesel.RemainingStartSeconds:0.0} s remaining). " +
                $"{dependency.NameEn} unlocks only after the EDG reaches rated output.",
                $"後備電源啟動中（剩餘 {emergencyDiesel.RemainingStartSeconds:0.0} 秒）。" +
                $"{dependency.NameZh} 要等柴油機達到額定輸出先會解鎖。");
        }

        if (!emergencyDiesel.IsRunning)
        {
            bool needsFuel = !emergencyDiesel.HasFuel;
            return new ReactorDependencyCheck(
                false,
                needsFuel ? "Emergency diesel needs fuel" : "Emergency diesel stopped",
                needsFuel ? "應急柴油發電機要入油" : "應急柴油發電機已停",
                needsFuel
                    ? $"Fill the session-only simulated diesel tank, then manually start the EDG to power {dependency.NameEn}. Nuclear path: {nuclear.DetailEn}"
                    : $"Manually start the fueled feature-bus EDG to power {dependency.NameEn}. Nuclear path: {nuclear.DetailEn}",
                needsFuel
                    ? $"請先為只限今次 session 嘅模擬柴油缸入滿油，再手動啟動柴油機為 {dependency.NameZh} 供電。核電路徑：{nuclear.DetailZh}"
                    : $"請手動啟動已入油嘅功能匯流排柴油機，為 {dependency.NameZh} 供電。核電路徑：{nuclear.DetailZh}");
        }

        double dieselCapacity = double.IsFinite(emergencyDiesel.CapacityMW)
            ? Math.Max(0, emergencyDiesel.CapacityMW)
            : 0;
        if (dieselCapacity < dependency.MinimumElectricMW)
        {
            return new ReactorDependencyCheck(
                false,
                "Emergency diesel output too low",
                "應急柴油發電機輸出太低",
                $"{dependency.NameEn} needs {dependency.MinimumElectricMW:0} MWe; the EDG can supply " +
                $"{dieselCapacity:0.0} MWe.",
                $"{dependency.NameZh} 需要 {dependency.MinimumElectricMW:0} MWe；柴油機只可供應 " +
                $"{dieselCapacity:0.0} MWe。");
        }

        if (!dieselModuleSlotAvailable)
        {
            return new ReactorDependencyCheck(
                false,
                "Emergency diesel module outlets full",
                "應急柴油發電機插槽已滿",
                $"The EDG is already powering {emergencyDiesel.ActiveModuleCount} of " +
                $"{emergencyDiesel.MaxModuleCount} allowed modules. Close or leave one EDG-powered module, then retry {dependency.NameEn}.",
                $"柴油機已為 {emergencyDiesel.ActiveModuleCount}/{emergencyDiesel.MaxModuleCount} 個可用模組供電。" +
                $"請關閉或離開其中一個柴油供電模組，再重試 {dependency.NameZh}。");
        }

        return new ReactorDependencyCheck(
            true,
            "Emergency diesel bus energized",
            "應急柴油發電機匯流排已供電",
            $"{dependency.NameEn} is cleared to run on the manually started " +
            $"{dieselCapacity:0.0} MWe feature-bus EDG ({emergencyDiesel.ActiveModuleCount}/" +
            $"{emergencyDiesel.MaxModuleCount} outlets currently in use).",
            $"{dependency.NameZh} 可使用已手動啟動嘅 {dieselCapacity:0.0} MWe " +
            $"功能匯流排柴油機運行（目前使用 {emergencyDiesel.ActiveModuleCount}/" +
            $"{emergencyDiesel.MaxModuleCount} 個插槽）。",
            ReactorDependencyPowerSource.EmergencyDiesel);
    }

    /// <summary>
    /// Runtime convenience wrapper that applies the persisted fallback policy and current session-only
    /// diesel state. Pure tests should call <see cref="Evaluate(ReactorDependency, ReactorStatusSnapshot, bool, bool, FeatureEmergencyDieselSnapshot)"/>.
    /// </summary>
    public static ReactorDependencyCheck EvaluateConfigured(
        ReactorDependency dependency,
        ReactorStatusSnapshot snapshot,
        bool apiEnabled = true,
        string? ownerToken = null)
    {
        var power = ReactorFeaturePowerService.I;
        var diesel = power.EmergencyDiesel;
        return Evaluate(
            dependency,
            snapshot,
            apiEnabled,
            power.AllowEmergencyDieselFallback,
            diesel,
            power.CanAcquireModule(ownerToken, dependency.Tag));
    }

    private static ReactorDependencyCheck EvaluateNuclear(
        ReactorDependency dependency,
        ReactorStatusSnapshot snapshot,
        bool apiEnabled)
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

        double electricMW = double.IsFinite(snapshot.ElectricMW)
            ? Math.Max(0, snapshot.ElectricMW)
            : 0;
        if (electricMW < dependency.MinimumElectricMW)
        {
            return new ReactorDependencyCheck(
                false,
                "Reactor output too low",
                "反應堆輸出太低",
                $"{dependency.NameEn} needs {dependency.MinimumElectricMW:0} MWe; the bus currently has {electricMW:0.0} MWe.",
                $"{dependency.NameZh} 需要 {dependency.MinimumElectricMW:0} MWe；目前電網只有 {electricMW:0.0} MWe。");
        }

        return new ReactorDependencyCheck(
            true,
            "Reactor bus energized",
            "反應堆電網已供電",
            $"{dependency.NameEn} is cleared to run on {electricMW:0.0} MWe.",
            $"{dependency.NameZh} 可使用目前 {electricMW:0.0} MWe 運行。",
            ReactorDependencyPowerSource.Nuclear);
    }

    private static bool SameTag(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
