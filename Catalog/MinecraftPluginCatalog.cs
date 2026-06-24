using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>一個可由 git 來源建置嘅外掛預設 · A plugin preset that can be built from git source.</summary>
public sealed record PluginPreset(
    LocalizedText Name,
    string GitUrl,
    MinecraftServerService.BuildSystem System,
    LocalizedText Blurb);

/// <summary>
/// Minecraft 外掛目錄 · Catalog of popular server plugins that build from git source (clone + Maven/Gradle).
/// 全部都係開源、可由原始碼建置 · All open-source, buildable from source. Bilingual.
/// </summary>
public static class MinecraftPluginCatalog
{
    public static readonly List<PluginPreset> Presets = new()
    {
        new(
            new LocalizedText("EssentialsX", "EssentialsX"),
            "https://github.com/EssentialsX/Essentials.git",
            MinecraftServerService.BuildSystem.Gradle,
            new LocalizedText("Core commands: /home, /spawn, /tpa, kits, economy.",
                              "核心指令：/home、/spawn、/tpa、kit、經濟系統。")),
        new(
            new LocalizedText("LuckPerms", "LuckPerms"),
            "https://github.com/LuckPerms/LuckPerms.git",
            MinecraftServerService.BuildSystem.Gradle,
            new LocalizedText("Powerful permissions and group management.",
                              "強大嘅權限同群組管理。")),
        new(
            new LocalizedText("ViaVersion", "ViaVersion"),
            "https://github.com/ViaVersion/ViaVersion.git",
            MinecraftServerService.BuildSystem.Gradle,
            new LocalizedText("Let newer clients join an older server version.",
                              "畀新版客戶端連去舊版伺服器。")),
        new(
            new LocalizedText("WorldEdit", "WorldEdit"),
            "https://github.com/EngineHub/WorldEdit.git",
            MinecraftServerService.BuildSystem.Gradle,
            new LocalizedText("In-game world editor — fast building and terraforming.",
                              "遊戲內世界編輯器 — 快速建築同地形改造。")),
    };
}
