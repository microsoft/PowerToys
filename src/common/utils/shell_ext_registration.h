#pragma once

#include <string>
#include <vector>
#include <windows.h>
#include <shlwapi.h>

namespace runtime_shell_ext
{
    struct Spec
    {
        // Mandatory
        std::wstring clsid; // e.g. {GUID}
        std::wstring sentinelKey; // e.g. Software\\Microsoft\\PowerToys\\ModuleName
        std::wstring sentinelValue; // e.g. ContextMenuRegistered
        std::vector<std::wstring> dllFileCandidates; // relative filenames (pick first existing)
        std::vector<std::wstring> contextMenuHandlerKeyPaths; // full HKCU relative paths where default value = CLSID

        // Optional
        std::wstring friendlyName; // if non-empty written as default under CLSID root
        bool writeOptInEmptyValue = true; // write ContextMenuOptIn="" under CLSID root (legacy pattern)
        bool writeThreadingModel = true; // write Apartment threading model
        std::vector<std::wstring> extraAssociationPaths; // additional key paths (DragDropHandlers etc.) default=CLSID
        std::vector<std::wstring> systemFileAssocExtensions; // e.g. .png -> Software\\Classes\\SystemFileAssociations\\.png\\ShellEx\\ContextMenuHandlers\\<HandlerName>
        std::wstring systemFileAssocHandlerName; // e.g. ImageResizer
        std::wstring representativeSystemExt; // used to decide if associations need repair (.png)
        bool logRepairs = true;
    };

    bool EnsureRegistered(const Spec& spec, HMODULE moduleInstance);
}
