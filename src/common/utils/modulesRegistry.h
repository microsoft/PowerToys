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
    const static std::vector<std::wstring> ExtSTL      = { L".stl" };
    const static std::vector<std::wstring> ExtQOI      = { L".qoi" };
    const static std::vector<std::wstring> ExtNoNoNo   = {
        L".svgz" //Monaco cannot handle this file type at all; it's a binary file.
    };
}

inline registry::ChangeSet getSvgPreviewHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{FCDD4EED-41AA-492F-8A84-31A1546226E0}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } /
                                   LR"d(PowerToys.SvgPreviewHandlerCpp.dll)d")
                                      .wstring(),
                                  L"SvgPreviewHandler",
                                  L"Svg Preview Handler",
                                  NonLocalizable::ExtSVG);
}

inline registry::ChangeSet getMdPreviewHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{60789D87-9C3C-44AF-B18C-3DE2C2820ED3}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.MarkdownPreviewHandlerCpp.dll)d").wstring(),
                                  L"MarkdownPreviewHandler",
                                  L"Markdown Preview Handler",
                                  NonLocalizable::ExtMarkdown);
}

inline registry::ChangeSet getMonacoPreviewHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;

    // Set up a list of extensions for the preview handler to take over
    std::vector<std::wstring> extensions;

    // Set up a list of extensions that Monaco support but the preview handler shouldn't take over
    std::vector<std::wstring> ExtExclusions;
    ExtExclusions.insert(ExtExclusions.end(), NonLocalizable::ExtMarkdown.begin(), NonLocalizable::ExtMarkdown.end());
    ExtExclusions.insert(ExtExclusions.end(), NonLocalizable::ExtSVG.begin(), NonLocalizable::ExtSVG.end());
    ExtExclusions.insert(ExtExclusions.end(), NonLocalizable::ExtNoNoNo.begin(), NonLocalizable::ExtNoNoNo.end());
    bool IsExcluded = false;

    std::wstring languagesFilePath = fs::path{ installationDir } / NonLocalizable::MONACO_LANGUAGES_FILE_NAME;
    auto json = json::from_file(languagesFilePath);

    if (json)
    {
        try
        {
            auto list = json->GetNamedArray(NonLocalizable::ListID);
            for (uint32_t i = 0; i < list.Size(); ++i)
            {
                auto entry = list.GetObjectAt(i);
                if (entry.HasKey(NonLocalizable::ExtensionsID))
                {
                    auto extensionsList = entry.GetNamedArray(NonLocalizable::ExtensionsID);

                    for (uint32_t j = 0; j < extensionsList.Size(); ++j)
                    {
                        auto extension = extensionsList.GetStringAt(j);

                        // Ignore extensions in the exclusion list
                        IsExcluded = false;

                        for (std::wstring k : ExtExclusions)
                        {
                            if (std::wstring{ extension } == k)
                            {
                                IsExcluded = true;
                                break;
                            }
                        }
                        if (IsExcluded)
                        {
                            continue;
                        }
                        extensions.push_back(std::wstring{ extension });
                    }
                }
            }
        }
        catch (...)
        {
        }
    }

    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{D8034CFA-F34B-41FE-AD45-62FCBB52A6DA}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.MonacoPreviewHandlerCpp.dll)d").wstring(),
                                  L"MonacoPreviewHandler",
                                  L"Monaco Preview Handler",
                                  extensions);
}

inline registry::ChangeSet getPdfPreviewHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{A5A41CC7-02CB-41D4-8C9B-9087040D6098}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.PdfPreviewHandlerCpp.dll)d").wstring(),
                                  L"PdfPreviewHandler",
                                  L"Pdf Preview Handler",
                                  NonLocalizable::ExtPDF);
}

inline registry::ChangeSet getGcodePreviewHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{A0257634-8812-4CE8-AF11-FA69ACAEAFAE}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.GcodePreviewHandlerCpp.dll)d").wstring(),
                                  L"GcodePreviewHandler",
                                  L"G-code Preview Handler",
                                  NonLocalizable::ExtGCode);
}

inline registry::ChangeSet getQoiPreviewHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{729B72CD-B72E-4FE9-BCBF-E954B33FE699}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.QoiPreviewHandlerCpp.dll)d").wstring(),
                                  L"QoiPreviewHandler",
                                  L"Qoi Preview Handler",
                                  NonLocalizable::ExtQOI);
}

inline registry::ChangeSet getSvgThumbnailHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::thumbnail,
                                  perUser,
                                  L"{10144713-1526-46C9-88DA-1FB52807A9FF}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.SvgThumbnailProviderCpp.dll)d").wstring(),
                                  L"SvgThumbnailProvider",
                                  L"Svg Thumbnail Provider",
                                  NonLocalizable::ExtSVG,
                                  L"image",
                                  L"Picture");
}

inline registry::ChangeSet getPdfThumbnailHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::thumbnail,
                                  perUser,
                                  L"{D8BB9942-93BD-412D-87E4-33FAB214DC1A}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.PdfThumbnailProviderCpp.dll)d").wstring(),
                                  L"PdfThumbnailProvider",
                                  L"Pdf Thumbnail Provider",
                                  NonLocalizable::ExtPDF);
}

