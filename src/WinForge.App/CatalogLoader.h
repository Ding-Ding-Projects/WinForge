#pragma once

#include "../WinForge.Core/ModuleRecord.h"

#include <filesystem>
#include <vector>

namespace winforge::app
{
    [[nodiscard]] std::filesystem::path ExecutableDirectory();
    [[nodiscard]] std::vector<winforge::core::ModuleRecord> LoadModuleCatalog();
}
