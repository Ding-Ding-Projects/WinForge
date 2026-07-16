#include "PassGen.h"

#include <Windows.h>
#include <bcrypt.h>

#include <algorithm>
#include <array>
#include <cmath>
#include <cwctype>
#include <limits>
#include <string>
#include <utility>
#include <vector>

#pragma comment(lib, "Bcrypt.lib")

namespace winforge::core::passgen
{
    namespace
    {
        constexpr std::array<std::wstring_view, 252> Dictionary{
            L"able", L"acid", L"aged", L"also", L"area", L"army", L"away", L"baby", L"back", L"ball", L"band", L"bank",
            L"base", L"bath", L"bear", L"beat", L"been", L"beer", L"bell", L"belt", L"best", L"bird", L"blue", L"boat",
            L"body", L"bone", L"book", L"born", L"both", L"bowl", L"bulk", L"burn", L"bush", L"busy", L"cake", L"call",
            L"calm", L"came", L"camp", L"card", L"care", L"case", L"cash", L"cast", L"cell", L"chat", L"chip", L"city",
            L"clay", L"club", L"coal", L"coat", L"code", L"cold", L"come", L"cook", L"cool", L"cope", L"copy", L"core",
            L"corn", L"cost", L"crew", L"crop", L"dark", L"data", L"date", L"dawn", L"days", L"dead", L"deal", L"dean",
            L"dear", L"debt", L"deep", L"deer", L"desk", L"dial", L"dice", L"diet", L"disk", L"does", L"done", L"door",
            L"dose", L"down", L"draw", L"drew", L"drop", L"drug", L"dual", L"duke", L"dust", L"duty", L"each", L"earn",
            L"ease", L"east", L"easy", L"edge", L"else", L"even", L"ever", L"face", L"fact", L"fade", L"fail", L"fair",
            L"fall", L"farm", L"fast", L"fate", L"fear", L"feed", L"feel", L"feet", L"fell", L"file", L"fill", L"film",
            L"find", L"fine", L"fire", L"firm", L"fish", L"five", L"flag", L"flat", L"flow", L"fold", L"folk", L"food",
            L"foot", L"ford", L"form", L"fort", L"four", L"free", L"from", L"fuel", L"full", L"fund", L"gain", L"game",
            L"gate", L"gave", L"gear", L"gift", L"girl", L"give", L"glad", L"goal", L"goat", L"gold", L"golf", L"gone",
            L"good", L"gray", L"grew", L"grow", L"gulf", L"hair", L"half", L"hall", L"hand", L"hang", L"hard", L"harm",
            L"hate", L"have", L"head", L"heal", L"hear", L"heat", L"held", L"hell", L"help", L"herb", L"here", L"hero",
            L"hide", L"high", L"hill", L"hint", L"hire", L"hold", L"hole", L"holy", L"home", L"hope", L"horn", L"host",
            L"hour", L"huge", L"hunt", L"hurt", L"idea", L"inch", L"into", L"iron", L"item", L"jazz", L"join", L"jump",
            L"jury", L"just", L"keen", L"keep", L"kick", L"kind", L"king", L"knee", L"knew", L"know", L"lace", L"lack",
            L"lake", L"lamp", L"land", L"lane", L"last", L"late", L"lawn", L"lazy", L"lead", L"leaf", L"lean", L"leap",
            L"left", L"lend", L"less", L"life", L"lift", L"like", L"lily", L"limb", L"line", L"link", L"lion", L"list",
            L"live", L"load", L"loan", L"lock", L"logo", L"lone", L"long", L"look", L"loop", L"lord", L"lose", L"loss",
        };

        [[nodiscard]] bool Contains(std::wstring_view value, wchar_t character)
        {
            return value.find(character) != std::wstring_view::npos;
        }

        [[nodiscard]] std::wstring Filter(std::wstring_view value, bool avoid_ambiguous)
        {
            if (!avoid_ambiguous)
            {
                return std::wstring(value);
            }

            std::wstring result;
            result.reserve(value.size());
            for (auto const character : value)
            {
                if (!Contains(Ambiguous, character))
                {
                    result.push_back(character);
                }
            }
            return result;
        }

