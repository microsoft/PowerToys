// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

// Non-localizable names the module's files share.
namespace cursorbrake
{
    // The module's key: the name the runner and Settings know it by, and its settings folder.
    inline constexpr wchar_t MODULE_NAME[] = L"CursorBrake";

    // The member of each settings property that holds its value.
    inline constexpr wchar_t JSON_KEY_VALUE[] = L"value";
}
