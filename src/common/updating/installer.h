#pragma once

#include <string>
#include <optional>

#include <winrt/Windows.Foundation.h>
#include <common/version/helper.h>

namespace updating
{
    winrt::Windows::Foundation::IAsyncOperation<bool> uninstall_previous_msix_version_async();
}