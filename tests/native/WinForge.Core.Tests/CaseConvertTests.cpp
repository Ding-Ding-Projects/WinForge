#include "CaseConvertTests.h"

#include "CaseConvert.h"

#include <iostream>
#include <string>
#include <string_view>

namespace
{
    using winforge::core::caseconvert::AllForms;
    using winforge::core::caseconvert::Tokenize;

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

}

NativeTestCounts RunCaseConvertTests()
{
    Suite suite;

    auto tokens = Tokenize(L"helloWorld42API");
    suite.Expect(tokens.size() == 4 && tokens[0] == L"hello" && tokens[1] == L"world" &&
        tokens[2] == L"42" && tokens[3] == L"api",
        "Case Converter tokenizes camelCase and digits");

    tokens = Tokenize(L"XMLHttpRequest");
    suite.Expect(tokens.size() == 2 && tokens[0] == L"xmlhttp" && tokens[1] == L"request",
        "Case Converter keeps acronym runs together");

    tokens = Tokenize(L"HTTP2Server");
    suite.Expect(tokens.size() == 3 && tokens[0] == L"http" && tokens[1] == L"2" && tokens[2] == L"server",
        "Case Converter splits digit boundaries");

    tokens = Tokenize(L"hello_world-foo.bar/baz\\qux:zap");
    suite.Expect(tokens.size() == 7 && tokens[0] == L"hello" && tokens[6] == L"zap",
        "Case Converter splits on every documented separator");

    tokens = Tokenize(L"漢字Test");
    suite.Expect(tokens.size() == 1 && tokens[0] == L"漢字test",
        "Case Converter keeps CJK and camel boundaries aligned with managed behavior");

    tokens = Tokenize(L"");
    suite.Expect(tokens.empty(), "Case Converter handles empty input");

    tokens = Tokenize(L"!@#$");
    suite.Expect(tokens.empty(), "Case Converter drops punctuation-only input");

    auto forms = AllForms(L"helloWorld42API");
    suite.Expect(forms.size() == 10 &&
        forms[0].en == L"camelCase" && forms[0].value == L"helloWorld42Api" &&
        forms[1].value == L"HelloWorld42Api" &&
        forms[2].value == L"hello_world_42_api" &&
        forms[3].value == L"hello-world-42-api" &&
        forms[4].value == L"HELLO_WORLD_42_API" &&
        forms[5].value == L"Hello World 42 Api" &&
        forms[6].value == L"Hello world 42 api" &&
        forms[7].value == L"hello.world.42.api" &&
        forms[8].value == L"hello/world/42/api" &&
        forms[9].value == L"Hello-World-42-Api",
        "Case Converter renders every supported form in the right order");

    forms = AllForms(L"İstanbul_ßeta_éclair");
    suite.Expect(forms[0].value == L"İstanbulßetaÉclair" &&
        forms[2].value == L"İstanbul_ßeta_éclair",
        "Case Converter preserves invariant dotted-I and accent handling");

    forms = AllForms(L"");
    suite.Expect(forms.size() == 10 && forms[0].value.empty() && forms[9].value.empty(),
        "Case Converter renders empty output for empty input");

    std::cout << "\nCase Converter tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
