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

    /// <summary>低層發送（包住所有錯誤）· Low-level send, swallowing every error.</summary>
    private static void Show(string title, string body)
    {
        try
        {
            if (!EnsureRegistered()) return;
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
                P("Updates available", "有可用更新"),
                count == 1
                    ? P("1 update is available.", "有 1 個更新可用。")
                    : P($"{count} updates are available.", $"有 {count} 個更新可用。"));
        }
        catch { }
    }

    /// <summary>正在升級某套件 · "Upgrading <name>…" (progress; gated by master + Progress toggle + mute).</summary>
    public static void ShowUpgrading(string name, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableProgressNotifications || !ManagerAllowed(managerKey)) return;
            Show(P("Updating package", "更新緊套件"), P($"Upgrading {name}…", $"升級緊 {name}…"));
        }
        catch { }
    }

    /// <summary>一般進度訊息 · A generic progress message (gated by master + Progress toggle + mute).</summary>
    public static void ShowProgress(string message, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableProgressNotifications || !ManagerAllowed(managerKey)) return;
            Show(P("Package manager", "套件管理"), message);
        }
        catch { }
    }

    /// <summary>操作成功 · Success (gated by master + Success toggle + mute).</summary>
    public static void ShowSuccess(string name, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableSuccessNotifications || !ManagerAllowed(managerKey)) return;
            Show(P("Update complete", "更新完成"), P($"{name} was updated successfully.", $"{name} 已成功更新。"));
        }
        catch { }
    }

    /// <summary>操作失敗 · Error (gated by master + Error toggle + mute).</summary>
    public static void ShowError(string name, string? detail = null, string? managerKey = null)
    {
        try
        {
            if (!MasterOn || PackageManagerSettings.DisableErrorNotifications || !ManagerAllowed(managerKey)) return;
            var body = string.IsNullOrWhiteSpace(detail)
                ? P($"Failed to update {name}.", $"{name} 更新失敗。")
                : P($"Failed to update {name}: {detail}", $"{name} 更新失敗：{detail}");
            Show(P("Update failed", "更新失敗"), body);
        }
        catch { }
    }
}
