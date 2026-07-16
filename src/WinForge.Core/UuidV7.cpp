#include "UuidV7.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#include <bcrypt.h>

#include <algorithm>
#include <array>
#include <chrono>
#include <cwctype>
#include <limits>
#include <mutex>
#include <stdexcept>
#include <string>
#include <utility>

#pragma comment(lib, "Bcrypt.lib")

namespace winforge::core::uuidv7
{
    namespace
    {
        constexpr std::uint64_t MaximumTimestamp = (std::uint64_t{ 1 } << 48) - 1;
        constexpr std::int64_t MinimumDateTimeUnixMilliseconds = -62'135'596'800'000LL;
        constexpr std::int64_t MaximumDateTimeUnixMilliseconds = 253'402'300'799'999LL;
        constexpr std::int64_t FileTimeEpochOffsetMilliseconds = 11'644'473'600'000LL;
        constexpr std::uint64_t UuidV1EpochHundredNanoseconds = 0x01B21DD213814000ULL;

        std::mutex g_monotonic_mutex;
        MonotonicState g_monotonic_state;

        [[nodiscard]] std::wstring Trim(std::wstring_view value)
        {
            auto first = value.begin();
            auto last = value.end();
            while (first != last && std::iswspace(*first))
            {
                ++first;
            }
            while (last != first && std::iswspace(*(last - 1)))
            {
                --last;
            }
            return std::wstring(first, last);
        }

        [[nodiscard]] bool StartsWithIgnoreCase(std::wstring_view value, std::wstring_view prefix)
        {
            if (value.size() < prefix.size())
            {
                return false;
            }
            for (std::size_t index = 0; index < prefix.size(); ++index)
            {
                if (std::towlower(value[index]) != std::towlower(prefix[index]))
                {
                    return false;
                }
            }
            return true;
        }

        [[nodiscard]] int HexValue(wchar_t character)
        {
            if (character >= L'0' && character <= L'9') return character - L'0';
            if (character >= L'a' && character <= L'f') return character - L'a' + 10;
            if (character >= L'A' && character <= L'F') return character - L'A' + 10;
            return -1;
        }

        [[nodiscard]] wchar_t HexDigit(std::uint8_t value)
        {
            constexpr wchar_t digits[] = L"0123456789abcdef";
            return digits[value & 0x0F];
        }

        void AppendByte(std::wstring& output, std::uint8_t byte)
        {
            output.push_back(HexDigit(static_cast<std::uint8_t>(byte >> 4)));
            output.push_back(HexDigit(byte));
        }

        [[nodiscard]] std::wstring FormatUuid(std::array<std::uint8_t, 16> const& bytes)
        {
            std::wstring result;
            result.reserve(36);
            for (std::size_t index = 0; index < bytes.size(); ++index)
            {
                if (index == 4 || index == 6 || index == 8 || index == 10)
                {
                    result.push_back(L'-');
                }
                AppendByte(result, bytes[index]);
            }
            return result;
        }

        [[nodiscard]] bool ParseUuid(
            std::wstring_view input,
            std::array<std::uint8_t, 16>& bytes,
            std::wstring& error)
        {
            auto text = Trim(input);
            if (text.empty())
            {
                error = L"empty";
                return false;
            }

            constexpr std::wstring_view urnPrefix = L"urn:uuid:";
            if (StartsWithIgnoreCase(text, urnPrefix))
            {
                text.erase(0, urnPrefix.size());
                text = Trim(text);
            }

            if (text.size() >= 2 &&
                ((text.front() == L'{' && text.back() == L'}') ||
                 (text.front() == L'(' && text.back() == L')')))
            {
                text = text.substr(1, text.size() - 2);
            }

            std::wstring compact;
            compact.reserve(32);
            if (text.size() == 36)
            {
                constexpr std::array<std::size_t, 4> HyphenOffsets{ 8, 13, 18, 23 };
                for (std::size_t index = 0; index < text.size(); ++index)
                {
                    auto const expectedHyphen = std::find(HyphenOffsets.begin(), HyphenOffsets.end(), index) != HyphenOffsets.end();
                    if (expectedHyphen)
                    {
                        if (text[index] != L'-')
                        {
                            error = L"notguid";
                            return false;
                        }
                        continue;
                    }
                    if (text[index] == L'-')
                    {
                        error = L"notguid";
                        return false;
                    }
                    compact.push_back(text[index]);
                }
            }
            else if (text.size() == 32)
            {
                compact = text;
            }
            else
            {
                error = L"notguid";
                return false;
            }

            for (std::size_t index = 0; index < bytes.size(); ++index)
            {
                auto const high = HexValue(compact[index * 2]);
                auto const low = HexValue(compact[(index * 2) + 1]);
                if (high < 0 || low < 0)
                {
                    error = L"notguid";
                    return false;
                }
                bytes[index] = static_cast<std::uint8_t>((high << 4) | low);
            }
            return true;
        }

