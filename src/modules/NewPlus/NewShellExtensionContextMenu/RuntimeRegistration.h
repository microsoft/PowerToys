// Header-only runtime registration for New+ Win10 context menu.
#pragma once

#include <windows.h>
#include <string>
#include <common/utils/shell_ext_registration.h>

// Provided by dll_main.cpp
extern HMODULE module_instance_handle;

namespace NewPlusRuntimeRegistration
{
    namespace {
        inline runtime_shell_ext::Spec BuildSpec()
        {
            runtime_shell_ext::Spec spec;
            spec.clsid = L"{FF90D477-E32A-4BE8-8CC5-A502A97F5401}";
            spec.sentinelKey = L"Software\\Microsoft\\PowerToys\\NewPlus";
            spec.sentinelValue = L"ContextMenuRegisteredWin10";
            spec.dllFileCandidates = { L"PowerToys.NewPlus.ShellExtension.win10.dll" };
            spec.contextMenuHandlerKeyPaths = { L"Software\\Classes\\Directory\\background\\ShellEx\\ContextMenuHandlers\\NewPlusShellExtensionWin10" };
            spec.friendlyName = L"NewPlus Shell Extension Win10";
            return spec;
        }
    }

    inline bool EnsureRegisteredWin10()
    {
        return runtime_shell_ext::EnsureRegistered(BuildSpec(), module_instance_handle);
    }

    inline void Unregister()
    {
        runtime_shell_ext::Unregister(BuildSpec());
    }
}
