#include "pch.h"
#include "modulesRegistry.h"

#include <common/utils/json.h>

#include <filesystem>

namespace fs = std::filesystem;

namespace NonLocalizable
{
    const static wchar_t* MONACO_LANGUAGES_FILE_NAME = L"Assets\\Monaco\\monaco_languages.json";
    const static wchar_t* ListID = L"list";
    const static wchar_t* ExtensionsID = L"extensions";
}

registry::ChangeSet getMonacoPreviewHandlerChangeSet(const std::wstring installationDir, const bool perUser)
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
