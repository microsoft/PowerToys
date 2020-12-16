#include "zipfolder.h"
#include "..\..\..\..\deps\cziplib\src\zip.h"

void zipFolder(std::filesystem::path zipPath, std::filesystem::path folderPath)
{
    struct zip_t* zip = zip_open(zipPath.string().c_str(), ZIP_DEFAULT_COMPRESSION_LEVEL, 'w');
    using recursive_directory_iterator = std::filesystem::recursive_directory_iterator;
    int rootSize = folderPath.wstring().size();
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