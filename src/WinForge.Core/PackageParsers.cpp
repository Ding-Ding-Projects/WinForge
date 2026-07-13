#include "PackageParsers.h"

#include <algorithm>
#include <cctype>
#include <cstdint>
#include <initializer_list>
#include <limits>
#include <optional>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace
{
    using winforge::core::packages::PackageOutputKind;
    using winforge::core::packages::PackageRecord;

    constexpr std::size_t MaxInputBytes = 16U * 1024U * 1024U;
    constexpr std::size_t MaxRecords = 10'000U;
    constexpr std::size_t MaxJsonNodes = 250'000U;
    constexpr std::size_t MaxJsonDepth = 64U;

    bool IsSpace(char character) noexcept
    {
        auto const value = static_cast<unsigned char>(character);
        return value == ' ' || value == '\t' || value == '\r' || value == '\n'
            || value == '\f' || value == '\v';
    }

    std::string_view TrimView(std::string_view value) noexcept
    {
        while (!value.empty() && IsSpace(value.front()))
        {
            value.remove_prefix(1);
        }
        while (!value.empty() && IsSpace(value.back()))
        {
            value.remove_suffix(1);
        }
        return value;
    }

    std::string TrimCopy(std::string_view value)
    {
        value = TrimView(value);
        return std::string(value);
    }

    char LowerAscii(char character) noexcept
    {
        auto const value = static_cast<unsigned char>(character);
        return value >= 'A' && value <= 'Z'
            ? static_cast<char>(value + ('a' - 'A'))
            : character;
    }

    bool EqualsInsensitive(std::string_view left, std::string_view right) noexcept
    {
        if (left.size() != right.size())
        {
            return false;
        }
        for (std::size_t index = 0; index < left.size(); ++index)
        {
            if (LowerAscii(left[index]) != LowerAscii(right[index]))
            {
                return false;
            }
        }
        return true;
    }

    bool StartsWithInsensitive(std::string_view value, std::string_view prefix) noexcept
    {
        return value.size() >= prefix.size()
            && EqualsInsensitive(value.substr(0, prefix.size()), prefix);
    }

    bool ContainsInsensitive(std::string_view value, std::string_view needle) noexcept
    {
        if (needle.empty())
        {
            return true;
        }
        if (needle.size() > value.size())
        {
            return false;
        }
        for (std::size_t start = 0; start + needle.size() <= value.size(); ++start)
        {
            if (EqualsInsensitive(value.substr(start, needle.size()), needle))
            {
                return true;
            }
        }
        return false;
    }

    int CompareInsensitive(std::string_view left, std::string_view right) noexcept
    {
        auto const count = std::min(left.size(), right.size());
        for (std::size_t index = 0; index < count; ++index)
        {
            auto const leftCharacter = LowerAscii(left[index]);
            auto const rightCharacter = LowerAscii(right[index]);
            if (leftCharacter < rightCharacter) return -1;
            if (leftCharacter > rightCharacter) return 1;
        }
        if (left.size() < right.size()) return -1;
        if (left.size() > right.size()) return 1;
        return 0;
    }

    bool ContainsAsciiWhitespace(std::string_view value) noexcept
    {
        return std::any_of(value.begin(), value.end(), [](char character)
        {
            return IsSpace(character);
        });
    }

    bool ContainsUnsafeControl(std::string_view value) noexcept
    {
        return std::any_of(value.begin(), value.end(), [](char character)
        {
            auto const byte = static_cast<unsigned char>(character);
            return byte == 0 || byte == '\r' || byte == '\n';
        });
    }

    bool HasAsciiLetterOrDigit(std::string_view value) noexcept
    {
        return std::any_of(value.begin(), value.end(), [](char character)
        {
            auto const byte = static_cast<unsigned char>(character);
            return (byte >= '0' && byte <= '9')
                || (byte >= 'A' && byte <= 'Z')
                || (byte >= 'a' && byte <= 'z');
        });
    }

    std::string StripAnsi(std::string_view value)
    {
        std::string result;
        result.reserve(value.size());
        for (std::size_t index = 0; index < value.size(); ++index)
        {
            auto const byte = static_cast<unsigned char>(value[index]);
            if (byte != 0x1bU || index + 1 >= value.size() || value[index + 1] != '[')
            {
                result.push_back(value[index]);
                continue;
            }

            index += 2;
            while (index < value.size())
            {
                auto const current = static_cast<unsigned char>(value[index]);
                if (current >= 0x40U && current <= 0x7eU)
                {
                    break;
                }
                ++index;
            }
        }
        return result;
    }

    template <typename Callback>
    void ForEachLine(std::string_view input, Callback&& callback)
    {
        std::size_t start = 0;
        while (start <= input.size())
        {
            auto const end = input.find_first_of("\r\n", start);
            callback(input.substr(start, end == std::string_view::npos ? input.size() - start : end - start));
            if (end == std::string_view::npos)
            {
                break;
            }
            start = end + 1;
            if (input[end] == '\r' && start < input.size() && input[start] == '\n')
            {
                ++start;
            }
        }
    }

    std::vector<std::string_view> SplitWhitespace(std::string_view value)
    {
        std::vector<std::string_view> parts;
        std::size_t index = 0;
        while (index < value.size())
        {
            while (index < value.size() && IsSpace(value[index]))
            {
                ++index;
            }
            auto const start = index;
            while (index < value.size() && !IsSpace(value[index]))
            {
                ++index;
            }
            if (start < index)
            {
                parts.emplace_back(value.substr(start, index - start));
            }
        }
        return parts;
    }

    std::vector<std::string_view> SplitWideColumns(std::string_view value)
    {
        std::vector<std::string_view> columns;
        std::size_t start = 0;
        std::size_t index = 0;
        while (index < value.size())
        {
            if (value[index] != ' ' && value[index] != '\t')
            {
                ++index;
                continue;
            }

            auto const separatorStart = index;
            while (index < value.size() && (value[index] == ' ' || value[index] == '\t'))
            {
                ++index;
            }
            if (index - separatorStart < 2)
            {
                continue;
            }

            auto const column = TrimView(value.substr(start, separatorStart - start));
            if (!column.empty())
            {
                columns.push_back(column);
            }
            start = index;
        }

        auto const column = TrimView(value.substr(start));
        if (!column.empty())
        {
            columns.push_back(column);
        }
        return columns;
    }

    bool IsRuleLine(std::string_view value) noexcept
    {
        value = TrimView(value);
        if (value.empty())
        {
            return false;
        }

        bool sawRule = false;
        for (std::size_t index = 0; index < value.size();)
        {
            auto const byte = static_cast<unsigned char>(value[index]);
            if (byte == '-' || byte == '=' || byte == '+' || byte == '|' || IsSpace(value[index]))
            {
                sawRule = sawRule || byte == '-' || byte == '=';
                ++index;
                continue;
            }

            // UTF-8 box-drawing characters are three-byte sequences beginning E2 94/95.
            if (byte == 0xe2U && index + 2 < value.size()
                && (static_cast<unsigned char>(value[index + 1]) == 0x94U
                    || static_cast<unsigned char>(value[index + 1]) == 0x95U))
            {
                sawRule = true;
                index += 3;
                continue;
            }
            return false;
        }
        return sawRule;
    }

    std::string NormalizeVerticalBars(std::string_view value)
    {
        std::string result;
        result.reserve(value.size());
        for (std::size_t index = 0; index < value.size();)
        {
            if (index + 2 < value.size()
                && static_cast<unsigned char>(value[index]) == 0xe2U
                && static_cast<unsigned char>(value[index + 1]) == 0x94U
                && static_cast<unsigned char>(value[index + 2]) == 0x82U)
            {
                result.push_back('|');
                index += 3;
            }
            else
            {
                result.push_back(value[index]);
                ++index;
            }
        }
        return result;
    }

    std::vector<std::string> SplitPipeCells(std::string_view value)
    {
        auto normalized = NormalizeVerticalBars(value);
        std::vector<std::string> cells;
        std::size_t start = 0;
        while (start <= normalized.size())
        {
            auto const end = normalized.find('|', start);
            auto cell = TrimCopy(std::string_view(normalized).substr(
                start,
                end == std::string::npos ? normalized.size() - start : end - start));
            if (!cell.empty())
            {
                cells.push_back(std::move(cell));
            }
            if (end == std::string::npos)
            {
                break;
            }
            start = end + 1;
        }
        return cells;
    }

    void AddRecord(std::vector<PackageRecord>& records, PackageRecord record)
    {
        if (records.size() >= MaxRecords)
        {
            return;
        }

        record.name = TrimCopy(record.name);
        record.id = TrimCopy(record.id);
        record.version = TrimCopy(record.version);
        record.availableVersion = TrimCopy(record.availableVersion);
        record.source = TrimCopy(record.source);
        record.managerKey = TrimCopy(record.managerKey);
        if (record.name.empty())
        {
            record.name = record.id;
        }
        if (!record.IsValid() || ContainsUnsafeControl(record.id) || ContainsUnsafeControl(record.managerKey))
        {
            return;
        }
        records.push_back(std::move(record));
    }

    enum class JsonKind
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object,
    };

    struct JsonValue
    {
        JsonKind kind{ JsonKind::Null };
        std::string scalar;
        std::vector<JsonValue> array;
        std::vector<std::pair<std::string, JsonValue>> object;

        [[nodiscard]] JsonValue const* Find(std::string_view key) const noexcept
        {
            if (kind != JsonKind::Object)
            {
                return nullptr;
            }
            for (auto entry = object.rbegin(); entry != object.rend(); ++entry)
            {
                if (entry->first == key)
                {
                    return &entry->second;
                }
            }
            return nullptr;
        }
    };

    class JsonParser
    {
    public:
        explicit JsonParser(std::string_view input) noexcept : input_(input)
        {
        }

        bool Parse(JsonValue& result)
        {
            nodes_ = 0;
            position_ = 0;
            SkipWhitespace();
            return ParseValue(result, 0);
        }

    private:
        void SkipWhitespace() noexcept
        {
            while (position_ < input_.size() && IsSpace(input_[position_]))
            {
                ++position_;
            }
        }

        bool ParseValue(JsonValue& result, std::size_t depth)
        {
            if (depth > MaxJsonDepth || ++nodes_ > MaxJsonNodes)
            {
                return false;
            }
            SkipWhitespace();
            if (position_ >= input_.size())
            {
                return false;
            }

            switch (input_[position_])
            {
            case '{':
                return ParseObject(result, depth);
            case '[':
                return ParseArray(result, depth);
            case '"':
                result.kind = JsonKind::String;
                return ParseString(result.scalar);
            case 't':
                result.kind = JsonKind::Boolean;
                result.scalar = "true";
                return ParseLiteral("true");
            case 'f':
                result.kind = JsonKind::Boolean;
                result.scalar = "false";
                return ParseLiteral("false");
            case 'n':
                result.kind = JsonKind::Null;
                result.scalar.clear();
                return ParseLiteral("null");
            default:
                result.kind = JsonKind::Number;
                return ParseNumber(result.scalar);
            }
        }

        bool ParseLiteral(std::string_view literal) noexcept
        {
            if (input_.substr(position_, literal.size()) != literal)
            {
                return false;
            }
            position_ += literal.size();
            return true;
        }

        static int HexValue(char character) noexcept
        {
            if (character >= '0' && character <= '9')
            {
                return character - '0';
            }
            character = LowerAscii(character);
            return character >= 'a' && character <= 'f' ? character - 'a' + 10 : -1;
        }

        bool ParseHex4(std::uint32_t& value) noexcept
        {
            if (position_ + 4 > input_.size())
            {
                return false;
            }
            value = 0;
            for (int count = 0; count < 4; ++count)
            {
                auto const digit = HexValue(input_[position_++]);
                if (digit < 0)
                {
                    return false;
                }
                value = value * 16U + static_cast<std::uint32_t>(digit);
            }
            return true;
        }

        static bool AppendUtf8(std::string& output, std::uint32_t value)
        {
            if (value <= 0x7fU)
            {
                output.push_back(static_cast<char>(value));
            }
            else if (value <= 0x7ffU)
            {
                output.push_back(static_cast<char>(0xc0U | (value >> 6U)));
                output.push_back(static_cast<char>(0x80U | (value & 0x3fU)));
            }
            else if (value <= 0xffffU && (value < 0xd800U || value > 0xdfffU))
            {
                output.push_back(static_cast<char>(0xe0U | (value >> 12U)));
                output.push_back(static_cast<char>(0x80U | ((value >> 6U) & 0x3fU)));
                output.push_back(static_cast<char>(0x80U | (value & 0x3fU)));
            }
            else if (value <= 0x10ffffU)
            {
                output.push_back(static_cast<char>(0xf0U | (value >> 18U)));
                output.push_back(static_cast<char>(0x80U | ((value >> 12U) & 0x3fU)));
                output.push_back(static_cast<char>(0x80U | ((value >> 6U) & 0x3fU)));
                output.push_back(static_cast<char>(0x80U | (value & 0x3fU)));
            }
            else
            {
                return false;
            }
            return true;
        }

        bool ParseString(std::string& result)
        {
            if (position_ >= input_.size() || input_[position_] != '"')
            {
                return false;
            }
            ++position_;
            result.clear();
            while (position_ < input_.size())
            {
                auto const character = static_cast<unsigned char>(input_[position_++]);
                if (character == '"')
                {
                    return true;
                }
                if (character < 0x20U)
                {
                    return false;
                }
                if (character != '\\')
                {
                    result.push_back(static_cast<char>(character));
                    continue;
                }
                if (position_ >= input_.size())
                {
                    return false;
                }

                switch (input_[position_++])
                {
                case '"': result.push_back('"'); break;
                case '\\': result.push_back('\\'); break;
                case '/': result.push_back('/'); break;
                case 'b': result.push_back('\b'); break;
                case 'f': result.push_back('\f'); break;
                case 'n': result.push_back('\n'); break;
                case 'r': result.push_back('\r'); break;
                case 't': result.push_back('\t'); break;
                case 'u':
                {
                    std::uint32_t codePoint = 0;
                    if (!ParseHex4(codePoint))
                    {
                        return false;
                    }
                    if (codePoint >= 0xd800U && codePoint <= 0xdbffU)
                    {
                        if (position_ + 2 > input_.size() || input_[position_] != '\\'
                            || input_[position_ + 1] != 'u')
                        {
                            return false;
                        }
                        position_ += 2;
                        std::uint32_t low = 0;
                        if (!ParseHex4(low) || low < 0xdc00U || low > 0xdfffU)
                        {
                            return false;
                        }
                        codePoint = 0x10000U + ((codePoint - 0xd800U) << 10U) + (low - 0xdc00U);
                    }
                    if (!AppendUtf8(result, codePoint))
                    {
                        return false;
                    }
                    break;
                }
                default:
                    return false;
                }
            }
            return false;
        }

        bool ParseNumber(std::string& result)
        {
            auto const start = position_;
            if (position_ < input_.size() && input_[position_] == '-')
            {
                ++position_;
            }
            if (position_ >= input_.size())
            {
                return false;
            }
            if (input_[position_] == '0')
            {
                ++position_;
            }
            else
            {
                auto const integerStart = position_;
                while (position_ < input_.size() && input_[position_] >= '0' && input_[position_] <= '9')
                {
                    ++position_;
                }
                if (integerStart == position_)
                {
                    return false;
                }
            }
            if (position_ < input_.size() && input_[position_] == '.')
            {
                ++position_;
                auto const fractionStart = position_;
                while (position_ < input_.size() && input_[position_] >= '0' && input_[position_] <= '9')
                {
                    ++position_;
                }
                if (fractionStart == position_)
                {
                    return false;
                }
            }
            if (position_ < input_.size() && (input_[position_] == 'e' || input_[position_] == 'E'))
            {
                ++position_;
                if (position_ < input_.size() && (input_[position_] == '+' || input_[position_] == '-'))
                {
                    ++position_;
                }
                auto const exponentStart = position_;
                while (position_ < input_.size() && input_[position_] >= '0' && input_[position_] <= '9')
                {
                    ++position_;
                }
                if (exponentStart == position_)
                {
                    return false;
                }
            }
            result.assign(input_.substr(start, position_ - start));
            return true;
        }

        bool ParseArray(JsonValue& result, std::size_t depth)
        {
            result.kind = JsonKind::Array;
            result.array.clear();
            ++position_;
            SkipWhitespace();
            if (position_ < input_.size() && input_[position_] == ']')
            {
                ++position_;
                return true;
            }
            while (position_ < input_.size())
            {
                JsonValue item;
                if (!ParseValue(item, depth + 1))
                {
                    return false;
                }
                result.array.push_back(std::move(item));
                SkipWhitespace();
                if (position_ >= input_.size())
                {
                    return false;
                }
                if (input_[position_] == ']')
                {
                    ++position_;
                    return true;
                }
                if (input_[position_++] != ',')
                {
                    return false;
                }
            }
            return false;
        }

        bool ParseObject(JsonValue& result, std::size_t depth)
        {
            result.kind = JsonKind::Object;
            result.object.clear();
            ++position_;
            SkipWhitespace();
            if (position_ < input_.size() && input_[position_] == '}')
            {
                ++position_;
                return true;
            }
            while (position_ < input_.size())
            {
                SkipWhitespace();
                std::string name;
                if (!ParseString(name))
                {
                    return false;
                }
                SkipWhitespace();
                if (position_ >= input_.size() || input_[position_++] != ':')
                {
                    return false;
                }
                JsonValue value;
                if (!ParseValue(value, depth + 1))
                {
                    return false;
                }
                result.object.emplace_back(std::move(name), std::move(value));
                SkipWhitespace();
                if (position_ >= input_.size())
                {
                    return false;
                }
                if (input_[position_] == '}')
                {
                    ++position_;
                    return true;
                }
                if (input_[position_++] != ',')
                {
                    return false;
                }
            }
            return false;
        }

        std::string_view input_;
        std::size_t position_{ 0 };
        std::size_t nodes_{ 0 };
    };

    std::optional<JsonValue> FindJson(
        std::string_view input,
        bool allowArray,
        bool allowObject)
    {
        if (input.size() > MaxInputBytes)
        {
            return std::nullopt;
        }

        std::size_t position = 0;
        for (std::size_t attempts = 0; attempts < 128 && position < input.size(); ++attempts)
        {
            auto const nextArray = allowArray ? input.find('[', position) : std::string_view::npos;
            auto const nextObject = allowObject ? input.find('{', position) : std::string_view::npos;
            auto const candidate = std::min(nextArray, nextObject);
            if (candidate == std::string_view::npos)
            {
                break;
            }

            JsonValue result;
            JsonParser parser(input.substr(candidate));
            if (parser.Parse(result)
                && ((allowArray && result.kind == JsonKind::Array)
                    || (allowObject && result.kind == JsonKind::Object)))
            {
                return result;
            }
            position = candidate + 1;
        }
        return std::nullopt;
    }

    JsonValue const* FindAny(JsonValue const& object, std::initializer_list<std::string_view> keys) noexcept
    {
        for (auto const key : keys)
        {
            if (auto const value = object.Find(key))
            {
                return value;
            }
        }
        return nullptr;
    }

    std::string Scalar(JsonValue const& object, std::initializer_list<std::string_view> keys)
    {
        auto const value = FindAny(object, keys);
        if (!value)
        {
            return {};
        }
        switch (value->kind)
        {
        case JsonKind::String:
        case JsonKind::Number:
        case JsonKind::Boolean:
            return value->scalar;
        default:
            return {};
        }
    }

    std::size_t Utf8ByteOffsetAtColumn(std::string_view value, std::size_t column) noexcept
    {
        std::size_t index = 0;
        std::size_t currentColumn = 0;
        while (index < value.size() && currentColumn < column)
        {
            auto const byte = static_cast<unsigned char>(value[index]);
            std::size_t length = 1;
            if ((byte & 0xe0U) == 0xc0U)
            {
                length = 2;
            }
            else if ((byte & 0xf0U) == 0xe0U)
            {
                length = 3;
            }
            else if ((byte & 0xf8U) == 0xf0U)
            {
                length = 4;
            }
            if (index + length > value.size())
            {
                length = 1;
            }
            index += length;
            ++currentColumn;
        }
        return index;
    }

    std::size_t Utf8ColumnCount(std::string_view value) noexcept
    {
        std::size_t count = 0;
        std::size_t index = 0;
        while (index < value.size())
        {
            auto const byte = static_cast<unsigned char>(value[index]);
            std::size_t length = 1;
            if ((byte & 0xe0U) == 0xc0U) length = 2;
            else if ((byte & 0xf0U) == 0xe0U) length = 3;
            else if ((byte & 0xf8U) == 0xf0U) length = 4;
            if (index + length > value.size()) length = 1;
            index += length;
            ++count;
        }
        return count;
    }

    std::string CutColumns(std::string_view line, std::size_t first, std::size_t last)
    {
        auto const begin = Utf8ByteOffsetAtColumn(line, first);
        auto const end = Utf8ByteOffsetAtColumn(line, last);
        if (begin >= line.size() || end <= begin)
        {
            return {};
        }
        return TrimCopy(line.substr(begin, end - begin));
    }

    std::size_t MinColumn(
        std::size_t fallback,
        std::initializer_list<std::size_t> candidates) noexcept
    {
        auto result = fallback;
        for (auto const candidate : candidates)
        {
            if (candidate != std::string_view::npos && candidate > 0)
            {
                result = std::min(result, candidate);
            }
        }
        return result;
    }

    std::optional<std::string> QuotedBucket(std::string_view line)
    {
        if (line.find("bucket") == std::string_view::npos
            && line.find("Bucket") == std::string_view::npos)
        {
            return std::nullopt;
        }
        auto const quote = line.find_first_of("'\"");
        if (quote == std::string_view::npos)
        {
            return std::nullopt;
        }
        auto const end = line.find(line[quote], quote + 1);
        if (end == std::string_view::npos || end == quote + 1)
        {
            return std::nullopt;
        }
        return std::string(line.substr(quote + 1, end - quote - 1));
    }

    bool IsNoiseId(std::string_view id) noexcept
    {
        return id.empty()
            || EqualsInsensitive(id, "name")
            || EqualsInsensitive(id, "package")
            || EqualsInsensitive(id, "packages")
            || EqualsInsensitive(id, "chocolatey")
            || EqualsInsensitive(id, "validation")
            || EqualsInsensitive(id, "output")
            || EqualsInsensitive(id, "warning")
            || EqualsInsensitive(id, "warn")
            || EqualsInsensitive(id, "installed")
            || EqualsInsensitive(id, "updates");
    }

    std::vector<PackageRecord> ParseWingetTableImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        if (output.size() > MaxInputBytes)
        {
            return records;
        }

        std::vector<std::string> lines;
        ForEachLine(output, [&](std::string_view raw)
        {
            lines.push_back(StripAnsi(raw));
        });

        std::size_t headerIndex = std::string::npos;
        std::size_t idColumn = std::string::npos;
        std::size_t versionColumn = std::string::npos;
        std::size_t availableColumn = std::string::npos;
        std::size_t matchColumn = std::string::npos;
        std::size_t sourceColumn = std::string::npos;
        for (std::size_t index = 0; index < lines.size(); ++index)
        {
            auto const& line = lines[index];
            auto const id = line.find("Id");
            auto const version = line.find("Version");
            if (id == std::string::npos || version == std::string::npos || id == 0 || version <= id)
            {
                continue;
            }
            headerIndex = index;
            idColumn = id;
            versionColumn = version;
            availableColumn = line.find("Available", version + 1);
            matchColumn = line.find("Match", version + 1);
            sourceColumn = line.find("Source", version + 1);
            break;
        }
        if (headerIndex == std::string::npos)
        {
            return records;
        }

        bool sawRule = false;
        for (std::size_t index = headerIndex + 1; index < lines.size(); ++index)
        {
            auto const line = std::string_view(lines[index]);
            auto const trimmed = TrimView(line);
            if (trimmed.empty())
            {
                continue;
            }
            if (IsRuleLine(trimmed))
            {
                sawRule = true;
                continue;
            }
            if (!sawRule)
            {
                continue;
            }

            auto const lineColumns = Utf8ColumnCount(line);
            auto const versionEnd = MinColumn(
                lineColumns,
                { availableColumn, matchColumn, sourceColumn });
            auto name = CutColumns(line, 0, idColumn);
            auto id = CutColumns(line, idColumn, versionColumn);
            auto version = CutColumns(line, versionColumn, versionEnd);
            auto available = availableColumn != std::string::npos
                ? CutColumns(
                    line,
                    availableColumn,
                    MinColumn(lineColumns, { matchColumn, sourceColumn }))
                : std::string{};
            auto source = sourceColumn != std::string::npos && sourceColumn < lineColumns
                ? CutColumns(line, sourceColumn, lineColumns)
                : std::string{};
            if (id.empty() || ContainsAsciiWhitespace(id))
            {
                continue;
            }
            AddRecord(records, {
                std::move(name), std::move(id), std::move(version), std::move(available),
                std::move(source), "winget" });
        }
        return records;
    }

    std::vector<PackageRecord> ParseScoopSearchImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        std::string bucket;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto line = TrimView(raw);
            if (auto parsedBucket = QuotedBucket(line))
            {
                bucket = std::move(*parsedBucket);
                return;
            }
            if (line.empty() || IsRuleLine(line) || StartsWithInsensitive(line, "name")
                || StartsWithInsensitive(line, "results") || StartsWithInsensitive(line, "warn")
                || StartsWithInsensitive(line, "no matches") || StartsWithInsensitive(line, "scoop"))
            {
                return;
            }
            auto const parts = SplitWhitespace(line);
            if (parts.size() < 2 || parts[0].find(':') != std::string_view::npos
                || parts[0].find('\'') != std::string_view::npos)
            {
                return;
            }
            auto version = TrimCopy(parts[1]);
            if (version.size() >= 2 && version.front() == '(' && version.back() == ')')
            {
                version = version.substr(1, version.size() - 2);
            }
            auto source = parts.size() > 2 ? TrimCopy(parts[2]) : bucket;
            AddRecord(records, {
                std::string(parts[0]), std::string(parts[0]), std::move(version), {},
                std::move(source), "scoop" });
        });
        return records;
    }

    std::vector<PackageRecord> ParseScoopInstalledJsonImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        auto root = FindJson(output, true, true);
        if (!root)
        {
            return records;
        }

        JsonValue const* apps = &*root;
        if (root->kind == JsonKind::Object)
        {
            apps = FindAny(*root, { "apps", "Apps" });
        }
        if (!apps || apps->kind != JsonKind::Array)
        {
            return records;
        }
        for (auto const& item : apps->array)
        {
            if (item.kind != JsonKind::Object)
            {
                continue;
            }
            auto name = Scalar(item, { "Name", "name" });
            if (name.empty())
            {
                continue;
            }
            AddRecord(records, {
                name,
                std::move(name),
                Scalar(item, { "Version", "version" }),
                {},
                Scalar(item, { "Source", "source", "Bucket", "bucket" }),
                "scoop" });
        }
        return records;
    }

    std::vector<PackageRecord> ParseScoopListImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto const line = TrimView(raw);
            if (line.empty() || IsRuleLine(line) || StartsWithInsensitive(line, "name")
                || StartsWithInsensitive(line, "installed") || StartsWithInsensitive(line, "warn")
                || StartsWithInsensitive(line, "no packages"))
            {
                return;
            }
            auto const parts = SplitWhitespace(line);
            if (parts.size() < 2 || IsNoiseId(parts[0]))
            {
                return;
            }
            AddRecord(records, {
                std::string(parts[0]), std::string(parts[0]), std::string(parts[1]), {},
                parts.size() > 2 ? std::string(parts[2]) : std::string{}, "scoop" });
        });
        return records;
    }

    std::vector<PackageRecord> ParseScoopStatusImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto const line = TrimView(raw);
            if (line.empty() || IsRuleLine(line) || StartsWithInsensitive(line, "name")
                || StartsWithInsensitive(line, "scoop") || StartsWithInsensitive(line, "everything")
                || StartsWithInsensitive(line, "warn") || StartsWithInsensitive(line, "updates"))
            {
                return;
            }
            auto const parts = SplitWhitespace(line);
            if (parts.size() < 3 || IsNoiseId(parts[0]) || parts[1] == parts[2])
            {
                return;
            }
            AddRecord(records, {
                std::string(parts[0]), std::string(parts[0]), std::string(parts[1]),
                std::string(parts[2]), parts.size() > 3 ? std::string(parts[3]) : std::string{},
                "scoop" });
        });
        return records;
    }

    std::vector<PackageRecord> ParseChocolateyImpl(std::string_view output, PackageOutputKind kind)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto clean = StripAnsi(raw);
            auto const line = TrimView(clean);
            if (line.empty() || IsRuleLine(line) || StartsWithInsensitive(line, "chocolatey")
                || StartsWithInsensitive(line, "validation failures")
                || StartsWithInsensitive(line, "outdated packages"))
            {
                return;
            }

            std::vector<std::string> cells;
            if (line.find('|') != std::string_view::npos)
            {
                cells = SplitPipeCells(line);
            }
            else
            {
                for (auto const part : SplitWhitespace(line))
                {
                    cells.emplace_back(part);
                }
            }
            if (cells.empty() || IsNoiseId(cells[0]))
            {
                return;
            }

            if (kind == PackageOutputKind::Updates)
            {
                if (cells.size() < 3 || cells[1].empty() || cells[2].empty() || cells[1] == cells[2])
                {
                    return;
                }
                AddRecord(records, {
                    cells[0], cells[0], cells[1], cells[2], {}, "choco" });
                return;
            }

            if (cells.size() < 2 || cells[1].empty() || EqualsInsensitive(cells[1], "packages"))
            {
                return;
            }
            AddRecord(records, { cells[0], cells[0], cells[1], {}, {}, "choco" });
        });
        return records;
    }

    std::vector<PackageRecord> ParsePipJsonImpl(std::string_view output, PackageOutputKind kind)
    {
        std::vector<PackageRecord> records;
        auto root = FindJson(output, true, true);
        if (!root)
        {
            return records;
        }

        JsonValue const* values = &*root;
        if (kind == PackageOutputKind::Search && root->kind == JsonKind::Object)
        {
            values = FindAny(*root, { "projects", "Projects" });
        }
        if (!values || values->kind != JsonKind::Array)
        {
            return records;
        }

        for (auto const& item : values->array)
        {
            if (item.kind != JsonKind::Object)
            {
                continue;
            }
            auto name = Scalar(item, { "name", "Name" });
            if (name.empty())
            {
                continue;
            }
            AddRecord(records, {
                name,
                std::move(name),
                Scalar(item, { "version", "Version" }),
                kind == PackageOutputKind::Updates
                    ? Scalar(item, { "latest_version", "latestVersion", "LatestVersion" })
                    : std::string{},
                kind == PackageOutputKind::Search ? "pypi.org" : std::string{},
                "pip" });
        }
        return records;
    }

    std::vector<PackageRecord> ParsePyPiSearchJsonImpl(
        std::string_view output,
        std::string_view query)
    {
        std::vector<PackageRecord> records;
        query = TrimView(query);
        if (query.empty())
        {
            return records;
        }

        auto root = FindJson(output, false, true);
        if (!root || root->kind != JsonKind::Object)
        {
            return records;
        }
        auto const projects = FindAny(*root, { "projects", "Projects" });
        if (!projects || projects->kind != JsonKind::Array)
        {
            return records;
        }

        std::vector<std::string> names;
        for (auto const& project : projects->array)
        {
            if (project.kind != JsonKind::Object)
            {
                continue;
            }
            auto name = Scalar(project, { "name", "Name" });
            if (name.empty() || !ContainsInsensitive(name, query))
            {
                continue;
            }
            auto const duplicate = std::any_of(names.begin(), names.end(), [&](std::string const& existing)
            {
                return EqualsInsensitive(existing, name);
            });
            if (!duplicate)
            {
                names.push_back(std::move(name));
            }
        }

        std::stable_sort(names.begin(), names.end(), [&](std::string const& left, std::string const& right)
        {
            auto const leftStarts = StartsWithInsensitive(left, query);
            auto const rightStarts = StartsWithInsensitive(right, query);
            if (leftStarts != rightStarts) return leftStarts;
            if (left.size() != right.size()) return left.size() < right.size();
            return CompareInsensitive(left, right) < 0;
        });

        for (auto& name : names)
        {
            if (records.size() == 20)
            {
                break;
            }
            auto id = name;
            AddRecord(records, {
                std::move(name), std::move(id), {}, {}, "pypi.org", "pip" });
        }
        return records;
    }

    void AddNpmSearchRecord(std::vector<PackageRecord>& records, JsonValue const& item, std::string managerKey)
    {
        if (item.kind != JsonKind::Object)
        {
            return;
        }
        auto name = Scalar(item, { "name", "Name" });
        auto version = Scalar(item, { "version", "Version" });
        if (name.empty() || version.empty())
        {
            return;
        }
        AddRecord(records, {
            name, std::move(name), std::move(version), {},
            managerKey == "bun" ? "npmjs.org" : std::string{}, std::move(managerKey) });
    }

    std::vector<PackageRecord> ParseNpmSearchImpl(std::string_view output, std::string managerKey)
    {
        std::vector<PackageRecord> records;
        auto array = FindJson(output, true, false);
        if (array)
        {
            for (auto const& item : array->array)
            {
                AddNpmSearchRecord(records, item, managerKey);
            }
            return records;
        }

        ForEachLine(output, [&](std::string_view raw)
        {
            auto const line = TrimView(raw);
            if (line.empty() || line.front() != '{')
            {
                return;
            }
            JsonValue item;
            JsonParser parser(line);
            if (parser.Parse(item))
            {
                AddNpmSearchRecord(records, item, managerKey);
            }
        });
        return records;
    }

    std::vector<PackageRecord> ParseNpmJsonImpl(std::string_view output, PackageOutputKind kind)
    {
        if (kind == PackageOutputKind::Search)
        {
            return ParseNpmSearchImpl(output, "npm");
        }

        std::vector<PackageRecord> records;
        auto root = FindJson(output, false, true);
        if (!root || root->kind != JsonKind::Object)
        {
            return records;
        }
        JsonValue const* packages = &*root;
        if (kind == PackageOutputKind::Installed)
        {
            packages = FindAny(*root, { "dependencies", "Dependencies" });
        }
        if (!packages || packages->kind != JsonKind::Object)
        {
            return records;
        }

        for (auto const& [name, item] : packages->object)
        {
            if (name.empty() || item.kind != JsonKind::Object)
            {
                continue;
            }
            if (kind == PackageOutputKind::Installed)
            {
                auto version = Scalar(item, { "version", "Version" });
                if (version.empty())
                {
                    continue;
                }
                AddRecord(records, { name, name, std::move(version), {}, {}, "npm" });
            }
            else
            {
                auto current = Scalar(item, { "current", "Current" });
                auto latest = Scalar(item, { "latest", "Latest" });
                if (current.empty() || latest.empty() || current == latest)
                {
                    continue;
                }
                AddRecord(records, { name, name, std::move(current), std::move(latest), {}, "npm" });
            }
        }
        return records;
    }

    std::vector<PackageRecord> ParseNpmRegistrySearchJsonImpl(
        std::string_view output,
        std::string managerKey)
    {
        std::vector<PackageRecord> records;
        if (managerKey != "bun" && managerKey != "npm")
        {
            return records;
        }
        auto root = FindJson(output, false, true);
        if (root && root->kind == JsonKind::Object)
        {
            auto const objects = FindAny(*root, { "objects", "Objects" });
            if (objects && objects->kind == JsonKind::Array)
            {
                for (auto const& entry : objects->array)
                {
                    if (entry.kind != JsonKind::Object)
                    {
                        continue;
                    }
                    auto const package = FindAny(entry, { "package", "Package" });
                    if (package)
                    {
                        auto const oldSize = records.size();
                        AddNpmSearchRecord(records, *package, managerKey);
                        if (records.size() > oldSize)
                        {
                            records.back().source = "npmjs.org";
                        }
                    }
                }
                return records;
            }
        }
        records = ParseNpmSearchImpl(output, managerKey);
        for (auto& record : records)
        {
            record.source = "npmjs.org";
        }
        return records;
    }

    std::vector<PackageRecord> ParseBunSearchJsonImpl(std::string_view output)
    {
        return ParseNpmRegistrySearchJsonImpl(output, "bun");
    }

    std::vector<PackageRecord> ParseBunInstalledImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto clean = StripAnsi(raw);
            std::string_view line(clean);
            constexpr std::string_view Tree = "\xe2\x94\x80\xe2\x94\x80";
            auto tree = line.find(Tree);
            std::size_t entryStart = std::string_view::npos;
            if (tree != std::string_view::npos)
            {
                entryStart = tree + Tree.size();
            }
            else if ((tree = line.find("-- ")) != std::string_view::npos)
            {
                entryStart = tree + 3;
            }
            if (entryStart == std::string_view::npos)
            {
                return;
            }

            auto const entry = TrimView(line.substr(entryStart));
            auto const at = entry.rfind('@');
            auto name = at > 0 ? TrimCopy(entry.substr(0, at)) : TrimCopy(entry);
            auto version = at > 0 ? TrimCopy(entry.substr(at + 1)) : std::string{};
            if (name.empty() || ContainsAsciiWhitespace(name))
            {
                return;
            }
            AddRecord(records, {
                name, std::move(name), std::move(version), {}, "npmjs.org", "bun" });
        });
        return records;
    }

    std::vector<PackageRecord> ParseBunOutdatedImpl(std::string_view output, bool preferLatest)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto cells = SplitPipeCells(StripAnsi(raw));
            if (cells.size() < 4 || EqualsInsensitive(cells[0], "package")
                || !HasAsciiLetterOrDigit(cells[0]) || IsRuleLine(cells[0]))
            {
                return;
            }
            auto available = preferLatest && !cells[3].empty() ? cells[3] : cells[2];
            if (cells[1].empty() || available.empty() || cells[1] == available)
            {
                return;
            }
            AddRecord(records, {
                cells[0], cells[0], cells[1], std::move(available), "npmjs.org", "bun" });
        });
        return records;
    }

    std::vector<PackageRecord> ParseDotnetToolTableImpl(
        std::string_view output,
        PackageOutputKind kind)
    {
        std::vector<PackageRecord> records;
        if (kind == PackageOutputKind::Updates)
        {
            return records;
        }
        bool pastHeader = false;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto clean = StripAnsi(raw);
            auto const line = TrimView(clean);
            if (line.empty())
            {
                return;
            }
            if (!pastHeader)
            {
                if (IsRuleLine(line))
                {
                    pastHeader = true;
                }
                return;
            }
            auto const columns = SplitWideColumns(line);
            if (columns.empty() || IsNoiseId(columns[0]))
            {
                return;
            }
            AddRecord(records, {
                std::string(columns[0]), std::string(columns[0]),
                columns.size() > 1 ? std::string(columns[1]) : std::string{},
                {}, {}, "dotnet" });
        });
        return records;
    }

    std::vector<PackageRecord> ParsePowerShellJsonImpl(std::string_view output, std::string managerKey)
    {
        std::vector<PackageRecord> records;
        auto root = FindJson(output, true, true);
        if (!root)
        {
            return records;
        }

        auto add = [&](JsonValue const& item)
        {
            if (item.kind != JsonKind::Object)
            {
                return;
            }
            auto name = Scalar(item, { "Name", "name" });
            if (name.empty())
            {
                return;
            }
            AddRecord(records, {
                name,
                std::move(name),
                Scalar(item, { "Version", "version" }),
                Scalar(item, { "AvailableVersion", "availableVersion", "available_version" }),
                Scalar(item, { "Repository", "repository", "Source", "source" }),
                managerKey });
        };

        if (root->kind == JsonKind::Array)
        {
            for (auto const& item : root->array)
            {
                add(item);
            }
        }
        else
        {
            add(*root);
        }
        return records;
    }

    std::vector<PackageRecord> ParseCargoSearchImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto clean = StripAnsi(raw);
            auto const line = TrimView(clean);
            auto const equals = line.find('=');
            if (line.empty() || equals == std::string_view::npos || equals == 0)
            {
                return;
            }
            auto name = TrimCopy(line.substr(0, equals));
            if (name.empty() || ContainsAsciiWhitespace(name))
            {
                return;
            }
            auto const rest = TrimView(line.substr(equals + 1));
            auto const firstQuote = rest.find('"');
            auto const secondQuote = firstQuote == std::string_view::npos
                ? std::string_view::npos
                : rest.find('"', firstQuote + 1);
            auto version = firstQuote != std::string_view::npos && secondQuote > firstQuote
                ? std::string(rest.substr(firstQuote + 1, secondQuote - firstQuote - 1))
                : std::string{};
            AddRecord(records, { name, std::move(name), std::move(version), {}, {}, "cargo" });
        });
        return records;
    }

    std::vector<PackageRecord> ParseCargoInstalledImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto clean = StripAnsi(raw);
            if (clean.empty() || IsSpace(clean.front()))
            {
                return;
            }
            auto line = TrimView(clean);
            while (!line.empty() && line.back() == ':')
            {
                line.remove_suffix(1);
                line = TrimView(line);
            }
            auto const parts = SplitWhitespace(line);
            if (parts.size() < 2 || IsNoiseId(parts[0]))
            {
                return;
            }
            auto version = parts.size() > 1 ? std::string(parts[1]) : std::string{};
            if (!version.empty() && (version.front() == 'v' || version.front() == 'V'))
            {
                version.erase(version.begin());
            }
            AddRecord(records, {
                std::string(parts[0]), std::string(parts[0]), std::move(version), {}, {}, "cargo" });
        });
        return records;
    }

    std::string NormalizeVersionToken(std::string_view token)
    {
        token = TrimView(token);
        if (!token.empty() && (token.front() == 'v' || token.front() == 'V'))
        {
            token.remove_prefix(1);
        }
        while (!token.empty() && (token.back() == ',' || token.back() == ':'))
        {
            token.remove_suffix(1);
        }
        if (token.empty() || token.front() < '0' || token.front() > '9'
            || token.find('.') == std::string_view::npos)
        {
            return {};
        }
        return std::string(token);
    }

    std::vector<PackageRecord> ParseCargoUpdatesImpl(std::string_view output)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto clean = StripAnsi(raw);
            auto const parts = SplitWhitespace(TrimView(clean));
            if (parts.size() < 3 || IsNoiseId(parts[0]))
            {
                return;
            }
            std::vector<std::string> versions;
            for (auto const part : parts)
            {
                auto version = NormalizeVersionToken(part);
                if (!version.empty())
                {
                    versions.push_back(std::move(version));
                    if (versions.size() == 2)
                    {
                        break;
                    }
                }
            }
            if (versions.size() < 2 || versions[0] == versions[1])
            {
                return;
            }
            auto id = TrimCopy(parts[0]);
            while (!id.empty() && id.back() == ':')
            {
                id.pop_back();
            }
            AddRecord(records, {
                id, std::move(id), std::move(versions[0]), std::move(versions[1]),
                "crates.io", "cargo" });
        });
        return records;
    }

    std::vector<PackageRecord> ParseVcpkgImpl(std::string_view output, PackageOutputKind kind)
    {
        std::vector<PackageRecord> records;
        ForEachLine(output, [&](std::string_view raw)
        {
            auto clean = StripAnsi(raw);
            auto const line = TrimView(clean);
            if (line.empty() || StartsWithInsensitive(line, "the result")
                || StartsWithInsensitive(line, "if your port") || StartsWithInsensitive(line, "no packages"))
            {
                return;
            }

            if (kind == PackageOutputKind::Updates)
            {
                auto const arrow = line.find("->");
                if (arrow == std::string_view::npos || arrow == 0)
                {
                    return;
                }
                auto const left = SplitWhitespace(TrimView(line.substr(0, arrow)));
                auto const right = SplitWhitespace(TrimView(line.substr(arrow + 2)));
                if (left.size() < 2 || right.empty() || left[0].find(':') == std::string_view::npos)
                {
                    return;
                }
                auto id = TrimCopy(left[0]);
                auto current = TrimCopy(left.back());
                auto available = TrimCopy(right[0]);
                if (current.empty() || available.empty() || current == available)
                {
                    return;
                }
                auto const separator = id.rfind(':');
                auto name = separator == std::string::npos ? id : id.substr(0, separator);
                auto source = separator == std::string::npos ? "vcpkg" : id.substr(separator + 1);
                AddRecord(records, {
                    std::move(name), std::move(id), std::move(current), std::move(available),
                    std::move(source), "vcpkg" });
                return;
            }

            auto const parts = SplitWhitespace(line);
            if (parts.size() < 2 || IsNoiseId(parts[0]))
            {
                return;
            }
            AddRecord(records, {
                std::string(parts[0]), std::string(parts[0]),
                parts.size() > 1 ? std::string(parts[1]) : std::string{},
                {}, {}, "vcpkg" });
        });
        return records;
    }

    template <typename Callback>
    std::vector<PackageRecord> Guarded(std::string_view input, Callback&& callback) noexcept
    {
        try
        {
            if (input.size() > MaxInputBytes)
            {
                return {};
            }
            return callback();
        }
        catch (...)
        {
            return {};
        }
    }
}

