#pragma once
#include <common/logger/logger.h>

class FancyZonesLogger
{
    static std::shared_ptr<Logger> logger;
public:
    static void Init(std::wstring moduleSaveLocation);
    static std::shared_ptr<Logger> GetLogger();
};