#pragma once

#include <filesystem>
#include <optional>

namespace fs = std::filesystem;
namespace updating
{
    bool dotnet_is_installed(const size_t major, const size_t minor, const size_t requiredMinimalPatch);
    std::optional<fs::path> download_dotnet(const wchar_t* dotnetDesktopDownloadLink);
    bool install_dotnet(fs::path dotnet_download_path, const bool silent);
}