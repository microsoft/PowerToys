// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <atlfile.h>
#include <atlstr.h>
#include <shobjidl_core.h>
#include <string>
#include <filesystem>
#include <sstream>
#include <Shlwapi.h>
#include <vector>
#include <wil\resource.h>
#include <wil\win32_helpers.h>
#include <wil\stl.h>
#include <wrl/module.h>
#include <wrl/implements.h>
#include <wrl/client.h>

#include "Generated Files/resource.h"

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <Helpers.h>
#include <Settings.h>
#include <trace.h>

#include <mutex>
#include <thread>
#include <shellapi.h>

using namespace Microsoft::WRL;

HINSTANCE g_hInst = 0;

#define BUFSIZE 4096 * 4

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst = hModule;
        Trace::RegisterProvider();
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

class __declspec(uuid("1861E28B-A1F0-4EF4-A1FE-4C8CA88E2174")) PowerRenameContextMenuCommand final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite>
{
public:
    virtual const wchar_t* Title() { return L"PowerRename"; }
    virtual const EXPCMDFLAGS Flags() { return ECF_DEFAULT; }
    virtual const EXPCMDSTATE State(_In_opt_ IShellItemArray* selection) { return ECS_ENABLED; }

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* name)
    {
        return SHStrDup(context_menu_caption.c_str(), name);
    }

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
    {
        if (!CSettingsInstance().GetShowIconOnMenu())
        {
            *icon = nullptr;
            return E_NOTIMPL;
        }

        std::wstring iconResourcePath = get_module_folderpath(g_hInst);
        iconResourcePath += L"\\Assets\\PowerRename\\";
        iconResourcePath += L"PowerRenameUI.ico";
        return SHStrDup(iconResourcePath.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* infoTip)
    {
        *infoTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(_Out_ GUID* guidCommandName)
    {
        *guidCommandName = __uuidof(this);
        return S_OK;
    }

    IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* selection, _In_ BOOL okToBeSlow, _Out_ EXPCMDSTATE* cmdState)
    {
        *cmdState = ECS_ENABLED;

        if (!CSettingsInstance().GetEnabled())
        {
            *cmdState = ECS_HIDDEN;
            return S_OK;
        }

        // Check if we should only be on the extended context menu
        if (CSettingsInstance().GetExtendedContextMenuOnly())
        {
            *cmdState = ECS_HIDDEN;
            return S_OK;
        }

        // When right clicking directory background, selection is empty. This prevents checking if there
        // are renamable items, but internal PowerRename logic will prevent renaming non-renamable items anyway.
        if (nullptr == selection) {
            return S_OK;
        }

        // Check if at least one of the selected items is actually renamable.
        if (!ShellItemArrayContainsRenamableItem(selection))
        {
            *cmdState = ECS_HIDDEN;
            return S_OK;
        }

        return S_OK;
    }

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*) noexcept
    try
    {
        if (selection)
        {
            RunPowerRename(selection);
        }

        return S_OK;
    }
    CATCH_RETURN();

    IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* flags)
    {
        *flags = Flags();
        return S_OK;
    }
    IFACEMETHODIMP EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** enumCommands)
    {
        *enumCommands = nullptr;
        return E_NOTIMPL;
    }

    // IObjectWithSite
    IFACEMETHODIMP SetSite(_In_ IUnknown* site) noexcept
    {
        m_site = site;
        return S_OK;
    }
    IFACEMETHODIMP GetSite(_In_ REFIID riid, _COM_Outptr_ void** site) noexcept { return m_site.CopyTo(riid, site); }

protected:
    ComPtr<IUnknown> m_site;

