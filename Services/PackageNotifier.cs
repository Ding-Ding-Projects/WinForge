using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace WinForge.Services;

/// <summary>
/// 套件更新通知 · Toast notifications for the in-app package manager (UniGetUI parity).
/// 每個通知都受 <see cref="PackageManagerSettings"/> 嘅總開關、細項開關同「每管理器靜音」管制。
/// Every notification is gated by the master switch, the relevant granular toggle, and the
/// per-manager mute set in <see cref="PackageManagerSettings"/>. 用 Windows App SDK 嘅 AppNotifications；
/// 若該平台未提供，會自動 no-op（全程 try/catch），令背景排程永遠唔會因為通知而擲例外。
/// Uses Windows App SDK AppNotifications; if unavailable it degrades to a no-op (all wrapped in try/catch),
/// so the background scheduler never throws because of notifications.
/// </summary>
public static class PackageNotifier
{
    private static bool _registered;
    private static bool _registerFailed;

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>確保通知管理器已註冊（只試一次）· Ensure the notification manager is registered (tried once).</summary>
    private static bool EnsureRegistered()
    {
        if (_registered) return true;
        if (_registerFailed) return false;
        try
        {
            AppNotificationManager.Default.Register();
            _registered = true;
            return true;
        }
        catch
        {
            _registerFailed = true; // 未封裝／無身分／平台缺失 → 之後一律 no-op · degrade to no-op.
            return false;
        }
    }

    private static bool MasterOn => PackageManagerSettings.NotificationsEnabled;

    private static bool ManagerAllowed(string? managerKey)
        => string.IsNullOrEmpty(managerKey) || !PackageManagerSettings.IsManagerMuted(managerKey);

    /// <summary>
    /// Low-level send. The in-app centre is authoritative and remains available when unpackaged
    /// Windows toast registration is unavailable; the operating-system toast is a best-effort mirror.
    /// </summary>
    private static void Show(
        string titleEn,
        string titleZh,
        string bodyEn,
        string bodyZh,
        AppNoticeSeverity severity,
        string key,
        int? autoDismissMs = null)
    {
        try
        {
            AppNotificationService.Publish(new AppNoticeDraft(
                titleEn,
                titleZh,
                bodyEn,
                bodyZh,
                severity,
                Key: key,
                AutoDismissMs: autoDismissMs));

            if (!EnsureRegistered()) return;
            var title = P(titleEn, titleZh);
            var body = P(bodyEn, bodyZh);
            var builder = new AppNotificationBuilder().AddText(title);
            if (!string.IsNullOrEmpty(body)) builder.AddText(body);
            var notification = builder.BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch { /* no-op fallback — keep the build & scheduler green */ }
    }

    // ===== public API =====

    /// <summary>有可用更新 · "N updates are available" (gated by master + UpdatesAvailable toggle).</summary>
    public static void ShowUpdatesAvailable(int count)
    {
        try
        {
            if (count <= 0) return;
            if (!MasterOn || PackageManagerSettings.DisableUpdatesAvailableNotifications) return;
            Show(
                "Updates available",
                "有可用更新",
                count == 1
                    ? "1 update is available."
                    : $"{count} updates are available.",
                count == 1
                    ? "有 1 個更新可用。"
                    : $"有 {count} 個更新可用。",
                AppNoticeSeverity.Informational,
                "package.updates.available");
        }
        catch { }
    }

    /// <summary>正在升級某套件 · "Upgrading <name>…" (progress; gated by master + Progress toggle + mute).</summary>
    public static void ShowUpgrading(string name, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableProgressNotifications || !ManagerAllowed(managerKey)) return;
            Show(
                "Updating package",
                "更新緊套件",
                $"Upgrading {name}…",
                $"升級緊 {name}…",
                AppNoticeSeverity.Progress,
                OperationKey(managerKey, name),
                autoDismissMs: 0);
        }
        catch { }
    }

    /// <summary>一般進度訊息 · A generic progress message (gated by master + Progress toggle + mute).</summary>
    public static void ShowProgress(string message, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableProgressNotifications || !ManagerAllowed(managerKey)) return;
            Show(
                "Package manager",
                "套件管理",
                message,
                message,
                AppNoticeSeverity.Progress,
                OperationKey(managerKey, "progress"));
        }
        catch { }
    }

    /// <summary>操作成功 · Success (gated by master + Success toggle + mute).</summary>
    public static void ShowSuccess(string name, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableSuccessNotifications || !ManagerAllowed(managerKey)) return;
            Show(
                "Update complete",
                "更新完成",
                $"{name} was updated successfully.",
                $"{name} 已成功更新。",
                AppNoticeSeverity.Success,
                OperationKey(managerKey, name));
        }
        catch { }
    }

    /// <summary>操作失敗 · Error (gated by master + Error toggle + mute).</summary>
    public static void ShowError(string name, string? detail = null, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableErrorNotifications || !ManagerAllowed(managerKey)) return;
            var bodyEn = string.IsNullOrWhiteSpace(detail)
                ? $"Failed to update {name}."
                : $"Failed to update {name}: {detail}";
            var bodyZh = string.IsNullOrWhiteSpace(detail)
                ? $"{name} 更新失敗。"
                : $"{name} 更新失敗：{detail}";
            Show(
                "Update failed",
                "更新失敗",
                bodyEn,
                bodyZh,
                AppNoticeSeverity.Error,
                OperationKey(managerKey, name));
        }
        catch { }
    }

    private static string OperationKey(string? managerKey, string name)
        => $"package.operation:{managerKey ?? "all"}:{name}";
}
