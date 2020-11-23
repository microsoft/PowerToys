#include "pch.h"
#include <common\settings_helpers.h>
#include <filesystem>
#include "FancyZonesLogger.h"

std::shared_ptr<Logger> FancyZonesLogger::logger;

void FancyZonesLogger::Init(std::wstring moduleSaveLocation)
{
    std::filesystem::path logFilePath(moduleSaveLocation);
    logFilePath.append(LogSettings::fancyZonesLogPath);
    logger = std::make_shared<Logger>(LogSettings::fancyZonesLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
    logger->info("FancyZones logger initialized");
}

std::shared_ptr<Logger> FancyZonesLogger::GetLogger()
{
    if (!logger)
    {
        throw "Fancy zones logger is not constructed";
    }

    return logger;
}
