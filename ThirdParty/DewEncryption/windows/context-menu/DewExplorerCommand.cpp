#include <windows.h>
#include <shellapi.h>
#include <shobjidl_core.h>
#include <wrl/client.h>
#include <wrl/implements.h>
#include <wrl/module.h>

#include <algorithm>
#include <cwctype>
#include <string>
#include <vector>

using Microsoft::WRL::ClassicCom;
using Microsoft::WRL::ComPtr;
using Microsoft::WRL::InhibitRoOriginateError;
using Microsoft::WRL::Module;
using Microsoft::WRL::ModuleType;
using Microsoft::WRL::RuntimeClass;
using Microsoft::WRL::RuntimeClassFlags;

namespace {

constexpr wchar_t kConfigSubkey[] = L"Software\\DewEncryption\\ContextMenu";
constexpr wchar_t kFallbackPython[] = L"python";

enum class DewCommandKind {
    Snapshot,
    History,
    Watch,
    QuickCreateContainer,
    VeraCryptEncrypt,
    GitCommitPush,
    DockerUpload,
    DockerSaveHere,
};

std::wstring QuoteArg(const std::wstring& arg) {
    if (arg.find_first_of(L" \\\"") == std::wstring::npos) {
        return arg;
    }

    std::wstring out;
    out.push_back(L'"');
    for (size_t i = 0; i < arg.size(); ++i) {
        if (arg[i] == L'\\') {
            size_t end = i + 1;
            while (end < arg.size() && arg[end] == L'\\') {
                ++end;
            }
            size_t slash_count = end - i;
            if (end == arg.size() || arg[end] == L'"') {
                slash_count *= 2;
            }
            out.append(slash_count, L'\\');
            i = end - 1;
        } else if (arg[i] == L'"') {
            out.append(L"\\\"");
        } else {
            out.push_back(arg[i]);
        }
    }
    out.push_back(L'"');
    return out;
}

HRESULT CopyString(const std::wstring& value, PWSTR* out) {
    if (!out) {
        return E_POINTER;
    }
    *out = nullptr;
    const size_t bytes = (value.size() + 1) * sizeof(wchar_t);
    auto* buffer = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
    if (!buffer) {
        return E_OUTOFMEMORY;
    }
    memcpy(buffer, value.c_str(), bytes);
    *out = buffer;
    return S_OK;
}

std::wstring ReadConfigString(const wchar_t* value_name) {
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, kConfigSubkey, 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, kConfigSubkey, 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
            return L"";
        }
    }

    DWORD type = 0;
    DWORD bytes = 0;
    LONG result = RegQueryValueExW(key, value_name, nullptr, &type, nullptr, &bytes);
    if (result != ERROR_SUCCESS || (type != REG_SZ && type != REG_EXPAND_SZ) || bytes == 0) {
        RegCloseKey(key);
        return L"";
    }

    std::wstring value(bytes / sizeof(wchar_t), L'\0');
    result = RegQueryValueExW(key, value_name, nullptr, &type, reinterpret_cast<LPBYTE>(value.data()), &bytes);
    RegCloseKey(key);
    if (result != ERROR_SUCCESS) {
        return L"";
    }
    while (!value.empty() && value.back() == L'\0') {
        value.pop_back();
    }
    if (type == REG_EXPAND_SZ) {
        DWORD needed = ExpandEnvironmentStringsW(value.c_str(), nullptr, 0);
        if (needed > 0) {
            std::wstring expanded(needed, L'\0');
            DWORD written = ExpandEnvironmentStringsW(value.c_str(), expanded.data(), needed);
            if (written > 0 && written <= needed) {
                while (!expanded.empty() && expanded.back() == L'\0') {
                    expanded.pop_back();
                }
                return expanded;
            }
        }
    }
    return value;
}

std::wstring InstallRoot() {
    return ReadConfigString(L"InstallRoot");
}

std::wstring PythonPath() {
    std::wstring python = ReadConfigString(L"PythonPath");
    return python.empty() ? std::wstring(kFallbackPython) : python;
}

std::wstring JoinPath(const std::wstring& left, const std::wstring& right) {
    if (left.empty()) {
        return right;
    }
    if (left.back() == L'\\' || left.back() == L'/') {
        return left + right;
    }
    return left + L"\\" + right;
}

std::vector<std::wstring> SelectionPaths(IShellItemArray* items) {
    std::vector<std::wstring> paths;
    if (!items) {
        return paths;
    }

    DWORD count = 0;
    if (FAILED(items->GetCount(&count))) {
        return paths;
    }

    for (DWORD i = 0; i < count; ++i) {
        ComPtr<IShellItem> item;
        if (FAILED(items->GetItemAt(i, &item))) {
            continue;
        }

        PWSTR raw_path = nullptr;
        if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &raw_path)) && raw_path) {
            paths.emplace_back(raw_path);
            CoTaskMemFree(raw_path);
        }
    }
    return paths;
}

