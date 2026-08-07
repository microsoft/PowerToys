// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "Bindings.h"

#include <cwctype>

namespace PTSettingsSvc
{
    namespace
    {
        // The one place in the service where module-specific knowledge lives.
        // Each row: { exe basename, namespace id, file name }.
        //
        // The on-disk file keeps its original, human-readable name (e.g.
        // workspaces.json) rather than an opaque "blob.bin": the service still
        // treats the bytes as opaque (it never parses them), but a real name
        // aids diagnostics and lets native direct-readers (the Launcher hot
        // path) open the same file by the name they already use.
        //
        // Workspaces ships five executables; all operate on the same namespace
        // ("Workspaces") / file and so share one store.  The runner
        // (PowerToys.exe) is bound to the same namespace so it can perform the
        // one-shot legacy migration during startup.
        //
        // To add a new module:
        //   1. Add a row for each of its executables here (with its file name).
        //   2. Point that module's read/write code at PTSettingsClient.
        //   No service code changes required.
        constexpr CallerBinding kBindings[] = {
            { L"PowerToys.WorkspacesEditor.exe",         L"Workspaces", L"workspaces.json" },
            { L"PowerToys.WorkspacesLauncher.exe",       L"Workspaces", L"workspaces.json" },
            { L"PowerToys.WorkspacesSnapshotTool.exe",   L"Workspaces", L"workspaces.json" },
            { L"PowerToys.WorkspacesWindowArranger.exe", L"Workspaces", L"workspaces.json" },
            { L"PowerToys.WorkspacesLauncherUI.exe",     L"Workspaces", L"workspaces.json" },

            // Runner can act on behalf of any module that needs runner-owned
            // one-shot tasks (e.g. legacy migration).  v6.0 ships with one
            // such module so the runner gets exactly one row.
            { L"PowerToys.exe",                          L"Workspaces", L"workspaces.json" },
        };

        bool ICaseEquals(const wchar_t* a, const wchar_t* b)
        {
            while (*a && *b)
            {
                if (std::towlower(*a) != std::towlower(*b)) return false;
                ++a; ++b;
            }
            return *a == 0 && *b == 0;
        }
    }

    const CallerBinding* FindBindingByExeBasename(const std::wstring& basename)
    {
        for (const auto& row : kBindings)
        {
            if (ICaseEquals(basename.c_str(), row.exeBasename))
            {
                return &row;
            }
        }
        return nullptr;
    }

    bool IsValidNamespaceId(const wchar_t* id)
    {
        if (!id || !*id) return false;
        size_t len = 0;
        for (const wchar_t* p = id; *p; ++p, ++len)
        {
            if (len >= 64) return false;
            wchar_t c = *p;
            bool ok = (c >= L'A' && c <= L'Z') ||
                      (c >= L'a' && c <= L'z') ||
                      (c >= L'0' && c <= L'9') ||
                      c == L'_' || c == L'-' || c == L'.';
            if (!ok) return false;
        }
        return len > 0;
    }
}
