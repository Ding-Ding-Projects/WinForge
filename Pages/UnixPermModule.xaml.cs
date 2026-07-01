using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// chmod 計算機 · Unix permission (chmod) calculator. A 3×3 grid of CheckBoxes (Owner/Group/Other ×
/// Read/Write/Execute) plus setuid/setgid/sticky. Live octal + symbolic + `chmod` command; the octal
/// and symbolic TextBoxes are two-way (editing them updates the boxes). Robust parsing, never throws.
/// </summary>
public sealed partial class UnixPermModule : Page
{
    private bool _suppress; // guard re-entrancy while syncing controls.

    public UnixPermModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        // Sensible default: 0644 (rw-r--r--).
        ApplyMode(UnixPermService.OwnerR | UnixPermService.OwnerW | UnixPermService.GroupR | UnixPermService.OtherR);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "chmod Calculator · chmod 計算機";
        HeaderBlurb.Text = P("Tick the read/write/execute bits for owner, group and others to build a Unix file mode. See the octal, the symbolic string and a ready-to-run chmod command — or type an octal / symbolic value to set the boxes.",
            "揀返擁有者、群組、其他人嘅讀 / 寫 / 執行權限，砌返個 Unix 檔案模式。即時睇到八進位、符號字串同一句可以照抄嘅 chmod 指令 — 或者直接打八進位／符號值嚟反填格仔。");

        MatrixTitle.Text = P("Permission bits", "權限位");
        ColRead.Text = P("Read", "讀");
        ColWrite.Text = P("Write", "寫");
        ColExec.Text = P("Exec", "執行");
        RowOwner.Text = P("Owner", "擁有者");
        RowGroup.Text = P("Group", "群組");
        RowOther.Text = P("Other", "其他");

        SetUidChk.Content = P("setuid (4000)", "setuid（4000）");
        SetGidChk.Content = P("setgid (2000)", "setgid（2000）");
        StickyChk.Content = P("sticky (1000)", "sticky 黏著位（1000）");

        OctalLabel.Text = P("Octal", "八進位");
        SymbolicLabel.Text = P("Symbolic", "符號");
        CommandLabel.Text = P("Command", "指令");

        CopyOctalBtn.Content = P("Copy", "複製");
        CopySymbolicBtn.Content = P("Copy", "複製");
        CopyCommandBtn.Content = P("Copy", "複製");

