#include "UnitPrice.h"

#include "DesignTools.h"

#include <cmath>
#include <limits>

namespace
{
    [[nodiscard]] double NaN() noexcept
    {
        return std::numeric_limits<double>::quiet_NaN();
    }

    [[nodiscard]] bool IsDotNetWhitespace(wchar_t value) noexcept
    {
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] std::wstring_view Trim(std::wstring_view value) noexcept
    {
        std::size_t first{};
        auto last = value.size();
        while (first < last && IsDotNetWhitespace(value[first])) ++first;
        while (last > first && IsDotNetWhitespace(value[last - 1])) --last;
        return value.substr(first, last - first);
    }

    [[nodiscard]] std::wstring FormatFinite(double value, int fractionalDigits)
    {
        // .NET custom numeric formatting does not retain a negative sign for
        // negative zero. Normalizing it here also avoids a platform-dependent
        // signbit result from the shared exact formatter.
        if (value == 0.0) value = 0.0;
        return winforge::core::aspectratio::FormatDisplayNumber(value, fractionalDigits, L".");
    }
}

namespace winforge::core::unitprice
{
    double Clean(double value) noexcept
    {
        return std::isfinite(value) ? value : 0.0;
    }

    std::vector<Computed> Compute(std::span<PriceQuantity const> rows)
    {
        std::vector<Computed> results;
        try
        {
            results.reserve(rows.size());
            auto bestPerUnit = std::numeric_limits<double>::infinity();
            for (auto const& row : rows)
            {
                Computed computed;
                computed.per_unit = NaN();
                computed.percent_more = NaN();

                auto const price = Clean(row.price);
                auto const quantity = Clean(row.quantity);
                if (quantity > 0.0 && price >= 0.0)
                {
                    computed.valid = true;
                    computed.per_unit = price / quantity;
                    if (computed.per_unit < bestPerUnit)
                    {
                        bestPerUnit = computed.per_unit;
                    }
                }
                results.push_back(computed);
            }

            if (std::isfinite(bestPerUnit) && bestPerUnit > 0.0)
            {
                for (auto& computed : results)
                {
                    if (!computed.valid) continue;
                    computed.is_best = computed.per_unit <= bestPerUnit * (1.0 + 1.0e-9);
                    computed.percent_more = computed.is_best
                        ? 0.0
                        : (computed.per_unit - bestPerUnit) / bestPerUnit * 100.0;
                }
            }
            else if (std::isfinite(bestPerUnit))
            {
                // A genuine zero-price row is free. Keep the managed epsilon
                // so a subnormal rounded result is still presented as a tie.
                for (auto& computed : results)
                {
                    if (!computed.valid) continue;
                    computed.is_best = computed.per_unit <= 1.0e-12;
                    computed.percent_more = computed.is_best
                        ? 0.0
                        : std::numeric_limits<double>::infinity();
                }
            }
        }
        catch (...)
        {
            // The managed helper is intentionally never-throwing. Preserve any
            // prefix that was already computed rather than failing UI updates.
        }
        return results;
    }

    std::wstring FormatPerUnit(
        std::wstring_view currency,
        double perUnit,
        std::wstring_view unit)
    {
        if (!std::isfinite(perUnit)) return L"—";

        std::wstring output(currency);
        output += FormatFinite(perUnit, 4);
        if (auto const trimmed = Trim(unit); !trimmed.empty())
        {
            output.push_back(L'/');
            output.append(trimmed);
        }
        return output;
    }

    std::wstring FormatPercentMore(bool isBest, double percentMore)
    {
        if (isBest || std::isnan(percentMore)) return {};
        if (std::isinf(percentMore)) return L"∞";
        return L"+" + FormatFinite(percentMore, 1) + L"%";
    }

    std::wstring BuildClipboard(
        std::wstring_view currency,
        std::span<Item const> items)
    {
        std::wstring output;
        try
        {
            std::vector<PriceQuantity> pairs;
            pairs.reserve(items.size());
            for (auto const& item : items)
            {
                pairs.push_back({ item.price, item.quantity });
            }
            auto const computed = Compute(pairs);

            for (std::size_t index{}; index < items.size(); ++index)
            {
                auto const& item = items[index];
                auto const& result = index < computed.size() ? computed[index] : Computed{
                    .per_unit = NaN(), .percent_more = NaN() };
                auto const label = Trim(item.label);
                auto const unit = Trim(item.unit);

                if (label.empty())
                {
                    output += L"#" + std::to_wstring(index + 1);
                }
                else
                {
                    output.append(label);
                }
                output += L": ";
                output.append(currency);
                output += FormatFinite(Clean(item.price), 2);
                output += L" / ";
                output += FormatFinite(Clean(item.quantity), 4);
                output.append(unit);
                output += L" = ";
                output += FormatPerUnit(currency, result.per_unit, unit);
                if (result.is_best)
                {
                    output += L"  ★ BEST / 最抵";
                }
                else if (auto const percent = FormatPercentMore(result.is_best, result.percent_more); !percent.empty())
                {
                    output += L"  (";
                    output += percent;
                    output += L")";
                }
                output.push_back(L'\n');
            }
        }
        catch (...)
        {
            // Match the oracle's best-effort clipboard summary behavior.
        }
        return output;
    }
}
