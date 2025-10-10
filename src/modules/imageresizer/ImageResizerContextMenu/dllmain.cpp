// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <atlfile.h>
#include <atlstr.h>
#include <Shlwapi.h>
#include <shobjidl_core.h>
#include <string>

#include <common/telemetry/EtwTrace/EtwTrace.h>
#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <Settings.h>
#include <trace.h>

#include <wil/win32_helpers.h>
#include <wrl/module.h>
#include "Generated Files/resource.h"

using namespace Microsoft::WRL;

HINSTANCE g_hInst = 0;
Shared::Trace::ETWTrace trace(L"ImageResizerContextMenu");

#define BUFSIZE 4096 * 4

BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD ul_reason_for_call,
                      LPVOID lpReserved)
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

class __declspec(uuid("8F491918-259F-451A-950F-8C3EBF4864AF")) ImageResizerContextMenuCommand final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite>
{
public:
    virtual const wchar_t* Title() { return L"Image Resizer"; }
    virtual const EXPCMDFLAGS Flags() { return ECF_DEFAULT; }
    virtual const EXPCMDSTATE State(_In_opt_ IShellItemArray* selection) { return ECS_ENABLED; }

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray* items, _Outptr_result_nullonfailure_ PWSTR* name)
    {
        return SHStrDup(context_menu_caption.c_str(), name);
    }

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray*, _Outptr_result_nullonfailure_ PWSTR* icon)
    {
        std::wstring iconResourcePath = get_module_folderpath(g_hInst);
        iconResourcePath += L"\\Assets\\ImageResizer\\";
        iconResourcePath += L"ImageResizer.ico";
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
        if (nullptr == selection)
        {
            // We've observed that it's possible that a null gets passed instead of an empty array. Just don't show the context menu in this case.
            *cmdState = ECS_HIDDEN;
            return S_OK;
        }

        if (!CSettingsInstance().GetEnabled())
        {
            *cmdState = ECS_HIDDEN;
            return S_OK;
        }
        // Hide if the file is not an image
        *cmdState = ECS_HIDDEN;
        // Suppressing C26812 warning as the issue is in the shtypes.h library
#pragma warning(suppress : 26812)
        PERCEIVED type;
        PERCEIVEDFLAG flag;
        IShellItem* shellItem = nullptr;
        //Check extension of first item in the list (the item which is right-clicked on)
        HRESULT getItemResult = selection->GetItemAt(0, &shellItem);
        if (S_OK != getItemResult || nullptr == shellItem)
        {
            // Some safeguards to avoid runtime errors.
            *cmdState = ECS_HIDDEN;
            return S_OK;
        }
        LPTSTR pszPath;
        // Retrieves the entire file system path of the file from its shell item
        HRESULT getDisplayResult = shellItem->GetDisplayName(SIGDN_FILESYSPATH, &pszPath);
        if (S_OK != getDisplayResult || nullptr == pszPath)
        {
            // Avoid crashes in the following code.
            return E_FAIL;
        }

        LPTSTR pszExt = PathFindExtension(pszPath);
        if (nullptr == pszExt)
        {
            CoTaskMemFree(pszPath);
            // Avoid crashes in the following code.
            return E_FAIL;
        }

        // TODO: Instead, detect whether there's a WIC codec installed that can handle this file
        AssocGetPerceivedType(pszExt, &type, &flag, NULL);

        CoTaskMemFree(pszPath);
        // If selected file is an image...

        if (type == PERCEIVED_TYPE_IMAGE)
        {
            *cmdState = ECS_ENABLED;
        }
        return S_OK;
    }

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*) noexcept
    try
    {
        trace.UpdateState(true);

        Trace::Invoked();
        HRESULT hr = S_OK;

        if (selection)
        {
            hr = ResizePictures(selection);
        }

        Trace::InvokedRet(hr);

        trace.UpdateState(false);
        trace.Flush();

        return hr;
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

    HRESULT ResizePictures(IShellItemArray* psiItemArray)
    {
        // Set the application path based on the location of the dll
        std::wstring path = get_module_folderpath(g_hInst);
        path = path + L"\\PowerToys.ImageResizer.exe";

        std::wstring pipe_name(L"\\\\.\\pipe\\powertoys_imageresizerinput_");
        UUID temp_uuid;
        wchar_t* uuid_chars = nullptr;
        if (UuidCreate(&temp_uuid) == RPC_S_UUID_NO_ADDRESS)
        {
            auto val = get_last_error_message(GetLastError());
            Logger::warn(L"UuidCreate cannot create guid. {}", val.has_value() ? val.value() : L"");
        }
        else if (UuidToString(&temp_uuid, reinterpret_cast<RPC_WSTR*>(&uuid_chars)) != RPC_S_OK)
        {
            auto val = get_last_error_message(GetLastError());
            Logger::warn(L"UuidToString cannot convert to string. {}", val.has_value() ? val.value() : L"");
        }

        if (uuid_chars != nullptr)
        {
            pipe_name += std::wstring(uuid_chars);
            RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));
            uuid_chars = nullptr;
        }
        create_pipe_thread = std::thread(&ImageResizerContextMenuCommand::StartNamedPipeServerAndSendData, this, pipe_name);

        std::wstring aumidTarget =
            L"shell:AppsFolder\\Microsoft.PowerToys.SparseApp_djwsxzxb4ksa8!PowerToys.ImageResizerUI"; // PFN!AppId
        std::wstring args = L"--pipe \"" + pipe_name + L"\"";

        // Use ShellExecuteEx so you get a process handle:
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
        sei.lpVerb = L"open";
        sei.lpFile = aumidTarget.c_str();
        sei.lpParameters = args.c_str();
        sei.nShow = SW_SHOWNORMAL;

        if (ShellExecuteExW(&sei))
        {
            Logger::trace(L"Started ImageResizer (sparse) with pipe {}", pipe_name);
        }
        else
        {
            Logger::error(L"Failed to start ImageResizer (sparse). {}", get_last_error_or_default(GetLastError()));
        }
        // RunNonElevatedEx(path.c_str(), pipe_name, get_module_folderpath(g_hInst));

        create_pipe_thread.join();

        if (hPipe != INVALID_HANDLE_VALUE)
        {
            CAtlFile writePipe(hPipe);

            //m_pdtobj will be NULL when invoked from the MSIX build as Initialize is never called (IShellExtInit functions aren't called in case of MSIX).
            DWORD fileCount = 0;
            // Gets the list of files currently selected using the IShellItemArray
            psiItemArray->GetCount(&fileCount);
            // Iterate over the list of files
            for (DWORD i = 0; i < fileCount; i++)
            {
                IShellItem* shellItem;
                HRESULT getItemAtResult = psiItemArray->GetItemAt(i, &shellItem);
                if (SUCCEEDED(getItemAtResult))
                {
                    LPWSTR itemName;
                    // Retrieves the entire file system path of the file from its shell item
                    HRESULT getDisplayResult = shellItem->GetDisplayName(SIGDN_FILESYSPATH, &itemName);
                    if (SUCCEEDED(getDisplayResult))
                    {
                        CString fileName(itemName);
                        fileName.Append(_T("\r\n"));
                        // Write the file path into the input stream for image resizer
                        writePipe.Write(fileName, fileName.GetLength() * sizeof(TCHAR));
                    }
                }
            }
            writePipe.Close();
        }

        return S_OK;
    }

    std::thread create_pipe_thread;
    HANDLE hPipe = INVALID_HANDLE_VALUE;
    std::wstring context_menu_caption = GET_RESOURCE_STRING_FALLBACK(IDS_IMAGERESIZER_CONTEXT_MENU_ENTRY, L"Resize with Image Resizer");
};

CoCreatableClass(ImageResizerContextMenuCommand)
    CoCreatableClassWrlCreatorMapInclude(ImageResizerContextMenuCommand)

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