        // Re-emit derived text so it reflects the current language.
        Refresh(CurrentMode());
    }

    // ---- read current mode off the checkboxes ----
    private int CurrentMode()
    {
        int m = 0;
        if (OwnerR.IsChecked == true) m |= UnixPermService.OwnerR;
        if (OwnerW.IsChecked == true) m |= UnixPermService.OwnerW;
        if (OwnerX.IsChecked == true) m |= UnixPermService.OwnerX;
        if (GroupR.IsChecked == true) m |= UnixPermService.GroupR;
        if (GroupW.IsChecked == true) m |= UnixPermService.GroupW;
        if (GroupX.IsChecked == true) m |= UnixPermService.GroupX;
        if (OtherR.IsChecked == true) m |= UnixPermService.OtherR;
        if (OtherW.IsChecked == true) m |= UnixPermService.OtherW;
        if (OtherX.IsChecked == true) m |= UnixPermService.OtherX;
        if (SetUidChk.IsChecked == true) m |= UnixPermService.SetUid;
        if (SetGidChk.IsChecked == true) m |= UnixPermService.SetGid;
        if (StickyChk.IsChecked == true) m |= UnixPermService.Sticky;
        return m;
    }

    // ---- push a mode onto every control + derived text ----
    private void ApplyMode(int mode)
    {
        _suppress = true;
        try
        {
            OwnerR.IsChecked = (mode & UnixPermService.OwnerR) != 0;
            OwnerW.IsChecked = (mode & UnixPermService.OwnerW) != 0;
            OwnerX.IsChecked = (mode & UnixPermService.OwnerX) != 0;
            GroupR.IsChecked = (mode & UnixPermService.GroupR) != 0;
            GroupW.IsChecked = (mode & UnixPermService.GroupW) != 0;
            GroupX.IsChecked = (mode & UnixPermService.GroupX) != 0;
            OtherR.IsChecked = (mode & UnixPermService.OtherR) != 0;
            OtherW.IsChecked = (mode & UnixPermService.OtherW) != 0;
            OtherX.IsChecked = (mode & UnixPermService.OtherX) != 0;
            SetUidChk.IsChecked = (mode & UnixPermService.SetUid) != 0;
            SetGidChk.IsChecked = (mode & UnixPermService.SetGid) != 0;
            StickyChk.IsChecked = (mode & UnixPermService.Sticky) != 0;

            OctalBox.Text = UnixPermService.ToOctal(mode);
            SymbolicBox.Text = UnixPermService.ToSymbolic(mode);
        }
        finally { _suppress = false; }
        Refresh(mode);
    }

    // ---- refresh derived (command + status) without touching the two-way boxes ----
    private void Refresh(int mode)
    {
        try
        {
            CommandText.Text = $"chmod {UnixPermService.ToChmodOctal(mode)} file";
            StatusText.Text = P($"Mode {UnixPermService.ToOctal(mode)} — {UnixPermService.ToSymbolic(mode)}",
                $"模式 {UnixPermService.ToOctal(mode)} — {UnixPermService.ToSymbolic(mode)}");
        }
        catch
        {
            StatusText.Text = P("Could not render this mode.", "無法呈現此模式。");
        }
    }

    // ---- checkbox → everything ----
    private void Bit_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        int mode = CurrentMode();
        _suppress = true;
        try
        {
            OctalBox.Text = UnixPermService.ToOctal(mode);
            SymbolicBox.Text = UnixPermService.ToSymbolic(mode);
        }
        finally { _suppress = false; }
        Refresh(mode);
    }

    // ---- octal box → everything ----
    private void Octal_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        if (UnixPermService.TryParseOctal(OctalBox.Text, out int mode))
        {
            _suppress = true;
            try { SymbolicBox.Text = UnixPermService.ToSymbolic(mode); }
            finally { _suppress = false; }
            SyncChecks(mode);
            Refresh(mode);
        }
        else
        {
            StatusText.Text = P("Invalid octal — use digits 0–7 (e.g. 755 or 4755).",
                "八進位唔啱 — 只可以用 0–7（例如 755 或 4755）。");
        }
    }

    // ---- symbolic box → everything ----
    private void Symbolic_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        if (UnixPermService.TryParseSymbolic(SymbolicBox.Text, out int mode))
        {
            _suppress = true;
            try { OctalBox.Text = UnixPermService.ToOctal(mode); }
            finally { _suppress = false; }
            SyncChecks(mode);
            Refresh(mode);
        }
        else
        {
            StatusText.Text = P("Invalid symbolic — 9 chars like rwxr-xr-x (s/S/t/T allowed).",
                "符號唔啱 — 要 9 個字元，例如 rwxr-xr-x（可用 s/S/t/T）。");
        }
    }

    // Update only the checkboxes from a mode, without re-writing the text boxes.
    private void SyncChecks(int mode)
    {
        _suppress = true;
        try
        {
            OwnerR.IsChecked = (mode & UnixPermService.OwnerR) != 0;
            OwnerW.IsChecked = (mode & UnixPermService.OwnerW) != 0;
            OwnerX.IsChecked = (mode & UnixPermService.OwnerX) != 0;
            GroupR.IsChecked = (mode & UnixPermService.GroupR) != 0;
            GroupW.IsChecked = (mode & UnixPermService.GroupW) != 0;
            GroupX.IsChecked = (mode & UnixPermService.GroupX) != 0;
            OtherR.IsChecked = (mode & UnixPermService.OtherR) != 0;
            OtherW.IsChecked = (mode & UnixPermService.OtherW) != 0;
            OtherX.IsChecked = (mode & UnixPermService.OtherX) != 0;
            SetUidChk.IsChecked = (mode & UnixPermService.SetUid) != 0;
            SetGidChk.IsChecked = (mode & UnixPermService.SetGid) != 0;
            StickyChk.IsChecked = (mode & UnixPermService.Sticky) != 0;
        }
        finally { _suppress = false; }
    }

    // ---- copy helpers ----
    private void CopyOctal_Click(object sender, RoutedEventArgs e) => Copy(UnixPermService.ToOctal(CurrentMode()));
    private void CopySymbolic_Click(object sender, RoutedEventArgs e) => Copy(UnixPermService.ToSymbolic(CurrentMode()));
    private void CopyCommand_Click(object sender, RoutedEventArgs e) => Copy($"chmod {UnixPermService.ToChmodOctal(CurrentMode())} file");

    private void Copy(string text)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(text ?? string.Empty);
            Clipboard.SetContent(dp);
            StatusText.Text = P($"Copied: {text}", $"已複製：{text}");
        }
        catch
        {
            StatusText.Text = P("Could not copy to the clipboard.", "無法複製到剪貼簿。");
        }
    }
}
