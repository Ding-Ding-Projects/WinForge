using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// CrystalDiskInfo 風格嘅原生 SMART 硬碟健康監測 · CrystalDiskInfo-style native SMART disk-health monitor.
/// 純 WMI + PInvoke 讀取（DeviceIoControl / NVMe health log），完全唔啟動任何外部程式。
/// Pure WMI + PInvoke (DeviceIoControl / NVMe health log); never launches any external tool. Bilingual.
/// </summary>
public sealed partial class DiskHealthModule : Page
{
    /// <summary>一行 SMART 屬性（畀表格用）· One SMART attribute row for the table view.</summary>
    public sealed class AttrRow
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Current { get; init; } = "";
        public string Worst { get; init; } = "";
        public string Threshold { get; init; } = "";
        public string Raw { get; init; } = "";
    }

    /// <summary>一張磁碟卡 · A single drive card view-model (notifies for live temperature updates).</summary>
    public sealed class DiskCard : INotifyPropertyChanged
    {
        public int Index { get; init; }
        public string Glyph { get; set; } = "";
        public string Model { get; set; } = "";
        public string SubLine { get; set; } = "";

        private string _healthLabel = "";
        public string HealthLabel { get => _healthLabel; set { _healthLabel = value; OnPC(); } }
        private Brush _healthBrush = Transparent;
        public Brush HealthBrush { get => _healthBrush; set { _healthBrush = value; OnPC(); } }

        public string TempCaption { get; set; } = "";
        private string _tempValue = "";
        public string TempValue { get => _tempValue; set { _tempValue = value; OnPC(); } }

        public string HoursCaption { get; set; } = "";
        public string HoursValue { get; set; } = "";
        public string CyclesCaption { get; set; } = "";
        public string CyclesValue { get; set; } = "";
        public string ExtraCaption { get; set; } = "";
        public string ExtraValue { get; set; } = "";

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = "";

        public string AttrHeader { get; set; } = "";
        public Visibility AttrVisibility { get; set; } = Visibility.Collapsed;
        public ObservableCollection<AttrRow> Rows { get; } = new();

        // Column header captions (bilingual, set in Reload).
        public string ColId { get; set; } = "";
        public string ColName { get; set; } = "";
        public string ColCur { get; set; } = "";
        public string ColWorst { get; set; } = "";
        public string ColThr { get; set; } = "";
        public string ColRaw { get; set; } = "";

        private static readonly Brush Transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPC([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly ObservableCollection<DiskCard> _cards = new();
    private List<DiskHealth> _disks = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool _busy;

    public DiskHealthModule()
    {
        InitializeComponent();
        Cards.ItemsSource = _cards;
        _timer.Tick += (_, _) => RefreshTemps();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) => { Render(); await Reload(); _timer.Start(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; _timer.Stop(); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        Rebind();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Disk Health (SMART) · 硬碟健康（SMART）";
        HeaderBlurb.Text = P(
            "A CrystalDiskInfo-style health monitor read natively from each drive's SMART data — temperature, power-on hours, wear and every attribute. No external tools.",
            "CrystalDiskInfo 風格嘅健康監測，直接由每個磁碟嘅 SMART 資料原生讀取 — 溫度、通電時數、耗損同所有屬性。唔使任何外部程式。");
        RefreshBtn.Content = P("Refresh", "重新整理");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private async Task Reload()
    {
        if (_busy) return;
        _busy = true;
        Busy.IsActive = true;
        RefreshBtn.IsEnabled = false;
        AdminBar.IsOpen = false;
        EmptyBar.IsOpen = false;
        try
        {
            _disks = await Task.Run(() => SmartService.Enumerate());
            Rebind();

            if (_disks.Count == 0)
            {
                EmptyBar.Title = P("No drives found", "搵唔到磁碟");
                EmptyBar.Message = P("No physical disks were reported by WMI.", "WMI 冇回報任何實體磁碟。");
                EmptyBar.IsOpen = true;
            }
            else if (!AdminHelper.IsElevated && _disks.Any(d => !d.SmartRead))
            {
                AdminBar.Title = P("Limited data", "資料有限");
                AdminBar.Message = P(
                    "Some SMART reads were blocked — run WinForge as administrator to see full health for every drive.",
                    "部分 SMART 讀取被阻擋 — 以管理員身分運行 WinForge 先可以睇到每個磁碟嘅完整健康資料。");
                AdminBar.IsOpen = true;
            }
        }
        finally
        {
            _busy = false;
            Busy.IsActive = false;
            RefreshBtn.IsEnabled = true;
        }
    }

    /// <summary>Rebuild all cards from the last scan (also used on language change).</summary>
    private void Rebind()
    {
        _cards.Clear();
        foreach (var d in _disks)
            _cards.Add(BuildCard(d));
    }

    private DiskCard BuildCard(DiskHealth d)
    {
        string iface = string.IsNullOrEmpty(d.InterfaceType) ? (d.IsNvme ? "NVMe" : "—") : d.InterfaceType;
        string media = d.IsNvme ? "NVMe SSD" : d.MediaType;
        var sub = $"{P("Serial", "序號")}: {Show(d.Serial)}  ·  {P("Firmware", "韌體")}: {Show(d.Firmware)}  ·  "
                + $"{iface} · {media} · {SmartService.HumanSize(d.CapacityBytes)}  ·  \\\\.\\PhysicalDrive{d.Index}";

        var (he, hz) = d.HealthText();
        var card = new DiskCard
        {
            Index = d.Index,
            Glyph = d.IsNvme ? "" : (d.MediaType == "SSD" ? "" : ""),
            Model = string.IsNullOrWhiteSpace(d.Model) ? P("Unknown drive", "未知磁碟") : d.Model,
            SubLine = sub,
            HealthLabel = P(he, hz),
            HealthBrush = HealthBrush(d.Health),

            TempCaption = P("Temperature", "溫度"),
            TempValue = d.TemperatureC is { } t ? $"{t} °C" : "—",
            HoursCaption = P("Power-on hours", "通電時數"),
            HoursValue = d.PowerOnHours is { } poh ? $"{poh:N0} h" : "—",
            CyclesCaption = P("Power cycles", "通電次數"),
            CyclesValue = d.PowerCycles is { } pc ? $"{pc:N0}" : "—",

            ColId = "ID",
            ColName = P("Attribute", "屬性"),
            ColCur = P("Cur", "現值"),
            ColWorst = P("Worst", "最差"),
            ColThr = P("Thr", "門檻"),
            ColRaw = P("Raw", "原始值"),
        };

        // Fourth headline tile depends on media type.
        if (d.IsNvme && d.PercentageUsed is { } pu)
        {
            card.ExtraCaption = P("Life used", "已使用壽命");
            card.ExtraValue = $"{pu}%";
        }
        else if (d.ReallocatedSectors is { } rs)
        {
            card.ExtraCaption = P("Reallocated", "重新分配");
            card.ExtraValue = $"{rs:N0}";
        }
        else
        {
            card.ExtraCaption = P("Interface", "介面");
            card.ExtraValue = iface;
        }

        if (d.SmartRead && d.Attributes.Count > 0)
        {
            card.AttrVisibility = Visibility.Visible;
            card.AttrHeader = P($"All SMART attributes ({d.Attributes.Count})", $"所有 SMART 屬性（{d.Attributes.Count}）");
            foreach (var a in d.Attributes)
                card.Rows.Add(new AttrRow
                {
                    Id = $"{a.Id:X2}",
                    Name = P(a.NameEn, a.NameZh),
                    Current = a.Current > 0 ? a.Current.ToString() : "—",
                    Worst = a.Worst > 0 ? a.Worst.ToString() : "—",
                    Threshold = a.Threshold > 0 ? a.Threshold.ToString() : "—",
                    Raw = a.Raw.ToString("N0"),
                });
        }

        if (!d.SmartRead && (d.ErrorEn is not null || d.ErrorZh is not null))
        {
            card.HasError = true;
            card.ErrorMessage = P(d.ErrorEn ?? "SMART data unavailable.", d.ErrorZh ?? "無法取得 SMART 資料。");
        }

        return card;
    }

    private string Show(string s) => string.IsNullOrWhiteSpace(s) ? P("n/a", "不適用") : s;

    private static Brush HealthBrush(StatusColor c) => c switch
    {
        StatusColor.Good => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x16, 0x7A, 0x3D)),
        StatusColor.Warn => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0xB2, 0x7A, 0x00)),
        StatusColor.Bad => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0xC4, 0x2B, 0x1C)),
        _ => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x6B, 0x6B, 0x6B)),
    };

    /// <summary>Cheap timer refresh — re-read temperatures only and update the live tiles + pill.</summary>
    private async void RefreshTemps()
    {
        if (_busy || _disks.Count == 0) return;
        var snapshot = _disks;
        await Task.Run(() =>
        {
            foreach (var d in snapshot) SmartService.RefreshTemperature(d);
        });

        foreach (var d in snapshot)
        {
            var card = _cards.FirstOrDefault(c => c.Index == d.Index);
            if (card is null) continue;
            card.TempValue = d.TemperatureC is { } t ? $"{t} °C" : "—";
            var (he, hz) = d.HealthText();
            card.HealthLabel = P(he, hz);
            card.HealthBrush = HealthBrush(d.Health);
        }
    }
}
