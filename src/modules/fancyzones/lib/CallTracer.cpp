#include "pch.h"
#include "CallTracer.h"

namespace
{
    // Non-localizable
    const std::string entering = " Enter";
    const std::string exiting = " Exit";
}

CallTracer::CallTracer(const char* functionName) :
    functionName(functionName)
{
    Logger::trace((functionName + entering).c_str());
}

CallTracer::~CallTracer()
{
    Logger::trace((functionName + exiting).c_str());
}
