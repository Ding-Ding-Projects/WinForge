using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 撰寫窗 · Compose / reply / reply-all / forward dialog with attachments, sent via SMTP.
/// 用 <see cref="MailComposer"/> 砌 MimeMessage，<see cref="MailService.SendAsync"/> 寄出。
/// </summary>
public sealed partial class MailComposeDialog : ContentDialog
{
    private readonly MailAccount _account;
    private readonly MailDraft _draft;
    private readonly MailMessageBody? _inReplyTo;
    private bool _sent;

    public MailComposeDialog(MailAccount account, MailDraft draft, MailMessageBody? inReplyTo)
    {
        InitializeComponent();
        _account = account;
        _draft = draft;
        _inReplyTo = inReplyTo;
        Render();
        ToBox.Text = draft.To;
        CcBox.Text = draft.Cc;
        BccBox.Text = draft.Bcc;
        SubjectBox.Text = draft.Subject;
        BodyBox.Text = draft.Body;
        RefreshAttachments();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Title = P("Compose message", "撰寫訊息");
        PrimaryButtonText = P("Send", "寄出");
        CloseButtonText = P("Discard", "捨棄");
        DefaultButton = ContentDialogButton.Primary;

        FromLine.Text = P("From: ", "寄件人：") + _account.Label;
        ToBox.Header = P("To (comma-separated)", "收件人（逗號分隔）");
        ToBox.PlaceholderText = "someone@example.com";
        CcBox.Header = "Cc";
        BccBox.Header = "Bcc";
        SubjectBox.Header = P("Subject", "主旨");
        BodyBox.Header = P("Message", "內文");
        AttachLabel.Text = P("Attach file", "加附件");
    }

    /// <summary>顯示撰寫窗；回傳係咪已寄出 · Show the compose dialog; returns whether it was sent.</summary>
    public async Task<bool> ShowComposeAsync()
    {
        await ShowAsync();
        return _sent;
    }

    private async void Attach_Click(object sender, RoutedEventArgs e)
    {
        var files = await FileDialogs.OpenFilesAsync();
        foreach (var f in files)
            if (!_draft.AttachmentPaths.Contains(f)) _draft.AttachmentPaths.Add(f);
        RefreshAttachments();
    }

    private void RefreshAttachments()
    {
        AttachList.Items.Clear();
        foreach (var path in _draft.AttachmentPaths)
        {
            var chip = new Button { Margin = new Thickness(0, 4, 6, 0) };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            sp.Children.Add(new FontIcon { FontSize = 12, Glyph = "" });
            sp.Children.Add(new TextBlock { Text = System.IO.Path.GetFileName(path), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new FontIcon { FontSize = 11, Glyph = "" }); // x
            chip.Content = sp;
            var captured = path;
            chip.Click += (_, _) => { _draft.AttachmentPaths.Remove(captured); RefreshAttachments(); };
            AttachList.Items.Add(chip);
        }
    }

    private async void Send_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        _draft.To = ToBox.Text;
        _draft.Cc = CcBox.Text;
        _draft.Bcc = BccBox.Text;
        _draft.Subject = SubjectBox.Text;
        _draft.Body = BodyBox.Text;

        if (string.IsNullOrWhiteSpace(_draft.To))
        {
            Warn(P("Add at least one recipient", "請至少加一個收件人"));
            args.Cancel = true;
            return;
        }

        var deferral = args.GetDeferral();
        Busy.IsActive = true;
        try
        {
            var msg = MailComposer.Build(_account, _draft, _inReplyTo);
            await MailService.SendAsync(_account, msg);
            _sent = true;
        }
        catch (Exception ex)
        {
            Warn(P("Send failed: ", "寄出失敗：") + ex.Message);
            args.Cancel = true;
        }
        finally
        {
            Busy.IsActive = false;
            deferral.Complete();
        }
    }

    private void Warn(string msg)
    {
        Bar.Severity = InfoBarSeverity.Error;
        Bar.Title = P("Cannot send", "未能寄出");
        Bar.Message = msg;
        Bar.IsOpen = true;
    }
}
