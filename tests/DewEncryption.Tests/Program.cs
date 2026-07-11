using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinForge.Services;

internal static class Program
{
    private static int _passed;
    private static int _failed;
    private static int _skipped;
    private static readonly string TestRoot = Path.Combine(Path.GetTempPath(), $"WinForge-Dew-Tests-{Guid.NewGuid():N}");

    private static async Task<int> Main(string[] args)
    {
        Directory.CreateDirectory(TestRoot);
        var tests = new (string Name, Func<Task> Body)[]
        {
            ("single file path mapping", TestSingleFilePathMapping),
            ("drive-root containment", TestDriveRootContainment),
            ("duplicate top-level names rejected", TestDuplicateNames),
            ("unsupported nested metadata is rejected safely", TestUnsupportedMetadataRejected),
            ("dangling restore links are rejected", TestDanglingRestoreLink),
            ("root restore rolls back on type mismatch", TestRootRestoreRollback),
            ("snapshot, no-change, history and details", TestSnapshotHistory),
            ("ignored files are force-added", TestIgnoredFilesAreCaptured),
            ("empty folder has compatible no-HEAD history", TestEmptyFolderCompatibility),
            ("restore creates safe in-place result", TestRestoreRoundTrip),
            ("invalid commit cannot change source", TestInvalidCommit),
            ("deleted target reopens and restores", TestDeletedTargetAfterRestart),
            ("extracted repository restores beside itself", TestExtractedRepositoryLayout),
            ("multi-source reopen requires explicit target confirmation", TestMultiSourceConfirmation),
            ("SHA-256 Dew repository history", TestSha256Repository),
            ("pinned upstream snapshot interoperability", TestUpstreamInteroperability),
            ("executable Git config is rejected", TestDangerousGitConfiguration),
            ("auto-history debounce records a change", TestWatcherDebounce),
            ("encrypted 7z round trip and hidden names", TestEncryptedArchive),
            ("unencrypted archive has compatible root", TestUnencryptedArchiveRoot),
        };

        var selectedTests = args.Length == 0
            ? tests
            : tests.Where(test => args.Any(filter =>
                test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (selectedTests.Length == 0)
        {
            Console.Error.WriteLine("No Dew Encryption tests matched the supplied filter.");
            return 2;
        }

        try
        {
            foreach (var test in selectedTests) await Run(test.Name, test.Body);
        }
        finally { DeleteTree(TestRoot); }

        Console.WriteLine($"\nDew Encryption tests: {_passed} passed, {_skipped} skipped, {_failed} failed ({selectedTests.Length} total)");
        return _failed == 0 ? 0 : 1;
    }

    private static async Task Run(string name, Func<Task> body)
    {
        Console.WriteLine($"RUN   {name}");
        try
        {
            await body();
            _passed++;
            Console.WriteLine($"PASS  {name}");
        }
        catch (SkipTestException ex)
        {
            _skipped++;
            Console.WriteLine($"SKIP  {name}\n      {ex.Message}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"FAIL  {name}\n      {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Task TestSingleFilePathMapping()
    {
        var root = NewCase();
        var file = Path.Combine(root, "hello.txt");
        File.WriteAllText(file, "hello");
        var project = DewSnapshotCore.CreateProject([file]);
        Equal(file, project.Root);
        Equal(Path.Combine(root, DewSnapshotCore.ArchiveDirectoryName), project.ArchiveDirectory);
        Equal(Path.Combine(project.ArchiveDirectory, DewSnapshotCore.RepositoryDirectoryName), project.RepositoryDirectory);
        return Task.CompletedTask;
    }

    private static Task TestDriveRootContainment()
    {
        var drive = Path.GetPathRoot(TestRoot)!;
        True(DewSnapshotCore.IsSameOrDescendant(TestRoot, drive), "A path must be inside its drive root.");
        True(!DewSnapshotCore.IsSameOrDescendant(drive, TestRoot), "A drive root is not inside a child path.");
        return Task.CompletedTask;
    }

    private static Task TestDuplicateNames()
    {
        var root = NewCase();
        var left = Path.Combine(root, "left");
        var right = Path.Combine(root, "right");
        Directory.CreateDirectory(left);
        Directory.CreateDirectory(right);
        var a = Path.Combine(left, "same.txt");
        var b = Path.Combine(right, "same.txt");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        Throws<InvalidOperationException>(() => DewSnapshotCore.CreateProject([a, b]));
        return Task.CompletedTask;
    }

    private static Task TestUnsupportedMetadataRejected()
    {
        var root = NewCase();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "kept.txt"), "kept");
        var project = DewSnapshotCore.CreateProject([source]);
        Directory.CreateDirectory(project.RepositoryDirectory);
        DewSnapshotCore.ReplaceWorkingTree(project);
        var captured = Path.Combine(project.RepositoryDirectory, "files", "source");
        True(File.Exists(Path.Combine(captured, "kept.txt")), "Normal content was not copied.");
        Directory.CreateDirectory(Path.Combine(source, ".git"));
        File.WriteAllText(Path.Combine(source, ".git", "secret"), "must not disappear");
        Throws<InvalidOperationException>(() => DewSnapshotCore.ReplaceWorkingTree(project));
        True(File.Exists(Path.Combine(captured, "kept.txt")), "Rejected snapshot replaced the prior working tree.");
        return Task.CompletedTask;
    }

    private static Task TestRootRestoreRollback()
    {
        var root = NewCase();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "live.txt"), "live");
        var archive = Path.Combine(source, DewSnapshotCore.ArchiveDirectoryName);
        Directory.CreateDirectory(archive);
        File.WriteAllText(Path.Combine(archive, "history-marker"), "keep");
        var stagedWrongType = Path.Combine(root, "staged-file");
        File.WriteAllText(stagedWrongType, "wrong");

        Throws<InvalidDataException>(() =>
            DewSnapshotCore.RestoreSingleSelection(stagedWrongType, source, archive));
        Equal("live", File.ReadAllText(Path.Combine(source, "live.txt")));
        Equal("keep", File.ReadAllText(Path.Combine(archive, "history-marker")));
        return Task.CompletedTask;
    }

    private static Task TestDanglingRestoreLink()
    {
        var root = NewCase();
        var staged = Path.Combine(root, "staged");
        var target = Path.Combine(root, "target-link");
        var missingLinkTarget = Path.Combine(root, "missing-target");
        Directory.CreateDirectory(staged);
        File.WriteAllText(Path.Combine(staged, "data.txt"), "must stay contained");
        try { Directory.CreateSymbolicLink(target, missingLinkTarget); }
        catch (UnauthorizedAccessException) { throw new SkipTestException("Creating a test symbolic link is not permitted."); }
        catch (PlatformNotSupportedException) { throw new SkipTestException("Symbolic links are unavailable."); }

        try
        {
            True(DewSnapshotCore.EntryExistsNoFollow(target), "A dangling link was treated as absent.");
            Throws<InvalidDataException>(() => DewSnapshotCore.RestoreSingleSelection(staged, target,
                Path.Combine(root, DewSnapshotCore.ArchiveDirectoryName)));
            True(!File.Exists(Path.Combine(missingLinkTarget, "data.txt")),
                "Restore followed a dangling link outside its target.");
        }
        finally { try { Directory.Delete(target); } catch { } }
        return Task.CompletedTask;
    }

    private static async Task TestSnapshotHistory()
    {
        var (_, source, project) = MakeFolderProject("v1");
        var first = await DewEncryptionService.TakeSnapshotAsync(project, "first");
        True(first.Changed, "Initial snapshot was not committed.");
        var same = await DewEncryptionService.TakeSnapshotAsync(project, "same");
        True(!same.Changed, "Unchanged content created a commit.");
        File.WriteAllText(Path.Combine(source, "data.txt"), "v2");
        var second = await DewEncryptionService.TakeSnapshotAsync(project, "second");
        True(second.Changed, "Changed content was not committed.");

        var history = await DewEncryptionService.ListHistoryAsync(project);
        True(history.Count == 2, $"Expected two commits, got {history.Count}.");
        Equal("second", history[0].Subject);
        var details = await DewEncryptionService.GetCommitDetailsAsync(project, history[0].Hash);
        True(details.Changes.Any(c => c.Path.EndsWith("data.txt", StringComparison.Ordinal)),
            "Changed file was absent from commit details.");
        True(File.Exists(Path.Combine(project.RepositoryDirectory, "files", Path.GetFileName(source), "data.txt")),
            "Compatible repo working tree is missing.");
    }

    private static async Task TestIgnoredFilesAreCaptured()
    {
        var (_, source, project) = MakeFolderProject("visible");
        File.WriteAllText(Path.Combine(source, ".gitignore"), "secret.txt\nignored-by-info.txt\n");
        File.WriteAllText(Path.Combine(source, "secret.txt"), "must be tracked");
        await DewEncryptionService.TakeSnapshotAsync(project, "forced ignore test");
        var fromGit = await RunProcess("git", project.RepositoryDirectory,
            ["show", "HEAD:files/source/secret.txt"]);
        True(fromGit.Contains("must be tracked", StringComparison.Ordinal), ".gitignore excluded snapshot data.");

        File.WriteAllText(Path.Combine(source, "ignored-by-info.txt"), "also tracked");
        File.WriteAllText(Path.Combine(project.RepositoryDirectory, ".git", "info", "exclude"),
            "**/ignored-by-info.txt\n");
        await DewEncryptionService.TakeSnapshotAsync(project, "info exclude test");
        fromGit = await RunProcess("git", project.RepositoryDirectory,
            ["show", "HEAD:files/source/ignored-by-info.txt"]);
        True(fromGit.Contains("also tracked", StringComparison.Ordinal), ".git/info/exclude excluded snapshot data.");
    }

    private static async Task TestEmptyFolderCompatibility()
    {
        var root = NewCase();
        var source = Path.Combine(root, "empty");
        Directory.CreateDirectory(source);
        var project = DewSnapshotCore.CreateProject([source]);
        var snapshot = await DewEncryptionService.TakeSnapshotAsync(project, "empty");
        Equal("", snapshot.Commit);
        True(!snapshot.Changed, "An empty folder unexpectedly produced a Git commit.");
        var opened = await DewEncryptionService.OpenExistingProjectAsync(project.RepositoryDirectory);
        Equal(source, opened.RestoreTargets.Single());
        Equal(0, (await DewEncryptionService.ListHistoryAsync(opened)).Count);
    }

    private static async Task TestRestoreRoundTrip()
    {
        var (_, source, project) = MakeFolderProject("original");
        var first = await DewEncryptionService.TakeSnapshotAsync(project, "original");
        var initialHash = (await DewEncryptionService.ListHistoryAsync(project)).Single().Hash;
        File.WriteAllText(Path.Combine(source, "data.txt"), "new state");
        await DewEncryptionService.TakeSnapshotAsync(project, "new");
        File.WriteAllText(Path.Combine(source, "unsaved.txt"), "safety");

        await DewEncryptionService.RestoreAsync(project, source, initialHash);
        Equal("original", File.ReadAllText(Path.Combine(source, "data.txt")));
        True(!File.Exists(Path.Combine(source, "unsaved.txt")), "Restore did not remove post-snapshot content.");
        var history = await DewEncryptionService.ListHistoryAsync(project);
        True(history.Any(h => h.Subject.Contains("safety snapshot", StringComparison.OrdinalIgnoreCase)),
            "Restore did not commit the unsaved pre-restore state.");
        True(!string.IsNullOrWhiteSpace(first.Commit));
    }

    private static async Task TestInvalidCommit()
    {
        var (_, source, project) = MakeFolderProject("unchanged");
        await DewEncryptionService.TakeSnapshotAsync(project, "initial");
        var before = Hash(Path.Combine(source, "data.txt"));
        await ThrowsAsync<DewOperationException>(() => DewEncryptionService.RestoreAsync(project, source, "deadbeef"));
        Equal(before, Hash(Path.Combine(source, "data.txt")));
    }

    private static async Task TestDeletedTargetAfterRestart()
    {
        var root = NewCase();
        var source = Path.Combine(root, "recover.txt");
        File.WriteAllText(source, "recover me");
        var project = DewSnapshotCore.CreateProject([source]);
        await DewEncryptionService.TakeSnapshotAsync(project, "exists");
        var original = (await DewEncryptionService.ListHistoryAsync(project)).Single().Hash;
        File.Delete(source);
        await DewEncryptionService.TakeSnapshotAsync(project, "deleted");

        var reopened = await DewEncryptionService.OpenExistingProjectAsync(project.RepositoryDirectory);
        Equal(source, reopened.RestoreTargets.Single());
        await DewEncryptionService.RestoreAsync(reopened, source, original);
        Equal("recover me", File.ReadAllText(source));
    }

    private static async Task TestExtractedRepositoryLayout()
    {
        var (_, _, project) = MakeFolderProject("portable");
        await DewEncryptionService.TakeSnapshotAsync(project, "portable");
        var commit = (await DewEncryptionService.ListHistoryAsync(project)).Single().Hash;
        var extracted = Path.Combine(NewCase(), DewSnapshotCore.RepositoryDirectoryName);
        CopyTree(project.RepositoryDirectory, extracted);

        var opened = await DewEncryptionService.OpenExistingProjectAsync(extracted);
        var target = opened.RestoreTargets.Single();
        Equal(Path.Combine(Path.GetDirectoryName(extracted)!, "source"), target);
        True(!File.Exists(Path.Combine(target, "data.txt")), "Extracted target unexpectedly existed before restore.");
        await DewEncryptionService.RestoreAsync(opened, target, commit);
        Equal("portable", File.ReadAllText(Path.Combine(target, "data.txt")));
        await ThrowsAsync<DewOperationException>(() => DewEncryptionService.RestoreAsync(opened, target, commit));
        Equal("portable", File.ReadAllText(Path.Combine(target, "data.txt")));
    }

    private static async Task TestMultiSourceConfirmation()
    {
        var root = NewCase();
        var first = Path.Combine(root, "first.txt");
        var second = Path.Combine(root, "second.txt");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        var project = DewSnapshotCore.CreateProject([first, second]);
        await DewEncryptionService.TakeSnapshotAsync(project, "multi");
        var reopened = await DewEncryptionService.OpenExistingProjectAsync(project.RepositoryDirectory);
        True(reopened.RequiresExplicitRestoreTargetConfirmation,
            "Reopened multi-source history did not mark its inferred targets.");
        var commit = (await DewEncryptionService.ListHistoryAsync(reopened)).Single().Hash;
        await ThrowsAsync<DewOperationException>(() =>
            DewEncryptionService.RestoreAsync(reopened, reopened.RestoreTargets[0], commit));
    }

    private static async Task TestSha256Repository()
    {
        var (_, source, project) = MakeFolderProject("sha256");
        Directory.CreateDirectory(project.RepositoryDirectory);
        var initError = await CaptureException(() => RunProcess("git", project.RepositoryDirectory,
            ["init", "--object-format=sha256"]));
        if (initError is not null) throw new SkipTestException("This Git build does not support SHA-256 repositories.");
        await RunProcess("git", project.RepositoryDirectory, ["config", "user.name", "Dew Encryption"]);
        await RunProcess("git", project.RepositoryDirectory, ["config", "user.email", "dew-encryption@local"]);
        var captured = Path.Combine(project.RepositoryDirectory, DewSnapshotCore.WorkingTreeDirectoryName, "source");
        Directory.CreateDirectory(captured);
        File.Copy(Path.Combine(source, "data.txt"), Path.Combine(captured, "data.txt"));
        await RunProcess("git", project.RepositoryDirectory, ["add", "-A"]);
        await RunProcess("git", project.RepositoryDirectory, ["commit", "-m", "sha256 snapshot"]);

        var history = await DewEncryptionService.ListHistoryAsync(project);
        True(history.Count == 1 && history[0].Hash.Length == 64,
            "SHA-256 Dew history did not return a 64-character commit ID.");
    }

    private static async Task TestUpstreamInteroperability()
    {
        var python = FindOnPath("py") ?? FindOnPath("python");
        if (python is null) throw new SkipTestException("Python is unavailable.");
        var pythonPrefix = Path.GetFileNameWithoutExtension(python)
            .Equals("py", StringComparison.OrdinalIgnoreCase) ? new[] { "-3" } : Array.Empty<string>();
        var upstream = Path.GetFullPath(Path.Combine("ThirdParty", "DewEncryption"));
        if (!Directory.Exists(upstream)) throw new SkipTestException("Pinned upstream source is unavailable.");

        var root = NewCase();
        var source = Path.Combine(root, "interop");
        Directory.CreateDirectory(source);
        var file = Path.Combine(source, "shared.txt");
        File.WriteAllText(file, "from upstream");
        string pySource = JsonSerializer.Serialize(source);
        await RunProcess(python, upstream, pythonPrefix.Concat(new[]
        {
            "-B", "-c",
            $"from pathlib import Path; from dew_encryption.core import snapshot; print(snapshot([Path({pySource})]).commit)",
        }).ToArray());

        var project = DewSnapshotCore.CreateProject([source]);
        var upstreamHistory = await DewEncryptionService.ListHistoryAsync(project);
        True(upstreamHistory.Count == 1, "WinForge could not read the upstream snapshot.");
        var upstreamCommit = upstreamHistory[0].Hash;

        File.WriteAllText(file, "changed before WinForge restore");
        await DewEncryptionService.RestoreAsync(project, source, upstreamCommit);
        Equal("from upstream", File.ReadAllText(file));

        File.WriteAllText(file, "from WinForge");
        var winForgeSnapshot = await DewEncryptionService.TakeSnapshotAsync(project);
        True(!string.IsNullOrWhiteSpace(winForgeSnapshot.Commit), "WinForge did not create an interop snapshot.");
        string pyRepo = JsonSerializer.Serialize(project.RepositoryDirectory);
        var upstreamRead = await RunProcess(python, upstream, pythonPrefix.Concat(new[]
        {
            "-B", "-c",
            $"from pathlib import Path; from dew_encryption.core import history; print(len(history(Path({pyRepo}))))",
        }).ToArray());
        True(upstreamRead.Contains("3", StringComparison.Ordinal), "Upstream could not read WinForge history.");

        File.WriteAllText(file, "changed before upstream restore");
        string pyCommit = JsonSerializer.Serialize(winForgeSnapshot.Commit);
        await RunProcess(python, upstream, pythonPrefix.Concat(new[]
        {
            "-B", "-c",
            $"from pathlib import Path; from dew_encryption.core import restore_commit; restore_commit(Path({pyRepo}), {pyCommit}, Path({pySource}))",
        }).ToArray());
        Equal("from WinForge", File.ReadAllText(file));
    }

    private static async Task TestDangerousGitConfiguration()
    {
        var (_, _, project) = MakeFolderProject("safe");
        await DewEncryptionService.TakeSnapshotAsync(project, "initial");
        await RunProcess("git", project.RepositoryDirectory, ["config", "log.showSignature", "true"]);
        await ThrowsAsync<DewOperationException>(() => DewEncryptionService.ListHistoryAsync(project));
        await RunProcess("git", project.RepositoryDirectory, ["config", "--unset", "log.showSignature"]);
        await RunProcess("git", project.RepositoryDirectory, ["config", "core.hooksPath", "hooks-owned-by-attacker"]);
        await ThrowsAsync<DewOperationException>(() => DewEncryptionService.TakeSnapshotAsync(project, "blocked"));
    }

    private static async Task TestWatcherDebounce()
    {
        var (_, source, project) = MakeFolderProject("one");
        await DewEncryptionService.TakeSnapshotAsync(project, "initial");
        var tcs = new TaskCompletionSource<DewSnapshotResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new DewHistoryWatcher(project, TimeSpan.FromMilliseconds(500));
        watcher.SnapshotCompleted += (_, e) =>
        {
            if (e.Error is not null) tcs.TrySetException(e.Error);
            else if (e.Result?.Changed == true) tcs.TrySetResult(e.Result);
        };
        watcher.Start();
        File.WriteAllText(Path.Combine(source, "data.txt"), "two");
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(12)));
        True(completed == tcs.Task, "Watcher did not commit within the timeout.");
        await tcs.Task;
    }

