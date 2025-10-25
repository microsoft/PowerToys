#include "pch.h"
#include "modulesRegistry.h"

#include <utility>
namespace
{
    registry::ChangeSet createPreviewChangeSet(registry::shellex::PreviewHandlerType type,
                                               bool perUser,
                                               const wchar_t* clsid,
                                               std::wstring dllName,
                                               std::wstring handlerClsid,
                                               std::wstring handlerDisplayName,
                                               std::vector<std::wstring> extensions,
                                               std::wstring perceivedType = L"",
                                               std::wstring fileKindType = L"")
    {
        using namespace registry::shellex;

        return generatePreviewHandler(type,
                                      perUser,
                                      clsid,
                                      get_std_product_version(),
                                      std::move(dllName),
                                      std::move(handlerClsid),
                                      std::move(handlerDisplayName),
                                      std::move(extensions),
                                      std::move(perceivedType),
                                      std::move(fileKindType));
    }
}

registry::ChangeSet getSvgPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::preview,
        perUser,
        L"{FCDD4EED-41AA-492F-8A84-31A1546226E0}",
        (fs::path{ installationDir } / LR"d(PowerToys.SvgPreviewHandlerCpp.dll)d").wstring(),
        L"SvgPreviewHandler",
        L"Svg Preview Handler",
        NonLocalizable::ExtSVG);
}

registry::ChangeSet getMdPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::preview,
        perUser,
        L"{60789D87-9C3C-44AF-B18C-3DE2C2820ED3}",
        (fs::path{ installationDir } / LR"d(PowerToys.MarkdownPreviewHandlerCpp.dll)d").wstring(),
        L"MarkdownPreviewHandler",
        L"Markdown Preview Handler",
        NonLocalizable::ExtMarkdown);
}

registry::ChangeSet getMonacoPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    std::vector<std::wstring> extensions;

    std::vector<std::wstring> exclusions;
    exclusions.insert(exclusions.end(), NonLocalizable::ExtMarkdown.begin(), NonLocalizable::ExtMarkdown.end());
    exclusions.insert(exclusions.end(), NonLocalizable::ExtSVG.begin(), NonLocalizable::ExtSVG.end());
    exclusions.insert(exclusions.end(), NonLocalizable::ExtNoNoNo.begin(), NonLocalizable::ExtNoNoNo.end());

    std::wstring languagesFilePath = fs::path{ installationDir } / NonLocalizable::MONACO_LANGUAGES_FILE_NAME;
    auto jsonValue = json::from_file(languagesFilePath);

    if (jsonValue)
    {
        try
        {
            auto list = jsonValue->GetNamedArray(NonLocalizable::ListID);
            for (uint32_t i = 0; i < list.Size(); ++i)
            {
                auto entry = list.GetObjectAt(i);
                if (entry.HasKey(NonLocalizable::ExtensionsID))
                {
                    auto extensionsList = entry.GetNamedArray(NonLocalizable::ExtensionsID);

                    for (uint32_t j = 0; j < extensionsList.Size(); ++j)
                    {
                        auto extension = extensionsList.GetStringAt(j);

                        bool isExcluded = false;
                        for (const auto& excluded : exclusions)
                        {
                            if (std::wstring{ extension } == excluded)
                            {
                                isExcluded = true;
                                break;
                            }
                        }

                        if (!isExcluded)
                        {
                            extensions.emplace_back(extension);
                        }
                    }
                }
            }
        }
        catch (...)
        {
        }
    }

    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::preview,
        perUser,
        L"{D8034CFA-F34B-41FE-AD45-62FCBB52A6DA}",
        (fs::path{ installationDir } / LR"d(PowerToys.MonacoPreviewHandlerCpp.dll)d").wstring(),
        L"MonacoPreviewHandler",
        L"Monaco Preview Handler",
        std::move(extensions));
}

registry::ChangeSet getPdfPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::preview,
        perUser,
        L"{A5A41CC7-02CB-41D4-8C9B-9087040D6098}",
        (fs::path{ installationDir } / LR"d(PowerToys.PdfPreviewHandlerCpp.dll)d").wstring(),
        L"PdfPreviewHandler",
        L"Pdf Preview Handler",
        NonLocalizable::ExtPDF);
}

