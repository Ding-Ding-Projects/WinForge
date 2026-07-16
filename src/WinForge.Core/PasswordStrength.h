#pragma once

#include <cstdint>
#include <string>
#include <string_view>

namespace winforge::core::passwordstrength
{
    struct Result
    {
        std::int32_t length{};
        std::int32_t pool_size{};
        double entropy_bits{};
        std::int32_t band{};
        double fraction{};
        bool has_lower{};
        bool has_upper{};
        bool has_digit{};
        bool has_symbol{};
        bool has_space{};
        bool no_repeats{};
        bool no_sequences{};
        bool len8{};
        bool len12{};
        bool len16{};
        bool is_common{};
        double online_seconds{};
        double offline_gpu_seconds{};
        double fast_seconds{};
    };

    enum class HumanTimeLanguage : std::uint8_t
    {
        English,
        Cantonese,
    };

    /// <summary>
    /// Analyses one in-memory password using the current managed Password Strength
    /// contract. It never stores, logs, persists, or transmits the input.
    /// </summary>
    [[nodiscard]] Result Analyze(std::wstring_view password) noexcept;

    /// <summary>
    /// Formats a crack-time estimate with the managed threshold and banker's-rounding
    /// behavior. It returns a localised fallback instead of throwing.
    /// </summary>
    [[nodiscard]] std::wstring HumanTime(double seconds, HumanTimeLanguage language) noexcept;
}
