#pragma once

struct NativeTestCounts
{
    int passed{ 0 };
    int failed{ 0 };
};

NativeTestCounts RunPackageManagerTests();
