#pragma once

#include <cstddef>
#include <cstdint>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::passgen
{
    inline constexpr std::wstring_view Lowercase = L"abcdefghijklmnopqrstuvwxyz";
    inline constexpr std::wstring_view Uppercase = L"ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    inline constexpr std::wstring_view Digits = L"0123456789";
    inline constexpr std::wstring_view Symbols = L"!@#$%^&*()-_=+[]{};:,.?/";
    inline constexpr std::wstring_view Ambiguous = L"O0Il1|";

    struct PasswordOptions
    {
        int length{ 16 };
        bool lower{ true };
        bool upper{ true };
        bool digits{ true };
        bool symbols{ true };
        bool avoid_ambiguous{ false };
        bool no_repeats{ false };
    };

    struct PassphraseOptions
    {
        int word_count{ 4 };
        std::wstring separator{ L"-" };
        bool capitalize{ false };
        bool append_digit{ false };
    };

    enum class ErrorCode : std::uint8_t
    {
        NoCharacterSets,
        EmptyPool,
        LengthTooShort,
        NoRepeatsPoolTooSmall,
        WordCountTooSmall,
        RandomFailure,
    };

    class GenerationError final : public std::runtime_error
    {
    public:
        GenerationError(ErrorCode code, char const* message);

        [[nodiscard]] ErrorCode Code() const noexcept;

    private:
        ErrorCode m_code;
    };

    /// <summary>
    /// The only source of entropy used by the pure native generator. Tests inject a
    /// deterministic source; production uses SystemRandomSource below.
    /// </summary>
    class RandomSource
    {
    public:
        virtual ~RandomSource() = default;
        [[nodiscard]] virtual std::uint32_t NextUInt32() = 0;
    };

    class SystemRandomSource final : public RandomSource
    {
    public:
        [[nodiscard]] std::uint32_t NextUInt32() override;
    };

    /// <summary>
    /// Uniformly chooses [0, upper_exclusive) with rejection sampling. It never
    /// introduces modulo bias, even when the range does not divide 2^32.
    /// </summary>
    [[nodiscard]] std::uint32_t UniformIndex(RandomSource& source, std::uint32_t upper_exclusive);

    [[nodiscard]] std::wstring BuildPool(PasswordOptions const& options);
    [[nodiscard]] std::wstring GeneratePassword(PasswordOptions const& options, RandomSource& source);
    [[nodiscard]] std::wstring GeneratePassword(PasswordOptions const& options);
    [[nodiscard]] std::wstring GeneratePassphrase(PassphraseOptions const& options, RandomSource& source);
    [[nodiscard]] std::wstring GeneratePassphrase(PassphraseOptions const& options);
    [[nodiscard]] std::vector<std::wstring> GeneratePasswordBatch(
        PasswordOptions const& options,
        int count,
        RandomSource& source);
    [[nodiscard]] std::vector<std::wstring> GeneratePassphraseBatch(
        PassphraseOptions const& options,
        int count,
        RandomSource& source);
    [[nodiscard]] std::wstring JoinLines(std::vector<std::wstring> const& values);

    [[nodiscard]] double PasswordEntropyBits(int length, int pool_size);
    [[nodiscard]] double PassphraseEntropyBits(int words, int dictionary_size, bool append_digit);

    [[nodiscard]] std::size_t DictionarySize() noexcept;
    [[nodiscard]] std::wstring_view DictionaryWord(std::size_t index);
}
