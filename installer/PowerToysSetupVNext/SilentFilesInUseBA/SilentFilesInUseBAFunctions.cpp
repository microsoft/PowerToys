// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

#include "precomp.h"
#include "BalBaseBAFunctions.h"
#include "BalBaseBAFunctionsProc.h"

class CSilentFilesInUseBAFunctions : public CBalBaseBAFunctions
{
public: // IBootstrapperApplication
    virtual STDMETHODIMP OnDetectBegin(
        __in BOOL fCached,
        __in BOOTSTRAPPER_REGISTRATION_TYPE registrationType,
        __in DWORD cPackages,
        __inout BOOL* pfCancel
        )
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION SYSTEM ACTIVE *** Running detect begin BA function. fCached=%d, registrationType=%d, cPackages=%u, fCancel=%d", fCached, registrationType, cPackages, *pfCancel);

    LExit:
        return hr;
    }

public: // IBAFunctions
    virtual STDMETHODIMP OnPlanBegin(
        __in DWORD cPackages,
        __inout BOOL* pfCancel
        )
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION SYSTEM ACTIVE *** Running plan begin BA function. cPackages=%u, fCancel=%d", cPackages, *pfCancel);

        //-------------------------------------------------------------------------------------------------
        // YOUR CODE GOES HERE
        // BalExitOnFailure(hr, "Change this message to represent real error handling.");
        //-------------------------------------------------------------------------------------------------

    LExit:
        return hr;
    }

    virtual STDMETHODIMP OnExecuteBegin(
        __in DWORD cExecutingPackages,
        __inout BOOL* pfCancel
        )
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
        __in BOOTSTRAPPER_FILES_IN_USE_TYPE source,
        __inout int* pResult
        )
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
        __in HRESULT hrStatus,
        __inout BOOL* pfCancel
        )
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CUSTOM BA FUNCTION SYSTEM ACTIVE *** Running execute complete BA function. hrStatus=0x%x, fCancel=%d", hrStatus, *pfCancel);

        return hr;
    }

public:
    //
    // Constructor - initialize member variables.
    //
    CSilentFilesInUseBAFunctions(
        __in HMODULE hModule
        ) : CBalBaseBAFunctions(hModule)
    {
        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** BA FUNCTION CONSTRUCTOR *** CSilentFilesInUseBAFunctions created");
    }

    //
    // Destructor - release member variables.
    //
    ~CSilentFilesInUseBAFunctions()
    {
        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** BA FUNCTION DESTRUCTOR *** CSilentFilesInUseBAFunctions destroyed");
    }
};


HRESULT WINAPI CreateBAFunctions(
    __in HMODULE hModule,
    __in const BA_FUNCTIONS_CREATE_ARGS* pArgs,
    __inout BA_FUNCTIONS_CREATE_RESULTS* pResults
    )
{
    HRESULT hr = S_OK;
    CSilentFilesInUseBAFunctions* pBAFunctions = NULL;

    // First thing - log that we're being called
    BalInitialize(pArgs->pEngine);
    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS CALLED *** BA Function DLL is being loaded!");

    pBAFunctions = new CSilentFilesInUseBAFunctions(hModule);
    ExitOnNull(pBAFunctions, hr, E_OUTOFMEMORY, "Failed to create new CSilentFilesInUseBAFunctions object.");

    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS *** Created CSilentFilesInUseBAFunctions object");

    hr = pBAFunctions->OnCreate(pArgs->pEngine, pArgs->pCommand);
    ExitOnFailure(hr, "Failed to call OnCreate CPrereqBaf.");

    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS *** OnCreate completed successfully");

    pResults->pfnBAFunctionsProc = BalBaseBAFunctionsProc;
    pResults->pvBAFunctionsProcContext = pBAFunctions;
    pBAFunctions = NULL;

    BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "*** CREATEBAFUNCTIONS SUCCESS *** BA Function system initialized");

LExit:
    if (FAILED(hr))
    {
        BalLog(BOOTSTRAPPER_LOG_LEVEL_ERROR, "*** CREATEBAFUNCTIONS FAILED *** hr=0x%x", hr);
    }
    ReleaseObject(pBAFunctions);

    return hr;
}
