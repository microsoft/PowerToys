// PowerScripts Windows 11 modern context-menu handler.
//
// A self-contained IExplorerCommand COM server (no PowerToys common dependencies). It surfaces a
// top-level "PowerScript" entry with a dynamic submenu of the file scripts that match the current
// selection. The actual matching/running logic lives in PowerScripts.Host.exe (deployed next to
// this DLL); the handler is a thin shell that:
//   * GetState  -> runs "Host shell-menu --files <paths>", caches the id/name lines, hides itself
//                  when nothing matches.
//   * EnumSubCommands -> turns each cached line into a submenu item.
//   * Invoke (item) -> runs "Host run <id> --files <paths>".

#include <windows.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <wrl/module.h>
#include <wrl/implements.h>
#include <wrl/client.h>

#include <string>
#include <vector>

using namespace Microsoft::WRL;

namespace
{
    HMODULE g_hModule = nullptr;
    long g_refModule = 0;

    // Full path to PowerScripts.Host.exe, assumed to sit next to this DLL.
    std::wstring FindHostExe()
    {
        wchar_t path[MAX_PATH] = {};
        GetModuleFileNameW(g_hModule, path, ARRAYSIZE(path));
        std::wstring dir(path);
        const size_t slash = dir.find_last_of(L"\\/");
        if (slash != std::wstring::npos)
        {
            dir.erase(slash + 1);
        }
        return dir + L"PowerScripts.Host.exe";
    }

