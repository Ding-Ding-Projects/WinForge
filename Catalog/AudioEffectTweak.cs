using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 建立音訊效果操作 · Factory for Audio-Editor effect operations rendered as control rows.
/// 每個操作喺 <see cref="AppState.CurrentAudioClip"/> 上跑一條 ffmpeg -af 濾鏡，成功就將結果換做新嘅工作 clip。
/// Each op runs an ffmpeg audio filter on the editor's current clip and, on success, swaps the produced
/// scratch WAV in as the new working clip — so applying ops chains like an edit history.
/// </summary>
public static class AudioEffectTweak
{
    /// <summary>一個 -af 濾鏡效果 · An effect expressed as a single ffmpeg audio-filter string.</summary>
    public static TweakDefinition Filter(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string filter, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => ApplyFilter(filter, ct),
            requiresAdmin: false, destructive: false, keywords: keywords);

    /// <summary>用任意 ffmpeg 參數嘅效果（{in}/{out} 佔位符）· An effect using arbitrary ffmpeg args.</summary>
    public static TweakDefinition Raw(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string args, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => ApplyRaw(args, ct),
            requiresAdmin: false, destructive: false, keywords: keywords);

    private static async Task<TweakResult> ApplyFilter(string filter, CancellationToken ct)
    {
        var (r, path) = await FfmpegAudioService.ApplyFilterAsync(AppState.CurrentAudioClip, filter, ct);
        if (r.Success && path is not null) AppState.CurrentAudioClip = path;
        return r;
    }

    private static async Task<TweakResult> ApplyRaw(string args, CancellationToken ct)
    {
        var (r, path) = await FfmpegAudioService.RunAsync(AppState.CurrentAudioClip, args, ct);
        if (r.Success && path is not null) AppState.CurrentAudioClip = path;
        return r;
    }
}
