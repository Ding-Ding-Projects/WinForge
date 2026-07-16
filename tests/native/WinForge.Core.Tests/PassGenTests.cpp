#include "PassGenTests.h"

#include "PassGen.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <functional>
#include <iostream>
#include <set>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>
#include <utility>

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

    struct SequenceRandom final : winforge::core::passgen::RandomSource
    {
        explicit SequenceRandom(std::vector<std::uint32_t> sequence)
            : values(std::move(sequence))
        {
        }

        [[nodiscard]] std::uint32_t NextUInt32() override
        {
            if (values.empty())
            {
                throw std::runtime_error("empty deterministic entropy source");
            }
            auto const value = values[index % values.size()];
            ++index;
            return value;
        }

        std::vector<std::uint32_t> values;
        std::size_t index{};
    };

    bool HasAll(std::wstring_view value, std::wstring_view set)
    {
        return std::all_of(value.begin(), value.end(), [set](wchar_t character)
        {
            return set.find(character) != std::wstring_view::npos;
        });
    }

    bool ContainsAny(std::wstring_view value, std::wstring_view set)
    {
        return std::any_of(value.begin(), value.end(), [set](wchar_t character)
        {
            return set.find(character) != std::wstring_view::npos;
        });
    }

    bool ThrowsCode(
        std::function<void()> const& action,
        winforge::core::passgen::ErrorCode expected)
    {
        try
        {
            action();
        }
        catch (winforge::core::passgen::GenerationError const& error)
        {
            return error.Code() == expected;
        }
        catch (...)
        {
            return false;
        }
        return false;
    }

    int LineCount(std::wstring_view value)
    {
        if (value.empty()) return 0;
        auto count = 1;
        for (auto const character : value)
        {
            if (character == L'\n') ++count;
        }
        return count;
    }
}

NativeTestCounts RunPassGenTests()
{
    using namespace winforge::core::passgen;
    Suite suite;

    PasswordOptions defaults;
    auto const defaultPool = BuildPool(defaults);
    suite.Expect(
        defaultPool == L"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+[]{};:,.?/" &&
            defaultPool.size() == 86,
        "Password Generator preserves the managed default character pool");

    defaults.avoid_ambiguous = true;
    auto const filteredPool = BuildPool(defaults);
    suite.Expect(
        filteredPool.size() == 81 && !ContainsAny(filteredPool, Ambiguous) &&
            filteredPool == L"abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%^&*()-_=+[]{};:,.?/",
        "Password Generator strips precisely the managed ambiguous glyphs");

    defaults.avoid_ambiguous = false;
    SequenceRandom defaultEntropy({ 0xFFFFFFFEu, 0xFFFFFFFDu, 0xFFFFFFFCu, 0xFFFFFFFBu });
    auto const generated = GeneratePassword(defaults, defaultEntropy);
    suite.Expect(
        generated.size() == 16 && HasAll(generated, defaultPool) &&
            ContainsAny(generated, Lowercase) && ContainsAny(generated, Uppercase) &&
            ContainsAny(generated, Digits) && ContainsAny(generated, Symbols),
        "Password Generator emits selected classes and only selected-pool characters");

    PasswordOptions noRepeat;
    noRepeat.lower = true;
    noRepeat.upper = false;
    noRepeat.digits = false;
    noRepeat.symbols = false;
    noRepeat.no_repeats = true;
    noRepeat.length = 26;
    SequenceRandom noRepeatEntropy({ 0xFFFFFFFEu });
    auto const noRepeatGenerated = GeneratePassword(noRepeat, noRepeatEntropy);
    std::set<wchar_t> unique(noRepeatGenerated.begin(), noRepeatGenerated.end());
    suite.Expect(
        noRepeatGenerated.size() == 26 && unique.size() == 26 && HasAll(noRepeatGenerated, Lowercase),
        "Password Generator guarantees unique characters in no-repeat mode");

    PasswordOptions noSets;
    noSets.lower = noSets.upper = noSets.digits = noSets.symbols = false;
    SequenceRandom entropy({ 0xFFFFFFFEu });
    suite.Expect(
        ThrowsCode([&] { static_cast<void>(GeneratePassword(noSets, entropy)); }, ErrorCode::NoCharacterSets),
        "Password Generator rejects a request without character sets");

    PasswordOptions tooShort;
    tooShort.length = 3;
    suite.Expect(
        ThrowsCode([&] { static_cast<void>(GeneratePassword(tooShort, entropy)); }, ErrorCode::LengthTooShort),
        "Password Generator rejects a length that cannot cover every selected class");

    PasswordOptions tooManyUnique = noRepeat;
    tooManyUnique.length = 27;
    suite.Expect(
        ThrowsCode([&] { static_cast<void>(GeneratePassword(tooManyUnique, entropy)); }, ErrorCode::NoRepeatsPoolTooSmall),
        "Password Generator rejects an impossible no-repeat request");

    SequenceRandom rejectionEntropy({ 0u, 6u });
    suite.Expect(
        UniformIndex(rejectionEntropy, 10) == 6,
        "Password Generator uses rejection sampling instead of modulo-biased bounded draws");

    PassphraseOptions phrase;
    phrase.word_count = 3;
    phrase.capitalize = true;
    phrase.append_digit = true;
    SequenceRandom phraseEntropy({ 252u, 253u, 503u, 17u });
    suite.Expect(
        GeneratePassphrase(phrase, phraseEntropy) == L"Able-Acid-Loss7",
        "Password Generator preserves passphrase dictionary ordering capitalization separator and digit behavior");

    phrase.word_count = 0;
    suite.Expect(
        ThrowsCode([&] { static_cast<void>(GeneratePassphrase(phrase, entropy)); }, ErrorCode::WordCountTooSmall),
        "Password Generator rejects a zero-word passphrase request");

    suite.Expect(
        DictionarySize() == 252 && DictionaryWord(0) == L"able" && DictionaryWord(1) == L"acid" &&
            DictionaryWord(DictionarySize() - 1) == L"loss",
        "Password Generator ships the full managed 252-word dictionary in native code");

    SequenceRandom batchEntropy({ 0xFFFFFFFEu });
    auto const passwordBatch = GeneratePasswordBatch(defaults, 3, batchEntropy);
    auto const passwordLines = JoinLines(passwordBatch);
    suite.Expect(
        passwordBatch.size() == 3 && LineCount(passwordLines) == 3 && passwordLines.find(L"\r\n") != std::wstring::npos,
        "Password Generator produces Windows-newline-separated password batches");

    PassphraseOptions batchPhrase;
    SequenceRandom batchPhraseEntropy({ 0xFFFFFFFEu });
    auto const phraseBatch = GeneratePassphraseBatch(batchPhrase, 101, batchPhraseEntropy);
    suite.Expect(
        phraseBatch.size() == 100,
        "Password Generator clamps batch requests to the managed one-hundred-item limit");

    auto const closeEnough = [](double actual, double expected)
    {
        return std::abs(actual - expected) < 0.000000001;
    };
    suite.Expect(
        closeEnough(PasswordEntropyBits(16, 86), 102.820236075234) &&
            closeEnough(PasswordEntropyBits(16, 81), 101.437600046154) &&
            closeEnough(PassphraseEntropyBits(4, 252, false), 31.909119694) &&
            closeEnough(PassphraseEntropyBits(4, 252, true), 35.231047788887) &&
            PasswordEntropyBits(0, 86) == 0.0 && PassphraseEntropyBits(0, 252, false) == 0.0,
        "Password Generator preserves password and passphrase entropy formulas");

    return suite.counts;
}
