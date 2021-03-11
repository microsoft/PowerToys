#pragma once
#include "common/logger/logger.h"

class CallTracer
{
    std::string functionName;
public:
    CallTracer(const char* functionName);
    ~CallTracer();
};