        void FillRandom(std::uint8_t* data, std::size_t size)
        {
            if (size == 0)
            {
                return;
            }
            if (size > static_cast<std::size_t>((std::numeric_limits<ULONG>::max)()))
            {
                throw std::runtime_error("random request too large");
            }
            auto const status = BCryptGenRandom(
                nullptr,
                data,
                static_cast<ULONG>(size),
                BCRYPT_USE_SYSTEM_PREFERRED_RNG);
            if (status < 0)
            {
                throw std::runtime_error("cryptographic random generator failed");
            }
        }

        [[nodiscard]] std::int64_t CurrentUnixMilliseconds()
        {
            auto const now = std::chrono::time_point_cast<std::chrono::milliseconds>(
                std::chrono::system_clock::now());
            auto const count = now.time_since_epoch().count();
            if (count < 0)
            {
                return 0;
            }
            if (static_cast<std::uint64_t>(count) > MaximumTimestamp)
            {
                throw std::overflow_error("UUID v7 timestamp exceeds RFC 9562 range");
            }
            return count;
        }

        [[nodiscard]] bool IsDisplayableUnixMilliseconds(std::int64_t unixMilliseconds)
        {
            // FileTimeToSystemTime represents 1601 onward. UUID v1 can encode
            // older values, so decode retains the timestamp even when display is
            // unavailable through Windows' calendar conversion API.
            return unixMilliseconds >= -FileTimeEpochOffsetMilliseconds &&
                unixMilliseconds <= MaximumDateTimeUnixMilliseconds;
        }

        [[nodiscard]] bool UnixMillisecondsToSystemTime(std::int64_t unixMilliseconds, SYSTEMTIME& systemTime)
        {
            if (!IsDisplayableUnixMilliseconds(unixMilliseconds))
            {
                return false;
            }

            auto const fileTimeMilliseconds = unixMilliseconds + FileTimeEpochOffsetMilliseconds;
            ULARGE_INTEGER raw{};
            raw.QuadPart = static_cast<ULONGLONG>(fileTimeMilliseconds) * 10'000ULL;
            FILETIME fileTime{};
            fileTime.dwLowDateTime = raw.LowPart;
            fileTime.dwHighDateTime = raw.HighPart;
            return FileTimeToSystemTime(&fileTime, &systemTime) != FALSE;
        }

        [[nodiscard]] std::wstring FormatSystemTime(SYSTEMTIME const& systemTime, std::wstring_view suffix)
        {
            wchar_t buffer[48]{};
            swprintf_s(
                buffer,
                _countof(buffer),
                L"%04u-%02u-%02uT%02u:%02u:%02u.%03u%.*s",
                systemTime.wYear,
                systemTime.wMonth,
                systemTime.wDay,
                systemTime.wHour,
                systemTime.wMinute,
                systemTime.wSecond,
                systemTime.wMilliseconds,
                static_cast<int>(suffix.size()),
                suffix.data());
            return buffer;
        }

        [[nodiscard]] std::wstring FormatOffset(std::int64_t seconds)
        {
            auto const sign = seconds < 0 ? L'-' : L'+';
            auto absolute = seconds < 0 ? -seconds : seconds;
            auto const hours = absolute / 3600;
            auto const minutes = (absolute % 3600) / 60;
            wchar_t buffer[8]{};
            swprintf_s(buffer, _countof(buffer), L"%c%02lld:%02lld", sign, hours, minutes);
            return buffer;
        }

