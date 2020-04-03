#include "pch.h"
#include "update_state.h"

#include <common/json.h>
#include <common/timeutil.h>
#include <common/settings_helpers.h>

namespace
{
    const wchar_t PERSISTENT_STATE_FILENAME[] = L"\\update_state.json";
    const wchar_t UPDATE_STATE_MUTEX[] = L"PTUpdateStateMutex";
}

UpdateState deserialize(const json::JsonObject& json)
{
    UpdateState result;

    result.github_update_last_checked_date = timeutil::from_string(json.GetNamedString(L"github_update_last_checked_date", L"invalid").c_str());
    result.pending_update = json.GetNamedBoolean(L"pending_update", false);

    return result;
}

json::JsonObject serialize(const UpdateState& state)
{
    json::JsonObject json;
    if (state.github_update_last_checked_date.has_value())
    {
        json.SetNamedValue(L"github_update_last_checked_date", json::value(timeutil::to_string(*state.github_update_last_checked_date)));
    }
    json.SetNamedValue(L"pending_update", json::value(state.pending_update));

    return json;
}

UpdateState UpdateState::read()
{
    const auto file_name = PTSettingsHelper::get_root_save_folder_location() + PERSISTENT_STATE_FILENAME;
    std::optional<json::JsonObject> json;
    {
        wil::unique_mutex_nothrow mutex{ CreateMutexW(nullptr, FALSE, UPDATE_STATE_MUTEX) };
        auto lock = mutex.acquire();
        json = json::from_file(file_name);
    }
    return json ? deserialize(*json) : UpdateState{};
}

void UpdateState::store(std::function<void(UpdateState&)> state_modifier)
{
    const auto file_name = PTSettingsHelper::get_root_save_folder_location() + PERSISTENT_STATE_FILENAME;

    std::optional<json::JsonObject> json;
    {
        wil::unique_mutex_nothrow mutex{ CreateMutexW(nullptr, FALSE, UPDATE_STATE_MUTEX) };
        auto lock = mutex.acquire();
        json = json::from_file(file_name);
        UpdateState state;
        if (json)
        {
            state = deserialize(*json);
        }
        state_modifier(state);
        json.emplace(serialize(state));
        json::to_file(file_name, *json);
    }
}
