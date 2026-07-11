using System.Diagnostics;

namespace WinForge.Services;

internal static class ShellRunner
{
    public static string ResolveExe(string tool)
    {
        var info = new ProcessStartInfo
        {
            FileName = "where.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add(tool);
        using var process = Process.Start(info);
        if (process is null) return tool;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? tool;
    }
}
