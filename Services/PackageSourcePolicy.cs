using System;

namespace WinForge.Services;

/// <summary>
/// Safe package-source selection policy. A <see cref="PackageItem.Source"/> is not a generic
/// command fragment: every manager has different source semantics, and some displayed values are
/// metadata rather than a selectable registry. This is the one authority that normalizes a source
/// before command preview, queueing, or execution.
///
/// 安全套件來源揀選政策。<see cref="PackageItem.Source"/> 唔係可以直接插入指令嘅文字：
/// 每個管理器對來源嘅意思都唔同，而部分顯示值只係中繼資料，唔係可揀登記處。所有指令預覽、
/// 排隊同執行之前，都要先由呢度正規化來源。
/// </summary>
public static class PackageSourcePolicy
{
    /// <summary>
    /// A validated package reference plus the exact source suffix that a manager understands.
    /// <see cref="CommandSuffix"/> is always assembled from a fixed flag and either a strict token
    /// or a trusted constant; callers must never append the original source text themselves.
    /// </summary>
    public sealed record Resolution(string NormalizedSource, string PackageId, string CommandSuffix);

    /// <summary>Known source labels that are not installable remote sources.</summary>
    public static bool IsExplicitlyIncompatible(string? source)
    {
        var value = (source ?? "").Trim().ToLowerInvariant();
        return value is "local" or "local pc" or "localpc" or "unknown" or "msstore-fallback";
    }

    /// <summary>
    /// Resolve a package source for one manager and operation. Failure codes deliberately never
    /// contain caller-controlled text, so they are safe for UI/status reporting.
    /// </summary>
    public static bool TryResolve(string? managerKey, string? packageId, string? source,
        PackageOperations.Op operation, out Resolution resolution, out string failureCode)
    {
        var key = (managerKey ?? "").Trim().ToLowerInvariant();
        var id = (packageId ?? "").Trim();
        var requested = (source ?? "").Trim();
        resolution = new Resolution("", id, "");
        failureCode = "";

        // An absent source preserves each manager's normal configured/default behaviour.
        if (requested.Length == 0) return true;
        if (IsExplicitlyIncompatible(requested))
        {
            failureCode = "source-local-or-unknown";
            return false;
        }

        switch (key)
        {
            // WinGet and Chocolatey select a registered source by name. SourceManager registers
            // names independently; only a narrow, cmd-safe token is allowed on this boundary.
            case "winget":
            case "choco":
                return TryToken(requested, id, "--source", out resolution, out failureCode);

            // Scoop represents a bucket by qualifying the package reference, not by accepting an
            // arbitrary --source argument. Installation needs that qualifier; installed/update rows
            // retain a validated bucket as metadata because Scoop has no separate source switch.
            case "scoop":
                return TryResolveScoop(requested, id, operation, out resolution, out failureCode);

            // These public/default labels map to fixed, reviewed endpoints. A bundle cannot turn a
            // display Source into an arbitrary index URL or registry URL.
            case "pip":
                return TryTrustedDefault(requested, id, operation,
                    new[] { "pypi", "pypi.org", "https://pypi.org/simple", "https://pypi.org/simple/" },
                    "pypi.org", "--index-url https://pypi.org/simple", out resolution, out failureCode);
            case "npm":
            case "bun":
                return TryTrustedDefault(requested, id, operation,
                    new[] { "npm", "npmjs.org", "registry.npmjs.org", "https://registry.npmjs.org", "https://registry.npmjs.org/" },
                    "npmjs.org", "--registry https://registry.npmjs.org/", out resolution, out failureCode);
            case "dotnet":
                return TryTrustedDefault(requested, id, operation,
                    new[] { "nuget", "nuget.org", "https://api.nuget.org/v3/index.json" },
                    "nuget.org", "--add-source https://api.nuget.org/v3/index.json", out resolution, out failureCode);
            case "cargo":
                return TryTrustedDefault(requested, id, operation,
                    new[] { "crates.io", "crates-io" },
                    "crates.io", "--registry crates-io", out resolution, out failureCode);

            // PowerShellGet only exposes -Repository for installation. PSResourceGet exposes it
            // for install/update. The no-selector operations retain a validated repository as queue
            // identity metadata instead of silently dropping it or regressing normal uninstall rows.
            case "psgallery":
                return TryToken(requested, id,
                    operation == PackageOperations.Op.Install ? "-Repository" : "",
                    out resolution, out failureCode);
            case "pwsh7":
                return TryToken(requested, id,
                    operation == PackageOperations.Op.Uninstall ? "" : "-Repository",
                    out resolution, out failureCode);

            // WinForge currently puts vcpkg's target triplet in Source for update rows. It is not a
            // remote source switch: validate that it agrees with the id, retain it in queue identity,
            // and deliberately emit no fake registry argument.
            case "vcpkg":
                return TryResolveVcpkgTriplet(requested, id, out resolution, out failureCode);

            default:
                failureCode = "source-unsupported";
                return false;
        }
    }

