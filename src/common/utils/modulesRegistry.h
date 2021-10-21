#pragma once

#include "registry.h"

#include <filesystem>

namespace fs = std::filesystem;

inline registry::Changeset getSvgPreviewHandlerChangset(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{ddee2b8a-6807-48a6-bb20-2338174ff779}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } /
                                   LR"d(modules\FileExplorerPreview\SvgPreviewHandler.comhost.dll)d")
                                      .wstring(),
                                  registry::DOTNET_COMPONENT_CATEGORY_CLSID,
                                  L"Microsoft.PowerToys.PreviewHandler.Svg.SvgPreviewHandler",
                                  L"Svg Preview Handler",
                                  L".svg");
}

inline registry::Changeset getMdPreviewHandlerChangset(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(modules\FileExplorerPreview\MarkdownPreviewHandler.comhost.dll)d").wstring(),
                                  registry::DOTNET_COMPONENT_CATEGORY_CLSID,
                                  L"Microsoft.PowerToys.PreviewHandler.Markdown.MarkdownPreviewHandler",
                                  L"Markdown Preview Handler",
                                  L".md");
}

inline registry::Changeset getPdfPreviewHandlerChangset(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::preview,
                                  perUser,
                                  L"{07665729-6243-4746-95b7-79579308d1b2}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(modules\FileExplorerPreview\PdfPreviewHandler.comhost.dll)d").wstring(),
                                  registry::DOTNET_COMPONENT_CATEGORY_CLSID,
                                  L"Microsoft.PowerToys.PreviewHandler.Pdf.PdfPreviewHandler",
                                  L"Pdf Preview Handler",
                                  L".pdf");
}

inline registry::Changeset getSvgThumbnailHandlerChangset(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::thumbnail,
                                  perUser,
                                  L"{36B27788-A8BB-4698-A756-DF9F11F64F84}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(modules\FileExplorerPreview\SvgThumbnailProvider.comhost.dll)d").wstring(),
                                  registry::DOTNET_COMPONENT_CATEGORY_CLSID,
                                  L"Microsoft.PowerToys.ThumbnailHandler.Svg.SvgThumbnailProvider",
                                  L"Svg Thumbnail Provider",
                                  L".svg");
}

inline registry::Changeset getPdfThumbnailHandlerChangset(const std::wstring installationDir, const bool perUser)
{
    using namespace registry::shellex;
    return generatePreviewHandler(PreviewHandlerType::thumbnail,
                                  perUser,
                                  L"{BCC13D15-9720-4CC4-8371-EA74A274741E}",
                                  get_std_product_version(),
                                  (fs::path{ installationDir } / LR"d(modules\FileExplorerPreview\PdfThumbnailProvider.comhost.dll)d").wstring(),
                                  registry::DOTNET_COMPONENT_CATEGORY_CLSID,
                                  L"Microsoft.PowerToys.ThumbnailHandler.Pdf.PdfThumbnailProvider",
                                  L"Pdf Thumbnail Provider",
                                  L".pdf");
}

inline std::vector<registry::Changeset> getAllModulesChangesets(const std::wstring installationDir, const bool perUser)
{
    return { getSvgPreviewHandlerChangset(installationDir, perUser),
             getMdPreviewHandlerChangset(installationDir, perUser),
             getPdfPreviewHandlerChangset(installationDir, perUser),
             getSvgThumbnailHandlerChangset(installationDir, perUser),
             getPdfThumbnailHandlerChangset(installationDir, perUser) };
}