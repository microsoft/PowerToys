
This is a sample project showing how to create a BA function assembly.

The four interfaces are in the WixSampleBAFunctions.cpp file.


Example code:
~~~~~~~~~~~~~


        HRESULT hr = S_OK;
        HKEY hkKey = NULL;
        LPWSTR sczValue = NULL;
        LPWSTR sczFormatedValue = NULL;


        //---------------------------------------------------------------------------------------------
        // Example of BA function failure
        hr = E_NOTIMPL;
        BalExitOnFailure(hr, "Test failure.");
        //---------------------------------------------------------------------------------------------

        //---------------------------------------------------------------------------------------------
        // Example of setting a variables
        hr = m_pEngine->SetVariableString(L"Variable1", L"String value");
        BalExitOnFailure(hr, "Failed to set variable.");
        hr = m_pEngine->SetVariableNumeric(L"Variable2", 1234);
        BalExitOnFailure(hr, "Failed to set variable.");
        //---------------------------------------------------------------------------------------------

        //---------------------------------------------------------------------------------------------
        // Example of reading burn variable.
        BalGetStringVariable(L"WixBundleName", &sczValue);
        BalExitOnFailure(hr, "Failed to get variable.");

        hr = m_pEngine->SetVariableString(L"Variable4", sczValue);
        BalExitOnFailure(hr, "Failed to set variable.");
        //---------------------------------------------------------------------------------------------

        ReleaseNullStr(sczValue); // Release string so it can be re-used

        //---------------------------------------------------------------------------------------------
        // Examples of reading burn variable and formatting it.
        BalGetStringVariable(L"InstallFolder", &sczValue);
        BalExitOnFailure(hr, "Failed to get variable.");

        hr = m_pEngine->SetVariableString(L"Variable5", sczValue);
        BalExitOnFailure(hr, "Failed to set variable.");

        BalFormatString(sczValue, &sczFormatedValue);
        BalExitOnFailure(hr, "Failed to format variable.");

        hr = m_pEngine->SetVariableString(L"Variable6", sczFormatedValue);
        BalExitOnFailure(hr, "Failed to set variable.");
        //---------------------------------------------------------------------------------------------

        ReleaseNullStr(sczValue); // Release string so it can be re-used

        //---------------------------------------------------------------------------------------------
        // Example of reading 64 bit registry and setting the InstallFolder variable to the value read.
        hr = RegOpen(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v3.5", KEY_READ | KEY_WOW64_64KEY, &hkKey);
        BalExitOnFailure(hr, "Failed to open registry key.");

        hr = RegReadString(hkKey, L"InstallPath", &sczValue);
        BalExitOnFailure(hr, "Failed to read registry value.");

        // Example of function call
        PathBackslashTerminate(&sczValue);

        hr = m_pEngine->SetVariableString(L"InstallFolder", sczValue);
        BalExitOnFailure(hr, "Failed to set variable.");
        //---------------------------------------------------------------------------------------------

        ReleaseNullStr(sczValue); // Release string so it can be re-used

        //---------------------------------------------------------------------------------------------
        // Example of calling a function that return HRESULT
        hr = GetFileVersion();
        BalExitOnFailure(hr, "Failed to get version.");
        //---------------------------------------------------------------------------------------------

    LExit:
        ReleaseRegKey(hkKey);
        ReleaseStr(sczValue);
        ReleaseStr(sczFormatedValue);
