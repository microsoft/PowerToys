#pragma once

#include <fstream>
#include <filesystem>

#include <shlobj.h>

std::filesystem::path get_desktop_path()
{
    wchar_t* p;
    if (S_OK != SHGetKnownFolderPath(FOLDERID_Desktop, 0, NULL, &p)) return "";

    std::filesystem::path result = p;
    CoTaskMemFree(p);

    return result;
}

class Logger
{
private:
    inline static std::wofstream logger;

public:
    ~Logger()
    {
        logger.close();
    }

    static void init(std::string loggerName)
    {
        std::filesystem::path rootFolder(get_desktop_path());

        auto logsPath = rootFolder;
        logsPath.append(L"monitor_ids.txt");

        logger.open(logsPath.string(), std::ios_base::out | std::ios_base::app);
    }

    template<typename FormatString, typename... Args>
    static void log(FormatString fmt, Args&&... args)
    {
        logger << std::vformat(fmt, std::make_wformat_args(args...)) << std::endl;
    }
};
