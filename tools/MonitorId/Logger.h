#pragma once

#include <fstream>
#include <filesystem>
#include <shlobj.h>

#include <Windows.h>

inline std::optional<std::wstring> get_last_error_message(const DWORD dw)
{
    std::optional<std::wstring> message;
    try
    {
        const auto msg = std::system_category().message(dw);
        message.emplace(begin(msg), end(msg));
    }
    catch (...)
    {
    }
    return message;
}

inline std::wstring get_last_error_or_default(const DWORD dw)
{
    auto message = get_last_error_message(dw);
    return message.has_value() ? message.value() : L"";
}

std::filesystem::path get_desktop_path()
{
    wchar_t* p;
    if (S_OK != SHGetKnownFolderPath(FOLDERID_Desktop, 0, NULL, &p)) 
        return "";

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
        logsPath.append(L"window_styles.txt");

        logger.open(logsPath.string(), std::ios_base::out | std::ios_base::app);
    }

    template<typename FormatString, typename... Args>
    static void log(FormatString fmt, Args&&... args)
    {
        logger << std::vformat(fmt, std::make_wformat_args(args...)) << std::endl;
    }
};

