#include "Logging.h"

#include <iostream>
#include <fstream>
#include <mutex>
#include <iomanip>
#include <chrono>
#include <filesystem>

#include <initguid.h>
#include <mfapi.h>

#pragma warning(disable : 4127)

static std::mutex logMutex;
constexpr inline size_t maxLogSizeMegabytes = 10;
constexpr inline bool alwaysLogVerbose = true;

void LogToFile(std::wstring what, const bool verbose)
{
    std::error_code _;
    const auto tempPath = std::filesystem::temp_directory_path(_);
    if (verbose)
    {
        const bool verboseIndicatorFilePresent = std::filesystem::exists(tempPath / L"PowerToysVideoConferenceVerbose.flag", _);
        if (!alwaysLogVerbose && !verboseIndicatorFilePresent)
        {
            return;
        }
    }
    time_t now = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
    std::tm tm;
    localtime_s(&tm, &now);
    char prefix[64];
    const auto pid = GetCurrentProcessId();
    const auto iter = prefix + sprintf_s(prefix, "[%ld]", pid);
    std::strftime(iter, sizeof(prefix) - (prefix - iter), "[%d.%m %H:%M:%S] ", &tm);

    std::lock_guard lock{ logMutex };
    std::wstring logFilePath = tempPath;
#if defined(_WIN64)
    logFilePath += L"\\PowerToysVideoConference_x64.log";
#elif defined(_WIN32)
    logFilePath += L"\\PowerToysVideoConference_x86.log";
#endif
    size_t logSizeMBs = 0;
    try
    {
        logSizeMBs = static_cast<size_t>(std::filesystem::file_size(logFilePath) >> 20);
    }
    catch (...)
    {
    }
    if (logSizeMBs > maxLogSizeMegabytes)
    {
        std::error_code __;
        // Truncate the log file to zero
        std::filesystem::resize_file(logFilePath, 0, __);
    }
    std::wofstream myfile;
    myfile.open(logFilePath, std::fstream::app);

    static const auto newLaunch = [&] {
        myfile << prefix << "\n\n<<<NEW SESSION>>";
        return 0;
    }();

    myfile << prefix << what << "\n";
    myfile.close();
}

void LogToFile(std::string what, const bool verbose)
{
    std::wstring native{ begin(what), end(what) };
    LogToFile(std::move(native), verbose);
}

