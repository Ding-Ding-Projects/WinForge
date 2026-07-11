namespace WinForge.Models
{
    public sealed record TweakResult(bool Success)
    {
        public static TweakResult Ok(string en, string zh) => new(true);
        public static TweakResult Fail(string en, string zh) => new(false);
    }
}

namespace WinForge.Services
{
    public static class MediaService
    {
        public static bool IsInstalled { get; set; }
        public static string FFmpeg { get; set; } = "";
    }
}
