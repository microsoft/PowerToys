// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>
#include <atomic>

namespace WorkspacesSvc
{
    // Runs the named-pipe loop until `stopEvent` is signalled or
    // `kIdleShutdownSeconds` elapses without any client connecting.
    // Returns 0 on a clean stop, non-zero on a fatal error.
    DWORD RunPipeServer(HANDLE stopEvent);
}
