// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/elevation.h>

#include "FileLocksmithLib/IPC.h"
#include "FileLocksmithLib/Settings.h"
#include "FileLocksmithLib/Trace.h"

#include <Shlwapi.h>
#include <shobjidl_core.h>
#include <string>
#include <wrl/module.h>

#include "Generated Files/resource.h"


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

class __declspec(uuid("AAF1E27D-4976-49C2-8895-AAFA743C0A7E")) FileLocksmithContextMenuCommand final : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IExplorerCommand, IObjectWithSite>
{
public:
    virtual const wchar_t* Title() { return L"File Locksmith"; }
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
        iconResourcePath += L"\\Assets\\FileLocksmith\\";
        iconResourcePath += L"FileLocksmith.ico";
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

        if (!FileLocksmithSettingsInstance().GetEnabled())
        {
            *cmdState = ECS_HIDDEN;
        }

        if (FileLocksmithSettingsInstance().GetShowInExtendedContextMenu())
        {
            *cmdState = ECS_HIDDEN;
        }

        return S_OK;
    }

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* selection, _In_opt_ IBindCtx*) noexcept
    {
        Trace::Invoked();
        ipc::Writer writer;

        if (selection == nullptr)
        {
            return S_OK;
        }

        if (HRESULT result = writer.start(); FAILED(result))
        {
            Trace::InvokedRet(result);
            return result;
        }

        std::wstring path = get_module_folderpath(g_hInst);
        path = path + L"\\PowerToys.FileLocksmithUI.exe";

        HRESULT result;

        if (!RunNonElevatedEx(path.c_str(), L"", get_module_folderpath(g_hInst)))
        {
            result = E_FAIL;
            Trace::InvokedRet(result);
            return result;
        }

        DWORD num_items;
        selection->GetCount(&num_items);

        for (DWORD i = 0; i < num_items; i++)
        {
            IShellItem* item;
            result = selection->GetItemAt(i, &item);
            if (SUCCEEDED(result))
            {
                LPWSTR file_path;
                result = item->GetDisplayName(SIGDN_FILESYSPATH, &file_path);
                if (SUCCEEDED(result))
                {
                    // TODO Aggregate items and send to UI
                    writer.add_path(file_path);
                    CoTaskMemFree(file_path);
                }

                item->Release();
            }
        }

        Trace::InvokedRet(S_OK);
        return S_OK;
    }

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
    std::wstring context_menu_caption = GET_RESOURCE_STRING_FALLBACK(IDS_FILE_LOCKSMITH_CONTEXT_MENU_ENTRY, L"Unlock with File Locksmith");
};

CoCreatableClass(FileLocksmithContextMenuCommand)
CoCreatableClassWrlCreatorMapInclude(FileLocksmithContextMenuCommand)


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
