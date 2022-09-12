#include "pch.h"

#include "Registry.h"
#include "ExplorerCommand.h"
#include "Constants.h"
#include "dllmain.h"

#define HKEY_ROOT HKEY_CURRENT_USER

bool registry_write_string(LPCWSTR path, LPCWSTR property, LPCWSTR value)
{
    HKEY key;
    LSTATUS result = RegCreateKeyExW(
        HKEY_ROOT,
        path,
        0,
        NULL,
        REG_OPTION_NON_VOLATILE,
        KEY_ALL_ACCESS,
        NULL,
        &key,
        NULL
    );

    if (result != ERROR_SUCCESS)
    {
        return false;
    }

    if (value != NULL)
    {
        result = RegSetValueExW(key, property, 0, REG_SZ, reinterpret_cast<const BYTE*>(value), sizeof(WCHAR) * (1ull + lstrlenW(value)));
    }

    RegCloseKey(key);
    return result == ERROR_SUCCESS;
}

bool registry_delete_tree(LPCWSTR path)
{
    LSTATUS result = RegDeleteTreeW(HKEY_ROOT, path);
    return result == ERROR_SUCCESS;
}

bool add_registry_keys()
{
    if (!registry_write_string(
        L"Software\\Classes\\CLSID\\{" EXPLORER_COMMAND_UUID_STR L"}",
        NULL,
        constants::nonlocalizable::RegistryKeyDescription
    ))
    {
        return false;
    }

    if (!registry_write_string(
        L"Software\\Classes\\CLSID\\{" EXPLORER_COMMAND_UUID_STR L"}",
        L"ContextMenuOptIn",
        L""
    ))
    {
        return false;
    }

    static WCHAR module_file_name[MAX_PATH];
    DWORD result = GetModuleFileNameW(dll_instance, module_file_name, ARRAYSIZE(module_file_name));
    if (result == 0)
    {
        return false;
    }

    if (!registry_write_string(
        L"Software\\Classes\\CLSID\\{" EXPLORER_COMMAND_UUID_STR L"}\\InprocServer32",
        NULL,
        module_file_name
    ))
    {
        return false;
    }

    if (!registry_write_string(
        L"Software\\Classes\\CLSID\\{" EXPLORER_COMMAND_UUID_STR L"}\\InprocServer32",
        L"ThreadingModel",
        L"Apartment"
    ))
    {
        return false;
    }

    if (!registry_write_string(
        L"Software\\Classes\\AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\" REGISTRY_CONTEXT_MENU_KEY,
        L"",
        L"{" EXPLORER_COMMAND_UUID_STR L"}"
    ))
    {
        return false;
    }

    return true;
}

bool delete_registry_keys()
{
    bool ok = true;
    ok &= registry_delete_tree(
        L"Software\\Classes\\CLSID\\{" EXPLORER_COMMAND_UUID_STR "}"
    );

    ok &= registry_delete_tree(
        L"Software\\Classes\\AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\" REGISTRY_CONTEXT_MENU_KEY
    );

    return ok;
}
