#pragma once

#include <string>
#include <optional>
#include <future>

#include <common/version/helper.h>

namespace updating
{
    std::future<bool> uninstall_previous_msix_version_async();

    bool is_1809_or_older();
}