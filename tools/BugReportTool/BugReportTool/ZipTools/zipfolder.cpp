#include "ZipFolder.h"
#include "..\..\..\..\deps\cziplib\src\zip.h"
#include <common/utils/timeutil.h>

void ZipFolder(std::filesystem::path zipPath, std::filesystem::path folderPath)
{
    std::string reportFilename{ "PowerToysReport_" };
    reportFilename += timeutil::format_as_local("%F-%H-%M-%S", timeutil::now());
    reportFilename += ".zip";

    auto tmpZipPath = std::filesystem::temp_directory_path();
    tmpZipPath /= reportFilename;

    struct zip_t* zip = zip_open(tmpZipPath.string().c_str(), ZIP_DEFAULT_COMPRESSION_LEVEL, 'w');
    if (!zip)
    {
        printf("Can not open zip.");
        throw -1;
    }

    using recursive_directory_iterator = std::filesystem::recursive_directory_iterator;
    const size_t rootSize = folderPath.wstring().size();
    for (const auto& dirEntry : recursive_directory_iterator(folderPath))
    {
        if (dirEntry.is_regular_file())
        {
            auto path = dirEntry.path().string();
            auto relativePath = path.substr(rootSize, path.size());
            zip_entry_open(zip, relativePath.c_str());
            zip_entry_fwrite(zip, path.c_str());
            zip_entry_close(zip);
        }
    }

    zip_close(zip);

    std::error_code err;
    std::filesystem::copy(tmpZipPath, zipPath, err);
    if (err.value() != 0)
    {
        wprintf_s(L"Failed to copy %s. Error code: %d\n", tmpZipPath.c_str(), err.value());
    }

    err = {};
    std::filesystem::remove_all(tmpZipPath, err);
    if (err.value() != 0)
    {
        wprintf_s(L"Failed to delete %s. Error code: %d\n", tmpZipPath.c_str(), err.value());
    }
}