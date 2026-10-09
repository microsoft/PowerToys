// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

// Minimal subset of PowerRename Helpers used by NewPlus
// This is a copy from PowerRename's main branch to avoid cross-module dependencies
HRESULT GetDatedFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source, SYSTEMTIME fileTime);
