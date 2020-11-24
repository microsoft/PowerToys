#include "pch.h"
#include "runner-logger.h"

#include <common\settings_helpers.h>
#include <filesystem>

std::shared_ptr<Logger> runner_logger::logger;

void runner_logger::init(std::wstring moduleSaveLocation)
{
    std::filesystem::path logFilePath(moduleSaveLocation);
    logFilePath.append(LogSettings::runnerLogPath);
    logger = std::make_shared<Logger>(LogSettings::runnerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
    logger->info("Runner logger is initialized");
}

std::shared_ptr<Logger> runner_logger::get_logger()
{
    if (!logger)
    {
        throw "Runner logger is not initialized";
    }

    return logger;
}