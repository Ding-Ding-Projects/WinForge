#pragma once

#include <string_view>

namespace winforge::tests
{
    using Expectation = void (*)(bool condition, std::string_view name);

    // Returns -1 when the current invocation is not a process-runner helper.
    int TryRunProcessRunnerHelper(int argc, wchar_t** argv);
    void RunProcessRunnerTests(Expectation expect);
}
