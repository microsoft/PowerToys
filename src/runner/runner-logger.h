#pragma once
#include <common/logger/logger.h>

class runner_logger
{
    static std::shared_ptr<Logger> logger;

public:
    static void init(std::wstring module_save_location);
    static std::shared_ptr<Logger> get_logger();
};