    private static async Task TestEncryptedArchive()
    {
        var sevenZip = DewEncryptionService.FindSevenZip();
        if (sevenZip is null) throw new SkipTestException("7-Zip is unavailable.");

        var (_, source, project) = MakeFolderProject("secret body");
        var password = "  Pāss\"%&|^<>!測試🙂  ";
        var result = await DewEncryptionService.CreateArchiveAsync(project, password, "encrypted");
        True(result.Encrypted && File.Exists(result.ArchivePath), "Encrypted archive was not published.");
        await DewEncryptionService.TestArchiveAsync(result.ArchivePath, password);
        var wrong = await CaptureException(() => DewEncryptionService.TestArchiveAsync(result.ArchivePath, password + "x"));
        True(wrong is DewOperationException, "Wrong password unexpectedly passed.");
        True(!wrong!.Message.Contains(password, StringComparison.Ordinal), "A password leaked into an error.");

        var listing = await RunProcess(sevenZip, Path.GetDirectoryName(result.ArchivePath)!,
            ["l", "-sccUTF-8", "-p-", result.ArchivePath], requireSuccess: false);
        True(!listing.Contains("data.txt", StringComparison.Ordinal), "Header encryption exposed a captured file name.");
        Equal("secret body", File.ReadAllText(Path.Combine(source, "data.txt")));
    }