        [[nodiscard]] std::int64_t HundredNanosecondsToUnixMilliseconds(std::uint64_t timestamp)
        {
            auto const difference = timestamp >= UuidV1EpochHundredNanoseconds
                ? static_cast<std::int64_t>(timestamp - UuidV1EpochHundredNanoseconds)
                : -static_cast<std::int64_t>(UuidV1EpochHundredNanoseconds - timestamp);
            return difference / 10'000LL;
        }
    }

    std::wstring NewUuidV7FromEntropy(
        std::int64_t unixMilliseconds,
        std::array<std::uint8_t, 16> entropy,
        bool monotonic,
        MonotonicState& state)
    {
        if (unixMilliseconds < 0)
        {
            unixMilliseconds = 0;
        }
        if (static_cast<std::uint64_t>(unixMilliseconds) > MaximumTimestamp)
        {
            throw std::out_of_range("UUID v7 timestamp exceeds RFC 9562 range");
        }

        auto effectiveMilliseconds = static_cast<std::uint64_t>(unixMilliseconds);
        auto randA = static_cast<std::uint16_t>(
            ((static_cast<std::uint16_t>(entropy[6]) << 8) | entropy[7]) & 0x0FFFu);

        if (monotonic)
        {
            if (state.initialized)
            {
                effectiveMilliseconds = std::max(effectiveMilliseconds, state.last_unix_milliseconds);
                if (effectiveMilliseconds == state.last_unix_milliseconds)
                {
                    if (state.last_rand_a == 0x0FFFu)
                    {
                        if (effectiveMilliseconds == MaximumTimestamp)
                        {
                            throw std::overflow_error("UUID v7 monotonic timestamp range is exhausted");
                        }
                        ++effectiveMilliseconds;
                        // A fresh entropy-sourced value is valid in the next
                        // millisecond; the timestamp increment preserves ordering.
                    }
                    else
                    {
                        randA = static_cast<std::uint16_t>(state.last_rand_a + 1u);
                    }
                }
            }

            state.last_unix_milliseconds = effectiveMilliseconds;
            state.last_rand_a = randA;
            state.initialized = true;
        }

        entropy[0] = static_cast<std::uint8_t>((effectiveMilliseconds >> 40) & 0xFFu);
        entropy[1] = static_cast<std::uint8_t>((effectiveMilliseconds >> 32) & 0xFFu);
        entropy[2] = static_cast<std::uint8_t>((effectiveMilliseconds >> 24) & 0xFFu);
        entropy[3] = static_cast<std::uint8_t>((effectiveMilliseconds >> 16) & 0xFFu);
        entropy[4] = static_cast<std::uint8_t>((effectiveMilliseconds >> 8) & 0xFFu);
        entropy[5] = static_cast<std::uint8_t>(effectiveMilliseconds & 0xFFu);
        entropy[6] = static_cast<std::uint8_t>(0x70u | ((randA >> 8) & 0x0Fu));
        entropy[7] = static_cast<std::uint8_t>(randA & 0xFFu);
        entropy[8] = static_cast<std::uint8_t>(0x80u | (entropy[8] & 0x3Fu));
        return FormatUuid(entropy);
    }

    std::wstring NewUuidV7(bool monotonic)
    {
        std::array<std::uint8_t, 16> entropy{};
        FillRandom(entropy.data(), entropy.size());
        auto const now = CurrentUnixMilliseconds();

        if (!monotonic)
        {
            MonotonicState unused;
            return NewUuidV7FromEntropy(now, entropy, false, unused);
        }

        std::scoped_lock lock(g_monotonic_mutex);
        return NewUuidV7FromEntropy(now, entropy, true, g_monotonic_state);
    }

    std::wstring BulkUuidV7(int count, bool monotonic)
    {
        count = std::clamp(count, MinimumBulkCount, MaximumBulkCount);
        std::wstring result;
        result.reserve(static_cast<std::size_t>(count) * 37);
        for (int index = 0; index < count; ++index)
        {
            if (index > 0)
            {
                result.push_back(L'\n');
            }
            result.append(NewUuidV7(monotonic));
        }
        return result;
    }