        [[nodiscard]] std::wstring Excluding(std::wstring_view value, std::wstring_view used)
        {
            std::wstring result;
            result.reserve(value.size());
            for (auto const character : value)
            {
                if (!Contains(used, character))
                {
                    result.push_back(character);
                }
            }
            return result;
        }

        void Shuffle(std::wstring& value, RandomSource& source)
        {
            for (std::size_t index = value.size(); index > 1; --index)
            {
                auto const other = UniformIndex(source, static_cast<std::uint32_t>(index));
                std::swap(value[index - 1], value[other]);
            }
        }

        [[nodiscard]] wchar_t Pick(std::wstring_view value, RandomSource& source)
        {
            if (value.empty())
            {
                throw GenerationError(ErrorCode::EmptyPool, "Character pool is empty.");
            }
            return value[UniformIndex(source, static_cast<std::uint32_t>(value.size()))];
        }

        [[nodiscard]] int ClampBatchCount(int count)
        {
            return std::clamp(count, 1, 100);
        }
    }

    GenerationError::GenerationError(ErrorCode code, char const* message)
        : std::runtime_error(message),
          m_code(code)
    {
    }

    ErrorCode GenerationError::Code() const noexcept
    {
        return m_code;
    }

    std::uint32_t SystemRandomSource::NextUInt32()
    {
        std::uint32_t value{};
        auto const status = BCryptGenRandom(
            nullptr,
            reinterpret_cast<PUCHAR>(&value),
            static_cast<ULONG>(sizeof(value)),
            BCRYPT_USE_SYSTEM_PREFERRED_RNG);
        if (status < 0)
        {
            throw GenerationError(ErrorCode::RandomFailure, "Cryptographic random generator failed.");
        }
        return value;
    }

    std::uint32_t UniformIndex(RandomSource& source, std::uint32_t upper_exclusive)
    {
        if (upper_exclusive == 0)
        {
            throw GenerationError(ErrorCode::EmptyPool, "Character pool is empty.");
        }

        auto constexpr FullUint32Range =
            static_cast<std::uint64_t>((std::numeric_limits<std::uint32_t>::max)()) + 1ull;
        auto const threshold = static_cast<std::uint32_t>(FullUint32Range % upper_exclusive);
        for (;;)
        {
            auto const candidate = source.NextUInt32();
            if (candidate >= threshold)
            {
                return candidate % upper_exclusive;
            }
        }
    }

    std::wstring BuildPool(PasswordOptions const& options)
    {
        std::wstring result;
        if (options.lower) result += Filter(Lowercase, options.avoid_ambiguous);
        if (options.upper) result += Filter(Uppercase, options.avoid_ambiguous);
        if (options.digits) result += Filter(Digits, options.avoid_ambiguous);
        if (options.symbols) result += Filter(Symbols, options.avoid_ambiguous);
        return result;
    }

    std::wstring GeneratePassword(PasswordOptions const& options, RandomSource& source)
    {
        std::vector<std::wstring> required;
        if (options.lower) required.emplace_back(Filter(Lowercase, options.avoid_ambiguous));
        if (options.upper) required.emplace_back(Filter(Uppercase, options.avoid_ambiguous));
        if (options.digits) required.emplace_back(Filter(Digits, options.avoid_ambiguous));
        if (options.symbols) required.emplace_back(Filter(Symbols, options.avoid_ambiguous));

        if (required.empty())
        {
            throw GenerationError(ErrorCode::NoCharacterSets, "No character sets selected.");
        }

        auto const pool = BuildPool(options);
        if (pool.empty())
        {
            throw GenerationError(ErrorCode::EmptyPool, "Character pool is empty.");
        }
        if (options.length < static_cast<int>(required.size()))
        {
            throw GenerationError(ErrorCode::LengthTooShort, "Length is too short to include every selected set.");
        }
        if (options.no_repeats && options.length > static_cast<int>(pool.size()))
        {
            throw GenerationError(ErrorCode::NoRepeatsPoolTooSmall, "No-repeats needs a longer pool than the requested length.");
        }

        std::wstring result;
        result.reserve(static_cast<std::size_t>(options.length));
        std::wstring used;
        used.reserve(static_cast<std::size_t>(options.length));

        for (auto const& set : required)
        {
            auto const candidates = options.no_repeats ? Excluding(set, used) : set;
            if (candidates.empty())
            {
                throw GenerationError(ErrorCode::NoRepeatsPoolTooSmall, "No-repeats needs a longer pool than the requested length.");
            }
            auto const character = Pick(candidates, source);
            result.push_back(character);
            if (options.no_repeats)
            {
                used.push_back(character);
            }
        }

        while (result.size() < static_cast<std::size_t>(options.length))
        {
            auto const candidates = options.no_repeats ? Excluding(pool, used) : pool;
            if (candidates.empty())
            {
                throw GenerationError(ErrorCode::NoRepeatsPoolTooSmall, "No-repeats needs a longer pool than the requested length.");
            }
            auto const character = Pick(candidates, source);
            result.push_back(character);
            if (options.no_repeats)
            {
                used.push_back(character);
            }
        }

        Shuffle(result, source);
        return result;
    }

