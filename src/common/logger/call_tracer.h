#pragma once

#include <string>

#include "logger.h"

#define _TRACER_ CallTracer callTracer(__FUNCTION__)

class CallTracer
{
    std::string functionName;
public:
    CallTracer(const char* functionName);
    ~CallTracer();
};
