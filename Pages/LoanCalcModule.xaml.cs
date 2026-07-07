using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 貸款／按揭計算 · Loan / mortgage calculator — principal, annual rate %, term (years or months).
/// Computes the monthly payment (standard amortization; 0% handled), total paid, total interest,
/// and a monthly amortization schedule in a ListView. Copy summary. Bilingual (粵語). Never throws.
/// </summary>
public sealed partial class LoanCalcModule : Page
{
    private LoanCalcService.Result? _last;

    public LoanCalcModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Loan Calculator · 貸款計算", "貸款計算 · Loan Calculator");
            HeaderBlurb.Text = P("Work out the monthly payment, total interest and full amortization schedule for a fixed-rate loan or mortgage. All local — nothing leaves your PC.",
                "計出定息貸款或者按揭嘅每月還款額、總利息同埋成個攤還表。全部喺本機計，冇資料離開你部電腦。");

            PrincipalLabel.Text = P("Loan amount (principal)", "貸款金額（本金）");
            RateLabel.Text = P("Annual interest rate (%)", "年利率（%）");
            TermLabel.Text = P("Loan term", "還款期");
            YearsRadio.Content = P("Years", "年");
            MonthsRadio.Content = P("Months", "月");

            CalcButton.Content = P("Calculate", "計算");
            CopyButton.Content = P("Copy summary", "複製摘要");

            ScheduleHeading.Text = P("Amortization schedule", "攤還表");
            ColPeriod.Text = P("#", "期數");
            ColPayment.Text = P("Payment", "還款");
            ColPrincipal.Text = P("Principal", "本金");
            ColInterest.Text = P("Interest", "利息");
            ColBalance.Text = P("Balance", "餘額");

            // Re-render language-dependent result text if we already have a result.
            if (_last is { Ok: true }) ShowResult(_last);
            else if (_last is null)
            {
                MonthlyText.Text = "";
                TotalPaidText.Text = "";
                TotalInterestText.Text = "";
                StatusText.Text = P("Enter your figures and press Calculate.", "填好數字再撳「計算」。");
            }
        }
        catch (Exception ex)
        {
            SafeStatus("Display error: " + ex.Message, "顯示錯誤：" + ex.Message);
        }
    }

    private void TermUnit_Changed(object sender, RoutedEventArgs e)
    {
        // No recompute here — just leave the term number as-is; the unit is read at Calculate time.
    }

    private int TermMonths()
    {
        double raw = double.IsNaN(TermBox.Value) ? 0 : TermBox.Value;
        bool years = YearsRadio.IsChecked == true;
        double months = years ? raw * 12.0 : raw;
        if (months < 0) months = 0;
        return (int)Math.Round(months);
    }

    private void Calc_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double principal = double.IsNaN(PrincipalBox.Value) ? 0 : PrincipalBox.Value;
            double rate = double.IsNaN(RateBox.Value) ? 0 : RateBox.Value;
            int months = TermMonths();

            var r = LoanCalcService.Compute(principal, rate, months);
            _last = r;

            if (!r.Ok)
            {
                ScheduleList.ItemsSource = null;
                CapNote.Visibility = Visibility.Collapsed;
                MonthlyText.Text = "";
                TotalPaidText.Text = "";
                TotalInterestText.Text = "";
                SafeStatus(r.ErrorEn ?? "Invalid input.", r.ErrorZh ?? "輸入無效。");
                return;
            }

            ShowResult(r);
        }
        catch (Exception ex)
        {
            SafeStatus("Calculation failed: " + ex.Message, "計算失敗：" + ex.Message);
        }
    }

    private void ShowResult(LoanCalcService.Result r)
    {
        try
        {
            MonthlyText.Text = P($"Monthly payment: {r.MonthlyPayment:N2}", $"每月還款：{r.MonthlyPayment:N2}");
            TotalPaidText.Text = P($"Total paid over {r.Months} months: {r.TotalPaid:N2}",
                $"{r.Months} 個月合共還款：{r.TotalPaid:N2}");
            TotalInterestText.Text = P($"Total interest: {r.TotalInterest:N2}", $"總利息：{r.TotalInterest:N2}");

            ScheduleList.ItemsSource = r.Schedule;

            if (r.Capped)
            {
                CapNote.Text = P($"Showing the first {LoanCalcService.MaxRows} of {r.Months} payments.",
                    $"只顯示頭 {LoanCalcService.MaxRows} 期（共 {r.Months} 期）。");
                CapNote.Visibility = Visibility.Visible;
            }
            else
            {
                CapNote.Visibility = Visibility.Collapsed;
            }

            SafeStatus("Done.", "計好喇。");
        }
        catch (Exception ex)
        {
            SafeStatus("Display error: " + ex.Message, "顯示錯誤：" + ex.Message);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_last is not { Ok: true } r)
            {
                SafeStatus("Calculate first, then copy.", "先計算，再複製。");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(P("Loan summary", "貸款摘要"));
            sb.AppendLine(P($"Monthly payment: {r.MonthlyPayment:N2}", $"每月還款：{r.MonthlyPayment:N2}"));
            sb.AppendLine(P($"Term: {r.Months} months", $"還款期：{r.Months} 個月"));
            sb.AppendLine(P($"Total paid: {r.TotalPaid:N2}", $"合共還款：{r.TotalPaid:N2}"));
            sb.AppendLine(P($"Total interest: {r.TotalInterest:N2}", $"總利息：{r.TotalInterest:N2}"));

            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(sb.ToString());
            Clipboard.SetContent(dp);

            SafeStatus("Summary copied to clipboard.", "摘要已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            SafeStatus("Copy failed: " + ex.Message, "複製失敗：" + ex.Message);
        }
    }

    private void SafeStatus(string en, string zh)
    {
        try { StatusText.Text = P(en, zh); } catch { /* never throw from status */ }
    }
}
