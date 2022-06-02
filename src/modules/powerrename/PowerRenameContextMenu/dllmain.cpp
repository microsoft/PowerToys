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

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <Settings.h>
#include <trace.h>

#include <mutex>
#include <shellapi.h>

using namespace Microsoft::WRL;

HINSTANCE g_hInst = 0;

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
        return SHStrDup(app_name.c_str(), name);
    }

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
    {
        if (!CSettingsInstance().GetShowIconOnMenu())
        {
            *icon = nullptr;
            return E_NOTIMPL;
        }

        std::wstring iconResourcePath = get_module_folderpath(g_hInst);
        iconResourcePath += L"\\";
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
        *cmdState = CSettingsInstance().GetEnabled() ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*) noexcept
    try
    {
        HWND parent = nullptr;
        if (m_site)
        {
            ComPtr<IOleWindow> oleWindow;
            RETURN_IF_FAILED(m_site.As(&oleWindow));
            RETURN_IF_FAILED(oleWindow->GetWindow(&parent));
        }

        std::wostringstream title;
        title << Title();

        if (selection)
        {
            DWORD count;
            RETURN_IF_FAILED(selection->GetCount(&count));
            title << L" (" << count << L" selected items)";
        }
        else
        {
            title << L"(no selected items)";
        }
        std::filesystem::path modulePath{ wil::GetModuleFileNameW<std::wstring>() };
        std::wstring path = get_module_folderpath(g_hInst);
        path = path + L"\\PowerToys.PowerRename.exe";
        std::wstring iconResourcePath = get_module_filename();

        MessageBox(parent, iconResourcePath.c_str(), iconResourcePath.c_str(), MB_OK);
        RunPowerRename(selection);
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
    HRESULT RunPowerRename(IShellItemArray* psiItemArray)
    {
        HRESULT hr = E_FAIL;
        HWND parent = nullptr;
        if (m_site)
        {
            ComPtr<IOleWindow> oleWindow;
            RETURN_IF_FAILED(m_site.As(&oleWindow));
            RETURN_IF_FAILED(oleWindow->GetWindow(&parent));
        }


        if (CSettingsInstance().GetEnabled())
        {
            Trace::Invoked();
            // Set the application path based on the location of the dll
            std::wstring path = get_module_folderpath(g_hInst);
            path = path + L"\\PowerToys.PowerRename.exe";
            LPTSTR lpApplicationName = (LPTSTR)path.c_str();
            // Create an anonymous pipe to stream filenames
            SECURITY_ATTRIBUTES sa;
            HANDLE hReadPipe;
            HANDLE hWritePipe;
            sa.nLength = sizeof(SECURITY_ATTRIBUTES);
            sa.lpSecurityDescriptor = NULL;
            sa.bInheritHandle = TRUE;
            if (!CreatePipe(&hReadPipe, &hWritePipe, &sa, 0))
            {
                hr = HRESULT_FROM_WIN32(GetLastError());
                return hr;
            }
            if (!SetHandleInformation(hWritePipe, HANDLE_FLAG_INHERIT, 0))
            {
                hr = HRESULT_FROM_WIN32(GetLastError());
                return hr;
            }
            CAtlFile writePipe(hWritePipe);

            CString commandLine;
            commandLine.Format(_T("\"%s\""), lpApplicationName);
            int nSize = commandLine.GetLength() + 1;
            LPTSTR lpszCommandLine = new TCHAR[nSize];
            _tcscpy_s(lpszCommandLine, nSize, commandLine);

            STARTUPINFO startupInfo;
            ZeroMemory(&startupInfo, sizeof(STARTUPINFO));
            startupInfo.cb = sizeof(STARTUPINFO);
            startupInfo.hStdInput = hReadPipe;
            startupInfo.dwFlags = STARTF_USESHOWWINDOW | STARTF_USESTDHANDLES;
            startupInfo.wShowWindow = SW_SHOWNORMAL;

            PROCESS_INFORMATION processInformation;

            // Start the resizer
            CreateProcess(
                NULL,
                lpszCommandLine,
                NULL,
                NULL,
                TRUE,
                0,
                NULL,
                NULL,
                &startupInfo,
                &processInformation);

            RunNonElevatedEx(path.c_str(), {}, get_module_folderpath(g_hInst));

            delete[] lpszCommandLine;
            if (!CloseHandle(processInformation.hProcess))
            {
                hr = HRESULT_FROM_WIN32(GetLastError());
                return hr;
            }
            if (!CloseHandle(processInformation.hThread))
            {
                hr = HRESULT_FROM_WIN32(GetLastError());
                return hr;
            }

            //m_pdtobj will be NULL when invoked from the MSIX build as Initialize is never called (IShellExtInit functions aren't called in case of MSIX).
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
        Trace::InvokedRet(hr);

        return hr;
    }

    std::wstring app_name = L"PowerRename";
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