#pragma once

#include <filesystem>
#include <optional>

namespace fs = std::filesystem;
namespace updating
{
    bool dotnet_is_installed();
    std::optional<fs::path> download_dotnet();
    bool install_dotnet(fs::path dotnet_download_path, const bool silent);
}