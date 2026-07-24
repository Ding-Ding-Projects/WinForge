using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>Executes argv-only archive plans and owns the integrity-before-source-removal gate.</summary>
public static class ArchiveWorkflowService
{
    public static async Task<TweakResult> CreateAsync(ArchiveCreateOptions options, CancellationToken ct = default)
    {
        if (!ArchiveService.HasArchive)
            return TweakResult.Fail("No archive selected.", "未揀壓縮檔。");
        if (!ArchiveService.HasSource)
            return TweakResult.Fail("No source file/folder selected.", "未揀來源檔案／資料夾。");
        if (!File.Exists(ArchiveService.Source) && !Directory.Exists(ArchiveService.Source))
            return TweakResult.Fail("The selected source no longer exists.", "揀選嘅來源已經唔存在。");

        ArchiveCreatePlan plan;
        try { plan = ArchiveWorkflowCore.BuildCreatePlan(ArchiveService.Archive, ArchiveService.Source, options); }
        catch (Exception ex)
        {
            return TweakResult.Fail(
                ex.Message,
                "壓縮設定無效；請檢查路徑、格式、樣式同分卷大小。",
                ex.Message);
        }

        var create = await Run(plan.CreateArguments, plan.ArchivePath, ct);
        if (!create.Success || !plan.MoveSourceAfterIntegrityTest) return create;

        var integrity = await Run(plan.IntegrityArguments, plan.ArchivePath, ct);
        if (!integrity.Success)
            return TweakResult.Fail(
                "Archive integrity verification failed; the source was retained.",
                "壓縮檔完整性驗證失敗；來源已保留。",
                integrity.Output);

        try
        {
            MoveToRecycleBin(plan.SourcePath);
            return TweakResult.Ok(
                "Archive passed its integrity test; the source was moved to the Recycle Bin.",
                "壓縮檔通過完整性測試；來源已移到回收筒。",
                (create.Output + "\n" + integrity.Output).Trim());
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(
                "The archive is valid, but the source could not be moved to the Recycle Bin.",
                "壓縮檔有效，但來源未能移到回收筒。",
                ex.Message);
        }
    }

    public static Task<TweakResult> DeleteEntriesAsync(IReadOnlyList<string> masks, bool recursive, CancellationToken ct = default)
    {
        if (!ArchiveService.HasArchive)
            return Task.FromResult(TweakResult.Fail("No archive selected.", "未揀壓縮檔。"));
        try
        {
            var arguments = ArchiveWorkflowCore.BuildDeleteArguments(ArchiveService.Archive, masks, recursive);
            return Run(arguments, ArchiveService.Archive, ct);
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Fail(
                ex.Message,
                "刪除設定無效；請檢查壓縮檔同相對項目樣式。",
                ex.Message));
        }
    }

    private static Task<TweakResult> Run(IReadOnlyList<string> arguments, string archivePath, CancellationToken ct)
        => ShellRunner.RunArgumentsStreaming(
            ArchiveService.SevenZip,
            arguments,
            onLine: null,
            elevated: false,
            workingDirectory: Path.GetDirectoryName(archivePath),
            ct: ct);

    private static void MoveToRecycleBin(string source)
    {
        if (File.Exists(source))
        {
            FileSystem.DeleteFile(source, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }
        if (Directory.Exists(source))
        {
            FileSystem.DeleteDirectory(source, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            return;
        }
        throw new FileNotFoundException("The source disappeared before it could be moved.", source);
    }
}
