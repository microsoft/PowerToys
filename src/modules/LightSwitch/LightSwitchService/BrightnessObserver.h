#pragma once
#include <windows.h>
#include <comdef.h>
#include <wbemidl.h>
#include <functional>
#include <thread>
#include <atomic>
#include <optional>
#include <logger/logger.h>

#pragma comment(lib, "wbemuuid.lib")

// Polls the WMI WmiMonitorBrightness class every few seconds and fires a callback
// when the brightness value changes. Works for laptop/tablet integrated displays
// whose brightness is driven by an ambient light sensor (ALS) or by the user.
class BrightnessObserver
{
public:
    // callback receives the new brightness level (0-100)
    explicit BrightnessObserver(std::function<void(int)> callback, int pollIntervalSeconds = 5)
        : _callback(std::move(callback)), _pollInterval(pollIntervalSeconds), _stop(false)
    {
        _thread = std::thread([this]() { Run(); });
    }

    ~BrightnessObserver()
    {
        Stop();
    }

    void Stop()
    {
        _stop = true;
        if (_thread.joinable())
            _thread.join();
    }

private:
    std::function<void(int)> _callback;
    int _pollInterval;
    std::atomic<bool> _stop;
    std::thread _thread;

    static std::optional<int> QueryCurrentBrightness()
    {
        HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        bool coinitCalledHere = SUCCEEDED(hr); // RPC_E_CHANGED_MODE means already initialized

        IWbemLocator* pLoc = nullptr;
        hr = CoCreateInstance(CLSID_WbemLocator, nullptr, CLSCTX_INPROC_SERVER,
                              IID_IWbemLocator, reinterpret_cast<LPVOID*>(&pLoc));
        if (FAILED(hr))
        {
            if (coinitCalledHere) CoUninitialize();
            return std::nullopt;
        }

        IWbemServices* pSvc = nullptr;
        hr = pLoc->ConnectServer(_bstr_t(L"ROOT\\WMI"), nullptr, nullptr, nullptr,
                                 0, nullptr, nullptr, &pSvc);
        if (FAILED(hr))
        {
            pLoc->Release();
            if (coinitCalledHere) CoUninitialize();
            return std::nullopt;
        }

        hr = CoSetProxyBlanket(pSvc, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, nullptr,
                               RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE,
                               nullptr, EOAC_NONE);
        if (FAILED(hr))
        {
            pSvc->Release();
            pLoc->Release();
            if (coinitCalledHere) CoUninitialize();
            return std::nullopt;
        }

        IEnumWbemClassObject* pEnum = nullptr;
        hr = pSvc->ExecQuery(
            _bstr_t(L"WQL"),
            _bstr_t(L"SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE"),
            WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
            nullptr, &pEnum);

        std::optional<int> result = std::nullopt;

        if (SUCCEEDED(hr) && pEnum)
        {
            IWbemClassObject* pObj = nullptr;
            ULONG returned = 0;
            if (pEnum->Next(WBEM_INFINITE, 1, &pObj, &returned) == WBEM_S_NO_ERROR && returned)
            {
                VARIANT vt;
                VariantInit(&vt);
                if (SUCCEEDED(pObj->Get(L"CurrentBrightness", 0, &vt, nullptr, nullptr)))
                {
                    // CurrentBrightness is VT_UI1 (BYTE)
                    result = static_cast<int>(vt.bVal);
                }
                VariantClear(&vt);
                pObj->Release();
            }
            pEnum->Release();
        }

        pSvc->Release();
        pLoc->Release();
        if (coinitCalledHere) CoUninitialize();
        return result;
    }

    void Run()
    {
        int lastBrightness = -1;

        while (!_stop)
        {
            auto brightness = QueryCurrentBrightness();
            if (brightness.has_value() && brightness.value() != lastBrightness)
            {
                lastBrightness = brightness.value();
                Logger::info(L"[BrightnessObserver] Brightness changed to {}%", lastBrightness);
                try
                {
                    _callback(lastBrightness);
                }
                catch (...) {}
            }

            // Sleep in 1-second increments so we can respond to _stop quickly.
            for (int i = 0; i < _pollInterval && !_stop; ++i)
            {
                std::this_thread::sleep_for(std::chrono::seconds(1));
            }
        }
    }
};
