// Header-only runtime registration for FileLocksmith context menu extension.
#pragma once

#include <common/utils/shell_ext_registration.h>

namespace globals { extern HMODULE instance; }

namespace FileLocksmithRuntimeRegistration
{
    namespace
    {
        inline runtime_shell_ext::Spec BuildSpec()
        {
            runtime_shell_ext::Spec spec;
            spec.clsid = L"{84D68575-E186-46AD-B0CB-BAEB45EE29C0}";
            spec.sentinelKey = L"Software\\Microsoft\\PowerToys\\FileLocksmith";
            spec.sentinelValue = L"ContextMenuRegistered";
            spec.dllFileCandidates = { L"PowerToys.FileLocksmithExt.dll" };
            spec.contextMenuHandlerKeyPaths = {
                L"Software\\Classes\\AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\FileLocksmithExt",
                L"Software\\Classes\\Drive\\ShellEx\\ContextMenuHandlers\\FileLocksmithExt" };
            spec.friendlyName = L"File Locksmith Shell Extension";
            return spec;
        }
    }

    inline bool EnsureRegistered()
    {
        return runtime_shell_ext::EnsureRegistered(BuildSpec(), globals::instance);
    }

    inline void Unregister()
    {
        runtime_shell_ext::Unregister(BuildSpec());
    }
}
