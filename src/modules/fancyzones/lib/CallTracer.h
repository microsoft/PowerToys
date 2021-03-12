#pragma once

#include "common/logger/logger.h"

#define _TRACER_ CallTracer callTracer(__FUNCTION__)

class CallTracer
{
    std::string functionName;
public:
    CallTracer(const char* functionName);
    ~CallTracer();
};