std::wstring FirstPath(IShellItemArray* items) {
    std::vector<std::wstring> paths = SelectionPaths(items);
    return paths.empty() ? L"" : paths.front();
}

std::wstring BuildPythonArgs(DewCommandKind kind, const std::vector<std::wstring>& paths) {
    std::vector<std::wstring> args;
    switch (kind) {
    case DewCommandKind::Snapshot:
        args = {L"-m", L"dew_encryption"};
        args.insert(args.end(), paths.begin(), paths.end());
        break;
    case DewCommandKind::History:
        args = {L"-m", L"dew_encryption.gui"};
        if (!paths.empty()) {
            args.push_back(paths.front());
        }
        args.push_back(L"--history");
        break;
    case DewCommandKind::Watch:
        args = {L"-m", L"dew_encryption", L"watch"};
        if (!paths.empty()) {
            args.push_back(paths.front());
        }
        break;
    case DewCommandKind::QuickCreateContainer:
        args = {L"-m", L"dew_encryption", L"container-quick-create"};
        args.insert(args.end(), paths.begin(), paths.end());
        break;
    case DewCommandKind::VeraCryptEncrypt:
        args = {L"-m", L"dew_encryption", L"veracrypt-encrypt"};
        args.insert(args.end(), paths.begin(), paths.end());
        break;
    case DewCommandKind::GitCommitPush:
        args = {L"-m", L"dew_encryption", L"git-commit-push"};
        if (!paths.empty()) {
            args.push_back(paths.front());
        }
        break;
    case DewCommandKind::DockerUpload:
        args = {L"-m", L"dew_encryption.gui", L"--docker-upload"};
        if (!paths.empty()) {
            args.push_back(paths.front());
        }
        break;
    case DewCommandKind::DockerSaveHere:
        args = {L"-m", L"dew_encryption.gui", L"--docker-save-here"};
        if (!paths.empty()) {
            args.push_back(paths.front());
        }
        break;
    }

    std::wstring command_line;
    for (const auto& arg : args) {
        if (!command_line.empty()) {
            command_line.push_back(L' ');
        }
        command_line += QuoteArg(arg);
    }
    return command_line;
}

bool IsConsoleCommand(DewCommandKind kind) {
    return kind == DewCommandKind::QuickCreateContainer ||
           kind == DewCommandKind::VeraCryptEncrypt ||
           kind == DewCommandKind::GitCommitPush;
}

HRESULT LaunchPython(DewCommandKind kind, IShellItemArray* items) {
    std::vector<std::wstring> paths = SelectionPaths(items);
    std::wstring root = InstallRoot();
    std::wstring python = PythonPath();
    std::wstring args = BuildPythonArgs(kind, paths);

    if (IsConsoleCommand(kind)) {
        std::wstring command = L"Set-Location -LiteralPath " + QuoteArg(root) +
            L"; & " + QuoteArg(python) + L" " + args +
            L"; Read-Host 'Press Enter to close'";
        std::wstring ps_args = L"-NoProfile -ExecutionPolicy Bypass -NoExit -Command " + QuoteArg(command);
        HINSTANCE ret = ShellExecuteW(nullptr, L"open", L"powershell.exe", ps_args.c_str(), root.empty() ? nullptr : root.c_str(), SW_SHOWNORMAL);
        return reinterpret_cast<INT_PTR>(ret) > HINSTANCE_ERROR ? S_OK : HRESULT_FROM_WIN32(GetLastError());
    }

    HINSTANCE ret = ShellExecuteW(nullptr, L"open", python.c_str(), args.c_str(), root.empty() ? nullptr : root.c_str(), SW_SHOWNORMAL);
    return reinterpret_cast<INT_PTR>(ret) > HINSTANCE_ERROR ? S_OK : HRESULT_FROM_WIN32(GetLastError());
}

