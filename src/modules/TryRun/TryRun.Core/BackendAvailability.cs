// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record BackendAvailability(bool WindowsAvailable, bool LinuxAvailable, string WindowsDetail, string LinuxDetail, string NativeVersion);
