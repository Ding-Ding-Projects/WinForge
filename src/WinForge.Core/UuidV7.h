#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <string_view>

namespace winforge::core::uuidv7
{
    constexpr int MinimumBulkCount = 1;
    constexpr int MaximumBulkCount = 1000;

    // State is caller-owned for the deterministic overload so tests and callers
    // can prove same-millisecond, backwards-clock, and counter-overflow behavior
    // without depending on the system clock or a probabilistic random source.
    struct MonotonicState
    {
        std::uint64_t last_unix_milliseconds{};
        std::uint16_t last_rand_a{};
        bool initialized{ false };
    };

    struct DecodeResult
    {
        bool ok{ false };
        std::wstring error{};
        int version{};
        int variant{};
        std::wstring variant_name{};
        bool has_timestamp{ false };
        std::int64_t unix_milliseconds{};
        std::wstring canonical{};
    };

    // Produces an RFC 9562 UUIDv7 with BCrypt cryptographic randomness. Monotonic
    // mode guarantees lexical order within this process even when the clock moves
    // backwards or the 12-bit rand_a counter is exhausted.
    [[nodiscard]] std::wstring NewUuidV7(bool monotonic);
    [[nodiscard]] std::wstring BulkUuidV7(int count, bool monotonic);

    // Deterministic core entry point used by the test suite. The caller owns state
    // and any synchronization around it. `entropy` supplies the non-timestamp bits.
    [[nodiscard]] std::wstring NewUuidV7FromEntropy(
        std::int64_t unix_milliseconds,
        std::array<std::uint8_t, 16> entropy,
        bool monotonic,
        MonotonicState& state);

    // Parses canonical, compact, braced/parenthesized, and urn:uuid: UUID values.
    // v7 timestamps are decoded from the RFC 9562 48-bit Unix-ms field; v1 values
    // are decoded best-effort to milliseconds for compatibility with the managed oracle.
    [[nodiscard]] DecodeResult DecodeUuid(std::wstring_view input);

    // Formatting helpers return an empty string when the timestamp cannot be
    // represented by the Windows calendar APIs used for display.
    [[nodiscard]] std::wstring FormatTimestampUtc(std::int64_t unix_milliseconds);
    [[nodiscard]] std::wstring FormatTimestampLocal(std::int64_t unix_milliseconds);
}
