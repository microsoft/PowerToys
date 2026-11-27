// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CursorBrakeFullScreen.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Data.Json.h>

#include <optional>
#include <string>
#include <string_view>
#include <vector>

// Reads the full-screen settings from the module's settings JSON. A value the module cannot use
// gives no result, and the caller keeps the value in effect.
namespace cursorbrake
{
    // The `fullscreen_behavior` choice in the settings' `properties` object. No result when the
    // property is missing, is not an object whose `value` is a number, or holds anything but 0, 1
    // or 2.
    std::optional<FullScreenBehavior> ReadFullScreenBehavior(const winrt::Windows::Data::Json::JsonObject& properties);

    // The excluded apps in the text of the setting: one per line, either line ending, with
    // surrounding whitespace, blank lines and lines of only path separators dropped, uppercased for
    // the case-blind match that check_excluded_app makes against an uppercased process path.
    std::vector<std::wstring> ParseExcludedApps(std::wstring_view text);

    // The excluded apps in the settings' `properties` object. No result when the property is
    // missing or is not an object whose `value` is a string.
    std::optional<std::vector<std::wstring>> ReadExcludedApps(const winrt::Windows::Data::Json::JsonObject& properties);
}