    /// <summary>Map a policy failure to a stable bilingual message without exposing raw input.</summary>
    public static (string En, string Zh, string Code) MessageFor(string? failureCode) => failureCode switch
    {
        "source-local-or-unknown" => (
            "The package source is local or unknown and cannot be executed.",
            "套件來源係本機或者未知，唔可以執行。",
            "source-local-or-unknown"),
        "source-operation-unsupported" => (
            "The selected package source cannot be used for this operation.",
            "所揀套件來源唔可以用喺呢個操作。",
            "source-operation-unsupported"),
        "source-id-mismatch" => (
            "The package source does not match the package reference.",
            "套件來源同套件參照唔相符。",
            "source-id-mismatch"),
        "source-unsupported" => (
            "This package manager does not support the selected package source.",
            "呢個套件管理器唔支援所揀嘅套件來源。",
            "source-unsupported"),
        _ => (
            "The package source contains unsafe or unsupported characters.",
            "套件來源包含唔安全或者唔支援嘅字元。",
            "invalid-package-source"),
    };

    /// <summary>
    /// Stable, delimiter-safe identity for selection and de-duplication. Invalid source text never
    /// becomes part of the key; it will be rejected at the command boundary, while UI selection can
    /// still remain deterministic and harmless.
    /// </summary>
    public static string IdentityKey(string? managerKey, string? packageId, string? source,
        PackageOperations.Op operation)
    {
        var manager = (managerKey ?? "").Trim().ToLowerInvariant();
        var id = (packageId ?? "").Trim();
        var normalizedSource = "invalid";
        if (TryResolve(manager, id, source, operation, out var resolution, out _))
            normalizedSource = resolution.NormalizedSource;
        return $"manager={manager.Length}:{manager}|id={id.Length}:{id}|source={normalizedSource.Length}:{normalizedSource}";
    }

    private static bool TryToken(string requested, string id, string flag,
        out Resolution resolution, out string failureCode)
    {
        resolution = new Resolution("", id, "");
        if (!IsSafeToken(requested))
        {
            failureCode = "invalid-package-source";
            return false;
        }
        // Construct the command value only after the source has satisfied the strict token rule.
        resolution = new Resolution(requested.ToLowerInvariant(), id,
            flag.Length == 0 ? "" : $"{flag} {requested}");
        failureCode = "";
        return true;
    }

    private static bool TryResolveScoop(string requested, string id, PackageOperations.Op operation,
        out Resolution resolution, out string failureCode)
    {
        resolution = new Resolution("", id, "");
        if (!IsSafeToken(requested))
        {
            failureCode = "invalid-package-source";
            return false;
        }

        // Update/uninstall identify an already installed app. Preserve the validated bucket in the
        // operation identity but do not invent a Scoop source flag where none exists.
        if (operation != PackageOperations.Op.Install)
        {
            resolution = new Resolution(requested.ToLowerInvariant(), id, "");
            failureCode = "";
            return true;
        }

        var slash = id.IndexOf('/');
        if (slash < 0)
        {
            resolution = new Resolution(requested.ToLowerInvariant(), $"{requested}/{id}", "");
            failureCode = "";
            return true;
        }

        // A Scoop package reference has at most one bucket separator; preserve a matching one,
        // never overwrite an already-qualified reference with another bucket.
        if (slash == 0 || slash != id.LastIndexOf('/')
            || !string.Equals(id[..slash], requested, StringComparison.OrdinalIgnoreCase))
        {
            failureCode = "source-id-mismatch";
            return false;
        }
        resolution = new Resolution(requested.ToLowerInvariant(), id, "");
        failureCode = "";
        return true;
    }

    private static bool TryTrustedDefault(string requested, string id, PackageOperations.Op operation,
        string[] accepted, string normalized, string suffix,
        out Resolution resolution, out string failureCode)
    {
        resolution = new Resolution("", id, "");
        foreach (var candidate in accepted)
            if (string.Equals(requested, candidate, StringComparison.OrdinalIgnoreCase))
            {
                // Uninstall commands for these managers do not expose a source selector. The source
                // remains validated queue identity metadata, which preserves normal installed rows.
                resolution = new Resolution(normalized, id,
                    operation == PackageOperations.Op.Uninstall ? "" : suffix);
                failureCode = "";
                return true;
            }
        failureCode = "source-unsupported";
        return false;
    }

    private static bool TryResolveVcpkgTriplet(string requested, string id,
        out Resolution resolution, out string failureCode)
    {
        resolution = new Resolution("", id, "");
        if (!IsSafeToken(requested))
        {
            failureCode = "invalid-package-source";
            return false;
        }

        var colon = id.LastIndexOf(':');
        if (colon < 0)
        {
            if (!string.Equals(requested, "vcpkg", StringComparison.OrdinalIgnoreCase))
            {
                failureCode = "source-id-mismatch";
                return false;
            }
        }
        else if (colon == id.Length - 1
                 || !string.Equals(id[(colon + 1)..], requested, StringComparison.OrdinalIgnoreCase))
        {
            failureCode = "source-id-mismatch";
            return false;
        }

        resolution = new Resolution(requested.ToLowerInvariant(), id, "");
        failureCode = "";
        return true;
    }

    /// <summary>Strict token form that cmd.exe and PowerShell cannot reinterpret as syntax.</summary>
    private static bool IsSafeToken(string value)
    {
        if (value.Length is 0 or > 64 || !char.IsLetterOrDigit(value[0])) return false;
        foreach (var c in value)
            if (!(char.IsLetterOrDigit(c) || c is '.' or '_' or '-')) return false;
        return true;
    }
}
