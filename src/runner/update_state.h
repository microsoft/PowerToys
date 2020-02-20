#pragma once

#include <ctime>
#include <optional>

struct UpdateState
{
    std::optional<std::time_t> github_update_last_checked_date;

    static UpdateState load();
    void save();
};