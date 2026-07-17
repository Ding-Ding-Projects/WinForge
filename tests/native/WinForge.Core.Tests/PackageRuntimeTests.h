#pragma once

struct PackageRuntimeTestCounts
{
    int passed{};
    int failed{};
};

PackageRuntimeTestCounts RunPackageRuntimeTests();
