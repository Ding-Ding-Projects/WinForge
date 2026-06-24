using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 建立媒體操作 · Factory for ffmpeg/ffprobe operations on the selected media file.
/// 由 <see cref="MediaOperations"/> 目錄使用。Used by the generated <see cref="MediaOperations"/> catalog.
/// </summary>
public static class MediaTweak
{
    /// <summary>執行一條 ffmpeg/ffprobe 指令（args 內可用 {in} {out} 佔位符）。</summary>
    public static TweakDefinition Op(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string ffArgs, bool needsOutput = true, bool useProbe = false, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => MediaService.RunArgs(ffArgs, needsOutput, useProbe, ct),
            requiresAdmin: false, destructive: false, keywords: keywords);
}
