#include "UnixPermTests.h"

#include "UnixPerm.h"

#include <iostream>
#include <string_view>

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

NativeTestCounts RunUnixPermTests()
{
    using namespace winforge::core::unixperm;
    Suite suite;

    auto const mode0644 = static_cast<Mode>(OwnerR | OwnerW | GroupR | OtherR);
    suite.Expect(ToOctal(mode0644) == L"0644", "chmod emits the managed four-digit octal default");
    suite.Expect(ToChmodOctal(mode0644) == L"644", "chmod omits a zero special digit in command form");
    suite.Expect(ToSymbolic(mode0644) == L"rw-r--r--", "chmod renders the managed symbolic default");

    auto const mode7755 = static_cast<Mode>(SetUid | SetGid | Sticky | OwnerR | OwnerW | OwnerX |
        GroupR | GroupX | OtherR | OtherX);
    suite.Expect(ToOctal(mode7755) == L"7755" && ToChmodOctal(mode7755) == L"7755",
        "chmod retains all special bits in octal command form");
    suite.Expect(ToSymbolic(mode7755) == L"rwsr-sr-t", "chmod renders lowercase special execute letters");

    auto const specialWithoutExecute = static_cast<Mode>(SetUid | SetGid | Sticky | OwnerR | GroupR | OtherR);
    suite.Expect(ToSymbolic(specialWithoutExecute) == L"r-Sr-Sr-T",
        "chmod renders uppercase special letters when execute is absent");
    suite.Expect(Normalize(0xFFFFu) == PermMask, "chmod masks values to twelve permission bits");

    Mode parsed{};
    suite.Expect(TryParseOctal(L"755", parsed) && parsed == static_cast<Mode>(0x1ED),
        "chmod parses three-digit octal");
    suite.Expect(TryParseOctal(L"  0755  ", parsed) && ToOctal(parsed) == L"0755",
        "chmod trims and parses a four-digit octal mode");
    suite.Expect(TryParseOctal(L"\x3000" L"0O4755\x00A0", parsed) && ToOctal(parsed) == L"4755",
        "chmod accepts the managed Unicode trim set and case-insensitive 0o prefix");
    suite.Expect(!TryParseOctal(L"0x755", parsed), "chmod rejects a hexadecimal prefix");
    suite.Expect(!TryParseOctal(L"0888", parsed), "chmod rejects non-octal digits atomically");
    suite.Expect(!TryParseOctal(L"12345", parsed), "chmod rejects more than four octal digits");
    suite.Expect(!TryParseOctal(L"   ", parsed), "chmod rejects empty octal input");

    suite.Expect(TryParseSymbolic(L"rwxr-xr-x", parsed) && ToOctal(parsed) == L"0755",
        "chmod parses a standard symbolic mode");
    suite.Expect(TryParseSymbolic(L"-rwsr-sr-t", parsed) && ToOctal(parsed) == L"7755",
        "chmod accepts an ls file-type prefix and special execute letters");
    suite.Expect(TryParseSymbolic(L"r-Sr-Sr-T", parsed) && ToSymbolic(parsed) == L"r-Sr-Sr-T",
        "chmod round-trips uppercase special letters");
    suite.Expect(!TryParseSymbolic(L"rwxr-xr-z", parsed), "chmod rejects an invalid symbolic character");
    suite.Expect(!TryParseSymbolic(L"rwxr-x", parsed), "chmod rejects a short symbolic mode");

    auto everyModeRoundTrips = true;
    for (std::uint32_t raw{}; raw <= PermMask; ++raw)
    {
        auto const expected = static_cast<Mode>(raw);
        Mode fromOctal{};
        Mode fromSymbolic{};
        if (!TryParseOctal(ToOctal(expected), fromOctal) || fromOctal != expected ||
            !TryParseSymbolic(ToSymbolic(expected), fromSymbolic) || fromSymbolic != expected)
        {
            everyModeRoundTrips = false;
            break;
        }
    }
    suite.Expect(everyModeRoundTrips, "chmod round-trips all 4096 permission modes through both representations");

    std::cout << "\nchmod tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
