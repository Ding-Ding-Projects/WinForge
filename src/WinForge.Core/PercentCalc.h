#pragma once

#include <cstdint>
#include <string>
#include <string_view>

namespace winforge::core::percentcalc
{
    // Native equivalent of PercentCalcService.CalcResult. `value` is suitable
    // for direct display and explicit clipboard copy; it is empty on invalid
    // input, while a valid arithmetic overflow formats as an em dash just as
    // the managed service does.
    struct CalcResult
    {
        bool ok{ false };
        std::wstring value{};
        double number{};
    };

    struct TipResult
    {
        bool ok{ false };
        double tipAmount{};
        double total{};
        double perPerson{};
    };

    struct RatioResult
    {
        bool ok{ false };
        std::int64_t a{};
        std::int64_t b{};
    };

    // Accepts surrounding Unicode whitespace and one trailing percent sign.
    // The active UI passes its current decimal separator, then this routine
    // also accepts the invariant decimal point to mirror the managed fallback.
    [[nodiscard]] bool TryParse(
        std::wstring_view raw,
        double& value,
        std::wstring_view currentDecimalSeparator = L".");

    // Managed-equivalent `Math.Round(value, 6, AwayFromZero)` followed by
    // `0.######` formatting. Non-finite values deliberately become an em dash.
    [[nodiscard]] std::wstring Format(
        double value,
        std::wstring_view decimalSeparator = L".");

    [[nodiscard]] CalcResult PercentOf(
        std::wstring_view x,
        std::wstring_view y,
        std::wstring_view decimalSeparator = L".");
    [[nodiscard]] CalcResult WhatPercent(
        std::wstring_view x,
        std::wstring_view y,
        std::wstring_view decimalSeparator = L".");
    [[nodiscard]] CalcResult PercentChange(
        std::wstring_view a,
        std::wstring_view b,
        std::wstring_view decimalSeparator = L".");
    [[nodiscard]] CalcResult AdjustBy(
        std::wstring_view y,
        std::wstring_view x,
        bool increase,
        std::wstring_view decimalSeparator = L".");
    [[nodiscard]] TipResult Tip(
        std::wstring_view bill,
        std::wstring_view tipPercent,
        std::wstring_view split,
        std::wstring_view currentDecimalSeparator = L".");
    [[nodiscard]] RatioResult SimplifyRatio(
        std::wstring_view a,
        std::wstring_view b,
        std::wstring_view currentDecimalSeparator = L".");
}
