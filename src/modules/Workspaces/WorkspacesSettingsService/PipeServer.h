// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>
#include <atomic>

namespace PTSettingsSvc
{
    // Runs the named-pipe loop until `stopEvent` is signalled.
    // Returns 0 on a clean stop, non-zero on a fatal error.
    DWORD RunPipeServer(HANDLE stopEvent);
}
