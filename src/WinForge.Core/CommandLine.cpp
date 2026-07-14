#include "CommandLine.h"

#include <algorithm>
#include <cwctype>
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <shellapi.h>

namespace
{
    std::wstring Trim(std::wstring_view value)
    {
        auto first = value.begin();
        auto last = value.end();
        while (first != last && std::iswspace(*first))
        {
            ++first;
        }
        while (last != first && std::iswspace(*(last - 1)))
        {
            --last;
        }
        return std::wstring(first, last);
    }

    winforge::core::LaunchRequest SplitRoute(std::wstring value)
    {
        winforge::core::LaunchRequest request;
        value = Trim(value);
        if (value.size() >= 2 && value.front() == L'"' && value.back() == L'"')
        {
            value = value.substr(1, value.size() - 2);
        }

        auto const query = value.find(L'?');
        auto const fragment = value.find(L'#');
        auto split = std::min(
            query == std::wstring::npos ? value.size() : query,
            fragment == std::wstring::npos ? value.size() : fragment);

        request.route = winforge::core::NormalizeRouteKey(value.substr(0, split));
        request.argument = split < value.size() ? value.substr(split) : L"";

        if (request.route.rfind(L"search:", 0) == 0 || request.route.rfind(L"manual:", 0) == 0)
        {
            auto const colon = request.route.find(L':');
            request.argument = request.route.substr(colon + 1);
            request.route = request.route.substr(0, colon);
        }

        // The managed Package Manager launch aliases select a view as well as a module.
        // Catalog aliases only retain the module identity, so preserve the view fragment
        // here before the native route index resolves the canonical module record.
        if (request.argument.empty())
        {
            if (request.route == L"package-discover" || request.route == L"packages-discover")
            {
                request.route = L"module.packages";
                request.argument = L"#discover";
            }
            else if (request.route == L"package-updates" || request.route == L"packages-updates")
            {
                request.route = L"module.packages";
                request.argument = L"#updates";
            }
            else if (request.route == L"package-installed" || request.route == L"packages-installed")
            {
                request.route = L"module.packages";
                request.argument = L"#installed";
            }
            else if (request.route == L"package-bundles" || request.route == L"packages-bundles")
            {
                request.route = L"module.packages";
                request.argument = L"#bundles";
            }
            else if (request.route == L"package-sources" || request.route == L"packages-sources")
            {
                request.route = L"module.packages";
                request.argument = L"#sources";
            }
            else if (request.route == L"package-ignored" || request.route == L"packages-ignored")
            {
                request.route = L"module.packages";
                request.argument = L"#ignored";
            }
            else if (request.route == L"package-setup" || request.route == L"packages-setup")
            {
                request.route = L"module.packages";
                request.argument = L"#setup";
            }
            else if (request.route == L"package-settings" || request.route == L"packages-settings")
            {
                request.route = L"module.packages";
                request.argument = L"#settings";
            }
            else if (request.route == L"package-operations" || request.route == L"packages-operations")
            {
                request.route = L"module.packages";
                request.argument = L"#operations";
            }
        }

        if (request.route.empty())
        {
            request.route = L"dashboard";
        }
        return request;
    }
}

namespace winforge::core
{
    std::wstring NormalizeRouteKey(std::wstring_view value)
    {
        auto result = Trim(value);
        std::transform(result.begin(), result.end(), result.begin(), [](wchar_t character)
        {
            return static_cast<wchar_t>(std::towlower(character));
        });
        return result;
    }

    LaunchRequest ParseLaunchRequest(std::vector<std::wstring> const& arguments)
    {
        for (std::size_t index = 1; index < arguments.size(); ++index)
        {
            auto const normalized = NormalizeRouteKey(arguments[index]);
            if (normalized == L"--page" && index + 1 < arguments.size())
            {
                return SplitRoute(arguments[index + 1]);
            }
            if (normalized.rfind(L"--page=", 0) == 0)
            {
                return SplitRoute(arguments[index].substr(7));
            }
            if (normalized == L"--reactor")
            {
                return SplitRoute(L"reactor");
            }
        }

        return {};
    }

    LaunchRequest CurrentProcessLaunchRequest()
    {
        int count = 0;
        auto raw = CommandLineToArgvW(GetCommandLineW(), &count);
        if (!raw)
        {
            return {};
        }

        std::vector<std::wstring> arguments;
        arguments.reserve(static_cast<std::size_t>(count));
        for (int index = 0; index < count; ++index)
        {
            arguments.emplace_back(raw[index]);
        }
        LocalFree(raw);
        return ParseLaunchRequest(arguments);
    }
}