private:

    HRESULT StartNamedPipeServerAndSendData(std::wstring pipe_name)
    {
        hPipe = CreateNamedPipe(
            pipe_name.c_str(),
            PIPE_ACCESS_DUPLEX |
                WRITE_DAC,
            PIPE_TYPE_MESSAGE |
                PIPE_READMODE_MESSAGE |
                PIPE_WAIT,
            PIPE_UNLIMITED_INSTANCES,
            BUFSIZE,
            BUFSIZE,
            0,
            NULL);

        if (hPipe == NULL || hPipe == INVALID_HANDLE_VALUE)
        {
            return E_FAIL;
        }

        // This call blocks until a client process connects to the pipe
        BOOL connected = ConnectNamedPipe(hPipe, NULL);
        if (!connected)
        {
            if (GetLastError() == ERROR_PIPE_CONNECTED)
            {
                return S_OK;
            }
            else
            {
                CloseHandle(hPipe);
            }
            return E_FAIL;
        }

        return S_OK;
    }

    HRESULT RunPowerRename(IShellItemArray* psiItemArray)
    {
        if (CSettingsInstance().GetEnabled())
        {
            Trace::Invoked();
            // Set the application path based on the location of the dll
            std::wstring path = get_module_folderpath(g_hInst);
            path = path + L"\\PowerToys.PowerRename.exe";

            std::wstring pipe_name(L"\\\\.\\pipe\\powertoys_powerrenameinput_");
            UUID temp_uuid;
            wchar_t* uuid_chars = nullptr;
            if (UuidCreate(&temp_uuid) == RPC_S_UUID_NO_ADDRESS)
            {
                auto val = get_last_error_message(GetLastError());
                Logger::warn(L"UuidCreate can not create guid. {}", val.has_value() ? val.value() : L"");
            }
            else if (UuidToString(&temp_uuid, reinterpret_cast<RPC_WSTR*>(& uuid_chars)) != RPC_S_OK)
            {
                auto val = get_last_error_message(GetLastError());
                Logger::warn(L"UuidToString can not convert to string. {}", val.has_value() ? val.value() : L"");
            }

            if (uuid_chars != nullptr)
            {
                pipe_name += std::wstring(uuid_chars);
                RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));
                uuid_chars = nullptr;
            }
            create_pipe_thread = std::thread(&PowerRenameContextMenuCommand::StartNamedPipeServerAndSendData, this, pipe_name);
            RunNonElevatedEx(path.c_str(), pipe_name, get_module_folderpath(g_hInst));
            create_pipe_thread.join();

            if (hPipe != INVALID_HANDLE_VALUE)
            {
                CAtlFile writePipe(hPipe);

                DWORD fileCount = 0;
                // Gets the list of files currently selected using the IShellItemArray
                psiItemArray->GetCount(&fileCount);
                // Iterate over the list of files
                for (DWORD i = 0; i < fileCount; i++)
                {
                    IShellItem* shellItem;
                    psiItemArray->GetItemAt(i, &shellItem);
                    LPWSTR itemName;
                    // Retrieves the entire file system path of the file from its shell item
                    shellItem->GetDisplayName(SIGDN_FILESYSPATH, &itemName);
                    CString fileName(itemName);
                    // File name can't contain '?'
                    fileName.Append(_T("?"));
                    // Write the file path into the input stream for image resizer
                    writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
                }
                writePipe.Close();
            }
        }
        Trace::InvokedRet(S_OK);

        return S_OK;
    }


    std::thread create_pipe_thread;
    HANDLE hPipe = INVALID_HANDLE_VALUE;
    std::wstring context_menu_caption = GET_RESOURCE_STRING_FALLBACK(IDS_POWERRENAME_CONTEXT_MENU_ENTRY, L"Rename with PowerRename");
};

CoCreatableClass(PowerRenameContextMenuCommand)
CoCreatableClassWrlCreatorMapInclude(PowerRenameContextMenuCommand)

STDAPI DllGetActivationFactory(_In_ HSTRING activatableClassId, _COM_Outptr_ IActivationFactory** factory)
{
    return Module<ModuleType::InProc>::GetModule().GetActivationFactory(activatableClassId, factory);
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _COM_Outptr_ void** instance)
{
    return Module<InProc>::GetModule().GetClassObject(rclsid, riid, instance);
}