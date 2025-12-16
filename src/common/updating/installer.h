#pragma once

#include <string>
#include <optional>
#include <future>

#include <common/version/helper.h>

namespace updating
{
    winrt::Windows::Foundation::IAsyncOperation<bool> uninstall_previous_msix_version_async();
}