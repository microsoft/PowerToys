#pragma once

#include <ctime>
#include <optional>
#include <functional>

// All fields must be default-initialized
struct UpdateState
{
    enum State
    {
        upToDate = 0,
        errorDownloading = 1,
        readyToDownload = 2,
        readyToInstall = 3,
        networkError = 4
    } state = upToDate;
    std::wstring releasePageUrl;
    std::optional<std::time_t> githubUpdateLastCheckedDate;
    std::wstring downloadedInstallerFilename;

    // To prevent concurrent modification of the file, we enforce this interface, which locks the file while
    // the state_modifier is active.
    static void store(std::function<void(UpdateState&)> stateModifier);
    static UpdateState read();
};