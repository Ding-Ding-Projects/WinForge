#include "pch.h"
#include "CatalogLoader.h"

#include <fstream>
#include <sstream>
#include <stdexcept>

namespace
{
    std::wstring ToWide(winrt::hstring const& value)
    {
        return std::wstring(value.c_str(), value.size());
    }
}

namespace winforge::app
{
    std::filesystem::path ExecutableDirectory()
    {
        std::wstring buffer(32768, L'\0');
        auto const length = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
        if (length == 0 || length >= buffer.size())
        {
            throw std::runtime_error("Unable to locate the WinForge executable directory.");
        }
        buffer.resize(length);
        return std::filesystem::path(buffer).parent_path();
    }

    std::vector<winforge::core::ModuleRecord> LoadModuleCatalog()
    {
        auto const path = ExecutableDirectory() / L"Resources" / L"modules.json";
        std::ifstream input(path, std::ios::binary);
        if (!input)
        {
            throw std::runtime_error("Native module catalog is missing from the application output.");
        }

        std::ostringstream stream;
        stream << input.rdbuf();
        auto json = stream.str();
        if (json.size() >= 3 &&
            static_cast<unsigned char>(json[0]) == 0xEF &&
            static_cast<unsigned char>(json[1]) == 0xBB &&
            static_cast<unsigned char>(json[2]) == 0xBF)
        {
            json.erase(0, 3);
        }
        auto const root = winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(json));
        auto const routes = root.GetNamedArray(L"routes");

        std::vector<winforge::core::ModuleRecord> result;
        result.reserve(routes.Size());
        for (auto const& value : routes)
        {
            auto const route = value.GetObject();
            winforge::core::ModuleRecord record;
            record.id = ToWide(route.GetNamedString(L"id"));
            record.tag = ToWide(route.GetNamedString(L"tag"));
            record.kind = ToWide(route.GetNamedString(L"kind"));
            record.name.en = ToWide(route.GetNamedString(L"en"));
            record.name.zh = ToWide(route.GetNamedString(L"zh"));
            record.glyph = ToWide(route.GetNamedString(L"glyph", L""));
            record.keywords = ToWide(route.GetNamedString(L"keywords", L""));

            auto const aliases = route.GetNamedArray(L"aliases");
            record.aliases.reserve(aliases.Size());
            for (auto const& alias : aliases)
            {
                record.aliases.push_back(ToWide(alias.GetString()));
            }
            result.push_back(std::move(record));
        }
        return result;
    }
}
