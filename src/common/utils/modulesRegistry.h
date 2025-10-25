#pragma once

#include "registry.h"

#include <common/utils/json.h>

#include <filesystem>

namespace fs = std::filesystem;

namespace NonLocalizable
{
    const static wchar_t* MONACO_LANGUAGES_FILE_NAME = L"Assets\\Monaco\\monaco_languages.json";
    const static wchar_t* ListID = L"list";
    const static wchar_t* ExtensionsID = L"extensions";
    const static std::vector<std::wstring> ExtSVG      = { L".svg" };
    const static std::vector<std::wstring> ExtMarkdown = { L".md", L".markdown", L".mdown", L".mkdn", L".mkd", L".mdwn", L".mdtxt", L".mdtext" };
    const static std::vector<std::wstring> ExtPDF      = { L".pdf" };
    const static std::vector<std::wstring> ExtGCode    = { L".gcode" };
    const static std::vector<std::wstring> ExtBGCode   = { L".bgcode" };
    const static std::vector<std::wstring> ExtSTL      = { L".stl" };
    const static std::vector<std::wstring> ExtQOI      = { L".qoi" };
    const static std::vector<std::wstring> ExtNoNoNo   = {
        L".svgz" //Monaco cannot handle this file type at all; it's a binary file.
    };
}

registry::ChangeSet getSvgPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getMdPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getMonacoPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getPdfPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getGcodePreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getBgcodePreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getQoiPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getSvgThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getPdfThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getGcodeThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getBgcodeThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getStlThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getQoiThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getRegistryPreviewSetDefaultAppChangeSet(const std::wstring& installationDir, bool perUser);

registry::ChangeSet getRegistryPreviewChangeSet(const std::wstring& installationDir, bool perUser);

std::vector<registry::ChangeSet> getAllOnByDefaultModulesChangeSets(const std::wstring& installationDir);

std::vector<registry::ChangeSet> getAllModulesChangeSets(const std::wstring& installationDir);
