#pragma once

#include <ctime>
#include <optional>
#include <functional>

// All fields must be default-initialized
struct UpdateState
{
    std::optional<std::time_t> github_update_last_checked_date;
    bool pending_update = false;

    // To prevent concurrent modification of the file, we enforce this interface, which locks the file while
    // the state_modifier is active.
    static void store(std::function<void(UpdateState&)> state_modifier);
    static UpdateState read();
};