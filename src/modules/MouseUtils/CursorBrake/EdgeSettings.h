// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CursorBrakeEdges.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Data.Json.h>

#include <optional>
#include <vector>

// Reads the `edges` setting from the module's settings JSON. A value the module cannot use falls
// back rather than failing, entry by entry, so one bad entry in a hand-edited file leaves the
// rest in effect.
namespace cursorbrake
{
    // Bounds on what a hand-edited file can make the hook thread resolve.
    constexpr size_t MAX_EDGE_ENTRIES = 256;
    constexpr size_t MAX_MONITOR_ID_LENGTH = 512;

    // The overrides in a list of `edges` entries, in order. Only the first MAX_EDGE_ENTRIES
    // elements are read. An element is skipped when it is not an object, when `monitors` is not
    // two distinct non-empty strings of at most MAX_MONITOR_ID_LENGTH characters, when `mode` is
    // not 0, 1 or 2, when `one_way` is present and not a bool, or when `push_distance` is present
    // and not a finite number. A missing `one_way` means both ways and a missing `push_distance`
    // the default; the distance is clamped like the global one.
    std::vector<EdgeOverride> ParseEdgeOverrides(const winrt::Windows::Data::Json::JsonArray& entries);

    // The overrides in the settings' `properties` object: none when it has no `edges` property,
    // and no result when the property is not an object whose `value` is a list, in which case the
    // caller keeps the overrides it has.
    std::optional<std::vector<EdgeOverride>> ReadEdgesSetting(const winrt::Windows::Data::Json::JsonObject& properties);
}