    DecodeResult DecodeUuid(std::wstring_view input)
    {
        DecodeResult result;
        std::array<std::uint8_t, 16> bytes{};
        if (!ParseUuid(input, bytes, result.error))
        {
            return result;
        }

        result.ok = true;
        result.canonical = FormatUuid(bytes);
        result.version = static_cast<int>((bytes[6] >> 4) & 0x0F);

        auto const highNibble = static_cast<std::uint8_t>(bytes[8] >> 4);
        if ((highNibble & 0x8u) == 0)
        {
            result.variant = 0;
            result.variant_name = L"NCS (0xxx)";
        }
        else if ((highNibble & 0x4u) == 0)
        {
            result.variant = 2;
            result.variant_name = L"RFC 4122/9562 (10xx)";
        }
        else if ((highNibble & 0x2u) == 0)
        {
            result.variant = 6;
            result.variant_name = L"Microsoft (110x)";
        }
        else
        {
            result.variant = 7;
            result.variant_name = L"Reserved (111x)";
        }

        if (result.version == 7)
        {
            auto const timestamp =
                (static_cast<std::uint64_t>(bytes[0]) << 40) |
                (static_cast<std::uint64_t>(bytes[1]) << 32) |
                (static_cast<std::uint64_t>(bytes[2]) << 24) |
                (static_cast<std::uint64_t>(bytes[3]) << 16) |
                (static_cast<std::uint64_t>(bytes[4]) << 8) |
                static_cast<std::uint64_t>(bytes[5]);
            if (timestamp <= static_cast<std::uint64_t>(MaximumDateTimeUnixMilliseconds))
            {
                result.has_timestamp = true;
                result.unix_milliseconds = static_cast<std::int64_t>(timestamp);
            }
        }
        else if (result.version == 1)
        {
            auto const timestamp =
                (static_cast<std::uint64_t>(bytes[6] & 0x0Fu) << 56) |
                (static_cast<std::uint64_t>(bytes[7]) << 48) |
                (static_cast<std::uint64_t>(bytes[4]) << 40) |
                (static_cast<std::uint64_t>(bytes[5]) << 32) |
                (static_cast<std::uint64_t>(bytes[0]) << 24) |
                (static_cast<std::uint64_t>(bytes[1]) << 16) |
                (static_cast<std::uint64_t>(bytes[2]) << 8) |
                static_cast<std::uint64_t>(bytes[3]);
            auto const unixMilliseconds = HundredNanosecondsToUnixMilliseconds(timestamp);
            if (unixMilliseconds >= MinimumDateTimeUnixMilliseconds &&
                unixMilliseconds <= MaximumDateTimeUnixMilliseconds)
            {
                result.has_timestamp = true;
                result.unix_milliseconds = unixMilliseconds;
            }
        }

        return result;
    }

    std::wstring FormatTimestampUtc(std::int64_t unixMilliseconds)
    {
        SYSTEMTIME utc{};
        if (!UnixMillisecondsToSystemTime(unixMilliseconds, utc))
        {
            return {};
        }
        return FormatSystemTime(utc, L"Z");
    }

    std::wstring FormatTimestampLocal(std::int64_t unixMilliseconds)
    {
        SYSTEMTIME utc{};
        if (!UnixMillisecondsToSystemTime(unixMilliseconds, utc))
        {
            return {};
        }

        SYSTEMTIME local{};
        if (!SystemTimeToTzSpecificLocalTime(nullptr, &utc, &local))
        {
            return {};
        }

        FILETIME utcFileTime{};
        FILETIME localFileTime{};
        if (!SystemTimeToFileTime(&utc, &utcFileTime) || !SystemTimeToFileTime(&local, &localFileTime))
        {
            return {};
        }
        ULARGE_INTEGER utcRaw{};
        utcRaw.LowPart = utcFileTime.dwLowDateTime;
        utcRaw.HighPart = utcFileTime.dwHighDateTime;
        ULARGE_INTEGER localRaw{};
        localRaw.LowPart = localFileTime.dwLowDateTime;
        localRaw.HighPart = localFileTime.dwHighDateTime;
        auto const offsetSeconds =
            (static_cast<std::int64_t>(localRaw.QuadPart) - static_cast<std::int64_t>(utcRaw.QuadPart)) / 10'000'000LL;
        return FormatSystemTime(local, FormatOffset(offsetSeconds));
    }
}