inline registry::ChangeSet getGcodeThumbnailHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::thumbnail,
                                  perUser,
                                  L"{F2847CBE-CD03-4C83-A359-1A8052C1B9D5}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.GcodeThumbnailProviderCpp.dll)d").wstring(),
                                  L"GcodeThumbnailProvider",
                                  L"G-code Thumbnail Provider",
                                  NonLocalizable::ExtGCode);
}

inline registry::ChangeSet getStlThumbnailHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::thumbnail,
                                  perUser,
                                  L"{77257004-6F25-4521-B602-50ECC6EC62A6}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.StlThumbnailProviderCpp.dll)d").wstring(),
                                  L"StlThumbnailProvider",
                                  L"Stl Thumbnail Provider",
                                  NonLocalizable::ExtSTL);
}

inline registry::ChangeSet getQoiThumbnailHandlerChangeSet(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::thumbnail,
                                  perUser,
                                  L"{AD856B15-D25E-4008-AFB7-AFAA55586188}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(PowerToys.QoiThumbnailProviderCpp.dll)d").wstring(),
                                  L"QoiThumbnailProvider",
                                  L"Qoi Thumbnail Provider",
                                  NonLocalizable::ExtQOI,
                                  L"image",
                                  L"Picture");
}

inline registry::ChangeSet getRegistryPreviewSetDefaultAppChangeSet(const std::wstring installationDir, const bool perUser)
{
    const HKEY scope = perUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;

    using vec_t = std::vector<registry::ValueChange>;
    vec_t changes;

    std::wstring appName = L"Registry Preview";
    std::wstring fullAppName = L"PowerToys.RegistryPreview";
    std::wstring registryKeyPrefix = L"Software\\Classes\\";

    std::wstring appPath = installationDir + L"\\WinUI3Apps\\PowerToys.RegistryPreview.exe";
    std::wstring command = appPath + L" \"----ms-protocol:ms-encodedlaunch:App?ContractId=Windows.File&Verb=open&File=%1\"";

    changes.push_back({ scope, registryKeyPrefix + fullAppName + L"\\" + L"Application", L"ApplicationName", appName });
    changes.push_back({ scope, registryKeyPrefix + fullAppName + L"\\" + L"DefaultIcon", std::nullopt, appPath });
    changes.push_back({ scope, registryKeyPrefix + fullAppName + L"\\" + L"shell\\open\\command", std::nullopt, command });
    changes.push_back({ scope, registryKeyPrefix + L".reg\\OpenWithProgIDs", fullAppName, L"" });

    return { changes };
}

inline registry::ChangeSet getRegistryPreviewChangeSet(const std::wstring installationDir,const bool perUser)
{
    const HKEY scope = perUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;

    using vec_t = std::vector<registry::ValueChange>;
    vec_t changes;

    std::wstring command = installationDir;
    command.append(L"\\WinUI3Apps\\PowerToys.RegistryPreview.exe \"%1\"");
    changes.push_back({ scope, L"Software\\Classes\\regfile\\shell\\preview\\command", std::nullopt, command });

    std::wstring icon_path = installationDir;
    icon_path.append(L"\\WinUI3Apps\\Assets\\RegistryPreview\\RegistryPreview.ico");
    changes.push_back({ scope, L"Software\\Classes\\regfile\\shell\\preview", L"icon", icon_path });

    return { changes };
}

inline std::vector<registry::ChangeSet> getAllOnByDefaultModulesChangeSets(const std::wstring installationDir)
{
    constexpr bool PER_USER = true;
    return { getSvgPreviewHandlerChangeSet(installationDir, PER_USER),
             getMdPreviewHandlerChangeSet(installationDir, PER_USER),
             getMonacoPreviewHandlerChangeSet(installationDir, PER_USER),
             getGcodePreviewHandlerChangeSet(installationDir, PER_USER),
             getQoiPreviewHandlerChangeSet(installationDir, PER_USER),
             getSvgThumbnailHandlerChangeSet(installationDir, PER_USER),
             getGcodeThumbnailHandlerChangeSet(installationDir, PER_USER),
             getStlThumbnailHandlerChangeSet(installationDir, PER_USER),
             getQoiThumbnailHandlerChangeSet(installationDir, PER_USER),
             getRegistryPreviewChangeSet(installationDir, PER_USER) };
}

inline std::vector<registry::ChangeSet> getAllModulesChangeSets(const std::wstring installationDir)
{
    constexpr bool PER_USER = true;
    return { getSvgPreviewHandlerChangeSet(installationDir, PER_USER),
             getMdPreviewHandlerChangeSet(installationDir, PER_USER),
             getMonacoPreviewHandlerChangeSet(installationDir, PER_USER),
             getPdfPreviewHandlerChangeSet(installationDir, PER_USER),
             getGcodePreviewHandlerChangeSet(installationDir, PER_USER),
             getQoiPreviewHandlerChangeSet(installationDir, PER_USER),
             getSvgThumbnailHandlerChangeSet(installationDir, PER_USER),
             getPdfThumbnailHandlerChangeSet(installationDir, PER_USER),
             getGcodeThumbnailHandlerChangeSet(installationDir, PER_USER),
             getStlThumbnailHandlerChangeSet(installationDir, PER_USER),
             getQoiThumbnailHandlerChangeSet(installationDir, PER_USER),
             getRegistryPreviewChangeSet(installationDir, PER_USER),
             getRegistryPreviewSetDefaultAppChangeSet(installationDir, PER_USER) };
}
