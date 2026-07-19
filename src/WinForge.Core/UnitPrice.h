#pragma once

#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::unitprice
{
    struct PriceQuantity
    {
        double price{};
        double quantity{};
    };

    struct Item
    {
        std::wstring label;
        double price{};
        double quantity{};
        std::wstring unit;
    };

    struct Computed
    {
        bool valid{};
        double per_unit{};
        bool is_best{};
        double percent_more{};
    };

    // Mirrors UnitPriceService.Clean: non-finite editor values are harmless
    // zeroes, while finite negative values stay visible to the caller so the
    // comparison can mark them invalid.
    [[nodiscard]] double Clean(double value) noexcept;

    // Computes local price-per-unit comparisons in source order. A free item
    // makes every non-free valid item infinitely more expensive, and entries
    // whose division overflows deliberately receive no best marker just like
    // the managed oracle.
    [[nodiscard]] std::vector<Computed> Compute(std::span<PriceQuantity const> rows);

    // Invariant-format presentation helpers for the native page and its
    // clipboard summary. No locale-sensitive formatting is used here.
    [[nodiscard]] std::wstring FormatPerUnit(
        std::wstring_view currency,
        double perUnit,
        std::wstring_view unit);
    [[nodiscard]] std::wstring FormatPercentMore(bool isBest, double percentMore);

    // Builds the exact local plain-text summary used only after an explicit
    // Copy action. Every source row, including invalid rows, is retained.
    [[nodiscard]] std::wstring BuildClipboard(
        std::wstring_view currency,
        std::span<Item const> items);
}
