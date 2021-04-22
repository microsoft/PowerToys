#include "pch.h"
#include "update_state.h"

#include <common/utils/json.h>
#include <common/utils/timeutil.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace
{
    const wchar_t PERSISTENT_STATE_FILENAME[] = L"\\UpdateState.json";
    const wchar_t UPDATE_STATE_MUTEX[] = L"Local\\PowerToysRunnerUpdateStateMutex";
}

UpdateState deserialize(const json::JsonObject& json)
{
    UpdateState result;

    result.state = static_cast<UpdateState::State>(json.GetNamedNumber(L"state", UpdateState::upToDate));
    result.releasePageUrl = json.GetNamedString(L"releasePageUrl", L"");
    result.githubUpdateLastCheckedDate = timeutil::from_string(json.GetNamedString(L"githubUpdateLastCheckedDate", L"invalid").c_str());
    result.downloadedInstallerFilename = json.GetNamedString(L"downloadedInstallerFilename", L"");
    return result;
}

json::JsonObject serialize(const UpdateState& state)
{
    json::JsonObject json;

    if (state.githubUpdateLastCheckedDate.has_value())
    {
        json.SetNamedValue(L"githubUpdateLastCheckedDate", json::value(timeutil::to_string(*state.githubUpdateLastCheckedDate)));
    }
    json.SetNamedValue(L"releasePageUrl", json::value(state.releasePageUrl));
    json.SetNamedValue(L"state", json::value(static_cast<double>(state.state)));
    json.SetNamedValue(L"downloadedInstallerFilename", json::value(state.downloadedInstallerFilename));

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