    // Extracts the filesystem paths from a shell selection.
    std::vector<std::wstring> ExtractPaths(IShellItemArray* selection)
    {
        std::vector<std::wstring> result;
        if (selection == nullptr)
        {
            return result;
        }

        DWORD count = 0;
        if (FAILED(selection->GetCount(&count)))
        {
            return result;
        }

        for (DWORD i = 0; i < count; ++i)
        {
            ComPtr<IShellItem> item;
            if (FAILED(selection->GetItemAt(i, &item)))
            {
                continue;
            }

            PWSTR pszPath = nullptr;
            if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &pszPath)) && pszPath != nullptr)
            {
                result.emplace_back(pszPath);
                CoTaskMemFree(pszPath);
            }
        }

        return result;
    }

    // Quotes a single command-line argument.
    std::wstring Quote(const std::wstring& value)
    {
        return L"\"" + value + L"\"";
    }

    std::wstring BuildFilesArguments(const std::vector<std::wstring>& files)
    {
        std::wstring args;
        for (const auto& file : files)
        {
            args += L" " + Quote(file);
        }
        return args;
    }

    // Runs a Host command and returns its stdout. Used only for the (small) shell-menu listing.
    std::wstring RunHostCapture(const std::wstring& arguments)
    {
        std::wstring output;

        SECURITY_ATTRIBUTES sa = {};
        sa.nLength = sizeof(sa);
        sa.bInheritHandle = TRUE;

        HANDLE readPipe = nullptr;
        HANDLE writePipe = nullptr;
        if (!CreatePipe(&readPipe, &writePipe, &sa, 0))
        {
            return output;
        }
        SetHandleInformation(readPipe, HANDLE_FLAG_INHERIT, 0);

        std::wstring commandLine = Quote(FindHostExe()) + L" " + arguments;

        STARTUPINFOW si = {};
        si.cb = sizeof(si);
        si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
        si.wShowWindow = SW_HIDE;
        si.hStdOutput = writePipe;
        si.hStdError = writePipe;

        PROCESS_INFORMATION pi = {};
        std::vector<wchar_t> mutableCmd(commandLine.begin(), commandLine.end());
        mutableCmd.push_back(L'\0');

        if (!CreateProcessW(nullptr, mutableCmd.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
        {
            CloseHandle(readPipe);
            CloseHandle(writePipe);
            return output;
        }

        CloseHandle(writePipe);

        char buffer[4096];
        DWORD read = 0;
        std::string raw;
        while (ReadFile(readPipe, buffer, sizeof(buffer), &read, nullptr) && read > 0)
        {
            raw.append(buffer, read);
        }

        CloseHandle(readPipe);
        WaitForSingleObject(pi.hProcess, 15000);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);

        if (!raw.empty())
        {
            const int needed = MultiByteToWideChar(CP_UTF8, 0, raw.c_str(), static_cast<int>(raw.size()), nullptr, 0);
            if (needed > 0)
            {
                output.resize(needed);
                MultiByteToWideChar(CP_UTF8, 0, raw.c_str(), static_cast<int>(raw.size()), output.data(), needed);
            }
        }

        return output;
    }

    // Runs a Host command fire-and-forget (used to actually execute a script).
    void RunHostDetached(const std::wstring& arguments)
    {
        std::wstring commandLine = Quote(FindHostExe()) + L" " + arguments;

        STARTUPINFOW si = {};
        si.cb = sizeof(si);
        si.dwFlags = STARTF_USESHOWWINDOW;
        si.wShowWindow = SW_HIDE;

        PROCESS_INFORMATION pi = {};
        std::vector<wchar_t> mutableCmd(commandLine.begin(), commandLine.end());
        mutableCmd.push_back(L'\0');

        if (CreateProcessW(nullptr, mutableCmd.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
        {
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
        }
    }

    struct ScriptEntry
    {
        std::wstring Id;
        std::wstring Name;
    };

    // Parses "id\tname" lines into entries.
    std::vector<ScriptEntry> ParseMenu(const std::wstring& text)
    {
        std::vector<ScriptEntry> entries;
        size_t start = 0;
        while (start < text.size())
        {
            size_t end = text.find(L'\n', start);
            std::wstring line = (end == std::wstring::npos) ? text.substr(start) : text.substr(start, end - start);
            start = (end == std::wstring::npos) ? text.size() : end + 1;

            if (!line.empty() && line.back() == L'\r')
            {
                line.pop_back();
            }
            if (line.empty())
            {
                continue;
            }

            const size_t tab = line.find(L'\t');
            if (tab == std::wstring::npos)
            {
                continue;
            }

            ScriptEntry entry;
            entry.Id = line.substr(0, tab);
            entry.Name = line.substr(tab + 1);
            if (!entry.Id.empty())
            {
                entries.push_back(std::move(entry));
            }
        }
        return entries;
    }
}

// A single submenu item: "Convert Markdown to Text", etc.
class PowerScriptSubCommand : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand>
{
public:
    PowerScriptSubCommand(std::wstring id, std::wstring name, std::vector<std::wstring> files) :
        m_id(std::move(id)), m_name(std::move(name)), m_files(std::move(files))
    {
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override { return SHStrDupW(m_name.c_str(), name); }
    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override { *icon = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tip) override { *tip = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP GetCanonicalName(GUID* guid) override { *guid = GUID_NULL; return S_OK; }
    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override { *state = ECS_ENABLED; return S_OK; }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override { *flags = ECF_DEFAULT; return S_OK; }
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumerator) override { *enumerator = nullptr; return E_NOTIMPL; }

    IFACEMETHODIMP Invoke(IShellItemArray* selection, IBindCtx*) override
    {
        std::vector<std::wstring> files = m_files;
        if (files.empty())
        {
            files = ExtractPaths(selection);
        }

        RunHostDetached(L"run " + m_id + L" --files" + BuildFilesArguments(files));
        return S_OK;
    }

private:
    std::wstring m_id;
    std::wstring m_name;
    std::vector<std::wstring> m_files;
};

// IEnumExplorerCommand over the submenu items.
class PowerScriptEnum : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IEnumExplorerCommand>
{
public:
    explicit PowerScriptEnum(std::vector<ComPtr<IExplorerCommand>> commands) :
        m_commands(std::move(commands))
    {
    }

    IFACEMETHODIMP Next(ULONG count, IExplorerCommand** commands, ULONG* fetched) override
    {
        ULONG produced = 0;
        for (; produced < count && m_index < m_commands.size(); ++produced, ++m_index)
        {
            m_commands[m_index].CopyTo(&commands[produced]);
        }
        if (fetched != nullptr)
        {
            *fetched = produced;
        }
        return (produced == count) ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Skip(ULONG count) override
    {
        m_index += count;
        return (m_index <= m_commands.size()) ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP Reset() override
    {
        m_index = 0;
        return S_OK;
    }

    IFACEMETHODIMP Clone(IEnumExplorerCommand** out) override
    {
        *out = nullptr;
        return E_NOTIMPL;
    }

private:
    std::vector<ComPtr<IExplorerCommand>> m_commands;
    size_t m_index = 0;
};

// Top-level "PowerScript" command with a dynamic submenu.
class __declspec(uuid("9FF7C126-9562-4F16-A6FB-9622B26E0D62")) PowerScriptCommand :
    public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite>
{
public:
    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* name) override { return SHStrDupW(L"PowerScripts", name); }
    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override { *icon = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tip) override { *tip = nullptr; return E_NOTIMPL; }
    IFACEMETHODIMP GetCanonicalName(GUID* guid) override { *guid = GUID_NULL; return S_OK; }

    // Called before EnumSubCommands on the same instance; we use it to compute (and cache) the
    // matching scripts and to hide the entry when nothing matches.
    IFACEMETHODIMP GetState(IShellItemArray* selection, BOOL, EXPCMDSTATE* state) override
    {
        m_files = ExtractPaths(selection);
        m_entries.clear();

        if (!m_files.empty())
        {
            const std::wstring output = RunHostCapture(L"shell-menu --files" + BuildFilesArguments(m_files));
            m_entries = ParseMenu(output);
        }

        *state = m_entries.empty() ? ECS_HIDDEN : ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override { *flags = ECF_HASSUBCOMMANDS; return S_OK; }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** enumerator) override
    {
        *enumerator = nullptr;

        std::vector<ComPtr<IExplorerCommand>> commands;
        for (const auto& entry : m_entries)
        {
            commands.push_back(Make<PowerScriptSubCommand>(entry.Id, entry.Name, m_files));
        }

        auto enumObject = Make<PowerScriptEnum>(std::move(commands));
        return enumObject.CopyTo(enumerator);
    }

    IFACEMETHODIMP Invoke(IShellItemArray*, IBindCtx*) override { return S_OK; }

    // IObjectWithSite
    IFACEMETHODIMP SetSite(IUnknown* site) override { m_site = site; return S_OK; }
    IFACEMETHODIMP GetSite(REFIID riid, void** ppv) override { return m_site.CopyTo(riid, ppv); }

private:
    ComPtr<IUnknown> m_site;
    std::vector<std::wstring> m_files;
    std::vector<ScriptEntry> m_entries;
};

CoCreatableClass(PowerScriptCommand);

STDAPI DllGetActivationFactory(_In_ HSTRING activatableClassId, _COM_Outptr_ IActivationFactory** factory)
{
    return Module<ModuleType::InProc>::GetModule().GetActivationFactory(activatableClassId, factory);
}

STDAPI DllCanUnloadNow()
{
    return (Module<InProc>::GetModule().GetObjectCount() == 0 && g_refModule == 0) ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _COM_Outptr_ void** ppv)
{
    return Module<InProc>::GetModule().GetClassObject(rclsid, riid, ppv);
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    default:
        break;
    }
    return TRUE;
}
