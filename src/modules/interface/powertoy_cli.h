#pragma once

#include <string>
#include <vector>
#include <utility>
#include <common/utils/json.h>

namespace pt::cli
{
    struct CommandParameter
    {
        std::wstring name;
        bool required = false;
        std::wstring description;
    };

    struct CommandDescriptor
    {
        std::wstring action;
        std::wstring description;
        std::vector<CommandParameter> parameters;
        bool requiresElevation = false;
        bool longRunning = false;
    };

    struct CommandInvocation
    {
        std::wstring action;
        json::JsonObject args;
    };

    struct CommandResult
    {
        bool ok = false;
        json::JsonObject data;
        std::wstring errorCode;
        std::wstring errorMessage;

        static CommandResult Success(json::JsonObject data = {});
        static CommandResult Error(std::wstring code, std::wstring message);
    };

    inline CommandResult CommandResult::Success(json::JsonObject data)
    {
        CommandResult result;
        result.ok = true;
        result.data = std::move(data);
        return result;
    }

    inline CommandResult CommandResult::Error(std::wstring code, std::wstring message)
    {
        CommandResult result;
        result.ok = false;
        result.errorCode = std::move(code);
        result.errorMessage = std::move(message);
        return result;
    }

    class IModuleCommandProvider
    {
    public:
        virtual ~IModuleCommandProvider() = default;
        virtual std::wstring ModuleKey() const = 0;
        virtual std::vector<CommandDescriptor> DescribeCommands() const = 0;
        virtual CommandResult Execute(const CommandInvocation& invocation) = 0;
    };
}
