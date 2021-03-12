#include "pch.h"
#include "CallTracer.h"
#include <thread>

namespace
{
    // Non-localizable
    const std::string entering = " Enter";
    const std::string exiting = " Exit";

    std::mutex indentLevelMutex;
    std::map<std::thread::id, int> indentLevel;

    std::string GetIndentation()
    {
        std::unique_lock lock(indentLevelMutex);
        int level = indentLevel[std::this_thread::get_id()];

        if (level == 0)
        {
            return {};
        }
        else
        {
            return std::string(2 * min(level, 64) - 1, ' ') + " - ";
        }
    }
}

CallTracer::CallTracer(const char* functionName) :
    functionName(functionName)
{  
    Logger::trace((GetIndentation() + functionName + entering).c_str());
}

CallTracer::~CallTracer()
{
    Logger::trace((GetIndentation() + functionName + exiting).c_str());
}
