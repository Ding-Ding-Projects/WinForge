using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 通訊深層連結啟動器 · Communications deep-link launcher.
/// 砌好並啟動 Outlook 草稿 / mailto: / Discord / Teams / Telegram / Slack / Phone Link 深層連結，
/// 全部喺 app 內輸入。永遠唔自動寄（只開草稿／撥號介面）。Bilingual.
/// Builds &amp; launches drafts and protocol deep links from native in-app fields — never auto-sends.
/// </summary>
public sealed partial class CommunicationsModule : Page
{
    public CommunicationsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); ShowOutlookState(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Communications · 通訊";
        HeaderBlurb.Text = P(
            "Build & launch deep links for mail and chat apps — fill in the fields below and click. Everything opens a draft / compose / dialer; nothing is ever sent automatically.",
            "為信件同傾偈 app 砌深層連結 — 填好下面啲格再撳掣。全部只會開草稿／撰寫／撥號介面，永遠唔會自動寄出。");

        // Mail
        MailTitle.Text = P("Mail (Outlook draft & mailto:)", "信件（Outlook 草稿同 mailto:）");
        MailTo.Header = P("To (comma-separated)", "收件人（逗號分隔）");
        MailTo.PlaceholderText = "someone@example.com, other@example.com";
        MailCc.Header = "Cc";
        MailBcc.Header = "Bcc";
        MailSubject.Header = P("Subject", "主旨");
        MailBody.Header = P("Body", "內文");
        MailtoBtn.Content = P("Open mailto: draft (default app)", "開 mailto: 草稿（預設 App）");
        OutlookBtn.Content = P("New Outlook draft (classic)", "開 Outlook 草稿（傳統）");
        AttachLabel.Text = P("Attach a file to a new mail (classic Outlook only)", "為新郵件加附件（只限傳統 Outlook）");
        AttachPath.PlaceholderText = P("No file picked", "未揀檔案");
        AttachBrowseBtn.Content = P("Browse…", "瀏覽…");
        AttachSendBtn.Content = P("New mail with attachment", "開附件郵件");
        FolderLabel.Text = P("Jump to an Outlook folder on launch (classic Outlook only)", "開 Outlook 時跳去資料夾（只限傳統 Outlook）");
        FolderBtn.Content = P("Open folder", "開資料夾");
        BuildFolderBox();

        // Discord
        DiscordTitle.Text = P("Discord", "Discord");
        DiscordGuild.Header = P("Server (guild) ID", "Server（guild）ID");
        DiscordChannel.Header = P("Channel ID (optional)", "頻道 ID（可選）");
        DiscordChannelBtn.Content = P("Open channel", "開頻道");
        DiscordDm.Header = P("DM channel ID", "私訊頻道 ID");
        DiscordDmBtn.Content = P("Open DM", "開私訊");
        DiscordHomeBtn.Content = P("DMs home", "私訊主頁");

        // Teams
        TeamsTitle.Text = P("Microsoft Teams", "Microsoft Teams");
        TeamsUsers.Header = P("Chat with (UPNs/emails, comma-separated)", "同邊個傾（UPN／電郵，逗號分隔）");
        TeamsUsers.PlaceholderText = "joe@contoso.com, bob@contoso.com";
        TeamsTopic.Header = P("Topic name (optional)", "主題名（可選）");
        TeamsMessage.Header = P("Pre-filled message (optional)", "預填訊息（可選）");
        TeamsChatBtn.Content = P("Start chat", "開傾偈");
        TeamsCallBtn.Content = P("Call these users", "打俾佢哋");
        TeamsMeetingLabel.Text = P("Schedule a new meeting", "排個新會議");
        TeamsMtgSubject.Header = P("Meeting subject", "會議主旨");
        TeamsMtgAttendees.Header = P("Attendees (emails, comma-separated)", "與會者（電郵，逗號分隔）");
        TeamsStartDate.Header = P("Start date", "開始日期");
        TeamsStartTime.Header = P("Start time", "開始時間");
        TeamsEndDate.Header = P("End date", "結束日期");
        TeamsEndTime.Header = P("End time", "結束時間");
        TeamsMeetingBtn.Content = P("Open meeting form", "開會議表格");

        // Telegram
        TelegramTitle.Text = P("Telegram", "Telegram");
        TgShareUrl.Header = P("Share a URL (optional)", "分享 URL（可選）");
        TgShareText.Header = P("Text", "文字");
        TgShareBtn.Content = P("Share to Telegram", "分享去 Telegram");
        TgUsername.Header = P("Open chat by @username", "開傾偈（@使用者名稱）");
        TgPost.Header = P("Post # (opt.)", "貼文 #（可選）");
        TgResolveBtn.Content = P("Open chat", "開傾偈");

        // Slack
        SlackTitle.Text = P("Slack", "Slack");
        SlackTeam.Header = P("Team ID (Txxxx)", "Team ID（Txxxx）");
        SlackChannel.Header = P("Channel ID (Cxxxx)", "頻道 ID（Cxxxx）");
        SlackChannelBtn.Content = P("Open channel", "開頻道");
        SlackUser.Header = P("User ID (Uxxxx) for DM", "使用者 ID（Uxxxx）開私訊");
        SlackDmBtn.Content = P("Open DM", "開私訊");
        SlackOpenBtn.Content = P("Focus workspace", "對焦 workspace");

        // Phone Link
        PhoneTitle.Text = P("Phone Link (tel: / sms:)", "Phone Link（tel: / sms:）");
        PhoneNumber.Header = P("Phone number", "電話號碼");
        PhoneNumber.PlaceholderText = "+18005551234";
        PhoneSmsBody.Header = P("SMS text (optional)", "SMS 文字（可選）");
        PhoneCallBtn.Content = P("Call", "打電話");
        PhoneSmsBtn.Content = P("Text (SMS)", "傳 SMS");

        // Defaults
        DefaultsTitle.Text = P("Pick the default mail / protocol handler", "揀邊個做預設信件 App");
        DefaultsBlurb.Text = P(
            "Windows blocks apps from silently changing the default handler. This opens the Default apps Settings page so you can reassign mailto, discord, tg, msteams and slack handlers yourself.",
            "Windows 唔畀 app 暗中改預設 handler。撳呢個會開「預設 App」設定頁，等你自己重設 mailto、discord、tg、msteams、slack 嘅處理器。");
        DefaultsBtn.Content = P("Open Default apps settings", "開「預設 App」設定");
    }

    private void BuildFolderBox()
    {
        var idx = FolderBox.SelectedIndex < 0 ? 0 : FolderBox.SelectedIndex;
        FolderBox.Items.Clear();
        foreach (var f in CommunicationsService.OutlookFolders)
            FolderBox.Items.Add(new ComboBoxItem { Content = $"{f.En} · {f.Zh}", Tag = f.Value });
        FolderBox.SelectedIndex = idx;
    }

    private void ShowOutlookState()
    {
        var exe = CommunicationsService.ResolveOutlookExe();
        if (exe is null)
        {
            OutlookBar.Severity = InfoBarSeverity.Informational;
            OutlookBar.Title = P("Classic Outlook not found", "揾唔到傳統 Outlook");
            OutlookBar.Message = P(
                "Outlook draft / attach / folder buttons need classic Outlook (OUTLOOK.EXE). Use the mailto: button with the new Outlook.",
                "Outlook 草稿／附件／資料夾按鈕需要傳統 Outlook（OUTLOOK.EXE）。新版 Outlook 請用 mailto: 按鈕。");
            OutlookBar.IsOpen = true;
        }
        else
        {
            OutlookBar.Severity = InfoBarSeverity.Success;
            OutlookBar.Title = P("Classic Outlook detected", "偵測到傳統 Outlook");
            OutlookBar.Message = exe;
            OutlookBar.IsOpen = true;
        }
    }

    private string Msg(TweakResult r) => r.Message?.Primary ?? (r.Success ? "OK" : "Error");

    private void Done(string en, string zh, TweakResult r)
    {
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = P(en, zh);
        ResultBar.Message = Msg(r);
        ResultBar.IsOpen = true;
    }

    // ---------- Mail ----------

    private void Mailto_Click(object sender, RoutedEventArgs e)
        => Done("Open mailto: draft", "開 mailto: 草稿",
            CommunicationsService.OpenMailto(MailTo.Text, MailSubject.Text, MailCc.Text, MailBcc.Text, MailBody.Text));

    private void Outlook_Click(object sender, RoutedEventArgs e)
        => Done("New Outlook draft", "開 Outlook 草稿",
            CommunicationsService.OutlookCompose(MailTo.Text, MailSubject.Text, MailCc.Text, MailBcc.Text, MailBody.Text));

    private async void AttachBrowse_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync();
        if (path is not null) AttachPath.Text = path;
    }

    private void AttachSend_Click(object sender, RoutedEventArgs e)
        => Done("New mail with attachment", "開附件郵件",
            CommunicationsService.OutlookAttach(AttachPath.Text, MailTo.Text, MailSubject.Text, MailBody.Text));

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var folder = (FolderBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Inbox";
        Done("Open Outlook folder", "開 Outlook 資料夾", CommunicationsService.OutlookSelectFolder(folder));
    }

    // ---------- Discord ----------

    private void DiscordChannel_Click(object sender, RoutedEventArgs e)
        => Done("Open Discord channel", "開 Discord 頻道",
            CommunicationsService.DiscordChannel(DiscordGuild.Text, DiscordChannel.Text));

    private void DiscordDm_Click(object sender, RoutedEventArgs e)
        => Done("Open Discord DM", "開 Discord 私訊", CommunicationsService.DiscordDm(DiscordDm.Text));

    private void DiscordHome_Click(object sender, RoutedEventArgs e)
        => Done("Open Discord DMs home", "開 Discord 私訊主頁", CommunicationsService.DiscordDmHome());

    // ---------- Teams ----------

    private void TeamsChat_Click(object sender, RoutedEventArgs e)
        => Done("Start Teams chat", "開 Teams 傾偈",
            CommunicationsService.TeamsChat(TeamsUsers.Text, TeamsTopic.Text, TeamsMessage.Text));

    private void TeamsCall_Click(object sender, RoutedEventArgs e)
        => Done("Teams call", "Teams 通話", CommunicationsService.TeamsCall(TeamsUsers.Text));

    private void TeamsMeeting_Click(object sender, RoutedEventArgs e)
    {
        var start = CombineIso(TeamsStartDate.Date, TeamsStartTime.SelectedTime);
        var end = CombineIso(TeamsEndDate.Date, TeamsEndTime.SelectedTime);
        Done("Open Teams meeting form", "開 Teams 會議表格",
            CommunicationsService.TeamsMeeting(TeamsMtgSubject.Text, TeamsMtgAttendees.Text, start, end, string.Empty));
    }

    private static string CombineIso(DateTimeOffset? date, TimeSpan? time)
    {
        if (date is null) return string.Empty;
        var d = date.Value;
        var t = time ?? TimeSpan.Zero;
        var local = new DateTimeOffset(d.Year, d.Month, d.Day, t.Hours, t.Minutes, 0, d.Offset);
        return local.ToString("yyyy-MM-ddTHH:mm:sszzz");
    }

    // ---------- Telegram ----------

    private void TgShare_Click(object sender, RoutedEventArgs e)
        => Done("Share to Telegram", "分享去 Telegram",
            CommunicationsService.TelegramShare(TgShareUrl.Text, TgShareText.Text));

    private void TgResolve_Click(object sender, RoutedEventArgs e)
        => Done("Open Telegram chat", "開 Telegram 傾偈",
            CommunicationsService.TelegramResolve(TgUsername.Text, TgPost.Text));

    // ---------- Slack ----------

    private void SlackChannel_Click(object sender, RoutedEventArgs e)
        => Done("Open Slack channel", "開 Slack 頻道",
            CommunicationsService.SlackChannel(SlackTeam.Text, SlackChannel.Text));

    private void SlackDm_Click(object sender, RoutedEventArgs e)
        => Done("Open Slack DM", "開 Slack 私訊",
            CommunicationsService.SlackDm(SlackTeam.Text, SlackUser.Text));

    private void SlackOpen_Click(object sender, RoutedEventArgs e)
        => Done("Focus Slack workspace", "對焦 Slack workspace",
            CommunicationsService.SlackOpen(SlackTeam.Text));

    // ---------- Phone Link ----------

    private void PhoneCall_Click(object sender, RoutedEventArgs e)
        => Done("Call", "打電話", CommunicationsService.PhoneCall(PhoneNumber.Text));

    private void PhoneSms_Click(object sender, RoutedEventArgs e)
        => Done("Text (SMS)", "傳 SMS", CommunicationsService.PhoneSms(PhoneNumber.Text, PhoneSmsBody.Text));

    // ---------- Defaults ----------

    private void Defaults_Click(object sender, RoutedEventArgs e)
        => Done("Open Default apps settings", "開「預設 App」設定",
            CommunicationsService.OpenDefaultAppsSettings());
}
