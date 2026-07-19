#pragma once

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::baseconvert
{
    inline constexpr int MinBase = 2;
    inline constexpr int MaxBase = 36;

    enum class BitOp;

    // A small, dependency-free signed arbitrary-precision integer. It keeps
    // the native migration independent of the managed BigInteger oracle while
    // retaining its unbounded base-conversion and bitwise contract.
    class Integer
    {
    public:
        [[nodiscard]] bool IsZero() const noexcept;
        [[nodiscard]] bool IsNegative() const noexcept;

        // Deliberately public for the dependency-free arithmetic helpers in
        // BaseConvert.cpp; callers use the conversion functions below rather
        // than treating this as a general-purpose numeric surface.
    public:
        bool m_negative{};
        std::vector<std::uint32_t> m_words; // little-endian magnitude

        void Normalize() noexcept;

        friend bool TryParse(std::wstring_view text, int radix, Integer& value) noexcept;
        friend bool TryParseOperand(std::wstring_view text, Integer& value) noexcept;
        friend std::wstring ToBase(Integer const& value, int radix) noexcept;
        friend std::wstring ToGroupedBinary(Integer const& value) noexcept;
        friend std::wstring ToHexPrefixed(Integer const& value) noexcept;
        friend std::int64_t BitLength(Integer const& value) noexcept;
        friend bool FitsIn64Bits(Integer const& value) noexcept;
        friend std::wstring To64BitBinary(Integer const& value) noexcept;
        friend Integer Evaluate(BitOp op, Integer const& a, Integer const& b, int shift) noexcept;
    };

    // Matches the managed String.Trim()/String.IsNullOrWhiteSpace character set
    // used by the page for parsing and diagnostics.
    [[nodiscard]] std::wstring_view TrimManagedWhitespace(std::wstring_view text) noexcept;
    [[nodiscard]] bool IsBlank(std::wstring_view text) noexcept;
    [[nodiscard]] bool TryParse(std::wstring_view text, int radix, Integer& value) noexcept;
    [[nodiscard]] bool TryParseOperand(std::wstring_view text, Integer& value) noexcept;

    // Returns lowercase digits with no radix prefix. Invalid radices return an
    // empty string, matching BaseConvertService.ToBase.
    [[nodiscard]] std::wstring ToBase(Integer const& value, int radix) noexcept;
    [[nodiscard]] std::wstring ToGroupedBinary(Integer const& value) noexcept;
    [[nodiscard]] std::wstring ToHexPrefixed(Integer const& value) noexcept;
    [[nodiscard]] std::int64_t BitLength(Integer const& value) noexcept;
    [[nodiscard]] bool FitsIn64Bits(Integer const& value) noexcept;
    [[nodiscard]] std::wstring To64BitBinary(Integer const& value) noexcept;

    enum class BitOp
    {
        And,
        Or,
        Xor,
        Nand,
        Nor,
        LeftShift,
        RightShift,
    };

    [[nodiscard]] Integer Evaluate(BitOp op, Integer const& a, Integer const& b, int shift) noexcept;
}
