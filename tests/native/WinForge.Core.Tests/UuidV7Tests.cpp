#include "UuidV7Tests.h"

#include "UuidV7.h"

#include <array>
#include <iostream>
#include <string>
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

    std::array<std::uint8_t, 16> Entropy(std::uint8_t randAHigh, std::uint8_t randALow, std::uint8_t variantTail = 0x5D)
    {
        return {
            0x00, 0x01, 0x02, 0x03,
            0x04, 0x05, randAHigh, randALow,
            variantTail, 0x09, 0x0A, 0x0B,
            0x0C, 0x0D, 0x0E, 0x0F,
        };
    }

    int CountLines(std::wstring_view value)
    {
        if (value.empty()) return 0;
        int count = 1;
        for (auto const character : value)
        {
            if (character == L'\n') ++count;
        }
        return count;
    }
}

NativeTestCounts RunUuidV7Tests()
{
    using namespace winforge::core::uuidv7;
    Suite suite;

    MonotonicState state;
    auto generated = NewUuidV7FromEntropy(0x0123456789ABLL, Entropy(0x0A, 0xBC), true, state);
    suite.Expect(
        generated == L"01234567-89ab-7abc-9d09-0a0b0c0d0e0f",
        "UUID v7 writes RFC 9562 timestamp version and variant bits");

    auto decoded = DecodeUuid(generated);
    suite.Expect(
        decoded.ok && decoded.version == 7 && decoded.variant == 2 &&
            decoded.variant_name == L"RFC 4122/9562 (10xx)" &&
            decoded.has_timestamp && decoded.unix_milliseconds == 0x0123456789ABLL,
        "UUID v7 round-trips its embedded Unix-millisecond timestamp");

    suite.Expect(
        FormatTimestampUtc(0) == L"1970-01-01T00:00:00.000Z" &&
            !FormatTimestampLocal(0).empty(),
        "UUID timestamps format in UTC and local time");

    MonotonicState orderingState;
    auto first = NewUuidV7FromEntropy(1'000, Entropy(0x0F, 0xFE), true, orderingState);
    auto second = NewUuidV7FromEntropy(1'000, Entropy(0x00, 0x01), true, orderingState);
    auto overflow = NewUuidV7FromEntropy(1'000, Entropy(0x00, 0x01), true, orderingState);
    auto backwardsClock = NewUuidV7FromEntropy(999, Entropy(0x00, 0x01), true, orderingState);
    auto overflowDecoded = DecodeUuid(overflow);
    auto backwardsDecoded = DecodeUuid(backwardsClock);
    suite.Expect(
        first < second && second < overflow && overflow < backwardsClock &&
            overflowDecoded.has_timestamp && overflowDecoded.unix_milliseconds == 1'001 &&
            backwardsDecoded.has_timestamp && backwardsDecoded.unix_milliseconds == 1'001,
        "UUID v7 monotonic mode survives same-ms counter overflow and a backwards clock");

    MonotonicState plainState;
    auto plain = NewUuidV7FromEntropy(42, Entropy(0x00, 0x01, 0x00), false, plainState);
    auto plainDecoded = DecodeUuid(plain);
    suite.Expect(
        plainDecoded.ok && plainDecoded.version == 7 && plainDecoded.variant == 2 &&
            plainDecoded.has_timestamp && plainDecoded.unix_milliseconds == 42 && !plainState.initialized,
        "UUID v7 non-monotonic mode preserves entropy and does not mutate ordering state");

    auto canonical = DecodeUuid(L" urn:uuid:{01234567-89AB-7ABC-9D09-0A0B0C0D0E0F} ");
    suite.Expect(
        canonical.ok && canonical.canonical == L"01234567-89ab-7abc-9d09-0a0b0c0d0e0f",
        "UUID decoder accepts URN braces and canonicalizes case");

    auto v1 = DecodeUuid(L"f81d4fae-7dec-11d0-a765-00a0c91e6bf6");
    suite.Expect(
        v1.ok && v1.version == 1 && v1.variant == 2 && v1.has_timestamp &&
            FormatTimestampUtc(v1.unix_milliseconds).starts_with(L"1997-02-03T17:43:12.216"),
        "UUID decoder extracts a best-effort UUID v1 timestamp");

    auto v4 = DecodeUuid(L"00112233-4455-4677-8899-aabbccddeeff");
    suite.Expect(
        v4.ok && v4.version == 4 && !v4.has_timestamp,
        "UUID decoder leaves non-time-ordered versions without a timestamp");

    auto empty = DecodeUuid(L"   ");
    auto invalid = DecodeUuid(L"not-a-uuid");
    suite.Expect(
        !empty.ok && empty.error == L"empty" && !invalid.ok && invalid.error == L"notguid",
        "UUID decoder reports guarded empty and malformed input");

    auto const bulk = BulkUuidV7(3, true);
    suite.Expect(
        CountLines(bulk) == 3,
        "UUID v7 bulk generation emits one UUID per requested line");

    auto const clampedLow = BulkUuidV7(0, true);
    suite.Expect(
        CountLines(clampedLow) == MinimumBulkCount,
        "UUID v7 bulk generation clamps the low count");

    std::cout << "\nUUID v7 tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
