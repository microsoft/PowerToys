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

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "Running detect begin BA function. fCached=%d, registrationType=%d, cPackages=%u, fCancel=%d", fCached, registrationType, cPackages, *pfCancel);

        //-------------------------------------------------------------------------------------------------
        // YOUR CODE GOES HERE
        BalExitOnFailure(hr, "Change this message to represent real error handling.");
        //-------------------------------------------------------------------------------------------------

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

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "Running plan begin BA function. cPackages=%u, fCancel=%d", cPackages, *pfCancel);

        //-------------------------------------------------------------------------------------------------
        // YOUR CODE GOES HERE
        BalExitOnFailure(hr, "Change this message to represent real error handling.");
        //-------------------------------------------------------------------------------------------------

    LExit:
        return hr;
    }

    virtual STDMETHODIMP OnExecuteFilesInUse(
        __in_z LPCWSTR wzPackageId,
        __in DWORD cFiles,
        __in_ecount_z(cFiles) LPCWSTR* rgwzFiles,
        __in int nRecommendation,
        __inout int* pResult
        )
    {
        HRESULT hr = S_OK;

        BalLog(BOOTSTRAPPER_LOG_LEVEL_STANDARD, "Running OnExecuteFilesInUse BA function. packageId=%ls, cFiles=%u, recommendation=%d", wzPackageId, cFiles, nRecommendation);

        // Always return IDIGNORE to silently ignore files in use
        *pResult = IDIGNORE;

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
    }

    //
    // Destructor - release member variables.
    //
    ~CSilentFilesInUseBAFunctions()
    {
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

    pBAFunctions = new CSilentFilesInUseBAFunctions(hModule);
    ExitOnNull(pBAFunctions, hr, E_OUTOFMEMORY, "Failed to create new CSilentFilesInUseBAFunctions object.");

    hr = pBAFunctions->OnCreate(pArgs->pEngine, pArgs->pCommand);
    ExitOnFailure(hr, "Failed to call OnCreate CPrereqBaf.");

    pResults->pfnBAFunctionsProc = BalBaseBAFunctionsProc;
    pResults->pvBAFunctionsProcContext = pBAFunctions;
    pBAFunctions = NULL;

LExit:
    ReleaseObject(pBAFunctions);

    return hr;
}
