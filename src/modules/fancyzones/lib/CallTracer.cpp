#include "pch.h"
#include "CallTracer.h"

namespace
{
    // Non-localizable
    const std::string entering = "Entering: ";
    const std::string exiting = "Exiting: ";
}

CallTracer::CallTracer(const char* functionName) :
    functionName(functionName)
{
    Logger::trace((entering + functionName).c_str());
}

CallTracer::~CallTracer()
{
    Logger::trace((exiting + functionName).c_str());
}
