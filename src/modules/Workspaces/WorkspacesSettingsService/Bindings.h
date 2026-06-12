// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Caller-to-namespace binding table for PTSettingsSvc.
//
// The service is intentionally namespace-agnostic at the storage layer —
// every PutBlob / GetBlob touches `<DataRoot>\<namespaceId>\<userSid>\blob.bin`.
// The only place the service knows anything module-specific is this
// table: which executable basenames are allowed to talk to it, and which
// namespace each one operates on.
//
// Adding a new PowerToys module to the protection scheme is a one-line
// change here (plus pointing that module's read/write code at PTSettingsClient).

#pragma once

#include <string>

namespace PTSettingsSvc
{
    struct CallerBinding
    {
        const wchar_t* exeBasename;   // case-insensitive compare
        const wchar_t* namespaceId;   // subfolder under <DataRoot>
    };

    // Pointer into a static, immutable table.  Lifetime is the lifetime of
    // the service process.  Do not free.  Returns nullptr if the basename
    // isn't allow-listed.
    const CallerBinding* FindBindingByExeBasename(const std::wstring& basename);

    // Returns true if `id` looks like a syntactically valid namespace id —
    // ASCII alphanumeric / underscore / hyphen / dot, no path separators,
    // length 1..64.  Defensive check used before turning the id into a
    // directory name.
    bool IsValidNamespaceId(const wchar_t* id);
}
