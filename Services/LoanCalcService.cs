using System;
using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>
/// 貸款／按揭計算 · Loan / mortgage calculator — pure managed amortization maths.
/// Standard fixed-rate amortization: M = P·r / (1 − (1+r)^-n), with the 0% rate special-cased.
/// Never throws; callers get a Result with an Ok flag and a bilingual error tag when input is bad.
/// </summary>
public static class LoanCalcService
{
    /// <summary>One row of the amortization schedule.</summary>
    public sealed class ScheduleRow
    {
        public int Period { get; init; }
        public double Payment { get; init; }
        public double Principal { get; init; }
        public double Interest { get; init; }
        public double Balance { get; init; }

        // Pre-formatted display strings for classic {Binding} in the ListView.
        public string PeriodText => Period.ToString();
        public string PaymentText => Money(Payment);
        public string PrincipalText => Money(Principal);
        public string InterestText => Money(Interest);
        public string BalanceText => Money(Balance);

        private static string Money(double v) => v.ToString("N2");
    }

    /// <summary>Outcome of a computation — never-throw; check <see cref="Ok"/>.</summary>
    public sealed class Result
    {
        public bool Ok { get; init; }
        public string? ErrorEn { get; init; }
        public string? ErrorZh { get; init; }

        public double MonthlyPayment { get; init; }
        public double TotalPaid { get; init; }
        public double TotalInterest { get; init; }
        public int Months { get; init; }
        public bool Capped { get; init; }
        public IReadOnlyList<ScheduleRow> Schedule { get; init; } = Array.Empty<ScheduleRow>();
    }

    /// <summary>Max schedule rows kept in memory / shown in the ListView.</summary>
    public const int MaxRows = 600;

    /// <summary>
    /// Compute the payment, totals and amortization schedule.
    /// </summary>
    /// <param name="principal">Loan amount (must be &gt; 0).</param>
    /// <param name="annualRatePercent">Annual interest rate as a percent, e.g. 5.25 (may be 0).</param>
    /// <param name="months">Term in whole months (must be &gt; 0).</param>
    public static Result Compute(double principal, double annualRatePercent, int months)
    {
        try
        {
            if (double.IsNaN(principal) || double.IsInfinity(principal) || principal <= 0)
                return Err("Enter a loan amount greater than 0.", "請輸入大過 0 嘅貸款金額。");
            if (double.IsNaN(annualRatePercent) || double.IsInfinity(annualRatePercent) || annualRatePercent < 0)
                return Err("Interest rate must be 0 or more.", "利率要係 0 或者以上。");
            if (months <= 0)
                return Err("Term must be at least 1 month.", "還款期至少要 1 個月。");
            if (months > 12000)
                return Err("Term is too long (max 1000 years).", "還款期太長（最多 1000 年）。");

            double monthlyRate = (annualRatePercent / 100.0) / 12.0;

            double payment;
            if (monthlyRate <= 0)
            {
                // 0% — straight division.
                payment = principal / months;
            }
            else
            {
                double factor = Math.Pow(1.0 + monthlyRate, -months);
                double denom = 1.0 - factor;
                if (denom <= 0 || double.IsNaN(denom) || double.IsInfinity(denom))
                    return Err("Could not compute a payment for these values.", "呢啲數值計唔到還款額。");
                payment = principal * monthlyRate / denom;
            }

            if (double.IsNaN(payment) || double.IsInfinity(payment) || payment <= 0)
                return Err("Could not compute a payment for these values.", "呢啲數值計唔到還款額。");

            // Build the schedule, capping the number of retained rows.
            var rows = new List<ScheduleRow>(Math.Min(months, MaxRows));
            double balance = principal;
            double totalPaid = 0, totalInterest = 0;
            for (int p = 1; p <= months; p++)
            {
                double interest = balance * monthlyRate;
                double principalPart = payment - interest;
                double thisPayment = payment;

                // Final period: absorb rounding drift so the balance lands on 0.
                if (p == months || principalPart >= balance)
                {
                    principalPart = balance;
                    thisPayment = balance + interest;
                    balance = 0;
                }
                else
                {
                    balance -= principalPart;
                }

                totalPaid += thisPayment;
                totalInterest += interest;

                if (rows.Count < MaxRows)
                {
                    rows.Add(new ScheduleRow
                    {
                        Period = p,
                        Payment = thisPayment,
                        Principal = principalPart,
                        Interest = interest,
                        Balance = balance < 0 ? 0 : balance,
                    });
                }

                if (balance <= 0) break;
            }

            return new Result
            {
                Ok = true,
                MonthlyPayment = payment,
                TotalPaid = totalPaid,
                TotalInterest = totalInterest,
                Months = months,
                Capped = months > MaxRows,
                Schedule = rows,
            };
        }
        catch (Exception ex)
        {
            return Err("Calculation failed: " + ex.Message, "計算失敗：" + ex.Message);
        }
    }

    private static Result Err(string en, string zh) => new() { Ok = false, ErrorEn = en, ErrorZh = zh };
}
