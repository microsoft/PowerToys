#include "ZipFolder.h"
#include "..\..\..\..\deps\cziplib\src\zip.h"

void ZipFolder(std::filesystem::path zipPath, std::filesystem::path folderPath)
{
    struct zip_t* zip = zip_open(zipPath.string().c_str(), ZIP_DEFAULT_COMPRESSION_LEVEL, 'w');
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
}