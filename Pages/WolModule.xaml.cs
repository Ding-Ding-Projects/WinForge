using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 網絡喚醒 · Wake-on-LAN — enter a target MAC (any common notation), an optional broadcast
/// address and UDP port, then send a magic packet to power the machine on. Pure managed
/// (UdpClient); nothing throws. Bilingual (Loc.I.Pick). Keeps a small recent-targets list.
/// </summary>
public sealed partial class WolModule : Page
{
    private readonly List<string> _recent = new();

    public WolModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Wake-on-LAN · 網絡喚醒";
        HeaderBlurb.Text = P("Send a Wake-on-LAN “magic packet” to power on a sleeping or shut-down PC on your network. The target must have WOL enabled in its BIOS/UEFI and network adapter.",
            "傳送 Wake-on-LAN「魔術封包」去喚醒你網絡上瞓咗或者熄咗嘅電腦。對象要喺 BIOS/UEFI 同網卡開咗 WOL 先得。");
        MacLabel.Text = P("Target MAC address", "對象 MAC 位址");
        BroadcastLabel.Text = P("Broadcast address (optional)", "廣播位址（可選）");
        PortLabel.Text = P("UDP port", "UDP 連接埠");
        SendButton.Content = P("Send magic packet", "傳送魔術封包");
        RecentLabel.Text = P("Recently sent", "最近傳送");
        if (StatusText.Text.Length == 0)
            StatusText.Text = P("Enter a MAC address and send.", "輸入 MAC 位址然後傳送。");
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SendButton.IsEnabled = false;
            StatusText.Text = P("Sending…", "傳送中…");

            int port = (int)(double.IsNaN(PortBox.Value) ? 9 : PortBox.Value);
            var result = await WolService.SendAsync(MacBox.Text, BroadcastBox.Text, port);
            StatusText.Text = P(result.En, result.Zh);

            if (result.Ok) AddRecent(P(result.En, result.Zh));
        }
        catch (Exception ex)
        {
            // Defensive: SendAsync is already guarded, but never let the handler throw.
            StatusText.Text = P($"Unexpected error: {ex.Message}", $"意外錯誤：{ex.Message}");
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void AddRecent(string entry)
    {
        _recent.Insert(0, $"{DateTime.Now:HH:mm}  {entry}");
        while (_recent.Count > 8) _recent.RemoveAt(_recent.Count - 1);

        RecentList.Items.Clear();
        foreach (var line in _recent)
            RecentList.Items.Add(new TextBlock
            {
                Text = line,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
            });
        RecentCard.Visibility = Visibility.Visible;
    }
}
