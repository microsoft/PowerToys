#pragma once

#include <modules/interface/powertoy_module_interface.h>
#include <modules/interface/powertoy_cli.h>
#include <common/utils/json.h>

#include <unordered_map>
#include <shared_mutex>
#include <vector>
#include <optional>

struct CommandModuleReflection
{
    std::wstring moduleKey;
    std::vector<pt::cli::CommandDescriptor> commands;
};

class CommandRegistry
{
public:
    static CommandRegistry& instance();

    void register_module(PowertoyModuleIface* module);

    pt::cli::CommandResult execute(const std::wstring& moduleKey, const pt::cli::CommandInvocation& invocation);

    std::vector<CommandModuleReflection> snapshot() const;
    std::optional<CommandModuleReflection> snapshot(const std::wstring& moduleKey) const;

private:
    struct Entry
    {
        pt::cli::IModuleCommandProvider* provider = nullptr;
        std::wstring moduleKey;
        std::unordered_map<std::wstring, pt::cli::CommandDescriptor> descriptorsByAction;
    };

    static std::wstring normalize_key(const std::wstring& value);

    mutable std::shared_mutex mutex_;
    std::unordered_map<std::wstring, Entry> entries_;
};
