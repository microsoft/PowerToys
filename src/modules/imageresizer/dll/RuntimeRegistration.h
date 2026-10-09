// Header-only runtime registration for ImageResizer shell extension.
#pragma once

#include <common/utils/shell_ext_registration.h>

extern "C" IMAGE_DOS_HEADER __ImageBase; // provided by linker

namespace ImageResizerRuntimeRegistration
{
    namespace
    {
        inline runtime_shell_ext::Spec BuildSpec()
        {
            runtime_shell_ext::Spec spec;
            spec.clsid = L"{51B4D7E5-7568-4234-B4BB-47FB3C016A69}";
            spec.sentinelKey = L"Software\\Microsoft\\PowerToys\\ImageResizer";
            spec.sentinelValue = L"ContextMenuRegistered";
            spec.dllFileCandidates = { L"PowerToys.ImageResizerExt.dll" };
            spec.contextMenuHandlerKeyPaths = { };
            spec.systemFileAssocHandlerName = L"ImageResizer";
            spec.systemFileAssocExtensions = { L".bmp", L".dib", L".gif", L".jfif", L".jpe", L".jpeg", L".jpg", L".jxr", L".png", L".rle", L".tif", L".tiff", L".wdp" };
            spec.representativeSystemExt = L".png"; // probe for repair
            spec.extraAssociationPaths = { L"Software\\Classes\\Directory\\ShellEx\\DragDropHandlers\\ImageResizer" };
            spec.friendlyName = L"ImageResizer Shell Extension";
            return spec;
        }
    }

    inline bool EnsureRegistered()
    {
        return runtime_shell_ext::EnsureRegistered(BuildSpec(), reinterpret_cast<HMODULE>(&__ImageBase));
    }

    inline void Unregister()
    {
        runtime_shell_ext::Unregister(BuildSpec());
    }
}
