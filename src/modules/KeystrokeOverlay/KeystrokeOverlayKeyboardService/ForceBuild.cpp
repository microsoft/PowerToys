// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// ============================================================================
// This file exists solely to ensure that the KeystrokeOverlayKeyboardService
// project runs its MSBuild pipeline. Without at least one .cpp file that is
// compiled, StaticLibrary projects do not trigger AfterTargets="Build", and
// custom build steps (such as copying KeystrokeServer.exe) will not run.
// ============================================================================

#include "pch.h"

void ForceBuild()
{
    // Intentionally empty.
    // Do not remove this file.
}
