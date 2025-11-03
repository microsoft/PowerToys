// Header-only runtime registration for PowerRename context menu extension.
#pragma once

#include <common/utils/shell_ext_registration.h>

// Provided by dllmain.cpp
extern HINSTANCE g_hInst;

namespace PowerRenameRuntimeRegistration
{
    namespace
    {
        inline runtime_shell_ext::Spec BuildSpec()
        {
            runtime_shell_ext::Spec spec;
            spec.clsid = L"{0440049F-D1DC-4E46-B27B-98393D79486B}";
            spec.sentinelKey = L"Software\\Microsoft\\PowerToys\\PowerRename";
            spec.sentinelValue = L"ContextMenuRegistered";
            spec.dllFileCandidates = { L"PowerToys.PowerRenameExt.dll" };
            spec.contextMenuHandlerKeyPaths = {
                L"Software\\Classes\\AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\PowerRenameExt",
                L"Software\\Classes\\Directory\\background\\ShellEx\\ContextMenuHandlers\\PowerRenameExt" };
            spec.friendlyName = L"PowerRename Shell Extension";
            return spec;
        }
    }

    inline bool EnsureRegistered()
    {
        return runtime_shell_ext::EnsureRegistered(BuildSpec(), g_hInst);
    }

    inline void Unregister()
    {
        runtime_shell_ext::Unregister(BuildSpec());
    }
}
