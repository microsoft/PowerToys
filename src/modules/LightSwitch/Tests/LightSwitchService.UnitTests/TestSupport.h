// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <roapi.h>
#include <winrt/Windows.Data.Json.h>

namespace LightSwitchServiceUnitTests
{
    class Apartment
    {
    public:
        Apartment() : m_result(RoInitialize(RO_INIT_MULTITHREADED))
        {
            if (m_result != RPC_E_CHANGED_MODE)
            {
                winrt::check_hresult(m_result);
            }
        }
        ~Apartment()
        {
            if (SUCCEEDED(m_result))
            {
                RoUninitialize();
            }
        }

    private:
        HRESULT m_result;
    };

    inline winrt::Windows::Data::Json::JsonObject TestState()
    {
        using namespace winrt::Windows::Data::Json;
        JsonObject state;
        state.SetNamedValue(L"systemTheme", JsonValue::CreateStringValue(L"light"));
        state.SetNamedValue(L"appsTheme", JsonValue::CreateStringValue(L"unknown"));
        state.SetNamedValue(L"changeSystem", JsonValue::CreateBooleanValue(true));
        state.SetNamedValue(L"changeApps", JsonValue::CreateBooleanValue(false));
        state.SetNamedValue(L"scheduleMode", JsonValue::CreateStringValue(L"Off"));
        state.SetNamedValue(L"manualOverride", JsonValue::CreateBooleanValue(false));
        return state;
    }
}