namespace winforge::core::packages
{
    std::vector<PackageRecord> ParseWingetTable(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseWingetTableImpl(output); });
    }

    std::vector<PackageRecord> ParseScoopSearch(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseScoopSearchImpl(output); });
    }

    std::vector<PackageRecord> ParseScoopInstalledJson(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseScoopInstalledJsonImpl(output); });
    }

    std::vector<PackageRecord> ParseScoopList(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseScoopListImpl(output); });
    }

    std::vector<PackageRecord> ParseScoopStatus(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseScoopStatusImpl(output); });
    }

    std::vector<PackageRecord> ParseChocolatey(
        std::string_view output,
        PackageOutputKind kind) noexcept
    {
        return Guarded(output, [&] { return ParseChocolateyImpl(output, kind); });
    }

    std::vector<PackageRecord> ParsePipJson(
        std::string_view output,
        PackageOutputKind kind) noexcept
    {
        return Guarded(output, [&] { return ParsePipJsonImpl(output, kind); });
    }

    std::vector<PackageRecord> ParsePyPiSearchJson(
        std::string_view output,
        std::string_view query) noexcept
    {
        return Guarded(output, [&] { return ParsePyPiSearchJsonImpl(output, query); });
    }

    std::vector<PackageRecord> ParseNpmJson(
        std::string_view output,
        PackageOutputKind kind) noexcept
    {
        return Guarded(output, [&] { return ParseNpmJsonImpl(output, kind); });
    }

    std::vector<PackageRecord> ParseBunSearchJson(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseBunSearchJsonImpl(output); });
    }

    std::vector<PackageRecord> ParseNpmRegistrySearchJson(
        std::string_view output,
        std::string_view managerKey) noexcept
    {
        return Guarded(output, [&]
        {
            return ParseNpmRegistrySearchJsonImpl(output, std::string(managerKey));
        });
    }

    std::vector<PackageRecord> ParseBunInstalled(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseBunInstalledImpl(output); });
    }

    std::vector<PackageRecord> ParseBunOutdated(std::string_view output, bool preferLatest) noexcept
    {
        return Guarded(output, [&] { return ParseBunOutdatedImpl(output, preferLatest); });
    }

    std::vector<PackageRecord> ParseDotnetToolTable(
        std::string_view output,
        PackageOutputKind kind) noexcept
    {
        return Guarded(output, [&] { return ParseDotnetToolTableImpl(output, kind); });
    }

    std::vector<PackageRecord> ParsePowerShellGalleryJson(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParsePowerShellJsonImpl(output, "psgallery"); });
    }

    std::vector<PackageRecord> ParsePowerShell7Json(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParsePowerShellJsonImpl(output, "pwsh7"); });
    }

    std::vector<PackageRecord> ParseCargoSearch(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseCargoSearchImpl(output); });
    }

    std::vector<PackageRecord> ParseCargoInstalled(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseCargoInstalledImpl(output); });
    }

    std::vector<PackageRecord> ParseCargoUpdates(std::string_view output) noexcept
    {
        return Guarded(output, [&] { return ParseCargoUpdatesImpl(output); });
    }

    std::vector<PackageRecord> ParseVcpkg(
        std::string_view output,
        PackageOutputKind kind) noexcept
    {
        return Guarded(output, [&] { return ParseVcpkgImpl(output, kind); });
    }

    PackageParseResult ParsePackageOutput(
        PackageParserKind parser,
        std::string_view output,
        PackageOutputKind outputKind,
        std::string_view managerKey,
        std::string_view query,
        bool preferLatest) noexcept
    {
        try
        {
            PackageParseResult result;
            switch (parser)
            {
            case PackageParserKind::None:
                return result;
            case PackageParserKind::WingetTable:
                result.packages = ParseWingetTable(output);
                return result;
            case PackageParserKind::ScoopSearch:
                result.packages = outputKind == PackageOutputKind::Updates
                    ? ParseScoopStatus(output)
                    : outputKind == PackageOutputKind::Installed
                        ? ParseScoopList(output)
                        : ParseScoopSearch(output);
                return result;
            case PackageParserKind::ScoopExport:
                result.packages = ParseScoopInstalledJson(output);
                return result;
            case PackageParserKind::ChocolateyDelimited:
                result.packages = ParseChocolatey(output, outputKind);
                return result;
            case PackageParserKind::JsonPackages:
                result.packages = ParsePipJson(output, outputKind);
                return result;
            case PackageParserKind::NpmJson:
                result.packages = ParseNpmJson(output, outputKind);
                return result;
            case PackageParserKind::DotnetTable:
                result.packages = ParseDotnetToolTable(output, outputKind);
                return result;
            case PackageParserKind::DotnetUpdatesFromNuGet:
                result.supported = false;
                result.requiresRuntimeResolution = true;
                result.diagnostic = "dotnet updates require installed-tool rows plus per-package NuGet version-index requests";
                return result;
            case PackageParserKind::PowerShellPackagesJson:
                result.packages = managerKey == "pwsh7"
                    ? ParsePowerShell7Json(output)
                    : ParsePowerShellGalleryJson(output);
                return result;
            case PackageParserKind::CargoSearch:
                result.packages = ParseCargoSearch(output);
                return result;
            case PackageParserKind::CargoInstalled:
                result.packages = ParseCargoInstalled(output);
                return result;
            case PackageParserKind::CargoUpdates:
                result.packages = ParseCargoUpdates(output);
                return result;
            case PackageParserKind::BunInstalled:
                result.packages = ParseBunInstalled(output);
                return result;
            case PackageParserKind::BunUpdates:
                result.packages = ParseBunOutdated(output, preferLatest);
                return result;
            case PackageParserKind::VcpkgSearch:
                result.packages = ParseVcpkg(output, PackageOutputKind::Search);
                return result;
            case PackageParserKind::VcpkgInstalled:
                result.packages = ParseVcpkg(output, PackageOutputKind::Installed);
                return result;
            case PackageParserKind::VcpkgUpdates:
                result.packages = ParseVcpkg(output, PackageOutputKind::Updates);
                return result;
            case PackageParserKind::PyPiSearch:
                result.packages = ParsePyPiSearchJson(output, query);
                result.requiresRuntimeResolution = !result.packages.empty();
                if (result.requiresRuntimeResolution)
                {
                    result.diagnostic = "PyPI project index parsed; latest versions require per-package detail requests";
                }
                return result;
            case PackageParserKind::NpmRegistrySearch:
                result.packages = ParseNpmRegistrySearchJson(
                    output,
                    managerKey.empty() ? std::string_view("bun") : managerKey);
                return result;
            default:
                result.supported = false;
                result.diagnostic = "unknown package parser kind";
                return result;
            }
        }
        catch (...)
        {
            PackageParseResult result;
            result.supported = false;
            return result;
        }
    }
}
