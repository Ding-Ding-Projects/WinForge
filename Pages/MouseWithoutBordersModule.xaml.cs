using System;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 無界滑鼠（鍵鼠共享 KVM）· Mouse Without Borders module — control several PCs on the same LAN
/// with one keyboard and mouse, plus shared clipboard. Shows this PC's name / security key / IP for
/// pairing, lets another machine be added by key + IP over an AES-encrypted TCP control channel,
/// arranges up to four machines left-to-right, and forwards captured input across the screen edge.
/// All user-facing strings are bilingual (繁體中文／English). Independent native implementation —
/// it studies the PowerToys design but shares no code.
/// </summary>
public sealed partial class MouseWithoutBordersModule : Page
{
    private readonly MouseWithoutBordersService _svc = MouseWithoutBordersService.Instance;
    private DispatcherQueue? _ui;
    private bool _wired;

    public MouseWithoutBordersModule()
    {
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) =>
        {
            WireService();
            LoadInto();
            Render();
            RefreshAll();
        };
        Unloaded += (_, _) => UnwireService();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void WireService()
    {
        if (_wired) return;
        _svc.StateChanged += OnStateChanged;
        _svc.Log += OnLog;
        _svc.ClipboardTextReceived += OnClipboardReceived;
        _wired = true;
    }

    private void UnwireService()
    {
        if (!_wired) return;
        _svc.StateChanged -= OnStateChanged;
        _svc.Log -= OnLog;
        _svc.ClipboardTextReceived -= OnClipboardReceived;
        _wired = false;
    }

    private void OnStateChanged() => _ui?.TryEnqueue(RefreshAll);
    private void OnLog(string line) => _ui?.TryEnqueue(() => AppendLog(line));

