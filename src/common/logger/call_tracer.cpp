#include "pch.h"
#include "call_tracer.h"

#include <map>
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

        if (level <= 0)
        {
            return {};
        }
        else
        {
            return std::string(static_cast<int64_t>(2) * min(level, 64) - 1, ' ') + " - ";
        }
    }

    void Indent()
    {
        std::unique_lock lock(indentLevelMutex);
        indentLevel[std::this_thread::get_id()]++;
    }

    void Unindent()
    {
        std::unique_lock lock(indentLevelMutex);
        indentLevel[std::this_thread::get_id()]--;
    }
}

CallTracer::CallTracer(const char* functionName) :
    functionName(functionName)
{
    Logger::trace((GetIndentation() + functionName + entering).c_str());
    Indent();
}

CallTracer::~CallTracer()
{
    Unindent();
    Logger::trace((GetIndentation() + functionName + exiting).c_str());
}