    std::wstring GeneratePassword(PasswordOptions const& options)
    {
        SystemRandomSource source;
        return GeneratePassword(options, source);
    }

    std::wstring GeneratePassphrase(PassphraseOptions const& options, RandomSource& source)
    {
        if (options.word_count < 1)
        {
            throw GenerationError(ErrorCode::WordCountTooSmall, "Word count must be at least 1.");
        }

        std::wstring result;
        for (int index = 0; index < options.word_count; ++index)
        {
            if (index > 0)
            {
                result += options.separator;
            }

            auto word = std::wstring(Dictionary[UniformIndex(source, static_cast<std::uint32_t>(Dictionary.size()))]);
            if (options.capitalize && !word.empty())
            {
                word.front() = static_cast<wchar_t>(std::towupper(word.front()));
            }
            result += word;
        }

        if (options.append_digit)
        {
            result.push_back(static_cast<wchar_t>(L'0' + UniformIndex(source, 10)));
        }
        return result;
    }

    std::wstring GeneratePassphrase(PassphraseOptions const& options)
    {
        SystemRandomSource source;
        return GeneratePassphrase(options, source);
    }

    std::vector<std::wstring> GeneratePasswordBatch(
        PasswordOptions const& options,
        int count,
        RandomSource& source)
    {
        std::vector<std::wstring> result;
        result.reserve(static_cast<std::size_t>(ClampBatchCount(count)));
        for (int index = 0; index < ClampBatchCount(count); ++index)
        {
            result.push_back(GeneratePassword(options, source));
        }
        return result;
    }

    std::vector<std::wstring> GeneratePassphraseBatch(
        PassphraseOptions const& options,
        int count,
        RandomSource& source)
    {
        std::vector<std::wstring> result;
        result.reserve(static_cast<std::size_t>(ClampBatchCount(count)));
        for (int index = 0; index < ClampBatchCount(count); ++index)
        {
            result.push_back(GeneratePassphrase(options, source));
        }
        return result;
    }

    std::wstring JoinLines(std::vector<std::wstring> const& values)
    {
        std::wstring result;
        for (std::size_t index = 0; index < values.size(); ++index)
        {
            if (index > 0)
            {
                result += L"\r\n";
            }
            result += values[index];
        }
        return result;
    }

    double PasswordEntropyBits(int length, int pool_size)
    {
        if (length <= 0 || pool_size <= 1)
        {
            return 0.0;
        }
        return static_cast<double>(length) * std::log2(static_cast<double>(pool_size));
    }

    double PassphraseEntropyBits(int words, int dictionary_size, bool append_digit)
    {
        if (words <= 0 || dictionary_size <= 1)
        {
            return 0.0;
        }
        auto result = static_cast<double>(words) * std::log2(static_cast<double>(dictionary_size));
        if (append_digit)
        {
            result += std::log2(10.0);
        }
        return result;
    }

    std::size_t DictionarySize() noexcept
    {
        return Dictionary.size();
    }

    std::wstring_view DictionaryWord(std::size_t index)
    {
        if (index >= Dictionary.size())
        {
            throw std::out_of_range("Dictionary word index out of range.");
        }
        return Dictionary[index];
    }
}