    private void OnClipboardReceived(string text)
    {
        _ui?.TryEnqueue(() =>
        {
            try
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(text);
                Clipboard.SetContent(dp);
                AppendLog(P("Applied clipboard text from a peer.", "已套用對方傳來嘅剪貼簿文字。"));
            }
            catch { }
        });
    }

    // ---- render bilingual labels --------------------------------------------------

    private void Render()
    {
        HeaderTitle.Text = "Mouse Without Borders · 無界滑鼠";
        HeaderBlurb.Text = P(
            "Control several PCs on the same network with one keyboard and mouse, and share the clipboard. Show this PC's security key and IP, then enter them on another machine to pair. Arrange machines left-to-right; when the pointer crosses a screen edge toward a neighbour, control moves to that PC.",
            "用一套鍵盤滑鼠操控同一網絡上嘅多部電腦，仲可以共用剪貼簿。顯示本機嘅安全密鑰同 IP，喺另一部機輸入嚟配對。將機器由左到右排列；當指標越過螢幕邊界去鄰機時，控制權就交畀嗰部電腦。");

        EnableTitle.Text = P("Enable Mouse Without Borders · 啟用無界滑鼠", "啟用無界滑鼠");
        EnableSub.Text = P("Starts the encrypted control channel and installs the low-level input hooks.",
            "啟動加密控制頻道並安裝低層輸入鈎子。");
        ClipboardChk.Content = P("Share clipboard text · 共用剪貼簿文字", "共用剪貼簿文字");
        WrapChk.Content = P("Wrap around edges · 邊界環繞", "邊界環繞");

        ThisPcTitle.Text = P("This PC · 本機", "本機");
        NameLbl.Text = P("Name", "機器名");
        SaveNameBtn.Content = P("Save · 儲存", "儲存");
        KeyLbl.Text = P("Key", "密鑰");
        IpLbl.Text = P("IP", "IP");
        ToolTipService.SetToolTip(CopyKeyBtn, P("Copy key · 複製密鑰", "複製密鑰"));
        ToolTipService.SetToolTip(RegenKeyBtn, P("Regenerate key · 重新產生密鑰", "重新產生密鑰"));
        KeyHint.Text = P("Give this security key + IP to the other machine. Both machines must use the same key. The key is stored encrypted (DPAPI) and never leaves this PC in the clear.",
            "將呢個安全密鑰同 IP 畀另一部機。兩部機要用同一個密鑰。密鑰以 DPAPI 加密儲存，唔會以明文離開本機。");

        PairTitle.Text = P("Pair a machine · 配對機器", "配對機器");
        PairBlurb.Text = P("On the other PC, open this page, copy its key and IP, then enter them here to connect.",
            "喺另一部電腦開呢一頁，複製佢嘅密鑰同 IP，再喺呢度輸入嚟連線。");
        PairAddBtn.Content = P("Add & connect · 加入並連線", "加入並連線");

        LayoutTitle.Text = P("Machine layout · 機器版面", "機器版面");
        LayoutBlurb.Text = P("Place up to four machines left-to-right. Slot 1 is this PC; the pointer crosses into a neighbour at the matching screen edge.",
            "最多排列四部機器（左至右）。第 1 格係本機；指標喺對應嘅螢幕邊界會過去鄰機。");

        MachinesTitle.Text = P("Paired machines · 已配對機器", "已配對機器");
        MachinesEmpty.Text = P("No machines paired yet — add one above.", "未有配對機器 — 喺上面加入一部。");

        LogTitle.Text = P("Activity · 活動記錄", "活動記錄");
        Disclaimer.Text = P(
            "Cross-machine control and clipboard sync require two live PCs on the same LAN to validate end-to-end. The pairing UI, encrypted protocol, hooks and injection are fully implemented here.",
            "跨機控制同剪貼簿同步需要同一區域網內兩部真實電腦先可以完整驗證。配對介面、加密協定、鈎子同注入已喺呢度完整實作。");

        RefreshStatus();
        RefreshLayout();
        RefreshMachines();
    }

    private void LoadInto()
    {
        NameBox.Text = _svc.MachineName;
        PortValue.Text = _svc.Port.ToString();
        ClipboardChk.IsChecked = _svc.ClipboardShare;
        WrapChk.IsChecked = _svc.WrapAround;
        EnableSwitch.IsOn = _svc.Enabled && _svc.IsListening;
        var key = _svc.EnsureSecurityKey();
        KeyValue.Text = key;
        IpValue.Text = string.Join(", ", _svc.LocalIPv4Addresses());
        PairPortBox.Text = MwbProtocol.DefaultPort.ToString();
    }

    private void RefreshAll()
    {
        RefreshStatus();
        RefreshLayout();
        RefreshMachines();
    }

    private void RefreshStatus()
    {
        if (StatusBar == null) return;
        if (_svc.IsListening)
        {
            int n = _svc.ConnectedCount;
            string control = string.IsNullOrEmpty(_svc.ControlOwner)
                ? P("control: this PC", "控制：本機")
                : P($"control: {_svc.ControlOwner}", $"控制：{_svc.ControlOwner}");
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Message = P($"Running · listening on port {_svc.Port} · {n} connected · {control}",
                $"運行中 · 監聽埠 {_svc.Port} · 已連線 {n} 部 · {control}");
        }
        else
        {
            StatusBar.Severity = InfoBarSeverity.Informational;
            StatusBar.Message = P("Stopped — enable above to start pairing and forwarding.",
                "已停止 — 喺上面啟用嚟開始配對同轉發。");
        }
    }

    // ---- layout editor ------------------------------------------------------------

    private void RefreshLayout()
    {
        if (LayoutGrid == null) return;
        LayoutGrid.Children.Clear();
        var machines = _svc.Machines;

        for (int slot = 0; slot < 4; slot++)
        {
            MwbMachine? occupant = slot == 0 ? null : machines.FirstOrDefault(m => m.Slot == slot);
            bool isLocal = slot == 0;

            var border = new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                MinHeight = 86,
            };
            border.SetValue(Grid.ColumnProperty, slot);
            border.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                isLocal ? "AccentFillColorDefaultBrush" : "CardBackgroundFillColorSecondaryBrush"];
            border.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

            var panel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = P($"Slot {slot + 1}", $"第 {slot + 1} 格"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });

            if (isLocal)
            {
                panel.Children.Add(new TextBlock { Text = P("This PC", "本機"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
                panel.Children.Add(new TextBlock { Text = _svc.MachineName, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            }
            else if (occupant != null)
            {
                panel.Children.Add(new TextBlock { Text = occupant.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                var clearBtn = new Button { Content = P("Clear", "清除"), Padding = new Thickness(8, 2, 8, 2), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
                var capturedName = occupant.Name;
                clearBtn.Click += (_, _) => { _svc.SetSlot(capturedName, -1); };
                panel.Children.Add(clearBtn);
            }
            else
            {
                var combo = new ComboBox { PlaceholderText = P("Empty", "空"), Width = 120, FontSize = 12 };
                foreach (var m in machines.Where(m => m.Slot != slot))
                    combo.Items.Add(m.Name);
                int capturedSlot = slot;
                combo.SelectionChanged += (s, _) =>
                {
                    if (combo.SelectedItem is string name) _svc.SetSlot(name, capturedSlot);
                };
                panel.Children.Add(combo);
            }

            border.Child = panel;
            LayoutGrid.Children.Add(border);
        }
    }

    // ---- paired machines list -----------------------------------------------------

    private void RefreshMachines()
    {
        if (MachinesHost == null) return;
        MachinesHost.Children.Clear();
        var machines = _svc.Machines;
        MachinesEmpty.Visibility = machines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var m in machines)
        {
            var state = _svc.StateOf(m.Name);
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new FontIcon { Glyph = "", FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            dot.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
                state == MwbLinkState.Connected ? "SystemFillColorSuccessBrush"
                : state == MwbLinkState.Error ? "SystemFillColorCriticalBrush"
                : state == MwbLinkState.Connecting ? "SystemFillColorCautionBrush"
                : "TextFillColorTertiaryBrush"];
            dot.SetValue(Grid.ColumnProperty, 0);
            row.Children.Add(dot);

            var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            info.SetValue(Grid.ColumnProperty, 1);
            info.Children.Add(new TextBlock { Text = m.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            info.Children.Add(new TextBlock
            {
                Text = $"{m.Host}:{m.Port}  ·  {StateText(state)}",
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            row.Children.Add(info);

            var name = m.Name;
            if (state == MwbLinkState.Connected)
            {
                var disc = new Button { Content = P("Disconnect", "斷線"), VerticalAlignment = VerticalAlignment.Center };
                disc.SetValue(Grid.ColumnProperty, 2);
                disc.Click += (_, _) => _svc.Disconnect(name);
                row.Children.Add(disc);

                var take = new Button { Content = P("Take control", "交控制權"), VerticalAlignment = VerticalAlignment.Center };
                take.SetValue(Grid.ColumnProperty, 3);
                take.Click += (_, _) =>
                {
                    if (string.IsNullOrEmpty(_svc.ControlOwner)) _svc.GiveControlTo(name);
                    else _svc.ReturnControlLocal();
                };
                row.Children.Add(take);
            }
            else
            {
                var conn = new Button { Content = P("Connect", "連線"), VerticalAlignment = VerticalAlignment.Center, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                conn.SetValue(Grid.ColumnProperty, 2);
                conn.Click += async (_, _) => await _svc.ConnectAsync(name);
                row.Children.Add(conn);
            }

            var remove = new Button { VerticalAlignment = VerticalAlignment.Center };
            remove.Content = new FontIcon { Glyph = "", FontSize = 14 };
            ToolTipService.SetToolTip(remove, P("Remove", "移除"));
            remove.SetValue(Grid.ColumnProperty, 4);
            remove.Click += (_, _) => _svc.RemoveMachine(name);
            row.Children.Add(remove);

            MachinesHost.Children.Add(row);
        }
    }

    private string StateText(MwbLinkState s) => s switch
    {
        MwbLinkState.Connected => P("connected", "已連線"),
        MwbLinkState.Connecting => P("connecting…", "連線中…"),
        MwbLinkState.Error => P("error", "出錯"),
        _ => P("disconnected", "未連線"),
    };

    // ---- handlers -----------------------------------------------------------------

    private void EnableSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        // Persist port first so Start() listens on the right one.
        if (int.TryParse(PortValue.Text, out var p) && p is > 0 and < 65536) _svc.Port = p;
        if (EnableSwitch.IsOn) _svc.Start();
        else _svc.Stop();
        RefreshStatus();
    }

    private void ClipboardChk_Click(object sender, RoutedEventArgs e) => _svc.ClipboardShare = ClipboardChk.IsChecked == true;
    private void WrapChk_Click(object sender, RoutedEventArgs e) => _svc.WrapAround = WrapChk.IsChecked == true;

    private void SaveName_Click(object sender, RoutedEventArgs e)
    {
        var n = (NameBox.Text ?? "").Trim();
        if (n.Length > 0) { _svc.MachineName = n; RefreshLayout(); AppendLog(P($"Machine name set to {n}.", $"機器名已設為 {n}。")); }
    }

    private void CopyKey_Click(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(KeyValue.Text);
        Clipboard.SetContent(dp);
        AppendLog(P("Security key copied.", "已複製安全密鑰。"));
    }

    private void RegenKey_Click(object sender, RoutedEventArgs e)
    {
        var key = MwbProtocol.GenerateKey();
        _svc.SetSecurityKey(key);
        KeyValue.Text = key;
        AppendLog(P("Generated a new security key. Re-pair other machines with it.",
            "已產生新嘅安全密鑰。請用新密鑰重新配對其他機器。"));
    }

    private async void PairAdd_Click(object sender, RoutedEventArgs e)
    {
        var name = (PairNameBox.Text ?? "").Trim();
        var ip = (PairIpBox.Text ?? "").Trim();
        var key = (PairKeyBox.Text ?? "").Trim();
        if (!int.TryParse(PairPortBox.Text, out var port) || port is <= 0 or >= 65536) port = MwbProtocol.DefaultPort;

        if (name.Length == 0) name = ip;
        if (ip.Length == 0)
        {
            PairResult.IsOpen = true;
            PairResult.Severity = InfoBarSeverity.Error;
            PairResult.Message = P("Enter the other machine's IP address.", "請輸入另一部機嘅 IP 位址。");
            return;
        }
        if (key.Length == 0)
        {
            PairResult.IsOpen = true;
            PairResult.Severity = InfoBarSeverity.Error;
            PairResult.Message = P("Enter the other machine's security key.", "請輸入另一部機嘅安全密鑰。");
            return;
        }

        _svc.AddOrUpdateMachine(name, ip, port, key);
        PairNameBox.Text = PairIpBox.Text = PairKeyBox.Text = "";

        if (!_svc.IsListening) _svc.Start();
        var ok = await _svc.ConnectAsync(name);

        PairResult.IsOpen = true;
        PairResult.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        PairResult.Message = ok
            ? P($"Added {name} and connecting…", $"已加入 {name}，連線中…")
            : P($"Added {name}, but couldn't reach it yet — check the IP, port and firewall.",
                $"已加入 {name}，但暫時連唔到 — 請檢查 IP、埠同防火牆。");
        RefreshAll();
    }

    // ---- log ----------------------------------------------------------------------

    private void AppendLog(string line)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        LogBox.Text = $"[{ts}] {line}\r\n" + LogBox.Text;
        if (LogBox.Text.Length > 8000) LogBox.Text = LogBox.Text[..8000];
    }
}