registry::ChangeSet getGcodePreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::preview,
        perUser,
        L"{A0257634-8812-4CE8-AF11-FA69ACAEAFAE}",
        (fs::path{ installationDir } / LR"d(PowerToys.GcodePreviewHandlerCpp.dll)d").wstring(),
        L"GcodePreviewHandler",
        L"G-code Preview Handler",
        NonLocalizable::ExtGCode);
}

registry::ChangeSet getBgcodePreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::preview,
        perUser,
        L"{0e6d5bdd-d5f8-4692-a089-8bb88cdd37f4}",
        (fs::path{ installationDir } / LR"d(PowerToys.BgcodePreviewHandlerCpp.dll)d").wstring(),
        L"BgcodePreviewHandler",
        L"Binary G-code Preview Handler",
        NonLocalizable::ExtBGCode);
}

registry::ChangeSet getQoiPreviewHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::preview,
        perUser,
        L"{729B72CD-B72E-4FE9-BCBF-E954B33FE699}",
        (fs::path{ installationDir } / LR"d(PowerToys.QoiPreviewHandlerCpp.dll)d").wstring(),
        L"QoiPreviewHandler",
        L"Qoi Preview Handler",
        NonLocalizable::ExtQOI);
}

registry::ChangeSet getSvgThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::thumbnail,
        perUser,
        L"{10144713-1526-46C9-88DA-1FB52807A9FF}",
        (fs::path{ installationDir } / LR"d(PowerToys.SvgThumbnailProviderCpp.dll)d").wstring(),
        L"SvgThumbnailProvider",
        L"Svg Thumbnail Provider",
        NonLocalizable::ExtSVG,
        L"image",
        L"Picture");
}

registry::ChangeSet getPdfThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::thumbnail,
        perUser,
        L"{D8BB9942-93BD-412D-87E4-33FAB214DC1A}",
        (fs::path{ installationDir } / LR"d(PowerToys.PdfThumbnailProviderCpp.dll)d").wstring(),
        L"PdfThumbnailProvider",
        L"Pdf Thumbnail Provider",
        NonLocalizable::ExtPDF);
}

registry::ChangeSet getGcodeThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::thumbnail,
        perUser,
        L"{F2847CBE-CD03-4C83-A359-1A8052C1B9D5}",
        (fs::path{ installationDir } / LR"d(PowerToys.GcodeThumbnailProviderCpp.dll)d").wstring(),
        L"GcodeThumbnailProvider",
        L"G-code Thumbnail Provider",
        NonLocalizable::ExtGCode);
}

registry::ChangeSet getBgcodeThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::thumbnail,
        perUser,
        L"{5c93a1e4-99d0-4fb3-991c-6c296a27be21}",
        (fs::path{ installationDir } / LR"d(PowerToys.BgcodeThumbnailProviderCpp.dll)d").wstring(),
        L"BgcodeThumbnailProvider",
        L"Binary G-code Thumbnail Provider",
        NonLocalizable::ExtBGCode);
}

registry::ChangeSet getStlThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::thumbnail,
        perUser,
        L"{77257004-6F25-4521-B602-50ECC6EC62A6}",
        (fs::path{ installationDir } / LR"d(PowerToys.StlThumbnailProviderCpp.dll)d").wstring(),
        L"StlThumbnailProvider",
        L"Stl Thumbnail Provider",
        NonLocalizable::ExtSTL);
}

registry::ChangeSet getQoiThumbnailHandlerChangeSet(const std::wstring& installationDir, bool perUser)
{
    return createPreviewChangeSet(
        registry::shellex::PreviewHandlerType::thumbnail,
        perUser,
        L"{AD856B15-D25E-4008-AFB7-AFAA55586188}",
        (fs::path{ installationDir } / LR"d(PowerToys.QoiThumbnailProviderCpp.dll)d").wstring(),
        L"QoiThumbnailProvider",
        L"Qoi Thumbnail Provider",
        NonLocalizable::ExtQOI,
        L"image",
        L"Picture");
}

