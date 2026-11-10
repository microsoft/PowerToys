// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

#pragma once

#include "pch.h"
#include "BalBaseBAFunctions.h"
#include <common/updating/protectedStorageUpdate.h>
#include <functional>
#include <utility>

class CSilentFilesInUseBAFunctions : public CBalBaseBAFunctions
{
public: // IBootstrapperApplication
    virtual STDMETHODIMP OnCreate(IBootstrapperEngine* engine, BOOTSTRAPPER_COMMAND* command) override
    {
        m_relatedBundle = command->relationType != BOOTSTRAPPER_RELATION_NONE;
        return CBalBaseBAFunctions::OnCreate(engine, command);
    }

    virtual STDMETHODIMP OnDetectBegin(
        __in BOOL fCached,
        __in BOOTSTRAPPER_REGISTRATION_TYPE registrationType,
        __in DWORD cPackages,
        __inout BOOL* pfCancel
        ) override
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION SYSTEM ACTIVE *** Running detect begin BA function. fCached=%d, registrationType=%d, cPackages=%u, fCancel=%d", fCached, registrationType, cPackages, *pfCancel);

        return hr;
    }

public: // IBAFunctions
    virtual STDMETHODIMP OnPlanBegin(
        __in DWORD cPackages,
        __inout BOOL* pfCancel
        ) override
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION SYSTEM ACTIVE *** Running plan begin BA function. cPackages=%u, fCancel=%d", cPackages, *pfCancel);

        m_removeOwner = false;
        return hr;
    }

    virtual STDMETHODIMP OnPlanComplete(
        __in HRESULT hrStatus
        ) override
    {
        m_removeOwner = false;
        if (FAILED(hrStatus))
        {
            BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "Owner cleanup not armed: planning failed (0x%08x).", hrStatus);
            return S_OK;
        }

        // WiX publishes WixBundleAction after OnPlanBegin.
        LONGLONG perUser = 0;
        LONGLONG action = 0;
        const HRESULT perUserResult = m_pEngine->GetVariableNumeric(L"ProtectedStoragePerUserBundle", &perUser);
        const HRESULT actionResult = m_pEngine->GetVariableNumeric(L"WixBundleAction", &action);
        m_removeOwner = SUCCEEDED(perUserResult) && SUCCEEDED(actionResult) &&
                        updating::ShouldRemoveOwnerCarrier(perUser == 1, action == BOOTSTRAPPER_ACTION_UNINSTALL, m_relatedBundle, true);
        BalLog(FAILED(perUserResult) || FAILED(actionResult) ? BOOTSTRAPPER_LOG_LEVEL_ERROR : BOOTSTRAPPER_LOG_LEVEL_STANDARD,
               "Owner cleanup plan complete: perUser=%lld (0x%08x), action=%lld (0x%08x), related=%d, armed=%d.",
               perUser, perUserResult, action, actionResult, m_relatedBundle, m_removeOwner);

        return S_OK;
    }

    virtual STDMETHODIMP OnExecuteBegin(
        __in DWORD cExecutingPackages,
        __inout BOOL* pfCancel
        ) override
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION SYSTEM ACTIVE *** Running execute begin BA function. cExecutingPackages=%u, fCancel=%d", cExecutingPackages, *pfCancel);

        return hr;
    }

    virtual STDMETHODIMP OnExecuteFilesInUse(
        __in_z LPCWSTR wzPackageId,
        __in DWORD cFiles,
        __in_ecount_z(cFiles) LPCWSTR* rgwzFiles,
        __in int nRecommendation,
        __in BOOTSTRAPPER_FILES_IN_USE_TYPE /* source */,
        __inout int* pResult
        ) override
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION CALLED *** Running OnExecuteFilesInUse BA function. packageId=%ls, cFiles=%u, recommendation=%d", wzPackageId, cFiles, nRecommendation);

        // Log each file that's in use
        for (DWORD i = 0; i < cFiles; i++)
        {
            BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** FILE IN USE [%u]: %ls", i, rgwzFiles[i]);
        }

    /*
     * Summary: Why we return IDIGNORE here
     *
     * - Goal: Keep behavior consistent with our previous WiX 3 installer to avoid "files in use / close apps" prompts and preserve silent installs (e.g., winget).
     * - WiX 5 change: We can no longer suppress that dialog the same way. Combined with winget adding /silent, this BAFunction returns IDIGNORE to continue without prompts.
     * - Main trigger: Win10-style context menu uses registry + DLL; Explorer/dllhost.exe (COM Surrogate) often holds locks. Killing them is disruptive; this is a pragmatic trade-off.
     * - Trade-off: Some file replacements may defer until reboot (PendingFileRename), but installation remains non-interruptive.
     * - Full fix: Rewrite a custom Bootstrapper Application if we need complete control over prompts and behavior.
     * - Note: Even with this handler, a full-UI install (e.g., double-clicking the installer) can still show a FilesInUse dialog; this primarily targets silent installs.
     */
        *pResult = IDIGNORE;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** BA FUNCTION RETURNING IDIGNORE - SILENTLY CONTINUING ***");

        return hr;
    }

    virtual STDMETHODIMP OnExecuteComplete(
        __in HRESULT hrStatus
        ) override
    {
        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION SYSTEM ACTIVE *** Running execute complete BA function. hrStatus=0x%x, ownerCleanup=%d, attempted=%d", hrStatus, m_removeOwner, m_removeAttempted);

        if (m_removeOwner && SUCCEEDED(hrStatus) && !m_removeAttempted)
        {
            m_removeAttempted = true;
            m_removeOwnerCallback();
        }

        return S_OK;
    }

    CSilentFilesInUseBAFunctions(
        __in HMODULE hModule,
        std::function<void()> removeOwner
        ) : CBalBaseBAFunctions(hModule), m_removeOwnerCallback(std::move(removeOwner))
    {
        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** BA FUNCTION CONSTRUCTOR *** CSilentFilesInUseBAFunctions created");
    }

    ~CSilentFilesInUseBAFunctions()
    {
        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** BA FUNCTION DESTRUCTOR *** CSilentFilesInUseBAFunctions destroyed");
    }

private:
    std::function<void()> m_removeOwnerCallback;
    bool m_relatedBundle = false;
    bool m_removeOwner = false;
    bool m_removeAttempted = false;
};
