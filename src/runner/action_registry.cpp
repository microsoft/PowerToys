#include "pch.h"
#include "action_registry.h"

#include "powertoy_module.h"

#include <common/logger/logger.h>

namespace
{
    constexpr wchar_t ACTION_ID_PROPERTY[] = L"action_id";
    constexpr wchar_t MODULE_KEY_PROPERTY[] = L"module_key";
    constexpr wchar_t AVAILABLE_PROPERTY[] = L"available";
}

PowerToysActionRegistry& PowerToysActionRegistry::Instance()
{
    static PowerToysActionRegistry instance;
    return instance;
}

json::JsonObject PowerToysActionRegistry::ErrorResult(const std::wstring& error_code, const std::wstring& message)
{
    json::JsonObject result;
    result.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(false));
    result.SetNamedValue(L"error_code", json::value(error_code));
    result.SetNamedValue(L"message", json::value(message));
    return result;
}

void PowerToysActionRegistry::RefreshLocked()
{
    actions.clear();
    duplicate_action_ids.clear();

    for (const auto& [module_key, module] : modules())
    {
        json::JsonArray descriptors;

        try
        {
            descriptors = module.json_actions();
        }
        catch (...)
        {
            Logger::error(L"PowerToysActionRegistry: malformed actions from module {}", module_key);
            continue;
        }

        for (const auto& value : descriptors)
        {
            if (value.ValueType() != json::JsonValueType::Object)
            {
                Logger::warn(L"PowerToysActionRegistry: ignoring non-object action descriptor from module {}", module_key);
                continue;
            }

            auto descriptor = value.GetObjectW();
            const auto action_id_hstring = descriptor.GetNamedString(ACTION_ID_PROPERTY, L"");
            const std::wstring action_id = action_id_hstring.c_str();
            if (action_id.empty())
            {
                Logger::warn(L"PowerToysActionRegistry: ignoring action without action_id from module {}", module_key);
                continue;
            }

            if (actions.contains(action_id))
            {
                actions.erase(action_id);
                duplicate_action_ids.insert(action_id);
                Logger::error(L"PowerToysActionRegistry: duplicate action_id {} detected", action_id);
                continue;
            }

            if (duplicate_action_ids.contains(action_id))
            {
                continue;
            }

            actions.emplace(action_id, RegisteredAction{
                .module_key = module_key,
                .descriptor = descriptor,
            });
        }
    }
}

json::JsonArray PowerToysActionRegistry::ListActions()
{
    std::scoped_lock lock{ mutex };
    RefreshLocked();

    json::JsonArray result;
    for (const auto& [action_id, registered_action] : actions)
    {
        auto descriptor = registered_action.descriptor;
        descriptor.SetNamedValue(ACTION_ID_PROPERTY, json::value(action_id));
        descriptor.SetNamedValue(MODULE_KEY_PROPERTY, json::value(registered_action.module_key));

        const auto module_it = modules().find(registered_action.module_key);
        const bool is_available = module_it != modules().end() && module_it->second->is_enabled();
        descriptor.SetNamedValue(AVAILABLE_PROPERTY, json::JsonValue::CreateBooleanValue(is_available));
        result.Append(descriptor);
    }

    return result;
}

json::JsonObject PowerToysActionRegistry::InvokeAction(const std::wstring& action_id, const std::wstring& serialized_args)
{
    std::scoped_lock lock{ mutex };
    RefreshLocked();

    if (duplicate_action_ids.contains(action_id))
    {
        return ErrorResult(L"duplicate_action_id", L"Multiple modules registered the same action identifier.");
    }

    const auto action_it = actions.find(action_id);
    if (action_it == actions.end())
    {
        return ErrorResult(L"action_not_found", L"The requested PowerToys action is not registered.");
    }

    const auto module_it = modules().find(action_it->second.module_key);
    if (module_it == modules().end())
    {
        return ErrorResult(L"module_not_found", L"The module that owns this action is not loaded.");
    }

    if (!module_it->second->is_enabled())
    {
        return ErrorResult(L"module_unavailable", L"The module that owns this action is currently disabled.");
    }

    std::wstring raw_result;
    try
    {
        raw_result = module_it->second.invoke_action(action_id, serialized_args);
    }
    catch (...)
    {
        return ErrorResult(L"invoke_failed", L"The module threw while invoking the requested action.");
    }

    if (raw_result.empty())
    {
        return ErrorResult(L"empty_result", L"The module returned an empty result.");
    }

    json::JsonObject result;
    if (!json::JsonObject::TryParse(raw_result, result))
    {
        return ErrorResult(L"invalid_result", L"The module returned malformed action result JSON.");
    }

    if (!result.HasKey(L"success"))
    {
        result.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(true));
    }

    return result;
}