registry::ChangeSet getRegistryPreviewSetDefaultAppChangeSet(const std::wstring& installationDir, bool perUser)
{
    const HKEY scope = perUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;

    std::vector<registry::ValueChange> changes;

    std::wstring appName = L"Registry Preview";
    std::wstring fullAppName = L"PowerToys.RegistryPreview";
    std::wstring registryKeyPrefix = L"Software\\Classes\\";

    std::wstring appPath = installationDir + L"\\WinUI3Apps\\PowerToys.RegistryPreview.exe";
    std::wstring command = appPath + L" \"----ms-protocol:ms-encodedlaunch:App?ContractId=Windows.File&Verb=open&File=%1\"";

    changes.push_back({ scope, registryKeyPrefix + fullAppName + L"\\Application", L"ApplicationName", appName });
    changes.push_back({ scope, registryKeyPrefix + fullAppName + L"\\DefaultIcon", std::nullopt, appPath });
    changes.push_back({ scope, registryKeyPrefix + fullAppName + L"\\shell\\open\\command", std::nullopt, command });
    changes.push_back({ scope, registryKeyPrefix + L".reg\\OpenWithProgIDs", fullAppName, L"" });

    return { changes };
}

registry::ChangeSet getRegistryPreviewChangeSet(const std::wstring& installationDir, bool perUser)
{
    const HKEY scope = perUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;

    std::vector<registry::ValueChange> changes;

    std::wstring command = installationDir;
    command.append(L"\\WinUI3Apps\\PowerToys.RegistryPreview.exe \"%1\"");
    changes.push_back({ scope, L"Software\\Classes\\regfile\\shell\\preview\\command", std::nullopt, command });

    std::wstring iconPath = installationDir;
    iconPath.append(L"\\WinUI3Apps\\Assets\\RegistryPreview\\RegistryPreview.ico");
    changes.push_back({ scope, L"Software\\Classes\\regfile\\shell\\preview", L"icon", iconPath });

    return { changes };
}

std::vector<registry::ChangeSet> getAllOnByDefaultModulesChangeSets(const std::wstring& installationDir)
{
    constexpr bool perUser = true;
    return {
        getSvgPreviewHandlerChangeSet(installationDir, perUser),
        getMdPreviewHandlerChangeSet(installationDir, perUser),
        getMonacoPreviewHandlerChangeSet(installationDir, perUser),
        getGcodePreviewHandlerChangeSet(installationDir, perUser),
        getBgcodePreviewHandlerChangeSet(installationDir, perUser),
        getQoiPreviewHandlerChangeSet(installationDir, perUser),
        getSvgThumbnailHandlerChangeSet(installationDir, perUser),
        getGcodeThumbnailHandlerChangeSet(installationDir, perUser),
        getBgcodeThumbnailHandlerChangeSet(installationDir, perUser),
        getStlThumbnailHandlerChangeSet(installationDir, perUser),
        getQoiThumbnailHandlerChangeSet(installationDir, perUser),
        getRegistryPreviewChangeSet(installationDir, perUser)
    };
}

std::vector<registry::ChangeSet> getAllModulesChangeSets(const std::wstring& installationDir)
{
    constexpr bool perUser = true;
    return {
        getSvgPreviewHandlerChangeSet(installationDir, perUser),
        getMdPreviewHandlerChangeSet(installationDir, perUser),
        getMonacoPreviewHandlerChangeSet(installationDir, perUser),
        getPdfPreviewHandlerChangeSet(installationDir, perUser),
        getGcodePreviewHandlerChangeSet(installationDir, perUser),
        getBgcodePreviewHandlerChangeSet(installationDir, perUser),
        getQoiPreviewHandlerChangeSet(installationDir, perUser),
        getSvgThumbnailHandlerChangeSet(installationDir, perUser),
        getPdfThumbnailHandlerChangeSet(installationDir, perUser),
        getGcodeThumbnailHandlerChangeSet(installationDir, perUser),
        getBgcodeThumbnailHandlerChangeSet(installationDir, perUser),
        getStlThumbnailHandlerChangeSet(installationDir, perUser),
        getQoiThumbnailHandlerChangeSet(installationDir, perUser),
        getRegistryPreviewChangeSet(installationDir, perUser),
        getRegistryPreviewSetDefaultAppChangeSet(installationDir, perUser)
    };
}