std::string toMediaTypeString(GUID subtype)
{
    if (subtype == MFVideoFormat_YUY2)
        return "MFVideoFormat_YUY2";
    else if (subtype == MFVideoFormat_RGB32)
        return "MFVideoFormat_RGB32";
    else if (subtype == MFVideoFormat_RGB24)
        return "MFVideoFormat_RGB24";
    else if (subtype == MFVideoFormat_ARGB32)
        return "MFVideoFormat_ARGB32";
    else if (subtype == MFVideoFormat_RGB555)
        return "MFVideoFormat_RGB555";
    else if (subtype == MFVideoFormat_RGB565)
        return "MFVideoFormat_RGB565";
    else if (subtype == MFVideoFormat_RGB8)
        return "MFVideoFormat_RGB8";
    else if (subtype == MFVideoFormat_L8)
        return "MFVideoFormat_L8";
    else if (subtype == MFVideoFormat_L16)
        return "MFVideoFormat_L16";
    else if (subtype == MFVideoFormat_D16)
        return "MFVideoFormat_D16";
    else if (subtype == MFVideoFormat_AYUV)
        return "MFVideoFormat_AYUV";
    else if (subtype == MFVideoFormat_YVYU)
        return "MFVideoFormat_YVYU";
    else if (subtype == MFVideoFormat_YVU9)
        return "MFVideoFormat_YVU9";
    else if (subtype == MFVideoFormat_UYVY)
        return "MFVideoFormat_UYVY";
    else if (subtype == MFVideoFormat_NV11)
        return "MFVideoFormat_NV11";
    else if (subtype == MFVideoFormat_NV12)
        return "MFVideoFormat_NV12";
    else if (subtype == MFVideoFormat_YV12)
        return "MFVideoFormat_YV12";
    else if (subtype == MFVideoFormat_I420)
        return "MFVideoFormat_I420";
    else if (subtype == MFVideoFormat_IYUV)
        return "MFVideoFormat_IYUV";
    else if (subtype == MFVideoFormat_Y210)
        return "MFVideoFormat_Y210";
    else if (subtype == MFVideoFormat_Y216)
        return "MFVideoFormat_Y216";
    else if (subtype == MFVideoFormat_Y410)
        return "MFVideoFormat_Y410";
    else if (subtype == MFVideoFormat_Y416)
        return "MFVideoFormat_Y416";
    else if (subtype == MFVideoFormat_Y41P)
        return "MFVideoFormat_Y41P";
    else if (subtype == MFVideoFormat_Y41T)
        return "MFVideoFormat_Y41T";
    else if (subtype == MFVideoFormat_Y42T)
        return "MFVideoFormat_Y42T";
    else if (subtype == MFVideoFormat_P210)
        return "MFVideoFormat_P210";
    else if (subtype == MFVideoFormat_P216)
        return "MFVideoFormat_P216";
    else if (subtype == MFVideoFormat_P010)
        return "MFVideoFormat_P010";
    else if (subtype == MFVideoFormat_P016)
        return "MFVideoFormat_P016";
    else if (subtype == MFVideoFormat_v210)
        return "MFVideoFormat_v210";
    else if (subtype == MFVideoFormat_v216)
        return "MFVideoFormat_v216";
    else if (subtype == MFVideoFormat_v410)
        return "MFVideoFormat_v410";
    else if (subtype == MFVideoFormat_MP43)
        return "MFVideoFormat_MP43";
    else if (subtype == MFVideoFormat_MP4S)
        return "MFVideoFormat_MP4S";
    else if (subtype == MFVideoFormat_M4S2)
        return "MFVideoFormat_M4S2";
    else if (subtype == MFVideoFormat_MP4V)
        return "MFVideoFormat_MP4V";
    else if (subtype == MFVideoFormat_WMV1)
        return "MFVideoFormat_WMV1";
    else if (subtype == MFVideoFormat_WMV2)
        return "MFVideoFormat_WMV2";
    else if (subtype == MFVideoFormat_WMV3)
        return "MFVideoFormat_WMV3";
    else if (subtype == MFVideoFormat_WVC1)
        return "MFVideoFormat_WVC1";
    else if (subtype == MFVideoFormat_MSS1)
        return "MFVideoFormat_MSS1";
    else if (subtype == MFVideoFormat_MSS2)
        return "MFVideoFormat_MSS2";
    else if (subtype == MFVideoFormat_MPG1)
        return "MFVideoFormat_MPG1";
    else if (subtype == MFVideoFormat_DVSL)
        return "MFVideoFormat_DVSL";
    else if (subtype == MFVideoFormat_DVSD)
        return "MFVideoFormat_DVSD";
    else if (subtype == MFVideoFormat_DVHD)
        return "MFVideoFormat_DVHD";
    else if (subtype == MFVideoFormat_DV25)
        return "MFVideoFormat_DV25";
    else if (subtype == MFVideoFormat_DV50)
        return "MFVideoFormat_DV50";
    else if (subtype == MFVideoFormat_DVH1)
        return "MFVideoFormat_DVH1";
    else if (subtype == MFVideoFormat_DVC)
        return "MFVideoFormat_DVC";
    else if (subtype == MFVideoFormat_H264)
        return "MFVideoFormat_H264";
    else if (subtype == MFVideoFormat_H265)
        return "MFVideoFormat_H265";
    else if (subtype == MFVideoFormat_MJPG)
        return "MFVideoFormat_MJPG";
    else if (subtype == MFVideoFormat_420O)
        return "MFVideoFormat_420O";
    else if (subtype == MFVideoFormat_HEVC)
        return "MFVideoFormat_HEVC";
    else if (subtype == MFVideoFormat_HEVC_ES)
        return "MFVideoFormat_HEVC_ES";
    else if (subtype == MFVideoFormat_VP80)
        return "MFVideoFormat_VP80";
    else if (subtype == MFVideoFormat_VP90)
        return "MFVideoFormat_VP90";
    else if (subtype == MFVideoFormat_ORAW)
        return "MFVideoFormat_ORAW";
    else
        return "Other VideoFormat";
}