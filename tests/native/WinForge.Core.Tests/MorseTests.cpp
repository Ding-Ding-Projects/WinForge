#include "MorseTests.h"

#include "Morse.h"

#include <cmath>
#include <cstdint>
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

    [[nodiscard]] bool Near(double left, double right) noexcept
    {
        return std::abs(left - right) <= 1e-12;
    }

    [[nodiscard]] bool HasFlash(
        std::vector<winforge::core::morse::Flash> const& timeline,
        std::size_t index,
        bool on,
        int units) noexcept
    {
        return index < timeline.size() && timeline[index].on == on && timeline[index].units == units;
    }
}

NativeTestCounts RunMorseTests()
{
    using namespace winforge::core::morse;
    Suite suite;

    auto encoded = ToMorse(L"AZ 09.,?", L" ", L" / ");
    suite.Expect(encoded.text == L".- --.. / ----- ----. .-.-.- --..-- ..--.." && encoded.unknown.empty(),
        "Morse encodes letters digits and canonical punctuation");

    auto decoded = FromMorse(encoded.text);
    suite.Expect(decoded == L"AZ 09.,?", "Morse decodes a standard encoded sentence");

    encoded = ToMorse(L"0123456789.,?'!/()&:;=+-_\"$@", L" ", L" / ");
    suite.Expect(
        FromMorse(encoded.text) == L"0123456789.,?'!/()&:;=+-_\"$@",
        "Morse round trips every registered digit and punctuation token");

    encoded = ToMorse(L"a\u017Fb", L" ", L" / ");
    suite.Expect(encoded.text == L".- ... -..." && encoded.unknown.empty(),
        "Morse uses invariant UTF-16 uppercase so long s maps to S");

    encoded = ToMorse(L"A\tB\r\nC", L" ", L" / ");
    suite.Expect(encoded.text == L".- / -... / -.-.",
        "Morse encoding splits words only on managed ASCII whitespace separators");

    encoded = ToMorse(L"A B", L"", L"");
    suite.Expect(encoded.text == L".- / -...", "Morse encoding restores managed default separators when both are empty");

    suite.Expect(ToMorse(L"A B", L" ", L"   ").text == L".-   -...",
        "Morse supports the triple-space word separator preset");
    suite.Expect(ToMorse(L"AB", L"  ", L" / ").text == L".-  -...",
        "Morse supports the double-space letter separator preset");

    encoded = ToMorse(L"A\u2603\u2603B", L" ", L" / ");
    suite.Expect(encoded.text == L".- # # -..." && encoded.unknown.size() == 1 && encoded.unknown[0] == L'\u2603',
        "Morse marks unknown characters while retaining a unique encounter-order warning list");

    std::wstring emoji;
    emoji.push_back(static_cast<wchar_t>(0xD83D));
    emoji.push_back(static_cast<wchar_t>(0xDE00));
    encoded = ToMorse(emoji, L" ", L" / ");
    suite.Expect(encoded.text == L"# #" && encoded.unknown.size() == 2 &&
            static_cast<std::uint16_t>(encoded.unknown[0]) == 0xD83Du &&
            static_cast<std::uint16_t>(encoded.unknown[1]) == 0xDE00u,
        "Morse preserves managed per-UTF-16-unit unknown-character behavior");

    suite.Expect(FromMorse(L"\u00B7 \u2013\u2022\u2022 | _") == L"ED T",
        "Morse decoding accepts middle-dot bullet dash and vertical-bar aliases");
    suite.Expect(FromMorse(L".-   -...") == L"AB",
        "Morse decoding keeps the managed triple-space letter-gap quirk");
    suite.Expect(FromMorse(L".- // -...") == L"A B",
        "Morse decoding collapses empty slash segments into one word boundary");
    suite.Expect(FromMorse(L"# .-") == L"#A", "Morse decoding preserves the explicit placeholder token");
    suite.Expect(FromMorse(L".- ^") == L"A\xFFFD", "Morse decoding marks an unrecognized token with replacement text");
    suite.Expect(FromMorse(L"\n.-\r\n") == L"A", "Morse decoding trims outer managed whitespace");
    suite.Expect(FromMorse(L".-\n-...") == L"\xFFFD",
        "Morse decoding keeps an internal newline inside one unrecognized token");
    suite.Expect(FromMorse(L"\x00A0") == L"" && FromMorse(L"\x00A0.-\x00A0") == L"A",
        "Morse decoding follows managed Unicode whitespace checks at outer edges");

    auto timeline = BuildTimeline(L"SOS");
    suite.Expect(timeline.size() == 17 &&
            HasFlash(timeline, 0, true, 1) && HasFlash(timeline, 1, false, 1) &&
            HasFlash(timeline, 5, false, 3) && HasFlash(timeline, 6, true, 3) &&
            HasFlash(timeline, 11, false, 3) && HasFlash(timeline, 16, true, 1),
        "Morse flash timeline uses dot dash intra-letter and letter timing units");

    timeline = BuildTimeline(L"E T");
    suite.Expect(timeline.size() == 3 && HasFlash(timeline, 0, true, 1) &&
            HasFlash(timeline, 1, false, 7) && HasFlash(timeline, 2, true, 3),
        "Morse flash timeline uses a seven-unit word gap");

    timeline = BuildTimeline(L"E \u2603 T");
    suite.Expect(timeline.size() == 4 && HasFlash(timeline, 0, true, 1) &&
            HasFlash(timeline, 1, false, 7) && HasFlash(timeline, 2, false, 7) &&
            HasFlash(timeline, 3, true, 3),
        "Morse flash timeline skips unknown characters without collapsing source word gaps");

    suite.Expect(BuildTimeline(L"").empty(), "Morse flash timeline is empty for empty text");
    suite.Expect(Near(UnitMsForWpm(15.0), 80.0) && Near(UnitMsForWpm(1.0), 1200.0) &&
            Near(UnitMsForWpm(60.0), 20.0),
        "Morse WPM timing follows the PARIS standard across its valid range");
    suite.Expect(Near(UnitMsForWpm(0.0), 1200.0) &&
            Near(UnitMsForWpm(61.0), 20.0) &&
            Near(UnitMsForWpm((std::numeric_limits<double>::quiet_NaN)()), 1200.0) &&
            Near(UnitMsForWpm((std::numeric_limits<double>::infinity)()), 20.0) &&
            Near(UnitMsForWpm(-(std::numeric_limits<double>::infinity)()), 1200.0),
        "Morse WPM timing clamps invalid nonfinite and out-of-range values like the managed service");

    std::cout << "\nMorse tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
