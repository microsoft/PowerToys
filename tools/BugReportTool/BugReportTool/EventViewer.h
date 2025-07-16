#pragma once
#include <filesystem>

namespace EventViewer
{
    void ReportEventViewerInfo(const std::filesystem::path& tmpDir);
    void ReportAppXDeploymentLogs(const std::filesystem::path& tmpDir);
}
