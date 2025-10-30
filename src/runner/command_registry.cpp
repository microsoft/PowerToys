#include "pch.h"
#include "command_registry.h"

#include <common/logger/logger.h>
#include <common/utils/elevation.h>

#include <algorithm>
#include <cwctype>

CommandRegistry& CommandRegistry::instance()
{
    static CommandRegistry registry;
    return registry;
}

void CommandRegistry::register_module(PowertoyModuleIface* module)
{
    if (!module)
    {
        return;
    }

    auto provider = module->command_provider();
    if (!provider)
    {
        return;
    }

    std::wstring moduleKey = provider->ModuleKey();
    if (moduleKey.empty())
    {
        moduleKey = module->get_key();
    }

    auto descriptors = provider->DescribeCommands();
    if (descriptors.empty())
    {
        return;
    }

    Entry entry{};
    entry.provider = provider;
    entry.moduleKey = moduleKey;

    for (const auto& descriptor : descriptors)
    {
        auto normalizedAction = normalize_key(descriptor.action);
        entry.descriptorsByAction.emplace(normalizedAction, descriptor);
    }

    std::unique_lock guard{ mutex_ };
    entries_[normalize_key(moduleKey)] = std::move(entry);
}

pt::cli::CommandResult CommandRegistry::execute(const std::wstring& moduleKey, const pt::cli::CommandInvocation& invocation)
{
    std::shared_lock guard{ mutex_ };

    auto moduleIt = entries_.find(normalize_key(moduleKey));
    if (moduleIt == entries_.end())
    {
        return pt::cli::CommandResult::Error(L"E_MODULE_NOT_FOUND", L"Module not registered for CLI use.");
    }

    auto& entry = moduleIt->second;

    auto descriptorIt = entry.descriptorsByAction.find(normalize_key(invocation.action));
    if (descriptorIt == entry.descriptorsByAction.end())
    {
        return pt::cli::CommandResult::Error(L"E_COMMAND_NOT_FOUND", L"Command not available for this module.");
    }

    const auto& descriptor = descriptorIt->second;
    if (descriptor.requiresElevation && !is_process_elevated())
    {
        return pt::cli::CommandResult::Error(L"E_NEEDS_ELEVATION", L"This command requires elevation.");
    }

    auto provider = entry.provider;
    guard.unlock();

    try
    {
        return provider->Execute(invocation);
    }
    catch (const std::exception& ex)
    {
        Logger::error(L"CLI command execution failed: {}", winrt::to_hstring(ex.what()));
        return pt::cli::CommandResult::Error(L"E_INTERNAL", L"Command execution failed due to an internal error.");
    }
    catch (...)
    {
        Logger::error(L"CLI command execution failed due to an unknown exception.");
        return pt::cli::CommandResult::Error(L"E_INTERNAL", L"Command execution failed due to an unknown error.");
    }
}

std::vector<CommandModuleReflection> CommandRegistry::snapshot() const
{
    std::shared_lock guard{ mutex_ };
    std::vector<CommandModuleReflection> result;
    result.reserve(entries_.size());

    for (const auto& [normalizedKey, entry] : entries_)
    {
        CommandModuleReflection reflection;
        reflection.moduleKey = entry.moduleKey;
        for (const auto& [actionKey, descriptor] : entry.descriptorsByAction)
        {
            reflection.commands.push_back(descriptor);
        }
        result.push_back(std::move(reflection));
    }

    return result;
}

std::optional<CommandModuleReflection> CommandRegistry::snapshot(const std::wstring& moduleKey) const
{
    std::shared_lock guard{ mutex_ };
    auto it = entries_.find(normalize_key(moduleKey));
    if (it == entries_.end())
    {
        return std::nullopt;
    }

    CommandModuleReflection reflection;
    reflection.moduleKey = it->second.moduleKey;
    for (const auto& [actionKey, descriptor] : it->second.descriptorsByAction)
    {
        reflection.commands.push_back(descriptor);
    }

    return reflection;
}

std::wstring CommandRegistry::normalize_key(const std::wstring& value)
{
    std::wstring normalized = value;
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), [](wchar_t ch) {
        return static_cast<wchar_t>(std::towlower(ch));
    });
    return normalized;
}
