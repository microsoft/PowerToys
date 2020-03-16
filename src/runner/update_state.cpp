#include "pch.h"
#include "update_state.h"

#include <common/json.h>
#include <common/timeutil.h>
#include <common/settings_helpers.h>

namespace
{
    const wchar_t PERSISTENT_STATE_FILENAME[] = L"\\update_state.json";
}

UpdateState UpdateState::load()
{
    const auto file_name = PTSettingsHelper::get_root_save_folder_location() + PERSISTENT_STATE_FILENAME;
    auto json = json::from_file(file_name);
    UpdateState state;

    if (!json)
    {
        return state;
    }

    state.github_update_last_checked_date = timeutil::from_string(json->GetNamedString(L"github_update_last_checked_date", L"invalid").c_str());

    return state;
}

void UpdateState::save()
{
    json::JsonObject json;
    if (github_update_last_checked_date.has_value())
    {
        json.SetNamedValue(L"github_update_last_checked_date", json::value(timeutil::to_string(*github_update_last_checked_date)));
    }
    const auto file_name = PTSettingsHelper::get_root_save_folder_location() + PERSISTENT_STATE_FILENAME;
    json::to_file(file_name, json);
}