    private static async Task TestUnencryptedArchiveRoot()
    {
        var sevenZip = DewEncryptionService.FindSevenZip();
        if (sevenZip is null) throw new SkipTestException("7-Zip is unavailable.");

        var (_, source, project) = MakeFolderProject("portable body");
        var result = await DewEncryptionService.CreateArchiveAsync(project, "", "portable");
        True(!result.Encrypted && File.Exists(result.ArchivePath), "Plain archive was not published.");
        await DewEncryptionService.TestArchiveAsync(result.ArchivePath, "");
        var listing = await RunProcess(sevenZip, Path.GetDirectoryName(result.ArchivePath)!,
            ["l", "-ba", "-sccUTF-8", "-p-", result.ArchivePath]);
        True(listing.Contains(DewSnapshotCore.RepositoryDirectoryName, StringComparison.Ordinal),
            "The compatible repository root is absent from the archive.");
        True(!listing.Contains(Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase),
            "The archive exposed an absolute source path.");
    }

    private static (string Root, string Source, DewProjectContext Project) MakeFolderProject(string content)
    {
        var root = NewCase();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "data.txt"), content, new UTF8Encoding(false));
        return (root, source, DewSnapshotCore.CreateProject([source]));
    }

    private static string NewCase()
    {
        var path = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string? FindOnPath(string command)
    {
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var names = Path.HasExtension(command) ? new[] { command } : extensions.Select(ext => command + ext);
        foreach (var rawDirectory in (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = rawDirectory.Trim().Trim('"');
            foreach (var name in names)
            {
                var candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    private static async Task<string> RunProcess(string file, string workingDirectory, IReadOnlyList<string> args,
        bool requireSuccess = true)
    {
        var info = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        foreach (var key in info.Environment.Keys
            .Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
            info.Environment.Remove(key);
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = "NUL";
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync(); } catch { }
            throw new TimeoutException($"{file} did not exit within 30 seconds.");
        }
        var output = (await stdout) + Environment.NewLine + (await stderr);
        if (requireSuccess && process.ExitCode != 0)
            throw new InvalidOperationException($"{file} exited {process.ExitCode}: {output}");
        return output;
    }

    private static async Task<Exception?> CaptureException(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception ex) { return ex; }
    }

    private static void True(bool condition, string message = "Assertion failed.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class SkipTestException(string message) : Exception(message);

    private static void DeleteTree(string path)
    {
        var full = Path.GetFullPath(path);
        var temp = Path.GetFullPath(Path.GetTempPath());
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(full).StartsWith("WinForge-Dew-Tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete an unexpected test path.");
        try { if (Directory.Exists(full)) Directory.Delete(full, recursive: true); } catch { }
    }
}
