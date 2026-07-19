#include "UnitPriceTests.h"

#include "UnitPrice.h"

#include <cmath>
#include <iostream>
#include <limits>
#include <string>
#include <string_view>
#include <vector>

namespace
{
    struct Suite
    {
        NativeTestCounts counts;

        void Expect(bool condition, std::string_view name)
        {
            if (condition)
            {
                ++counts.passed;
                std::cout << "PASS " << name << '\n';
            }
            else
            {
                ++counts.failed;
                std::cerr << "FAIL " << name << '\n';
            }
        }
    };
}

NativeTestCounts RunUnitPriceTests()
{
    using namespace winforge::core::unitprice;
    Suite suite;

    suite.Expect(Clean(std::numeric_limits<double>::quiet_NaN()) == 0.0 &&
        Clean(std::numeric_limits<double>::infinity()) == 0.0 &&
        Clean(-std::numeric_limits<double>::infinity()) == 0.0 &&
        Clean(-2.5) == -2.5,
        "unit price cleans only NaN and infinities while retaining finite negative input");

    std::vector<PriceQuantity> regular{ { 5.0, 250.0 }, { 3.0, 100.0 } };
    auto const normal = Compute(regular);
    suite.Expect(normal.size() == 2 && normal[0].valid && normal[0].is_best &&
        normal[0].per_unit == 0.02 && normal[0].percent_more == 0.0 &&
        normal[1].valid && !normal[1].is_best && normal[1].per_unit == 0.03 &&
        std::abs(normal[1].percent_more - 50.0) < 1e-10,
        "unit price marks the lowest valid per-unit cost and calculates percent more");

    std::vector<PriceQuantity> tolerance{ { 1.0, 100.0 }, { 1.0000000005, 100.0 } };
    auto const ties = Compute(tolerance);
    suite.Expect(ties[0].is_best && ties[1].is_best &&
        ties[0].percent_more == 0.0 && ties[1].percent_more == 0.0,
        "unit price honors the managed relative tie tolerance");

    std::vector<PriceQuantity> invalid{ { 4.0, 0.0 }, { -1.0, 50.0 }, { 2.0, 100.0 } };
    auto const invalidResult = Compute(invalid);
    suite.Expect(!invalidResult[0].valid && std::isnan(invalidResult[0].per_unit) &&
        !invalidResult[1].valid && std::isnan(invalidResult[1].percent_more) &&
        invalidResult[2].valid && invalidResult[2].is_best,
        "unit price excludes zero quantity and negative price rows without losing valid rows");

    std::vector<PriceQuantity> freeRows{ { 0.0, 100.0 }, { 0.0, 250.0 }, { 2.0, 100.0 } };
    auto const freeResult = Compute(freeRows);
    suite.Expect(freeResult[0].is_best && freeResult[1].is_best &&
        !freeResult[2].is_best && std::isinf(freeResult[2].percent_more),
        "unit price marks all free rows best and non-free rows infinitely more expensive");

    std::vector<PriceQuantity> nonFinite{ {
        std::numeric_limits<double>::infinity(), 1.0 }, {
        std::numeric_limits<double>::quiet_NaN(), 10.0 } };
    auto const nonFiniteResult = Compute(nonFinite);
    suite.Expect(nonFiniteResult.size() == 2 && nonFiniteResult[0].valid && nonFiniteResult[0].is_best &&
        nonFiniteResult[0].per_unit == 0.0 && nonFiniteResult[1].valid && nonFiniteResult[1].is_best,
        "unit price cleans non-finite editor values before free-value comparison");

    std::vector<PriceQuantity> overflow{ { std::numeric_limits<double>::max(), 0.0 + std::numeric_limits<double>::min() } };
    auto const overflowResult = Compute(overflow);
    suite.Expect(overflowResult.size() == 1 && overflowResult[0].valid &&
        std::isinf(overflowResult[0].per_unit) && !overflowResult[0].is_best &&
        std::isnan(overflowResult[0].percent_more),
        "unit price preserves the managed no-best overflow division edge case");

    suite.Expect(FormatPerUnit(L"$", 0.0123456, L" g ") == L"$0.0123/g" &&
        FormatPerUnit(L"", 2.0, L"\u00A0") == L"2" &&
        FormatPerUnit(L"$", std::numeric_limits<double>::infinity(), L"g") == L"—",
        "unit price formats invariant per-unit amounts and trims only nonblank units");

    suite.Expect(FormatPercentMore(true, 50.0).empty() &&
        FormatPercentMore(false, std::numeric_limits<double>::quiet_NaN()).empty() &&
        FormatPercentMore(false, std::numeric_limits<double>::infinity()) == L"∞" &&
        FormatPercentMore(false, 12.34) == L"+12.3%",
        "unit price formats best NaN infinity and rounded percentage badges exactly");

    std::vector<Item> clipboardRows{
        { L"Coffee", 5.0, 250.0, L"g" },
        { L"Tea", 3.0, 100.0, L"g" },
    };
    suite.Expect(BuildClipboard(L"$", clipboardRows) ==
        L"Coffee: $5 / 250g = $0.02/g  ★ BEST / 最抵\n"
        L"Tea: $3 / 100g = $0.03/g  (+50%)\n",
        "unit price builds the exact labeled best-value clipboard summary with LF terminators");

    std::vector<Item> fallbackRows{
        { L" \u00A0", std::numeric_limits<double>::quiet_NaN(), 2.50000, L"  ml  " },
        { L"  Juice  ", -1.0, 0.0, L"\u3000" },
    };
    suite.Expect(BuildClipboard(L"€", fallbackRows) ==
        L"#1: €0 / 2.5ml = €0/ml  ★ BEST / 最抵\n"
        L"Juice: €-1 / 0 = —\n",
        "unit price clipboard trims labels units and formats invalid rows without dropping them");

    std::vector<Item> freeClipboard{ { L"Free", 0.0, 1.0, L"ea" }, { L"Paid", 1.0, 1.0, L"ea" } };
    suite.Expect(BuildClipboard(L"", freeClipboard) ==
        L"Free: 0 / 1ea = 0/ea  ★ BEST / 最抵\n"
        L"Paid: 1 / 1ea = 1/ea  (∞)\n",
        "unit price clipboard carries the free-item infinity marker locally");

    suite.Expect(Compute({}).empty() && BuildClipboard(L"$", {}).empty(),
        "unit price accepts empty comparison and clipboard inputs");

    std::cout << "\nunit price tests: " << suite.counts.passed << " passed, "
              << suite.counts.failed << " failed\n";
    return suite.counts;
}