class DewExplorerCommandBase : public RuntimeClass<RuntimeClassFlags<ClassicCom | InhibitRoOriginateError>, IExplorerCommand> {
public:
    explicit DewExplorerCommandBase(DewCommandKind kind, const wchar_t* title)
        : kind_(kind), title_(title) {}

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override {
        return CopyString(title_, name);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override {
        std::wstring root = InstallRoot();
        if (root.empty()) {
            *icon = nullptr;
            return E_NOTIMPL;
        }
        return CopyString(JoinPath(root, L"assets\\icons\\dew-main.ico"), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* info_tip) override {
        return CopyString(title_, info_tip);
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guid_command_name) override {
        *guid_command_name = GUID_NULL;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* cmd_state) override {
        if (!cmd_state) {
            return E_POINTER;
        }
        std::vector<std::wstring> paths = SelectionPaths(items);
        if (paths.empty() && kind_ != DewCommandKind::DockerSaveHere && kind_ != DewCommandKind::GitCommitPush) {
            *cmd_state = ECS_DISABLED;
        } else {
            *cmd_state = ECS_ENABLED;
        }
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override {
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enum_commands) override {
        *enum_commands = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override {
        return LaunchPython(kind_, items);
    }

private:
    DewCommandKind kind_;
    std::wstring title_;
};

} // namespace

extern "C" BOOL WINAPI DllMain(HINSTANCE, DWORD, LPVOID) {
    return TRUE;
}

class __declspec(uuid("611F3291-DC8B-47D6-97C0-0D779913B5C6")) DewSnapshotCommand final : public DewExplorerCommandBase {
public:
    DewSnapshotCommand() : DewExplorerCommandBase(DewCommandKind::Snapshot, L"Dew Encryption") {}
};

class __declspec(uuid("B7C50B51-DD33-495C-B595-28AE6E7F7CB6")) DewHistoryCommand final : public DewExplorerCommandBase {
public:
    DewHistoryCommand() : DewExplorerCommandBase(DewCommandKind::History, L"Dew Encryption history") {}
};

class __declspec(uuid("286364AE-FED7-4648-ACCC-66FF2C85B4EE")) DewWatchCommand final : public DewExplorerCommandBase {
public:
    DewWatchCommand() : DewExplorerCommandBase(DewCommandKind::Watch, L"Dew Encryption start file history") {}
};

class __declspec(uuid("046033C1-C212-4FDB-B7C6-3E698A56A37A")) DewQuickCreateContainerCommand final : public DewExplorerCommandBase {
public:
    DewQuickCreateContainerCommand() : DewExplorerCommandBase(DewCommandKind::QuickCreateContainer, L"Dew Encryption quick create container") {}
};

class __declspec(uuid("05AAAB86-21D0-4D2F-9031-9C89E0DC5277")) DewVeraCryptEncryptCommand final : public DewExplorerCommandBase {
public:
    DewVeraCryptEncryptCommand() : DewExplorerCommandBase(DewCommandKind::VeraCryptEncrypt, L"Dew Encryption VeraCrypt encrypt") {}
};

class __declspec(uuid("2A1C6932-5EBC-4E15-86F1-4F611CDC8683")) DewGitCommitPushCommand final : public DewExplorerCommandBase {
public:
    DewGitCommitPushCommand() : DewExplorerCommandBase(DewCommandKind::GitCommitPush, L"Dew Encryption commit and push repo") {}
};

class __declspec(uuid("F9D7D759-9E6A-4729-ABDA-6AA059B3AA79")) DewDockerUploadCommand final : public DewExplorerCommandBase {
public:
    DewDockerUploadCommand() : DewExplorerCommandBase(DewCommandKind::DockerUpload, L"Dew Encryption upload to Docker or custom remote") {}
};

class __declspec(uuid("427D8DDC-2DDE-4783-BDC5-8804CC0C9E34")) DewDockerSaveHereCommand final : public DewExplorerCommandBase {
public:
    DewDockerSaveHereCommand() : DewExplorerCommandBase(DewCommandKind::DockerSaveHere, L"Dew Encryption save Docker image here") {}
};

CoCreatableClass(DewSnapshotCommand)
CoCreatableClass(DewHistoryCommand)
CoCreatableClass(DewWatchCommand)
CoCreatableClass(DewQuickCreateContainerCommand)
CoCreatableClass(DewVeraCryptEncryptCommand)
CoCreatableClass(DewGitCommitPushCommand)
CoCreatableClass(DewDockerUploadCommand)
CoCreatableClass(DewDockerSaveHereCommand)

CoCreatableClassWrlCreatorMapInclude(DewSnapshotCommand)
CoCreatableClassWrlCreatorMapInclude(DewHistoryCommand)
CoCreatableClassWrlCreatorMapInclude(DewWatchCommand)
CoCreatableClassWrlCreatorMapInclude(DewQuickCreateContainerCommand)
CoCreatableClassWrlCreatorMapInclude(DewVeraCryptEncryptCommand)
CoCreatableClassWrlCreatorMapInclude(DewGitCommitPushCommand)
CoCreatableClassWrlCreatorMapInclude(DewDockerUploadCommand)
CoCreatableClassWrlCreatorMapInclude(DewDockerSaveHereCommand)

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv) {
    if (!ppv) {
        return E_POINTER;
    }
    *ppv = nullptr;
    return Module<ModuleType::InProc>::GetModule().GetClassObject(rclsid, riid, ppv);
}

STDAPI DllCanUnloadNow() {
    return Module<ModuleType::InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetActivationFactory(HSTRING activatable_class_id, IActivationFactory** factory) {
    return Module<ModuleType::InProc>::GetModule().GetActivationFactory(activatable_class_id, factory);
}
