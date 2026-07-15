#include "CodecTests.h"

#include "Codec.h"

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

    using winforge::core::codec::Decode;
    using winforge::core::codec::Encode;
    using winforge::core::codec::Encoding;
}

NativeTestCounts winforge::tests::codec::RunCodecTests()
{
    Suite suite;

    auto result = Encode(L"foo", Encoding::Base32);
    suite.Expect(result.ok && result.text == L"MZXW6===", "Codec Base32 matches RFC 4648 padding");
    result = Encode(L"foo", Encoding::Base32NoPad);
    suite.Expect(result.ok && result.text == L"MZXW6", "Codec Base32 no-pad matches RFC 4648");
    result = Decode(L"m z-x w 6 = = =", Encoding::Base32);
    suite.Expect(result.ok && result.text == L"foo", "Codec Base32 accepts case, whitespace, hyphen, and padding");

    result = Encode(L"Hello World!", Encoding::Base58);
    auto const base58RoundTrip = result.ok ? Decode(result.text, Encoding::Base58) : decltype(Decode(L"", Encoding::Base58)){};
    suite.Expect(result.ok && result.text == L"2NEpo7TZRRrLZSi2U", "Codec Base58 produces deterministic output");
    suite.Expect(base58RoundTrip.ok && base58RoundTrip.text == L"Hello World!", "Codec Base58 round-trips UTF-8 text");

    result = Encode(L"Hello", Encoding::Ascii85);
    auto const ascii85RoundTrip = result.ok ? Decode(result.text, Encoding::Ascii85) : decltype(Decode(L"", Encoding::Ascii85)){};
    suite.Expect(result.ok && result.text == L"<~87cURDZ~>", "Codec Ascii85 matches Adobe output");
    suite.Expect(ascii85RoundTrip.ok && ascii85RoundTrip.text == L"Hello", "Codec Ascii85 round-trips text");
    result = Encode(L"", Encoding::Ascii85);
    suite.Expect(result.ok && result.text == L"<~~>", "Codec Ascii85 preserves empty marker");
    result = Decode(L"<~z~>", Encoding::Ascii85);
    suite.Expect(result.ok && result.text == std::wstring(4, L'\0'), "Codec Ascii85 decodes zero shortcut");

    auto const unicode = std::wstring{ L'\u00E9', static_cast<wchar_t>(0xD83D), static_cast<wchar_t>(0xDE42) };
    for (auto const encoding : { Encoding::Base32, Encoding::Base32NoPad, Encoding::Base58, Encoding::Ascii85 })
    {
        auto const encoded = Encode(unicode, encoding);
        auto const decoded = encoded.ok ? Decode(encoded.text, encoding) : decltype(Decode(L"", encoding)){};
        suite.Expect(encoded.ok && decoded.ok && decoded.text == unicode, "Codec round-trips Unicode UTF-8 bytes");
    }

    result = Decode(L"not-valid!", Encoding::Base58);
    suite.Expect(!result.ok && result.text.empty(), "Codec Base58 rejects malformed input atomically");
    result = Decode(L"<~!~>", Encoding::Ascii85);
    suite.Expect(!result.ok && result.text.empty(), "Codec Ascii85 rejects a one-character trailing group");

    std::cout << "\nCodec tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
